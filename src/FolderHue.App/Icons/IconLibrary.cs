using System.Globalization;
using System.Runtime.Versioning;
using FolderHue.Core.Icons;
using FolderHue.Core.Palette;
using FolderHue.Core.Storage;

namespace FolderHue.App.Icons;

/// <summary>
/// Pre-generates and maintains the icon library in <c>%LOCALAPPDATA%\FolderHue\icons</c>.
/// </summary>
/// <remarks>
/// The whole palette is produced in one go, at install time. The shell therefore never generates
/// anything: it merely points at an existing file, which keeps it thin and fast inside
/// <c>explorer.exe</c> (CLAUDE.md 4.3).
/// <para>
/// One icon per color + emblem pair, never copied into the user's folders (CLAUDE.md 4.2).
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class IconLibrary
{
    /// <summary>
    /// Renderer version. Bump it whenever the drawing changes, to force a regeneration.
    /// </summary>
    private const int RendererVersion = 5;

    private readonly AppPaths _paths;
    private readonly Log _log;

    /// <summary>Builds the library.</summary>
    /// <param name="paths">Working locations.</param>
    /// <param name="log">Diagnostic log.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public IconLibrary(AppPaths paths, Log log)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(log);

        _paths = paths;
        _log = log;
    }

    /// <summary>Builds the library for the current machine.</summary>
    /// <returns>A ready-to-use instance.</returns>
    public static IconLibrary CreateDefault() => new(AppPaths.Default, Log.Default);

    /// <summary>Indicates whether the library is complete and up to date.</summary>
    /// <returns><see langword="true"/> when no generation is needed.</returns>
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
    /// Generates every missing icon.
    /// </summary>
    /// <param name="force">
    /// <see langword="true"/> to regenerate even what already exists, for instance after a Windows
    /// theme change.
    /// </param>
    /// <param name="progress">Reports progress as (produced, total).</param>
    /// <returns>How many icons were actually written.</returns>
    /// <exception cref="InvalidOperationException">The template could not be extracted from the shell.</exception>
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

        // The context menu chips: the brand logo for the root entry, and one tint per color in
        // front of each palette entry.
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

        // The "Emblem" submenu badges, drawn large so that they stay legible at 16 px.
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
        _log.Info($"Icon library up to date: {written} icon(s) written out of {total}.");

        return written;
    }

    /// <summary>
    /// Loads the template and renders a preview, for the settings window.
    /// </summary>
    /// <returns>A renderer, or <see langword="null"/> when the template is unavailable.</returns>
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

    /// <summary>Paths of the menu chips the library is expected to hold.</summary>
    /// <returns>
    /// The brand logo, one tint per palette color, then one badge per emblem.
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
        // TintableColors and not Colors: the neutral color produces neutral.ico, the original
        // folder icon, plus its combinations with an emblem.
        foreach (FolderColor color in PaletteCatalog.TintableColors)
        {
            foreach (Emblem emblem in PaletteCatalog.Emblems)
            {
                yield return (color, emblem);
            }
        }
    }

    /// <summary>
    /// Stamp identifying the library's expected state.
    /// </summary>
    /// <returns>A string that changes as soon as the palette or the renderer does.</returns>
    private static string CurrentStamp() => string.Create(
        CultureInfo.InvariantCulture,
        $"v{RendererVersion};c{PaletteCatalog.Colors.Count};e{PaletteCatalog.Emblems.Count};s{IconSizes.All.Count}");

    /// <summary>
    /// Writes the neutral template to disk, for reference and diagnostics.
    /// </summary>
    /// <param name="template">The BGRA buffers extracted from the shell.</param>
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
            // The reference template is only a diagnostic convenience.
            _log.Warn("The reference template could not be written: " + e.Message);
        }
    }
}
