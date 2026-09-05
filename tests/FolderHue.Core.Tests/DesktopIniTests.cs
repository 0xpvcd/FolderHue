using FolderHue.Core.Folders;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Checks the project's most delicate rule: merge an existing <c>desktop.ini</c> without ever
/// overwriting it (CLAUDE.md 6.1).
/// </summary>
public sealed class DesktopIniTests
{
    private const string Existing = """
        [.ShellClassInfo]
        FolderType=Documents
        LocalizedResourceName=@%SystemRoot%\system32\shell32.dll,-21770

        [ViewState]
        Mode=
        Vid=
        FolderType=Documents
        """;

    private static readonly (string Section, string Key)[] OwnedKeys =
        [(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey)];

    [Fact]
    public void SetValue_preserves_existing_keys()
    {
        DesktopIni ini = DesktopIni.Parse(Existing);

        ini.SetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey, @"C:\icons\blue.ico,0");

        Assert.Equal("Documents", ini.GetValue(DesktopIni.ShellClassInfoSection, "FolderType"));
        Assert.Equal(
            @"@%SystemRoot%\system32\shell32.dll,-21770",
            ini.GetValue(DesktopIni.ShellClassInfoSection, "LocalizedResourceName"));
        Assert.Equal("", ini.GetValue("ViewState", "Mode"));
    }

    [Fact]
    public void SetValue_adds_the_key_to_the_right_section()
    {
        DesktopIni ini = DesktopIni.Parse(Existing);

        ini.SetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey, @"C:\icons\blue.ico,0");
        string text = ini.ToText();

        int shellClassInfo = text.IndexOf("[.ShellClassInfo]", StringComparison.Ordinal);
        int iconResource = text.IndexOf("IconResource=", StringComparison.Ordinal);
        int viewState = text.IndexOf("[ViewState]", StringComparison.Ordinal);

        Assert.InRange(iconResource, shellClassInfo, viewState);
    }

    [Fact]
    public void SetValue_creates_the_section_when_it_is_missing()
    {
        var ini = new DesktopIni();

        ini.SetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey, @"C:\icons\red.ico,0");

        Assert.Contains("[.ShellClassInfo]", ini.ToText(), StringComparison.Ordinal);
        Assert.Equal(@"C:\icons\red.ico,0", ini.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
    }

    [Fact]
    public void SetValue_replaces_without_duplicating()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\r\nIconResource=old.ico,0\r\n");

        ini.SetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey, "new.ico,0");

        Assert.Equal("new.ico,0", ini.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
        Assert.Single(ini.ToText().Split("IconResource=")[1..]);
    }

    [Fact]
    public void GetValue_ignores_the_case_of_names()
    {
        DesktopIni ini = DesktopIni.Parse("[.shellclassinfo]\r\niconresource=a.ico,0\r\n");

        Assert.Equal("a.ico,0", ini.GetValue(".ShellClassInfo", "IconResource"));
    }

    [Fact]
    public void Parse_preserves_comments()
    {
        const string text = "; commentaire de l'utilisateur\r\n[.ShellClassInfo]\r\nFolderType=Generic\r\n";

        string roundTrip = DesktopIni.Parse(text).ToText();

        Assert.Contains("; commentaire de l'utilisateur", roundTrip, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_accepts_Unix_line_endings()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\nIconResource=a.ico,0\n");

        Assert.Equal("a.ico,0", ini.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
    }

    [Fact]
    public void RemoveValue_takes_the_key_out()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\r\nIconResource=a.ico,0\r\nFolderType=Generic\r\n");

        Assert.True(ini.RemoveValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));

        Assert.Null(ini.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
        Assert.Equal("Generic", ini.GetValue(DesktopIni.ShellClassInfoSection, "FolderType"));
    }

    [Fact]
    public void RemoveSectionIfEmpty_keeps_a_populated_section()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\r\nFolderType=Generic\r\n");

        Assert.False(ini.RemoveSectionIfEmpty(DesktopIni.ShellClassInfoSection));
    }

    [Fact]
    public void RemoveSectionIfEmpty_deletes_an_emptied_section()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\r\nIconResource=a.ico,0\r\n");
        ini.RemoveValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey);

        Assert.True(ini.RemoveSectionIfEmpty(DesktopIni.ShellClassInfoSection));
        Assert.True(ini.IsEmpty);
    }

    [Fact]
    public void ContainsOnlyKeys_detects_a_foreign_key()
    {
        DesktopIni ini = DesktopIni.Parse(Existing);

        Assert.False(ini.ContainsOnlyKeys(OwnedKeys));
    }

    [Fact]
    public void ContainsOnlyKeys_accepts_a_file_holding_only_ours()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\r\nIconResource=a.ico,0\r\n");

        Assert.True(ini.ContainsOnlyKeys(OwnedKeys));
    }

    [Fact]
    public void ToText_ends_every_line_with_CRLF()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\nIconResource=a.ico,0\n");

        Assert.EndsWith("\r\n", ini.ToText(), StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n", ini.ToText().Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }
}
