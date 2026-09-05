using FolderHue.Core.Storage;

namespace FolderHue.Core.Tests;

/// <summary>
/// A disposable workspace: a temporary directory deleted when the test ends.
/// </summary>
/// <remarks>
/// CLAUDE.md 8 forbids tests from touching a real user folder. Everything therefore goes through
/// this class, the simulated application root included.
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

    /// <summary>Simulated application root, under the temporary directory.</summary>
    internal AppPaths AppPaths { get; }

    /// <summary>Creates a working subfolder and returns its absolute path.</summary>
    internal string CreateFolder(string name)
    {
        string path = Path.Combine(_root, "work", name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Creates a dummy icon file for one color + emblem pair.</summary>
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
            // A leftover temporary directory must not fail a test.
        }
    }

    private static void ClearReadOnlyRecursively(string path)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists)
        {
            return;
        }

        // Colored folders carry ReadOnly and their desktop.ini files are hidden + system:
        // without this cleanup the recursive delete fails.
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
