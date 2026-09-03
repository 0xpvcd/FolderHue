namespace FolderHue.Core.Storage;

/// <summary>
/// Trace de ce que nous avons modifie sur un dossier donne.
/// </summary>
/// <remarks>
/// Cette trace n'est pas un simple confort : elle conditionne une reinitialisation reellement
/// propre. Sans elle, impossible de savoir si l'attribut <c>+r</c> du dossier etait deja present
/// avant notre passage, et le retirer casserait la configuration de l'utilisateur (CLAUDE.md §6.3).
/// </remarks>
public sealed class AppliedEntry
{
    /// <summary>Chemin absolu du dossier colorise.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Identifiant de la couleur appliquee.</summary>
    public string ColorId { get; set; } = string.Empty;

    /// <summary>Identifiant de l'embleme applique, <c>"none"</c> si aucun.</summary>
    public string EmblemId { get; set; } = Palette.Emblem.NoneId;

    /// <summary>
    /// <see langword="true"/> si c'est nous qui avons pose l'attribut ReadOnly sur le dossier.
    /// Seul ce cas autorise a le retirer lors de la reinitialisation.
    /// </summary>
    public bool WeSetReadOnly { get; set; }

    /// <summary><see langword="true"/> si un <c>desktop.ini</c> existait deja avant notre passage.</summary>
    public bool HadDesktopIni { get; set; }

    /// <summary>
    /// Chemin de la sauvegarde du <c>desktop.ini</c> d'origine, ou <see langword="null"/> s'il n'y
    /// avait rien a sauvegarder.
    /// </summary>
    public string? BackupPath { get; set; }

    /// <summary>Date d'application, en UTC.</summary>
    public DateTimeOffset AppliedUtc { get; set; }
}
