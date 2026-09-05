using System.Runtime.InteropServices;
using FolderHue.Shell.Com;
using FolderHue.Shell.Commands;

namespace FolderHue.Shell;

/// <summary>
/// The native entry points the DLL exports.
/// </summary>
/// <remarks>
/// <c>[UnmanagedCallersOnly]</c> emits real native exports in the NativeAOT image: that is what
/// replaces registration through <c>RegAsm</c>, which AOT cannot do (CLAUDE.md 2.1).
/// </remarks>
internal static class Exports
{
    /// <summary>
    /// Returns the class factory for the requested CLSID.
    /// </summary>
    /// <param name="rclsid">CLSID requested by COM.</param>
    /// <param name="riid">Interface requested, in practice <c>IClassFactory</c>.</param>
    /// <param name="ppv">Receives the interface pointer.</param>
    /// <returns>An HRESULT.</returns>
    /// <remarks>
    /// Win32: <c>DllGetClassObject</c>, combaseapi.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/combaseapi/nf-combaseapi-dllgetclassobject
    /// </remarks>
    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
    public static unsafe int DllGetClassObject(Guid* rclsid, Guid* riid, IntPtr* ppv)
    {
        if (ppv is null)
        {
            return HResult.Pointer;
        }

        *ppv = IntPtr.Zero;

        if (rclsid is null || riid is null)
        {
            return HResult.Pointer;
        }

        try
        {
            // The server exposes a single class, the root command. It is rendered by the classic
            // menu, which is where a registry verb appears on both Windows 10 and 11.
            Func<object>? create = null;

            if (*rclsid == Guids.RootCommandClsid)
            {
                create = static () => new RootCommand();
            }

            if (create is null)
            {
                // Unknown CLSID: this is what COM expects in order to move on to the next server.
                return unchecked((int)0x80040154); // REGDB_E_CLASSNOTREG
            }

            int hr = ShellComWrappers.GetComInterface(new ClassFactory(create), *riid, out IntPtr factory);
            *ppv = factory;
            return hr;
        }
        catch (Exception e)
        {
            // No exception may cross the native boundary: Explorer would come down with it.
            ShellServices.Log.Error("DllGetClassObject failed.", e);
            return HResult.Fail;
        }
    }

    /// <summary>
    /// Indicates whether the DLL may be unloaded.
    /// </summary>
    /// <returns>Always <c>S_FALSE</c>.</returns>
    /// <remarks>
    /// Win32: <c>DllCanUnloadNow</c>, combaseapi.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/combaseapi/nf-combaseapi-dllcanunloadnow
    /// <para>
    /// The NativeAOT runtime does not reinitialise after an unload, so we always refuse. That is
    /// the recommended behaviour for a managed COM server.
    /// </para>
    /// </remarks>
    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
    public static int DllCanUnloadNow() => HResult.False;
}
