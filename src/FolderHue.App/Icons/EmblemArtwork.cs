using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using FolderHue.Core.Palette;

namespace FolderHue.App.Icons;

/// <summary>
/// Dessine les emblemes en vectoriel, a la volee.
/// </summary>
/// <remarks>
/// Les emblemes sont dessines plutot que charges depuis des PNG : rien a licencier, rien a
/// versionner, et un rendu net a toutes les resolutions de <c>IconSizes.All</c>.
/// <para>
/// Ils sont compositees dans le <c>.ico</c>, jamais poses en overlay via
/// <c>IShellIconOverlayIdentifier</c> : Windows ne charge qu'une quinzaine d'overlays au total et
/// OneDrive, Dropbox ou Git en consomment deja la plupart (CLAUDE.md §2).
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class EmblemArtwork
{
    /// <summary>Part de la largeur de l'icone occupee par la pastille.</summary>
    private const float BadgeRatio = 0.40f;

    /// <summary>
    /// En dessous de ce diametre de pastille, le glyphe devient illisible et seule la couleur reste.
    /// </summary>
    /// <remarks>
    /// Le seuil porte sur la <b>pastille</b>, pas sur l'icone. Compositee sur un dossier la
    /// pastille ne fait que <see cref="BadgeRatio"/> de l'icone ; dessinee en puce de menu elle
    /// occupe presque tout. Un seuil exprime en taille d'icone aurait prive les puces de leur
    /// glyphe alors qu'elles ont largement la place.
    /// </remarks>
    private const float MinimumBadgeSize = 9f;

    /// <summary>Part de la puce laissee libre autour de la pastille.</summary>
    private const float ChipInsetRatio = 0.06f;

    /// <summary>Pastille de l'absence d'embleme, dessinee pour la puce « Aucun ».</summary>
    private static readonly Color EmptyBadgeColor = Color.FromArgb(255, 158, 165, 176);

    /// <summary>
    /// Composite un embleme en bas a droite d'une icone.
    /// </summary>
    /// <param name="graphics">Surface de dessin de l'icone.</param>
    /// <param name="glyph">Forme a dessiner.</param>
    /// <param name="iconSize">Taille de l'icone, en pixels.</param>
    internal static void Draw(Graphics graphics, EmblemGlyph glyph, int iconSize)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (glyph == EmblemGlyph.None)
        {
            return;
        }

        float badge = iconSize * BadgeRatio;
        float left = iconSize - badge - (iconSize * 0.02f);
        float top = iconSize - badge - (iconSize * 0.02f);

        DrawBadge(graphics, glyph, new RectangleF(left, top, badge, badge), BadgeColor(glyph));
    }

    /// <summary>
    /// Dessine la pastille d'un embleme en grand, pour servir de puce de menu.
    /// </summary>
    /// <param name="graphics">Surface de dessin de la puce.</param>
    /// <param name="glyph">Forme a dessiner. <see cref="EmblemGlyph.None"/> donne une pastille vide.</param>
    /// <param name="chipSize">Taille de la puce, en pixels.</param>
    /// <remarks>
    /// Contrairement a <see cref="Draw"/>, <see cref="EmblemGlyph.None"/> n'est pas ignore : la
    /// puce de l'entree « Aucun » est une pastille neutre, sans quoi cette entree serait la seule
    /// du menu sans icone.
    /// </remarks>
    internal static void DrawChip(Graphics graphics, EmblemGlyph glyph, int chipSize)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        float inset = chipSize * ChipInsetRatio;
        var bounds = new RectangleF(inset, inset, chipSize - (inset * 2f), chipSize - (inset * 2f));

        DrawBadge(graphics, glyph, bounds, glyph == EmblemGlyph.None ? EmptyBadgeColor : BadgeColor(glyph));
    }

    /// <summary>
    /// Dessine une pastille et son glyphe dans un rectangle donne.
    /// </summary>
    /// <param name="graphics">Surface de dessin.</param>
    /// <param name="glyph">Forme a dessiner.</param>
    /// <param name="bounds">Rectangle englobant la pastille.</param>
    /// <param name="fill">Couleur de remplissage de la pastille.</param>
    private static void DrawBadge(Graphics graphics, EmblemGlyph glyph, RectangleF bounds, Color fill)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;

        // Liseré de contraste : sans lui, une pastille rouge sur un dossier rouge disparait.
        float ring = Math.Max(1f, bounds.Width * 0.10f);
        using (var pen = new Pen(Color.White, ring))
        {
            pen.Alignment = PenAlignment.Center;
            using var outline = new SolidBrush(fill);
            graphics.FillEllipse(outline, bounds);
            graphics.DrawEllipse(pen, bounds);
        }

        if (bounds.Width < MinimumBadgeSize)
        {
            // Trop petit : la couleur de la pastille porte a elle seule l'information.
            return;
        }

        DrawGlyph(graphics, glyph, bounds);
    }

    private static Color BadgeColor(EmblemGlyph glyph) => glyph switch
    {
        EmblemGlyph.Exclamation => Color.FromArgb(255, 214, 45, 60),
        EmblemGlyph.Arrow => Color.FromArgb(255, 13, 110, 253),
        EmblemGlyph.Check => Color.FromArgb(255, 25, 135, 84),
        EmblemGlyph.Lock => Color.FromArgb(255, 90, 98, 110),
        EmblemGlyph.Star => Color.FromArgb(255, 240, 173, 12),
        _ => Color.Transparent,
    };

    private static void DrawGlyph(Graphics graphics, EmblemGlyph glyph, RectangleF bounds)
    {
        float size = bounds.Width;
        float cx = bounds.X + (size / 2f);
        float cy = bounds.Y + (size / 2f);

        using var brush = new SolidBrush(Color.White);
        using var pen = new Pen(Color.White, Math.Max(1f, size * 0.14f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        switch (glyph)
        {
            case EmblemGlyph.Exclamation:
                float barWidth = size * 0.14f;
                graphics.FillRectangle(
                    brush,
                    cx - (barWidth / 2f), cy - (size * 0.26f), barWidth, size * 0.32f);
                graphics.FillEllipse(
                    brush,
                    cx - (barWidth / 2f), cy + (size * 0.14f), barWidth, barWidth);
                break;

            case EmblemGlyph.Arrow:
                graphics.FillPolygon(brush,
                [
                    new PointF(cx - (size * 0.14f), cy - (size * 0.22f)),
                    new PointF(cx + (size * 0.22f), cy),
                    new PointF(cx - (size * 0.14f), cy + (size * 0.22f)),
                ]);
                break;

            case EmblemGlyph.Check:
                graphics.DrawLines(pen,
                [
                    new PointF(cx - (size * 0.22f), cy + (size * 0.02f)),
                    new PointF(cx - (size * 0.05f), cy + (size * 0.18f)),
                    new PointF(cx + (size * 0.23f), cy - (size * 0.19f)),
                ]);
                break;

            case EmblemGlyph.Lock:
                float bodyWidth = size * 0.42f;
                float bodyHeight = size * 0.30f;
                graphics.FillRectangle(
                    brush, cx - (bodyWidth / 2f), cy - (size * 0.02f), bodyWidth, bodyHeight);

                using (var shackle = new Pen(Color.White, Math.Max(1f, size * 0.10f)))
                {
                    float shackleSize = size * 0.28f;
                    graphics.DrawArc(
                        shackle,
                        cx - (shackleSize / 2f), cy - (size * 0.26f), shackleSize, shackleSize * 1.1f,
                        180f, 180f);
                }

                break;

            case EmblemGlyph.Star:
                graphics.FillPolygon(brush, StarPoints(cx, cy, size * 0.30f, size * 0.13f));
                break;

            case EmblemGlyph.None:
            default:
                break;
        }
    }

    private static PointF[] StarPoints(float cx, float cy, float outer, float inner)
    {
        var points = new PointF[10];

        for (int i = 0; i < 10; i++)
        {
            float radius = (i % 2 == 0) ? outer : inner;
            double angle = (Math.PI / 5f * i) - (Math.PI / 2f);
            points[i] = new PointF(
                cx + (float)(radius * Math.Cos(angle)),
                cy + (float)(radius * Math.Sin(angle)));
        }

        return points;
    }
}
