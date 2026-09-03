using System.Globalization;
using System.Runtime.Versioning;
using FolderHue.App.Icons;
using FolderHue.Core.Folders;
using FolderHue.Core.Resources;
using FolderHue.Core.Storage;

namespace FolderHue.App;

/// <summary>
/// Point d'entree de l'application de reglages.
/// </summary>
/// <remarks>
/// L'executable joue trois roles : l'interface de reglages quand il est lance sans argument, le
/// generateur d'icones appele par l'installation, et le porte-parole du shell — qui ne peut pas
/// afficher de boite de dialogue depuis <c>explorer.exe</c> sans risque (CLAUDE.md §6.5).
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
            Log.Default.Error("Echec inattendu de FolderHue.App.", e);
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
                    $"{written} icone(s) generee(s) dans {AppPaths.Default.IconsDirectory}"));
                return 0;

            case AppCommands.ResetAll:
                return ResetAll();

            case AppCommands.Apply:
                return ApplyFromCommandLine(args);

            case AppCommands.ReportSkipped:
                ReportSkipped(args.Length > 1 ? args[1] : "0");
                return 0;

            case AppCommands.GeneratePackageAssets:
                if (args.Length < 2)
                {
                    Console.Error.WriteLine($"Usage : {AppCommands.GeneratePackageAssets} <dossier de sortie>");
                    return 2;
                }

                PackageAssets.Generate(args[1]);
                return 0;

            default:
                Console.Error.WriteLine($"Argument inconnu : {args[0]}");
                return 2;
        }
    }

    /// <summary>
    /// Regenere la bibliotheque d'icones puis applique l'operation demandee par le shell.
    /// </summary>
    /// <param name="args">
    /// <c>--apply &lt;couleur&gt; &lt;embleme&gt; &lt;dossier&gt;…</c>, ou
    /// <see cref="AppCommands.Absent"/> tient lieu de valeur non precisee.
    /// </param>
    /// <returns>0 si tous les dossiers ont abouti.</returns>
    /// <remarks>
    /// Le menu contextuel appelle ce chemin quand une icone manque. Le shell ne genere jamais
    /// d'icone lui-meme (CLAUDE.md §4.3) ; se contenter de lancer la pre-generation laisserait le
    /// clic de l'utilisateur sans effet et sans message. C'est donc ici que l'action est reprise,
    /// hors du processus de l'Explorateur.
    /// </remarks>
    private static int ApplyFromCommandLine(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine(
                $"Usage : {AppCommands.Apply} <couleur|{AppCommands.Absent}> " +
                $"<embleme|{AppCommands.Absent}> <dossier> [dossier...]");
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
    /// Reinitialise tous les dossiers connus du journal.
    /// </summary>
    /// <returns>0 si tout a abouti.</returns>
    /// <remarks>
    /// Aucun dossier ni fichier utilisateur n'est supprime : seules nos propres modifications sont
    /// retirees (CLAUDE.md §6.6).
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
    /// Affiche le message expliquant que des dossiers ont ete refuses.
    /// </summary>
    /// <param name="rawCount">Le nombre de dossiers refuses, tel que transmis par le shell.</param>
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
