using FolderHue.Core.Storage;
using Microsoft.Win32;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Covers the registration of the context menu in the registry.
/// </summary>
/// <remarks>
/// These tests genuinely write to <c>HKEY_CURRENT_USER</c>, but never under
/// <c>Software\Classes</c>: each gives itself a throwaway tree beneath
/// <c>Software\FolderHue\Tests</c> and deletes it on the way out. The machine's real registration
/// is therefore neither read nor modified, which stops a test from making the menu entry disappear
/// for whoever runs it.
/// </remarks>
public sealed class ShellRegistrationTests : IDisposable
{
    private readonly string _classesPath =
        $@"Software\FolderHue\Tests\{Guid.NewGuid():N}\Classes";

    private readonly ShellRegistration _registration;
    private readonly string _library;
    private readonly string _icon;
    private readonly string _workspace;

    public ShellRegistrationTests()
    {
        _registration = new ShellRegistration(_classesPath);

        _workspace = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspace);

        _library = Path.Combine(_workspace, ShellRegistration.LibraryFileName);
        _icon = Path.Combine(_workspace, "logo.ico");
        File.WriteAllBytes(_library, [0x4D, 0x5A]);
        File.WriteAllBytes(_icon, [0x00, 0x00, 0x01, 0x00]);
    }

    [Fact]
    public void Register_declares_the_com_server_in_process()
    {
        _registration.Register(_library, _icon);

        using RegistryKey? server =
            Registry.CurrentUser.OpenSubKey($@"{_registration.ClassIdKeyPath}\InprocServer32");

        Assert.NotNull(server);
        Assert.Equal(_library, server.GetValue(null));
        Assert.Equal("Apartment", server.GetValue("ThreadingModel"));
    }

    [Fact]
    public void Register_points_the_verb_at_the_command_handler()
    {
        _registration.Register(_library, _icon);

        using RegistryKey? verb = Registry.CurrentUser.OpenSubKey(_registration.VerbKeyPath);

        Assert.NotNull(verb);
        Assert.Equal($"{{{ShellRegistration.ClassIdText}}}", verb.GetValue("ExplorerCommandHandler"));
        Assert.Equal(_icon, verb.GetValue("Icon"));
    }

    /// <summary>
    /// Without <c>MultiSelectModel</c> the shell invokes the verb once per selected folder:
    /// coloring five folders would start five operations instead of one (feature F4).
    /// </summary>
    [Fact]
    public void Register_asks_for_a_single_invoke_on_a_multiple_selection()
    {
        _registration.Register(_library, _icon);

        using RegistryKey? verb = Registry.CurrentUser.OpenSubKey(_registration.VerbKeyPath);

        Assert.Equal("Player", verb!.GetValue("MultiSelectModel"));
    }

    [Fact]
    public void IsRegistered_follows_the_registration()
    {
        Assert.False(_registration.IsRegistered());

        _registration.Register(_library, _icon);
        Assert.True(_registration.IsRegistered());

        _registration.Unregister();
        Assert.False(_registration.IsRegistered());
    }

    /// <summary>
    /// A DLL that is declared but missing from disk does not count as a registration: that is the
    /// state an incomplete uninstall leaves, and it must lead to re-registering rather than to
    /// assuming all is well.
    /// </summary>
    [Fact]
    public void IsRegistered_rejects_a_registration_pointing_at_a_missing_library()
    {
        _registration.Register(_library, _icon);
        File.Delete(_library);

        Assert.False(_registration.IsRegistered());
    }

    [Fact]
    public void Unregister_removes_both_keys()
    {
        _registration.Register(_library, _icon);

        Assert.True(_registration.Unregister());

        Assert.Null(Registry.CurrentUser.OpenSubKey(_registration.VerbKeyPath));
        Assert.Null(Registry.CurrentUser.OpenSubKey(_registration.ClassIdKeyPath));
    }

    /// <summary>An uninstall that replays the removal must not fail.</summary>
    [Fact]
    public void Unregister_is_harmless_when_nothing_is_declared()
    {
        Assert.False(_registration.Unregister());
        Assert.False(_registration.Unregister());
    }

    /// <summary>
    /// The CLSID is shared with the COM server: were it to change here without changing there,
    /// the entry would vanish from the menu without a single message (CLAUDE.md 10).
    /// </summary>
    [Fact]
    public void The_class_id_is_a_well_formed_guid()
    {
        Assert.True(Guid.TryParse(ShellRegistration.ClassIdText, out Guid parsed));
        Assert.Equal(ShellRegistration.ClassIdText, parsed.ToString("D").ToUpperInvariant());
    }

    public void Dispose()
    {
        try
        {
            // Delete the whole test tree, not just the two keys that were written.
            string root = _classesPath[.._classesPath.LastIndexOf('\\')];
            Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A leftover key under Software\FolderHue\Tests must not fail a test.
        }

        try
        {
            Directory.Delete(_workspace, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
