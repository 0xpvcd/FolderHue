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

    /// <summary>
    /// Repositionne la date de derniere ecriture d'un dossier a l'instant present.
    /// </summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <remarks>
    /// Sans cet appel, changer la couleur d'un dossier <b>deja</b> colorise ne se voit pas.
    /// <para>
    /// Reecrire <c>desktop.ini</c> ne touche que le contenu du fichier : l'entree de repertoire du
    /// dossier, elle, ne bouge pas. Une creation ou une suppression la modifie, une reecriture sur
    /// place non. Mesure sur trois applications successives : la premiere, qui cree le fichier,
    /// avance la date du dossier ; les deux suivantes la laissent a la milliseconde pres.
    /// </para>
    /// <para>
    /// C'est exactement le motif observe a l'ecran : dans une serie d'applications sur le meme
    /// dossier, seule la premiere repeignait. L'explication retenue est que <c>SHChangeNotify</c>
    /// invite l'Explorateur a regarder de nouveau, mais qu'il consulte d'abord ce qu'il a en cache
    /// pour ce dossier, n'y voit aucune date plus recente et en conclut qu'il n'y a rien a relire.
    /// Avancer la date lui retire cette echappatoire. Le mecanisme interne n'est pas documente ;
    /// ce qui est mesure, c'est l'horodatage, et ce qui se verifie, c'est l'icone a l'ecran.
    /// </para>
    /// <para>
    /// L'echec est absorbe : un dossier dont on ne peut pas changer l'horodatage reste colorise
    /// correctement, il faudra seulement un F5 pour le voir.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> est vide.</exception>
    public static void TouchFolder(string folderPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(folderPath);

        if (!Directory.Exists(folderPath))
        {
            return;
        }

        try
        {
            Directory.SetLastWriteTimeUtc(folderPath, DateTime.UtcNow);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
        }
    }
}
