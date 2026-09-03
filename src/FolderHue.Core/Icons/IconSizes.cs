namespace FolderHue.Core.Icons;

/// <summary>
/// Les resolutions embarquees dans chaque <c>.ico</c> genere.
/// </summary>
public static class IconSizes
{
    /// <summary>
    /// Toutes les tailles produites, en pixels, par ordre croissant.
    /// </summary>
    /// <remarks>
    /// La liste couvre les vues de l'Explorateur, de « Details » (16 px) a « Tres grandes icones »
    /// (256 px), ainsi que les paliers intermediaires utilises par la mise a l'echelle DPI
    /// (CLAUDE.md §4.3).
    /// </remarks>
    public static IReadOnlyList<int> All { get; } = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    /// <summary>
    /// Les resolutions embarquees dans les <c>.ico</c> de logo.
    /// </summary>
    /// <remarks>
    /// Un logo de menu n'est jamais affiche au-dela d'une cinquantaine de pixels : le shell
    /// demande <c>SM_CXSMICON</c>, soit 16 px a 100 % et 32 px a 200 %. Inutile d'embarquer les
    /// grandes trames de <see cref="All"/>, qui ne serviraient qu'a alourdir le fichier.
    /// </remarks>
    public static IReadOnlyList<int> Logo { get; } = [16, 20, 24, 32, 40, 48, 64];

    /// <summary>Taille maximale representable dans un conteneur ICO.</summary>
    public const int MaxSize = 256;

    /// <summary>
    /// Indique si une taille doit etre encodee en PNG dans le conteneur ICO.
    /// </summary>
    /// <param name="size">La taille, en pixels.</param>
    /// <returns><see langword="true"/> pour 256 px, <see langword="false"/> sinon.</returns>
    /// <remarks>
    /// L'encodage PNG est obligatoire pour la trame 256 px (CLAUDE.md §4.3). Les petites tailles
    /// restent en DIB : c'est le format historiquement le mieux gere par toutes les vues du shell.
    /// </remarks>
    public static bool UsePng(int size) => size >= MaxSize;
}
