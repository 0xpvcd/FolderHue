using System.Globalization;
using FolderHue.Core.Resources;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Locks down the application's default language.
/// </summary>
/// <remarks>
/// English is now the neutral language and French a satellite. The reverse held for a long time,
/// and the swap comes undone quietly: a <c>NeutralLanguage</c> reverting to <c>fr-FR</c>, or a
/// file being renamed, is enough for everyone to see the menu in French with no test complaining.
/// Hence these checks, which look at observable behaviour rather than at configuration.
/// </remarks>
public sealed class LocalizationTests
{
    /// <summary>Runs a lookup under a given UI culture, then restores the previous one.</summary>
    private static string ReadUnder(string culture, string key)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            return Loc.Get(key);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    [InlineData("")]
    public void Unknown_cultures_fall_back_to_english(string culture)
    {
        Assert.Equal("Reset color", ReadUnder(culture, "Menu_Reset"));
        Assert.Equal("Original color", ReadUnder(culture, "Color_Neutral"));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("fr-CA")]
    [InlineData("fr")]
    public void French_cultures_get_the_french_satellite(string culture)
    {
        Assert.Equal("Réinitialiser la couleur", ReadUnder(culture, "Menu_Reset"));
    }

    /// <summary>
    /// A missing key returns the key itself: an odd-looking label beats an exception crossing the
    /// COM boundary, which would bring Explorer down (CLAUDE.md 6.5).
    /// </summary>
    [Fact]
    public void A_missing_key_returns_the_key_rather_than_throwing()
    {
        Assert.Equal("Cle_Inexistante", Loc.Get("Cle_Inexistante"));
        Assert.Equal(string.Empty, Loc.Get(string.Empty));
    }

    /// <summary>
    /// Both languages must cover exactly the same keys: a menu entry without a translation would
    /// fall back to English in the middle of a French menu.
    /// </summary>
    [Fact]
    public void Both_languages_answer_every_menu_key()
    {
        string[] keys =
        [
            "Menu_Root", "Menu_Emblem", "Menu_Reset", "Color_Neutral",
            "Color_Red", "Color_Orange", "Color_Amber", "Color_Yellow",
            "Color_Green", "Color_Emerald", "Color_Cyan", "Color_Blue",
            "Color_Indigo", "Color_Violet", "Color_Pink", "Color_Graphite",
            "Emblem_None", "Emblem_Important", "Emblem_Progress",
            "Emblem_Done", "Emblem_Locked", "Emblem_Favorite",
        ];

        foreach (string key in keys)
        {
            Assert.NotEqual(key, ReadUnder("en-US", key));
            Assert.NotEqual(key, ReadUnder("fr-FR", key));
        }
    }
}
