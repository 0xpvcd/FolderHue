namespace FolderHue.Core.Palette;

/// <summary>
/// A status marker composited onto the folder icon (important, in progress, done...).
/// </summary>
/// <remarks>
/// Emblems are composited into the generated <c>.ico</c>, never through
/// <c>IShellIconOverlayIdentifier</c>: Windows loads only about fifteen overlays in total, and
/// OneDrive, Dropbox or Git already take most of them (CLAUDE.md 2).
/// </remarks>
/// <param name="Id">
/// Stable lowercase identifier. <see cref="None"/> uses <c>"none"</c>, which means "no emblem"
/// and does not appear in the icon file name.
/// </param>
/// <param name="ResourceKey">Key in <c>Strings.resx</c> for the label shown to the user.</param>
/// <param name="Glyph">
/// The shape the renderer draws. This is a logical value, not a character to display as-is:
/// <c>FolderHue.App</c> draws every glyph as vector artwork.
/// </param>
public sealed record Emblem(string Id, string ResourceKey, EmblemGlyph Glyph)
{
    /// <summary>Identifier standing for the absence of an emblem.</summary>
    public const string NoneId = "none";

    /// <summary>No emblem: the icon carries the color only.</summary>
    public static Emblem None { get; } = new(NoneId, "Emblem_None", EmblemGlyph.None);
}

/// <summary>Geometric shape drawn for an emblem.</summary>
public enum EmblemGlyph
{
    /// <summary>Nothing is drawn.</summary>
    None = 0,

    /// <summary>Exclamation mark - "important".</summary>
    Exclamation,

    /// <summary>Arrow or chevron - "in progress".</summary>
    Arrow,

    /// <summary>Check mark - "done".</summary>
    Check,

    /// <summary>Padlock - "locked".</summary>
    Lock,

    /// <summary>Star - "favorite".</summary>
    Star,
}
