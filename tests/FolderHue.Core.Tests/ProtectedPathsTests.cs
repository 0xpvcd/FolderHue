using System.Diagnostics;
using FolderHue.Core.Folders;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Checks the exclusion list, which is not negotiable (CLAUDE.md 6.2).
/// </summary>
public sealed class ProtectedPathsTests
{
    private sealed class FakeKnownFolders(IReadOnlyList<string> exact, IReadOnlyList<string> subtrees)
        : IKnownFolderProvider
    {
        public IReadOnlyList<string> GetExactProtectedFolders() => exact;

        public IReadOnlyList<string> GetProtectedSubtrees() => subtrees;
    }

    [Fact]
    public void Evaluate_refuses_a_volume_root()
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        ProtectionResult result = protection.Evaluate(@"C:\");

        Assert.True(result.IsProtected);
        Assert.Equal(ProtectedPaths.ReasonVolumeRoot, result.ReasonKey);
    }

    [Fact]
    public void Evaluate_refuses_a_network_share_root()
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        ProtectionResult result = protection.Evaluate(@"\\serveur\partage");

        Assert.True(result.IsProtected);
        Assert.Equal(ProtectedPaths.ReasonVolumeRoot, result.ReasonKey);
    }

    [Fact]
    public void Evaluate_refuses_everything_under_a_system_tree()
    {
        using var workspace = new TempWorkspace();
        string windows = workspace.CreateFolder("Windows");
        string nested = Path.Combine(windows, "System32", "drivers");
        Directory.CreateDirectory(nested);

        var protection = new ProtectedPaths(new FakeKnownFolders([], [windows]), workspace.AppPaths);

        Assert.Equal(ProtectedPaths.ReasonSystemTree, protection.Evaluate(windows).ReasonKey);
        Assert.Equal(ProtectedPaths.ReasonSystemTree, protection.Evaluate(nested).ReasonKey);
    }

    [Fact]
    public void Evaluate_refuses_a_known_folder_but_allows_its_contents()
    {
        using var workspace = new TempWorkspace();
        string documents = workspace.CreateFolder("Documents");
        string project = Path.Combine(documents, "Projets");
        Directory.CreateDirectory(project);

        var protection = new ProtectedPaths(new FakeKnownFolders([documents], []), workspace.AppPaths);

        // The known folder itself is refused...
        Assert.Equal(ProtectedPaths.ReasonKnownFolder, protection.Evaluate(documents).ReasonKey);

        // ...but what it contains stays colorable, or the feature would lose its point.
        Assert.False(protection.Evaluate(project).IsProtected);
    }

    [Fact]
    public void Evaluate_refuses_our_own_workspace()
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        ProtectionResult result = protection.Evaluate(workspace.AppPaths.IconsDirectory);

        Assert.True(result.IsProtected);
        Assert.Equal(ProtectedPaths.ReasonApplicationData, result.ReasonKey);
    }

    [Fact]
    public void Evaluate_refuses_a_folder_that_does_not_exist()
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        string missing = Path.Combine(workspace.CreateFolder("parent"), "missing");

        Assert.Equal(ProtectedPaths.ReasonNotFound, protection.Evaluate(missing).ReasonKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_refuses_an_empty_path(string path)
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        Assert.Equal(ProtectedPaths.ReasonInvalidPath, protection.Evaluate(path).ReasonKey);
    }

    [Fact]
    public void Evaluate_accepts_an_ordinary_folder()
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        string folder = workspace.CreateFolder("Photos de vacances");

        Assert.False(protection.Evaluate(folder).IsProtected);
    }

    [Fact]
    public void Evaluate_refuses_a_junction_point()
    {
        using var workspace = new TempWorkspace();
        string target = workspace.CreateFolder("cible");
        string link = Path.Combine(workspace.CreateFolder("liens"), "jonction");

        if (!TryCreateJunction(link, target))
        {
            // Creating a junction may be forbidden by machine policy: do not turn that into a
            // test failure.
            return;
        }

        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        Assert.Equal(ProtectedPaths.ReasonReparsePoint, protection.Evaluate(link).ReasonKey);
    }

    private static bool TryCreateJunction(string link, string target)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{link}\" \"{target}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process is null)
            {
                return false;
            }

            process.WaitForExit(10_000);
            return process.ExitCode == 0 && Directory.Exists(link);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
