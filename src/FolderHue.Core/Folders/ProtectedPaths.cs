using System.Runtime.Versioning;
using FolderHue.Core.Storage;

namespace FolderHue.Core.Folders;

/// <summary>
/// Supplies the list of well-known system folders to protect.
/// </summary>
/// <remarks>
/// The interface exists so that tests can inject a fixed list rather than querying the machine's
/// shell (CLAUDE.md 8).
/// </remarks>
public interface IKnownFolderProvider
{
    /// <summary>
    /// Folders protected as themselves: only the folder is refused, not what it contains.
    /// </summary>
    /// <returns>Absolute paths. Empty or unresolved entries are ignored.</returns>
    IReadOnlyList<string> GetExactProtectedFolders();

    /// <summary>
    /// Folders protected along with everything beneath them.
    /// </summary>
    /// <returns>Absolute paths. Empty or unresolved entries are ignored.</returns>
    IReadOnlyList<string> GetProtectedSubtrees();
}

/// <summary>
/// Resolves the current machine's protected folders through <c>SHGetKnownFolderPath</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class KnownFolderProvider : IKnownFolderProvider
{
    // KNOWNFOLDERID, KnownFolders.h. Docs: https://learn.microsoft.com/windows/win32/shell/knownfolderid
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
/// Verdict on a folder proposed for coloring.
/// </summary>
/// <param name="IsProtected"><see langword="true"/> when the folder must be refused.</param>
/// <param name="ReasonKey">
/// Key in <c>Strings.resx</c> explaining the refusal, or <see langword="null"/> when the folder is
/// acceptable.
/// </param>
public readonly record struct ProtectionResult(bool IsProtected, string? ReasonKey)
{
    /// <summary>The folder is acceptable.</summary>
    public static ProtectionResult Allowed { get; } = new(false, null);

    /// <summary>Builds a refusal.</summary>
    /// <param name="reasonKey">Resource key explaining the refusal.</param>
    /// <returns>The matching verdict.</returns>
    public static ProtectionResult Denied(string reasonKey) => new(true, reasonKey);
}

/// <summary>
/// Enforces the exclusion list: the folders the application must never modify.
/// </summary>
/// <remarks>
/// This list is not negotiable (CLAUDE.md 6.2). It covers the system trees, the user's known
/// folders, volume roots, reparse points (junctions and symbolic links) and our own working
/// directories.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class ProtectedPaths
{
    /// <summary>The path is empty or syntactically invalid.</summary>
    public const string ReasonInvalidPath = "Protection_InvalidPath";

    /// <summary>The folder does not exist.</summary>
    public const string ReasonNotFound = "Protection_NotFound";

    /// <summary>The path names the root of a volume or a share.</summary>
    public const string ReasonVolumeRoot = "Protection_VolumeRoot";

    /// <summary>The folder belongs to a system tree.</summary>
    public const string ReasonSystemTree = "Protection_SystemTree";

    /// <summary>The folder is a Windows known folder.</summary>
    public const string ReasonKnownFolder = "Protection_KnownFolder";

    /// <summary>The folder is a junction or a symbolic link.</summary>
    public const string ReasonReparsePoint = "Protection_ReparsePoint";

    /// <summary>The folder belongs to FolderHue's own workspace.</summary>
    public const string ReasonApplicationData = "Protection_ApplicationData";

    private readonly AppPaths _paths;
    private readonly IReadOnlyList<string> _exact;
    private readonly IReadOnlyList<string> _subtrees;

    /// <summary>Builds the exclusion list.</summary>
    /// <param name="knownFolders">Source of the known folders.</param>
    /// <param name="paths">The application's own locations, protected as well.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ProtectedPaths(IKnownFolderProvider knownFolders, AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(knownFolders);
        ArgumentNullException.ThrowIfNull(paths);

        _paths = paths;
        _exact = Normalize(knownFolders.GetExactProtectedFolders());
        _subtrees = Normalize(knownFolders.GetProtectedSubtrees());
    }

    /// <summary>Builds the exclusion list for the current machine.</summary>
    /// <returns>An instance wired to the real shell and to <see cref="AppPaths.Default"/>.</returns>
    public static ProtectedPaths CreateDefault() => new(new KnownFolderProvider(), AppPaths.Default);

    /// <summary>Determines whether a folder may be colored.</summary>
    /// <param name="folderPath">Folder path, absolute or relative.</param>
    /// <returns>The verdict, carrying a resource key when the folder is refused.</returns>
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

        // The root of a volume or a network share cannot be customised: desktop.ini is ignored
        // there, and ReadOnly on a root has side effects.
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

        // A junction or a symbolic link points elsewhere: writing into it would modify a target
        // the user did not select.
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

        // UNC case: \\server\share has no useful parent, Path.GetDirectoryName returns
        // \\server, which is not a real folder.
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
                // A known folder that does not resolve is simply ignored.
            }
        }

        return result;
    }
}
