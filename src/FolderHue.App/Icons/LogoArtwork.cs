using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using FolderHue.Core.Icons;
using FolderHue.Core.Palette;

namespace FolderHue.App.Icons;

/// <summary>
/// Dessine le logo de l'application et ses declinaisons de couleur.
/// </summary>
/// <remarks>
/// Transcription vectorielle de <c>logo/FolderHue_logo.svg</c> : carre a coins arrondis, degrade
/// oriente a 135 degres, glyphe de dossier plein centre. Le SVG n'est pas rasterise a l'execution
/// — cela demanderait un moteur SVG, donc une dependance NuGet a justifier (CLAUDE.md §11) — il
/// est redessine en GDI+, exactement comme les emblemes.
/// <para>
/// L'ombre portee du SVG n'est volontairement pas reprise : une ombre cuite dans un <c>.ico</c> a
/// fond transparent devient une bavure grise des 16 px.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class LogoArtwork
{
    /// <summary>Rayon des coins du carre, en part du cote. <c>rx=143.36</c> sur 512 dans le SVG.</summary>
    private const float CornerRatio = 143.36f / 512f;

    /// <summary>Marge avant le glyphe, en part du cote. <c>translate(117.76)</c> dans le SVG.</summary>
    private const float GlyphOffsetRatio = 117.76f / 512f;

    /// <summary>Cote occupe par le glyphe, en part du cote. <c>24 x scale(11.52)</c> dans le SVG.</summary>
    private const float GlyphSpanRatio = 276.48f / 512f;

    /// <summary>Cote de la boite de dessin du glyphe, en unites SVG.</summary>
    private const float GlyphViewBox = 24f;

    /// <summary>
    /// Position des deux teintes le long de la diagonale du carre, dans [0, 1].
    /// </summary>
    /// <remarks>
    /// <c>gradientTransform="rotate(135 .5 .5)"</c> fait tourner l'axe horizontal par defaut de
    /// 135 degres autour du centre : le degrade court du haut a droite vers le bas a gauche, et
    /// ses deux extremites tombent a (.8536, .1464) et (.1464, .8536).
    /// <para>
    /// Cet axe est plus court que la diagonale du carre, si bien que les coins haut-droit et
    /// bas-gauche debordent. Le SVG les remplit d'une couleur pleine (<c>spreadMethod="pad"</c>,
    /// la valeur par defaut) ; GDI+, lui, <b>repete</b> le degrade et fait apparaitre deux bandes
    /// diagonales dans les coins. On trace donc le degrade sur toute la diagonale en doublant les
    /// arrets de couleur a ces deux positions, ce qui reproduit exactement le remplissage plein.
    /// </para>
    /// </remarks>
    private const float GradientStop = 0.1464f;

    // Degrade du SVG : #ff5b3d en haut a droite, #ff9f0a en bas a gauche.
    private static readonly Color GradientStart = Color.FromArgb(0xFF, 0x5B, 0x3D);
    private static readonly Color GradientEnd = Color.FromArgb(0xFF, 0x9F, 0x0A);

    /// <summary>
    /// Luminances du degrade de depart des declinaisons de couleur.
    /// </summary>
    /// <remarks>
    /// Une declinaison ne part pas du degrade de la marque tel quel. <c>HslTint</c> remplace la
    /// teinte mais <b>conserve la luminance</b> : la puce heritait donc de celle du logo, plus
    /// sombre que le gabarit de dossier extrait du shell, et annoncait une couleur plus soutenue
    /// que le resultat obtenu.
    /// <para>
    /// Mesure du gabarit a 32 px, sur ses pixels opaques : saturation 1,00 et luminance
    /// p10 = 0,54 / p50 = 0,70 / p90 = 0,77. Le degrade de depart est donc celui de la marque
    /// releve dans cette bande, saturation inchangee. La puce et le dossier subissent alors la
    /// meme transformation depuis le meme point de depart.
    /// </para>
    /// </remarks>
    private const float TintSourceHighlight = 0.76f;

    /// <summary>Luminance basse du degrade de depart. Voir <see cref="TintSourceHighlight"/>.</summary>
    private const float TintSourceShadow = 0.62f;

    /// <summary>Blanc casse du glyphe, <c>#fffaf3</c> dans le SVG.</summary>
    private static readonly Color GlyphColor = Color.FromArgb(0xFF, 0xFA, 0xF3);

    // Le degrade de la marque, releve dans la bande de luminance du gabarit de dossier.
    private static readonly Color TintSourceStart = WithLightness(GradientStart, TintSourceHighlight);
    private static readonly Color TintSourceEnd = WithLightness(GradientEnd, TintSourceShadow);

    /// <summary>Rend le logo aux couleurs de la marque.</summary>
    /// <param name="size">Cote de l'image, en pixels.</param>
    /// <returns>Un bitmap dont l'appelant devient proprietaire.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> n'est pas positif.</exception>
    internal static Bitmap Render(int size) => Render(size, null);

    /// <summary>
    /// Rend le logo, eventuellement decline dans une teinte de la palette.
    /// </summary>
    /// <param name="size">Cote de l'image, en pixels.</param>
    /// <param name="color">La teinte a appliquer, ou <see langword="null"/> pour la marque.</param>
    /// <returns>Un bitmap dont l'appelant devient proprietaire.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> n'est pas positif.</exception>
    internal static Bitmap Render(int size, FolderColor? color)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        if (color is null)
        {
            return Downscale(size, brand: true);
        }

        using Bitmap source = Downscale(size, brand: false);
        byte[] pixels = BitmapBuffer.ToBgra(source);

        // Meme transformation, meme luminance de depart que le gabarit de dossier : la puce du
        // menu annonce la couleur que le dossier prendra.
        HslTint.Apply(pixels, color);

        return BitmapBuffer.FromBgra(pixels, size);
    }

    /// <summary>
    /// Ecrit un <c>.ico</c> multi-resolution du logo.
    /// </summary>
    /// <param name="outputPath">Chemin du fichier a produire.</param>
    /// <param name="color">La teinte a appliquer, ou <see langword="null"/> pour la marque.</param>
    /// <exception cref="ArgumentException"><paramref name="outputPath"/> est vide.</exception>
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
    /// Dessine le logo, en surechantillonnant.
    /// </summary>
    /// <param name="size">Cote final, en pixels.</param>
    /// <param name="brand">
    /// <see langword="true"/> pour le degrade orange de la marque, <see langword="false"/> pour
    /// celui, eclairci, des declinaisons.
    /// </param>
    /// <returns>Un bitmap dont l'appelant devient proprietaire.</returns>
    private static Bitmap Downscale(int size, bool brand) => Supersampler.Render(size, (graphics, side) =>
    {
        using (GraphicsPath square = CreateRoundedSquare(side, side * CornerRatio))
        using (LinearGradientBrush brush = CreateBackgroundBrush(side, brand))
        {
            graphics.FillPath(brush, square);
        }

        using (GraphicsPath glyph = CreateFolderGlyph(side))
        using (var brush = new SolidBrush(GlyphColor))
        {
            graphics.FillPath(brush, glyph);
        }
    });

    /// <summary>
    /// Construit le degrade du fond, tendu sur la diagonale complete du carre.
    /// </summary>
    /// <param name="size">Cote du carre, en pixels.</param>
    /// <param name="brand">
    /// <see langword="true"/> pour le degrade de la marque, <see langword="false"/> pour celui,
    /// eclairci, que <c>HslTint</c> viendra teinter.
    /// </param>
    /// <returns>Le pinceau, dont l'appelant devient proprietaire.</returns>
    private static LinearGradientBrush CreateBackgroundBrush(float size, bool brand)
    {
        Color start = brand ? GradientStart : TintSourceStart;
        Color end = brand ? GradientEnd : TintSourceEnd;

        // Du coin haut-droit au coin bas-gauche : c'est la diagonale que suit rotate(135).
        var brush = new LinearGradientBrush(
            new PointF(size, 0f), new PointF(0f, size), start, end);

        brush.InterpolationColors = new ColorBlend(4)
        {
            Colors = [start, start, end, end],
            Positions = [0f, GradientStop, 1f - GradientStop, 1f],
        };

        return brush;
    }

    /// <summary>
    /// Rend une couleur a une luminance donnee, teinte et saturation conservees.
    /// </summary>
    /// <param name="source">La couleur de depart.</param>
    /// <param name="lightness">La luminance visee, dans [0, 1].</param>
    /// <returns>La couleur ajustee, opaque.</returns>
    private static Color WithLightness(Color source, float lightness)
    {
        HslColor hsl = HslColor.FromRgb(source.R, source.G, source.B);
        (byte r, byte g, byte b) = new HslColor(hsl.H, hsl.S, lightness).ToRgb();

        return Color.FromArgb(255, r, g, b);
    }

    /// <summary>Trace le carre a coins arrondis du fond.</summary>
    /// <param name="size">Cote du carre.</param>
    /// <param name="radius">Rayon des coins.</param>
    /// <returns>Le trace, dont l'appelant devient proprietaire.</returns>
    private static GraphicsPath CreateRoundedSquare(float size, float radius)
    {
        float diameter = radius * 2f;
        var path = new GraphicsPath();

        path.AddArc(0f, 0f, diameter, diameter, 180f, 90f);
        path.AddArc(size - diameter, 0f, diameter, diameter, 270f, 90f);
        path.AddArc(size - diameter, size - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(0f, size - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();

        return path;
    }

    /// <summary>
    /// Trace le glyphe de dossier plein, mis a l'echelle et positionne.
    /// </summary>
    /// <param name="size">Cote de l'icone, en pixels.</param>
    /// <returns>Le trace, dont l'appelant devient proprietaire.</returns>
    /// <remarks>
    /// Transcription du <c>path</c> du SVG, exprime dans une boite de 24 unites : corps a coins de
    /// rayon 3, onglet a gauche relie au corps par une diagonale a 45 degres.
    /// </remarks>
    private static GraphicsPath CreateFolderGlyph(float size)
    {
        var path = new GraphicsPath();

        path.AddArc(2f, 3f, 6f, 6f, 180f, 90f);        // coin haut-gauche : (2,6) -> (5,3)
        path.AddLine(5f, 3f, 8.7f, 3f);                // bord superieur de l'onglet
        path.AddArc(7.7f, 3f, 2f, 2f, 270f, 45f);      // epaulement de l'onglet
        path.AddLine(9.41f, 3.29f, 12.11f, 6f);        // diagonale vers le corps
        path.AddLine(12.11f, 6f, 19f, 6f);             // bord superieur du corps
        path.AddArc(16f, 6f, 6f, 6f, 270f, 90f);       // coin haut-droit
        path.AddArc(16f, 14f, 6f, 6f, 0f, 90f);        // coin bas-droit
        path.AddArc(2f, 14f, 6f, 6f, 90f, 90f);        // coin bas-gauche
        path.CloseFigure();                            // bord gauche : (2,17) -> (2,6)

        float scale = size * GlyphSpanRatio / GlyphViewBox;

        using var transform = new Matrix();
        transform.Translate(size * GlyphOffsetRatio, size * GlyphOffsetRatio);
        transform.Scale(scale, scale);
        path.Transform(transform);

        return path;
    }
}
