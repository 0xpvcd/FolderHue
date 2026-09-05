using Microsoft.Win32;

namespace FolderHue.Core.Storage;

/// <summary>
/// Registers and removes the Explorer context menu verb under <c>HKEY_CURRENT_USER</c>.
/// </summary>
/// <remarks>
/// FolderHue is a classic, unpackaged shell extension: the shell finds it through the registry,
/// not through a package manifest. Two keys are involved, and both are removed on uninstall.
/// <list type="number">
///   <item>
///     <description>
///     <c>CLSID\{…}\InprocServer32</c> points at <c>FolderHue.Shell.dll</c>. The shell loads it
///     <b>inside explorer.exe</b>, which is why that DLL is compiled with NativeAOT.
///     </description>
///   </item>
///   <item>
///     <description>
///     <c>Directory\shell\FolderHue</c> declares the verb itself and hands the shell the CLSID
///     through <c>ExplorerCommandHandler</c>. The command answers <c>ECF_HASSUBCOMMANDS</c>, so
///     the shell asks it for the palette through <c>IEnumExplorerCommand</c>.
///     </description>
///   </item>
/// </list>
/// <para>
/// Writing under <c>Software\Classes</c> is the one place FolderHue steps outside
/// <c>Software\FolderHue</c>. It is the per-user half of <c>HKEY_CLASSES_ROOT</c>: no elevation,
/// no effect on other accounts, and nothing left behind once <see cref="Unregister"/> has run.
/// Nothing is ever written to <c>HKEY_LOCAL_MACHINE</c>.
/// </para>
/// </remarks>
public sealed class ShellRegistration
{
    /// <summary>
    /// CLSID of the context menu COM server.
    /// </summary>
    /// <remarks>
    /// <b>Single source of truth.</b> <c>FolderHue.Shell.Com.Guids</c> derives its value from this
    /// constant rather than repeating it: the registry and the COM server must agree to the
    /// character, and a mismatch shows up as a menu entry that simply never appears.
    /// </remarks>
    public const string ClassIdText = "C228C2F8-706B-4A2E-9C48-74F3062BE146";

    /// <summary>Name of the verb key, and the label shown before the handler is loaded.</summary>
    public const string VerbName = "FolderHue";

    /// <summary>File name of the shell extension, as it sits next to the application.</summary>
    public const string LibraryFileName = "FolderHue.Shell.dll";

    /// <summary>Default registry location of the per-user class registrations.</summary>
    public const string DefaultClassesPath = @"Software\Classes";

    private readonly string _classesPath;

    /// <summary>Creates a registration targeting the per-user class store.</summary>
    public ShellRegistration()
        : this(DefaultClassesPath)
    {
    }

    /// <summary>Creates a registration targeting an arbitrary key under <c>HKEY_CURRENT_USER</c>.</summary>
    /// <param name="classesPath">
    /// Path of the class store, relative to <c>HKEY_CURRENT_USER</c>. Tests pass a throwaway key
    /// so that they never touch the real shell registration.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="classesPath"/> is empty.</exception>
    public ShellRegistration(string classesPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(classesPath);
        _classesPath = classesPath.Trim('\\');
    }

    /// <summary>Registry path of the COM server key, relative to <c>HKEY_CURRENT_USER</c>.</summary>
    public string ClassIdKeyPath => $@"{_classesPath}\CLSID\{{{ClassIdText}}}";

    /// <summary>Registry path of the verb key, relative to <c>HKEY_CURRENT_USER</c>.</summary>
    public string VerbKeyPath => $@"{_classesPath}\Directory\shell\{VerbName}";

    /// <summary>Declares the context menu so that Explorer picks it up on its next start.</summary>
    /// <param name="shellLibraryPath">Absolute path of <c>FolderHue.Shell.dll</c>.</param>
    /// <param name="iconPath">Absolute path of the icon shown next to the root entry.</param>
    /// <exception cref="ArgumentException">A path is empty.</exception>
    /// <remarks>
    /// Explorer caches shell extensions: the entry only appears once <c>explorer.exe</c> has been
    /// restarted. The installer does that; see <c>scripts/build.ps1</c>.
    /// </remarks>
    public void Register(string shellLibraryPath, string iconPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(shellLibraryPath);
        ArgumentException.ThrowIfNullOrEmpty(iconPath);

        using (RegistryKey server = Registry.CurrentUser.CreateSubKey($@"{ClassIdKeyPath}\InprocServer32"))
        {
            server.SetValue(null, shellLibraryPath, RegistryValueKind.String);

            // The command runs on the thread that opened the menu, which is an STA.
            server.SetValue("ThreadingModel", "Apartment", RegistryValueKind.String);
        }

        using (RegistryKey clsid = Registry.CurrentUser.CreateSubKey(ClassIdKeyPath))
        {
            clsid.SetValue(null, VerbName, RegistryValueKind.String);
        }

        using RegistryKey verb = Registry.CurrentUser.CreateSubKey(VerbKeyPath);

        // Label used before the handler is loaded; IExplorerCommand.GetTitle supplies the
        // localized one afterwards.
        verb.SetValue(null, VerbName, RegistryValueKind.String);
        verb.SetValue("ExplorerCommandHandler", $"{{{ClassIdText}}}", RegistryValueKind.String);
        verb.SetValue("Icon", iconPath, RegistryValueKind.String);

        // "Player" asks the shell to invoke the verb once for the whole selection, rather than
        // once per folder. Applying a colour to five folders must stay a single operation.
        verb.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);
    }

    /// <summary>Removes every key <see cref="Register"/> creates.</summary>
    /// <returns><see langword="true"/> if at least one key was present and removed.</returns>
    /// <remarks>
    /// Deliberately narrow: only the two keys FolderHue owns are touched, never the trees that
    /// contain them.
    /// </remarks>
    public bool Unregister()
    {
        bool removed = DeleteTree(VerbKeyPath);
        removed |= DeleteTree(ClassIdKeyPath);
        return removed;
    }

    /// <summary>Indicates whether the verb is currently declared.</summary>
    /// <returns><see langword="true"/> if both keys exist and point at an existing library.</returns>
    public bool IsRegistered()
    {
        using RegistryKey? server = Registry.CurrentUser.OpenSubKey($@"{ClassIdKeyPath}\InprocServer32");
        if (server?.GetValue(null) is not string library || !File.Exists(library))
        {
            return false;
        }

        using RegistryKey? verb = Registry.CurrentUser.OpenSubKey(VerbKeyPath);
        return verb?.GetValue("ExplorerCommandHandler") is string handler
            && handler.Trim('{', '}').Equals(ClassIdText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DeleteTree(string path)
    {
        try
        {
            using RegistryKey? probe = Registry.CurrentUser.OpenSubKey(path);
            if (probe is null)
            {
                return false;
            }
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }

        Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        return true;
    }
}
