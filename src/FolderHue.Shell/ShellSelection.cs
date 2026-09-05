using System.Runtime.InteropServices;
using FolderHue.Shell.Com;

namespace FolderHue.Shell;

/// <summary>
/// Reads the selection the user right-clicked.
/// </summary>
/// <remarks>
/// Every method is defensive: they run inside <c>explorer.exe</c> and return a neutral value
/// rather than throw (CLAUDE.md 6.5).
/// </remarks>
internal static class ShellSelection
{
    /// <summary>Full file system path. <c>SIGDN_FILESYSPATH</c>, shobjidl_core.h.</summary>
    private const uint SigdnFileSysPath = 0x80058000;

    /// <summary>The item is a folder. <c>SFGAO_FOLDER</c>, shobjidl_core.h.</summary>
    private const uint SfgaoFolder = 0x20000000;

    /// <summary>The item exists in the file system. <c>SFGAO_FILESYSTEM</c>, shobjidl_core.h.</summary>
    private const uint SfgaoFileSystem = 0x40000000;

    /// <summary>Combines the attributes with a logical AND. <c>SIATTRIBFLAGS_AND</c>, shobjidl_core.h.</summary>
    private const uint SiAttribFlagsAnd = 0x1;

    /// <summary>
    /// Indicates whether the selection holds nothing but file system folders.
    /// </summary>
    /// <param name="psiItemArray">Native pointer to an <c>IShellItemArray</c>.</param>
    /// <returns>
    /// <see langword="false"/> as soon as one item is not a real folder: a file, "This PC", a
    /// library.
    /// </returns>
    /// <remarks>
    /// The call goes through <c>IShellItemArray::GetAttributes</c>, which combines attributes the
    /// shell already knows: no disk access, which is essential here since the method runs every
    /// time the menu opens (CLAUDE.md 4.4).
    /// </remarks>
    internal static bool IsFileSystemFolderSelection(IntPtr psiItemArray)
    {
        if (psiItemArray == IntPtr.Zero)
        {
            return false;
        }

        object? wrapper = null;

        try
        {
            wrapper = ShellComWrappers.Instance.GetOrCreateObjectForComInstance(
                psiItemArray, CreateObjectFlags.UniqueInstance);

            if (wrapper is not IShellItemArray array)
            {
                return false;
            }

            const uint mask = SfgaoFolder | SfgaoFileSystem;

            if (array.GetAttributes(SiAttribFlagsAnd, mask, out uint attributes) < 0)
            {
                return false;
            }

            return (attributes & mask) == mask;
        }
        catch (Exception e) when (e is COMException or InvalidCastException or NotSupportedException)
        {
            return false;
        }
        finally
        {
            Dispose(wrapper);
        }
    }

    /// <summary>
    /// Returns the paths of the selected folders.
    /// </summary>
    /// <param name="psiItemArray">Native pointer to an <c>IShellItemArray</c>.</param>
    /// <returns>
    /// The file system paths. Items that have none are silently skipped.
    /// </returns>
    internal static List<string> GetPaths(IntPtr psiItemArray)
    {
        var paths = new List<string>();

        if (psiItemArray == IntPtr.Zero)
        {
            return paths;
        }

        object? wrapper = null;

        try
        {
            wrapper = ShellComWrappers.Instance.GetOrCreateObjectForComInstance(
                psiItemArray, CreateObjectFlags.UniqueInstance);

            if (wrapper is not IShellItemArray array)
            {
                return paths;
            }

            if (array.GetCount(out uint count) < 0)
            {
                return paths;
            }

            for (uint i = 0; i < count; i++)
            {
                if (array.GetItemAt(i, out IShellItem item) < 0 || item is null)
                {
                    continue;
                }

                try
                {
                    string? path = GetPath(item);
                    if (!string.IsNullOrEmpty(path))
                    {
                        paths.Add(path);
                    }
                }
                finally
                {
                    Dispose(item);
                }
            }
        }
        catch (Exception e) when (e is COMException or InvalidCastException or NotSupportedException)
        {
            // An unreadable selection becomes an empty list, never an exception.
        }
        finally
        {
            Dispose(wrapper);
        }

        return paths;
    }

    private static string? GetPath(IShellItem item)
    {
        IntPtr buffer = IntPtr.Zero;

        try
        {
            if (item.GetDisplayName(SigdnFileSysPath, out buffer) < 0 || buffer == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUni(buffer);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                // GetDisplayName allocates with CoTaskMemAlloc: the caller frees it.
                Marshal.FreeCoTaskMem(buffer);
            }
        }
    }

    private static void Dispose(object? wrapper)
    {
        if (wrapper is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
