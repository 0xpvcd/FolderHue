using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FolderHue.Core.Icons;

namespace FolderHue.App.Icons;

/// <summary>
/// Extracts the machine's native folder icon to serve as the coloring template.
/// </summary>
/// <remarks>
/// No Microsoft asset is redistributed: the extraction happens on the user's machine, from their
/// own shell. It is also what makes a colored icon look like Windows 10 on Windows 10 and like
/// Windows 11 on Windows 11, with no version-specific code.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class BaseIconExtractor
{
    /// <summary>
    /// Extracts the template at every resolution in <see cref="IconSizes.All"/>.
    /// </summary>
    /// <returns>
    /// One top-down BGRA buffer, alpha not premultiplied, per requested size.
    /// </returns>
    /// <exception cref="InvalidOperationException">The shell supplied no folder icon.</exception>
    internal static IReadOnlyDictionary<int, byte[]> Extract()
    {
        (string path, int index) = LocateFolderIcon();
        var frames = new Dictionary<int, byte[]>(IconSizes.All.Count);

        foreach (int size in IconSizes.All)
        {
            frames[size] = ExtractSize(path, index, size)
                ?? throw new InvalidOperationException(
                    $"Could not extract the {size} px folder icon from \"{path}\".");
        }

        return frames;
    }

    /// <summary>
    /// Determines the file and index of the shell's folder icon.
    /// </summary>
    /// <returns>The resource file path and the icon index.</returns>
    /// <remarks>
    /// We ask the shell rather than hard-coding "imageres.dll,-3": that location has already moved
    /// between Windows versions.
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
                $"SHGetStockIconInfo failed (HRESULT 0x{hr:X8}).");
        }

        string path = ReadPath(ref info);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("The shell reported no folder icon file.");
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
    /// Extracts one resolution, trying both index conventions.
    /// </summary>
    /// <param name="path">Resource file.</param>
    /// <param name="index">Index the shell reported.</param>
    /// <param name="size">Desired size, in pixels.</param>
    /// <returns>The BGRA buffer, or <see langword="null"/> on failure.</returns>
    /// <remarks>
    /// A negative index names a resource identifier, a positive one a position. The shell does not
    /// say which convention it used, so we try the index as given, then its negation.
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
    /// Reads an icon's BGRA pixels.
    /// </summary>
    /// <param name="icon">Icon handle.</param>
    /// <param name="size">Expected size, in pixels.</param>
    /// <returns>The buffer, or <see langword="null"/> when the read failed.</returns>
    /// <remarks>
    /// <c>Bitmap.FromHicon</c> would be shorter but loses the alpha channel on many icons, so we
    /// go through <c>GetDIBits</c>, which returns the 32 bits exactly as stored.
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
    /// Rebuilds the alpha channel from the mask when the icon carries none.
    /// </summary>
    /// <param name="pixels">BGRA buffer to fix up.</param>
    /// <param name="mask">The icon's mask bitmap.</param>
    /// <param name="screen">Reference device context.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <remarks>
    /// Older 24-bit icons have an all-zero alpha channel: without this correction the template
    /// would be entirely transparent.
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
            // The mask is 1 bpp: GetDIBits expects a palette after the header, but tolerates its
            // absence as long as we only read the bits.
            NativeMethods.GetDIBits(screen, mask, 0, (uint)height, buffer, ref header, NativeMethods.DibRgbColors);
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // In the AND mask, a set bit means "transparent".
                bool transparent = (maskBits[(y * stride) + (x >> 3)] & (0x80 >> (x & 7))) != 0;
                pixels[(((y * width) + x) * 4) + 3] = transparent ? (byte)0 : (byte)255;
            }
        }
    }

    /// <summary>
    /// Resizes a BGRA buffer with bilinear interpolation.
    /// </summary>
    /// <param name="source">Original buffer.</param>
    /// <param name="width">Original width.</param>
    /// <param name="height">Original height.</param>
    /// <param name="size">Desired square size.</param>
    /// <returns>The resized buffer.</returns>
    /// <remarks>
    /// This path is only a safety net: <c>PrivateExtractIcons</c> normally returns the requested
    /// size already.
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
