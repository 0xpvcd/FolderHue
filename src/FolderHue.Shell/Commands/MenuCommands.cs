using System.Runtime.InteropServices.Marshalling;
using FolderHue.Core.Folders;
using FolderHue.Core.Palette;
using FolderHue.Core.Resources;
using FolderHue.Shell.Com;

namespace FolderHue.Shell.Commands;

/// <summary>
/// The root "FolderHue" command.
/// </summary>
/// <remarks>
/// This is the only class the COM server's CLSID exposes.
/// <para>
/// ⚠️ <b>The submenu must fit in 16 items, separators INCLUDED.</b> This comment long claimed the
/// opposite — "separators do not count" — and it was false. Taken to 17 items by adding
/// "Original color", Explorer did not truncate the submenu: it made <b>the entire root entry</b>
/// disappear, logo and all, without a single message. The COM server kept activating normally
/// (<c>tools/probe-shell.ps1</c>), which makes the failure indistinguishable from a registration
/// problem.
/// </para>
/// <para>
/// Current count, exactly 16: 12 colors + "Original color" + one separator + "Emblem" + "Reset".
/// There is <b>no headroom left</b>: any extra entry means removing another or nesting further
/// (CLAUDE.md 4.4).
/// </para>
/// <para>
/// ⚠️ That ceiling was measured on the packaged MSIX verb. It has <b>not</b> been re-verified
/// since the move to a classic registry verb; nothing says the two share the same limit. Measure
/// again with <c>tools/probe-menu.ps1</c> before relying on either answer.
/// </para>
/// </remarks>
[GeneratedComClass]
internal sealed partial class RootCommand : ExplorerCommandBase
{
    /// <inheritdoc/>
    protected override string Title => Loc.Get("Menu_Root");

    /// <inheritdoc/>
    /// <remarks>The application logo, in the brand colors.</remarks>
    protected override string? IconResource => ShellServices.Paths.BrandLogoPath + ",0";

    /// <inheritdoc/>
    protected override ExplorerCommandFlags Flags => ExplorerCommandFlags.HasSubCommands;

    /// <inheritdoc/>
    protected override IReadOnlyList<object> CreateSubCommands()
    {
        var commands = new List<object>(PaletteCatalog.Colors.Count + 4);

        foreach (FolderColor color in PaletteCatalog.Colors)
        {
            commands.Add(new ColorCommand(color));
        }

        // "Original color" is chosen like any hue, at the end of the palette. It gives the folder
        // its original icon back while keeping its emblem: putting the color back must not erase
        // the status marker, exactly as the reverse holds (CLAUDE.md 4.3). With no emblem, Apply
        // falls back to a reset on its own.
        commands.Add(new ColorCommand(PaletteCatalog.Neutral));

        // One separator, not two: it isolates the block of colors from the two actions that
        // follow. The second one, once placed before "Reset", was removed to stay within the 16
        // items — see the warning at the top of this class.
        commands.Add(new SeparatorCommand());
        commands.Add(new EmblemMenuCommand());
        commands.Add(new ResetCommand());

        return commands;
    }
}

/// <summary>
/// Applies one hue from the palette.
/// </summary>
/// <param name="color">The hue this menu entry stands for.</param>
[GeneratedComClass]
internal sealed partial class ColorCommand(FolderColor color) : ExplorerCommandBase
{
    private readonly FolderColor _color = color;

    /// <inheritdoc/>
    protected override string Title => Loc.Get(_color.ResourceKey);

    /// <inheritdoc/>
    /// <remarks>
    /// The logo tinted with this hue. Plain string concatenation: no disk access, not even to
    /// check that the file exists. Explorer reads this property every time the menu opens.
    /// <para>
    /// "Original color" is the exception: its chip is <c>neutral.ico</c> — the machine's real
    /// folder icon — rather than a tinted logo. The menu's rule is that a chip looks like the icon
    /// the folder will take (CLAUDE.md 9); a neutral tint would have changed nothing and stayed
    /// orange, announcing a color instead of its removal.
    /// </para>
    /// </remarks>
    protected override string? IconResource => _color.IsNeutral
        ? ShellServices.Paths.IconPath(_color.Id, null) + ",0"
        : ShellServices.Paths.LogoPath(_color.Id) + ",0";

    /// <inheritdoc/>
    protected override void Execute(IReadOnlyList<string> paths)
        => ShellServices.ApplyColor(paths, _color.Id);
}

/// <summary>
/// The "Emblem" entry, which opens the nested submenu of status markers.
/// </summary>
[GeneratedComClass]
internal sealed partial class EmblemMenuCommand : ExplorerCommandBase
{
    /// <inheritdoc/>
    protected override string Title => Loc.Get("Menu_Emblem");

    /// <inheritdoc/>
    /// <remarks>The neutral badge, standing in as a generic marker symbol.</remarks>
    protected override string? IconResource
        => ShellServices.Paths.EmblemChipPath(Emblem.NoneId) + ",0";

    /// <inheritdoc/>
    protected override ExplorerCommandFlags Flags => ExplorerCommandFlags.HasSubCommands;

    /// <inheritdoc/>
    protected override IReadOnlyList<object> CreateSubCommands()
    {
        var commands = new List<object>(PaletteCatalog.Emblems.Count);

        foreach (Emblem emblem in PaletteCatalog.Emblems)
        {
            commands.Add(new EmblemCommand(emblem));
        }

        return commands;
    }
}

/// <summary>
/// Applies an emblem while keeping the color already in place.
/// </summary>
/// <param name="emblem">The emblem this menu entry stands for.</param>
[GeneratedComClass]
internal sealed partial class EmblemCommand(Emblem emblem) : ExplorerCommandBase
{
    private readonly Emblem _emblem = emblem;

    /// <inheritdoc/>
    protected override string Title => Loc.Get(_emblem.ResourceKey);

    /// <inheritdoc/>
    /// <remarks>This marker's badge, drawn large so that it stays legible at 16 px.</remarks>
    protected override string? IconResource
        => ShellServices.Paths.EmblemChipPath(_emblem.Id) + ",0";

    /// <inheritdoc/>
    /// <remarks>
    /// Each folder's color is preserved. A folder that was never colored keeps its own: placing a
    /// marker must not tint it along the way.
    /// </remarks>
    protected override void Execute(IReadOnlyList<string> paths)
        => ShellServices.ApplyEmblem(paths, _emblem.Id);
}

/// <summary>
/// Removes the coloring and restores the folder's original state.
/// </summary>
[GeneratedComClass]
internal sealed partial class ResetCommand : ExplorerCommandBase
{
    /// <inheritdoc/>
    protected override string Title => Loc.Get("Menu_Reset");

    /// <inheritdoc/>
    /// <remarks>The original folder icon: exactly what the action gives back.</remarks>
    protected override string? IconResource
        => ShellServices.Paths.IconPath(PaletteCatalog.Neutral.Id, Emblem.NoneId) + ",0";

    /// <inheritdoc/>
    protected override void Execute(IReadOnlyList<string> paths)
        => ShellServices.Reset(paths);
}

/// <summary>
/// A visual separator in the menu.
/// </summary>
/// <remarks>
/// ⚠️ A separator <b>does</b> count towards the 16-item ceiling. This comment used to say the
/// opposite, contradicting the warning on <see cref="RootCommand"/> in the same file — and the
/// measurement sided with the warning.
/// </remarks>
[GeneratedComClass]
internal sealed partial class SeparatorCommand : ExplorerCommandBase
{
    /// <inheritdoc/>
    protected override string Title => string.Empty;

    /// <inheritdoc/>
    protected override ExplorerCommandFlags Flags => ExplorerCommandFlags.IsSeparator;
}
