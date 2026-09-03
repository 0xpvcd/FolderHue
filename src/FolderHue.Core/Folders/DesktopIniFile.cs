using System.Runtime.Versioning;
using System.Text;

namespace FolderHue.Core.Folders;

/// <summary>
/// Lecture et ecriture d'un <c>desktop.ini</c> sur disque, encodage et attributs compris.
/// </summary>
/// <remarks>
/// L'encodage d'origine est detecte puis <b>conserve</b> a la reecriture : un fichier ANSI ecrit
/// par un autre outil ne doit pas etre converti dans notre dos, sous peine de rendre illisibles
/// des valeurs comme <c>LocalizedResourceName</c>.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class DesktopIniFile
{
    /// <summary>Nom du fichier de personnalisation lu par l'Explorateur.</summary>
    public const string FileName = "desktop.ini";

    /// <summary>Nom de la sauvegarde que nous creons avant toute reecriture (CLAUDE.md §6.1).</summary>
    public const string BackupFileName = "desktop.ini.folderhue.bak";

    /// <summary>
    /// Encodage utilise pour un fichier que nous creons de toutes pieces.
    /// </summary>
    /// <remarks>
    /// UTF-16 petit-boutiste avec BOM : c'est le format Unicode que les API <c>PrivateProfile</c>
    /// de Win32 reconnaissent de facon fiable.
    /// </remarks>
    public static Encoding DefaultEncoding { get; } = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);

    /// <summary>Chemin du <c>desktop.ini</c> d'un dossier.</summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <returns>Le chemin complet du fichier, qu'il existe ou non.</returns>
    public static string PathFor(string folderPath) => Path.Combine(folderPath, FileName);

    /// <summary>Chemin de la sauvegarde du <c>desktop.ini</c> d'un dossier.</summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <returns>Le chemin complet de la sauvegarde, qu'elle existe ou non.</returns>
    public static string BackupPathFor(string folderPath) => Path.Combine(folderPath, BackupFileName);

    /// <summary>
    /// Lit un <c>desktop.ini</c>, ou retourne un document vide si le fichier n'existe pas.
    /// </summary>
    /// <param name="filePath">Chemin du fichier.</param>
    /// <returns>Le contenu analyse et l'encodage detecte.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> est vide.</exception>
    /// <exception cref="IOException">Le fichier existe mais n'a pas pu etre lu.</exception>
    public static DesktopIniDocument Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            return new DesktopIniDocument(new DesktopIni(), DefaultEncoding);
        }

        byte[] bytes = File.ReadAllBytes(filePath);
        Encoding encoding = DetectEncoding(bytes, out int bomLength);
        string text = encoding.GetString(bytes, bomLength, bytes.Length - bomLength);

        return new DesktopIniDocument(DesktopIni.Parse(text), encoding);
    }

    /// <summary>
    /// Ecrit un <c>desktop.ini</c> et lui repose les attributs Hidden + System.
    /// </summary>
    /// <param name="filePath">Chemin du fichier.</param>
    /// <param name="document">Le contenu et l'encodage a utiliser.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> est vide.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> vaut <see langword="null"/>.</exception>
    /// <exception cref="IOException">L'ecriture a echoue.</exception>
    public static void Write(string filePath, DesktopIniDocument document)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(document);

        // Un fichier cache ne peut pas etre ouvert en ecriture : on degage les attributs d'abord.
        FolderAttributes.ClearFileFlags(filePath);

        File.WriteAllText(filePath, document.Content.ToText(), document.Encoding);

        FolderAttributes.MakeHiddenSystem(filePath);
    }

    /// <summary>Supprime un <c>desktop.ini</c>, attributs compris.</summary>
    /// <param name="filePath">Chemin du fichier.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> est vide.</exception>
    /// <exception cref="IOException">La suppression a echoue.</exception>
    public static void Delete(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            return;
        }

        FolderAttributes.ClearFileFlags(filePath);
        File.Delete(filePath);
    }

    /// <summary>
    /// Detecte l'encodage d'un fichier a partir de sa marque d'ordre des octets.
    /// </summary>
    /// <param name="bytes">Le contenu brut du fichier.</param>
    /// <param name="bomLength">Recoit la longueur de la BOM, ou 0 s'il n'y en a pas.</param>
    /// <returns>L'encodage detecte.</returns>
    /// <remarks>
    /// Sans BOM, on tente l'UTF-8 strict ; s'il echoue, le fichier est de l'ANSI et on retombe sur
    /// Latin-1, qui a l'avantage d'etre integre au runtime et de ne jamais perdre d'octet.
    /// </remarks>
    public static Encoding DetectEncoding(ReadOnlySpan<byte> bytes, out int bomLength)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            bomLength = 2;
            return new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            bomLength = 2;
            return new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            bomLength = 3;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        bomLength = 0;

        try
        {
            new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1;
        }
    }
}

/// <summary>
/// Un <c>desktop.ini</c> lu sur disque : son contenu et l'encodage dans lequel il etait ecrit.
/// </summary>
/// <param name="Content">Le contenu analyse.</param>
/// <param name="Encoding">L'encodage a reutiliser lors de la reecriture.</param>
public sealed record DesktopIniDocument(DesktopIni Content, Encoding Encoding);
