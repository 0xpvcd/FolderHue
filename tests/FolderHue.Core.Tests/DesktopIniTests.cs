using FolderHue.Core.Folders;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Verifie la regle la plus sensible du projet : fusionner un <c>desktop.ini</c> existant sans
/// jamais l'ecraser (CLAUDE.md §6.1).
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
    public void SetValue_PreserveLesClesExistantes()
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
    public void SetValue_AjouteLaCleDansLaBonneSection()
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
    public void SetValue_CreeLaSectionQuandElleManque()
    {
        var ini = new DesktopIni();

        ini.SetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey, @"C:\icons\red.ico,0");

        Assert.Contains("[.ShellClassInfo]", ini.ToText(), StringComparison.Ordinal);
        Assert.Equal(@"C:\icons\red.ico,0", ini.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
    }

    [Fact]
    public void SetValue_RemplaceSansDupliquer()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\r\nIconResource=old.ico,0\r\n");

        ini.SetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey, "new.ico,0");

        Assert.Equal("new.ico,0", ini.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
        Assert.Single(ini.ToText().Split("IconResource=")[1..]);
    }

    [Fact]
    public void GetValue_IgnoreLaCasseDesNoms()
    {
        DesktopIni ini = DesktopIni.Parse("[.shellclassinfo]\r\niconresource=a.ico,0\r\n");

        Assert.Equal("a.ico,0", ini.GetValue(".ShellClassInfo", "IconResource"));
    }

    [Fact]
    public void Parse_ConserveLesCommentaires()
    {
        const string text = "; commentaire de l'utilisateur\r\n[.ShellClassInfo]\r\nFolderType=Generic\r\n";

        string roundTrip = DesktopIni.Parse(text).ToText();

        Assert.Contains("; commentaire de l'utilisateur", roundTrip, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_AccepteLesFinsDeLigneUnix()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\nIconResource=a.ico,0\n");

        Assert.Equal("a.ico,0", ini.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
    }

    [Fact]
    public void RemoveValue_RetireLaCle()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\r\nIconResource=a.ico,0\r\nFolderType=Generic\r\n");

        Assert.True(ini.RemoveValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));

        Assert.Null(ini.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey));
        Assert.Equal("Generic", ini.GetValue(DesktopIni.ShellClassInfoSection, "FolderType"));
    }

    [Fact]
    public void RemoveSectionIfEmpty_NeSupprimePasUneSectionHabitee()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\r\nFolderType=Generic\r\n");

        Assert.False(ini.RemoveSectionIfEmpty(DesktopIni.ShellClassInfoSection));
    }

    [Fact]
    public void RemoveSectionIfEmpty_SupprimeUneSectionVidee()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\r\nIconResource=a.ico,0\r\n");
        ini.RemoveValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey);

        Assert.True(ini.RemoveSectionIfEmpty(DesktopIni.ShellClassInfoSection));
        Assert.True(ini.IsEmpty);
    }

    [Fact]
    public void ContainsOnlyKeys_DetecteUneCleEtrangere()
    {
        DesktopIni ini = DesktopIni.Parse(Existing);

        Assert.False(ini.ContainsOnlyKeys(OwnedKeys));
    }

    [Fact]
    public void ContainsOnlyKeys_AccepteUnFichierNeContenantQueLesNotres()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\r\nIconResource=a.ico,0\r\n");

        Assert.True(ini.ContainsOnlyKeys(OwnedKeys));
    }

    [Fact]
    public void ToText_TermineChaqueLigneParCrLf()
    {
        DesktopIni ini = DesktopIni.Parse("[.ShellClassInfo]\nIconResource=a.ico,0\n");

        Assert.EndsWith("\r\n", ini.ToText(), StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n", ini.ToText().Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }
}
