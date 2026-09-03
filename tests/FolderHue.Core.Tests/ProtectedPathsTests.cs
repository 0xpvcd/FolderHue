using System.Diagnostics;
using FolderHue.Core.Folders;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Verifie la liste d'exclusion, qui n'est pas negociable (CLAUDE.md §6.2).
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
    public void Evaluate_RefuseLaRacineDUnVolume()
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        ProtectionResult result = protection.Evaluate(@"C:\");

        Assert.True(result.IsProtected);
        Assert.Equal(ProtectedPaths.ReasonVolumeRoot, result.ReasonKey);
    }

    [Fact]
    public void Evaluate_RefuseLaRacineDUnPartageReseau()
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        ProtectionResult result = protection.Evaluate(@"\\serveur\partage");

        Assert.True(result.IsProtected);
        Assert.Equal(ProtectedPaths.ReasonVolumeRoot, result.ReasonKey);
    }

    [Fact]
    public void Evaluate_RefuseToutSousUneArborescenceSysteme()
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
    public void Evaluate_RefuseUnDossierConnuMaisAutoriseSonContenu()
    {
        using var workspace = new TempWorkspace();
        string documents = workspace.CreateFolder("Documents");
        string project = Path.Combine(documents, "Projets");
        Directory.CreateDirectory(project);

        var protection = new ProtectedPaths(new FakeKnownFolders([documents], []), workspace.AppPaths);

        // Le dossier connu lui-meme est refuse...
        Assert.Equal(ProtectedPaths.ReasonKnownFolder, protection.Evaluate(documents).ReasonKey);

        // ...mais son contenu reste colorisable, sinon la fonctionnalite perdrait son interet.
        Assert.False(protection.Evaluate(project).IsProtected);
    }

    [Fact]
    public void Evaluate_RefuseNotrePropreEspaceDeTravail()
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        ProtectionResult result = protection.Evaluate(workspace.AppPaths.IconsDirectory);

        Assert.True(result.IsProtected);
        Assert.Equal(ProtectedPaths.ReasonApplicationData, result.ReasonKey);
    }

    [Fact]
    public void Evaluate_RefuseUnDossierInexistant()
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        string missing = Path.Combine(workspace.CreateFolder("parent"), "absent");

        Assert.Equal(ProtectedPaths.ReasonNotFound, protection.Evaluate(missing).ReasonKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_RefuseUnCheminVide(string path)
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        Assert.Equal(ProtectedPaths.ReasonInvalidPath, protection.Evaluate(path).ReasonKey);
    }

    [Fact]
    public void Evaluate_AccepteUnDossierOrdinaire()
    {
        using var workspace = new TempWorkspace();
        var protection = new ProtectedPaths(new FakeKnownFolders([], []), workspace.AppPaths);

        string folder = workspace.CreateFolder("Photos de vacances");

        Assert.False(protection.Evaluate(folder).IsProtected);
    }

    [Fact]
    public void Evaluate_RefuseUnePointDeJonction()
    {
        using var workspace = new TempWorkspace();
        string target = workspace.CreateFolder("cible");
        string link = Path.Combine(workspace.CreateFolder("liens"), "jonction");

        if (!TryCreateJunction(link, target))
        {
            // La creation de jonction peut etre interdite par la strategie de la machine :
            // on ne transforme pas cela en echec de test.
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
