using FolderHue.Core.Icons;
using FolderHue.Core.Palette;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Checks the central property of coloring: replace the hue without touching anything else
/// (CLAUDE.md 4.3).
/// </summary>
public sealed class HslTintTests
{
    private static readonly FolderColor Blue = new("blue", "Color_Blue", 214f, 1.15f, 0.55f, 0f);

    [Fact]
    public void Apply_preserves_alpha()
    {
        byte[] pixels = [10, 120, 200, 137];

        HslTint.Apply(pixels, Blue);

        Assert.Equal(137, pixels[3]);
    }

    [Fact]
    public void Apply_preserves_lightness()
    {
        byte[] pixels = [40, 90, 190, 255];
        HslColor before = HslColor.FromRgb(pixels[2], pixels[1], pixels[0]);

        HslTint.Apply(pixels, Blue);

        HslColor after = HslColor.FromRgb(pixels[2], pixels[1], pixels[0]);
        Assert.Equal(before.L, after.L, precision: 2);
    }

    [Fact]
    public void Apply_replaces_the_hue()
    {
        byte[] pixels = [40, 200, 60, 255];

        HslTint.Apply(pixels, Blue);

        HslColor after = HslColor.FromRgb(pixels[2], pixels[1], pixels[0]);
        Assert.Equal(Blue.Hue, after.H, precision: 0);
    }

    [Fact]
    public void Apply_skips_fully_transparent_pixels()
    {
        byte[] pixels = [11, 22, 33, 0];

        HslTint.Apply(pixels, Blue);

        Assert.Equal<byte[]>([11, 22, 33, 0], pixels);
    }

    [Fact]
    public void Apply_colors_neutral_grey_thanks_to_the_saturation_floor()
    {
        // A grey template has zero saturation: without a floor, changing the hue would do nothing.
        byte[] pixels = [128, 128, 128, 255];

        HslTint.Apply(pixels, Blue);

        HslColor after = HslColor.FromRgb(pixels[2], pixels[1], pixels[0]);
        Assert.True(after.S > 0.4f, $"Saturation attendue au-dessus du plancher, obtenue {after.S}.");
        Assert.Equal(Blue.Hue, after.H, precision: 0);
    }

    [Fact]
    public void Apply_leaves_the_highlights_nearly_neutral()
    {
        // The floor is weighted towards the midtones: white must stay white, or the icon loses its
        // relief.
        byte[] pixels = [252, 252, 252, 255];

        HslTint.Apply(pixels, Blue);

        HslColor after = HslColor.FromRgb(pixels[2], pixels[1], pixels[0]);
        Assert.True(after.S < 0.1f, $"Une haute lumiere ne doit pas se saturer, obtenue {after.S}.");
    }

    [Fact]
    public void Apply_with_a_desaturating_color_produces_grey()
    {
        FolderColor graphite = new("graphite", "Color_Graphite", 0f, 0f, 0f, 0f);
        byte[] pixels = [40, 90, 190, 255];

        HslTint.Apply(pixels, graphite);

        Assert.Equal(pixels[0], pixels[1]);
        Assert.Equal(pixels[1], pixels[2]);
    }

    [Fact]
    public void Apply_refuses_a_badly_sized_buffer()
    {
        byte[] pixels = [1, 2, 3];

        Assert.Throws<ArgumentException>(() => HslTint.Apply(pixels, Blue));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(255, 255, 255)]
    [InlineData(255, 0, 0)]
    [InlineData(0, 255, 0)]
    [InlineData(0, 0, 255)]
    [InlineData(17, 200, 143)]
    public void Converting_RGB_to_HSL_and_back_is_reversible(byte r, byte g, byte b)
    {
        (byte outR, byte outG, byte outB) = HslColor.FromRgb(r, g, b).ToRgb();

        Assert.Equal(r, outR);
        Assert.Equal(g, outG);
        Assert.Equal(b, outB);
    }

    [Theory]
    [InlineData(-30f, 330f)]
    [InlineData(390f, 30f)]
    [InlineData(360f, 0f)]
    public void Normalize_brings_the_hue_back_into_range(float input, float expected)
    {
        Assert.Equal(expected, HslColor.Normalize(input), precision: 3);
    }

    [Fact]
    public void MidtoneWeight_peaks_at_mid_lightness_and_is_zero_at_the_extremes()
    {
        Assert.Equal(1f, HslTint.MidtoneWeight(0.5f), precision: 3);
        Assert.Equal(0f, HslTint.MidtoneWeight(0f), precision: 3);
        Assert.Equal(0f, HslTint.MidtoneWeight(1f), precision: 3);
    }
}
