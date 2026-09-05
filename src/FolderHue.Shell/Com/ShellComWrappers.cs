using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace FolderHue.Shell.Com;

/// <summary>
/// The COM server's single <c>ComWrappers</c> instance.
/// </summary>
/// <remarks>
/// <see cref="StrategyBasedComWrappers"/> relies on the vtables <c>[GeneratedComInterface]</c>
/// emits at compile time: this is the <c>ComWrappers</c> route CLAUDE.md 2.1 requires, free of
/// reflection and therefore NativeAOT-compatible.
/// </remarks>
internal static class ShellComWrappers
{
    /// <summary>The instance shared by the whole server.</summary>
    /// <remarks>
    /// One instance for the entire process: two instances would produce two distinct CCWs for the
    /// same object, and COM identity would no longer hold.
    /// </remarks>
    internal static ComWrappers Instance { get; } = new StrategyBasedComWrappers();

    /// <summary>
    /// Exposes a managed object as a COM interface pointer.
    /// </summary>
    /// <param name="instance">The object to expose.</param>
    /// <param name="riid">The interface requested.</param>
    /// <param name="pointer">Receives the pointer, already reference-counted.</param>
    /// <returns>An HRESULT.</returns>
    internal static int GetComInterface(object instance, in Guid riid, out IntPtr pointer)
    {
        pointer = IntPtr.Zero;

        IntPtr unknown = Instance.GetOrCreateComInterfaceForObject(instance, CreateComInterfaceFlags.None);

        if (unknown == IntPtr.Zero)
        {
            return HResult.NoInterface;
        }

        try
        {
            Guid iid = riid;
            return Marshal.QueryInterface(unknown, ref iid, out pointer);
        }
        finally
        {
            // GetOrCreateComInterfaceForObject returns one reference; the QueryInterface above
            // added a second, which is the one the caller receives.
            Marshal.Release(unknown);
        }
    }

    /// <summary>
    /// Exposes a managed object as an <c>IExplorerCommand</c> pointer.
    /// </summary>
    /// <param name="command">The command to expose.</param>
    /// <param name="pointer">Receives the pointer, already reference-counted.</param>
    /// <returns>An HRESULT.</returns>
    internal static int GetExplorerCommand(object command, out IntPtr pointer)
        => GetComInterface(command, Guids.IExplorerCommand, out pointer);
}
