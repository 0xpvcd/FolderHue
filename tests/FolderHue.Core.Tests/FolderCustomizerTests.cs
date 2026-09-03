using FolderHue.Core.Folders;
using FolderHue.Core.Palette;
using FolderHue.Core.Storage;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Verifie le cycle appliquer puis reinitialiser, et surtout que la reinitialisation est
/// reellement propre (CLAUDE.md §6.3).
/// </summary>
public sealed class FolderCustomizerTests
{
    private sealed class NoKnownFolders : IKnownFolderProvider
    {
        public IReadOnlyList<string> GetExactProtectedFolders() => [];

        public IReadOnlyList<string> GetProtectedSubtrees() => [];
    }

    private static FolderCustomizer CreateCustomizer(TempWorkspace workspace)
        => new(
            workspace.AppPaths,
            new ProtectedPaths(new NoKnownFolders(), workspace.AppPaths),
            new AppliedJournal(workspace.AppPaths.JournalFile),
            new Log(workspace.AppPaths.LogFile));

    [Fact]
    public void Apply_EcritLIconeEtPoseLesTroisConditions()
    {
        using var workspace = new TempWorkspace();
        string icon = workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        OperationResult result = customizer.Apply(folder, "blue", Emblem.NoneId);

        Assert.True(result.Success, result.ReasonKey);

        string iniPath = DesktopIniFile.PathFor(folder);
        Assert.True(File.Exists(iniPath));

        // 1. la cle pointe sur notre icone
        DesktopIniDocument document = DesktopIniFile.Read(iniPath);
        Assert.Equal(
            icon + ",0",
            document.Content.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));

        // 2. desktop.ini est cache + systeme
        FileAttributes iniAttributes = File.GetAttributes(iniPath);
        Assert.True((iniAttributes & FileAttributes.Hidden) != 0);
        Assert.True((iniAttributes & FileAttributes.System) != 0);

        // 3. le dossier lui-meme porte ReadOnly, sans quoi l'Explorateur ignore tout
        Assert.True(FolderAttributes.IsFolderCustomizable(folder));
    }

    [Fact]
    public void Apply_InscritLeDossierAuJournal()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("red", "important");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "red", "important");

        AppliedEntry? entry = customizer.Journal.Find(folder);
        Assert.NotNull(entry);
        Assert.Equal("red", entry.ColorId);
        Assert.Equal("important", entry.EmblemId);
        Assert.True(entry.WeSetReadOnly);
        Assert.False(entry.HadDesktopIni);
    }

    [Fact]
    public void Apply_SansEmblemeConserveCeluiDejaApplique()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("red", "done");
        workspace.CreateFakeIcon("blue", "done");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "red", "done");
        customizer.Apply(folder, "blue", emblemId: null);

        AppliedEntry? entry = customizer.Journal.Find(folder);
        Assert.NotNull(entry);
        Assert.Equal("blue", entry.ColorId);
        Assert.Equal("done", entry.EmblemId);
    }

    [Fact]
    public void Apply_SauvegardeUnDesktopIniPreexistant()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("green");
        string folder = workspace.CreateFolder("dossier");
        string iniPath = DesktopIniFile.PathFor(folder);
        File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nFolderType=Documents\r\n");

        FolderCustomizer customizer = CreateCustomizer(workspace);
        customizer.Apply(folder, "green", Emblem.NoneId);

        Assert.True(File.Exists(DesktopIniFile.BackupPathFor(folder)));

        // La cle preexistante survit a la fusion : c'est la regle §6.1.
        DesktopIniDocument document = DesktopIniFile.Read(iniPath);
        Assert.Equal("Documents", document.Content.GetValue(DesktopIni.ShellClassInfoSection, "FolderType"));
    }

    [Fact]
    public void Apply_NEcrasePasUneSauvegardeDejaPresente()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("green");
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");
        File.WriteAllText(DesktopIniFile.PathFor(folder), "[.ShellClassInfo]\r\nFolderType=Documents\r\n");

        FolderCustomizer customizer = CreateCustomizer(workspace);
        customizer.Apply(folder, "green", Emblem.NoneId);
        customizer.Apply(folder, "blue", Emblem.NoneId);

        string backup = DesktopIniFile.BackupPathFor(folder);
        FolderAttributes.ClearFileFlags(backup);

        // La sauvegarde doit toujours contenir l'original, pas notre premiere ecriture.
        Assert.DoesNotContain("IconResource", File.ReadAllText(backup), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_RefuseUnDossierProtege()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        OperationResult result = customizer.Apply(workspace.AppPaths.IconsDirectory, "blue", Emblem.NoneId);

        Assert.False(result.Success);
        Assert.Equal(ProtectedPaths.ReasonApplicationData, result.ReasonKey);
    }

    [Fact]
    public void Apply_EchoueProprementSiLIconeNEstPasGeneree()
    {
        using var workspace = new TempWorkspace();
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        OperationResult result = customizer.Apply(folder, "blue", Emblem.NoneId);

        Assert.False(result.Success);
        Assert.Equal(FolderCustomizer.ReasonIconMissing, result.ReasonKey);
        Assert.False(File.Exists(DesktopIniFile.PathFor(folder)));
    }

    [Fact]
    public void Apply_RefuseUneCouleurInconnue()
    {
        using var workspace = new TempWorkspace();
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        OperationResult result = customizer.Apply(folder, "chartreuse", Emblem.NoneId);

        Assert.False(result.Success);
        Assert.Equal(FolderCustomizer.ReasonUnknownColor, result.ReasonKey);
    }

    [Fact]
    public void ResolveColorFor_RendLaCouleurNeutreQuandLeDossierEstInconnu()
    {
        using var workspace = new TempWorkspace();
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        // Surtout pas la premiere teinte de la palette : poser un embleme sur un dossier jamais
        // colorise le faisait virer au rouge au passage.
        Assert.Equal(PaletteCatalog.Neutral.Id, customizer.ResolveColorFor(folder));
    }

    [Fact]
    public void Apply_UnEmblemeSeulNImposeAucuneCouleur()
    {
        using var workspace = new TempWorkspace();
        string icon = workspace.CreateFakeIcon(PaletteCatalog.Neutral.Id, "important");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        OperationResult result = customizer.Apply(
            folder, customizer.ResolveColorFor(folder), "important");

        Assert.True(result.Success, result.ReasonKey);

        DesktopIniDocument document = DesktopIniFile.Read(DesktopIniFile.PathFor(folder));
        Assert.Equal(
            icon + ",0",
            document.Content.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));

        AppliedEntry? entry = customizer.Journal.Find(folder);
        Assert.NotNull(entry);
        Assert.Equal(PaletteCatalog.Neutral.Id, entry.ColorId);
        Assert.Equal("important", entry.EmblemId);
    }

    [Fact]
    public void Apply_ConserveLaCouleurQuandOnNePoseQuUnEmbleme()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        workspace.CreateFakeIcon("blue", "done");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);
        customizer.Apply(folder, customizer.ResolveColorFor(folder), "done");

        AppliedEntry? entry = customizer.Journal.Find(folder);
        Assert.NotNull(entry);
        Assert.Equal("blue", entry.ColorId);
        Assert.Equal("done", entry.EmblemId);
    }

    [Fact]
    public void Apply_NiCouleurNiEmbleme_ReinitialiseLeDossier()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);

        // Retirer l'embleme d'un dossier dont la couleur est « celle d'origine » ne laisse rien a
        // afficher : ecrire un desktop.ini pointant sur une copie de l'icone par defaut serait du
        // bruit. La seule action correcte est de rendre le dossier a son etat initial.
        OperationResult result = customizer.Apply(folder, PaletteCatalog.Neutral.Id, Emblem.NoneId);

        Assert.True(result.Success, result.ReasonKey);
        Assert.False(File.Exists(DesktopIniFile.PathFor(folder)));
        Assert.False((File.GetAttributes(folder) & FileAttributes.ReadOnly) != 0);
        Assert.Null(customizer.Journal.Find(folder));
    }

    [Fact]
    public void Reset_SupprimeDesktopIniQuandNousLAvionsCree()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);
        OperationResult result = customizer.Reset(folder);

        Assert.True(result.Success, result.ReasonKey);
        Assert.False(File.Exists(DesktopIniFile.PathFor(folder)));
        Assert.False(File.Exists(DesktopIniFile.BackupPathFor(folder)));
        Assert.Null(customizer.Journal.Find(folder));
    }

    [Fact]
    public void Reset_SupprimeDesktopIniApresPlusieursApplications()
    {
        // Regression : a la seconde application, le desktop.ini en place est le NOTRE. Le
        // sauvegarder reviendrait a le prendre pour l'original de l'utilisateur, et la
        // reinitialisation le restaurerait au lieu de le supprimer.
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("green");
        workspace.CreateFakeIcon("green", "done");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "green", Emblem.NoneId);
        customizer.Apply(folder, "green", "done");
        customizer.Reset(folder);

        Assert.False(File.Exists(DesktopIniFile.PathFor(folder)));
        Assert.False(File.Exists(DesktopIniFile.BackupPathFor(folder)));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
    }

    [Fact]
    public void Apply_NeSauvegardePasSaPropreProduction()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        workspace.CreateFakeIcon("red");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);
        customizer.Apply(folder, "red", Emblem.NoneId);

        Assert.False(File.Exists(DesktopIniFile.BackupPathFor(folder)));

        AppliedEntry? entry = customizer.Journal.Find(folder);
        Assert.NotNull(entry);
        Assert.False(entry.HadDesktopIni);
        Assert.Null(entry.BackupPath);
    }

    [Fact]
    public void Reset_RetireLAttributReadOnlyQueNousAvionsPose()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);
        customizer.Reset(folder);

        Assert.False((File.GetAttributes(folder) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public void Apply_RetireLAttributQuIlVientDePoserSiLEcritureEchoue()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");

        // L'attribut est desormais pose AVANT l'ecriture de desktop.ini, pour ne pas laisser
        // l'Explorateur conclure « aucune personnalisation » entre les deux (CLAUDE.md §4.1).
        // Il ne doit donc pas survivre a une ecriture qui echoue : un dossier passe en lecture
        // seule sans jamais avoir ete colorise serait une modification gratuite du disque.
        Directory.CreateDirectory(DesktopIniFile.PathFor(folder));

        FolderCustomizer customizer = CreateCustomizer(workspace);
        OperationResult result = customizer.Apply(folder, "blue", Emblem.NoneId);

        Assert.False(result.Success);
        Assert.False((File.GetAttributes(folder) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public void Apply_ConserveUnReadOnlyPreexistantQuandLEcritureEchoue()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");
        File.SetAttributes(folder, File.GetAttributes(folder) | FileAttributes.ReadOnly);
        Directory.CreateDirectory(DesktopIniFile.PathFor(folder));

        FolderCustomizer customizer = CreateCustomizer(workspace);
        OperationResult result = customizer.Apply(folder, "blue", Emblem.NoneId);

        // Le rattrapage ne retire que ce que nous avons pose nous-memes.
        Assert.False(result.Success);
        Assert.True((File.GetAttributes(folder) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public void Reset_ConserveUnReadOnlyQueNousNAvionsPasPose()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");
        File.SetAttributes(folder, File.GetAttributes(folder) | FileAttributes.ReadOnly);

        FolderCustomizer customizer = CreateCustomizer(workspace);
        customizer.Apply(folder, "blue", Emblem.NoneId);
        customizer.Reset(folder);

        // L'utilisateur avait deja ce reglage : le retirer serait une regression de son cote.
        Assert.True((File.GetAttributes(folder) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public void Reset_AllegeUnDesktopIniPreexistantSansLeSupprimer()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");
        string iniPath = DesktopIniFile.PathFor(folder);
        File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nFolderType=Documents\r\n");

        FolderCustomizer customizer = CreateCustomizer(workspace);
        customizer.Apply(folder, "blue", Emblem.NoneId);
        customizer.Reset(folder);

        Assert.True(File.Exists(iniPath));

        DesktopIniDocument document = DesktopIniFile.Read(iniPath);
        Assert.Equal("Documents", document.Content.GetValue(DesktopIni.ShellClassInfoSection, "FolderType"));
        Assert.Null(document.Content.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
    }

    [Fact]
    public void Reset_RestaureUneIconePersonnaliseePreexistante()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");
        string iniPath = DesktopIniFile.PathFor(folder);
        File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nIconResource=C:\\perso\\mon.ico,0\r\n");

        FolderCustomizer customizer = CreateCustomizer(workspace);
        customizer.Apply(folder, "blue", Emblem.NoneId);
        customizer.Reset(folder);

        // L'icone que l'utilisateur avait choisie avant nous doit revenir, pas disparaitre.
        DesktopIniDocument document = DesktopIniFile.Read(iniPath);
        Assert.Equal(
            @"C:\perso\mon.ico,0",
            document.Content.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
    }

    [Fact]
    public void Reset_NeTouchePasAUnDesktopIniQuiNEstPasLeNotre()
    {
        using var workspace = new TempWorkspace();
        string folder = workspace.CreateFolder("dossier");
        string iniPath = DesktopIniFile.PathFor(folder);
        const string content = "[.ShellClassInfo]\r\nIconResource=C:\\autre\\outil.ico,0\r\n";
        File.WriteAllText(iniPath, content);

        FolderCustomizer customizer = CreateCustomizer(workspace);
        OperationResult result = customizer.Reset(folder);

        Assert.True(result.Success, result.ReasonKey);

        DesktopIniDocument document = DesktopIniFile.Read(iniPath);
        Assert.Equal(
            @"C:\autre\outil.ico,0",
            document.Content.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
    }

    [Fact]
    public void Reset_EstIdempotent()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);

        Assert.True(customizer.Reset(folder).Success);
        Assert.True(customizer.Reset(folder).Success);
    }

    [Fact]
    public void ApplyPuisReset_RendLeDossierAIdentique()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("violet");
        string folder = workspace.CreateFolder("dossier");
        FileAttributes before = File.GetAttributes(folder);

        FolderCustomizer customizer = CreateCustomizer(workspace);
        customizer.Apply(folder, "violet", Emblem.NoneId);
        customizer.Reset(folder);

        Assert.Equal(before, File.GetAttributes(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
    }

    /// <summary>
    /// Une date de reference nettement dans le passe, pour que la comparaison ne depende pas de la
    /// resolution de l'horloge systeme.
    /// </summary>
    private static readonly DateTime LongAgo = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_SurUnDossierDejaColorise_AvanceLaDateDuDossier()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        workspace.CreateFakeIcon("red");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        Assert.True(customizer.Apply(folder, "blue", Emblem.NoneId).Success);

        // La premiere application CREE desktop.ini, ce qui modifie l'entree de repertoire du
        // dossier. La seconde ne fait que reecrire le fichier sur place : sans intervention, la
        // date du dossier resterait figee et l'Explorateur ne verrait aucune raison de relire.
        Directory.SetLastWriteTimeUtc(folder, LongAgo);

        Assert.True(customizer.Apply(folder, "red", Emblem.NoneId).Success);

        Assert.True(
            Directory.GetLastWriteTimeUtc(folder) > LongAgo,
            "La date du dossier doit avancer, sinon le changement de couleur ne se voit qu'apres F5.");
    }

    [Fact]
    public void Reset_AvanceLaDateDuDossier()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("dossier");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        Assert.True(customizer.Apply(folder, "blue", Emblem.NoneId).Success);
        Directory.SetLastWriteTimeUtc(folder, LongAgo);

        Assert.True(customizer.Reset(folder).Success);

        Assert.True(Directory.GetLastWriteTimeUtc(folder) > LongAgo);
    }

    [Fact]
    public void TouchFolder_SurUnCheminInexistant_NeLevePas()
    {
        using var workspace = new TempWorkspace();
        string absent = Path.Combine(workspace.CreateFolder("dossier"), "jamais-cree");

        FolderAttributes.TouchFolder(absent);
    }
}
