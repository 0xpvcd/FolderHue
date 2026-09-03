using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using FolderHue.Core.Icons;
using FolderHue.Core.Palette;

namespace FolderHue.App.Icons;

/// <summary>
/// Produit un <c>.ico</c> multi-resolution pour une combinaison couleur + embleme.
/// </summary>
/// <remarks>
/// Chaine de traitement, par resolution : gabarit natif, teinte HSL (code pur de
/// <c>FolderHue.Core</c>), compositing de l'embleme en GDI+, puis encodage — PNG a 256 px,
/// DIB en dessous (CLAUDE.md §4.3).
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class IconRenderer
{
    private readonly IReadOnlyDictionary<int, byte[]> _baseFrames;

    /// <summary>Construit un moteur de rendu adosse a un gabarit deja extrait.</summary>
    /// <param name="baseFrames">Tampons BGRA du gabarit, indexes par taille.</param>
    /// <exception cref="ArgumentNullException"><paramref name="baseFrames"/> vaut <see langword="null"/>.</exception>
    internal IconRenderer(IReadOnlyDictionary<int, byte[]> baseFrames)
    {
        ArgumentNullException.ThrowIfNull(baseFrames);
        _baseFrames = baseFrames;
    }

    /// <summary>Genere le fichier d'icone d'une combinaison.</summary>
    /// <param name="color">La teinte a appliquer.</param>
    /// <param name="emblem">L'embleme a compositer, eventuellement <see cref="Emblem.None"/>.</param>
    /// <param name="outputPath">Chemin du <c>.ico</c> a ecrire.</param>
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
    /// Rend le gabarit d'apercu d'une couleur, pour l'interface de reglages.
    /// </summary>
    /// <param name="color">La teinte a appliquer.</param>
    /// <param name="emblem">L'embleme a compositer.</param>
    /// <param name="size">Taille souhaitee, qui doit exister dans le gabarit.</param>
    /// <returns>Un bitmap dont l'appelant devient proprietaire, ou <see langword="null"/>.</returns>
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
    /// Ecrit la puce de menu d'un embleme : la pastille seule, en grand.
    /// </summary>
    /// <param name="emblem">L'embleme a representer, <see cref="Emblem.None"/> compris.</param>
    /// <param name="outputPath">Chemin du <c>.ico</c> a ecrire.</param>
    /// <remarks>
    /// Aucun gabarit n'est necessaire : la pastille est entierement dessinee. C'est pourquoi la
    /// methode est statique, contrairement au rendu des icones de dossier.
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
            // La trame 256 px doit etre encodee en PNG : c'est ce que le shell attend pour les
            // grandes vignettes.
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return new IcoFrame(size, size, stream.ToArray(), IsPng: true);
        }

        return new IcoFrame(size, size, DibFrameBuilder.Build(BitmapBuffer.ToBgra(bitmap), size, size), IsPng: false);
    }
}
