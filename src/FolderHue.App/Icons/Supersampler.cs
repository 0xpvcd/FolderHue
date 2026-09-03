using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace FolderHue.App.Icons;

/// <summary>
/// Dessine une image en grand puis la reduit a la taille demandee.
/// </summary>
/// <remarks>
/// GDI+ anticrenele correctement une forme de 64 px, beaucoup moins bien la meme forme dessinee
/// directement en 16 px : les coins arrondis et les petits cercles y decrochent. Toutes les
/// vignettes du projet passent donc par ici.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class Supersampler
{
    /// <summary>Facteur d'agrandissement applique avant reduction.</summary>
    internal const int Factor = 4;

    /// <summary>
    /// Rend une image carree en surechantillonnant.
    /// </summary>
    /// <param name="size">Cote final, en pixels.</param>
    /// <param name="draw">
    /// Dessine le contenu. Recoit la surface et le cote <b>agrandi</b> : tout doit etre exprime
    /// relativement a ce cote, jamais en pixels absolus.
    /// </param>
    /// <returns>Un bitmap dont l'appelant devient proprietaire.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="draw"/> vaut <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> n'est pas positif.</exception>
    internal static Bitmap Render(int size, Action<Graphics, int> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        int large = size * Factor;

        using var source = new Bitmap(large, large, PixelFormat.Format32bppArgb);

        using (Graphics graphics = Graphics.FromImage(source))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            draw(graphics, large);
        }

        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        using (Graphics graphics = Graphics.FromImage(result))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.DrawImage(
                source, new Rectangle(0, 0, size, size), 0, 0, large, large, GraphicsUnit.Pixel);
        }

        return result;
    }
}
