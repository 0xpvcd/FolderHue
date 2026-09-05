using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace FolderHue.App.Icons;

/// <summary>
/// Draws an image large, then shrinks it to the requested size.
/// </summary>
/// <remarks>
/// GDI+ antialiases a 64 px shape properly, and the same shape drawn straight at 16 px far less
/// well: rounded corners and small circles break down. Every thumbnail in the project therefore
/// goes through here.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class Supersampler
{
    /// <summary>Magnification factor applied before shrinking.</summary>
    internal const int Factor = 4;

    /// <summary>
    /// Renders a square image with supersampling.
    /// </summary>
    /// <param name="size">Final side, in pixels.</param>
    /// <param name="draw">
    /// Draws the content. Receives the surface and the <b>magnified</b> side: everything must be
    /// expressed relative to that side, never in absolute pixels.
    /// </param>
    /// <returns>A bitmap the caller takes ownership of.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="draw"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is not positive.</exception>
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
