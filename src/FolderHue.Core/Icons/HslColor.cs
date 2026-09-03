namespace FolderHue.Core.Icons;

/// <summary>
/// Une couleur en espace Teinte / Saturation / Luminance.
/// </summary>
/// <remarks>
/// C'est l'espace de travail de la colorisation : il permet de remplacer la teinte d'un pixel sans
/// toucher a sa luminance, donc en conservant l'ombrage et le relief du gabarit (CLAUDE.md §4.3).
/// </remarks>
/// <param name="H">Teinte en degres, dans [0, 360[.</param>
/// <param name="S">Saturation, dans [0, 1].</param>
/// <param name="L">Luminance, dans [0, 1].</param>
public readonly record struct HslColor(float H, float S, float L)
{
    /// <summary>Convertit une couleur RVB 8 bits par canal en HSL.</summary>
    /// <param name="r">Composante rouge.</param>
    /// <param name="g">Composante verte.</param>
    /// <param name="b">Composante bleue.</param>
    /// <returns>La couleur equivalente en HSL.</returns>
    public static HslColor FromRgb(byte r, byte g, byte b)
    {
        float rf = r / 255f;
        float gf = g / 255f;
        float bf = b / 255f;

        float max = MathF.Max(rf, MathF.Max(gf, bf));
        float min = MathF.Min(rf, MathF.Min(gf, bf));
        float l = (max + min) / 2f;

        if (max - min < float.Epsilon)
        {
            // Pixel neutre : la teinte n'a pas de sens, on la fixe a 0.
            return new HslColor(0f, 0f, l);
        }

        float d = max - min;
        float s = l > 0.5f ? d / (2f - max - min) : d / (max + min);

        float h;
        if (max == rf)
        {
            h = (gf - bf) / d + (gf < bf ? 6f : 0f);
        }
        else if (max == gf)
        {
            h = ((bf - rf) / d) + 2f;
        }
        else
        {
            h = ((rf - gf) / d) + 4f;
        }

        return new HslColor(h * 60f, s, l);
    }

    /// <summary>Convertit cette couleur HSL en RVB 8 bits par canal.</summary>
    /// <returns>Les trois composantes, arrondies au plus proche.</returns>
    public (byte R, byte G, byte B) ToRgb()
    {
        float s = Math.Clamp(S, 0f, 1f);
        float l = Math.Clamp(L, 0f, 1f);

        if (s < float.Epsilon)
        {
            byte gray = ToByte(l);
            return (gray, gray, gray);
        }

        float q = l < 0.5f ? l * (1f + s) : l + s - (l * s);
        float p = (2f * l) - q;
        float h = Normalize(H) / 360f;

        return (ToByte(HueToChannel(p, q, h + (1f / 3f))),
                ToByte(HueToChannel(p, q, h)),
                ToByte(HueToChannel(p, q, h - (1f / 3f))));
    }

    /// <summary>Ramene une teinte quelconque dans l'intervalle [0, 360[.</summary>
    /// <param name="hue">Teinte en degres, eventuellement negative ou superieure a 360.</param>
    /// <returns>La teinte equivalente dans [0, 360[.</returns>
    public static float Normalize(float hue)
    {
        float h = hue % 360f;
        return h < 0f ? h + 360f : h;
    }

    private static float HueToChannel(float p, float q, float t)
    {
        if (t < 0f)
        {
            t += 1f;
        }

        if (t > 1f)
        {
            t -= 1f;
        }

        if (t < 1f / 6f)
        {
            return p + ((q - p) * 6f * t);
        }

        if (t < 1f / 2f)
        {
            return q;
        }

        if (t < 2f / 3f)
        {
            return p + ((q - p) * ((2f / 3f) - t) * 6f);
        }

        return p;
    }

    private static byte ToByte(float value) => (byte)Math.Clamp(MathF.Round(value * 255f), 0f, 255f);
}
