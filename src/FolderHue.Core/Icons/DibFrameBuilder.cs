namespace FolderHue.Core.Icons;

/// <summary>
/// Construit la representation DIB d'une trame d'icone : en-tete, image XOR et masque AND.
/// </summary>
/// <remarks>
/// C'est le format historique des trames d'un <c>.ico</c>. Contrairement au BMP de fichier, il n'y
/// a <b>pas</b> de <c>BITMAPFILEHEADER</c>, et la hauteur declaree dans l'en-tete vaut le double de
/// la hauteur reelle : elle couvre l'image XOR empilee sur le masque AND.
/// </remarks>
public static class DibFrameBuilder
{
    /// <summary>Taille d'un <c>BITMAPINFOHEADER</c>, en octets.</summary>
    public const int BitmapInfoHeaderSize = 40;

    /// <summary>
    /// Assemble une trame DIB 32 bits a partir d'un tampon BGRA oriente de haut en bas.
    /// </summary>
    /// <param name="bgraTopDown">
    /// Pixels BGRA, alpha non premultiplie, premiere ligne en haut. La longueur doit valoir
    /// <paramref name="width"/> * <paramref name="height"/> * 4.
    /// </param>
    /// <param name="width">Largeur en pixels.</param>
    /// <param name="height">Hauteur en pixels.</param>
    /// <returns>Les octets de la trame, prets pour <see cref="IcoFrame"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Une dimension est hors de [1, 256].</exception>
    /// <exception cref="ArgumentException">La longueur du tampon ne correspond pas aux dimensions.</exception>
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
                $"Le tampon doit contenir {expected} octets pour {width}x{height}, il en contient {bgraTopDown.Length}.",
                nameof(bgraTopDown));
        }

        // Les lignes 32 bpp sont naturellement alignees sur 4 octets ; le masque AND, en 1 bpp, ne
        // l'est pas et doit etre complete.
        int xorStride = width * 4;
        int andStride = ((width + 31) / 32) * 4;
        int xorSize = xorStride * height;
        int andSize = andStride * height;

        byte[] result = new byte[BitmapInfoHeaderSize + xorSize + andSize];
        Span<byte> span = result;

        WriteHeader(span, width, height, xorSize + andSize);

        // Le DIB se lit de bas en haut : on inverse l'ordre des lignes.
        Span<byte> xor = span.Slice(BitmapInfoHeaderSize, xorSize);
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> source = bgraTopDown.Slice(y * xorStride, xorStride);
            source.CopyTo(xor.Slice((height - 1 - y) * xorStride, xorStride));
        }

        // Masque AND : bit a 1 = pixel transparent. En 32 bpp le shell se fie a l'alpha, mais le
        // masque doit exister et rester coherent pour les chemins de rendu anciens.
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
        BinaryLittleEndian.WriteInt32(destination.Slice(8, 4), height * 2);      // biHeight : XOR + AND
        BinaryLittleEndian.WriteInt16(destination.Slice(12, 2), 1);              // biPlanes
        BinaryLittleEndian.WriteInt16(destination.Slice(14, 2), 32);             // biBitCount
        BinaryLittleEndian.WriteInt32(destination.Slice(16, 4), 0);              // biCompression = BI_RGB
        BinaryLittleEndian.WriteInt32(destination.Slice(20, 4), imageSize);      // biSizeImage
        // biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant restent a zero.
    }
}
