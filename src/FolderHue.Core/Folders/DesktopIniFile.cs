using System.Runtime.Versioning;
using System.Text;

namespace FolderHue.Core.Folders;

/// <summary>
/// Reads and writes a <c>desktop.ini</c> on disk, encoding and attributes included.
/// </summary>
/// <remarks>
/// The original encoding is detected and then <b>preserved</b> on rewrite: an ANSI file written by
/// another tool must not be converted behind the user's back, or values such as
/// <c>LocalizedResourceName</c> would become unreadable.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class DesktopIniFile
{
    /// <summary>Name of the customisation file Explorer reads.</summary>
    public const string FileName = "desktop.ini";

    /// <summary>Name of the backup we create before any rewrite (CLAUDE.md 6.1).</summary>
    public const string BackupFileName = "desktop.ini.folderhue.bak";

    /// <summary>
    /// Encoding used for a file we create from scratch.
    /// </summary>
    /// <remarks>
    /// Little-endian UTF-16 with a BOM: the Unicode form Win32's <c>PrivateProfile</c> APIs
    /// recognise reliably.
    /// </remarks>
    public static Encoding DefaultEncoding { get; } = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);

    /// <summary>Path of a folder's <c>desktop.ini</c>.</summary>
    /// <param name="folderPath">Path of the folder.</param>
    /// <returns>The full path of the file, whether it exists or not.</returns>
    public static string PathFor(string folderPath) => Path.Combine(folderPath, FileName);

    /// <summary>Path of the backup of a folder's <c>desktop.ini</c>.</summary>
    /// <param name="folderPath">Path of the folder.</param>
    /// <returns>The full path of the backup, whether it exists or not.</returns>
    public static string BackupPathFor(string folderPath) => Path.Combine(folderPath, BackupFileName);

    /// <summary>
    /// Reads a <c>desktop.ini</c>, or returns an empty document when the file does not exist.
    /// </summary>
    /// <param name="filePath">Path of the file.</param>
    /// <returns>The parsed content and the detected encoding.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    /// <exception cref="IOException">The file exists but could not be read.</exception>
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
    /// Writes a <c>desktop.ini</c> and puts its Hidden + System attributes back.
    /// </summary>
    /// <param name="filePath">Path of the file.</param>
    /// <param name="document">The content and the encoding to use.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The write failed.</exception>
    public static void Write(string filePath, DesktopIniDocument document)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(document);

        // A hidden file cannot be opened for writing: clear the attributes first.
        FolderAttributes.ClearFileFlags(filePath);

        File.WriteAllText(filePath, document.Content.ToText(), document.Encoding);

        FolderAttributes.MakeHiddenSystem(filePath);
    }

    /// <summary>Deletes a <c>desktop.ini</c>, attributes included.</summary>
    /// <param name="filePath">Path of the file.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    /// <exception cref="IOException">The deletion failed.</exception>
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
    /// Detects a file's encoding from its byte order mark.
    /// </summary>
    /// <param name="bytes">The raw file content.</param>
    /// <param name="bomLength">Receives the BOM length, or 0 when there is none.</param>
    /// <returns>The detected encoding.</returns>
    /// <remarks>
    /// With no BOM, strict UTF-8 is attempted; when that fails the file is ANSI and we fall back to
    /// Latin-1, which has the advantage of being built into the runtime and of never losing a byte.
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
/// A <c>desktop.ini</c> read from disk: its content and the encoding it was written in.
/// </summary>
/// <param name="Content">The parsed content.</param>
/// <param name="Encoding">The encoding to reuse when rewriting.</param>
public sealed record DesktopIniDocument(DesktopIni Content, Encoding Encoding);
