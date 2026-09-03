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

    /// <summary>Element de menu textuel. <c>MF_STRING</c>, winuser.h.</summary>
    internal const uint MfString = 0x00000000;

    /// <summary>Sous-menu. <c>MF_POPUP</c>, winuser.h.</summary>
    internal const uint MfPopup = 0x00000010;

    /// <summary>Separateur. <c>MF_SEPARATOR</c>, winuser.h.</summary>
    internal const uint MfSeparator = 0x00000800;

    /// <summary>Position et non identifiant. <c>MF_BYPOSITION</c>, winuser.h.</summary>
    internal const uint MfByPosition = 0x00000400;

    /// <summary>
    /// Cree un menu deroulant vide.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>CreatePopupMenu</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-createpopupmenu
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "CreatePopupMenu", SetLastError = true)]
    internal static partial IntPtr CreatePopupMenu();

    /// <summary>
    /// Ajoute un element a la fin d'un menu.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>AppendMenuW</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-appendmenuw
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    /// <summary>
    /// Insere un element a une position donnee.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>InsertMenuW</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-insertmenuw
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "InsertMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    /// <summary>
    /// Construit un <c>IShellItemArray</c> a partir de l'objet de donnees d'une selection.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>SHCreateShellItemArrayFromDataObject</c>, shobjidl_core.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-shcreateshellitemarrayfromdataobject
    /// C'est ce qui permet au handler herite de reutiliser exactement le meme code de lecture de
    /// selection que la commande moderne.
    /// </remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHCreateShellItemArrayFromDataObject")]
    internal static partial int SHCreateShellItemArrayFromDataObject(IntPtr pdo, in Guid riid, out IntPtr ppv);

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
