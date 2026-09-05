using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using FolderHue.Core.Icons;
using FolderHue.Core.Palette;

namespace FolderHue.App.Icons;

/// <summary>
/// Produces a multi-resolution <c>.ico</c> for one color + emblem pair.
/// </summary>
/// <remarks>
/// Pipeline, per resolution: native template, HSL tint (pure code from <c>FolderHue.Core</c>),
/// emblem compositing in GDI+, then encoding - PNG at 256 px, DIB below that (CLAUDE.md 4.3).
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class IconRenderer
{
    private readonly IReadOnlyDictionary<int, byte[]> _baseFrames;

    /// <summary>Builds a renderer on top of an already-extracted template.</summary>
    /// <param name="baseFrames">The template's BGRA buffers, keyed by size.</param>
    /// <exception cref="ArgumentNullException"><paramref name="baseFrames"/> is <see langword="null"/>.</exception>
    internal IconRenderer(IReadOnlyDictionary<int, byte[]> baseFrames)
    {
        ArgumentNullException.ThrowIfNull(baseFrames);
        _baseFrames = baseFrames;
    }

    /// <summary>Generates the icon file for one pair.</summary>
    /// <param name="color">The hue to apply.</param>
    /// <param name="emblem">The emblem to composite, possibly <see cref="Emblem.None"/>.</param>
    /// <param name="outputPath">Path of the <c>.ico</c> to write.</param>
    internal void Render(FolderColor color, Emblem emblem, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(emblem);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        var frames = new List<IcoFrame>(IconSizes.All.Count);

        foreach (int size in IconSizes.All)
        {
            if (!_baseFrames.TryGetValue(size, out byte[]? template))
            {
                continue;
            }

            byte[] pixels = (byte[])template.Clone();
            HslTint.Apply(pixels, color);

            frames.Add(emblem.Glyph == EmblemGlyph.None && !IconSizes.UsePng(size)
                ? new IcoFrame(size, size, DibFrameBuilder.Build(pixels, size, size), IsPng: false)
                : Compose(pixels, size, emblem.Glyph));
        }

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        IcoWriter.WriteFile(outputPath, frames);
    }

    /// <summary>
    /// Renders a color's preview for the settings window.
    /// </summary>
    /// <param name="color">The hue to apply.</param>
    /// <param name="emblem">The emblem to composite.</param>
    /// <param name="size">Desired size, which must exist in the template.</param>
    /// <returns>A bitmap the caller takes ownership of, or <see langword="null"/>.</returns>
    internal Bitmap? RenderPreview(FolderColor color, Emblem emblem, int size)
    {
        if (!_baseFrames.TryGetValue(size, out byte[]? template))
        {
            return null;
        }

        byte[] pixels = (byte[])template.Clone();
        HslTint.Apply(pixels, color);

        Bitmap bitmap = BitmapBuffer.FromBgra(pixels, size);

        if (emblem.Glyph != EmblemGlyph.None)
        {
            using Graphics graphics = Graphics.FromImage(bitmap);
            EmblemArtwork.Draw(graphics, emblem.Glyph, size);
        }

        return bitmap;
    }

    /// <summary>
    /// Writes an emblem's menu chip: the badge on its own, drawn large.
    /// </summary>
    /// <param name="emblem">The emblem to represent, <see cref="Emblem.None"/> included.</param>
    /// <param name="outputPath">Path of the <c>.ico</c> to write.</param>
    /// <remarks>
    /// No template is needed: the badge is drawn from scratch. That is why this method is static,
    /// unlike the folder icon rendering.
    /// </remarks>
    internal static void WriteEmblemChip(Emblem emblem, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(emblem);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        var frames = new List<IcoFrame>(IconSizes.Logo.Count);

        foreach (int size in IconSizes.Logo)
        {
            using Bitmap bitmap = Supersampler.Render(
                size, (graphics, side) => EmblemArtwork.DrawChip(graphics, emblem.Glyph, side));

            frames.Add(new IcoFrame(
                size, size, DibFrameBuilder.Build(BitmapBuffer.ToBgra(bitmap), size, size), IsPng: false));
        }

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        IcoWriter.WriteFile(outputPath, frames);
    }

    private static IcoFrame Compose(byte[] pixels, int size, EmblemGlyph glyph)
    {
        using Bitmap bitmap = BitmapBuffer.FromBgra(pixels, size);

        if (glyph != EmblemGlyph.None)
        {
            using Graphics graphics = Graphics.FromImage(bitmap);
            EmblemArtwork.Draw(graphics, glyph, size);
        }

        if (IconSizes.UsePng(size))
        {
            // The 256 px frame must be PNG-encoded: that is what the shell expects for large
            // thumbnails.
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return new IcoFrame(size, size, stream.ToArray(), IsPng: true);
        }

        return new IcoFrame(size, size, DibFrameBuilder.Build(BitmapBuffer.ToBgra(bitmap), size, size), IsPng: false);
    }
}
