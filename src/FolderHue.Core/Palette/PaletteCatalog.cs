namespace FolderHue.Core.Palette;

/// <summary>
/// Single source of truth for the palette: colors, emblems, and the naming of generated icons.
/// </summary>
/// <remarks>
/// This catalogue is shared by <c>FolderHue.Shell</c> (menu labels and verbs) and by
/// <c>FolderHue.App</c> (pre-generating the <c>.ico</c> files). It is deliberately static and
/// touches no disk: <c>GetTitle</c> and <c>GetIcon</c> are called every time the context menu
/// opens, inside the <c>explorer.exe</c> process (CLAUDE.md 4.4).
/// <para>
/// The submenu holds 12 colors + "Original color" + a separator + "Emblem" + "Reset color" =
/// <b>exactly 16</b> items, which is the documented ceiling. There is no headroom left: any new
/// entry means removing another one or nesting further (CLAUDE.md 4.4).
/// </para>
/// </remarks>
public static class PaletteCatalog
{
    private const float DefaultSaturationScale = 1.15f;
    private const float DefaultSaturationFloor = 0.55f;

    /// <summary>The 12 hues offered in the context menu, in display order.</summary>
    public static IReadOnlyList<FolderColor> Colors { get; } =
    [
        new("red",      "Color_Red",      358f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("orange",   "Color_Orange",    24f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("amber",    "Color_Amber",     42f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("yellow",   "Color_Yellow",    54f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("green",    "Color_Green",    120f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("emerald",  "Color_Emerald",  160f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("cyan",     "Color_Cyan",     190f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("blue",     "Color_Blue",     214f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("indigo",   "Color_Indigo",   245f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("violet",   "Color_Violet",   275f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("pink",     "Color_Pink",     330f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("graphite", "Color_Graphite",   0f, 0f,                     0f,                     -0.02f),
    ];

    /// <summary>
    /// The emblem states offered, <see cref="Emblem.None"/> included and listed first.
    /// </summary>
    public static IReadOnlyList<Emblem> Emblems { get; } =
    [
        Emblem.None,
        new("important", "Emblem_Important", EmblemGlyph.Exclamation),
        new("progress",  "Emblem_Progress",  EmblemGlyph.Arrow),
        new("done",      "Emblem_Done",      EmblemGlyph.Check),
        new("locked",    "Emblem_Locked",    EmblemGlyph.Lock),
        new("favorite",  "Emblem_Favorite",  EmblemGlyph.Star),
    ];

    /// <summary>
    /// The entry standing for "the folder's original color".
    /// </summary>
    /// <remarks>
    /// It appears at the end of the palette, labelled "Original color", and it is also the
    /// fallback used when the user puts an emblem on a folder that was never colored. Without it
    /// the code fell back to the first hue of the palette and the folder turned red along the way
    /// — placing a status marker must not decide a color on the user's behalf.
    /// </remarks>
    public static FolderColor Neutral { get; } =
        new("neutral", "Color_Neutral", FolderColor.NoHue, 1f, 0f, 0f);

    /// <summary>
    /// Every entry an icon must be pre-generated for.
    /// </summary>
    /// <remarks>
    /// The menu palette plus <see cref="Neutral"/>: <c>neutral.ico</c> is the original folder icon,
    /// and <c>neutral+done.ico</c> that same icon carrying an emblem.
    /// </remarks>
    public static IReadOnlyList<FolderColor> TintableColors { get; } = [.. Colors, Neutral];

    /// <summary>How many distinct icons the pre-generation must produce.</summary>
    public static int IconCombinationCount => TintableColors.Count * Emblems.Count;

    /// <summary>Returns the color with that identifier, or <see langword="null"/> when unknown.</summary>
    /// <param name="id">Stable color identifier, compared case-insensitively.</param>
    public static FolderColor? FindColor(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (FolderColor color in TintableColors)
        {
            if (string.Equals(color.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return color;
            }
        }

        return null;
    }

    /// <summary>Returns the emblem with that identifier, or <see langword="null"/> when unknown.</summary>
    /// <param name="id">
    /// Stable emblem identifier. <see langword="null"/> or empty is treated as
    /// <see cref="Emblem.None"/>, which keeps the call site in the shell simple.
    /// </param>
    public static Emblem? FindEmblem(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Emblem.None;
        }

        foreach (Emblem emblem in Emblems)
        {
            if (string.Equals(emblem.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return emblem;
            }
        }

        return null;
    }

    /// <summary>
    /// File name of the <c>.ico</c> holding an emblem's menu chip.
    /// </summary>
    /// <param name="emblemId">Emblem identifier, <see cref="Emblem.NoneId"/> included.</param>
    /// <returns>A file name, without a path, for instance <c>emblem-done.ico</c>.</returns>
    /// <remarks>
    /// The badge on its own, drawn large. Composited onto a folder it is only 40% of the icon,
    /// which is six pixels in a menu: illegible. The menu chip therefore draws it full size.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="emblemId"/> is empty.</exception>
    public static string EmblemChipFileName(string emblemId)
    {
        ArgumentException.ThrowIfNullOrEmpty(emblemId);
        return string.Concat("emblem-", emblemId.ToLowerInvariant(), ".ico");
    }

    /// <summary>
    /// File name of the <c>.ico</c> holding the application logo, in the brand colors.
    /// </summary>
    /// <remarks>
    /// This is the icon of the context menu's root entry. It lives next to the palette, in
    /// <c>%LOCALAPPDATA%\FolderHue\icons</c>, and is pre-generated at the same time.
    /// </remarks>
    public const string BrandLogoFileName = "logo.ico";

    /// <summary>
    /// File name of the <c>.ico</c> holding the logo tinted with one palette color.
    /// </summary>
    /// <param name="colorId">Color identifier, for instance <c>"blue"</c>.</param>
    /// <returns>A file name, without a path, for instance <c>logo-blue.ico</c>.</returns>
    /// <remarks>
    /// These tints are the chips shown in front of each color in the context menu. They go through
    /// exactly the same HSL transformation as the matching folder icon, so the chip and the result
    /// on the folder cannot drift apart.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="colorId"/> is empty.</exception>
    public static string LogoFileName(string colorId)
    {
        ArgumentException.ThrowIfNullOrEmpty(colorId);
        return string.Concat("logo-", colorId.ToLowerInvariant(), ".ico");
    }

    /// <summary>
    /// File name of the <c>.ico</c> matching a color + emblem pair.
    /// </summary>
    /// <param name="colorId">Color identifier, for instance <c>"blue"</c>.</param>
    /// <param name="emblemId">
    /// Emblem identifier. <c>"none"</c>, <see langword="null"/> or empty produce <c>blue.ico</c>;
    /// anything else produces <c>blue+important.ico</c>.
    /// </param>
    /// <returns>A file name, without a path.</returns>
    public static string IconFileName(string colorId, string? emblemId)
    {
        ArgumentException.ThrowIfNullOrEmpty(colorId);

        bool hasEmblem = !string.IsNullOrEmpty(emblemId)
            && !string.Equals(emblemId, Emblem.NoneId, StringComparison.OrdinalIgnoreCase);

        return hasEmblem
            ? string.Concat(colorId.ToLowerInvariant(), "+", emblemId!.ToLowerInvariant(), ".ico")
            : string.Concat(colorId.ToLowerInvariant(), ".ico");
    }
}
