namespace FolderHue.Core.Storage;

/// <summary>
/// A record of what FolderHue changed on one folder.
/// </summary>
/// <remarks>
/// This is not a convenience: it is what makes a genuinely clean reset possible. Without it there
/// is no way to tell whether the folder already carried the read-only attribute beforehand, and
/// removing it would break the user's own configuration (CLAUDE.md 6.3).
/// </remarks>
public sealed class AppliedEntry
{
    /// <summary>Absolute path of the colored folder.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Identifier of the color applied.</summary>
    public string ColorId { get; set; } = string.Empty;

    /// <summary>Identifier of the emblem applied, <c>"none"</c> when there is none.</summary>
    public string EmblemId { get; set; } = Palette.Emblem.NoneId;

    /// <summary>
    /// <see langword="true"/> when FolderHue is the one that set the folder's ReadOnly attribute.
    /// Only then may a reset remove it.
    /// </summary>
    public bool WeSetReadOnly { get; set; }

    /// <summary><see langword="true"/> when a <c>desktop.ini</c> already existed beforehand.</summary>
    public bool HadDesktopIni { get; set; }

    /// <summary>
    /// Path of the backup of the original <c>desktop.ini</c>, or <see langword="null"/> when there
    /// was nothing to back up.
    /// </summary>
    public string? BackupPath { get; set; }

    /// <summary>When the color was applied, in UTC.</summary>
    public DateTimeOffset AppliedUtc { get; set; }
}
