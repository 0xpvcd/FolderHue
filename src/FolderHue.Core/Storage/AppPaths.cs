namespace FolderHue.Core.Storage;

/// <summary>
/// Resout les emplacements de travail de l'application sous <c>%LOCALAPPDATA%\FolderHue</c>.
/// </summary>
/// <remarks>
/// C'est le seul endroit du systeme de fichiers ou l'application ecrit, en dehors des dossiers que
/// l'utilisateur selectionne explicitement (CLAUDE.md §11).
/// <para>
/// La classe est instanciable pour que les tests puissent travailler dans un dossier temporaire
/// jetable plutot que dans le profil reel (CLAUDE.md §8).
/// </para>
/// </remarks>
public sealed class AppPaths
{
    /// <summary>Nom du dossier applicatif sous <c>%LOCALAPPDATA%</c>.</summary>
    public const string FolderName = "FolderHue";

    /// <summary>
    /// Variable d'environnement permettant de forcer la racine, utilisee par les tests.
    /// </summary>
    public const string RootEnvironmentVariable = "FOLDERHUE_ROOT";

    /// <summary>Cree un jeu de chemins enracine sur un dossier donne.</summary>
    /// <param name="root">Racine, absolue. Elle n'a pas besoin d'exister.</param>
    /// <exception cref="ArgumentException"><paramref name="root"/> est vide.</exception>
    public AppPaths(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);

        Root = Path.GetFullPath(root);
        IconsDirectory = Path.Combine(Root, "icons");
        BaseDirectory = Path.Combine(Root, "base");
        LogsDirectory = Path.Combine(Root, "logs");
        JournalFile = Path.Combine(Root, "applied.json");
        BaseIconFile = Path.Combine(BaseDirectory, "folder-base.ico");
        IconLibraryStampFile = Path.Combine(IconsDirectory, ".version");
        LogFile = Path.Combine(LogsDirectory, "folderhue.log");
    }

    /// <summary>Les chemins reels de la machine courante.</summary>
    public static AppPaths Default { get; } = new(ResolveDefaultRoot());

    /// <summary>Racine applicative, typiquement <c>%LOCALAPPDATA%\FolderHue</c>.</summary>
    public string Root { get; }

    /// <summary>Dossier des <c>.ico</c> pre-generes, un par combinaison couleur + embleme.</summary>
    public string IconsDirectory { get; }

    /// <summary>Dossier du gabarit d'icone extrait du shell de la machine.</summary>
    public string BaseDirectory { get; }

    /// <summary>Dossier des journaux de diagnostic.</summary>
    public string LogsDirectory { get; }

    /// <summary>Fichier <c>applied.json</c> : la trace de ce que nous avons modifie.</summary>
    public string JournalFile { get; }

    /// <summary>Gabarit d'icone de dossier neutre servant de base a la colorisation.</summary>
    public string BaseIconFile { get; }

    /// <summary>Marqueur de version de la bibliotheque d'icones, pour eviter de la regenerer.</summary>
    public string IconLibraryStampFile { get; }

    /// <summary>Fichier de journal courant.</summary>
    public string LogFile { get; }

    /// <summary>Chemin absolu du <c>.ico</c> d'une combinaison couleur + embleme.</summary>
    /// <param name="colorId">Identifiant de la couleur.</param>
    /// <param name="emblemId">Identifiant de l'embleme, ou <see langword="null"/> pour aucun.</param>
    /// <returns>Un chemin absolu, que le fichier existe ou non.</returns>
    public string IconPath(string colorId, string? emblemId)
        => Path.Combine(IconsDirectory, Palette.PaletteCatalog.IconFileName(colorId, emblemId));

    /// <summary>Chemin absolu de la puce d'un embleme.</summary>
    /// <param name="emblemId">Identifiant de l'embleme.</param>
    /// <returns>Un chemin absolu, que le fichier existe ou non.</returns>
    public string EmblemChipPath(string emblemId)
        => Path.Combine(IconsDirectory, Palette.PaletteCatalog.EmblemChipFileName(emblemId));

    /// <summary>Chemin absolu du logo de l'application, aux couleurs de la marque.</summary>
    /// <returns>Un chemin absolu, que le fichier existe ou non.</returns>
    public string BrandLogoPath => Path.Combine(IconsDirectory, Palette.PaletteCatalog.BrandLogoFileName);

    /// <summary>Chemin absolu de la declinaison du logo dans une teinte de la palette.</summary>
    /// <param name="colorId">Identifiant de la couleur.</param>
    /// <returns>Un chemin absolu, que le fichier existe ou non.</returns>
    public string LogoPath(string colorId)
        => Path.Combine(IconsDirectory, Palette.PaletteCatalog.LogoFileName(colorId));

    /// <summary>Cree les dossiers de travail s'ils n'existent pas.</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(IconsDirectory);
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    /// <summary>
    /// Indique si un chemin se trouve sous la racine applicative.
    /// </summary>
    /// <param name="path">Chemin a tester, absolu ou relatif.</param>
    /// <returns><see langword="true"/> si le chemin est la racine ou se trouve dessous.</returns>
    /// <remarks>
    /// Sert a interdire la colorisation de nos propres dossiers de travail (CLAUDE.md §6.2).
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

        string root = Root.TrimEnd(Path.DirectorySeparatorChar);

        return full.Equals(root, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveDefaultRoot()
    {
        string? forced = Environment.GetEnvironmentVariable(RootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(forced))
        {
            return forced;
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Sous MSIX, un processus peut voir un %LOCALAPPDATA% redirige vers le magasin prive du
        // paquet. Le shell (dans explorer.exe) et l'app doivent imperativement designer le meme
        // dossier d'icones : on revient donc au chemin reel du profil des qu'on detecte la
        // redirection.
        if (localAppData.Contains(@"\Packages\", StringComparison.OrdinalIgnoreCase))
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile))
            {
                localAppData = Path.Combine(profile, "AppData", "Local");
            }
        }

        return Path.Combine(localAppData, FolderName);
    }
}
