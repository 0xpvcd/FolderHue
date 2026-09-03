<#
.SYNOPSIS
    Installe le paquet FolderHue signe et relance l'Explorateur.

.DESCRIPTION
    Attention, contre-intuitif : un enregistrement sparse en mode developpeur
    (Add-AppxPackage -Register -ExternalLocation) ne suffit PAS pour une extension de menu
    contextuel. Le paquet s'enregistre, le serveur COM s'active, mais l'Explorateur n'affiche
    jamais l'entree. Verifie sur cette machine : seul un paquet SIGNE et reellement installe
    fonctionne.

    Ce script installe donc artifacts\FolderHue.msix. Il ne desactive aucun controle de
    signature (CLAUDE.md §11) : il en exige un au contraire.

    Prerequis, une seule fois :
        .\scripts\make-devcert.ps1
        puis l'import eleve que ce script indique en cas d'absence
        .\scripts\build.ps1 -FullPackage -CertificateThumbprint <empreinte>

.PARAMETER SkipExplorerRestart
    N'arrete pas l'Explorateur. L'extension n'apparaitra qu'apres un redemarrage manuel.
#>
[CmdletBinding()]
param(
    [switch] $SkipExplorerRestart
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot
$package = Join-Path $repository 'artifacts\FolderHue.msix'

function Write-Step { param([string] $Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Ok   { param([string] $Message) Write-Host "    OK   $Message" -ForegroundColor Green }

# --- Le paquet existe-t-il et est-il signe ? -------------------------------

Write-Step 'Paquet signe'

if (-not (Test-Path $package)) {
    throw ("artifacts\FolderHue.msix est absent. Produisez-le avec " +
           ".\scripts\build.ps1 -FullPackage -CertificateThumbprint <empreinte>.")
}

$signature = Get-AuthenticodeSignature $package
if ($signature.Status -ne 'Valid') {
    throw ("Le paquet n'est pas signe validement (statut : $($signature.Status)). " +
           "Un paquet non signe s'installe mais son entree de menu reste invisible. " +
           "Executez .\scripts\make-devcert.ps1 puis rebatissez avec -CertificateThumbprint.")
}

Write-Ok "signe par $($signature.SignerCertificate.Subject)"

# --- Le certificat est-il approuve par la machine ? ------------------------

Write-Step 'Certificat approuve'

$thumbprint = $signature.SignerCertificate.Thumbprint
$trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $thumbprint }

if (-not $trusted) {
    $pfx = Join-Path $repository 'artifacts\FolderHue-dev.pfx'
    throw ("Le certificat de signature n'est pas dans « Personnes de confiance » de la machine. " +
           "Importez-le depuis une console elevee :`n" +
           "    Import-PfxCertificate -FilePath '$pfx' -CertStoreLocation Cert:\LocalMachine\TrustedPeople")
}

Write-Ok "empreinte $thumbprint approuvee"

# --- Retrait de la version precedente --------------------------------------

[xml] $appx = Get-Content (Join-Path $repository 'src\FolderHue.Package\AppxManifest.xml') -Raw
$packageName = $appx.Package.Identity.Name

Write-Step "Retrait de $packageName"

$existing = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
if ($existing) {
    $existing | Remove-AppxPackage
    Write-Ok 'ancienne version retiree'
}
else {
    Write-Ok 'aucune version precedente'
}

# --- Installation ----------------------------------------------------------

Write-Step 'Installation'

Add-AppxPackage -Path $package
$installed = Get-AppxPackage -Name $packageName

Write-Ok "installe ($($installed.SignatureKind)) dans $($installed.InstallLocation)"

# --- Redemarrage de l'Explorateur ------------------------------------------

if ($SkipExplorerRestart) {
    Write-Host '    !    Explorateur non redemarre : l''extension n''apparaitra pas encore.' -ForegroundColor Yellow
}
else {
    # L'Explorateur verrouille la DLL une fois chargee : sans ce redemarrage, le build suivant
    # echouerait et l'ancienne version resterait active (CLAUDE.md §5).
    Write-Step 'Redemarrage de l''Explorateur'
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
    if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) {
        Start-Process explorer.exe
    }
    Write-Ok 'Explorateur relance'
}

Write-Host ''
Write-Host 'Faites un clic droit sur un dossier : « FolderHue » doit apparaitre,'
Write-Host 'aussi bien dans le menu direct de Windows 11 que dans le menu classique.'
Write-Host 'Si ce n''est pas le cas, fermez puis rouvrez la session Windows : le cache des'
Write-Host 'handlers packages est parfois tenace (CLAUDE.md §5).'
