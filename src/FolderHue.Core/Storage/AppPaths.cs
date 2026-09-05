namespace FolderHue.Core.Storage;

/// <summary>
/// Resolves the application's working locations under <c>%LOCALAPPDATA%\FolderHue</c>.
/// </summary>
/// <remarks>
/// This is the only place in the file system the application writes to, apart from the folders the
/// user explicitly selects (CLAUDE.md 11).
/// <para>
/// The class is instantiable so that tests can work in a disposable temporary directory rather
/// than in the real profile (CLAUDE.md 8).
/// </para>
/// </remarks>
public sealed class AppPaths
{
    /// <summary>Name of the application folder under <c>%LOCALAPPDATA%</c>.</summary>
    public const string FolderName = "FolderHue";

    /// <summary>
    /// Environment variable that forces the root, used by the tests.
    /// </summary>
    public const string RootEnvironmentVariable = "FOLDERHUE_ROOT";

    /// <summary>Creates a set of paths rooted at a given directory.</summary>
    /// <param name="root">Data root, absolute. It does not need to exist.</param>
    /// <exception cref="ArgumentException"><paramref name="root"/> is empty.</exception>
    /// <remarks>
    /// The install directory is derived from the running executable. The application lives wherever
    /// the installer put it, not at an imposed location, so the executable is what tells the truth.
    /// </remarks>
    public AppPaths(string root)
        : this(root, AppContext.BaseDirectory)
    {
    }

    /// <summary>Creates a set of paths, also fixing the install directory.</summary>
    /// <param name="root">Data root, absolute. It does not need to exist.</param>
    /// <param name="installDirectory">Directory holding the application and the shell DLL.</param>
    /// <exception cref="ArgumentException">Either path is empty.</exception>
    public AppPaths(string root, string installDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentException.ThrowIfNullOrEmpty(installDirectory);

        InstallDirectory = Path.GetFullPath(installDirectory);
        Root = Path.GetFullPath(root);
        IconsDirectory = Path.Combine(Root, "icons");
        BaseDirectory = Path.Combine(Root, "base");
        LogsDirectory = Path.Combine(Root, "logs");
        JournalFile = Path.Combine(Root, "applied.json");
        BaseIconFile = Path.Combine(BaseDirectory, "folder-base.ico");
        IconLibraryStampFile = Path.Combine(IconsDirectory, ".version");
        LogFile = Path.Combine(LogsDirectory, "folderhue.log");
    }

    /// <summary>The real paths of the current machine.</summary>
    public static AppPaths Default { get; } = new(ResolveDefaultRoot());

    /// <summary>Application root, typically <c>%LOCALAPPDATA%\FolderHue</c>.</summary>
    public string Root { get; }

    /// <summary>
    /// Directory where the installer placed the application and <c>FolderHue.Shell.dll</c>.
    /// </summary>
    /// <remarks>
    /// Typically <c>%LOCALAPPDATA%\Programs\FolderHue</c>, but the user may pick another one: the
    /// value is derived from the running executable, never assumed.
    /// </remarks>
    public string InstallDirectory { get; }

    /// <summary>Directory of pre-generated <c>.ico</c> files, one per color + emblem pair.</summary>
    public string IconsDirectory { get; }

    /// <summary>Directory of the icon template extracted from the machine's shell.</summary>
    public string BaseDirectory { get; }

    /// <summary>Directory of the diagnostic logs.</summary>
    public string LogsDirectory { get; }

    /// <summary>The <c>applied.json</c> file: the record of what we changed.</summary>
    public string JournalFile { get; }

    /// <summary>Neutral folder icon template used as the basis for coloring.</summary>
    public string BaseIconFile { get; }

    /// <summary>Version stamp of the icon library, so it is not regenerated needlessly.</summary>
    public string IconLibraryStampFile { get; }

    /// <summary>The current log file.</summary>
    public string LogFile { get; }

    /// <summary>Absolute path of the <c>.ico</c> for a color + emblem pair.</summary>
    /// <param name="colorId">Color identifier.</param>
    /// <param name="emblemId">Emblem identifier, or <see langword="null"/> for none.</param>
    /// <returns>An absolute path, whether the file exists or not.</returns>
    public string IconPath(string colorId, string? emblemId)
        => Path.Combine(IconsDirectory, Palette.PaletteCatalog.IconFileName(colorId, emblemId));

    /// <summary>Absolute path of an emblem's menu chip.</summary>
    /// <param name="emblemId">Emblem identifier.</param>
    /// <returns>An absolute path, whether the file exists or not.</returns>
    public string EmblemChipPath(string emblemId)
        => Path.Combine(IconsDirectory, Palette.PaletteCatalog.EmblemChipFileName(emblemId));

    /// <summary>Absolute path of the application logo, in the brand colors.</summary>
    /// <returns>An absolute path, whether the file exists or not.</returns>
    public string BrandLogoPath => Path.Combine(IconsDirectory, Palette.PaletteCatalog.BrandLogoFileName);

    /// <summary>Absolute path of the logo tinted with one palette color.</summary>
    /// <param name="colorId">Color identifier.</param>
    /// <returns>An absolute path, whether the file exists or not.</returns>
    public string LogoPath(string colorId)
        => Path.Combine(IconsDirectory, Palette.PaletteCatalog.LogoFileName(colorId));

    /// <summary>Creates the working directories when they do not exist.</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(IconsDirectory);
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    /// <summary>
    /// Indicates whether a path belongs to FolderHue itself.
    /// </summary>
    /// <param name="path">Path to test, absolute or relative.</param>
    /// <returns>
    /// <see langword="true"/> when the path is, or sits under, the data root or the install
    /// directory.
    /// </returns>
    /// <remarks>
    /// Used to forbid coloring our own directories (CLAUDE.md 6.2). The install directory counts
    /// just as much as the data one: dropping a <c>desktop.ini</c> and a read-only attribute in
    /// there would get in the way of uninstalling, for nothing.
    /// </remarks>
    public bool ContainsPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return IsAtOrUnder(full, Root) || IsAtOrUnder(full, InstallDirectory);
    }

    private static bool IsAtOrUnder(string candidate, string directory)
    {
        string trimmed = directory.TrimEnd(Path.DirectorySeparatorChar);

        return candidate.Equals(trimmed, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(trimmed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveDefaultRoot()
    {
        string? forced = Environment.GetEnvironmentVariable(RootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(forced))
        {
            return forced;
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(localAppData, FolderName);
    }
}
