using System.Diagnostics;
using System.Globalization;
using FolderHue.Core.Folders;
using FolderHue.Core.Storage;

namespace FolderHue.Shell;

/// <summary>
/// Services partages par les commandes du menu, initialises paresseusement.
/// </summary>
/// <remarks>
/// Rien de couteux n'est construit au chargement de la DLL : <c>GetTitle</c> et <c>GetIcon</c> sont
/// appeles a chaque ouverture du menu et doivent rester instantanes (CLAUDE.md §4.4). La liste
/// d'exclusion, qui interroge une quinzaine de dossiers connus, n'est construite qu'au premier
/// <c>Invoke</c>.
/// </remarks>
internal static class ShellServices
{
    private static readonly Lazy<FolderCustomizer> LazyCustomizer =
        new(FolderCustomizer.CreateDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Emplacements de travail. Simple calcul de chemins, sans acces disque.</summary>
    internal static AppPaths Paths => AppPaths.Default;

    /// <summary>Journal de diagnostic.</summary>
    internal static Log Log => Log.Default;

    /// <summary>Le personnalisateur de dossiers, construit au premier usage.</summary>
    internal static FolderCustomizer Customizer => LazyCustomizer.Value;

    /// <summary>
    /// Applique une teinte a toute la selection.
    /// </summary>
    /// <param name="paths">Les dossiers selectionnes.</param>
    /// <param name="colorId">La teinte a appliquer.</param>
    /// <remarks>
    /// L'embleme n'est pas precise : appliquer une couleur ne doit pas effacer le marqueur en
    /// place.
    /// </remarks>
    internal static void ApplyColor(IReadOnlyList<string> paths, string colorId)
        => Run(paths, path => Customizer.Apply(path, colorId, null), colorId, AppCommands.Absent);

    /// <summary>
    /// Applique un embleme a toute la selection, en conservant la couleur de chaque dossier.
    /// </summary>
    /// <param name="paths">Les dossiers selectionnes.</param>
    /// <param name="emblemId">Le marqueur a poser.</param>
    internal static void ApplyEmblem(IReadOnlyList<string> paths, string emblemId)
        => Run(
            paths,
            path => Customizer.Apply(path, Customizer.ResolveColorFor(path), emblemId),
            AppCommands.Absent,
            emblemId);

    /// <summary>
    /// Reinitialise toute la selection.
    /// </summary>
    /// <param name="paths">Les dossiers selectionnes.</param>
    /// <remarks>
    /// La reinitialisation ne consomme aucune icone : elle ne peut donc jamais echouer faute de
    /// pre-generation, et n'a rien a deleguer a l'application.
    /// </remarks>
    internal static void Reset(IReadOnlyList<string> paths)
        => Run(paths, Customizer.Reset, colorArgument: null, emblemArgument: null);

    /// <summary>
    /// Applique une operation a toute la selection et rend compte des echecs.
    /// </summary>
    /// <param name="paths">Les dossiers selectionnes.</param>
    /// <param name="operation">L'operation a appliquer a chacun.</param>
    /// <param name="colorArgument">
    /// La teinte a transmettre a l'application pour une reprise, ou <see langword="null"/> si
    /// l'operation n'est pas reprenable.
    /// </param>
    /// <param name="emblemArgument">Le marqueur a transmettre pour une reprise.</param>
    /// <remarks>
    /// C'est ici que se joue la selection multiple (F4) : un refus sur un dossier n'interrompt
    /// jamais le traitement des autres.
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
            // Une boite de dialogue affichee depuis explorer.exe est un risque : c'est
            // l'application qui parle a l'utilisateur.
            LaunchApp(AppCommands.ReportSkipped, refused.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Confie a l'application les dossiers dont l'icone n'etait pas encore generee.
    /// </summary>
    /// <param name="colorArgument">La teinte demandee, ou <c>-</c> pour conserver l'existante.</param>
    /// <param name="emblemArgument">Le marqueur demande, ou <c>-</c> pour conserver l'existant.</param>
    /// <param name="paths">Les dossiers a reprendre.</param>
    /// <remarks>
    /// Le shell ne genere jamais d'icone lui-meme (CLAUDE.md §4.3). Il ne suffit pas pour autant
    /// de lancer la pre-generation et d'en rester la : le clic de l'utilisateur resterait sans
    /// effet et sans message. L'application regenere ce qui manque <b>puis applique</b> l'action
    /// demandee, hors du processus de l'Explorateur.
    /// </remarks>
    private static void Retry(string? colorArgument, string? emblemArgument, IReadOnlyList<string> paths)
    {
        if (colorArgument is null || emblemArgument is null)
        {
            // Operation non reprenable : on se contente de remettre la palette d'aplomb.
            LaunchApp(AppCommands.Pregenerate);
            return;
        }

        var arguments = new List<string>(paths.Count + 3) { AppCommands.Apply, colorArgument, emblemArgument };
        arguments.AddRange(paths);

        LaunchApp([.. arguments]);
    }

    /// <summary>
    /// Lance l'application de reglages, qui est deployee a cote de la DLL.
    /// </summary>
    /// <param name="arguments">Les arguments de ligne de commande.</param>
    private static void LaunchApp(params string[] arguments)
    {
        try
        {
            // Surtout pas AppContext.BaseDirectory : charge dans un processus hote, il rend
            // C:\Windows\System32 et l'application ne serait jamais trouvee.
            string? directory = NativeMethods.GetModuleDirectory();

            if (directory is null)
            {
                Log.Warn("Le dossier de la DLL n'a pas pu etre determine.");
                return;
            }

            string executable = Path.Combine(directory, "FolderHue.App.exe");

            if (!File.Exists(executable))
            {
                Log.Warn($"L'application est introuvable a cote de la DLL : « {executable} ».");
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
            Log.Error("Impossible de lancer FolderHue.App.", e);
        }
    }
}
