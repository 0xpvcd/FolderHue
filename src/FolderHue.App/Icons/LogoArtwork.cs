using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using FolderHue.Core.Icons;
using FolderHue.Core.Palette;

namespace FolderHue.App.Icons;

/// <summary>
/// Renders the application logo and its color tints.
/// </summary>
/// <remarks>
/// The logo is a 3D rendering embedded from <c>logo/</c>, no longer a vector transcription in
/// GDI+: the relief, the gloss and the folder's shadow cannot be retraced as paths. Both images are
/// embedded resources, decoded by <c>System.Drawing</c> - no NuGet dependency is added
/// (CLAUDE.md 11).
/// <list type="bullet">
/// <item><c>Logo_FoldersHue.png</c> - the brand logo, on its rainbow background.</item>
/// <item><c>Logo_Uni_FoldersHue.png</c> - the same folder on a flat background, the sole source of
/// the color tints.</item>
/// </list>
/// <para>
/// Both files are opaque and delivered on a black background: the transparency of the rounded
/// corners is reconstructed here (see <see cref="TransparencyKeyLevel"/>). It is <b>not</b>
/// retraced by hand, because the two images do not share a corner radius - measured: about 280 px
/// on the brand logo and 200 px on the flat one, measured on the 1254 px originals; the sources
/// now ship at 512 px and the radii scale with them.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class LogoArtwork
{
    /// <summary>Embedded resource of the brand logo.</summary>
    private const string BrandResourceName = "FolderHue.App.Icons.Logo_FoldersHue.png";

    /// <summary>Embedded resource of the flat-background logo, source of the tints.</summary>
    private const string TintableResourceName = "FolderHue.App.Icons.Logo_Uni_FoldersHue.png";

    /// <summary>
    /// Level, on the strongest channel, below which a pixel counts as outside the logo.
    /// </summary>
    /// <remarks>
    /// Both PNGs ship without an alpha channel, the logo sitting on pure black. Measured on
    /// <c>Logo_Uni_FoldersHue.png</c>: the background is 0 or 1, and the darkest pixel inside the
    /// shape is 101. The threshold can therefore be coarse without risk - all it has to do is
    /// separate two populations a hundred levels apart.
    /// <para>
    /// Deriving the alpha from the image rather than retracing a rounded square follows each
    /// file's <b>real</b> silhouette, its <i>squircle</i> curve included, and survives either logo
    /// being replaced.
    /// </para>
    /// </remarks>
    private const int TransparencyKeyLevel = 24;

    /// How many passes push the edge color outwards.
    /// </summary>
    /// <remarks>
    /// A transparent pixel still holds a color, and downscaling mixes it with its opaque
    /// neighbours. Leaving the original black would ring the logo with a dark halo from the very
    /// first reduction. The edge color is therefore pushed out by a few pixels before shrinking;
    /// two passes suffice, since the first reduction already halves the side.
    /// </remarks>
    private const int EdgeExtensionPasses = 2;

    /// Saturation below which a pixel escapes the tint entirely.
    /// </summary>
    /// <remarks>
    /// Histogram of <c>Logo_Uni_FoldersHue.png</c>: the background is 63% of the pixels above
    /// S = 0.95, the ivory folder spreads between 0.25 and 0.50, and the band separating them is
    /// nearly empty. The ramp sits in that valley, which keeps the folder ivory in all twelve
    /// colors - a green folder on a green background would no longer read.
    /// </remarks>
    private const float GlyphSaturation = 0.60f;

    /// <summary>Saturation above which a pixel is fully tinted.</summary>
    /// <remarks>See <see cref="GlyphSaturation"/>.</remarks>
    private const float GroundSaturation = 0.90f;

    /// Target median lightness for a tint's background.
    /// </summary>
    /// <remarks>
    /// <c>HslTint</c> replaces the hue but <b>keeps the lightness</b>: without correction the chip
    /// would inherit the flat logo's and announce a darker color than the folder actually takes.
    /// <para>
    /// Measured on the template extracted from the shell, at 32 px, over its opaque pixels:
    /// saturation 1.00 and lightness p10 = 0.539 / p50 = 0.704 / p90 = 0.769. The flat logo's
    /// background is measured at load time (<see cref="MeasureGroundLightness"/>) and shifted to
    /// this median: the correction therefore recomputes itself when the logo is replaced, instead
    /// of being frozen in a constant that would quietly go wrong.
    /// </para>
    /// </remarks>
    private const float TargetGroundLightness = 0.704f;

    private static readonly LogoSource Brand = new(BrandResourceName);
    private static readonly LogoSource Tintable = new(TintableResourceName);

    /// <summary>Renders the logo in the brand colors.</summary>
    /// <param name="size">Side of the image, in pixels.</param>
    /// <returns>A bitmap the caller takes ownership of.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is not positive.</exception>
    internal static Bitmap Render(int size) => Render(size, null);

    /// <summary>
    /// Renders the logo, optionally tinted with one palette color.
    /// </summary>
    /// <param name="size">Side of the image, in pixels.</param>
    /// <param name="color">The hue to apply, or <see langword="null"/> for the brand colors.</param>
    /// <returns>A bitmap the caller takes ownership of.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is not positive.</exception>
    internal static Bitmap Render(int size, FolderColor? color)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        if (color is null)
        {
            return BitmapBuffer.FromBgra(Brand.Scaled(size), size);
        }

        // Copy: Scaled returns the cache's buffer, and what follows writes into it.
        byte[] pixels = (byte[])Tintable.Scaled(size).Clone();

        if (color.IsNeutral)
        {
            // "No hue": same convention as HslTint. The original color's chip is neutral.ico
            // anyway, the real folder icon (CLAUDE.md 4.3).
            return BitmapBuffer.FromBgra(pixels, size);
        }

        byte[] tinted = (byte[])pixels.Clone();

        // The flat logo's background is darker than the folder template: it is lifted into the
        // template's band before tinting, or the chip announces too deep a color.
        ShiftLightness(tinted, TargetGroundLightness - Tintable.GroundLightness);
        HslTint.Apply(tinted, color);

        // Only the background takes the tint: the folder stays the brand's ivory.
        BlendGround(pixels, tinted);

        return BitmapBuffer.FromBgra(pixels, size);
    }

    /// <summary>
    /// Writes a multi-resolution <c>.ico</c> of the logo.
    /// </summary>
    /// <param name="outputPath">Path of the file to produce.</param>
    /// <param name="color">The hue to apply, or <see langword="null"/> for the brand colors.</param>
    /// <exception cref="ArgumentException"><paramref name="outputPath"/> is empty.</exception>
    internal static void WriteIcon(string outputPath, FolderColor? color)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        var frames = new List<IcoFrame>(IconSizes.Logo.Count);

        foreach (int size in IconSizes.Logo)
        {
            using Bitmap bitmap = Render(size, color);
            byte[] pixels = BitmapBuffer.ToBgra(bitmap);
            frames.Add(new IcoFrame(size, size, DibFrameBuilder.Build(pixels, size, size), IsPng: false));
        }

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        IcoWriter.WriteFile(outputPath, frames);
    }

    /// <summary>
    /// Shifts every pixel's lightness, keeping hue and saturation.
    /// </summary>
    /// <param name="bgra">Non-premultiplied BGRA pixels, modified in place.</param>
    /// <param name="delta">Offset to apply, within [-1, 1].</param>
    private static void ShiftLightness(Span<byte> bgra, float delta)
    {
        if (Math.Abs(delta) < 0.001f)
        {
            return;
        }

        for (int i = 0; i < bgra.Length; i += 4)
        {
            if (bgra[i + 3] == 0)
            {
                continue;
            }

            HslColor source = HslColor.FromRgb(bgra[i + 2], bgra[i + 1], bgra[i]);
            float lightness = Math.Clamp(source.L + delta, 0f, 1f);
            (byte r, byte g, byte b) = new HslColor(source.H, source.S, lightness).ToRgb();

            bgra[i] = b;
            bgra[i + 1] = g;
            bgra[i + 2] = r;
        }
    }

    /// <summary>
    /// Applies the tinted version to the background pixels only.
    /// </summary>
    /// <param name="baseline">Original pixels, modified in place and carrying the result.</param>
    /// <param name="tinted">The same image, tinted and lifted in lightness.</param>
    /// <remarks>
    /// The weight comes from the <b>original</b> pixel's saturation: the background, highly
    /// saturated, swings fully into the tint; the ivory folder, barely saturated, is untouched; the
    /// band in between - the folder's antialiased outline and its highlight - blends between the
    /// two (see <see cref="GlyphSaturation"/>).
    /// </remarks>
    private static void BlendGround(Span<byte> baseline, ReadOnlySpan<byte> tinted)
    {
        for (int i = 0; i < baseline.Length; i += 4)
        {
            if (baseline[i + 3] == 0)
            {
                continue;
            }

            HslColor source = HslColor.FromRgb(baseline[i + 2], baseline[i + 1], baseline[i]);
            float weight = Smoothstep(GlyphSaturation, GroundSaturation, source.S);

            if (weight <= 0f)
            {
                continue;
            }

            baseline[i] = Mix(baseline[i], tinted[i], weight);
            baseline[i + 1] = Mix(baseline[i + 1], tinted[i + 1], weight);
            baseline[i + 2] = Mix(baseline[i + 2], tinted[i + 2], weight);
        }
    }

    /// <summary>Interpolates two components.</summary>
    /// <param name="from">Value at weight zero.</param>
    /// <param name="to">Value at weight one.</param>
    /// <param name="weight">The weight, within [0, 1].</param>
    /// <returns>The interpolated component.</returns>
    private static byte Mix(byte from, byte to, float weight)
        => (byte)Math.Clamp((from * (1f - weight)) + (to * weight) + 0.5f, 0f, 255f);

    /// <summary>Smooth transition between two edges.</summary>
    /// <param name="edge0">Lower edge: below it the result is 0.</param>
    /// <param name="edge1">Upper edge: above it the result is 1.</param>
    /// <param name="value">The value to place.</param>
    /// <returns>The weight, within [0, 1].</returns>
    private static float Smoothstep(float edge0, float edge1, float value)
    {
        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    /// <summary>
    /// Median lightness of the background pixels.
    /// </summary>
    /// <param name="bgra">Non-premultiplied BGRA pixels.</param>
    /// <returns>The median, within [0, 1], or <see cref="TargetGroundLightness"/> when there is no background.</returns>
    /// <remarks>
    /// Keeps only the strongly saturated pixels, that is the flat background excluding the folder
    /// and its outline. See <see cref="TargetGroundLightness"/> for the use.
    /// </remarks>
    private static float MeasureGroundLightness(ReadOnlySpan<byte> bgra)
    {
        var samples = new List<float>(bgra.Length / 8);

        for (int i = 0; i < bgra.Length; i += 4)
        {
            if (bgra[i + 3] == 0)
            {
                continue;
            }

            HslColor color = HslColor.FromRgb(bgra[i + 2], bgra[i + 1], bgra[i]);

            if (color.S >= GroundSaturation)
            {
                samples.Add(color.L);
            }
        }

        if (samples.Count == 0)
        {
            return TargetGroundLightness;
        }

        samples.Sort();

        return samples[samples.Count / 2];
    }

    /// <summary>
    /// Replaces the PNGs' black background with transparency.
    /// </summary>
    /// <param name="bgra">BGRA pixels, modified in place.</param>
    private static void KeyOutBackground(Span<byte> bgra)
    {
        for (int i = 0; i < bgra.Length; i += 4)
        {
            int strongest = Math.Max(bgra[i], Math.Max(bgra[i + 1], bgra[i + 2]));
            bgra[i + 3] = strongest <= TransparencyKeyLevel ? (byte)0 : (byte)255;
        }
    }

    /// <summary>
    /// Pushes the edge color out into the neighbouring transparent pixels.
    /// </summary>
    /// <param name="bgra">BGRA pixels, modified in place. Alpha is left untouched.</param>
    /// <param name="width">Image width, in pixels.</param>
    /// <param name="height">Image height, in pixels.</param>
    /// <remarks>See <see cref="EdgeExtensionPasses"/> for why this pass exists.</remarks>
    private static void ExtendEdges(byte[] bgra, int width, int height)
    {
        bool[] filled = new bool[width * height];

        for (int i = 0; i < filled.Length; i++)
        {
            filled[i] = bgra[(i * 4) + 3] != 0;
        }

        for (int pass = 0; pass < EdgeExtensionPasses; pass++)
        {
            var added = new List<int>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;

                    if (filled[index])
                    {
                        continue;
                    }

                    int sumB = 0, sumG = 0, sumR = 0, count = 0;

                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + (k == 0 ? -1 : k == 1 ? 1 : 0);
                        int ny = y + (k == 2 ? -1 : k == 3 ? 1 : 0);

                        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            continue;
                        }

                        int neighbour = (ny * width) + nx;

                        if (!filled[neighbour])
                        {
                            continue;
                        }

                        sumB += bgra[neighbour * 4];
                        sumG += bgra[(neighbour * 4) + 1];
                        sumR += bgra[(neighbour * 4) + 2];
                        count++;
                    }

                    if (count == 0)
                    {
                        continue;
                    }

                    bgra[index * 4] = (byte)(sumB / count);
                    bgra[(index * 4) + 1] = (byte)(sumG / count);
                    bgra[(index * 4) + 2] = (byte)(sumR / count);
                    added.Add(index);
                }
            }

            foreach (int index in added)
            {
                filled[index] = true;
            }
        }
    }

    /// <summary>
    /// Shrinks an image by halving its side while the target is far away.
    /// </summary>
    /// <param name="source">The starting image.</param>
    /// <param name="size">Target side, in pixels.</param>
    /// <returns>A bitmap the caller takes ownership of.</returns>
    /// <remarks>
    /// Going from 512 px to 16 px in a single bicubic interpolation makes fine detail shimmer -
    /// the folder's outline and its highlight. Halving repeatedly averages every source pixel
    /// instead, exactly as the supersampling of the project's other thumbnails does
    /// (<see cref="Supersampler"/>).
    /// </remarks>
    private static Bitmap Downscale(Bitmap source, int size)
    {
        Bitmap current = source;
        bool owned = false;

        while (current.Width > size * 2)
        {
            Bitmap next = ScaleTo(current, Math.Max(size, current.Width / 2));

            if (owned)
            {
                current.Dispose();
            }

            current = next;
            owned = true;
        }

        Bitmap result = ScaleTo(current, size);

        if (owned)
        {
            current.Dispose();
        }

        return result;
    }

    /// <summary>Resizes a square image.</summary>
    /// <param name="source">The starting image.</param>
    /// <param name="size">Target side, in pixels.</param>
    /// <returns>A bitmap the caller takes ownership of.</returns>
    private static Bitmap ScaleTo(Bitmap source, int size)
    {
        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        using (Graphics graphics = Graphics.FromImage(result))
        using (var attributes = new ImageAttributes())
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.CompositingMode = CompositingMode.SourceCopy;

            // Without this setting GDI+ samples past the image and rings the result with a
            // one-pixel translucent line, visible as soon as the thumbnail is small.
            attributes.SetWrapMode(WrapMode.TileFlipXY);

            graphics.DrawImage(
                source,
                new Rectangle(0, 0, size, size),
                0, 0, source.Width, source.Height,
                GraphicsUnit.Pixel,
                attributes);
        }

        return result;
    }

    /// <summary>
    /// One of the two logo images: its original pixels and the reductions already computed.
    /// </summary>
    /// <remarks>
    /// Pre-generation asks for seven sizes across thirteen icons: without this cache the same
    /// reduction from the source would be redone eighty-four times.
    /// </remarks>
    private sealed class LogoSource(string resourceName)
    {
        private readonly object _gate = new();
        private readonly Dictionary<int, byte[]> _scaled = new();
        private byte[]? _pixels;
        private int _side;
        private float _groundLightness;

        /// <summary>Median lightness of the original image's background.</summary>
        internal float GroundLightness
        {
            get
            {
                Load();
                return _groundLightness;
            }
        }

        /// <summary>Renders the image at a given size.</summary>
        /// <param name="size">Target side, in pixels.</param>
        /// <returns>
        /// A BGRA buffer of <c>size * size * 4</c> bytes. It belongs to the cache: the caller must
        /// copy it before modifying it.
        /// </returns>
        internal byte[] Scaled(int size)
        {
            lock (_gate)
            {
                if (_scaled.TryGetValue(size, out byte[]? cached))
                {
                    return cached;
                }
            }

            Load();

            byte[] pixels;

            using (Bitmap full = BitmapBuffer.FromBgra(_pixels!, _side))
            using (Bitmap small = Downscale(full, size))
            {
                pixels = BitmapBuffer.ToBgra(small);
            }

            lock (_gate)
            {
                _scaled[size] = pixels;
            }

            return pixels;
        }

        /// <summary>Decodes the embedded resource, keys out its background and measures it.</summary>
        /// <exception cref="InvalidOperationException">The resource is missing from the assembly.</exception>
        private void Load()
        {
            lock (_gate)
            {
                if (_pixels is not null)
                {
                    return;
                }

                using Stream stream = typeof(LogoArtwork).Assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Logo resource not found: {resourceName}.");

                using var bitmap = new Bitmap(stream);

                if (bitmap.Width != bitmap.Height)
                {
                    throw new InvalidOperationException(
                        $"The logo {resourceName} must be square; it measures {bitmap.Width}x{bitmap.Height}.");
                }

                byte[] pixels = BitmapBuffer.ToBgra(bitmap);

                KeyOutBackground(pixels);
                ExtendEdges(pixels, bitmap.Width, bitmap.Height);

                _side = bitmap.Width;
                _groundLightness = MeasureGroundLightness(pixels);
                _pixels = pixels;
            }
        }
    }
}
