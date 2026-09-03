using System.Runtime.Versioning;
using FolderHue.Core.Storage;

namespace FolderHue.Core.Folders;

/// <summary>
/// Fournit la liste des dossiers connus du systeme a proteger.
/// </summary>
/// <remarks>
/// L'interface existe pour que les tests puissent injecter une liste fixe plutot que d'interroger
/// le shell de la machine (CLAUDE.md §8).
/// </remarks>
public interface IKnownFolderProvider
{
    /// <summary>
    /// Dossiers proteges en tant que tels : seul le dossier lui-meme est refuse, pas son contenu.
    /// </summary>
    /// <returns>Des chemins absolus. Les entrees vides ou non resolues sont ignorees.</returns>
    IReadOnlyList<string> GetExactProtectedFolders();

    /// <summary>
    /// Dossiers proteges avec toute leur descendance.
    /// </summary>
    /// <returns>Des chemins absolus. Les entrees vides ou non resolues sont ignorees.</returns>
    IReadOnlyList<string> GetProtectedSubtrees();
}

/// <summary>
/// Resout les dossiers proteges de la machine courante via <c>SHGetKnownFolderPath</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class KnownFolderProvider : IKnownFolderProvider
{
    // KNOWNFOLDERID, KnownFolders.h. Doc : https://learn.microsoft.com/windows/win32/shell/knownfolderid
    private static readonly Guid Desktop = new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
    private static readonly Guid Documents = new("FDD39AD0-238F-46AF-ADB4-6C85480369C7");
    private static readonly Guid Downloads = new("374DE290-123F-4565-9164-39C4925E467B");
    private static readonly Guid Pictures = new("33E28130-4E1E-4676-835A-98395C3BC3BB");
    private static readonly Guid Music = new("4BD8D571-6D19-48D3-BE97-422220080E43");
    private static readonly Guid Videos = new("18989B1D-99B5-455B-841C-AB7C74E4DDFC");
    private static readonly Guid Profile = new("5E6C858F-0E22-4760-9AFE-EA3317B67173");
    private static readonly Guid UserProfiles = new("0762D272-C50A-4BB0-A382-697DCD729B80");
    private static readonly Guid Public = new("DFDF76A2-C82A-4D63-906A-5644AC457385");
    private static readonly Guid Favorites = new("1777F761-68AD-4D8A-87BD-30B759FA33DD");
    private static readonly Guid RoamingAppData = new("3EB685DB-65F9-4CF6-A03A-E3EF65729F3D");
    private static readonly Guid LocalAppData = new("F1B32785-6FBA-4FCF-9D55-7B8E7F157091");

    private static readonly Guid Windows = new("F38BF404-1D43-42F2-9305-67DE0B28FC23");
    private static readonly Guid System32 = new("1AC14E77-02E7-4E5D-B744-2EB1AE5198B7");
    private static readonly Guid ProgramData = new("62AB5D82-FDC1-4DC3-A9DD-070D1D495D97");
    private static readonly Guid ProgramFiles = new("905E63B6-C1BF-494E-B29C-65B732D3D21A");
    private static readonly Guid ProgramFilesX86 = new("7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E");

    /// <inheritdoc/>
    public IReadOnlyList<string> GetExactProtectedFolders() =>
        Resolve(
        [
            Desktop, Documents, Downloads, Pictures, Music, Videos,
            Profile, UserProfiles, Public, Favorites, RoamingAppData, LocalAppData,
        ]);

    /// <inheritdoc/>
    public IReadOnlyList<string> GetProtectedSubtrees() =>
        Resolve([Windows, System32, ProgramData, ProgramFiles, ProgramFilesX86]);

    private static IReadOnlyList<string> Resolve(Guid[] folderIds)
    {
        var result = new List<string>(folderIds.Length);

        foreach (Guid id in folderIds)
        {
            string? path = NativeMethods.GetKnownFolderPath(id);
            if (!string.IsNullOrWhiteSpace(path))
            {
                result.Add(path);
            }
        }

        return result;
    }
}

/// <summary>
/// Verdict rendu sur un dossier candidat a la colorisation.
/// </summary>
/// <param name="IsProtected"><see langword="true"/> si le dossier doit etre refuse.</param>
/// <param name="ReasonKey">
/// Cle de <c>Strings.resx</c> expliquant le refus, ou <see langword="null"/> si le dossier est
/// acceptable.
/// </param>
public readonly record struct ProtectionResult(bool IsProtected, string? ReasonKey)
{
    /// <summary>Le dossier est acceptable.</summary>
    public static ProtectionResult Allowed { get; } = new(false, null);

    /// <summary>Construit un refus.</summary>
    /// <param name="reasonKey">Cle de ressource expliquant le refus.</param>
    /// <returns>Le verdict correspondant.</returns>
    public static ProtectionResult Denied(string reasonKey) => new(true, reasonKey);
}

/// <summary>
/// Applique la liste d'exclusion : les dossiers que l'application ne doit jamais modifier.
/// </summary>
/// <remarks>
/// Cette liste n'est pas negociable (CLAUDE.md §6.2). Elle couvre les arborescences systeme, les
/// dossiers connus de l'utilisateur, les racines de volume, les points de reanalyse (jonctions et
/// liens symboliques) et nos propres dossiers de travail.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class ProtectedPaths
{
    /// <summary>Le chemin est vide ou syntaxiquement invalide.</summary>
    public const string ReasonInvalidPath = "Protection_InvalidPath";

    /// <summary>Le dossier n'existe pas.</summary>
    public const string ReasonNotFound = "Protection_NotFound";

    /// <summary>Le chemin designe la racine d'un volume ou d'un partage.</summary>
    public const string ReasonVolumeRoot = "Protection_VolumeRoot";

    /// <summary>Le dossier appartient a une arborescence systeme.</summary>
    public const string ReasonSystemTree = "Protection_SystemTree";

    /// <summary>Le dossier est un dossier connu de Windows.</summary>
    public const string ReasonKnownFolder = "Protection_KnownFolder";

    /// <summary>Le dossier est une jonction ou un lien symbolique.</summary>
    public const string ReasonReparsePoint = "Protection_ReparsePoint";

    /// <summary>Le dossier appartient a l'espace de travail de FolderHue.</summary>
    public const string ReasonApplicationData = "Protection_ApplicationData";

    private readonly AppPaths _paths;
    private readonly IReadOnlyList<string> _exact;
    private readonly IReadOnlyList<string> _subtrees;

    /// <summary>Construit la liste d'exclusion.</summary>
    /// <param name="knownFolders">Source des dossiers connus.</param>
    /// <param name="paths">Emplacements de travail de l'application, egalement proteges.</param>
    /// <exception cref="ArgumentNullException">Un argument vaut <see langword="null"/>.</exception>
    public ProtectedPaths(IKnownFolderProvider knownFolders, AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(knownFolders);
        ArgumentNullException.ThrowIfNull(paths);

        _paths = paths;
        _exact = Normalize(knownFolders.GetExactProtectedFolders());
        _subtrees = Normalize(knownFolders.GetProtectedSubtrees());
    }

    /// <summary>Construit la liste d'exclusion de la machine courante.</summary>
    /// <returns>Une instance branchee sur le shell reel et sur <see cref="AppPaths.Default"/>.</returns>
    public static ProtectedPaths CreateDefault() => new(new KnownFolderProvider(), AppPaths.Default);

    /// <summary>Determine si un dossier peut etre colorise.</summary>
    /// <param name="folderPath">Chemin du dossier, absolu ou relatif.</param>
    /// <returns>Le verdict, assorti d'une cle de ressource en cas de refus.</returns>
    public ProtectionResult Evaluate(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return ProtectionResult.Denied(ReasonInvalidPath);
        }

        string full;
        try
        {
            full = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ProtectionResult.Denied(ReasonInvalidPath);
        }

        if (full.Length == 0)
        {
            return ProtectionResult.Denied(ReasonInvalidPath);
        }

        // La racine d'un volume ou d'un partage reseau ne se personnalise pas : desktop.ini y est
        // ignore et l'attribut ReadOnly sur la racine a des effets de bord.
        if (IsRoot(folderPath, full))
        {
            return ProtectionResult.Denied(ReasonVolumeRoot);
        }

        if (_paths.ContainsPath(full))
        {
            return ProtectionResult.Denied(ReasonApplicationData);
        }

        foreach (string subtree in _subtrees)
        {
            if (IsSameOrUnder(full, subtree))
            {
                return ProtectionResult.Denied(ReasonSystemTree);
            }
        }

        foreach (string exact in _exact)
        {
            if (full.Equals(exact, StringComparison.OrdinalIgnoreCase))
            {
                return ProtectionResult.Denied(ReasonKnownFolder);
            }
        }

        if (!Directory.Exists(full))
        {
            return ProtectionResult.Denied(ReasonNotFound);
        }

        // Une jonction ou un lien symbolique renvoie ailleurs : ecrire dedans reviendrait a
        // modifier une cible que l'utilisateur n'a pas selectionnee.
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
        {
            return ProtectionResult.Denied(ReasonReparsePoint);
        }

        return ProtectionResult.Allowed;
    }

    private static bool IsRoot(string original, string normalized)
    {
        string? parent = Path.GetDirectoryName(original.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(parent))
        {
            return true;
        }

        // Cas UNC : \\serveur\partage n'a pas de parent utile, Path.GetDirectoryName retourne
        // \\serveur, qui n'est pas un dossier reel.
        if (normalized.StartsWith(@"\\", StringComparison.Ordinal))
        {
            string body = normalized[2..];
            int separators = 0;
            foreach (char c in body)
            {
                if (c == Path.DirectorySeparatorChar)
                {
                    separators++;
                }
            }

            if (separators < 2)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameOrUnder(string candidate, string ancestor)
        => candidate.Equals(ancestor, StringComparison.OrdinalIgnoreCase)
        || candidate.StartsWith(ancestor + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string> paths)
    {
        var result = new List<string>(paths.Count);

        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            try
            {
                result.Add(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar));
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Un dossier connu non resolu est simplement ignore.
            }
        }

        return result;
    }
}
