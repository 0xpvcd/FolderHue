using FolderHue.Core.Icons;
using FolderHue.Core.Palette;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Verifie la propriete centrale de la colorisation : remplacer la teinte sans toucher au reste
/// (CLAUDE.md §4.3).
/// </summary>
public sealed class HslTintTests
{
    private static readonly FolderColor Blue = new("blue", "Color_Blue", 214f, 1.15f, 0.55f, 0f);

    [Fact]
    public void Apply_ConserveLAlpha()
    {
        byte[] pixels = [10, 120, 200, 137];

        HslTint.Apply(pixels, Blue);

        Assert.Equal(137, pixels[3]);
    }

    [Fact]
    public void Apply_ConserveLaLuminance()
    {
        byte[] pixels = [40, 90, 190, 255];
        HslColor before = HslColor.FromRgb(pixels[2], pixels[1], pixels[0]);

        HslTint.Apply(pixels, Blue);

        HslColor after = HslColor.FromRgb(pixels[2], pixels[1], pixels[0]);
        Assert.Equal(before.L, after.L, precision: 2);
    }

    [Fact]
    public void Apply_RemplaceLaTeinte()
    {
        byte[] pixels = [40, 200, 60, 255];

        HslTint.Apply(pixels, Blue);

        HslColor after = HslColor.FromRgb(pixels[2], pixels[1], pixels[0]);
        Assert.Equal(Blue.Hue, after.H, precision: 0);
    }

    [Fact]
    public void Apply_IgnoreLesPixelsTotalementTransparents()
    {
        byte[] pixels = [11, 22, 33, 0];

        HslTint.Apply(pixels, Blue);

        Assert.Equal<byte[]>([11, 22, 33, 0], pixels);
    }

    [Fact]
    public void Apply_ColoreUnGrisNeutreGraceAuPlancherDeSaturation()
    {
        // Un gabarit gris a une saturation nulle : sans plancher, changer la teinte ne ferait rien.
        byte[] pixels = [128, 128, 128, 255];

        HslTint.Apply(pixels, Blue);

        HslColor after = HslColor.FromRgb(pixels[2], pixels[1], pixels[0]);
        Assert.True(after.S > 0.4f, $"Saturation attendue au-dessus du plancher, obtenue {after.S}.");
        Assert.Equal(Blue.Hue, after.H, precision: 0);
    }

    [Fact]
    public void Apply_LaisseLesHautesLumieresQuasiNeutres()
    {
        // Le plancher est pondere par les tons moyens : un blanc doit rester blanc, sinon l'icone
        // perd son relief.
        byte[] pixels = [252, 252, 252, 255];

        HslTint.Apply(pixels, Blue);

        HslColor after = HslColor.FromRgb(pixels[2], pixels[1], pixels[0]);
        Assert.True(after.S < 0.1f, $"Une haute lumiere ne doit pas se saturer, obtenue {after.S}.");
    }

    [Fact]
    public void Apply_AvecUneCouleurDesaturanteProduitUnGris()
    {
        FolderColor graphite = new("graphite", "Color_Graphite", 0f, 0f, 0f, 0f);
        byte[] pixels = [40, 90, 190, 255];

        HslTint.Apply(pixels, graphite);

        Assert.Equal(pixels[0], pixels[1]);
        Assert.Equal(pixels[1], pixels[2]);
    }

    [Fact]
    public void Apply_RefuseUnTamponMalDimensionne()
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
    public void ConversionRgbHslRgb_EstReversible(byte r, byte g, byte b)
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
    public void Normalize_RameneLaTeinteDansLIntervalle(float input, float expected)
    {
        Assert.Equal(expected, HslColor.Normalize(input), precision: 3);
    }

    [Fact]
    public void MidtoneWeight_EstMaximalAMiLuminanceEtNulAuxExtremes()
    {
        Assert.Equal(1f, HslTint.MidtoneWeight(0.5f), precision: 3);
        Assert.Equal(0f, HslTint.MidtoneWeight(0f), precision: 3);
        Assert.Equal(0f, HslTint.MidtoneWeight(1f), precision: 3);
    }
}
