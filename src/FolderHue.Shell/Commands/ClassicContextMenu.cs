using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using FolderHue.Core.Palette;
using FolderHue.Core.Resources;
using FolderHue.Shell.Com;

namespace FolderHue.Shell.Commands;

/// <summary>
/// Handler de menu contextuel herite, double de la commande moderne.
/// </summary>
/// <remarks>
/// Un verbe package <c>desktop4</c> n'est rendu que par le menu moderne de Windows 11. Les
/// utilisateurs qui restaurent le menu classique — tweak
/// <c>{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}</c> — ne verraient jamais l'entree. Ce handler
/// expose la meme palette via <c>IContextMenu</c>, exactement comme le font les extensions de
/// PowerToys, qui embarquent elles aussi les deux implementations.
/// <para>
/// Les deux chemins partagent le catalogue, le personnalisateur et la liste d'exclusion : seule
/// la construction du menu differe.
/// </para>
/// </remarks>
[GeneratedComClass]
internal sealed partial class ClassicContextMenu : IShellExtInit, IContextMenu
{
    /// <summary>N'ajouter que le verbe par defaut. <c>CMF_DEFAULTONLY</c>, shobjidl_core.h.</summary>
    private const uint CmfDefaultOnly = 0x00000001;

    /// <summary>Decalage du <c>lpVerb</c> dans <c>CMINVOKECOMMANDINFO</c>, en 64 bits.</summary>
    private const int VerbOffset = 16;

    private List<string> _paths = [];

    /// <summary>Nombre d'identifiants de commande consommes par le menu.</summary>
    private static int CommandCount => PaletteCatalog.Colors.Count + PaletteCatalog.Emblems.Count + 1;

    /// <inheritdoc/>
    public int Initialize(IntPtr pidlFolder, IntPtr pdtobj, IntPtr hkeyProgID)
    {
        try
        {
            _paths = ReadSelection(pdtobj);
            return _paths.Count > 0 ? HResult.Ok : HResult.Fail;
        }
        catch (Exception e)
        {
            ShellServices.Log.Error("Initialize du menu herite a echoue.", e);
            return HResult.Fail;
        }
    }

    /// <inheritdoc/>
    public int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags)
    {
        try
        {
            // Le shell ne demande que le verbe par defaut, par exemple lors d'un double-clic.
            if ((uFlags & CmfDefaultOnly) != 0 || _paths.Count == 0)
            {
                return HResult.Ok;
            }

            if (idCmdLast - idCmdFirst < CommandCount)
            {
                return HResult.Ok;
            }

            IntPtr root = NativeMethods.CreatePopupMenu();
            if (root == IntPtr.Zero)
            {
                return HResult.Fail;
            }

            uint id = idCmdFirst;

            // Position de l'element suivant dans le sous-menu : les puces se posent par position,
            // et les separateurs en occupent une comme les autres.
            uint position = 0;

            foreach (FolderColor color in PaletteCatalog.Colors)
            {
                NativeMethods.AppendMenu(root, NativeMethods.MfString, id++, Loc.Get(color.ResourceKey));
                NativeMethods.SetMenuItemBitmap(
                    root, position++, MenuIcons.Get(ShellServices.Paths.LogoPath(color.Id)));
            }

            NativeMethods.AppendMenu(root, NativeMethods.MfSeparator, 0, null);
            position++;

            IntPtr emblems = NativeMethods.CreatePopupMenu();
            if (emblems != IntPtr.Zero)
            {
                uint emblemPosition = 0;

                foreach (Emblem emblem in PaletteCatalog.Emblems)
                {
                    NativeMethods.AppendMenu(emblems, NativeMethods.MfString, id++, Loc.Get(emblem.ResourceKey));
                    NativeMethods.SetMenuItemBitmap(
                        emblems,
                        emblemPosition++,
                        MenuIcons.Get(ShellServices.Paths.EmblemChipPath(emblem.Id)));
                }

                NativeMethods.AppendMenu(
                    root, NativeMethods.MfPopup, (UIntPtr)(ulong)emblems, Loc.Get("Menu_Emblem"));
                NativeMethods.SetMenuItemBitmap(
                    root, position++, MenuIcons.Get(ShellServices.Paths.EmblemChipPath(Emblem.NoneId)));
            }
            else
            {
                id += (uint)PaletteCatalog.Emblems.Count;
            }

            NativeMethods.AppendMenu(root, NativeMethods.MfSeparator, 0, null);
            position++;

            NativeMethods.AppendMenu(root, NativeMethods.MfString, id++, Loc.Get("Menu_Reset"));
            NativeMethods.SetMenuItemBitmap(
                root,
                position,
                MenuIcons.Get(ShellServices.Paths.IconPath(PaletteCatalog.Neutral.Id, Emblem.NoneId)));

            NativeMethods.InsertMenu(
                hmenu,
                indexMenu,
                NativeMethods.MfByPosition | NativeMethods.MfPopup,
                (UIntPtr)(ulong)root,
                Loc.Get("Menu_Root"));

            NativeMethods.SetMenuItemBitmap(
                hmenu, indexMenu, MenuIcons.Get(ShellServices.Paths.BrandLogoPath));

            // La partie basse du HRESULT porte le nombre d'identifiants consommes.
            return CommandCount;
        }
        catch (Exception e)
        {
            ShellServices.Log.Error("QueryContextMenu a echoue.", e);
            return HResult.Ok;
        }
    }

    /// <inheritdoc/>
    public int InvokeCommand(IntPtr pici)
    {
        // Filet de securite global : une exception ici ferait tomber l'Explorateur (CLAUDE.md §6.5).
        try
        {
            if (pici == IntPtr.Zero || _paths.Count == 0)
            {
                return HResult.InvalidArg;
            }

            IntPtr verb = Marshal.ReadIntPtr(pici, VerbOffset);

            // Un verbe passe sous forme d'identifiant tient dans le mot bas du pointeur.
            if (((ulong)verb >> 16) != 0)
            {
                return HResult.Fail;
            }

            Execute((int)((ulong)verb & 0xFFFF));
            return HResult.Ok;
        }
        catch (Exception e)
        {
            ShellServices.Log.Error("InvokeCommand a echoue.", e);
            return HResult.Fail;
        }
    }

    /// <inheritdoc/>
    public int GetCommandString(IntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax)
        => HResult.NotImplemented;

    /// <summary>
    /// Applique l'action correspondant au decalage choisi dans le menu.
    /// </summary>
    /// <param name="offset">Decalage, dans le meme ordre que la construction du menu.</param>
    private void Execute(int offset)
    {
        int colors = PaletteCatalog.Colors.Count;
        int emblems = PaletteCatalog.Emblems.Count;

        if (offset < colors)
        {
            ShellServices.ApplyColor(_paths, PaletteCatalog.Colors[offset].Id);
            return;
        }

        if (offset < colors + emblems)
        {
            ShellServices.ApplyEmblem(_paths, PaletteCatalog.Emblems[offset - colors].Id);
            return;
        }

        if (offset == colors + emblems)
        {
            ShellServices.Reset(_paths);
        }
    }

    /// <summary>
    /// Convertit l'objet de donnees de la selection en liste de dossiers.
    /// </summary>
    /// <param name="dataObject">L'<c>IDataObject</c> transmis par le shell.</param>
    /// <returns>Les dossiers du systeme de fichiers, ou une liste vide.</returns>
    /// <remarks>
    /// On repasse par <c>IShellItemArray</c> pour reutiliser exactement la meme lecture de
    /// selection que la commande moderne, filtrage des non-dossiers compris.
    /// </remarks>
    private static List<string> ReadSelection(IntPtr dataObject)
    {
        if (dataObject == IntPtr.Zero)
        {
            return [];
        }

        Guid iid = Guids.IShellItemArray;

        if (NativeMethods.SHCreateShellItemArrayFromDataObject(dataObject, in iid, out IntPtr array) < 0
            || array == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            // Meme regle que GetState : l'entree n'a de sens que sur des dossiers reels.
            return ShellSelection.IsFileSystemFolderSelection(array)
                ? ShellSelection.GetPaths(array)
                : [];
        }
        finally
        {
            Marshal.Release(array);
        }
    }
}
