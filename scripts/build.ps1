<#
.SYNOPSIS
    Construit FolderHue : build, tests, publication NativeAOT et assemblage du paquet.

.DESCRIPTION
    Enchaine les etapes suivantes :
      1. verification de coherence des identites (CLSID, packageName, publisher, applicationId) ;
      2. build de la solution et execution des tests unitaires de Core ;
      3. publication de FolderHue.App ;
      4. publication de FolderHue.Shell en NativeAOT ;
      5. assemblage de artifacts\package (DLL + app + manifeste + logos) ;
      6. pre-generation de la palette d'icones ;
      7. optionnellement, production d'un MSIX complet signable.

    L'etape 1 n'est pas cosmetique : un ecart d'un seul caractere entre le manifeste embarque
    dans la DLL et AppxManifest.xml fait que l'Explorateur ignore l'extension, sans le moindre
    message d'erreur (CLAUDE.md §10).

.PARAMETER Configuration
    Configuration de build. Release par defaut.

.PARAMETER SkipTests
    N'execute pas les tests unitaires. A eviter : CLAUDE.md §8 demande de lancer dotnet test
    avant tout commit touchant Core.

.PARAMETER FullPackage
    Produit en plus artifacts\FolderHue.msix, le paquet complet destine a la distribution
    publique et au Microsoft Store.

.PARAMETER CertificateThumbprint
    Empreinte d'un certificat du magasin personnel avec lequel signer le MSIX complet.
    Sans ce parametre, le paquet est produit mais laisse non signe.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $SkipTests,
    [switch] $FullPackage,
    [string] $CertificateThumbprint
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repository 'artifacts'
$layout = Join-Path $artifacts 'package'
$runtime = 'win-x64'

function Write-Step { param([string] $Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Ok   { param([string] $Message) Write-Host "    OK   $Message" -ForegroundColor Green }

# --- Outils ----------------------------------------------------------------

function Initialize-BuildTools {
    <#
        NativeAOT lie l'image finale avec link.exe. ILCompiler localise le toolset via
        vswhere.exe, et link.exe a besoin de mt.exe pour embarquer le manifeste : ni l'un ni
        l'autre n'est sur le PATH par defaut.
    #>
    $installer = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
    if (-not (Test-Path (Join-Path $installer 'vswhere.exe'))) {
        throw "vswhere.exe est introuvable. Executez d'abord .\scripts\setup-prereqs.ps1."
    }

    $sdkRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    $sdkBin = Get-ChildItem $sdkRoot -Directory -Filter '10.*' -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'x64' } |
        Where-Object { Test-Path (Join-Path $_ 'makeappx.exe') } |
        Select-Object -First 1

    if (-not $sdkBin) {
        throw "Le Windows SDK est introuvable. Executez d'abord .\scripts\setup-prereqs.ps1."
    }

    $env:PATH = "$installer;$sdkBin;$env:PATH"
    Write-Ok "toolchain : $sdkBin"
    return $sdkBin
}

# --- 1. Coherence des identites --------------------------------------------

function Assert-IdentityConsistency {
    Write-Step 'Coherence des identites (CLSID et identite MSIX)'

    $guidsFile = Join-Path $repository 'src\FolderHue.Shell\Com\Guids.cs'
    $dllManifest = Join-Path $repository 'src\FolderHue.Shell\FolderHue.Shell.manifest'
    $appxManifest = Join-Path $repository 'src\FolderHue.Package\AppxManifest.xml'

    foreach ($file in @($guidsFile, $dllManifest, $appxManifest)) {
        if (-not (Test-Path $file)) { throw "Fichier introuvable : $file" }
    }

    # CLSID declare dans le code
    $guidsText = Get-Content $guidsFile -Raw
    if ($guidsText -notmatch 'RootCommandClsidText\s*=\s*"([0-9A-Fa-f-]{36})"') {
        throw "Impossible de lire RootCommandClsidText dans $guidsFile."
    }
    $clsid = $Matches[1]

    # Identite declaree dans le manifeste embarque de la DLL
    [xml] $dll = Get-Content $dllManifest -Raw
    $msix = $dll.assembly.msix
    if (-not $msix) { throw "Le bloc <msix> est absent de $dllManifest." }

    # Identite declaree dans le paquet
    [xml] $appx = Get-Content $appxManifest -Raw
    $identity = $appx.Package.Identity
    $application = $appx.Package.Applications.Application

    $checks = @(
        @{ Name = 'packageName';   Dll = $msix.packageName;   Appx = $identity.Name }
        @{ Name = 'publisher';     Dll = $msix.publisher;     Appx = $identity.Publisher }
        @{ Name = 'applicationId'; Dll = $msix.applicationId; Appx = $application.Id }
    )

    foreach ($check in $checks) {
        if ($check.Dll -cne $check.Appx) {
            throw ("Divergence d'identite sur « {0} » : la DLL declare « {1} », le paquet « {2} ». " +
                   'Sans correspondance exacte, Explorer ignore silencieusement l''extension.') -f `
                   $check.Name, $check.Dll, $check.Appx
        }
    }

    if ($guidsText -notmatch 'ClassicMenuClsidText\s*=\s*"([0-9A-Fa-f-]{36})"') {
        throw "Impossible de lire ClassicMenuClsidText dans $guidsFile."
    }
    $classicClsid = $Matches[1]

    $appxText = Get-Content $appxManifest -Raw

    # La commande moderne doit apparaitre comme classe COM et comme verbe desktop5.
    foreach ($attribute in @('Id', 'Clsid')) {
        if ($appxText -notmatch "$attribute=`"$([regex]::Escape($clsid))`"") {
            throw "Le CLSID $clsid n'apparait pas en tant que $attribute dans AppxManifest.xml."
        }
    }

    # Le handler herite doit apparaitre comme classe COM et comme ExtensionHandler desktop9.
    foreach ($attribute in @('Id', 'Clsid')) {
        if ($appxText -notmatch "$attribute=`"$([regex]::Escape($classicClsid))`"") {
            throw "Le CLSID herite $classicClsid n'apparait pas en tant que $attribute dans AppxManifest.xml."
        }
    }

    # Un verbe desktop4 n'est rendu que par le menu moderne de Windows 11 : sans l'extension
    # heritee, l'entree est invisible pour qui a restaure l'ancien menu.
    if ($appxText -notmatch 'windows\.fileExplorerClassicContextMenuHandler') {
        throw ("L'extension windows.fileExplorerClassicContextMenuHandler est absente du manifeste : " +
               "l'entree n'apparaitra pas dans le menu contextuel classique.")
    }

    Write-Ok "CLSID $clsid (moderne) et $classicClsid (herite)"
    Write-Ok "identite $($identity.Name) / $($identity.Publisher) / $($application.Id)"
}

# --- Etapes de build -------------------------------------------------------

function Invoke-App {
    <#
        FolderHue.App est une application WinExe : l'operateur d'appel rend la main
        immediatement sans attendre sa fin, et $LASTEXITCODE ne veut alors rien dire. Il faut
        passer par Start-Process -Wait.
    #>
    param([string[]] $Arguments, [string] $Description)

    $executable = Join-Path $layout 'FolderHue.App.exe'
    $process = Start-Process -FilePath $executable -ArgumentList $Arguments -Wait -PassThru -NoNewWindow

    if ($process.ExitCode -ne 0) {
        throw "$Description a echoue (code $($process.ExitCode))."
    }
}

function Invoke-Dotnet {
    param([string[]] $Arguments, [string] $Description)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description a echoue (code $LASTEXITCODE)."
    }
}

function Build-Solution {
    Write-Step "Build ($Configuration)"
    Invoke-Dotnet @('build', (Join-Path $repository 'FolderHue.sln'), '-c', $Configuration, '--nologo') 'Le build'
    Write-Ok 'solution compilee'
}

function Test-Core {
    if ($SkipTests) {
        Write-Step 'Tests unitaires ignores (-SkipTests)'
        return
    }

    Write-Step 'Tests unitaires de Core'
    Invoke-Dotnet @(
        'test',
        (Join-Path $repository 'tests\FolderHue.Core.Tests'),
        '-c', $Configuration, '--nologo', '--no-build'
    ) 'Les tests'
    Write-Ok 'tests au vert'
}

function Publish-App {
    Write-Step 'Publication de FolderHue.App'
    Invoke-Dotnet @(
        'publish',
        (Join-Path $repository 'src\FolderHue.App'),
        '-c', $Configuration, '-r', $runtime, '--self-contained', 'false', '--nologo'
    ) 'La publication de l''application'
    Write-Ok 'application publiee'
}

function Publish-Shell {
    Write-Step 'Publication de FolderHue.Shell (NativeAOT)'
    Invoke-Dotnet @(
        'publish',
        (Join-Path $repository 'src\FolderHue.Shell'),
        '-c', $Configuration, '-r', $runtime, '/p:PublishAot=true', '--nologo'
    ) 'La publication NativeAOT'
    Write-Ok 'DLL native produite'
}

function Assert-ShellManifestEmbedded {
    param([string] $SdkBin, [string] $DllPath)

    Write-Step 'Verification du manifeste embarque dans la DLL'

    $extracted = Join-Path $artifacts 'embedded.manifest'
    & (Join-Path $SdkBin 'mt.exe') -nologo "-inputresource:$DllPath;#2" "-out:$extracted" | Out-Null

    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $extracted)) {
        throw ('La DLL ne contient pas de manifeste d''identite MSIX. ' +
               'Sans lui, Explorer ne charge pas l''extension et n''affiche aucune erreur.')
    }

    [xml] $embedded = Get-Content $extracted -Raw
    if (-not $embedded.assembly.msix) {
        throw "Le manifeste embarque ne contient pas de bloc <msix>."
    }

    Remove-Item $extracted -Force
    Write-Ok "identite embarquee : $($embedded.assembly.msix.packageName)"
}

function Build-Layout {
    param([string] $ShellPublish, [string] $AppPublish)

    Write-Step 'Assemblage de artifacts\package'

    # Un paquet sparse enregistre sur ce dossier verrouille FolderHue.Shell.dll : tant qu'il
    # est enregistre, la DLL est intouchable, meme Explorateur ferme. On le retire d'abord ;
    # install-dev.ps1 le remettra.
    [xml] $appx = Get-Content (Join-Path $repository 'src\FolderHue.Package\AppxManifest.xml') -Raw
    $registered = Get-AppxPackage -Name $appx.Package.Identity.Name -ErrorAction SilentlyContinue
    if ($registered) {
        $registered | Remove-AppxPackage
        Write-Ok 'paquet precedent desenregistre'
    }

    # On vide le dossier au lieu de le supprimer : un terminal ou un Explorateur positionne
    # dessus suffit a rendre la suppression du dossier lui-meme impossible.
    New-Item -ItemType Directory -Path $layout -Force | Out-Null
    Get-ChildItem $layout -Force | Remove-Item -Recurse -Force

    Copy-Item (Join-Path $ShellPublish 'FolderHue.Shell.dll') $layout

    # L'application et ses dependances gerees, sans les symboles ni la documentation XML.
    Get-ChildItem $AppPublish -File |
        Where-Object { $_.Extension -notin @('.pdb', '.xml') } |
        Copy-Item -Destination $layout

    foreach ($culture in @('fr', 'en')) {
        $satellite = Join-Path $AppPublish $culture
        if (Test-Path $satellite) {
            Copy-Item $satellite (Join-Path $layout $culture) -Recurse -Force
        }
    }

    Copy-Item (Join-Path $repository 'src\FolderHue.Package\AppxManifest.xml') $layout

    $assets = Join-Path $layout 'Assets'
    New-Item -ItemType Directory -Path $assets -Force | Out-Null

    Invoke-App @('--generate-package-assets', $assets) 'La generation des logos du paquet'

    Write-Ok "paquet assemble dans $layout"
}

function Initialize-IconLibrary {
    Write-Step 'Pre-generation de la palette d''icones'

    Invoke-App @('--pregenerate') 'La pre-generation des icones'

    Write-Ok 'palette prete'
}

function Build-FullPackage {
    param([string] $SdkBin)

    Write-Step 'MSIX complet'

    # AllowExternalContent n'a de sens que pour le paquet sparse de developpement.
    $staging = Join-Path $artifacts 'msix-staging'
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Get-ChildItem $staging -Force | Remove-Item -Recurse -Force
    # Copie entree par entree : Copy-Item avec un joker et -Recurse aplatit les sous-dossiers,
    # et le paquet se retrouverait sans son dossier Assets.
    Get-ChildItem $layout -Force | ForEach-Object {
        Copy-Item $_.FullName -Destination $staging -Recurse -Force
    }

    $manifestPath = Join-Path $staging 'AppxManifest.xml'
    [xml] $manifest = Get-Content $manifestPath -Raw
    $external = $manifest.Package.Properties.ChildNodes |
        Where-Object { $_.LocalName -eq 'AllowExternalContent' }

    if ($external) {
        $manifest.Package.Properties.RemoveChild($external) | Out-Null
        $manifest.Save($manifestPath)
    }

    $msix = Join-Path $artifacts 'FolderHue.msix'
    if (Test-Path $msix) { Remove-Item $msix -Force }

    & (Join-Path $SdkBin 'makeappx.exe') pack /d $staging /p $msix /o
    if ($LASTEXITCODE -ne 0) { throw "makeappx a echoue (code $LASTEXITCODE)." }

    Write-Ok "paquet : $msix"

    if ($CertificateThumbprint) {
        & (Join-Path $SdkBin 'signtool.exe') sign /fd SHA256 /sha1 $CertificateThumbprint `
            /tr http://timestamp.digicert.com /td SHA256 $msix
        if ($LASTEXITCODE -ne 0) { throw "signtool a echoue (code $LASTEXITCODE)." }
        Write-Ok 'paquet signe'
    }
    else {
        Write-Host '    !    paquet non signe. Le Microsoft Store le signera, ou fournissez -CertificateThumbprint.' -ForegroundColor Yellow
    }
}

# --- Enchainement ----------------------------------------------------------

$sdkBin = Initialize-BuildTools
Assert-IdentityConsistency

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null

Build-Solution
Test-Core
Publish-App
Publish-Shell

$shellPublish = Join-Path $repository "src\FolderHue.Shell\bin\$Configuration\net8.0-windows\$runtime\publish"
$appPublish = Join-Path $repository "src\FolderHue.App\bin\$Configuration\net8.0-windows\$runtime\publish"

Assert-ShellManifestEmbedded $sdkBin (Join-Path $shellPublish 'FolderHue.Shell.dll')
Build-Layout $shellPublish $appPublish
Initialize-IconLibrary

if ($FullPackage) {
    Build-FullPackage $sdkBin
}

Write-Host ''
Write-Step 'Build termine. Etape suivante : .\scripts\install-dev.ps1'
