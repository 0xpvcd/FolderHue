using System.Globalization;
using System.Runtime.Versioning;
using FolderHue.Core.Icons;
using FolderHue.Core.Palette;
using FolderHue.Core.Storage;

namespace FolderHue.App.Icons;

/// <summary>
/// Pre-genere et maintient la bibliotheque d'icones de <c>%LOCALAPPDATA%\FolderHue\icons</c>.
/// </summary>
/// <remarks>
/// Toute la palette est produite d'un coup, a l'installation. Le shell ne genere donc jamais rien :
/// il se contente de pointer un fichier existant, ce qui le garde mince et rapide dans
/// <c>explorer.exe</c> (CLAUDE.md §4.3).
/// <para>
/// Une icone par combinaison couleur + embleme, jamais copiee dans les dossiers de l'utilisateur
/// (CLAUDE.md §4.2).
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class IconLibrary
{
    /// <summary>
    /// Version du rendu. A incrementer des que le dessin change, pour forcer une regeneration.
    /// </summary>
    private const int RendererVersion = 4;

    private readonly AppPaths _paths;
    private readonly Log _log;

    /// <summary>Construit la bibliotheque.</summary>
    /// <param name="paths">Emplacements de travail.</param>
    /// <param name="log">Journal de diagnostic.</param>
    /// <exception cref="ArgumentNullException">Un argument vaut <see langword="null"/>.</exception>
    public IconLibrary(AppPaths paths, Log log)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(log);

        _paths = paths;
        _log = log;
    }

    /// <summary>Construit la bibliotheque de la machine courante.</summary>
    /// <returns>Une instance prete a l'emploi.</returns>
    public static IconLibrary CreateDefault() => new(AppPaths.Default, Log.Default);

    /// <summary>Indique si la bibliotheque est complete et a jour.</summary>
    /// <returns><see langword="true"/> si aucune generation n'est necessaire.</returns>
    public bool IsUpToDate()
    {
        if (!File.Exists(_paths.IconLibraryStampFile))
        {
            return false;
        }

        try
        {
            if (File.ReadAllText(_paths.IconLibraryStampFile).Trim() != CurrentStamp())
            {
                return false;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        foreach ((FolderColor color, Emblem emblem) in Combinations())
        {
            if (!File.Exists(_paths.IconPath(color.Id, emblem.Id)))
            {
                return false;
            }
        }

        foreach (string chip in ChipPaths())
        {
            if (!File.Exists(chip))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Genere toutes les icones manquantes.
    /// </summary>
    /// <param name="force">
    /// <see langword="true"/> pour regenerer meme ce qui existe deja, par exemple apres un
    /// changement de theme Windows.
    /// </param>
    /// <param name="progress">Rapporte l'avancement sous la forme (produites, total).</param>
    /// <returns>Le nombre d'icones effectivement ecrites.</returns>
    /// <exception cref="InvalidOperationException">Le gabarit n'a pas pu etre extrait du shell.</exception>
    public int EnsureAll(bool force = false, IProgress<(int Done, int Total)>? progress = null)
    {
        _paths.EnsureDirectories();

        if (!force && IsUpToDate())
        {
            return 0;
        }

        IReadOnlyDictionary<int, byte[]> template = BaseIconExtractor.Extract();
        WriteTemplateForReference(template);

        var renderer = new IconRenderer(template);
        int total = PaletteCatalog.IconCombinationCount
            + PaletteCatalog.Colors.Count + 1
            + PaletteCatalog.Emblems.Count;
        int done = 0;
        int written = 0;

        foreach ((FolderColor color, Emblem emblem) in Combinations())
        {
            string path = _paths.IconPath(color.Id, emblem.Id);

            if (force || !File.Exists(path))
            {
                renderer.Render(color, emblem, path);
                written++;
            }

            progress?.Report((++done, total));
        }

        // Les puces du menu contextuel : le logo de la marque pour l'entree racine, une
        // declinaison par teinte devant chaque couleur.
        if (force || !File.Exists(_paths.BrandLogoPath))
        {
            LogoArtwork.WriteIcon(_paths.BrandLogoPath, null);
            written++;
        }

        progress?.Report((++done, total));

        foreach (FolderColor color in PaletteCatalog.Colors)
        {
            string path = _paths.LogoPath(color.Id);

            if (force || !File.Exists(path))
            {
                LogoArtwork.WriteIcon(path, color);
                written++;
            }

            progress?.Report((++done, total));
        }

        // Les pastilles du sous-menu « Embleme », dessinees en grand pour rester lisibles a 16 px.
        foreach (Emblem emblem in PaletteCatalog.Emblems)
        {
            string path = _paths.EmblemChipPath(emblem.Id);

            if (force || !File.Exists(path))
            {
                IconRenderer.WriteEmblemChip(emblem, path);
                written++;
            }

            progress?.Report((++done, total));
        }

        File.WriteAllText(_paths.IconLibraryStampFile, CurrentStamp());
        _log.Info($"Bibliotheque d'icones a jour : {written} icone(s) ecrite(s) sur {total}.");

        return written;
    }

    /// <summary>
    /// Charge le gabarit et rend un apercu, pour l'interface de reglages.
    /// </summary>
    /// <returns>Un moteur de rendu, ou <see langword="null"/> si le gabarit est indisponible.</returns>
    internal static IconRenderer? TryCreateRenderer()
    {
        try
        {
            return new IconRenderer(BaseIconExtractor.Extract());
        }
        catch (Exception e) when (e is InvalidOperationException or IOException)
        {
            return null;
        }
    }

    /// <summary>Chemins des puces de menu attendues dans la bibliotheque.</summary>
    /// <returns>
    /// Le logo de la marque, une declinaison par teinte de la palette, puis une pastille par
    /// embleme.
    /// </returns>
    private IEnumerable<string> ChipPaths()
    {
        yield return _paths.BrandLogoPath;

        foreach (FolderColor color in PaletteCatalog.Colors)
        {
            yield return _paths.LogoPath(color.Id);
        }

        foreach (Emblem emblem in PaletteCatalog.Emblems)
        {
            yield return _paths.EmblemChipPath(emblem.Id);
        }
    }

    private static IEnumerable<(FolderColor Color, Emblem Emblem)> Combinations()
    {
        // TintableColors et non Colors : la couleur neutre produit neutral.ico, l'icone de dossier
        // d'origine, et ses combinaisons avec embleme.
        foreach (FolderColor color in PaletteCatalog.TintableColors)
        {
            foreach (Emblem emblem in PaletteCatalog.Emblems)
            {
                yield return (color, emblem);
            }
        }
    }

    /// <summary>
    /// Marqueur identifiant l'etat attendu de la bibliotheque.
    /// </summary>
    /// <returns>Une chaine qui change des que la palette ou le rendu evolue.</returns>
    private static string CurrentStamp() => string.Create(
        CultureInfo.InvariantCulture,
        $"v{RendererVersion};c{PaletteCatalog.Colors.Count};e{PaletteCatalog.Emblems.Count};s{IconSizes.All.Count}");

    /// <summary>
    /// Ecrit le gabarit neutre sur disque, a titre de reference et de diagnostic.
    /// </summary>
    /// <param name="template">Les tampons BGRA extraits du shell.</param>
    private void WriteTemplateForReference(IReadOnlyDictionary<int, byte[]> template)
    {
        try
        {
            var frames = new List<IcoFrame>(template.Count);

            foreach (int size in IconSizes.All)
            {
                if (template.TryGetValue(size, out byte[]? pixels) && !IconSizes.UsePng(size))
                {
                    frames.Add(new IcoFrame(size, size, DibFrameBuilder.Build(pixels, size, size), IsPng: false));
                }
            }

            if (frames.Count > 0)
            {
                IcoWriter.WriteFile(_paths.BaseIconFile, frames);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Le gabarit de reference n'est qu'un confort de diagnostic.
            _log.Warn("Le gabarit de reference n'a pas pu etre ecrit : " + e.Message);
        }
    }
}
