using FolderHue.Core.Storage;

namespace FolderHue.Core.Tests;

/// <summary>
/// Un espace de travail jetable : un dossier temporaire supprime a la fin du test.
/// </summary>
/// <remarks>
/// CLAUDE.md §8 interdit aux tests de toucher un vrai dossier utilisateur. Tout passe donc par
/// cette classe, y compris la racine applicative simulee.
/// </remarks>
internal sealed class TempWorkspace : IDisposable
{
    private readonly string _root;

    internal TempWorkspace()
    {
        _root = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        AppPaths = new AppPaths(Path.Combine(_root, "appdata"));
        AppPaths.EnsureDirectories();
    }

    /// <summary>Racine applicative simulee, sous le dossier temporaire.</summary>
    internal AppPaths AppPaths { get; }

    /// <summary>Cree un sous-dossier de travail et retourne son chemin absolu.</summary>
    internal string CreateFolder(string name)
    {
        string path = Path.Combine(_root, "work", name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Cree un fichier d'icone factice pour une combinaison couleur + embleme.</summary>
    internal string CreateFakeIcon(string colorId, string? emblemId = null)
    {
        string path = AppPaths.IconPath(colorId, emblemId);
        File.WriteAllBytes(path, [0x00, 0x00, 0x01, 0x00]);
        return path;
    }

    public void Dispose()
    {
        try
        {
            ClearReadOnlyRecursively(_root);
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Un dossier temporaire residuel ne doit pas faire echouer un test.
        }
    }

    private static void ClearReadOnlyRecursively(string path)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists)
        {
            return;
        }

        // Les dossiers colorises portent ReadOnly et leurs desktop.ini sont caches + systeme :
        // sans ce nettoyage, la suppression recursive echoue.
        foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            if (entry is DirectoryInfo && (entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            entry.Attributes = FileAttributes.Normal;
        }

        directory.Attributes = FileAttributes.Normal;
    }
}
