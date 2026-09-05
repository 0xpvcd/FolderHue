namespace FolderHue.Core.Palette;

/// <summary>
/// One predefined hue from the palette.
/// </summary>
/// <remarks>
/// Coloring is not an RGB multiply: the hue is replaced in HSL space and the saturation is
/// modulated, while the template's lightness and alpha channel are preserved (CLAUDE.md 4.3).
/// The values below therefore describe a transformation, not an absolute color.
/// </remarks>
/// <param name="Id">
/// Stable identifier, lowercase, no spaces. It is the key in the <c>applied.json</c> journal, in
/// the name of the generated <c>.ico</c> file and in the context menu verbs: once published it
/// must never change.
/// </param>
/// <param name="ResourceKey">Key in <c>Strings.resx</c> for the label shown to the user.</param>
/// <param name="Hue">Target hue in degrees, within [0, 360[.</param>
/// <param name="SaturationScale">
/// Factor applied to each pixel's original saturation. Zero produces a grey result.
/// </param>
/// <param name="SaturationFloor">
/// Minimum saturation forced onto near-neutral pixels, weighted by how close they are to the
/// midtones. Without that floor, a grey template would stay grey after a hue change.
/// </param>
/// <param name="LightnessDelta">Lightness offset applied after the hue change, within [-1, 1].</param>
public sealed record FolderColor(
    string Id,
    string ResourceKey,
    float Hue,
    float SaturationScale,
    float SaturationFloor,
    float LightnessDelta)
{
    /// <summary>
    /// Sentinel hue meaning "no transformation at all".
    /// </summary>
    /// <remarks>
    /// A valid hue lives in [0, 360[, so a negative value cannot be one. It expresses "leave the
    /// template alone", which is what putting an emblem on a folder the user never colored needs.
    /// </remarks>
    public const float NoHue = -1f;

    /// <summary>
    /// Indicates that this entry leaves the template untouched.
    /// </summary>
    /// <remarks>
    /// Not to be confused with <c>graphite</c>, which genuinely desaturates the icon: a neutral
    /// entry changes nothing whatsoever.
    /// </remarks>
    public bool IsNeutral => Hue < 0f;
}
