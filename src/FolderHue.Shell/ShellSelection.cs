using System.Runtime.InteropServices;
using FolderHue.Shell.Com;

namespace FolderHue.Shell;

/// <summary>
/// Lecture de la selection sur laquelle l'utilisateur a fait un clic droit.
/// </summary>
/// <remarks>
/// Toutes les methodes sont defensives : elles s'executent dans <c>explorer.exe</c> et retournent
/// une valeur neutre plutot que de lever (CLAUDE.md §6.5).
/// </remarks>
internal static class ShellSelection
{
    /// <summary>Chemin complet dans le systeme de fichiers. <c>SIGDN_FILESYSPATH</c>, shobjidl_core.h.</summary>
    private const uint SigdnFileSysPath = 0x80058000;

    /// <summary>L'element est un dossier. <c>SFGAO_FOLDER</c>, shobjidl_core.h.</summary>
    private const uint SfgaoFolder = 0x20000000;

    /// <summary>L'element existe dans le systeme de fichiers. <c>SFGAO_FILESYSTEM</c>, shobjidl_core.h.</summary>
    private const uint SfgaoFileSystem = 0x40000000;

    /// <summary>Combine les attributs par ET logique. <c>SIATTRIBFLAGS_AND</c>, shobjidl_core.h.</summary>
    private const uint SiAttribFlagsAnd = 0x1;

    /// <summary>
    /// Indique si la selection ne contient que des dossiers du systeme de fichiers.
    /// </summary>
    /// <param name="psiItemArray">Pointeur natif sur <c>IShellItemArray</c>.</param>
    /// <returns>
    /// <see langword="false"/> des qu'un element n'est pas un dossier reel : un fichier,
    /// « Ce PC », une bibliotheque.
    /// </returns>
    /// <remarks>
    /// L'appel passe par <c>IShellItemArray::GetAttributes</c>, qui combine les attributs deja
    /// connus du shell : aucun acces disque, ce qui est indispensable ici puisque la methode est
    /// appelee a chaque ouverture du menu (CLAUDE.md §4.4).
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
    /// Retourne les chemins des dossiers selectionnes.
    /// </summary>
    /// <param name="psiItemArray">Pointeur natif sur <c>IShellItemArray</c>.</param>
    /// <returns>
    /// Les chemins du systeme de fichiers. Les elements qui n'en ont pas sont ignores
    /// silencieusement.
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
            // Une selection illisible se traduit par une liste vide, jamais par une exception.
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
                // GetDisplayName alloue avec CoTaskMemAlloc : a l'appelant de liberer.
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
