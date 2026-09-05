using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FolderHue.Shell;

/// <summary>
/// The single P/Invoke entry point of <c>FolderHue.Shell</c> (CLAUDE.md 7).
/// </summary>
/// <remarks>
/// COM interfaces do not go through here: the source generators project them in
/// <c>Com/ComInterop.cs</c>.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static unsafe partial class NativeMethods
{
    /// <summary>The name passed is really an address. <c>GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS</c>, libloaderapi.h.</summary>
    private const uint GetModuleHandleExFlagFromAddress = 0x00000004;

    /// <summary>Do not bump the module's reference count. <c>..._UNCHANGED_REFCOUNT</c>, libloaderapi.h.</summary>
    private const uint GetModuleHandleExFlagUnchangedRefcount = 0x00000002;

    /// <summary>
    /// Finds the module containing a given address.
    /// </summary>
    /// <remarks>
    /// Win32: <c>GetModuleHandleExW</c>, kernel32.dll, header libloaderapi.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/libloaderapi/nf-libloaderapi-getmodulehandleexw
    /// </remarks>
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleExW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetModuleHandleEx(uint dwFlags, IntPtr lpModuleName, out IntPtr phModule);

    /// <summary>
    /// Returns the full file path of a loaded module.
    /// </summary>
    /// <remarks>
    /// Win32: <c>GetModuleFileNameW</c>, kernel32.dll, header libloaderapi.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/libloaderapi/nf-libloaderapi-getmodulefilenamew
    /// </remarks>
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", SetLastError = true)]
    private static partial uint GetModuleFileName(IntPtr hModule, char* lpFilename, uint nSize);

    /// <summary>
    /// The directory holding <c>FolderHue.Shell.dll</c>.
    /// </summary>
    /// <returns>The DLL's directory, or <see langword="null"/> when it could not be determined.</returns>
    /// <remarks>
    /// <c>AppContext.BaseDirectory</c> will not do: loaded inside a host process it returns the
    /// <b>host's</b> directory — <c>C:\Windows\System32</c> in practice — and not the DLL's. So we
    /// walk back to the module from the address of one of our own functions.
    /// </remarks>
    internal static string? GetModuleDirectory()
    {
        try
        {
            // A function exported by this module serves as the landmark.
            delegate* unmanaged<int> anchor = &Exports.DllCanUnloadNow;

            if (!GetModuleHandleEx(
                    GetModuleHandleExFlagFromAddress | GetModuleHandleExFlagUnchangedRefcount,
                    (IntPtr)anchor,
                    out IntPtr module)
                || module == IntPtr.Zero)
            {
                return null;
            }

            const int capacity = 32768; // longueur maximale d'un chemin etendu
            char[] buffer = new char[capacity];

            fixed (char* pointer = buffer)
            {
                uint length = GetModuleFileName(module, pointer, capacity);

                if (length == 0 || length >= capacity)
                {
                    return null;
                }

                return Path.GetDirectoryName(new string(pointer, 0, (int)length));
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }
}
