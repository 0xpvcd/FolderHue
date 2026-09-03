namespace FolderHue.Core.Storage;

/// <summary>
/// Ligne de commande de <c>FolderHue.App</c>, telle que l'appelle le menu contextuel.
/// </summary>
/// <remarks>
/// Le shell delegue a l'application tout ce qu'il ne doit pas faire lui-meme : generer une icone,
/// afficher une boite de dialogue (CLAUDE.md §4.3, §6.5). Les deux projets referencent donc les
/// memes constantes plutot que de recopier les chaines de part et d'autre — une divergence d'un
/// caractere se traduirait par un clic sans effet, sans le moindre message.
/// </remarks>
public static class AppCommands
{
    /// <summary>Pre-genere toute la palette, sans interface.</summary>
    public const string Pregenerate = "--pregenerate";

    /// <summary>Force la regeneration meme si les fichiers existent.</summary>
    public const string Force = "--force";

    /// <summary>Reinitialise tous les dossiers du journal, sans interface.</summary>
    public const string ResetAll = "--reset-all";

    /// <summary>Affiche le compte de dossiers refuses par la liste d'exclusion.</summary>
    public const string ReportSkipped = "--report-skipped";

    /// <summary>Produit les logos du paquet MSIX.</summary>
    public const string GeneratePackageAssets = "--generate-package-assets";

    /// <summary>
    /// Regenere ce qui manque puis applique l'operation demandee.
    /// </summary>
    /// <remarks>
    /// Forme attendue : <c>--apply &lt;couleur&gt; &lt;embleme&gt; &lt;dossier&gt;…</c>, ou
    /// <see cref="Absent"/> tient lieu de couleur ou d'embleme non precise.
    /// </remarks>
    public const string Apply = "--apply";

    /// <summary>
    /// Marque un argument non precise.
    /// </summary>
    /// <remarks>
    /// Une chaine vide ne conviendrait pas : <c>ProcessStartInfo.ArgumentList</c> la transmet bien,
    /// mais elle se confond avec une valeur oubliee. Un tiret est explicite et ne peut pas etre un
    /// identifiant de couleur ou d'embleme valide.
    /// </remarks>
    public const string Absent = "-";
}
