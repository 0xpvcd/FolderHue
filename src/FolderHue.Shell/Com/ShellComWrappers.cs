using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace FolderHue.Shell.Com;

/// <summary>
/// Instance <c>ComWrappers</c> unique du serveur COM.
/// </summary>
/// <remarks>
/// <see cref="StrategyBasedComWrappers"/> s'appuie sur les vtables produites par
/// <c>[GeneratedComInterface]</c> a la compilation : c'est la voie <c>ComWrappers</c> exigee par
/// CLAUDE.md §2.1, sans reflexion et donc compatible NativeAOT.
/// </remarks>
internal static class ShellComWrappers
{
    /// <summary>L'instance partagee par tout le serveur.</summary>
    /// <remarks>
    /// Une seule instance pour tout le processus : deux instances produiraient deux CCW distincts
    /// pour un meme objet, et l'identite COM ne serait plus respectee.
    /// </remarks>
    internal static ComWrappers Instance { get; } = new StrategyBasedComWrappers();

    /// <summary>
    /// Expose un objet gere en tant que pointeur d'interface COM.
    /// </summary>
    /// <param name="instance">L'objet a exposer.</param>
    /// <param name="riid">L'interface demandee.</param>
    /// <param name="pointer">Recoit le pointeur, deja incremente en reference.</param>
    /// <returns>Un HRESULT.</returns>
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
            // GetOrCreateComInterfaceForObject rend une reference : le QueryInterface ci-dessus en
            // a pose une seconde, celle que l'appelant recevra.
            Marshal.Release(unknown);
        }
    }

    /// <summary>
    /// Expose un objet gere en tant que pointeur <c>IExplorerCommand</c>.
    /// </summary>
    /// <param name="command">La commande a exposer.</param>
    /// <param name="pointer">Recoit le pointeur, deja incremente en reference.</param>
    /// <returns>Un HRESULT.</returns>
    internal static int GetExplorerCommand(object command, out IntPtr pointer)
        => GetComInterface(command, Guids.IExplorerCommand, out pointer);
}
