using FolderHue.Core.Folders;
using FolderHue.Core.Palette;
using FolderHue.Core.Storage;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Checks the apply-then-reset cycle, and above all that the reset is genuinely clean
/// (CLAUDE.md 6.3).
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
    public void Apply_writes_the_icon_and_sets_the_three_conditions()
    {
        using var workspace = new TempWorkspace();
        string icon = workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        OperationResult result = customizer.Apply(folder, "blue", Emblem.NoneId);

        Assert.True(result.Success, result.ReasonKey);

        string iniPath = DesktopIniFile.PathFor(folder);
        Assert.True(File.Exists(iniPath));

        // 1. the key points at our icon
        DesktopIniDocument document = DesktopIniFile.Read(iniPath);
        Assert.Equal(
            icon + ",0",
            document.Content.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));

        // 2. desktop.ini is hidden + system
        FileAttributes iniAttributes = File.GetAttributes(iniPath);
        Assert.True((iniAttributes & FileAttributes.Hidden) != 0);
        Assert.True((iniAttributes & FileAttributes.System) != 0);

        // 3. the folder itself carries ReadOnly, without which Explorer ignores everything
        Assert.True(FolderAttributes.IsFolderCustomizable(folder));
    }

    [Fact]
    public void Apply_records_the_folder_in_the_journal()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("red", "important");
        string folder = workspace.CreateFolder("folder");
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
    public void Apply_without_an_emblem_keeps_the_one_already_applied()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("red", "done");
        workspace.CreateFakeIcon("blue", "done");
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "red", "done");
        customizer.Apply(folder, "blue", emblemId: null);

        AppliedEntry? entry = customizer.Journal.Find(folder);
        Assert.NotNull(entry);
        Assert.Equal("blue", entry.ColorId);
        Assert.Equal("done", entry.EmblemId);
    }

    [Fact]
    public void Apply_backs_up_a_pre_existing_desktop_ini()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("green");
        string folder = workspace.CreateFolder("folder");
        string iniPath = DesktopIniFile.PathFor(folder);
        File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nFolderType=Documents\r\n");

        FolderCustomizer customizer = CreateCustomizer(workspace);
        customizer.Apply(folder, "green", Emblem.NoneId);

        Assert.True(File.Exists(DesktopIniFile.BackupPathFor(folder)));

        // The pre-existing key survives the merge: that is rule 6.1.
        DesktopIniDocument document = DesktopIniFile.Read(iniPath);
        Assert.Equal("Documents", document.Content.GetValue(DesktopIni.ShellClassInfoSection, "FolderType"));
    }

    [Fact]
    public void Apply_does_not_overwrite_an_existing_backup()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("green");
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");
        File.WriteAllText(DesktopIniFile.PathFor(folder), "[.ShellClassInfo]\r\nFolderType=Documents\r\n");

        FolderCustomizer customizer = CreateCustomizer(workspace);
        customizer.Apply(folder, "green", Emblem.NoneId);
        customizer.Apply(folder, "blue", Emblem.NoneId);

        string backup = DesktopIniFile.BackupPathFor(folder);
        FolderAttributes.ClearFileFlags(backup);

        // The backup must always hold the original, not our first write.
        Assert.DoesNotContain("IconResource", File.ReadAllText(backup), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_refuses_a_protected_folder()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        OperationResult result = customizer.Apply(workspace.AppPaths.IconsDirectory, "blue", Emblem.NoneId);

        Assert.False(result.Success);
        Assert.Equal(ProtectedPaths.ReasonApplicationData, result.ReasonKey);
    }

    [Fact]
    public void Apply_fails_cleanly_when_the_icon_was_not_generated()
    {
        using var workspace = new TempWorkspace();
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        OperationResult result = customizer.Apply(folder, "blue", Emblem.NoneId);

        Assert.False(result.Success);
        Assert.Equal(FolderCustomizer.ReasonIconMissing, result.ReasonKey);
        Assert.False(File.Exists(DesktopIniFile.PathFor(folder)));
    }

    [Fact]
    public void Apply_refuses_an_unknown_color()
    {
        using var workspace = new TempWorkspace();
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        OperationResult result = customizer.Apply(folder, "chartreuse", Emblem.NoneId);

        Assert.False(result.Success);
        Assert.Equal(FolderCustomizer.ReasonUnknownColor, result.ReasonKey);
    }

    [Fact]
    public void ResolveColorFor_returns_the_neutral_color_for_an_unknown_folder()
    {
        using var workspace = new TempWorkspace();
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        // Emphatically not the first hue of the palette: placing an emblem on a folder that had
        // never been colored used to turn it red along the way.
        Assert.Equal(PaletteCatalog.Neutral.Id, customizer.ResolveColorFor(folder));
    }

    [Fact]
    public void Apply_of_an_emblem_alone_forces_no_color()
    {
        using var workspace = new TempWorkspace();
        string icon = workspace.CreateFakeIcon(PaletteCatalog.Neutral.Id, "important");
        string folder = workspace.CreateFolder("folder");
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
    public void Apply_keeps_the_color_when_only_an_emblem_is_placed()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        workspace.CreateFakeIcon("blue", "done");
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);
        customizer.Apply(folder, customizer.ResolveColorFor(folder), "done");

        AppliedEntry? entry = customizer.Journal.Find(folder);
        Assert.NotNull(entry);
        Assert.Equal("blue", entry.ColorId);
        Assert.Equal("done", entry.EmblemId);
    }

    [Fact]
    public void Apply_with_neither_color_nor_emblem_resets_the_folder()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);

        // Removing the emblem from a folder whose color is "the original one" leaves nothing to
        // show: writing a desktop.ini pointing at a copy of the default icon would be noise. The
        // only correct action is to return the folder to its initial state.
        OperationResult result = customizer.Apply(folder, PaletteCatalog.Neutral.Id, Emblem.NoneId);

        Assert.True(result.Success, result.ReasonKey);
        Assert.False(File.Exists(DesktopIniFile.PathFor(folder)));
        Assert.False((File.GetAttributes(folder) & FileAttributes.ReadOnly) != 0);
        Assert.Null(customizer.Journal.Find(folder));
    }

    [Fact]
    public void Reset_deletes_desktop_ini_when_we_created_it()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);
        OperationResult result = customizer.Reset(folder);

        Assert.True(result.Success, result.ReasonKey);
        Assert.False(File.Exists(DesktopIniFile.PathFor(folder)));
        Assert.False(File.Exists(DesktopIniFile.BackupPathFor(folder)));
        Assert.Null(customizer.Journal.Find(folder));
    }

    [Fact]
    public void Reset_deletes_desktop_ini_after_several_applications()
    {
        // Regression: on the second application the desktop.ini in place is OURS. Backing it up
        // would mistake it for the user's original, and a reset would restore it instead of
        // deleting it.
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("green");
        workspace.CreateFakeIcon("green", "done");
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "green", Emblem.NoneId);
        customizer.Apply(folder, "green", "done");
        customizer.Reset(folder);

        Assert.False(File.Exists(DesktopIniFile.PathFor(folder)));
        Assert.False(File.Exists(DesktopIniFile.BackupPathFor(folder)));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
    }

    [Fact]
    public void Apply_does_not_back_up_its_own_output()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        workspace.CreateFakeIcon("red");
        string folder = workspace.CreateFolder("folder");
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
    public void Reset_clears_the_ReadOnly_attribute_we_set()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);
        customizer.Reset(folder);

        Assert.False((File.GetAttributes(folder) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public void Apply_clears_the_attribute_it_just_set_when_the_write_fails()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");

        // The attribute is now set BEFORE desktop.ini is written, so that Explorer cannot
        // conclude "no customisation" in between (CLAUDE.md 4.1). It must therefore not survive a
        // failed write: a folder turned read-only without ever having been colored would be a
        // gratuitous change to the user's disk.
        Directory.CreateDirectory(DesktopIniFile.PathFor(folder));

        FolderCustomizer customizer = CreateCustomizer(workspace);
        OperationResult result = customizer.Apply(folder, "blue", Emblem.NoneId);

        Assert.False(result.Success);
        Assert.False((File.GetAttributes(folder) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public void Apply_keeps_a_pre_existing_ReadOnly_when_the_write_fails()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");
        File.SetAttributes(folder, File.GetAttributes(folder) | FileAttributes.ReadOnly);
        Directory.CreateDirectory(DesktopIniFile.PathFor(folder));

        FolderCustomizer customizer = CreateCustomizer(workspace);
        OperationResult result = customizer.Apply(folder, "blue", Emblem.NoneId);

        // The cleanup only removes what we set ourselves.
        Assert.False(result.Success);
        Assert.True((File.GetAttributes(folder) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public void Reset_keeps_a_ReadOnly_we_did_not_set()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");
        File.SetAttributes(folder, File.GetAttributes(folder) | FileAttributes.ReadOnly);

        FolderCustomizer customizer = CreateCustomizer(workspace);
        customizer.Apply(folder, "blue", Emblem.NoneId);
        customizer.Reset(folder);

        // The user already had that setting: clearing it would be a regression on their side.
        Assert.True((File.GetAttributes(folder) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public void Reset_lightens_a_pre_existing_desktop_ini_without_deleting_it()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");
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
    public void Reset_restores_a_pre_existing_custom_icon()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");
        string iniPath = DesktopIniFile.PathFor(folder);
        File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nIconResource=C:\\perso\\mon.ico,0\r\n");

        FolderCustomizer customizer = CreateCustomizer(workspace);
        customizer.Apply(folder, "blue", Emblem.NoneId);
        customizer.Reset(folder);

        // The icon the user had chosen before us must come back, not vanish.
        DesktopIniDocument document = DesktopIniFile.Read(iniPath);
        Assert.Equal(
            @"C:\perso\mon.ico,0",
            document.Content.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
    }

    [Fact]
    public void Reset_leaves_a_desktop_ini_that_is_not_ours_alone()
    {
        using var workspace = new TempWorkspace();
        string folder = workspace.CreateFolder("folder");
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
    public void Reset_is_idempotent()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);

        Assert.True(customizer.Reset(folder).Success);
        Assert.True(customizer.Reset(folder).Success);
    }

    [Fact]
    public void Apply_then_Reset_leaves_the_folder_untouched()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("violet");
        string folder = workspace.CreateFolder("folder");
        FileAttributes before = File.GetAttributes(folder);

        FolderCustomizer customizer = CreateCustomizer(workspace);
        customizer.Apply(folder, "violet", Emblem.NoneId);
        customizer.Reset(folder);

        Assert.Equal(before, File.GetAttributes(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
    }
    [Fact]
    public void Apply_purges_the_record_of_a_vanished_folder()
    {
        // Regression: the "folder not found" refusal happened before the neutral path reached
        // Reset, the only place the record was removed. The entry therefore outlived its folder
        // indefinitely, and a folder recreating the same path inherited its backup
        // (CLAUDE.md 6.1).
        using var workspace = new TempWorkspace();
        workspace.CreateFakeIcon("blue");
        string folder = workspace.CreateFolder("folder");
        FolderCustomizer customizer = CreateCustomizer(workspace);

        customizer.Apply(folder, "blue", Emblem.NoneId);
        Assert.NotNull(customizer.Journal.Find(folder));

        File.SetAttributes(DesktopIniFile.PathFor(folder), FileAttributes.Normal);
        File.SetAttributes(folder, FileAttributes.Normal);
        Directory.Delete(folder, recursive: true);

        OperationResult result = customizer.Apply(folder, "blue", Emblem.NoneId);

        Assert.False(result.Success);
        Assert.Equal(ProtectedPaths.ReasonNotFound, result.ReasonKey);
        Assert.Null(customizer.Journal.Find(folder));
    }
}
