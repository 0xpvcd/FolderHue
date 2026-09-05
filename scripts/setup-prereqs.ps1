<#
.SYNOPSIS
    Installe et vérifie les prérequis de build de FolderHue.

.DESCRIPTION
    Trois choses sont nécessaires pour construire la solution complète :
      1. Le SDK .NET 8 (tous les projets).
      2. La toolchain C++ (MSVC + Windows SDK) — exigée par NativeAOT pour lier
         FolderHue.Shell.dll (cf. CLAUDE.md §2.1).
      3. Inno Setup 6 — qui produit FolderHue-Setup.exe.

    Les trois s'installent par winget. Le mode développeur Windows n'est plus requis :
    il ne servait qu'au déploiement MSIX, abandonné au profit d'un enregistrement
    classique sous HKEY_CURRENT_USER.

.PARAMETER SkipBuildTools
    N'installe pas Visual Studio Build Tools (téléchargement de plusieurs Go).
    Suffisant pour bâtir et tester Core, pas pour publier le Shell en NativeAOT.
#>
[CmdletBinding()]
param(
    [switch] $SkipBuildTools
)

$ErrorActionPreference = 'Stop'

function Write-Step   { param([string] $Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Ok     { param([string] $Message) Write-Host "    OK   $Message" -ForegroundColor Green }
function Write-Warn   { param([string] $Message) Write-Host "    !    $Message" -ForegroundColor Yellow }

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    throw "winget est introuvable. Installez « Programme d'installation d'application » depuis le Microsoft Store."
}

# --- 1. SDK .NET 8 ---------------------------------------------------------

Write-Step 'SDK .NET 8'

$sdks = @()
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $sdks = @(dotnet --list-sdks 2>$null)
}

if ($sdks -match '^8\.') {
    Write-Ok "déjà présent : $(($sdks -match '^8\.') -join ', ')"
}
else {
    Write-Host '    Installation de Microsoft.DotNet.SDK.8 (une élévation UAC peut être demandée)...'
    winget install --id Microsoft.DotNet.SDK.8 --exact --silent `
        --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) { throw "winget a échoué pour Microsoft.DotNet.SDK.8 (code $LASTEXITCODE)." }
    Write-Ok 'SDK .NET 8 installé. Ouvrez un nouveau terminal pour rafraîchir le PATH.'
}

# --- 2. Toolchain C++ (requise par NativeAOT) ------------------------------

Write-Step 'Toolchain C++ (MSVC + Windows SDK) pour NativeAOT'

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$hasVcTools = $false
if (Test-Path $vswhere) {
    $found = & $vswhere -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
                        -property installationPath -latest
    $hasVcTools = -not [string]::IsNullOrWhiteSpace($found)
}

if ($hasVcTools) {
    Write-Ok "workload C++ détectée : $found"
}
elseif ($SkipBuildTools) {
    Write-Warn 'ignorée (-SkipBuildTools). Le publish NativeAOT du Shell échouera.'
}
else {
    Write-Host '    Installation de Visual Studio 2022 Build Tools (plusieurs Go, comptez du temps)...'
    $override = '--quiet --wait --norestart ' +
                '--add Microsoft.VisualStudio.Workload.VCTools ' +
                '--add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 ' +
                '--add Microsoft.VisualStudio.Component.Windows11SDK.22621'
    winget install --id Microsoft.VisualStudio.2022.BuildTools --exact --silent `
        --accept-source-agreements --accept-package-agreements --override $override
    if ($LASTEXITCODE -ne 0) { throw "winget a échoué pour les Build Tools (code $LASTEXITCODE)." }
    Write-Ok 'Build Tools installés.'
}

# --- 3. Inno Setup (production de l'installeur) -----------------------------

Write-Step 'Inno Setup 6'

# winget, sans élévation, installe Inno Setup pour l'utilisateur seul : les trois
# emplacements sont donc à essayer.
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    Write-Ok "déjà présent : $iscc"
}
else {
    Write-Host "    Installation de JRSoftware.InnoSetup (une élévation UAC peut être demandée)..."
    winget install --id JRSoftware.InnoSetup --exact --silent `
        --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) { throw "winget a échoué pour Inno Setup (code $LASTEXITCODE)." }
    Write-Ok 'Inno Setup installé.'
}

Write-Host ''
Write-Step 'Prérequis traités. Étape suivante : .\scripts\build.ps1'
