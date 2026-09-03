namespace FolderHue.Core.Palette;

/// <summary>
/// Un marqueur de statut composite sur l'icone du dossier (important, en cours, termine...).
/// </summary>
/// <remarks>
/// Les emblemes sont compositees dans le <c>.ico</c> genere, jamais via
/// <c>IShellIconOverlayIdentifier</c> : Windows ne charge qu'une quinzaine d'overlays au total et
/// OneDrive, Dropbox ou Git en consomment deja la majorite (CLAUDE.md §2).
/// </remarks>
/// <param name="Id">
/// Identifiant stable, en minuscules. <see cref="None"/> utilise <c>"none"</c>, qui signifie
/// « aucun embleme » et n'apparait pas dans le nom du fichier d'icone.
/// </param>
/// <param name="ResourceKey">Cle de <c>Strings.resx</c> pour le libelle affiche a l'utilisateur.</param>
/// <param name="Glyph">
/// Forme dessinee par le moteur de rendu. C'est une valeur logique, pas un caractere a afficher
/// tel quel : <c>FolderHue.App</c> dessine chaque glyphe en vectoriel.
/// </param>
public sealed record Emblem(string Id, string ResourceKey, EmblemGlyph Glyph)
{
    /// <summary>Identifiant de l'absence d'embleme.</summary>
    public const string NoneId = "none";

    /// <summary>L'absence d'embleme : l'icone ne porte que la couleur.</summary>
    public static Emblem None { get; } = new(NoneId, "Emblem_None", EmblemGlyph.None);
}

/// <summary>Forme geometrique dessinee pour un embleme.</summary>
public enum EmblemGlyph
{
    /// <summary>Aucun dessin.</summary>
    None = 0,

    /// <summary>Point d'exclamation — « important ».</summary>
    Exclamation,

    /// <summary>Fleche / chevron — « en cours ».</summary>
    Arrow,

    /// <summary>Coche — « termine ».</summary>
    Check,

    /// <summary>Cadenas — « verrouille ».</summary>
    Lock,

    /// <summary>Etoile — « favori ».</summary>
    Star,
}
