namespace FolderHue.Core.Icons;

/// <summary>
/// Builds the DIB representation of an icon frame: header, XOR image and AND mask.
/// </summary>
/// <remarks>
/// This is the historical format of an <c>.ico</c> frame. Unlike a BMP file there is <b>no</b>
/// <c>BITMAPFILEHEADER</c>, and the height declared in the header is twice the real height: it
/// covers the XOR image stacked on top of the AND mask.
/// </remarks>
public static class DibFrameBuilder
{
    /// <summary>Size of a <c>BITMAPINFOHEADER</c>, in bytes.</summary>
    public const int BitmapInfoHeaderSize = 40;

    /// <summary>
    /// Assembles a 32-bit DIB frame from a top-down BGRA buffer.
    /// </summary>
    /// <param name="bgraTopDown">
    /// BGRA pixels, alpha not premultiplied, first row at the top. The length must be
    /// <paramref name="width"/> * <paramref name="height"/> * 4.
    /// </param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <returns>The frame bytes, ready for <see cref="IcoFrame"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is outside [1, 256].</exception>
    /// <exception cref="ArgumentException">The buffer length does not match the dimensions.</exception>
    public static byte[] Build(ReadOnlySpan<byte> bgraTopDown, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, IconSizes.MaxSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, IconSizes.MaxSize);

        int expected = width * height * 4;
        if (bgraTopDown.Length != expected)
        {
            throw new ArgumentException(
                $"The buffer must hold {expected} bytes for {width}x{height}, it holds {bgraTopDown.Length}.",
                nameof(bgraTopDown));
        }

        // 32 bpp rows are naturally aligned on 4 bytes; the 1 bpp AND mask is not, and has to be
        // padded.
        int xorStride = width * 4;
        int andStride = ((width + 31) / 32) * 4;
        int xorSize = xorStride * height;
        int andSize = andStride * height;

        byte[] result = new byte[BitmapInfoHeaderSize + xorSize + andSize];
        Span<byte> span = result;

        WriteHeader(span, width, height, xorSize + andSize);

        // A DIB reads bottom-up: the row order is reversed.
        Span<byte> xor = span.Slice(BitmapInfoHeaderSize, xorSize);
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> source = bgraTopDown.Slice(y * xorStride, xorStride);
            source.CopyTo(xor.Slice((height - 1 - y) * xorStride, xorStride));
        }

        // AND mask: a set bit means a transparent pixel. At 32 bpp the shell trusts the alpha
        // channel, but the mask must exist and stay consistent for the older rendering paths.
        Span<byte> and = span.Slice(BitmapInfoHeaderSize + xorSize, andSize);
        for (int y = 0; y < height; y++)
        {
            int rowStart = (height - 1 - y) * andStride;
            for (int x = 0; x < width; x++)
            {
                if (bgraTopDown[(y * xorStride) + (x * 4) + 3] == 0)
                {
                    and[rowStart + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                }
            }
        }

        return result;
    }

    private static void WriteHeader(Span<byte> destination, int width, int height, int imageSize)
    {
        BinaryLittleEndian.WriteInt32(destination[..4], BitmapInfoHeaderSize);   // biSize
        BinaryLittleEndian.WriteInt32(destination.Slice(4, 4), width);           // biWidth
        BinaryLittleEndian.WriteInt32(destination.Slice(8, 4), height * 2);      // biHeight: XOR + AND
        BinaryLittleEndian.WriteInt16(destination.Slice(12, 2), 1);              // biPlanes
        BinaryLittleEndian.WriteInt16(destination.Slice(14, 2), 32);             // biBitCount
        BinaryLittleEndian.WriteInt32(destination.Slice(16, 4), 0);              // biCompression = BI_RGB
        BinaryLittleEndian.WriteInt32(destination.Slice(20, 4), imageSize);      // biSizeImage
        // biXPelsPerMeter, biYPelsPerMeter, biClrUsed and biClrImportant stay at zero.
    }
}
