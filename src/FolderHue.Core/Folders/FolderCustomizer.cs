using System.Runtime.Versioning;
using FolderHue.Core.Palette;
using FolderHue.Core.Storage;

namespace FolderHue.Core.Folders;

/// <summary>
/// Applique et retire la colorisation d'un dossier.
/// </summary>
/// <remarks>
/// C'est le point d'entree metier appele par le menu contextuel comme par l'interface de reglages.
/// Aucune methode publique ne leve : toute erreur est journalisee et convertie en
/// <see cref="OperationResult"/> (CLAUDE.md §6.5).
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class FolderCustomizer
{
    /// <summary>L'icone demandee n'a pas encore ete generee.</summary>
    public const string ReasonIconMissing = "Error_IconMissing";

    /// <summary>L'identifiant de couleur est inconnu du catalogue.</summary>
    public const string ReasonUnknownColor = "Error_UnknownColor";

    /// <summary>L'identifiant d'embleme est inconnu du catalogue.</summary>
    public const string ReasonUnknownEmblem = "Error_UnknownEmblem";

    /// <summary>Les droits manquent pour modifier le dossier.</summary>
    public const string ReasonAccessDenied = "Error_AccessDenied";

    /// <summary>Une erreur d'entree / sortie est survenue.</summary>
    public const string ReasonIo = "Error_Io";

    /// <summary>Les cles que nous ecrivons dans <c>desktop.ini</c>, et elles seules.</summary>
    private static readonly (string Section, string Key)[] OwnedKeys =
        [(DesktopIni.ShellClassInfoSection, DesktopIni.IconResourceKey)];

    private readonly AppPaths _paths;
    private readonly ProtectedPaths _protection;
    private readonly AppliedJournal _journal;
    private readonly Log _log;

    /// <summary>Construit un personnalisateur.</summary>
    /// <param name="paths">Emplacements de travail.</param>
    /// <param name="protection">Liste d'exclusion des dossiers a ne jamais modifier.</param>
    /// <param name="journal">Journal des dossiers colorises.</param>
    /// <param name="log">Journal de diagnostic.</param>
    /// <exception cref="ArgumentNullException">Un argument vaut <see langword="null"/>.</exception>
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

    /// <summary>Construit un personnalisateur branche sur les emplacements reels de la machine.</summary>
    /// <returns>Une instance prete a l'emploi.</returns>
    public static FolderCustomizer CreateDefault()
    {
        AppPaths paths = AppPaths.Default;
        return new FolderCustomizer(
            paths,
            ProtectedPaths.CreateDefault(),
            new AppliedJournal(paths.JournalFile),
            Log.Default);
    }

    /// <summary>Le journal des dossiers actuellement colorises.</summary>
    public AppliedJournal Journal => _journal;

    /// <summary>
    /// Applique une couleur et un embleme a un dossier.
    /// </summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <param name="colorId">Identifiant de la couleur, issu de <see cref="PaletteCatalog"/>.</param>
    /// <param name="emblemId">
    /// Identifiant de l'embleme, ou <see langword="null"/> pour conserver l'embleme deja applique.
    /// </param>
    /// <returns>Le resultat de l'operation.</returns>
    public OperationResult Apply(string folderPath, string colorId, string? emblemId)
    {
        try
        {
            ProtectionResult protection = _protection.Evaluate(folderPath);
            if (protection.IsProtected)
            {
                return OperationResult.Failed(protection.ReasonKey!, folderPath);
            }

            string full = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar);
            AppliedEntry? existing = _journal.Find(full);

            // Un embleme non precise conserve celui deja en place : appliquer une couleur ne doit
            // pas effacer le marqueur de statut, et inversement.
            string resolvedEmblemId = emblemId ?? existing?.EmblemId ?? Emblem.NoneId;

            if (PaletteCatalog.FindColor(colorId) is not FolderColor color)
            {
                return OperationResult.Failed(ReasonUnknownColor, colorId);
            }

            if (PaletteCatalog.FindEmblem(resolvedEmblemId) is null)
            {
                return OperationResult.Failed(ReasonUnknownEmblem, resolvedEmblemId);
            }

            // Ni teinte ni embleme : il n'y a plus rien a afficher. Ecrire un desktop.ini pointant
            // sur une copie de l'icone d'origine serait du bruit sur le disque de l'utilisateur ;
            // la seule action correcte est de rendre le dossier a son etat initial.
            if (color.IsNeutral && string.Equals(resolvedEmblemId, Emblem.NoneId, StringComparison.OrdinalIgnoreCase))
            {
                return Reset(full);
            }

            string iconPath = _paths.IconPath(colorId, resolvedEmblemId);
            if (!File.Exists(iconPath))
            {
                // Le shell ne genere jamais d'icone : toute la palette est pre-generee a
                // l'installation (CLAUDE.md §4.3). L'appelant relancera la pre-generation.
                return OperationResult.Failed(ReasonIconMissing, iconPath);
            }

            string iniPath = DesktopIniFile.PathFor(full);
            string backupPath = DesktopIniFile.BackupPathFor(full);

            // La sauvegarde ne se decide qu'a la PREMIERE application (CLAUDE.md §6.1).
            // Ensuite, le desktop.ini en place est le notre : le sauvegarder reviendrait a prendre
            // notre propre production pour l'original de l'utilisateur, et la reinitialisation le
            // restaurerait au lieu de le supprimer.
            bool hadDesktopIni = existing?.HadDesktopIni ?? File.Exists(iniPath);
            string? recordedBackup = existing?.BackupPath;

            if (existing is null)
            {
                if (hadDesktopIni && !File.Exists(backupPath))
                {
                    File.Copy(iniPath, backupPath, overwrite: false);
                    FolderAttributes.MakeHiddenSystem(backupPath);
                }

                // Une sauvegarde orpheline, laissee par une execution dont le journal a ete perdu,
                // reste la trace de l'etat d'origine : on la reprend a notre compte.
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

            // L'attribut AVANT l'ecriture, et non l'inverse. L'Explorateur surveille lui-meme le
            // contenu des dossiers : s'il relit le dossier dans l'intervalle, il le voit porteur
            // d'un desktop.ini mais depourvu de l'attribut, en conclut « aucune personnalisation »
            // et met ce verdict en cache. Dans l'autre sens, la fenetre est inoffensive : un
            // dossier marque mais sans desktop.ini est simplement un dossier sans icone.
            bool weSetReadOnly = FolderAttributes.EnsureFolderCustomizable(full);

            try
            {
                DesktopIniFile.Write(iniPath, document);
            }
            catch
            {
                // L'attribut a ete pose pour une colorisation qui n'aura pas lieu : on le retire.
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

            // L'API officielle repose la meme icone. C'est elle, et non la notification, qui
            // repeint une vue deja ouverte : voir NativeMethods.SetFolderIcon.
            NativeMethods.SetFolderIcon(full, iconPath, 0);

            NativeMethods.NotifyFolderChanged(full);
            _log.Info($"Colorise : « {full} » en {colorId}/{resolvedEmblemId}.");
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
    /// Determine la couleur a conserver quand l'utilisateur ne choisit qu'un embleme.
    /// </summary>
    /// <param name="folderPath">Le dossier concerne.</param>
    /// <returns>
    /// La couleur deja appliquee, ou l'identifiant de <see cref="PaletteCatalog.Neutral"/> si le
    /// dossier n'a jamais ete colorise.
    /// </returns>
    /// <remarks>
    /// Le repli est la couleur d'origine, et surtout <b>pas</b> la premiere teinte de la palette :
    /// poser un marqueur de statut ne doit pas choisir une couleur a la place de l'utilisateur.
    /// <para>
    /// Le menu contextuel et l'application appellent tous deux cette methode : c'est elle qui
    /// garantit qu'ils resolvent la couleur de la meme facon.
    /// </para>
    /// </remarks>
    public string ResolveColorFor(string folderPath)
        => _journal.Find(folderPath)?.ColorId ?? PaletteCatalog.Neutral.Id;

    /// <summary>
    /// Retire la colorisation d'un dossier et restaure son etat d'origine.
    /// </summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <returns>Le resultat de l'operation. Un dossier jamais colorise retourne un succes.</returns>
    /// <remarks>
    /// La reinitialisation retire la cle <c>IconResource</c>, supprime <c>desktop.ini</c> s'il ne
    /// contenait que nos cles, et ne retire l'attribut ReadOnly du dossier que si le journal
    /// atteste que c'est nous qui l'avions pose (CLAUDE.md §6.3).
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
                // Le dossier a disparu : on nettoie simplement la trace.
                _journal.Remove(full);
                return OperationResult.Ok;
            }

            if (File.Exists(iniPath))
            {
                DesktopIniDocument document = DesktopIniFile.Read(iniPath);
                string? current = document.Content.GetValue(
                    DesktopIni.ShellClassInfoSection,
                    DesktopIni.IconResourceKey);

                // On ne touche a l'icone que si elle est la notre : un dossier portant une icone
                // posee par un autre outil doit rester intact.
                bool ours = entry is not null || PointsToOurIcons(current);

                if (ours)
                {
                    string? original = ReadBackupIconResource(backupPath);

                    if (original is not null)
                    {
                        // Le dossier avait deja une icone avant notre passage : on la restaure au
                        // lieu de la supprimer.
                        document.Content.SetValue(
                            DesktopIni.ShellClassInfoSection,
                            DesktopIni.IconResourceKey,
                            original);

                        DesktopIniFile.Write(iniPath, document);
                    }
                    else
                    {
                        // §6.3 : le fichier n'est supprime que s'il ne contenait que nos cles.
                        // Le test se fait avant le retrait, tant que le fichier est intact.
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
                            // Sinon on le laisse en place, allege de nos seules cles.
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
            _log.Info($"Reinitialise : « {full} ».");
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
    /// Retire l'attribut ReadOnly sans jamais lever.
    /// </summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <remarks>
    /// Utilise sur le chemin d'erreur uniquement : l'exception d'origine doit remonter intacte,
    /// pas etre masquee par un echec de nettoyage.
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

        // La valeur a la forme « chemin,index » : on isole le chemin avant la derniere virgule.
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
