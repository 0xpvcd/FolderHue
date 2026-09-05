<#
.SYNOPSIS
    Batit FolderHue et produit l'installeur.

.DESCRIPTION
    Enchaine, dans cet ordre :

      1. mise en place de la chaine d'outils (vswhere et Windows SDK) ;
      2. tests unitaires de Core ;
      3. publication NativeAOT de FolderHue.Shell.dll ;
      4. publication autonome de FolderHue.App, runtime .NET compris ;
      5. compilation de l'installeur Inno Setup.

    Le paquet MSIX a disparu : FolderHue s'enregistre desormais comme une extension shell
    classique, sous HKEY_CURRENT_USER, et se distribue par un simple executable. Plus de
    certificat a fabriquer, plus de deploiement sparse, plus de desenregistrement prealable
    qui laissait la machine sans extension quand le build echouait en cours de route.

.PARAMETER SkipTests
    N'execute pas les tests. A reserver aux allers-retours rapides sur le shell.

.PARAMETER NoInstaller
    S'arrete apres la publication : produit artifacts\app sans appeler Inno Setup.

.PARAMETER Configuration
    Configuration MSBuild. Release par defaut.
#>
[CmdletBinding()]
param(
    [switch] $SkipTests,
    [switch] $NoInstaller,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repository 'artifacts'
$appLayout = Join-Path $artifacts 'app'
$runtime = 'win-x64'

function Write-Step { param([string] $Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Ok   { param([string] $Message) Write-Host "    OK   $Message" -ForegroundColor Green }
function Write-Warn { param([string] $Message) Write-Host "    !    $Message" -ForegroundColor Yellow }

# --- Chaine d'outils --------------------------------------------------------

function Initialize-BuildTools {
    <#
        NativeAOT lie l'image finale avec link.exe, que ILCompiler localise via vswhere.exe.
        Ni l'un ni l'autre n'est sur le PATH par defaut : un « dotnet publish -p:PublishAot=true »
        lance a la main hors de ce script echoue a l'edition de liens.
    #>
    $installer = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
    if (-not (Test-Path (Join-Path $installer 'vswhere.exe'))) {
        throw "vswhere.exe est introuvable. Executez d'abord .\scripts\setup-prereqs.ps1."
    }

    $sdkRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    $sdkBin = Get-ChildItem $sdkRoot -Directory -Filter '10.*' -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'x64' } |
        Where-Object { Test-Path (Join-Path $_ 'mt.exe') } |
        Select-Object -First 1

    if (-not $sdkBin) {
        throw "Le Windows SDK est introuvable. Executez d'abord .\scripts\setup-prereqs.ps1."
    }

    $env:PATH = "$installer;$sdkBin;$env:PATH"
    Write-Ok "toolchain : $sdkBin"
}

function Get-InnoSetupCompiler {
    <#
        Inno Setup s'installe indifféremment pour la machine ou pour l'utilisateur : winget,
        sans élévation, le pose sous %LOCALAPPDATA%\Programs. On essaie donc les trois
        emplacements usuels, puis on interroge le registre de désinstallation, qui reste juste
        quel que soit le mode d'installation.
    #>
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { return $candidate }
    }

    $keys = @(
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )

    $location = Get-ItemProperty $keys -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like 'Inno Setup*' -and $_.InstallLocation } |
        Select-Object -First 1 -ExpandProperty InstallLocation

    if ($location) {
        $fromRegistry = Join-Path $location 'ISCC.exe'
        if (Test-Path $fromRegistry) { return $fromRegistry }
    }

    return $null
}

# --- Coherence -------------------------------------------------------------

function Assert-Consistency {
    <#
        Le CLSID vit dans Core (ShellRegistration) et le serveur COM s'y refere : il n'y a plus
        deux valeurs a tenir en phase, contrairement a l'epoque du manifeste MSIX. Reste a
        verifier que l'icone de l'executable existe, car elle est necessaire AVANT la
        compilation et ne peut donc pas etre produite par le build lui-meme.
    #>
    Write-Step 'Coherence'

    $icon = Join-Path $repository 'installer\FolderHue.ico'
    if (-not (Test-Path $icon)) {
        throw ("installer\FolderHue.ico est absent. Regenerez-le avec " +
               "« FolderHue.App --export-icon installer\FolderHue.ico » depuis une build precedente.")
    }
    Write-Ok 'icone de l''executable presente'

    $manifest = Join-Path $repository 'src\FolderHue.Shell\FolderHue.Shell.manifest'
    if (Test-Path $manifest) {
        throw ("src\FolderHue.Shell\FolderHue.Shell.manifest est revenu. Ce fragment declare une " +
               "identite MSIX : dans une DLL non packagee il n'a plus lieu d'etre.")
    }
    Write-Ok 'aucun residu MSIX'
}

# --- Etapes ----------------------------------------------------------------

function Invoke-Tests {
    Write-Step 'Tests unitaires'
    & dotnet test (Join-Path $repository 'tests\FolderHue.Core.Tests') -c $Configuration --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Les tests ont echoue (code $LASTEXITCODE)." }
    Write-Ok 'tests au vert'
}

function Publish-Shell {
    Write-Step 'FolderHue.Shell (NativeAOT)'
    & dotnet publish (Join-Path $repository 'src\FolderHue.Shell\FolderHue.Shell.csproj') `
        -c $Configuration -r $runtime -o $appLayout --nologo
    if ($LASTEXITCODE -ne 0) { throw "La publication du shell a echoue (code $LASTEXITCODE)." }

    $dll = Join-Path $appLayout 'FolderHue.Shell.dll'
    if (-not (Test-Path $dll)) { throw "FolderHue.Shell.dll est absent de $appLayout." }
    Write-Ok ("FolderHue.Shell.dll : {0:N0} Ko" -f ((Get-Item $dll).Length / 1KB))
}

function Publish-App {
    <#
        Publication autonome : le runtime .NET voyage avec l'application. C'est le prix a payer
        pour qu'un double-clic suffise, sans prerequis a installer. L'ordre compte, l'application
        etant publiee APRES le shell : elle ecrase les fichiers communs par les siens, et c'est
        bien FolderHue.Shell.dll, produit par l'etape precedente, qu'on veut conserver.
    #>
    Write-Step 'FolderHue.App (autonome)'
    & dotnet publish (Join-Path $repository 'src\FolderHue.App\FolderHue.App.csproj') `
        -c $Configuration -r $runtime --self-contained true -o $appLayout --nologo
    if ($LASTEXITCODE -ne 0) { throw "La publication de l'application a echoue (code $LASTEXITCODE)." }

    $exe = Join-Path $appLayout 'FolderHue.App.exe'
    if (-not (Test-Path $exe)) { throw "FolderHue.App.exe est absent de $appLayout." }

    $size = (Get-ChildItem $appLayout -Recurse -File | Measure-Object -Property Length -Sum).Sum
    Write-Ok ("artifacts\app : {0:N0} fichiers, {1:N0} Mo" -f `
        (Get-ChildItem $appLayout -Recurse -File).Count, ($size / 1MB))
}

function Build-Installer {
    Write-Step 'Installeur Inno Setup'

    $iscc = Get-InnoSetupCompiler
    if (-not $iscc) {
        Write-Warn 'Inno Setup est introuvable. Executez .\scripts\setup-prereqs.ps1.'
        Write-Warn "artifacts\app est pret : l'installeur seul manque."
        return
    }

    & $iscc (Join-Path $repository 'installer\FolderHue.iss') /Q
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup a echoue (code $LASTEXITCODE)." }

    $setup = Get-ChildItem $artifacts -Filter 'FolderHue-Setup-*.exe' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $setup) { throw "L'installeur n'a pas ete produit." }

    Write-Ok ("{0} : {1:N0} Mo" -f $setup.Name, ($setup.Length / 1MB))
    Write-Host ''
    Write-Host "    $($setup.FullName)" -ForegroundColor White
}

# --- Deroulement -----------------------------------------------------------

Initialize-BuildTools
Assert-Consistency

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
if (Test-Path $appLayout) { Remove-Item $appLayout -Recurse -Force }
New-Item -ItemType Directory -Path $appLayout -Force | Out-Null

if (-not $SkipTests) { Invoke-Tests } else { Write-Warn 'tests ignores' }

Publish-Shell
Publish-App

if (-not $NoInstaller) { Build-Installer } else { Write-Warn 'installeur ignore' }

Write-Host ''
Write-Ok 'termine'
