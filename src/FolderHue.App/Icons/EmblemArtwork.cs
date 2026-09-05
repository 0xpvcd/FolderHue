using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using FolderHue.Core.Palette;

namespace FolderHue.App.Icons;

/// <summary>
/// Draws the emblems as vector artwork, on the fly.
/// </summary>
/// <remarks>
/// Emblems are drawn rather than loaded from PNGs: nothing to license, nothing to version, and a
/// crisp result at every resolution in <c>IconSizes.All</c>.
/// <para>
/// They are composited into the <c>.ico</c>, never laid over it through
/// <c>IShellIconOverlayIdentifier</c>: Windows loads only about fifteen overlays in total, and
/// OneDrive, Dropbox or Git already take most of them (CLAUDE.md 2).
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class EmblemArtwork
{
    /// <summary>Share of the icon's width the badge occupies.</summary>
    private const float BadgeRatio = 0.40f;

    /// <summary>
    /// Below this badge diameter the glyph becomes illegible and only the color remains.
    /// </summary>
    /// <remarks>
    /// The threshold is on the <b>badge</b>, not the icon. Composited onto a folder the badge is
    /// only <see cref="BadgeRatio"/> of the icon; drawn as a menu chip it fills nearly all of it.
    /// A threshold expressed in icon size would have stripped the chips of their glyph even though
    /// they have plenty of room.
    /// </remarks>
    private const float MinimumBadgeSize = 9f;

    /// <summary>Share of the chip left clear around the badge.</summary>
    private const float ChipInsetRatio = 0.06f;

    /// <summary>Badge standing for no emblem, drawn for the "None" chip.</summary>
    private static readonly Color EmptyBadgeColor = Color.FromArgb(255, 158, 165, 176);

    /// <summary>
    /// Composites an emblem at the bottom right of an icon.
    /// </summary>
    /// <param name="graphics">The icon's drawing surface.</param>
    /// <param name="glyph">The shape to draw.</param>
    /// <param name="iconSize">Icon size, in pixels.</param>
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
    /// Draws an emblem's badge large, to serve as a menu chip.
    /// </summary>
    /// <param name="graphics">The chip's drawing surface.</param>
    /// <param name="glyph">The shape to draw. <see cref="EmblemGlyph.None"/> gives an empty badge.</param>
    /// <param name="chipSize">Chip size, in pixels.</param>
    /// <remarks>
    /// Unlike <see cref="Draw"/>, <see cref="EmblemGlyph.None"/> is not skipped: the "None" entry's
    /// chip is a neutral badge, without which that entry would be the only one in the menu with no
    /// icon.
    /// </remarks>
    internal static void DrawChip(Graphics graphics, EmblemGlyph glyph, int chipSize)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        float inset = chipSize * ChipInsetRatio;
        var bounds = new RectangleF(inset, inset, chipSize - (inset * 2f), chipSize - (inset * 2f));

        DrawBadge(graphics, glyph, bounds, glyph == EmblemGlyph.None ? EmptyBadgeColor : BadgeColor(glyph));
    }

    /// <summary>
    /// Draws a badge and its glyph inside a given rectangle.
    /// </summary>
    /// <param name="graphics">Drawing surface.</param>
    /// <param name="glyph">The shape to draw.</param>
    /// <param name="bounds">Rectangle bounding the badge.</param>
    /// <param name="fill">Fill color of the badge.</param>
    private static void DrawBadge(Graphics graphics, EmblemGlyph glyph, RectangleF bounds, Color fill)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;

        // Contrast outline: without it a red badge on a red folder disappears.
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
            // Too small: the badge color carries the information on its own.
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
