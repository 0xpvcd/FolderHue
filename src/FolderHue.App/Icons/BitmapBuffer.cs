using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FolderHue.App.Icons;

/// <summary>
/// Conversions between GDI+ <see cref="Bitmap"/> objects and raw BGRA buffers.
/// </summary>
/// <remarks>
/// This is the border between the graphics world, confined to <c>FolderHue.App</c>, and the pure
/// code in <c>FolderHue.Core</c> that tints and encodes (CLAUDE.md 2.1). The format is always
/// <see cref="PixelFormat.Format32bppArgb"/>: alpha <b>not premultiplied</b>, exactly the
/// convention <c>HslTint</c> and <c>DibFrameBuilder</c> expect.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class BitmapBuffer
{
    /// <summary>Builds a bitmap from a square BGRA buffer.</summary>
    /// <param name="bgra">Non-premultiplied BGRA pixels, of length <c>size * size * 4</c>.</param>
    /// <param name="size">Side of the image, in pixels.</param>
    /// <returns>A bitmap the caller takes ownership of.</returns>
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

    /// <summary>Extracts a bitmap's BGRA pixels.</summary>
    /// <param name="bitmap">The source bitmap.</param>
    /// <returns>A non-premultiplied BGRA buffer, row by row, top-down.</returns>
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
