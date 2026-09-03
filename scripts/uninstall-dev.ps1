<#
.SYNOPSIS
    Retire le paquet de developpement et, sur demande, reinitialise les dossiers colorises.

.DESCRIPTION
    La desinstallation n'est jamais destructive (CLAUDE.md §6.6) : elle propose de reinitialiser
    les dossiers listes dans le journal, mais ne supprime aucun dossier ni fichier de
    l'utilisateur. Les icones generees dans %LOCALAPPDATA%\FolderHue ne sont retirees que
    si vous le demandez explicitement.

.PARAMETER ResetFolders
    Reinitialise les dossiers colorises avant de retirer le paquet. Sans ce parametre, le script
    vous previendra si des dossiers sont encore colorises.

.PARAMETER RemoveAppData
    Supprime en plus %LOCALAPPDATA%\FolderHue (icones generees et journal).
    A n'utiliser qu'apres une reinitialisation : le journal est ce qui permet de rendre aux
    dossiers leur etat d'origine.
#>
[CmdletBinding()]
param(
    [switch] $ResetFolders,
    [switch] $RemoveAppData
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot
$appExecutable = Join-Path $repository 'artifacts\package\FolderHue.App.exe'
$appData = Join-Path $env:LOCALAPPDATA 'FolderHue'
$journal = Join-Path $appData 'applied.json'

function Write-Step { param([string] $Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Ok   { param([string] $Message) Write-Host "    OK   $Message" -ForegroundColor Green }
function Write-Warn { param([string] $Message) Write-Host "    !    $Message" -ForegroundColor Yellow }

# --- Dossiers encore colorises ---------------------------------------------

Write-Step 'Dossiers colorises'

$applied = 0
if (Test-Path $journal) {
    try {
        $data = Get-Content $journal -Raw | ConvertFrom-Json
        if ($data.PSObject.Properties.Name -contains 'Entries' -and $data.Entries) {
            $applied = @($data.Entries).Count
        }
    }
    catch {
        Write-Warn "journal illisible : $($_.Exception.Message)"
    }
}

if ($applied -eq 0) {
    Write-Ok 'aucun dossier colorise'
}
elseif ($ResetFolders) {
    if (-not (Test-Path $appExecutable)) {
        throw "FolderHue.App.exe est introuvable : impossible de reinitialiser. Lancez .\scripts\build.ps1."
    }

    Write-Step "Reinitialisation de $applied dossier(s)"
    & $appExecutable --reset-all
    if ($LASTEXITCODE -ne 0) {
        Write-Warn 'certains dossiers n''ont pas pu etre reinitialises ; consultez le journal.'
    }
    else {
        Write-Ok 'dossiers rendus a leur etat d''origine'
    }
}
else {
    Write-Warn "$applied dossier(s) restent colorises."
    Write-Host '         Leur icone survivra a la desinstallation, car elle est inscrite dans leur'
    Write-Host '         desktop.ini. Relancez avec -ResetFolders pour les rendre a leur etat d''origine.'
}

# --- Retrait du paquet -----------------------------------------------------

[xml] $appx = Get-Content (Join-Path $repository 'src\FolderHue.Package\AppxManifest.xml') -Raw
$packageName = $appx.Package.Identity.Name

Write-Step "Retrait de $packageName"

$existing = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
if ($existing) {
    $existing | Remove-AppxPackage
    Write-Ok 'paquet retire'
}
else {
    Write-Ok 'paquet deja absent'
}

# --- Donnees applicatives --------------------------------------------------

if ($RemoveAppData) {
    if ($applied -gt 0 -and -not $ResetFolders) {
        throw ('Refus de supprimer les donnees applicatives : ' +
               "$applied dossier(s) sont encore colorises et le journal est ce qui permet de les " +
               'restaurer. Relancez avec -ResetFolders -RemoveAppData.')
    }

    Write-Step 'Suppression des donnees applicatives'
    if (Test-Path $appData) {
        Remove-Item $appData -Recurse -Force
        Write-Ok "$appData supprime"
    }
    else {
        Write-Ok 'rien a supprimer'
    }
}

# --- Redemarrage de l'Explorateur ------------------------------------------

Write-Step 'Redemarrage de l''Explorateur'
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800
if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) {
    Start-Process explorer.exe
}
Write-Ok 'Explorateur relance'
