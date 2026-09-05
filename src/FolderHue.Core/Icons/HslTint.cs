using FolderHue.Core.Palette;

namespace FolderHue.Core.Icons;

/// <summary>
/// Applies a palette hue to a 32-bit bitmap, in HSL space.
/// </summary>
/// <remarks>
/// Deliberately pure code: no graphics dependency, therefore NativeAOT-compatible and testable
/// without Windows (CLAUDE.md 2.1). Decoding and encoding images is <c>FolderHue.App</c>'s job.
/// </remarks>
public static class HslTint
{
    /// <summary>
    /// Tints the buffer in place.
    /// </summary>
    /// <param name="bgra">
    /// Pixels as 8-bit-per-channel BGRA, alpha <b>not premultiplied</b>. The length must be a
    /// multiple of 4.
    /// </param>
    /// <param name="color">The hue to apply.</param>
    /// <remarks>
    /// For each pixel: alpha is kept as-is, lightness is kept (give or take
    /// <see cref="FolderColor.LightnessDelta"/>) and only the hue is replaced. That is what
    /// preserves the template's shading and relief; a plain RGB multiply would give a flat result
    /// (CLAUDE.md 4.3).
    /// <para>
    /// Fully transparent pixels are skipped: their color components are meaningless, and tinting
    /// them would make a halo appear when the image is resized.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The length of <paramref name="bgra"/> is not a multiple of 4.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="color"/> is <see langword="null"/>.</exception>
    public static void Apply(Span<byte> bgra, FolderColor color)
    {
        ArgumentNullException.ThrowIfNull(color);

        if (bgra.Length % 4 != 0)
        {
            throw new ArgumentException("The BGRA buffer length must be a multiple of 4.", nameof(bgra));
        }

        if (color.IsNeutral)
        {
            // "No hue": the template is rendered untouched. That is what allows an emblem to be
            // placed on a folder without forcing a color on it along the way.
            return;
        }

        float hue = HslColor.Normalize(color.Hue);

        for (int i = 0; i < bgra.Length; i += 4)
        {
            if (bgra[i + 3] == 0)
            {
                continue;
            }

            HslColor source = HslColor.FromRgb(bgra[i + 2], bgra[i + 1], bgra[i]);

            float lightness = Math.Clamp(source.L + color.LightnessDelta, 0f, 1f);
            float saturation = Math.Clamp(source.S * color.SaturationScale, 0f, 1f);
            float floor = color.SaturationFloor * MidtoneWeight(lightness);

            if (saturation < floor)
            {
                saturation = floor;
            }

            (byte r, byte g, byte b) = new HslColor(hue, saturation, lightness).ToRgb();

            bgra[i] = b;
            bgra[i + 1] = g;
            bgra[i + 2] = r;

            // bgra[i + 3]: alpha left untouched, on purpose.
        }
    }

    /// <summary>
    /// How much a pixel counts as a midtone: 1 at mid-lightness, 0 at pure black and pure white.
    /// </summary>
    /// <param name="lightness">The pixel's lightness, within [0, 1].</param>
    /// <returns>The weight, within [0, 1].</returns>
    /// <remarks>
    /// The saturation floor applies to midtones only. Without this weighting the folder's
    /// highlights would take the color too and the icon would lose its relief.
    /// </remarks>
    public static float MidtoneWeight(float lightness)
    {
        float l = Math.Clamp(lightness, 0f, 1f);
        return Math.Clamp(4f * l * (1f - l), 0f, 1f);
    }
}
