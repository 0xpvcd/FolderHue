<#
.SYNOPSIS
    Sonde du sous-menu : enumere les sous-commandes, et peut en invoquer une, sans ouvrir
    le moindre menu contextuel.

.DESCRIPTION
    Complete tools\probe-shell.ps1, qui repond "le serveur COM s'active-t-il ?". Celle-ci
    repond aux deux questions que l'autre laisse ouvertes, et qui sont justement celles dont
    la reponse est la plus trompeuse :

      1. COMBIEN d'elements le sous-menu compte-t-il, separateurs compris ? Au-dela de 16
         l'entree racine "FolderHue" disparait entierement du menu, logo compris, sans le
         moindre message - et pendant ce temps le serveur COM continue de s'activer
         normalement (CLAUDE.md, section 4.4). La panne est donc indiscernable d'un defaut
         d'enregistrement. C'est la verification la moins couteuse et celle a laquelle
         personne ne pense : elle tient desormais en une commande.

      2. QUE FAIT reellement un clic ? -Invoke appelle IExplorerCommand::Invoke sur la
         sous-commande choisie. L'activation se fait en CLSCTX_INPROC_SERVER, comme le fait
         le shell : le travail s'execute donc dans ce processus, exactement comme un vrai
         clic droit s'execute dans explorer.exe (section 4.6). C'est le seul moyen de comparer "Apply depuis un processus ordinaire" et
         "Apply depuis le shell" sans toucher a la souris.

    Les titres sont ceux que le shell afficherait : un separateur ressort en "(separateur)".

.PARAMETER Folder
    Dossier passe a la commande sous la forme d'un IShellItemArray, comme le ferait
    l'Explorateur avec la selection.

.PARAMETER Invoke
    Index de la sous-commande a invoquer, tel qu'affiche par l'enumeration. Omis, la sonde
    se contente d'enumerer - elle ne modifie alors aucun dossier.

.EXAMPLE
    .\tools\probe-menu.ps1 -Folder 'F:\un\dossier'
    .\tools\probe-menu.ps1 -Folder 'F:\un\dossier' -Invoke 7
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Folder,
    [int] $Invoke = -1
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[ComImport, Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IExplorerCommand
{
    [PreserveSig] int GetTitle(IntPtr psiItemArray, out IntPtr ppszName);
    [PreserveSig] int GetIcon(IntPtr psiItemArray, out IntPtr ppszIcon);
    [PreserveSig] int GetToolTip(IntPtr psiItemArray, out IntPtr ppszInfotip);
    [PreserveSig] int GetCanonicalName(out Guid pguidCommandName);
    [PreserveSig] int GetState(IntPtr psiItemArray, [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow, out uint pCmdState);
    [PreserveSig] int Invoke(IntPtr psiItemArray, IntPtr pbc);
    [PreserveSig] int GetFlags(out uint pFlags);
    [PreserveSig] int EnumSubCommands(out IEnumExplorerCommand ppEnum);
}

[ComImport, Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IEnumExplorerCommand
{
    [PreserveSig] int Next(uint celt, [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] IExplorerCommand[] pUICommand, out uint pceltFetched);
    [PreserveSig] int Skip(uint celt);
    [PreserveSig] int Reset();
    [PreserveSig] int Clone(out IEnumExplorerCommand ppenum);
}

public static class FolderHueMenuProbe
{
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint ctx, ref Guid iid, out IntPtr obj);
    [DllImport("ole32.dll")] public static extern int CoInitializeEx(IntPtr reserved, uint flags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, out IntPtr ppv);

    [DllImport("shell32.dll")]
    private static extern int SHCreateShellItemArrayFromShellItem(IntPtr psi, ref Guid riid, out IntPtr ppv);

    // L'extension est enregistree en InprocServer32 : le shell la charge DANS explorer.exe,
    // il n'y a plus de surrogate. Une activation en LOCAL_SERVER repondrait desormais
    // REGDB_E_CLASSNOTREG.
    private const uint CLSCTX_INPROC_SERVER = 0x1;

    private static string Title(IExplorerCommand command, IntPtr items)
    {
        IntPtr p;
        int hr = command.GetTitle(items, out p);
        if (hr < 0 || p == IntPtr.Zero) { return "<GetTitle hr=0x" + hr.ToString("X8") + ">"; }
        string s = Marshal.PtrToStringUni(p);
        Marshal.FreeCoTaskMem(p);
        return s;
    }

    // Le CLSID doit rester aligne sur Com/Guids.cs et sur AppxManifest.xml : une divergence
    // se traduit par un menu absent, sans message (CLAUDE.md, section 10).
    private static IExplorerCommand CreateRoot()
    {
        Guid clsid = new Guid("C228C2F8-706B-4A2E-9C48-74F3062BE146");
        Guid iid = new Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9");
        IntPtr p;
        int hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_INPROC_SERVER, ref iid, out p);
        if (hr < 0) { throw new COMException("CoCreateInstance en INPROC_SERVER", hr); }
        IExplorerCommand root = (IExplorerCommand)Marshal.GetTypedObjectForIUnknown(p, typeof(IExplorerCommand));
        Marshal.Release(p);
        return root;
    }

    private static IntPtr MakeSelection(string folder)
    {
        Guid iidItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");   // IShellItem
        Guid iidArray = new Guid("b63ea76d-1f85-456f-a19c-48159efa858b");  // IShellItemArray
        IntPtr item;
        int hr = SHCreateItemFromParsingName(folder, IntPtr.Zero, ref iidItem, out item);
        if (hr < 0) { throw new COMException("SHCreateItemFromParsingName", hr); }
        IntPtr array;
        hr = SHCreateShellItemArrayFromShellItem(item, ref iidArray, out array);
        Marshal.Release(item);
        if (hr < 0) { throw new COMException("SHCreateShellItemArrayFromShellItem", hr); }
        return array;
    }

    // Toute la boucle reste ici : PowerShell reperd le type de l'interface a chaque
    // aller-retour, un __ComObject ne se reconvertit pas vers une [ComImport] locale.
    public static string[] Run(string folder, int invokeIndex)
    {
        List<string> lines = new List<string>();
        IExplorerCommand root = CreateRoot();
        lines.Add("racine : " + Title(root, IntPtr.Zero) + " - activee en INPROC_SERVER");
        lines.Add("");

        IntPtr items = MakeSelection(folder);
        try
        {
            IEnumExplorerCommand e;
            int hr = root.EnumSubCommands(out e);
            if (hr < 0) { lines.Add("EnumSubCommands hr=0x" + hr.ToString("X8")); return lines.ToArray(); }

            IExplorerCommand[] buffer = new IExplorerCommand[1];
            int i = 0;
            while (true)
            {
                uint fetched;
                hr = e.Next(1, buffer, out fetched);
                if (hr != 0 || fetched == 0) { break; }

                string title = Title(buffer[0], items);
                if (title.Length == 0) { title = "(separateur)"; }

                if (i == invokeIndex)
                {
                    int ihr = buffer[0].Invoke(items, IntPtr.Zero);
                    lines.Add(string.Format("[{0,2}] {1}   <-- INVOKE, hr=0x{2:X8}", i, title, ihr));
                }
                else
                {
                    lines.Add(string.Format("[{0,2}] {1}", i, title));
                }
                i++;
            }

            lines.Add("");
            lines.Add(i + " elements, separateurs compris. Le maximum est 16 : au-dela,");
            lines.Add("l'entree racine disparait entierement du menu (CLAUDE.md, section 4.4).");
        }
        finally
        {
            Marshal.Release(items);
        }
        return lines.ToArray();
    }
}
'@

if (-not (Test-Path $Folder)) { throw "Dossier introuvable : $Folder" }

[void][FolderHueMenuProbe]::CoInitializeEx([IntPtr]::Zero, 2)   # COINIT_APARTMENTTHREADED
[FolderHueMenuProbe]::Run((Resolve-Path $Folder).Path, $Invoke) | ForEach-Object { Write-Host $_ }
