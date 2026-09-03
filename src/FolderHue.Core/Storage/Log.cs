using System.Globalization;
using System.Text;

namespace FolderHue.Core.Storage;

/// <summary>
/// Journal de diagnostic, plafonne en taille et garanti sans exception.
/// </summary>
/// <remarks>
/// Ce journal est ecrit depuis <c>explorer.exe</c> : aucune de ses methodes ne doit lever, meme
/// disque plein ou fichier verrouille (CLAUDE.md §6.5). Toute erreur d'ecriture est avalee.
/// </remarks>
public sealed class Log
{
    private const long MaxBytes = 256 * 1024;

    private readonly string _filePath;
    private readonly object _gate = new();

    /// <summary>Cree un journal adosse a un fichier.</summary>
    /// <param name="filePath">Chemin du fichier de journal.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> est vide.</exception>
    public Log(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _filePath = filePath;
    }

    /// <summary>Journal par defaut de la machine courante.</summary>
    public static Log Default { get; } = new(AppPaths.Default.LogFile);

    /// <summary>Ecrit une ligne d'information.</summary>
    /// <param name="message">Le message. <see langword="null"/> est ignore.</param>
    public void Info(string message) => Write("INFO ", message, null);

    /// <summary>Ecrit une ligne d'avertissement.</summary>
    /// <param name="message">Le message. <see langword="null"/> est ignore.</param>
    public void Warn(string message) => Write("WARN ", message, null);

    /// <summary>Ecrit une ligne d'erreur, avec l'exception associee si elle est fournie.</summary>
    /// <param name="message">Le message decrivant le contexte.</param>
    /// <param name="exception">L'exception a consigner, ou <see langword="null"/>.</param>
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        try
        {
            lock (_gate)
            {
                string? directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Rotate();

                var builder = new StringBuilder();
                builder.Append(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                       .Append(" [").Append(level).Append("] ")
                       .Append("pid:").Append(Environment.ProcessId).Append(' ')
                       .AppendLine(message);

                if (exception is not null)
                {
                    builder.AppendLine(exception.ToString());
                }

                File.AppendAllText(_filePath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Un journal indisponible ne doit jamais interrompre l'operation en cours.
        }
    }

    private void Rotate()
    {
        var info = new FileInfo(_filePath);
        if (!info.Exists || info.Length < MaxBytes)
        {
            return;
        }

        string previous = _filePath + ".1";

        try
        {
            File.Move(_filePath, previous, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Rotation impossible : on continue d'ecrire dans le fichier courant.
        }
    }
}
