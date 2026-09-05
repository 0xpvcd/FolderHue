using System.Diagnostics;
using System.Globalization;
using FolderHue.Core.Folders;
using FolderHue.Core.Storage;

namespace FolderHue.Shell;

/// <summary>
/// Services shared by the menu commands, initialised lazily.
/// </summary>
/// <remarks>
/// Nothing expensive is built when the DLL loads: <c>GetTitle</c> and <c>GetIcon</c> run every time
/// the menu opens and must stay instant (CLAUDE.md 4.4). The exclusion list, which queries some
/// fifteen known folders, is only built on the first <c>Invoke</c>.
/// </remarks>
internal static class ShellServices
{
    private static readonly Lazy<FolderCustomizer> LazyCustomizer =
        new(FolderCustomizer.CreateDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Working locations. Pure path arithmetic, no disk access.</summary>
    internal static AppPaths Paths => AppPaths.Default;

    /// <summary>Diagnostic log.</summary>
    internal static Log Log => Log.Default;

    /// <summary>The folder customizer, built on first use.</summary>
    internal static FolderCustomizer Customizer => LazyCustomizer.Value;

    /// <summary>
    /// Applies a hue to the whole selection.
    /// </summary>
    /// <param name="paths">The selected folders.</param>
    /// <param name="colorId">The hue to apply.</param>
    /// <remarks>
    /// The emblem is left unspecified: applying a color must not erase the marker already in
    /// place.
    /// </remarks>
    internal static void ApplyColor(IReadOnlyList<string> paths, string colorId)
        => Run(paths, path => Customizer.Apply(path, colorId, null), colorId, AppCommands.Absent);

    /// <summary>
    /// Applies an emblem to the whole selection, keeping each folder's color.
    /// </summary>
    /// <param name="paths">The selected folders.</param>
    /// <param name="emblemId">The marker to place.</param>
    internal static void ApplyEmblem(IReadOnlyList<string> paths, string emblemId)
        => Run(
            paths,
            path => Customizer.Apply(path, Customizer.ResolveColorFor(path), emblemId),
            AppCommands.Absent,
            emblemId);

    /// <summary>
    /// Resets the whole selection.
    /// </summary>
    /// <param name="paths">The selected folders.</param>
    /// <remarks>
    /// Resetting consumes no icon, so it can never fail for want of pre-generation and has nothing
    /// to delegate to the application.
    /// </remarks>
    internal static void Reset(IReadOnlyList<string> paths)
        => Run(paths, Customizer.Reset, colorArgument: null, emblemArgument: null);

    /// <summary>
    /// Applies an operation to the whole selection and reports the failures.
    /// </summary>
    /// <param name="paths">The selected folders.</param>
    /// <param name="operation">The operation to apply to each one.</param>
    /// <param name="colorArgument">
    /// The hue to hand to the application for a retry, or <see langword="null"/> when the
    /// operation cannot be retried.
    /// </param>
    /// <param name="emblemArgument">The marker to hand over for a retry.</param>
    /// <remarks>
    /// This is where multiple selection (F4) plays out: a refusal on one folder never interrupts
    /// the processing of the others.
    /// </remarks>
    private static void Run(
        IReadOnlyList<string> paths,
        Func<string, OperationResult> operation,
        string? colorArgument,
        string? emblemArgument)
    {
        var missingIcon = new List<string>();
        int refused = 0;

        foreach (string path in paths)
        {
            OperationResult result = operation(path);

            if (result.Success)
            {
                continue;
            }

            if (result.ReasonKey == FolderCustomizer.ReasonIconMissing)
            {
                missingIcon.Add(path);
            }
            else
            {
                refused++;
            }
        }

        if (missingIcon.Count > 0)
        {
            Retry(colorArgument, emblemArgument, missingIcon);
        }

        if (refused > 0)
        {
            // Showing a dialog from inside explorer.exe is a risk: the application is what talks
            // to the user.
            LaunchApp(AppCommands.ReportSkipped, refused.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Hands the application the folders whose icon had not been generated yet.
    /// </summary>
    /// <param name="colorArgument">The requested hue, or <c>-</c> to keep the existing one.</param>
    /// <param name="emblemArgument">The requested marker, or <c>-</c> to keep the existing one.</param>
    /// <param name="paths">The folders to retry.</param>
    /// <remarks>
    /// The shell never generates an icon itself (CLAUDE.md 4.3). Running the pre-generation and
    /// leaving it at that is not enough either: the user's click would stay silent and without
    /// effect. The application regenerates what is missing <b>and then applies</b> the requested
    /// action, outside Explorer's process.
    /// </remarks>
    private static void Retry(string? colorArgument, string? emblemArgument, IReadOnlyList<string> paths)
    {
        if (colorArgument is null || emblemArgument is null)
        {
            // Not retryable: just put the palette back in order.
            LaunchApp(AppCommands.Pregenerate);
            return;
        }

        var arguments = new List<string>(paths.Count + 3) { AppCommands.Apply, colorArgument, emblemArgument };
        arguments.AddRange(paths);

        LaunchApp([.. arguments]);
    }

    /// <summary>
    /// Launches the settings application, which is deployed next to the DLL.
    /// </summary>
    /// <param name="arguments">The command line arguments.</param>
    private static void LaunchApp(params string[] arguments)
    {
        try
        {
            // Emphatically not AppContext.BaseDirectory: loaded inside a host process it returns
            // C:\Windows\System32, and the application would never be found.
            string? directory = NativeMethods.GetModuleDirectory();

            if (directory is null)
            {
                Log.Warn("The DLL directory could not be determined.");
                return;
            }

            string executable = Path.Combine(directory, "FolderHue.App.exe");

            if (!File.Exists(executable))
            {
                Log.Warn($"The application was not found next to the DLL: \"{executable}\".");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process.Start(startInfo)?.Dispose();
        }
        catch (Exception e)
        {
            Log.Error("Could not launch FolderHue.App.", e);
        }
    }
}
