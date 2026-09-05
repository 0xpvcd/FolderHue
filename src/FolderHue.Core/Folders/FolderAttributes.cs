using System.Runtime.Versioning;

namespace FolderHue.Core.Folders;

/// <summary>
/// Sets and clears the file attributes a folder customisation requires.
/// </summary>
/// <remarks>
/// Three <b>cumulative</b> conditions are needed before Explorer honours a <c>desktop.ini</c>
/// (CLAUDE.md 4.1):
/// <list type="number">
///   <item><description>the <c>desktop.ini</c> file exists at the root of the folder;</description></item>
///   <item><description>that file carries the Hidden + System attributes;</description></item>
///   <item><description>the <b>folder itself</b> carries ReadOnly or System.</description></item>
/// </list>
/// Forgetting the third one is mistake number one on this kind of project: without it,
/// <c>desktop.ini</c> is ignored outright.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class FolderAttributes
{
    /// <summary>Marks a file hidden and system.</summary>
    /// <param name="filePath">Path of the file.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    public static void MakeHiddenSystem(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            return;
        }

        File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Hidden | FileAttributes.System);
    }

    /// <summary>
    /// Clears the Hidden, System and ReadOnly attributes of a file.
    /// </summary>
    /// <param name="filePath">Path of the file.</param>
    /// <remarks>
    /// Required before any rewrite or deletion: on Windows, opening a hidden file for writing
    /// throws <see cref="UnauthorizedAccessException"/>.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    public static void ClearFileFlags(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            return;
        }

        FileAttributes attributes = File.GetAttributes(filePath);
        FileAttributes cleared = attributes & ~(FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly);

        if (cleared != attributes)
        {
            File.SetAttributes(filePath, cleared);
        }
    }

    /// <summary>Indicates whether a folder already carries ReadOnly or System.</summary>
    /// <param name="folderPath">Path of the folder.</param>
    /// <returns>
    /// <see langword="true"/> when Explorer will already read that folder's <c>desktop.ini</c>
    /// without us touching its attributes.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> is empty.</exception>
    public static bool IsFolderCustomizable(string folderPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(folderPath);

        if (!Directory.Exists(folderPath))
        {
            return false;
        }

        FileAttributes attributes = File.GetAttributes(folderPath);
        return (attributes & (FileAttributes.ReadOnly | FileAttributes.System)) != 0;
    }

    /// <summary>
    /// Sets the ReadOnly attribute on a folder when it carries neither ReadOnly nor System.
    /// </summary>
    /// <param name="folderPath">Path of the folder.</param>
    /// <returns>
    /// <see langword="true"/> when <b>we</b> just set the attribute. That value must be kept in the
    /// journal: it alone permits removing it later (CLAUDE.md 6.3).
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> is empty.</exception>
    public static bool EnsureFolderCustomizable(string folderPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(folderPath);

        if (IsFolderCustomizable(folderPath))
        {
            return false;
        }

        File.SetAttributes(folderPath, File.GetAttributes(folderPath) | FileAttributes.ReadOnly);
        return true;
    }

    /// <summary>Clears the ReadOnly attribute of a folder.</summary>
    /// <param name="folderPath">Path of the folder.</param>
    /// <remarks>
    /// Call this only when the journal says we were the ones who set it: clearing it blindly would
    /// break a customisation the user had made themselves.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> is empty.</exception>
    public static void ClearFolderReadOnly(string folderPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(folderPath);

        if (!Directory.Exists(folderPath))
        {
            return;
        }

        FileAttributes attributes = File.GetAttributes(folderPath);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(folderPath, attributes & ~FileAttributes.ReadOnly);
        }
    }
}
