namespace FolderHue.Core.Palette;

/// <summary>
/// Source unique de verite de la palette : couleurs, emblemes et nommage des icones generees.
/// </summary>
/// <remarks>
/// Ce catalogue est partage par <c>FolderHue.Shell</c> (libelles et verbes du menu) et par
/// <c>FolderHue.App</c> (pre-generation des <c>.ico</c>). Il est volontairement statique et
/// sans acces disque : <c>GetTitle</c> et <c>GetIcon</c> sont appeles a chaque ouverture du menu
/// contextuel, dans le processus <c>explorer.exe</c> (CLAUDE.md §4.4).
/// <para>
/// Le menu comporte 12 couleurs + « Embleme » + « Reinitialiser » = 14 elements, sous la limite
/// de 16 elements par handler.
/// </para>
/// </remarks>
public static class PaletteCatalog
{
    private const float DefaultSaturationScale = 1.15f;
    private const float DefaultSaturationFloor = 0.55f;

    /// <summary>Les 12 teintes proposees dans le menu contextuel, dans l'ordre d'affichage.</summary>
    public static IReadOnlyList<FolderColor> Colors { get; } =
    [
        new("red",      "Color_Red",      358f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("orange",   "Color_Orange",    24f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("amber",    "Color_Amber",     42f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("yellow",   "Color_Yellow",    54f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("green",    "Color_Green",    120f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("emerald",  "Color_Emerald",  160f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("cyan",     "Color_Cyan",     190f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("blue",     "Color_Blue",     214f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("indigo",   "Color_Indigo",   245f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("violet",   "Color_Violet",   275f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("pink",     "Color_Pink",     330f, DefaultSaturationScale, DefaultSaturationFloor, 0f),
        new("graphite", "Color_Graphite",   0f, 0f,                     0f,                     -0.02f),
    ];

    /// <summary>
    /// Les etats d'embleme proposes, <see cref="Emblem.None"/> compris et place en tete.
    /// </summary>
    public static IReadOnlyList<Emblem> Emblems { get; } =
    [
        Emblem.None,
        new("important", "Emblem_Important", EmblemGlyph.Exclamation),
        new("progress",  "Emblem_Progress",  EmblemGlyph.Arrow),
        new("done",      "Emblem_Done",      EmblemGlyph.Check),
        new("locked",    "Emblem_Locked",    EmblemGlyph.Lock),
        new("favorite",  "Emblem_Favorite",  EmblemGlyph.Star),
    ];

    /// <summary>
    /// Entree hors palette designant « la couleur d'origine du dossier ».
    /// </summary>
    /// <remarks>
    /// Elle n'apparait pas dans le menu : c'est la couleur de repli quand l'utilisateur pose un
    /// embleme sur un dossier jamais colorise. Sans elle, le code retombait sur la premiere teinte
    /// de la palette et le dossier virait au rouge au passage — poser un marqueur ne doit pas
    /// decider d'une couleur a la place de l'utilisateur.
    /// </remarks>
    public static FolderColor Neutral { get; } =
        new("neutral", "Color_Neutral", FolderColor.NoHue, 1f, 0f, 0f);

    /// <summary>
    /// Toutes les entrees pour lesquelles une icone doit etre pre-generee.
    /// </summary>
    /// <remarks>
    /// La palette du menu, plus <see cref="Neutral"/> : <c>neutral.ico</c> est l'icone de dossier
    /// d'origine, et <c>neutral+done.ico</c> cette meme icone portant un embleme.
    /// </remarks>
    public static IReadOnlyList<FolderColor> TintableColors { get; } = [.. Colors, Neutral];

    /// <summary>Nombre d'icones distinctes que la pre-generation doit produire.</summary>
    public static int IconCombinationCount => TintableColors.Count * Emblems.Count;

    /// <summary>Retourne la couleur portant cet identifiant, ou <see langword="null"/> si inconnue.</summary>
    /// <param name="id">Identifiant stable de la couleur, comparaison insensible a la casse.</param>
    public static FolderColor? FindColor(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (FolderColor color in TintableColors)
        {
            if (string.Equals(color.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return color;
            }
        }

        return null;
    }

    /// <summary>Retourne l'embleme portant cet identifiant, ou <see langword="null"/> si inconnu.</summary>
    /// <param name="id">
    /// Identifiant stable de l'embleme. <see langword="null"/> ou vide est traite comme
    /// <see cref="Emblem.None"/>, ce qui simplifie l'appel depuis le shell.
    /// </param>
    public static Emblem? FindEmblem(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Emblem.None;
        }

        foreach (Emblem emblem in Emblems)
        {
            if (string.Equals(emblem.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return emblem;
            }
        }

        return null;
    }

    /// <summary>
    /// Nom du fichier <c>.ico</c> de la puce d'un embleme.
    /// </summary>
    /// <param name="emblemId">Identifiant de l'embleme, <see cref="Emblem.NoneId"/> compris.</param>
    /// <returns>Un nom de fichier, sans chemin, par exemple <c>emblem-done.ico</c>.</returns>
    /// <remarks>
    /// La pastille seule, dessinee en grand. Compositee sur un dossier elle ne fait que 40 % de
    /// l'icone, soit six pixels dans un menu : illisible. La puce du menu la dessine donc pleine
    /// taille.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="emblemId"/> est vide.</exception>
    public static string EmblemChipFileName(string emblemId)
    {
        ArgumentException.ThrowIfNullOrEmpty(emblemId);
        return string.Concat("emblem-", emblemId.ToLowerInvariant(), ".ico");
    }

    /// <summary>
    /// Nom du fichier <c>.ico</c> du logo de l'application, aux couleurs de la marque.
    /// </summary>
    /// <remarks>
    /// C'est l'icone de l'entree racine du menu contextuel. Elle vit a cote de la palette, dans
    /// <c>%LOCALAPPDATA%\FolderHue\icons</c>, et est pre-generee au meme moment.
    /// </remarks>
    public const string BrandLogoFileName = "logo.ico";

    /// <summary>
    /// Nom du fichier <c>.ico</c> de la declinaison du logo dans une teinte de la palette.
    /// </summary>
    /// <param name="colorId">Identifiant de la couleur, par exemple <c>"blue"</c>.</param>
    /// <returns>Un nom de fichier, sans chemin, par exemple <c>logo-blue.ico</c>.</returns>
    /// <remarks>
    /// Ces declinaisons servent de puces devant chaque couleur du menu contextuel. Elles
    /// subissent exactement la meme transformation HSL que l'icone de dossier correspondante :
    /// la puce et le resultat sur le dossier ne peuvent donc pas diverger.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="colorId"/> est vide.</exception>
    public static string LogoFileName(string colorId)
    {
        ArgumentException.ThrowIfNullOrEmpty(colorId);
        return string.Concat("logo-", colorId.ToLowerInvariant(), ".ico");
    }

    /// <summary>
    /// Nom du fichier <c>.ico</c> correspondant a une combinaison couleur + embleme.
    /// </summary>
    /// <param name="colorId">Identifiant de la couleur, par exemple <c>"blue"</c>.</param>
    /// <param name="emblemId">
    /// Identifiant de l'embleme. <c>"none"</c>, <see langword="null"/> ou vide produisent
    /// <c>blue.ico</c> ; sinon <c>blue+important.ico</c>.
    /// </param>
    /// <returns>Un nom de fichier, sans chemin.</returns>
    public static string IconFileName(string colorId, string? emblemId)
    {
        ArgumentException.ThrowIfNullOrEmpty(colorId);

        bool hasEmblem = !string.IsNullOrEmpty(emblemId)
            && !string.Equals(emblemId, Emblem.NoneId, StringComparison.OrdinalIgnoreCase);

        return hasEmblem
            ? string.Concat(colorId.ToLowerInvariant(), "+", emblemId!.ToLowerInvariant(), ".ico")
            : string.Concat(colorId.ToLowerInvariant(), ".ico");
    }
}
