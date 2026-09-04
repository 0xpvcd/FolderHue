using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FolderHue.Shell;

/// <summary>
/// Unique point d'entree P/Invoke de <c>FolderHue.Shell</c> (CLAUDE.md §7).
/// </summary>
/// <remarks>
/// Les interfaces COM ne passent pas par ici : elles sont projetees par les generateurs de source
/// dans <c>Com/ComInterop.cs</c>.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static unsafe partial class NativeMethods
{
    /// <summary>Le nom passe est en fait une adresse. <c>GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS</c>, libloaderapi.h.</summary>
    private const uint GetModuleHandleExFlagFromAddress = 0x00000004;

    /// <summary>Ne pas incrementer le compteur de references du module. <c>..._UNCHANGED_REFCOUNT</c>, libloaderapi.h.</summary>
    private const uint GetModuleHandleExFlagUnchangedRefcount = 0x00000002;

    /// <summary>
    /// Retrouve le module contenant une adresse donnee.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetModuleHandleExW</c>, kernel32.dll, en-tete libloaderapi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/libloaderapi/nf-libloaderapi-getmodulehandleexw
    /// </remarks>
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleExW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetModuleHandleEx(uint dwFlags, IntPtr lpModuleName, out IntPtr phModule);

    /// <summary>
    /// Retourne le chemin complet du fichier d'un module charge.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetModuleFileNameW</c>, kernel32.dll, en-tete libloaderapi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/libloaderapi/nf-libloaderapi-getmodulefilenamew
    /// </remarks>
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", SetLastError = true)]
    private static partial uint GetModuleFileName(IntPtr hModule, char* lpFilename, uint nSize);

    /// <summary>
    /// Dossier contenant <c>FolderHue.Shell.dll</c>.
    /// </summary>
    /// <returns>Le dossier de la DLL, ou <see langword="null"/> s'il n'a pas pu etre determine.</returns>
    /// <remarks>
    /// <c>AppContext.BaseDirectory</c> ne convient pas : charge dans un processus hote, il rend le
    /// dossier de <b>l'hote</b> — <c>C:\Windows\System32</c> en pratique — et non celui de la DLL.
    /// On remonte donc au module a partir de l'adresse d'une de nos propres fonctions.
    /// </remarks>
    internal static string? GetModuleDirectory()
    {
        try
        {
            // Une fonction exportee de ce module sert de point de repere.
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
