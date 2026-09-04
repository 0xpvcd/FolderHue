<#
.SYNOPSIS
    Sonde de diagnostic de l'extension shell : hebergement, activation COM, resolution d'icone.

.DESCRIPTION
    Valide tout ce que les tests unitaires ne peuvent pas atteindre, sans avoir a ouvrir un menu
    contextuel a la main. Trois questions, dans cet ordre de diagnostic :

      1. OU la DLL est-elle chargee ? Dans explorer.exe (in-proc) ou dans dllhost.exe
         (surrogate empaquete) ? Cela conditionne tout le reste : depuis un surrogate, le premier
         clic droit doit demarrer le processus - l'Explorateur n'attend pas et l'entree manque -
         et les objets GDI crees la-bas ne peuvent pas etre dessines par l'Explorateur.

      2. QUELS contextes d'activation COM sont enregistres ? Un CLSID qui repond
         REGDB_E_CLASSNOTREG (0x80040154) en CLSCTX_INPROC_SERVER ne pourra JAMAIS etre charge
         dans l'Explorateur, quoi qu'on fasse par ailleurs.

      3. QUELLE icone le shell attribue-t-il a un dossier ? Repond dans un processus neuf, donc
         sans cache herite : si la reponse est la bonne alors que l'Explorateur affiche autre
         chose, le probleme est un rafraichissement de vue, pas une ecriture.

.PARAMETER Folder
    Dossier a interroger pour la question 3. Facultatif.

.EXAMPLE
    .\tools\probe-shell.ps1
    .\tools\probe-shell.ps1 -Folder 'F:\WORKING\ENTREPRISE'
#>
[CmdletBinding()]
param(
    [string] $Folder
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step { param([string] $m) Write-Host "==> $m" -ForegroundColor Cyan }
function Write-Ok   { param([string] $m) Write-Host "    OK   $m" -ForegroundColor Green }
function Write-Bad  { param([string] $m) Write-Host "    !    $m" -ForegroundColor Yellow }

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class FolderHueProbe
{
    [DllImport("ole32.dll")]
    public static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint ctx, ref Guid iid, out IntPtr obj);
    [DllImport("ole32.dll")] public static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll")] public static extern void CoUninitialize();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SHGetFileInfoW(string path, uint attrs, ref SHFILEINFO info, uint size, uint flags);

    public const uint CLSCTX_INPROC_SERVER = 0x1;
    public const uint CLSCTX_LOCAL_SERVER  = 0x4;
    public const uint COINIT_APARTMENTTHREADED = 0x2;
    public const uint SHGFI_ICONLOCATION = 0x1000;
}
'@

# --- Identites, a garder alignees sur Com/Guids.cs -------------------------

# Le handler herite (E647099A, IShellExtInit) a ete retire : il doublait l'entree de menu (§4.6).
$clsids = @(
    @{ Nom = 'commande moderne (IExplorerCommand)'; Clsid = [Guid] 'C228C2F8-706B-4A2E-9C48-74F3062BE146'; Iid = [Guid] 'a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9' }
)

# --- 1. Ou la DLL est-elle chargee ? ---------------------------------------

Write-Step 'Hebergement de FolderHue.Shell.dll'

$hosts = @()
foreach ($p in Get-Process) {
    try {
        if ($p.Modules.ModuleName -contains 'FolderHue.Shell.dll') {
            $hosts += [pscustomobject]@{ Id = $p.Id; Nom = $p.ProcessName; Demarre = $p.StartTime }
        }
    }
    catch {
        # Processus protege ou termine entre-temps : sans interet ici.
    }
}

if ($hosts.Count -eq 0) {
    Write-Bad 'la DLL n''est chargee nulle part - ouvrez un menu contextuel sur un dossier, puis relancez'
}
foreach ($h in $hosts) {
    if ($h.Nom -eq 'explorer') {
        Write-Ok "chargee IN-PROC dans explorer.exe (pid $($h.Id))"
    }
    else {
        Write-Bad ("chargee dans {0}.exe (pid {1}, demarre a {2:HH:mm:ss}) - surrogate hors Explorateur" -f $h.Nom, $h.Id, $h.Demarre)
    }
}

# --- 2. Contextes d'activation enregistres ---------------------------------

Write-Step 'Contextes d''activation COM'

[void][FolderHueProbe]::CoInitializeEx([IntPtr]::Zero, [FolderHueProbe]::COINIT_APARTMENTTHREADED)
try {
    foreach ($entry in $clsids) {
        foreach ($ctx in @(
            @{ Nom = 'INPROC_SERVER'; Valeur = [FolderHueProbe]::CLSCTX_INPROC_SERVER },
            @{ Nom = 'LOCAL_SERVER '; Valeur = [FolderHueProbe]::CLSCTX_LOCAL_SERVER }
        )) {
            $clsid = $entry.Clsid
            $iid = $entry.Iid
            $obj = [IntPtr]::Zero
            $hr = [FolderHueProbe]::CoCreateInstance([ref] $clsid, [IntPtr]::Zero, $ctx.Valeur, [ref] $iid, [ref] $obj)

            $etiquette = "{0} en {1}" -f $entry.Nom, $ctx.Nom
            if ($hr -eq 0) {
                Write-Ok ("{0} : active (hr=0x{1:X8})" -f $etiquette, $hr)
                [void][System.Runtime.InteropServices.Marshal]::Release($obj)
            }
            elseif ($hr -eq -2147221164) {
                # REGDB_E_CLASSNOTREG : ce contexte n'est tout simplement pas enregistre.
                Write-Bad ("{0} : NON ENREGISTRE (REGDB_E_CLASSNOTREG)" -f $etiquette)
            }
            else {
                Write-Bad ("{0} : echec hr=0x{1:X8}" -f $etiquette, $hr)
            }
        }
    }
}
finally {
    [FolderHueProbe]::CoUninitialize()
}

# --- 3. Icone resolue par le shell -----------------------------------------

if ($Folder) {
    Write-Step "Icone que le shell attribue a « $Folder »"

    if (-not (Test-Path $Folder)) {
        Write-Bad 'dossier introuvable'
    }
    else {
        $info = New-Object FolderHueProbe+SHFILEINFO
        $taille = [System.Runtime.InteropServices.Marshal]::SizeOf($info)
        [void][FolderHueProbe]::SHGetFileInfoW($Folder, 0, [ref] $info, $taille, [FolderHueProbe]::SHGFI_ICONLOCATION)
        Write-Ok "resolue sur « $($info.szDisplayName) » index $($info.iIcon)"

        $ini = Join-Path $Folder 'desktop.ini'
        if (Test-Path $ini) {
            foreach ($ligne in Get-Content $ini) { Write-Host "         $ligne" }
        }
        else {
            Write-Bad 'aucun desktop.ini'
        }

        $attributs = (Get-Item $Folder -Force).Attributes
        if ($attributs -band [System.IO.FileAttributes]::ReadOnly) {
            Write-Ok "attributs du dossier : $attributs"
        }
        else {
            Write-Bad "attributs du dossier : $attributs - sans ReadOnly ni System, desktop.ini est ignore"
        }

        Write-Host ''
        Write-Host 'Ce processus est neuf : sa reponse ne doit rien au cache de l''Explorateur.'
        Write-Host 'Si elle est correcte alors que l''Explorateur affiche autre chose, le probleme'
        Write-Host 'est un rafraichissement de vue, pas une ecriture.'
    }
}
