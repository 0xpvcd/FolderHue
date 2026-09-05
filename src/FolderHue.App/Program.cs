using System.Globalization;
using System.Runtime.Versioning;
using FolderHue.App.Icons;
using FolderHue.Core.Folders;
using FolderHue.Core.Resources;
using FolderHue.Core.Storage;

namespace FolderHue.App;

/// <summary>
/// Entry point of the settings application.
/// </summary>
/// <remarks>
/// The executable plays three parts: the settings window when launched with no argument, the icon
/// generator the installer calls, and the shell's spokesman - the shell cannot safely show a
/// dialog from inside <c>explorer.exe</c> (CLAUDE.md 6.5).
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0)
            {
                return RunCommand(args);
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
            return 0;
        }
        catch (Exception e)
        {
            Log.Default.Error("Unexpected failure in FolderHue.App.", e);
            return 1;
        }
    }

    private static int RunCommand(string[] args)
    {
        switch (args[0].ToLowerInvariant())
        {
            case AppCommands.Pregenerate:
                bool force = args.Any(a => string.Equals(a, AppCommands.Force, StringComparison.OrdinalIgnoreCase));
                int written = IconLibrary.CreateDefault().EnsureAll(force);
                Console.Out.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{written} icon(s) generated in {AppPaths.Default.IconsDirectory}"));
                return 0;

            case AppCommands.ResetAll:
                return ResetAll();

            case AppCommands.Apply:
                return ApplyFromCommandLine(args);

            case AppCommands.ReportSkipped:
                ReportSkipped(args.Length > 1 ? args[1] : "0");
                return 0;

            case AppCommands.Register:
                return Register();

            case AppCommands.Unregister:
                return Unregister();

            case AppCommands.ExportIcon:
                if (args.Length < 2)
                {
                    Console.Error.WriteLine($"Usage: {AppCommands.ExportIcon} <.ico file>");
                    return 2;
                }

                return ExportIcon(args[1]);

            default:
                Console.Error.WriteLine($"Unknown argument: {args[0]}");
                return 2;
        }
    }

    /// <summary>
    /// Regenerates the icon library, then applies the operation the shell asked for.
    /// </summary>
    /// <param name="args">
    /// <c>--apply &lt;color&gt; &lt;emblem&gt; &lt;folder&gt;…</c>, where
    /// <see cref="AppCommands.Absent"/> stands for an unspecified value.
    /// </param>
    /// <returns>0 when every folder succeeded.</returns>
    /// <remarks>
    /// The context menu takes this path when an icon is missing. The shell never generates an icon
    /// itself (CLAUDE.md 4.3); merely running the pre-generation would leave the user's click
    /// silent and without effect. This is therefore where the action is picked up, outside
    /// Explorer's process.
    /// </remarks>
    private static int ApplyFromCommandLine(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine(
                $"Usage: {AppCommands.Apply} <color|{AppCommands.Absent}> " +
                $"<emblem|{AppCommands.Absent}> <folder> [folder...]");
            return 2;
        }

        string? colorId = args[1] == AppCommands.Absent ? null : args[1];
        string? emblemId = args[2] == AppCommands.Absent ? null : args[2];

        IconLibrary.CreateDefault().EnsureAll();

        FolderCustomizer customizer = FolderCustomizer.CreateDefault();
        int refused = 0;

        for (int i = 3; i < args.Length; i++)
        {
            string path = args[i];
            OperationResult result = customizer.Apply(
                path, colorId ?? customizer.ResolveColorFor(path), emblemId);

            if (!result.Success)
            {
                refused++;
                Console.Error.WriteLine($"{path} : {Loc.Get(result.ReasonKey ?? string.Empty)}");
            }
        }

        if (refused > 0)
        {
            ReportSkipped(refused.ToString(CultureInfo.InvariantCulture));
        }

        return refused == 0 ? 0 : 1;
    }

    /// <summary>
    /// Declares the context menu for the current user.
    /// </summary>
    /// <returns>0 when the declaration succeeded.</returns>
    /// <remarks>
    /// The DLL and the logo are looked for next to the executable: the installer decides where the
    /// application lives, not us. The logo may be missing if the palette was never generated, so it
    /// is produced before the keys are written - a menu entry with no chip looks broken.
    /// </remarks>
    private static int Register()
    {
        string directory = AppContext.BaseDirectory;
        string library = Path.Combine(directory, ShellRegistration.LibraryFileName);

        if (!File.Exists(library))
        {
            Console.Error.WriteLine($"{ShellRegistration.LibraryFileName} was not found next to the application: {directory}");
            return 1;
        }

        IconLibrary.CreateDefault().EnsureAll();

        var registration = new ShellRegistration();
        registration.Register(library, AppPaths.Default.BrandLogoPath);

        Log.Default.Info($"Context menu registered: \"{library}\".");
        Console.Out.WriteLine($"Context menu registered under HKCU\\{registration.VerbKeyPath}");
        return 0;
    }

    /// <summary>
    /// Writes the brand logo as an <c>.ico</c> to the requested location.
    /// </summary>
    /// <param name="destination">Path of the file to produce. Its directory is created if needed.</param>
    /// <returns>0 when the file was written.</returns>
    private static int ExportIcon(string destination)
    {
        IconLibrary.CreateDefault().EnsureAll();

        string source = AppPaths.Default.BrandLogoPath;
        if (!File.Exists(source))
        {
            Console.Error.WriteLine($"The logo has not been generated: {source}");
            return 1;
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(destination));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(source, destination, overwrite: true);
        Console.Out.WriteLine($"Logo exported: {destination}");
        return 0;
    }

    /// <summary>
    /// Removes the context menu declaration from the registry.
    /// </summary>
    /// <returns>0 in every case: an uninstall must never fail on this point.</returns>
    /// <remarks>
    /// No user folder is touched: the uninstaller separately offers to reset the colored folders
    /// (CLAUDE.md 6.6).
    /// </remarks>
    private static int Unregister()
    {
        bool removed = new ShellRegistration().Unregister();
        Log.Default.Info(removed ? "Context menu removed from the registry." : "Context menu was already absent from the registry.");
        Console.Out.WriteLine(removed ? "Context menu removed." : "Context menu already absent.");
        return 0;
    }

    /// <summary>
    /// Resets every folder the journal knows about.
    /// </summary>
    /// <returns>0 when everything succeeded.</returns>
    /// <remarks>
    /// No user folder or file is deleted: only our own modifications are removed (CLAUDE.md 6.6).
    /// </remarks>
    private static int ResetAll()
    {
        FolderCustomizer customizer = FolderCustomizer.CreateDefault();
        int failures = 0;

        foreach (AppliedEntry entry in customizer.Journal.ReadAll().ToArray())
        {
            OperationResult result = customizer.Reset(entry.Path);
            if (!result.Success)
            {
                failures++;
                Console.Error.WriteLine($"{entry.Path} : {Loc.Get(result.ReasonKey ?? string.Empty)}");
            }
        }

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Shows the message explaining that some folders were refused.
    /// </summary>
    /// <param name="rawCount">How many folders were refused, as the shell passed it.</param>
    private static void ReportSkipped(string rawCount)
    {
        if (!int.TryParse(rawCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || count <= 0)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        MessageBox.Show(
            Loc.Format("App_SkippedFolders", count),
            Loc.Get("App_Name"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
