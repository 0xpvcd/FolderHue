namespace FolderHue.Core.Icons;

/// <summary>
/// The resolutions embedded in every generated <c>.ico</c>.
/// </summary>
public static class IconSizes
{
    /// <summary>
    /// Every size produced, in pixels, ascending.
    /// </summary>
    /// <remarks>
    /// The list covers Explorer's views, from Details (16 px) to Extra large icons (256 px), plus
    /// the intermediate steps DPI scaling asks for (CLAUDE.md 4.3).
    /// </remarks>
    public static IReadOnlyList<int> All { get; } = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    /// <summary>
    /// The resolutions embedded in the logo <c>.ico</c> files.
    /// </summary>
    /// <remarks>
    /// A menu chip is never drawn beyond about fifty pixels: the shell asks for
    /// <c>SM_CXSMICON</c>, which is 16 px at 100% and 32 px at 200%. Embedding the large frames
    /// from <see cref="All"/> would only make the file heavier.
    /// </remarks>
    public static IReadOnlyList<int> Logo { get; } = [16, 20, 24, 32, 40, 48, 64];

    /// <summary>Largest size an ICO container can represent.</summary>
    public const int MaxSize = 256;

    /// <summary>
    /// Indicates whether a size must be PNG-encoded inside the ICO container.
    /// </summary>
    /// <param name="size">The size, in pixels.</param>
    /// <returns><see langword="true"/> for 256 px, <see langword="false"/> otherwise.</returns>
    /// <remarks>
    /// PNG encoding is mandatory for the 256 px frame (CLAUDE.md 4.3). Smaller sizes stay as DIBs,
    /// the format every shell view has always handled best.
    /// </remarks>
    public static bool UsePng(int size) => size >= MaxSize;
}
