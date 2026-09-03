namespace FolderHue.Core.Palette;

/// <summary>
/// Une teinte predefinie de la palette.
/// </summary>
/// <remarks>
/// La colorisation n'est pas une multiplication RVB : on remplace la teinte en espace HSL et on
/// module la saturation, en conservant la luminance et le canal alpha du gabarit (CLAUDE.md §4.3).
/// Les valeurs ci-dessous decrivent donc une transformation, pas une couleur absolue.
/// </remarks>
/// <param name="Id">
/// Identifiant stable, en minuscules, sans espace. Il sert de cle dans le journal
/// <c>applied.json</c>, dans le nom du fichier <c>.ico</c> genere et dans les verbes du menu
/// contextuel : il ne doit jamais changer une fois publie.
/// </param>
/// <param name="ResourceKey">Cle de <c>Strings.resx</c> pour le libelle affiche a l'utilisateur.</param>
/// <param name="Hue">Teinte cible, en degres, dans l'intervalle [0, 360[.</param>
/// <param name="SaturationScale">
/// Facteur applique a la saturation d'origine de chaque pixel. 0 produit un resultat gris.
/// </param>
/// <param name="SaturationFloor">
/// Saturation minimale imposee aux pixels quasi neutres, ponderee par leur position dans les
/// tons moyens. Sans ce plancher, un gabarit gris resterait gris apres changement de teinte.
/// </param>
/// <param name="LightnessDelta">Decalage de luminance applique apres la teinte, dans [-1, 1].</param>
public sealed record FolderColor(
    string Id,
    string ResourceKey,
    float Hue,
    float SaturationScale,
    float SaturationFloor,
    float LightnessDelta)
{
    /// <summary>
    /// Teinte sentinelle designant l'absence de transformation.
    /// </summary>
    /// <remarks>
    /// Une teinte valide vit dans [0, 360[ ; une valeur negative n'en est donc pas une. Elle sert
    /// a exprimer « garde le gabarit tel quel », ce dont on a besoin pour poser un embleme sur un
    /// dossier que l'utilisateur n'a jamais colorise.
    /// </remarks>
    public const float NoHue = -1f;

    /// <summary>
    /// Indique que cette entree laisse le gabarit intact.
    /// </summary>
    /// <remarks>
    /// A ne pas confondre avec <c>graphite</c>, qui desature reellement l'icone : une entree
    /// neutre ne touche a rien du tout.
    /// </remarks>
    public bool IsNeutral => Hue < 0f;
}
