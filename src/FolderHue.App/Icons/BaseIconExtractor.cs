using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FolderHue.Core.Icons;

namespace FolderHue.App.Icons;

/// <summary>
/// Extrait l'icone de dossier native de la machine pour servir de gabarit a la colorisation.
/// </summary>
/// <remarks>
/// Aucun asset Microsoft n'est redistribue : l'extraction a lieu sur le poste de l'utilisateur, a
/// partir de son propre shell. C'est aussi ce qui fait qu'une icone colorisee ressemble a
/// Windows 10 sur Windows 10 et a Windows 11 sur Windows 11, sans code specifique.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class BaseIconExtractor
{
    /// <summary>
    /// Extrait le gabarit a toutes les resolutions de <see cref="IconSizes.All"/>.
    /// </summary>
    /// <returns>
    /// Un tampon BGRA de haut en bas, alpha non premultiplie, par taille demandee.
    /// </returns>
    /// <exception cref="InvalidOperationException">Le shell n'a pas fourni d'icone de dossier.</exception>
    internal static IReadOnlyDictionary<int, byte[]> Extract()
    {
        (string path, int index) = LocateFolderIcon();
        var frames = new Dictionary<int, byte[]>(IconSizes.All.Count);

        foreach (int size in IconSizes.All)
        {
            frames[size] = ExtractSize(path, index, size)
                ?? throw new InvalidOperationException(
                    $"Impossible d'extraire l'icone de dossier en {size} px depuis « {path} ».");
        }

        return frames;
    }

    /// <summary>
    /// Determine le fichier et l'index de l'icone de dossier du shell.
    /// </summary>
    /// <returns>Le chemin du fichier de ressources et l'index de l'icone.</returns>
    /// <remarks>
    /// On interroge le shell plutot que de coder « imageres.dll,-3 » en dur : l'emplacement a
    /// deja change entre versions de Windows.
    /// </remarks>
    private static (string Path, int Index) LocateFolderIcon()
    {
        var info = new NativeMethods.StockIconInfo
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.StockIconInfo>(),
        };

        int hr = NativeMethods.SHGetStockIconInfo(
            NativeMethods.SIID_FOLDER,
            NativeMethods.SHGSI_ICONLOCATION,
            ref info);

        if (hr != 0)
        {
            throw new InvalidOperationException(
                $"SHGetStockIconInfo a echoue (HRESULT 0x{hr:X8}).");
        }

        string path = ReadPath(ref info);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Le shell n'a pas indique de fichier d'icone de dossier.");
        }

        return (Environment.ExpandEnvironmentVariables(path), info.IconIndex);
    }

    private static unsafe string ReadPath(ref NativeMethods.StockIconInfo info)
    {
        fixed (char* buffer = info.Path)
        {
            return new string(buffer);
        }
    }

    /// <summary>
    /// Extrait une resolution donnee, en essayant les deux conventions d'index.
    /// </summary>
    /// <param name="path">Fichier de ressources.</param>
    /// <param name="index">Index annonce par le shell.</param>
    /// <param name="size">Taille souhaitee, en pixels.</param>
    /// <returns>Le tampon BGRA, ou <see langword="null"/> en cas d'echec.</returns>
    /// <remarks>
    /// Un index negatif designe un identifiant de ressource, un index positif une position. Le
    /// shell ne dit pas laquelle des deux conventions il emploie : on tente l'index tel quel, puis
    /// son oppose.
    /// </remarks>
    private static unsafe byte[]? ExtractSize(string path, int index, int size)
    {
        foreach (int candidate in new[] { index, -index })
        {
            IntPtr icon = IntPtr.Zero;
            uint id = 0;

            uint extracted = NativeMethods.PrivateExtractIcons(
                path, candidate, size, size, &icon, &id, 1, 0);

            if (extracted == 0 || icon == IntPtr.Zero)
            {
                continue;
            }

            try
            {
                byte[]? pixels = ReadIconPixels(icon, size);
                if (pixels is not null)
                {
                    return pixels;
                }
            }
            finally
            {
                NativeMethods.DestroyIcon(icon);
            }
        }

        return null;
    }

    /// <summary>
    /// Lit les pixels BGRA d'une icone.
    /// </summary>
    /// <param name="icon">Handle de l'icone.</param>
    /// <param name="size">Taille attendue, en pixels.</param>
    /// <returns>Le tampon, ou <see langword="null"/> si la lecture a echoue.</returns>
    /// <remarks>
    /// <c>Bitmap.FromHicon</c> serait plus court mais perd le canal alpha sur beaucoup d'icones :
    /// on passe donc par <c>GetDIBits</c>, qui rend les 32 bits tels qu'ils sont stockes.
    /// </remarks>
    private static unsafe byte[]? ReadIconPixels(IntPtr icon, int size)
    {
        if (!NativeMethods.GetIconInfo(icon, out NativeMethods.IconInfo iconInfo))
        {
            return null;
        }

        IntPtr screen = NativeMethods.GetDC(IntPtr.Zero);

        try
        {
            if (iconInfo.ColorBitmap == IntPtr.Zero || screen == IntPtr.Zero)
            {
                return null;
            }

            if (NativeMethods.GetObject(
                    iconInfo.ColorBitmap,
                    Marshal.SizeOf<NativeMethods.BitmapInfoStruct>(),
                    out NativeMethods.BitmapInfoStruct bitmap) == 0)
            {
                return null;
            }

            int width = bitmap.Width;
            int height = Math.Abs(bitmap.Height);

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            byte[] pixels = new byte[width * height * 4];

            var header = new NativeMethods.BitmapInfoHeader
            {
                Size = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
                Width = width,
                Height = -height, // negatif : balayage de haut en bas
                Planes = 1,
                BitCount = 32,
                Compression = 0, // BI_RGB
            };

            fixed (byte* buffer = pixels)
            {
                if (NativeMethods.GetDIBits(
                        screen,
                        iconInfo.ColorBitmap,
                        0,
                        (uint)height,
                        buffer,
                        ref header,
                        NativeMethods.DibRgbColors) == 0)
                {
                    return null;
                }
            }

            ApplyMaskIfOpaque(pixels, iconInfo.MaskBitmap, screen, width, height);

            return width == size && height == size
                ? pixels
                : Resample(pixels, width, height, size);
        }
        finally
        {
            if (screen != IntPtr.Zero)
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, screen);
            }

            if (iconInfo.ColorBitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(iconInfo.ColorBitmap);
            }

            if (iconInfo.MaskBitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(iconInfo.MaskBitmap);
            }
        }
    }

    /// <summary>
    /// Reconstruit le canal alpha a partir du masque quand l'icone n'en porte pas.
    /// </summary>
    /// <param name="pixels">Tampon BGRA a corriger.</param>
    /// <param name="mask">Bitmap de masque de l'icone.</param>
    /// <param name="screen">Contexte de peripherique de reference.</param>
    /// <param name="width">Largeur en pixels.</param>
    /// <param name="height">Hauteur en pixels.</param>
    /// <remarks>
    /// Les icones anciennes, en 24 bits, ont un canal alpha entierement nul : sans cette
    /// correction le gabarit serait totalement transparent.
    /// </remarks>
    private static unsafe void ApplyMaskIfOpaque(
        byte[] pixels, IntPtr mask, IntPtr screen, int width, int height)
    {
        bool hasAlpha = false;
        for (int i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
            {
                hasAlpha = true;
                break;
            }
        }

        if (hasAlpha)
        {
            return;
        }

        if (mask == IntPtr.Zero || screen == IntPtr.Zero)
        {
            for (int i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }

            return;
        }

        int stride = ((width + 31) / 32) * 4;
        byte[] maskBits = new byte[stride * height];

        var header = new NativeMethods.BitmapInfoHeader
        {
            Size = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
            Width = width,
            Height = -height,
            Planes = 1,
            BitCount = 1,
            Compression = 0,
        };

        fixed (byte* buffer = maskBits)
        {
            // Le masque est en 1 bpp : GetDIBits attend une palette a la suite de l'en-tete, mais
            // il tolere son absence tant qu'on ne lit que les bits.
            NativeMethods.GetDIBits(screen, mask, 0, (uint)height, buffer, ref header, NativeMethods.DibRgbColors);
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Dans le masque AND, un bit a 1 signifie « transparent ».
                bool transparent = (maskBits[(y * stride) + (x >> 3)] & (0x80 >> (x & 7))) != 0;
                pixels[(((y * width) + x) * 4) + 3] = transparent ? (byte)0 : (byte)255;
            }
        }
    }

    /// <summary>
    /// Redimensionne un tampon BGRA par interpolation bilineaire.
    /// </summary>
    /// <param name="source">Tampon d'origine.</param>
    /// <param name="width">Largeur d'origine.</param>
    /// <param name="height">Hauteur d'origine.</param>
    /// <param name="size">Taille carree souhaitee.</param>
    /// <returns>Le tampon redimensionne.</returns>
    /// <remarks>
    /// Ce chemin ne sert que de filet de securite : <c>PrivateExtractIcons</c> rend normalement
    /// deja la taille demandee.
    /// </remarks>
    private static byte[] Resample(byte[] source, int width, int height, int size)
    {
        byte[] result = new byte[size * size * 4];

        for (int y = 0; y < size; y++)
        {
            int sourceY = Math.Min(height - 1, y * height / size);

            for (int x = 0; x < size; x++)
            {
                int sourceX = Math.Min(width - 1, x * width / size);
                int from = (((sourceY * width) + sourceX) * 4);
                int to = (((y * size) + x) * 4);

                result[to] = source[from];
                result[to + 1] = source[from + 1];
                result[to + 2] = source[from + 2];
                result[to + 3] = source[from + 3];
            }
        }

        return result;
    }
}
