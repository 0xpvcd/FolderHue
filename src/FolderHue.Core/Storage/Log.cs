using System.Globalization;
using System.Text;

namespace FolderHue.Core.Storage;

/// <summary>
/// Diagnostic log, capped in size and guaranteed not to throw.
/// </summary>
/// <remarks>
/// This log is written from inside <c>explorer.exe</c>: none of its methods may throw, not even on
/// a full disk or a locked file (CLAUDE.md 6.5). Every write error is swallowed.
/// </remarks>
public sealed class Log
{
    private const long MaxBytes = 256 * 1024;

    private readonly string _filePath;
    private readonly object _gate = new();

    /// <summary>Creates a log backed by a file.</summary>
    /// <param name="filePath">Path of the log file.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    public Log(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _filePath = filePath;
    }

    /// <summary>Default log for the current machine.</summary>
    public static Log Default { get; } = new(AppPaths.Default.LogFile);

    /// <summary>Writes an informational line.</summary>
    /// <param name="message">The message. <see langword="null"/> is ignored.</param>
    public void Info(string message) => Write("INFO ", message, null);

    /// <summary>Writes a warning line.</summary>
    /// <param name="message">The message. <see langword="null"/> is ignored.</param>
    public void Warn(string message) => Write("WARN ", message, null);

    /// <summary>Writes an error line, with the associated exception when one is supplied.</summary>
    /// <param name="message">The message describing the context.</param>
    /// <param name="exception">The exception to record, or <see langword="null"/>.</param>
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
            // An unavailable log must never interrupt the operation in progress.
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
            // Rotation impossible: keep writing to the current file.
        }
    }
}
