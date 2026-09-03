using System.Runtime.Versioning;

namespace FolderHue.Core.Folders;

/// <summary>
/// Pose et retire les attributs de fichier exiges par la personnalisation d'un dossier.
/// </summary>
/// <remarks>
/// Trois conditions <b>cumulatives</b> sont necessaires pour que l'Explorateur honore un
/// <c>desktop.ini</c> (CLAUDE.md §4.1) :
/// <list type="number">
///   <item><description>le fichier <c>desktop.ini</c> existe a la racine du dossier ;</description></item>
///   <item><description>ce fichier porte les attributs Hidden + System ;</description></item>
///   <item><description>le <b>dossier lui-meme</b> porte ReadOnly ou System.</description></item>
/// </list>
/// L'oubli du troisieme point est l'erreur numero un sur ce type de projet : sans lui,
/// <c>desktop.ini</c> est purement et simplement ignore.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class FolderAttributes
{
    /// <summary>Rend un fichier cache et systeme.</summary>
    /// <param name="filePath">Chemin du fichier.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> est vide.</exception>
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
    /// Retire les attributs Hidden, System et ReadOnly d'un fichier.
    /// </summary>
    /// <param name="filePath">Chemin du fichier.</param>
    /// <remarks>
    /// Necessaire avant toute reecriture ou suppression : sous Windows, ouvrir en ecriture un
    /// fichier cache leve <see cref="UnauthorizedAccessException"/>.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> est vide.</exception>
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

    /// <summary>Indique si un dossier porte deja ReadOnly ou System.</summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <returns>
    /// <see langword="true"/> si l'Explorateur lira deja le <c>desktop.ini</c> de ce dossier sans
    /// que nous ayons a toucher a ses attributs.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> est vide.</exception>
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
    /// Pose l'attribut ReadOnly sur un dossier s'il ne porte ni ReadOnly ni System.
    /// </summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <returns>
    /// <see langword="true"/> si <b>nous</b> venons de poser l'attribut. Cette valeur doit etre
    /// conservee dans le journal : elle seule autorisera a le retirer plus tard (CLAUDE.md §6.3).
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> est vide.</exception>
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

    /// <summary>Retire l'attribut ReadOnly d'un dossier.</summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <remarks>
    /// A n'appeler que si le journal indique que c'est nous qui l'avons pose : le retirer
    /// aveuglement casserait une personnalisation preexistante de l'utilisateur.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> est vide.</exception>
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
