using FolderHue.Core.Palette;
using FolderHue.Core.Storage;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Verifie le journal <c>applied.json</c>, qui conditionne une reinitialisation propre.
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
    public void UpsertPuisFind_FaitLAllerRetour()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string folder = workspace.CreateFolder("dossier");

        Assert.True(journal.Upsert(Entry(folder, "emerald")));

        AppliedEntry? found = journal.Find(folder);
        Assert.NotNull(found);
        Assert.Equal("emerald", found.ColorId);
        Assert.True(found.WeSetReadOnly);
    }

    [Fact]
    public void Upsert_RemplaceAuLieuDeDupliquer()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string folder = workspace.CreateFolder("dossier");

        journal.Upsert(Entry(folder, "red"));
        journal.Upsert(Entry(folder, "blue"));

        Assert.Single(journal.ReadAll());
        Assert.Equal("blue", journal.Find(folder)!.ColorId);
    }

    [Fact]
    public void Find_IgnoreLaCasseEtLaBarreFinale()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string folder = workspace.CreateFolder("Dossier");

        journal.Upsert(Entry(folder));

        Assert.NotNull(journal.Find(folder.ToUpperInvariant()));
        Assert.NotNull(journal.Find(folder + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Remove_RetireLEntree()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string folder = workspace.CreateFolder("dossier");

        journal.Upsert(Entry(folder));
        Assert.True(journal.Remove(folder));

        Assert.Null(journal.Find(folder));
        Assert.Empty(journal.ReadAll());
    }

    [Fact]
    public void ReadAll_RetourneUneListeVideQuandLeFichierEstAbsent()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(Path.Combine(workspace.AppPaths.Root, "absent.json"));

        Assert.Empty(journal.ReadAll());
    }

    [Fact]
    public void ReadAll_TraiteUnFichierCorrompuCommeUnJournalVide()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.AppPaths.JournalFile;
        File.WriteAllText(path, "{ ceci n'est pas du JSON");

        var journal = new AppliedJournal(path);

        // Perdre la trace est genant ; faire tomber explorer.exe le serait bien plus.
        Assert.Empty(journal.ReadAll());
    }

    [Fact]
    public void Upsert_ReecritParDessusUnFichierCorrompu()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.AppPaths.JournalFile;
        File.WriteAllText(path, "]]] corrompu");

        var journal = new AppliedJournal(path);
        string folder = workspace.CreateFolder("dossier");

        Assert.True(journal.Upsert(Entry(folder)));
        Assert.Single(journal.ReadAll());
    }

    [Fact]
    public void Upsert_ResisteAUneEcritureConcurrente()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.AppPaths.JournalFile;

        // Un Invoke traite N dossiers : plusieurs ecrivains simultanes sont le cas nominal.
        string[] folders = Enumerable.Range(0, 24)
            .Select(i => workspace.CreateFolder("dossier-" + i))
            .ToArray();

        Parallel.ForEach(folders, folder =>
        {
            var journal = new AppliedJournal(path);
            journal.Upsert(Entry(folder));
        });

        Assert.Equal(folders.Length, new AppliedJournal(path).ReadAll().Count);
    }

    [Fact]
    public void Mutate_NEcritPasQuandLaTransformationRetourneFaux()
    {
        using var workspace = new TempWorkspace();
        var journal = new AppliedJournal(workspace.AppPaths.JournalFile);
        string folder = workspace.CreateFolder("dossier");

        journal.Upsert(Entry(folder));

        Assert.False(journal.Mutate(data =>
        {
            data.Entries.Clear();
            return false;
        }));

        Assert.Single(journal.ReadAll());
    }
}
