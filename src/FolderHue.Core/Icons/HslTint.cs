using FolderHue.Core.Palette;

namespace FolderHue.Core.Icons;

/// <summary>
/// Applique une teinte de la palette a un bitmap 32 bits, en espace HSL.
/// </summary>
/// <remarks>
/// Code volontairement pur : aucune dependance graphique, donc compatible NativeAOT et testable
/// sans Windows (CLAUDE.md §2.1). Le decodage et l'encodage des images sont a la charge de
/// <c>FolderHue.App</c>.
/// </remarks>
public static class HslTint
{
    /// <summary>
    /// Teinte le tampon en place.
    /// </summary>
    /// <param name="bgra">
    /// Pixels au format BGRA 8 bits par canal, alpha <b>non premultiplie</b>. La longueur doit
    /// etre un multiple de 4.
    /// </param>
    /// <param name="color">La teinte a appliquer.</param>
    /// <remarks>
    /// Pour chaque pixel : l'alpha est conserve tel quel, la luminance est conservee (au decalage
    /// <see cref="FolderColor.LightnessDelta"/> pres) et seule la teinte est remplacee. C'est ce
    /// qui preserve l'ombrage et le relief du gabarit ; une simple multiplication RVB donnerait un
    /// resultat plat (CLAUDE.md §4.3).
    /// <para>
    /// Les pixels entierement transparents sont ignores : leurs composantes de couleur n'ont pas
    /// de sens et les teinter ferait apparaitre un halo lors du redimensionnement.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">La longueur de <paramref name="bgra"/> n'est pas un multiple de 4.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="color"/> vaut <see langword="null"/>.</exception>
    public static void Apply(Span<byte> bgra, FolderColor color)
    {
        ArgumentNullException.ThrowIfNull(color);

        if (bgra.Length % 4 != 0)
        {
            throw new ArgumentException("Le tampon BGRA doit avoir une longueur multiple de 4.", nameof(bgra));
        }

        if (color.IsNeutral)
        {
            // « Aucune teinte » : le gabarit est rendu tel quel. C'est ce qui permet de poser un
            // embleme sur un dossier sans lui imposer une couleur au passage.
            return;
        }

        float hue = HslColor.Normalize(color.Hue);

        for (int i = 0; i < bgra.Length; i += 4)
        {
            if (bgra[i + 3] == 0)
            {
                continue;
            }

            HslColor source = HslColor.FromRgb(bgra[i + 2], bgra[i + 1], bgra[i]);

            float lightness = Math.Clamp(source.L + color.LightnessDelta, 0f, 1f);
            float saturation = Math.Clamp(source.S * color.SaturationScale, 0f, 1f);
            float floor = color.SaturationFloor * MidtoneWeight(lightness);

            if (saturation < floor)
            {
                saturation = floor;
            }

            (byte r, byte g, byte b) = new HslColor(hue, saturation, lightness).ToRgb();

            bgra[i] = b;
            bgra[i + 1] = g;
            bgra[i + 2] = r;

            // bgra[i + 3] : alpha inchange, volontairement.
        }
    }

    /// <summary>
    /// Poids d'un pixel dans les tons moyens : 1 a mi-luminance, 0 au noir et au blanc purs.
    /// </summary>
    /// <param name="lightness">Luminance du pixel, dans [0, 1].</param>
    /// <returns>Le poids, dans [0, 1].</returns>
    /// <remarks>
    /// Le plancher de saturation n'est applique qu'aux tons moyens. Sans cette ponderation, les
    /// hautes lumieres du dossier viendraient se colorer et l'icone perdrait son relief.
    /// </remarks>
    public static float MidtoneWeight(float lightness)
    {
        float l = Math.Clamp(lightness, 0f, 1f);
        return Math.Clamp(4f * l * (1f - l), 0f, 1f);
    }
}
