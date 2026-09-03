using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FolderHue.App.Icons;

/// <summary>
/// Conversions entre <see cref="Bitmap"/> GDI+ et tampons BGRA bruts.
/// </summary>
/// <remarks>
/// C'est la frontiere entre le monde graphique, confine a <c>FolderHue.App</c>, et le code pur de
/// <c>FolderHue.Core</c> qui teinte et encode (CLAUDE.md §2.1). Le format est toujours
/// <see cref="PixelFormat.Format32bppArgb"/> : alpha <b>non premultiplie</b>, exactement la
/// convention attendue par <c>HslTint</c> et <c>DibFrameBuilder</c>.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class BitmapBuffer
{
    /// <summary>Construit un bitmap a partir d'un tampon BGRA carre.</summary>
    /// <param name="bgra">Pixels BGRA non premultiplies, de longueur <c>size * size * 4</c>.</param>
    /// <param name="size">Cote de l'image, en pixels.</param>
    /// <returns>Un bitmap dont l'appelant devient proprietaire.</returns>
    internal static Bitmap FromBgra(byte[] bgra, int size)
    {
        ArgumentNullException.ThrowIfNull(bgra);

        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        try
        {
            int rowBytes = size * 4;
            for (int y = 0; y < size; y++)
            {
                Marshal.Copy(bgra, y * rowBytes, data.Scan0 + (y * data.Stride), rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    /// <summary>Extrait les pixels BGRA d'un bitmap.</summary>
    /// <param name="bitmap">Le bitmap source.</param>
    /// <returns>Un tampon BGRA non premultiplie, ligne par ligne, de haut en bas.</returns>
    internal static byte[] ToBgra(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        int width = bitmap.Width;
        int height = bitmap.Height;
        byte[] pixels = new byte[width * height * 4];

        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int rowBytes = width * 4;
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + (y * data.Stride), pixels, y * rowBytes, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return pixels;
    }
}
