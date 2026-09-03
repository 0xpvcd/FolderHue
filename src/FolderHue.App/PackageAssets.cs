using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using FolderHue.App.Icons;

namespace FolderHue.App;

/// <summary>
/// Produit les logos exiges par le manifeste MSIX.
/// </summary>
/// <remarks>
/// Les logos sont derives du logo de l'application, redessine en vectoriel par
/// <see cref="LogoArtwork"/> et centre sur un fond transparent. Rien a versionner en binaire, et
/// le paquet, le menu contextuel et l'application affichent forcement la meme marque.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class PackageAssets
{
    private static readonly (string FileName, int Size)[] Logos =
    [
        ("Square44x44Logo.png", 44),
        ("Square44x44Logo.targetsize-24_altform-unplated.png", 24),
        ("Square150x150Logo.png", 150),
        ("StoreLogo.png", 50),
        ("Wide310x150Logo.png", 150),
    ];

    /// <summary>Ecrit les logos du paquet dans un dossier.</summary>
    /// <param name="outputDirectory">Dossier <c>Assets</c> du paquet.</param>
    /// <exception cref="ArgumentException"><paramref name="outputDirectory"/> est vide.</exception>
    internal static void Generate(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        using Bitmap source = LogoArtwork.Render(256);

        foreach ((string fileName, int size) in Logos)
        {
            bool wide = fileName.StartsWith("Wide", StringComparison.Ordinal);
            int width = wide ? size * 310 / 150 : size;

            using var canvas = new Bitmap(width, size, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(canvas))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                // Marge de 10 % : les vignettes du Store recadrent legerement.
                int glyph = (int)(size * 0.80f);
                graphics.DrawImage(source, (width - glyph) / 2, (size - glyph) / 2, glyph, glyph);
            }

            canvas.Save(Path.Combine(outputDirectory, fileName), ImageFormat.Png);
        }

        Console.Out.WriteLine($"{Logos.Length} logo(s) ecrit(s) dans {outputDirectory}");
    }
}
