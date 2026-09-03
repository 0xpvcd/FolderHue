using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using FolderHue.Shell.Com;

namespace FolderHue.Shell.Commands;

/// <summary>
/// Base commune aux entrees du menu contextuel.
/// </summary>
/// <remarks>
/// Chaque methode est enveloppee dans un <c>try/catch</c> : une exception non geree traversant la
/// frontiere COM ferait tomber <c>explorer.exe</c> (CLAUDE.md §6.5).
/// </remarks>
internal abstract partial class ExplorerCommandBase : IExplorerCommand
{
    /// <summary>Libelle affiche dans le menu. Doit etre immediat, sans acces disque.</summary>
    protected abstract string Title { get; }

    /// <summary>
    /// Ressource d'icone au format <c>chemin,index</c>, ou <see langword="null"/> pour aucune.
    /// </summary>
    protected virtual string? IconResource => null;

    /// <summary>Drapeaux de la commande.</summary>
    protected virtual ExplorerCommandFlags Flags => ExplorerCommandFlags.Default;

    /// <summary>Execute l'action de la commande sur les dossiers selectionnes.</summary>
    /// <param name="paths">Les dossiers selectionnes, deja resolus.</param>
    protected virtual void Execute(IReadOnlyList<string> paths)
    {
    }

    /// <summary>Sous-commandes de la commande, si elle en a.</summary>
    /// <returns>Les commandes filles, ou une liste vide.</returns>
    protected virtual IReadOnlyList<object> CreateSubCommands() => [];

    /// <inheritdoc/>
    public int GetTitle(IntPtr psiItemArray, out IntPtr ppszName)
    {
        ppszName = IntPtr.Zero;

        try
        {
            ppszName = Marshal.StringToCoTaskMemUni(Title);
            return HResult.Ok;
        }
        catch (Exception e) when (e is OutOfMemoryException or ArgumentException)
        {
            return HResult.Fail;
        }
    }

    /// <inheritdoc/>
    public int GetIcon(IntPtr psiItemArray, out IntPtr ppszIcon)
    {
        ppszIcon = IntPtr.Zero;

        string? icon = IconResource;
        if (string.IsNullOrEmpty(icon))
        {
            return HResult.NotImplemented;
        }

        try
        {
            ppszIcon = Marshal.StringToCoTaskMemUni(icon);
            return HResult.Ok;
        }
        catch (Exception e) when (e is OutOfMemoryException or ArgumentException)
        {
            return HResult.Fail;
        }
    }

    /// <inheritdoc/>
    public int GetToolTip(IntPtr psiItemArray, out IntPtr ppszInfotip)
    {
        ppszInfotip = IntPtr.Zero;
        return HResult.NotImplemented;
    }

    /// <inheritdoc/>
    public int GetCanonicalName(out Guid pguidCommandName)
    {
        pguidCommandName = Guid.Empty;
        return HResult.NotImplemented;
    }

    /// <inheritdoc/>
    public int GetState(IntPtr psiItemArray, bool fOkToBeSlow, out uint pCmdState)
    {
        try
        {
            // L'entree n'a de sens que sur des dossiers reels : ni sur un fichier, ni sur
            // « Ce PC », ni sur une bibliotheque (CLAUDE.md §4.4).
            pCmdState = ShellSelection.IsFileSystemFolderSelection(psiItemArray)
                ? (uint)ExplorerCommandState.Enabled
                : (uint)ExplorerCommandState.Hidden;

            return HResult.Ok;
        }
        catch (Exception e)
        {
            ShellServices.Log.Error("GetState a echoue.", e);
            pCmdState = (uint)ExplorerCommandState.Hidden;
            return HResult.Ok;
        }
    }

    /// <inheritdoc/>
    public int Invoke(IntPtr psiItemArray, IntPtr pbc)
    {
        // Filet de securite global : c'est ici qu'une exception ferait tomber l'Explorateur.
        try
        {
            List<string> paths = ShellSelection.GetPaths(psiItemArray);

            if (paths.Count > 0)
            {
                Execute(paths);
            }

            return HResult.Ok;
        }
        catch (Exception e)
        {
            ShellServices.Log.Error($"Invoke a echoue pour « {Title} ».", e);
            return HResult.Fail;
        }
    }

    /// <inheritdoc/>
    public int GetFlags(out uint pFlags)
    {
        pFlags = (uint)Flags;
        return HResult.Ok;
    }

    /// <inheritdoc/>
    public int EnumSubCommands(out IntPtr ppEnum)
    {
        ppEnum = IntPtr.Zero;

        try
        {
            IReadOnlyList<object> children = CreateSubCommands();

            if (children.Count == 0)
            {
                return HResult.NotImplemented;
            }

            return ShellComWrappers.GetComInterface(
                new SubCommandEnumerator(children),
                Guids.IEnumExplorerCommand,
                out ppEnum);
        }
        catch (Exception e)
        {
            ShellServices.Log.Error($"EnumSubCommands a echoue pour « {Title} ».", e);
            return HResult.Fail;
        }
    }
}
