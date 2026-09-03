using FolderHue.Core.Palette;
using FolderHue.Core.Resources;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Verifie le catalogue, source unique de verite partagee par le shell et l'application.
/// </summary>
public sealed class PaletteCatalogTests
{
    [Fact]
    public void LeMenuResteSousLaLimiteDeSeizeElements()
    {
        // Un handler IExplorerCommand accepte au plus 16 elements, separateurs non comptes
        // (CLAUDE.md §4.4). Le sous-menu racine affiche : les couleurs, l'entree « Embleme »
        // et l'entree « Reinitialiser ».
        int rootItems = PaletteCatalog.Colors.Count + 2;

        Assert.True(rootItems <= 16, $"Le sous-menu racine afficherait {rootItems} elements.");
    }

    [Fact]
    public void LeSousMenuDesEmblemesResteSousLaLimite()
        => Assert.True(PaletteCatalog.Emblems.Count <= 16);

    [Fact]
    public void LaPaletteRespecteLeCadrageDeDixAQuatorzeTeintes()
        => Assert.InRange(PaletteCatalog.Colors.Count, 10, 14);

    [Fact]
    public void LesIdentifiantsSontUniques()
    {
        Assert.Equal(
            PaletteCatalog.Colors.Count,
            PaletteCatalog.Colors.Select(c => c.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.Equal(
            PaletteCatalog.Emblems.Count,
            PaletteCatalog.Emblems.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void LesTeintesSontDansLIntervalleAttendu()
    {
        foreach (FolderColor color in PaletteCatalog.Colors)
        {
            Assert.InRange(color.Hue, 0f, 360f);
            Assert.InRange(color.SaturationScale, 0f, 3f);
            Assert.InRange(color.SaturationFloor, 0f, 1f);
        }
    }

    [Fact]
    public void ToutesLesClesDeRessourceSontResolues()
    {
        // Une cle absente se trahirait par un libelle egal a la cle elle-meme dans le menu.
        foreach (FolderColor color in PaletteCatalog.Colors)
        {
            Assert.NotEqual(color.ResourceKey, Loc.Get(color.ResourceKey));
        }

        foreach (Emblem emblem in PaletteCatalog.Emblems)
        {
            Assert.NotEqual(emblem.ResourceKey, Loc.Get(emblem.ResourceKey));
        }

        foreach (string key in new[] { "Menu_Root", "Menu_Emblem", "Menu_Reset" })
        {
            Assert.NotEqual(key, Loc.Get(key));
        }
    }

    [Theory]
    [InlineData("blue", null, "blue.ico")]
    [InlineData("blue", "none", "blue.ico")]
    [InlineData("blue", "", "blue.ico")]
    [InlineData("blue", "important", "blue+important.ico")]
    [InlineData("BLUE", "Important", "blue+important.ico")]
    public void IconFileName_NommeLaCombinaison(string colorId, string? emblemId, string expected)
        => Assert.Equal(expected, PaletteCatalog.IconFileName(colorId, emblemId));

    [Fact]
    public void FindColor_EstInsensibleALaCasseEtRejetteLInconnu()
    {
        Assert.NotNull(PaletteCatalog.FindColor("BLUE"));
        Assert.Null(PaletteCatalog.FindColor("chartreuse"));
        Assert.Null(PaletteCatalog.FindColor(null));
    }

    [Fact]
    public void FindEmblem_TraiteLAbsenceCommeAucunEmbleme()
    {
        Assert.Equal(Emblem.None, PaletteCatalog.FindEmblem(null));
        Assert.Equal(Emblem.None, PaletteCatalog.FindEmblem(""));
        Assert.Equal(Emblem.NoneId, PaletteCatalog.FindEmblem("none")!.Id);
        Assert.Null(PaletteCatalog.FindEmblem("inconnu"));
    }

    [Fact]
    public void IconCombinationCount_CorrespondALaPreGeneration()
        => Assert.Equal(
            PaletteCatalog.TintableColors.Count * PaletteCatalog.Emblems.Count,
            PaletteCatalog.IconCombinationCount);

    [Fact]
    public void LaCouleurNeutreNApparaitPasDansLeMenu()
    {
        // Elle doit etre generable et resolvable, mais jamais proposee : ce n'est pas une teinte,
        // c'est « laisse le dossier tel qu'il est ».
        Assert.DoesNotContain(PaletteCatalog.Neutral, PaletteCatalog.Colors);
        Assert.Contains(PaletteCatalog.Neutral, PaletteCatalog.TintableColors);
        Assert.Same(PaletteCatalog.Neutral, PaletteCatalog.FindColor("neutral"));
    }

    [Fact]
    public void LaCouleurNeutreNeTransformeRien()
    {
        Assert.True(PaletteCatalog.Neutral.IsNeutral);

        foreach (FolderColor color in PaletteCatalog.Colors)
        {
            Assert.False(color.IsNeutral, $"La teinte « {color.Id} » ne doit pas etre neutre.");
        }
    }

    [Fact]
    public void LesPucesDEmblemeOntDesNomsDistinctsDesAutresIcones()
    {
        var names = PaletteCatalog.Emblems
            .Select(e => PaletteCatalog.EmblemChipFileName(e.Id))
            .Concat(PaletteCatalog.TintableColors.Select(c => PaletteCatalog.IconFileName(c.Id, null)))
            .Concat(PaletteCatalog.Colors.Select(c => PaletteCatalog.LogoFileName(c.Id)))
            .Append(PaletteCatalog.BrandLogoFileName)
            .ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void LeNomDuLogoDeMarqueEstDistinctDeCeuxDesCouleurs()
    {
        // logo.ico ne doit jamais entrer en collision avec une declinaison logo-<couleur>.ico,
        // ni avec une icone de dossier : tout vit dans le meme dossier.
        var names = PaletteCatalog.Colors
            .Select(c => PaletteCatalog.LogoFileName(c.Id))
            .Append(PaletteCatalog.BrandLogoFileName)
            .ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (FolderColor color in PaletteCatalog.Colors)
        {
            Assert.NotEqual(
                PaletteCatalog.IconFileName(color.Id, null),
                PaletteCatalog.LogoFileName(color.Id),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LeNomDuLogoEstNormaliseEnMinuscules()
        => Assert.Equal("logo-blue.ico", PaletteCatalog.LogoFileName("BLUE"));

    [Fact]
    public void LeNomDuLogoRefuseUnIdentifiantVide()
        => Assert.Throws<ArgumentException>(() => PaletteCatalog.LogoFileName(string.Empty));
}
