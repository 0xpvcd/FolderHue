using System.Text.Json;

namespace FolderHue.Core.Storage;

/// <summary>
/// Local journal of colored folders, persisted in <c>applied.json</c>.
/// </summary>
/// <remarks>
/// Several writers are possible at once: one <c>Invoke</c> of the context menu handles N folders,
/// and <c>FolderHue.App</c> may be running alongside. Every operation therefore goes through a
/// named mutex and an atomic write (temporary file, then replace).
/// <para>
/// No method throws: an unreadable journal is treated as an empty one. Losing the record is
/// annoying; bringing <c>explorer.exe</c> down would be far worse (CLAUDE.md 6.5).
/// </para>
/// </remarks>
public sealed class AppliedJournal
{
    // Local\ and not Global\: the shell and the app run in the same user session, and Global\
    // would demand a specific privilege.
    private const string MutexName = @"Local\FolderHue.AppliedJournal";
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    private readonly string _filePath;

    /// <summary>Creates a journal backed by a file.</summary>
    /// <param name="filePath">Path of <c>applied.json</c>. The file need not exist.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    public AppliedJournal(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _filePath = filePath;
    }

    /// <summary>Reads every entry in the journal.</summary>
    /// <returns>The entries, or an empty list when the journal is missing or unreadable.</returns>
    public IReadOnlyList<AppliedEntry> ReadAll() => Read().Entries;

    /// <summary>Returns the entry for a folder.</summary>
    /// <param name="folderPath">Folder path, compared case-insensitively.</param>
    /// <returns>The entry, or <see langword="null"/> when the folder is not tracked.</returns>
    public AppliedEntry? Find(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return null;
        }

        string normalized = Normalize(folderPath);

        foreach (AppliedEntry entry in Read().Entries)
        {
            if (Normalize(entry.Path).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Adds or replaces a folder's entry.</summary>
    /// <param name="entry">The entry to record. Its <see cref="AppliedEntry.Path"/> is the key.</param>
    /// <returns><see langword="true"/> when the journal could be written.</returns>
    public bool Upsert(AppliedEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrEmpty(entry.Path))
        {
            return false;
        }

        return Mutate(data =>
        {
            string key = Normalize(entry.Path);
            data.Entries.RemoveAll(e => Normalize(e.Path).Equals(key, StringComparison.OrdinalIgnoreCase));
            data.Entries.Add(entry);
            return true;
        });
    }

    /// <summary>Removes a folder's entry.</summary>
    /// <param name="folderPath">Folder path.</param>
    /// <returns><see langword="true"/> when the journal could be written.</returns>
    public bool Remove(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return false;
        }

        string key = Normalize(folderPath);

        return Mutate(data =>
        {
            data.Entries.RemoveAll(e => Normalize(e.Path).Equals(key, StringComparison.OrdinalIgnoreCase));
            return true;
        });
    }

    /// <summary>
    /// Removes entries whose folder has disappeared from disk.
    /// </summary>
    /// <returns>How many entries were removed, 0 when the journal could not be rewritten.</returns>
    /// <remarks>
    /// A folder the user deleted or renamed leaves its record behind: nothing tells us, and the
    /// journal grows without bound. A stale record is not merely dead weight: if a different folder
    /// later takes the same path, <c>Apply</c> mistakes it for an earlier coloring and skips
    /// backing up its <c>desktop.ini</c> (CLAUDE.md 6.1).
    /// <para>
    /// An entry is removed only when the <b>volume</b> carrying it answers. An unplugged removable
    /// disk or an offline network share makes <see cref="Directory.Exists(string)"/> false for a
    /// perfectly live folder: purging it would lose the record of the <c>+r</c> attribute and make
    /// a clean reset impossible once the volume comes back (CLAUDE.md 6.3).
    /// </para>
    /// </remarks>
    public int PruneMissing()
    {
        int removed = 0;

        bool written = Mutate(data =>
        {
            removed = data.Entries.RemoveAll(entry => IsGone(entry.Path));
            return removed > 0;
        });

        return written ? removed : 0;
    }

    /// <summary>
    /// Determines whether a tracked folder has genuinely disappeared, as opposed to merely being
    /// unreachable.
    /// </summary>
    /// <param name="folderPath">Path held by the entry.</param>
    /// <returns>
    /// <see langword="true"/> only when the volume answers and the folder is no longer on it. When
    /// in doubt the answer is <see langword="false"/>: keeping a useless record is harmless,
    /// losing a real one is not.
    /// </returns>
    private static bool IsGone(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return true;
        }

        try
        {
            string full = Normalize(folderPath);
            if (Directory.Exists(full))
            {
                return false;
            }

            string? root = Path.GetPathRoot(full);
            return !string.IsNullOrEmpty(root) && Directory.Exists(root);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads, modifies and rewrites the journal under a lock.
    /// </summary>
    /// <param name="mutation">
    /// The transformation to apply. It must return <see langword="true"/> for the result to be
    /// written to disk.
    /// </param>
    /// <returns><see langword="true"/> when the write succeeded.</returns>
    public bool Mutate(Func<AppliedJournalData, bool> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        using var mutex = new Mutex(false, MutexName);
        bool held = false;

        try
        {
            try
            {
                held = mutex.WaitOne(LockTimeout);
            }
            catch (AbandonedMutexException)
            {
                // Another process died holding the lock: take it over.
                held = true;
            }

            if (!held)
            {
                return false;
            }

            AppliedJournalData data = ReadUnsynchronized();
            return mutation(data) && WriteUnsynchronized(data);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
        finally
        {
            if (held)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private AppliedJournalData Read()
    {
        try
        {
            return ReadUnsynchronized();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppliedJournalData();
        }
    }

    private AppliedJournalData ReadUnsynchronized()
    {
        if (!File.Exists(_filePath))
        {
            return new AppliedJournalData();
        }

        try
        {
            using FileStream stream = File.OpenRead(_filePath);
            return JsonSerializer.Deserialize(stream, AppliedJournalJsonContext.Default.AppliedJournalData)
                ?? new AppliedJournalData();
        }
        catch (JsonException)
        {
            // Corrupt journal: start from an empty one rather than blocking the application.
            return new AppliedJournalData();
        }
    }

    private bool WriteUnsynchronized(AppliedJournalData data)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporary = _filePath + ".tmp";

        using (FileStream stream = File.Create(temporary))
        {
            JsonSerializer.Serialize(stream, data, AppliedJournalJsonContext.Default.AppliedJournalData);
        }

        // Move with overwrite: the journal is never observed in a partial state.
        File.Move(temporary, _filePath, overwrite: true);
        return true;
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar);
        }
    }
}
