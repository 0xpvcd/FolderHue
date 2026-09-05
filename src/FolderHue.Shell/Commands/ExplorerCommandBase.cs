using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using FolderHue.Shell.Com;

namespace FolderHue.Shell.Commands;

/// <summary>
/// Common base for the context menu entries.
/// </summary>
/// <remarks>
/// Every method is wrapped in a <c>try/catch</c>: an unhandled exception crossing the COM boundary
/// would bring <c>explorer.exe</c> down (CLAUDE.md 6.5).
/// </remarks>
internal abstract partial class ExplorerCommandBase : IExplorerCommand
{
    /// <summary>Label shown in the menu. Must be instant, with no disk access.</summary>
    protected abstract string Title { get; }

    /// <summary>
    /// Icon resource in <c>path,index</c> form, or <see langword="null"/> for none.
    /// </summary>
    protected virtual string? IconResource => null;

    /// <summary>The command's flags.</summary>
    protected virtual ExplorerCommandFlags Flags => ExplorerCommandFlags.Default;

    /// <summary>Runs the command's action on the selected folders.</summary>
    /// <param name="paths">The selected folders, already resolved.</param>
    protected virtual void Execute(IReadOnlyList<string> paths)
    {
    }

    /// <summary>The command's subcommands, when it has any.</summary>
    /// <returns>The child commands, or an empty list.</returns>
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
            // The entry only makes sense on real folders: not on a file, not on "This PC", not
            // on a library (CLAUDE.md 4.4).
            pCmdState = ShellSelection.IsFileSystemFolderSelection(psiItemArray)
                ? (uint)ExplorerCommandState.Enabled
                : (uint)ExplorerCommandState.Hidden;

            return HResult.Ok;
        }
        catch (Exception e)
        {
            ShellServices.Log.Error("GetState failed.", e);
            pCmdState = (uint)ExplorerCommandState.Hidden;
            return HResult.Ok;
        }
    }

    /// <inheritdoc/>
    public int Invoke(IntPtr psiItemArray, IntPtr pbc)
    {
        // Global safety net: this is where an exception would bring Explorer down.
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
            ShellServices.Log.Error($"Invoke failed for \"{Title}\".", e);
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
            ShellServices.Log.Error($"EnumSubCommands failed for \"{Title}\".", e);
            return HResult.Fail;
        }
    }
}
