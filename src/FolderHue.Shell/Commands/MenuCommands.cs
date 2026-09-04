using System.Runtime.InteropServices.Marshalling;
using FolderHue.Core.Folders;
using FolderHue.Core.Palette;
using FolderHue.Core.Resources;
using FolderHue.Shell.Com;

namespace FolderHue.Shell.Commands;

/// <summary>
/// Commande racine « FolderHue ».
/// </summary>
/// <remarks>
/// C'est la seule classe exposee par le CLSID du serveur COM. Le sous-menu compte
/// 12 couleurs + « Couleur d'origine » + « Embleme » + « Reinitialiser », soit 15 elements — les
/// separateurs ne comptent pas dans la limite de 16 par handler (CLAUDE.md §4.4).
/// </remarks>
[GeneratedComClass]
internal sealed partial class RootCommand : ExplorerCommandBase
{
    /// <inheritdoc/>
    protected override string Title => Loc.Get("Menu_Root");

    /// <inheritdoc/>
    /// <remarks>Le logo de l'application, aux couleurs de la marque.</remarks>
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

        // « Couleur d'origine » se choisit comme une teinte, en fin de palette. Elle rend au
        // dossier son icone d'origine en conservant son embleme : reposer la couleur ne doit pas
        // effacer le marqueur de statut, exactement comme l'inverse (CLAUDE.md §4.3). Sans
        // embleme, Apply retombe de lui-meme sur une reinitialisation.
        commands.Add(new ColorCommand(PaletteCatalog.Neutral));

        commands.Add(new SeparatorCommand());
        commands.Add(new EmblemMenuCommand());
        commands.Add(new SeparatorCommand());
        commands.Add(new ResetCommand());

        return commands;
    }
}

/// <summary>
/// Applique une teinte de la palette.
/// </summary>
/// <param name="color">La teinte representee par cette entree de menu.</param>
[GeneratedComClass]
internal sealed partial class ColorCommand(FolderColor color) : ExplorerCommandBase
{
    private readonly FolderColor _color = color;

    /// <inheritdoc/>
    protected override string Title => Loc.Get(_color.ResourceKey);

    /// <inheritdoc/>
    /// <remarks>
    /// La declinaison du logo dans cette teinte. Simple concatenation de chaines : aucun acces
    /// disque, meme pour verifier que le fichier existe. L'Explorateur appelle cette propriete a
    /// chaque ouverture du menu.
    /// <para>
    /// Exception faite de « Couleur d'origine », dont la puce est <c>neutral.ico</c> — l'icone de
    /// dossier reelle de la machine — et non une declinaison du logo. La regle du menu est qu'une
    /// puce ressemble a l'icone que le dossier prendra (CLAUDE.md §9) ; une declinaison neutre
    /// n'aurait rien teinte et serait restee orange, annoncant une couleur au lieu de son retrait.
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
/// Entree « Embleme », qui ouvre le sous-menu imbrique des marqueurs de statut.
/// </summary>
[GeneratedComClass]
internal sealed partial class EmblemMenuCommand : ExplorerCommandBase
{
    /// <inheritdoc/>
    protected override string Title => Loc.Get("Menu_Emblem");

    /// <inheritdoc/>
    /// <remarks>La pastille neutre, qui sert de symbole generique de marqueur.</remarks>
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
/// Applique un embleme en conservant la couleur en place.
/// </summary>
/// <param name="emblem">L'embleme represente par cette entree de menu.</param>
[GeneratedComClass]
internal sealed partial class EmblemCommand(Emblem emblem) : ExplorerCommandBase
{
    private readonly Emblem _emblem = emblem;

    /// <inheritdoc/>
    protected override string Title => Loc.Get(_emblem.ResourceKey);

    /// <inheritdoc/>
    /// <remarks>La pastille de ce marqueur, dessinee en grand pour rester lisible a 16 px.</remarks>
    protected override string? IconResource
        => ShellServices.Paths.EmblemChipPath(_emblem.Id) + ",0";

    /// <inheritdoc/>
    /// <remarks>
    /// La couleur de chaque dossier est conservee. Un dossier jamais colorise garde la sienne :
    /// poser un marqueur ne doit pas le teindre au passage.
    /// </remarks>
    protected override void Execute(IReadOnlyList<string> paths)
        => ShellServices.ApplyEmblem(paths, _emblem.Id);
}

/// <summary>
/// Retire la colorisation et restaure l'etat d'origine du dossier.
/// </summary>
[GeneratedComClass]
internal sealed partial class ResetCommand : ExplorerCommandBase
{
    /// <inheritdoc/>
    protected override string Title => Loc.Get("Menu_Reset");

    /// <inheritdoc/>
    /// <remarks>L'icone de dossier d'origine : exactement ce que l'action rend.</remarks>
    protected override string? IconResource
        => ShellServices.Paths.IconPath(PaletteCatalog.Neutral.Id, Emblem.NoneId) + ",0";

    /// <inheritdoc/>
    protected override void Execute(IReadOnlyList<string> paths)
        => ShellServices.Reset(paths);
}

/// <summary>
/// Separateur visuel du menu.
/// </summary>
/// <remarks>
/// Les separateurs ne comptent pas dans la limite de 16 elements par handler.
/// </remarks>
[GeneratedComClass]
internal sealed partial class SeparatorCommand : ExplorerCommandBase
{
    /// <inheritdoc/>
    protected override string Title => string.Empty;

    /// <inheritdoc/>
    protected override ExplorerCommandFlags Flags => ExplorerCommandFlags.IsSeparator;
}
