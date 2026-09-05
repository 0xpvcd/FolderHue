using FolderHue.Core.Palette;
using FolderHue.Core.Resources;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Checks the catalogue, the single source of truth shared by the shell and the application.
/// </summary>
public sealed class PaletteCatalogTests
{
    /// <summary>
    /// How many items <c>RootCommand.CreateSubCommands</c> actually places in the submenu.
    /// </summary>
    /// <remarks>
    /// The colors, plus "Original color", plus one separator, plus "Emblem", plus "Reset".
    /// <para>
    /// ⚠️ This used to be <c>Colors.Count + 2</c>, on the belief that separators did not count and
    /// that the neutral color was not in the menu. Both were false, so the test computed 14 where
    /// the menu showed 16 — it could not have caught the very overflow it exists to prevent.
    /// </para>
    /// </remarks>
    private static int RootSubmenuItemCount => PaletteCatalog.Colors.Count + 4;

    /// <summary>
    /// Past 16 items, separators included, Explorer drops the whole root entry without a message
    /// (CLAUDE.md 4.4).
    /// </summary>
    [Fact]
    public void The_root_submenu_stays_within_sixteen_items()
    {
        Assert.True(
            RootSubmenuItemCount <= 16,
            $"The root submenu would show {RootSubmenuItemCount} items, and the entry would vanish.");
    }

    /// <summary>
    /// There is no headroom left: adding an entry means removing another or nesting further.
    /// </summary>
    [Fact]
    public void The_root_submenu_is_exactly_at_the_ceiling()
        => Assert.Equal(16, RootSubmenuItemCount);

    [Fact]
    public void The_emblem_submenu_stays_within_the_limit()
        => Assert.True(PaletteCatalog.Emblems.Count <= 16);

    [Fact]
    public void The_palette_holds_between_ten_and_fourteen_hues()
        => Assert.InRange(PaletteCatalog.Colors.Count, 10, 14);

    [Fact]
    public void Identifiers_are_unique()
    {
        Assert.Equal(
            PaletteCatalog.Colors.Count,
            PaletteCatalog.Colors.Select(c => c.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.Equal(
            PaletteCatalog.Emblems.Count,
            PaletteCatalog.Emblems.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Hues_sit_in_the_expected_range()
    {
        foreach (FolderColor color in PaletteCatalog.Colors)
        {
            Assert.InRange(color.Hue, 0f, 360f);
            Assert.InRange(color.SaturationScale, 0f, 3f);
            Assert.InRange(color.SaturationFloor, 0f, 1f);
        }
    }

    [Fact]
    public void Every_resource_key_resolves()
    {
        // A missing key would betray itself as a label equal to the key itself, in the menu.
        foreach (FolderColor color in PaletteCatalog.Colors)
        {
            Assert.NotEqual(color.ResourceKey, Loc.Get(color.ResourceKey));
        }

        foreach (Emblem emblem in PaletteCatalog.Emblems)
        {
            Assert.NotEqual(emblem.ResourceKey, Loc.Get(emblem.ResourceKey));
        }

        foreach (string key in new[] { "Menu_Root", "Menu_Emblem", "Menu_Reset", "Color_Neutral" })
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
    public void IconFileName_names_the_pair(string colorId, string? emblemId, string expected)
        => Assert.Equal(expected, PaletteCatalog.IconFileName(colorId, emblemId));

    [Fact]
    public void FindColor_ignores_case_and_rejects_the_unknown()
    {
        Assert.NotNull(PaletteCatalog.FindColor("BLUE"));
        Assert.Null(PaletteCatalog.FindColor("chartreuse"));
        Assert.Null(PaletteCatalog.FindColor(null));
    }

    [Fact]
    public void FindEmblem_treats_absence_as_no_emblem()
    {
        Assert.Equal(Emblem.None, PaletteCatalog.FindEmblem(null));
        Assert.Equal(Emblem.None, PaletteCatalog.FindEmblem(""));
        Assert.Equal(Emblem.NoneId, PaletteCatalog.FindEmblem("none")!.Id);
        Assert.Null(PaletteCatalog.FindEmblem("unknown"));
    }

    [Fact]
    public void IconCombinationCount_matches_the_pre_generation()
        => Assert.Equal(
            PaletteCatalog.TintableColors.Count * PaletteCatalog.Emblems.Count,
            PaletteCatalog.IconCombinationCount);

    /// <summary>
    /// The neutral color is generated and resolvable, but it is not one of the hues.
    /// </summary>
    /// <remarks>
    /// It does appear in the menu, at the end of the palette, but <c>RootCommand</c> adds it
    /// separately: it is not a tint, it means "leave the folder as it is".
    /// </remarks>
    [Fact]
    public void The_neutral_color_is_not_one_of_the_hues()
    {
        Assert.DoesNotContain(PaletteCatalog.Neutral, PaletteCatalog.Colors);
        Assert.Contains(PaletteCatalog.Neutral, PaletteCatalog.TintableColors);
        Assert.Same(PaletteCatalog.Neutral, PaletteCatalog.FindColor("neutral"));
    }

    [Fact]
    public void The_neutral_color_transforms_nothing()
    {
        Assert.True(PaletteCatalog.Neutral.IsNeutral);

        foreach (FolderColor color in PaletteCatalog.Colors)
        {
            Assert.False(color.IsNeutral, $"The hue \"{color.Id}\" must not be neutral.");
        }
    }

    [Fact]
    public void Emblem_chip_names_do_not_collide_with_the_other_icons()
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
    public void The_brand_logo_name_differs_from_every_color_name()
    {
        // logo.ico must never collide with a logo-<color>.ico tint, nor with a folder icon:
        // they all live in the same directory.
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
    public void The_logo_name_is_normalised_to_lowercase()
        => Assert.Equal("logo-blue.ico", PaletteCatalog.LogoFileName("BLUE"));

    [Fact]
    public void The_logo_name_rejects_an_empty_identifier()
        => Assert.Throws<ArgumentException>(() => PaletteCatalog.LogoFileName(string.Empty));
}
