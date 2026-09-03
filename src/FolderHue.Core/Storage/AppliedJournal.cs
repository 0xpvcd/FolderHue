using System.Text.Json;

namespace FolderHue.Core.Storage;

/// <summary>
/// Journal local des dossiers colorises, persiste dans <c>applied.json</c>.
/// </summary>
/// <remarks>
/// Plusieurs ecrivains sont possibles en meme temps : un <c>Invoke</c> du menu contextuel traite N
/// dossiers, et <c>FolderHue.App</c> peut tourner en parallele. Toutes les operations passent
/// donc par un mutex nomme et par une ecriture atomique (fichier temporaire puis remplacement).
/// <para>
/// Aucune methode ne leve : un journal illisible est traite comme un journal vide. Perdre la trace
/// est genant, faire tomber <c>explorer.exe</c> le serait beaucoup plus (CLAUDE.md §6.5).
/// </para>
/// </remarks>
public sealed class AppliedJournal
{
    // Local\ et non Global\ : le shell et l'app tournent dans la meme session utilisateur, et
    // Global\ exigerait un privilege particulier.
    private const string MutexName = @"Local\FolderHue.AppliedJournal";
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    private readonly string _filePath;

    /// <summary>Cree un journal adosse a un fichier.</summary>
    /// <param name="filePath">Chemin de <c>applied.json</c>. Le fichier peut ne pas exister.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> est vide.</exception>
    public AppliedJournal(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _filePath = filePath;
    }

    /// <summary>Lit toutes les entrees du journal.</summary>
    /// <returns>Les entrees, ou une liste vide si le journal est absent ou illisible.</returns>
    public IReadOnlyList<AppliedEntry> ReadAll() => Read().Entries;

    /// <summary>Retourne l'entree correspondant a un dossier.</summary>
    /// <param name="folderPath">Chemin du dossier, compare sans tenir compte de la casse.</param>
    /// <returns>L'entree, ou <see langword="null"/> si le dossier n'est pas suivi.</returns>
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

    /// <summary>Ajoute ou remplace l'entree d'un dossier.</summary>
    /// <param name="entry">L'entree a enregistrer. Son <see cref="AppliedEntry.Path"/> sert de cle.</param>
    /// <returns><see langword="true"/> si le journal a pu etre ecrit.</returns>
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

    /// <summary>Retire l'entree d'un dossier.</summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <returns><see langword="true"/> si le journal a pu etre ecrit.</returns>
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
    /// Lit, modifie et reecrit le journal sous verrou.
    /// </summary>
    /// <param name="mutation">
    /// Transformation a appliquer. Elle doit retourner <see langword="true"/> pour que le resultat
    /// soit ecrit sur disque.
    /// </param>
    /// <returns><see langword="true"/> si l'ecriture a reussi.</returns>
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
                // Un autre processus est mort en tenant le verrou : on reprend la main.
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
            // Journal corrompu : on repart d'un journal vide plutot que de bloquer l'application.
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

        // Move avec ecrasement : le journal n'est jamais observe dans un etat partiel.
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
