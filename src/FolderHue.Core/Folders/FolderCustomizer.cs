using System.Runtime.Versioning;
using FolderHue.Core.Palette;
using FolderHue.Core.Storage;

namespace FolderHue.Core.Folders;

/// <summary>
/// Applies and removes a folder's coloring.
/// </summary>
/// <remarks>
/// This is the business entry point, called by the context menu and by the settings window alike.
/// No public method throws: every error is logged and turned into an
/// <see cref="OperationResult"/> (CLAUDE.md 6.5).
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class FolderCustomizer
{
    /// <summary>The requested icon has not been generated yet.</summary>
    public const string ReasonIconMissing = "Error_IconMissing";

    /// <summary>The color identifier is unknown to the catalogue.</summary>
    public const string ReasonUnknownColor = "Error_UnknownColor";

    /// <summary>The emblem identifier is unknown to the catalogue.</summary>
    public const string ReasonUnknownEmblem = "Error_UnknownEmblem";

    /// <summary>Permissions are missing to modify the folder.</summary>
    public const string ReasonAccessDenied = "Error_AccessDenied";

    /// <summary>An input / output error occurred.</summary>
    public const string ReasonIo = "Error_Io";

    /// <summary>The keys we write into <c>desktop.ini</c>, and those only.</summary>
    private static readonly (string Section, string Key)[] OwnedKeys =
        [(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey)];

    private readonly AppPaths _paths;
    private readonly ProtectedPaths _protection;
    private readonly AppliedJournal _journal;
    private readonly Log _log;

    /// <summary>Builds a customizer.</summary>
    /// <param name="paths">Working locations.</param>
    /// <param name="protection">Exclusion list of folders never to modify.</param>
    /// <param name="journal">Journal of colored folders.</param>
    /// <param name="log">Diagnostic log.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public FolderCustomizer(AppPaths paths, ProtectedPaths protection, AppliedJournal journal, Log log)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(protection);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(log);

        _paths = paths;
        _protection = protection;
        _journal = journal;
        _log = log;
    }

    /// <summary>Builds a customizer wired to the machine's real locations.</summary>
    /// <returns>A ready-to-use instance.</returns>
    public static FolderCustomizer CreateDefault()
    {
        AppPaths paths = AppPaths.Default;
        return new FolderCustomizer(
            paths,
            ProtectedPaths.CreateDefault(),
            new AppliedJournal(paths.JournalFile),
            Log.Default);
    }

    /// <summary>The journal of currently colored folders.</summary>
    public AppliedJournal Journal => _journal;

    /// <summary>
    /// Applies a color and an emblem to a folder.
    /// </summary>
    /// <param name="folderPath">Path of the folder.</param>
    /// <param name="colorId">Color identifier, from <see cref="PaletteCatalog"/>.</param>
    /// <param name="emblemId">
    /// Emblem identifier, or <see langword="null"/> to keep whichever emblem is already applied.
    /// </param>
    /// <returns>The outcome of the operation.</returns>
    public OperationResult Apply(string folderPath, string colorId, string? emblemId)
    {
        try
        {
            ProtectionResult protection = _protection.Evaluate(folderPath);
            if (protection.IsProtected)
            {
                // The folder is gone: the refusal happens before the neutral path reaches Reset,
                // which used to be the only place a stale record was removed. Without this purge
                // the entry outlives its folder indefinitely.
                if (string.Equals(protection.ReasonKey, ProtectedPaths.ReasonNotFound, StringComparison.Ordinal))
                {
                    _journal.PruneMissing();
                }

                return OperationResult.Failed(protection.ReasonKey!, folderPath);
            }

            string full = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar);
            AppliedEntry? existing = _journal.Find(full);

            // An unspecified emblem keeps whichever one is in place: applying a color must not
            // erase the status marker, and the other way round.
            string resolvedEmblemId = emblemId ?? existing?.EmblemId ?? Emblem.NoneId;

            if (PaletteCatalog.FindColor(colorId) is not FolderColor color)
            {
                return OperationResult.Failed(ReasonUnknownColor, colorId);
            }

            if (PaletteCatalog.FindEmblem(resolvedEmblemId) is null)
            {
                return OperationResult.Failed(ReasonUnknownEmblem, resolvedEmblemId);
            }

            // Neither hue nor emblem: there is nothing left to show. Writing a desktop.ini
            // pointing at a copy of the original icon would be noise on the user's disk; the only
            // correct action is to return the folder to its initial state.
            if (color.IsNeutral && string.Equals(resolvedEmblemId, Emblem.NoneId, StringComparison.OrdinalIgnoreCase))
            {
                return Reset(full);
            }

            string iconPath = _paths.IconPath(colorId, resolvedEmblemId);
            if (!File.Exists(iconPath))
            {
                // The shell never generates an icon: the whole palette is pre-generated at
                // install time (CLAUDE.md 4.3). The caller will rerun the pre-generation.
                return OperationResult.Failed(ReasonIconMissing, iconPath);
            }

            string iniPath = DesktopIniFile.PathFor(full);
            string backupPath = DesktopIniFile.BackupPathFor(full);

            // The backup is decided on the FIRST application only (CLAUDE.md 6.1). After that,
            // the desktop.ini in place is ours: backing it up would mistake our own output for the
            // user's original, and a reset would restore it instead of deleting it.
            bool hadDesktopIni = existing?.HadDesktopIni ?? File.Exists(iniPath);
            string? recordedBackup = existing?.BackupPath;

            if (existing is null)
            {
                if (hadDesktopIni && !File.Exists(backupPath))
                {
                    File.Copy(iniPath, backupPath, overwrite: false);
                    FolderAttributes.MakeHiddenSystem(backupPath);
                }

                // An orphaned backup, left by a run whose journal was lost, is still the record
                // of the original state: adopt it.
                if (File.Exists(backupPath))
                {
                    recordedBackup = backupPath;
                }
            }

            DesktopIniDocument document = DesktopIniFile.Read(iniPath);
            document.Content.SetValue(
                DesktopIni.ShellClassInfoSection,
                DesktopIni.IconResourceKey,
                iconPath + ",0");

            // The attribute BEFORE the write, not the other way round. Explorer watches folder
            // contents for its own purposes: if it re-reads the folder in between, it sees a
            // desktop.ini with no attribute, concludes "no customisation" and caches that verdict.
            // The other way round the window is harmless: a marked folder with no desktop.ini is
            // simply a folder with no icon.
            bool weSetReadOnly = FolderAttributes.EnsureFolderCustomizable(full);

            try
            {
                DesktopIniFile.Write(iniPath, document);
            }
            catch
            {
                // The attribute was set for a coloring that will not happen: take it back off.
                if (weSetReadOnly)
                {
                    TryClearReadOnly(full);
                }

                throw;
            }

            _journal.Upsert(new AppliedEntry
            {
                Path = full,
                ColorId = colorId,
                EmblemId = resolvedEmblemId,
                WeSetReadOnly = existing?.WeSetReadOnly ?? weSetReadOnly,
                HadDesktopIni = hadDesktopIni,
                BackupPath = recordedBackup,
                AppliedUtc = DateTimeOffset.UtcNow,
            });

            // The official API re-writes the same icon. It, and not the notification, is what
            // repaints an already-open view: see NativeMethods.SetFolderIcon.
            NativeMethods.SetFolderIcon(full, iconPath, 0);

            NativeMethods.NotifyFolderChanged(full);
            _log.Info($"Colored: \"{full}\" as {colorId}/{resolvedEmblemId}.");
            return OperationResult.Ok;
        }
        catch (UnauthorizedAccessException e)
        {
            _log.Error($"Acces refuse en colorisant « {folderPath} ».", e);
            return OperationResult.Failed(ReasonAccessDenied, folderPath);
        }
        catch (Exception e) when (e is IOException or ArgumentException or NotSupportedException)
        {
            _log.Error($"Echec de la colorisation de « {folderPath} ».", e);
            return OperationResult.Failed(ReasonIo, folderPath);
        }
    }

    /// <summary>
    /// Determines which color to keep when the user picks an emblem only.
    /// </summary>
    /// <param name="folderPath">The folder concerned.</param>
    /// <returns>
    /// The color already applied, or the identifier of <see cref="PaletteCatalog.Neutral"/> when
    /// the folder was never colored.
    /// </returns>
    /// <remarks>
    /// The fallback is the original color, and emphatically <b>not</b> the first hue of the
    /// palette: placing a status marker must not choose a color on the user's behalf.
    /// <para>
    /// The context menu and the application both call this method: it is what guarantees they
    /// resolve the color the same way.
    /// </para>
    /// </remarks>
    public string ResolveColorFor(string folderPath)
        => _journal.Find(folderPath)?.ColorId ?? PaletteCatalog.Neutral.Id;

    /// <summary>
    /// Removes a folder's coloring and restores its original state.
    /// </summary>
    /// <param name="folderPath">Path of the folder.</param>
    /// <returns>The outcome. A folder that was never colored returns success.</returns>
    /// <remarks>
    /// Resetting removes the <c>IconResource</c> key, deletes <c>desktop.ini</c> when it held
    /// nothing but our keys, and removes the folder's ReadOnly attribute only when the journal
    /// attests that we were the ones who set it (CLAUDE.md 6.3).
    /// </remarks>
    public OperationResult Reset(string folderPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return OperationResult.Failed(ProtectedPaths.ReasonInvalidPath, folderPath);
            }

            string full = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar);
            AppliedEntry? entry = _journal.Find(full);
            string iniPath = DesktopIniFile.PathFor(full);
            string backupPath = DesktopIniFile.BackupPathFor(full);

            if (!Directory.Exists(full))
            {
                // The folder is gone: simply clean up the record.
                _journal.Remove(full);
                return OperationResult.Ok;
            }

            if (File.Exists(iniPath))
            {
                DesktopIniDocument document = DesktopIniFile.Read(iniPath);
                string? current = document.Content.GetValue(
                    DesktopIni.ShellClassInfoSection,
                    DesktopIni.IconResourceKey);

                // Touch the icon only when it is ours: a folder carrying an icon another tool
                // set must be left intact.
                bool ours = entry is not null || PointsToOurIcons(current);

                if (ours)
                {
                    string? original = ReadBackupIconResource(backupPath);

                    if (original is not null)
                    {
                        // The folder already had an icon before we came along: restore it rather
                        // than deleting it.
                        document.Content.SetValue(
                            DesktopIni.ShellClassInfoSection,
                            DesktopIni.IconResourceKey,
                            original);

                        DesktopIniFile.Write(iniPath, document);
                    }
                    else
                    {
                        // 6.3: the file is deleted only when it held nothing but our keys. The
                        // test runs before the removal, while the file is still intact.
                        bool onlyOurs = document.Content.ContainsOnlyKeys(OwnedKeys);

                        document.Content.RemoveValue(
                            DesktopIni.ShellClassInfoSection,
                            DesktopIni.IconResourceKey);

                        document.Content.RemoveSectionIfEmpty(DesktopIni.ShellClassInfoSection);

                        if (onlyOurs && document.Content.IsEmpty)
                        {
                            DesktopIniFile.Delete(iniPath);
                        }
                        else
                        {
                            // Otherwise leave it in place, lightened of our keys only.
                            DesktopIniFile.Write(iniPath, document);
                        }
                    }
                }
            }

            if (File.Exists(backupPath))
            {
                FolderAttributes.ClearFileFlags(backupPath);
                File.Delete(backupPath);
            }

            if (entry?.WeSetReadOnly == true)
            {
                FolderAttributes.ClearFolderReadOnly(full);
            }

            _journal.Remove(full);
            NativeMethods.NotifyFolderChanged(full);
            _log.Info($"Reset: \"{full}\".");
            return OperationResult.Ok;
        }
        catch (UnauthorizedAccessException e)
        {
            _log.Error($"Acces refuse en reinitialisant « {folderPath} ».", e);
            return OperationResult.Failed(ReasonAccessDenied, folderPath);
        }
        catch (Exception e) when (e is IOException or ArgumentException or NotSupportedException)
        {
            _log.Error($"Echec de la reinitialisation de « {folderPath} ».", e);
            return OperationResult.Failed(ReasonIo, folderPath);
        }
    }

    /// <summary>
    /// Removes the ReadOnly attribute without ever throwing.
    /// </summary>
    /// <param name="folderPath">Path of the folder.</param>
    /// <remarks>
    /// Used on the error path only: the original exception must travel up intact, not be masked by
    /// a cleanup failure.
    /// </remarks>
    private static void TryClearReadOnly(string folderPath)
    {
        try
        {
            FolderAttributes.ClearFolderReadOnly(folderPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
    }

    private bool PointsToOurIcons(string? iconResource)
    {
        if (string.IsNullOrEmpty(iconResource))
        {
            return false;
        }

        // The value has the form "path,index": isolate the path before the last comma.
        int comma = iconResource.LastIndexOf(',');
        string candidate = comma > 0 ? iconResource[..comma] : iconResource;

        return _paths.ContainsPath(candidate.Trim().Trim('"'));
    }

    private static string? ReadBackupIconResource(string backupPath)
    {
        if (!File.Exists(backupPath))
        {
            return null;
        }

        try
        {
            DesktopIniDocument backup = DesktopIniFile.Read(backupPath);
            return backup.Content.GetValue(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
