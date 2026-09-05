using FolderHue.Core.Palette;
using FolderHue.Core.Storage;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Checks the <c>applied.json</c> journal, on which a clean reset depends.
/// </summary>
public sealed class AppliedJournalTests
{
    private static AppliedEntry Entry(string path, string colorId = "blue") => new()
    {
        Path = path,
        ColorId = colorId,
        EmblemId = Emblem.NoneId,
        WeSetReadOnly = true,
        HadDesktopIni = false,
        BackupPath = null,
        AppliedUtc = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void Upsert_then_Find_round_trips()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string folder = workspace.CreateFolder("folder");

        Assert.True(journal.Upsert(Entry(folder, "emerald")));

        AppliedEntry? found = journal.Find(folder);
        Assert.NotNull(found);
        Assert.Equal("emerald", found.ColorId);
        Assert.True(found.WeSetReadOnly);
    }

    [Fact]
    public void Upsert_replaces_rather_than_duplicates()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string folder = workspace.CreateFolder("folder");

        journal.Upsert(Entry(folder, "red"));
        journal.Upsert(Entry(folder, "blue"));

        Assert.Single(journal.ReadAll());
        Assert.Equal("blue", journal.Find(folder)!.ColorId);
    }

    [Fact]
    public void Find_ignores_case_and_a_trailing_separator()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string folder = workspace.CreateFolder("Folder");

        journal.Upsert(Entry(folder));

        Assert.NotNull(journal.Find(folder.ToUpperInvariant()));
        Assert.NotNull(journal.Find(folder + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Remove_takes_the_entry_out()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string folder = workspace.CreateFolder("folder");

        journal.Upsert(Entry(folder));
        Assert.True(journal.Remove(folder));

        Assert.Null(journal.Find(folder));
        Assert.Empty(journal.ReadAll());
    }

    [Fact]
    public void ReadAll_returns_an_empty_list_when_the_file_is_missing()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(Path.Combine(workspace.AppPaths.Root, "missing.json"));

        Assert.Empty(journal.ReadAll());
    }

    [Fact]
    public void ReadAll_treats_a_corrupt_file_as_an_empty_journal()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.AppPaths.JournalFile;
        File.WriteAllText(path, "{ ceci n'est pas du JSON");

        var journal = new AppliedJournal(path);

        // Losing the record is annoying; bringing explorer.exe down would be far worse.
        Assert.Empty(journal.ReadAll());
    }

    [Fact]
    public void Upsert_overwrites_a_corrupt_file()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.AppPaths.JournalFile;
        File.WriteAllText(path, "]]] corrompu");

        var journal = new AppliedJournal(path);
        string folder = workspace.CreateFolder("folder");

        Assert.True(journal.Upsert(Entry(folder)));
        Assert.Single(journal.ReadAll());
    }

    [Fact]
    public void Upsert_survives_concurrent_writes()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.AppPaths.JournalFile;

        // One Invoke handles N folders: several simultaneous writers are the normal case.
        string[] folders = Enumerable.Range(0, 24)
            .Select(i => workspace.CreateFolder("folder-" + i))
            .ToArray();

        Parallel.ForEach(folders, folder =>
        {
            var journal = new AppliedJournal(path);
            journal.Upsert(Entry(folder));
        });

        Assert.Equal(folders.Length, new AppliedJournal(path).ReadAll().Count);
    }

    [Fact]
    public void Mutate_writes_nothing_when_the_transformation_returns_false()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string folder = workspace.CreateFolder("folder");

        journal.Upsert(Entry(folder));

        Assert.False(journal.Mutate(data =>
        {
            data.Entries.Clear();
            return false;
        }));

        Assert.Single(journal.ReadAll());
    }
    [Fact]
    public void PruneMissing_removes_the_record_of_a_deleted_folder()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string vivant = workspace.CreateFolder("vivant");
        string disparu = workspace.CreateFolder("disparu");

        journal.Upsert(Entry(vivant));
        journal.Upsert(Entry(disparu));
        Directory.Delete(disparu);

        Assert.Equal(1, journal.PruneMissing());
        Assert.Single(journal.ReadAll());
        Assert.NotNull(journal.Find(vivant));
        Assert.Null(journal.Find(disparu));
    }

    [Fact]
    public void PruneMissing_writes_nothing_when_every_folder_is_present()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string folder = workspace.CreateFolder("folder");

        journal.Upsert(Entry(folder));

        Assert.Equal(0, journal.PruneMissing());
        Assert.Single(journal.ReadAll());
    }

    [Fact]
    public void PruneMissing_keeps_the_record_of_an_unreachable_volume()
    {
        // An unplugged removable disk or an offline share makes Directory.Exists false for a
        // perfectly live folder. Purging its entry would lose the record of the +r attribute and
        // make a clean reset impossible once the volume returns (CLAUDE.md 6.3).
        string? absent = FirstUnusedDriveLetter();
        if (absent is null)
        {
            // A machine with every drive letter taken: the case cannot be simulated here.
            return;
        }

        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string offline = absent + @"older\colored";

        journal.Upsert(Entry(offline));

        Assert.Equal(0, journal.PruneMissing());
        Assert.NotNull(journal.Find(offline));
    }

    /// <summary>Returns a drive root absent from the machine, or null when there is none.</summary>
    private static string? FirstUnusedDriveLetter()
    {
        for (char letter = 'Z'; letter >= 'D'; letter--)
        {
            string root = letter + @":\";
            if (!Directory.Exists(root))
            {
                return letter + ":";
            }
        }

        return null;
    }
}
