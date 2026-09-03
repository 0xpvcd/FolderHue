namespace FolderHue.Shell.Com;

/// <summary>
/// Identifiants COM du projet.
/// </summary>
/// <remarks>
/// <b>Source unique de verite du CLSID.</b> La meme valeur doit apparaitre, au caractere pres,
/// dans <c>FolderHue.Package/AppxManifest.xml</c> — a la fois dans <c>com:Class/@Id</c> et
/// dans <c>desktop5:Verb/@Clsid</c>. Un ecart et l'entree n'apparait pas dans le menu, sans
/// message d'erreur (CLAUDE.md §10). <c>scripts/build.ps1</c> verifie cette correspondance.
/// </remarks>
internal static class Guids
{
    /// <summary>CLSID du serveur COM de la commande racine « FolderHue ».</summary>
    internal const string RootCommandClsidText = "C228C2F8-706B-4A2E-9C48-74F3062BE146";

    /// <summary>CLSID du serveur COM, sous forme binaire.</summary>
    internal static Guid RootCommandClsid { get; } = new(RootCommandClsidText);

    /// <summary>
    /// CLSID du handler de menu contextuel herite.
    /// </summary>
    /// <remarks>
    /// Un verbe package <c>desktop4</c> n'est rendu que par le menu moderne de Windows 11. Les
    /// utilisateurs qui restaurent le menu classique par le tweak
    /// <c>{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}</c> ne verraient donc jamais l'entree. Ce second
    /// CLSID expose la meme palette via <c>IContextMenu</c>, comme le fait PowerToys.
    /// </remarks>
    internal const string ClassicMenuClsidText = "E647099A-651E-4267-A7DA-1296BD370777";

    /// <summary>CLSID du handler herite, sous forme binaire.</summary>
    internal static Guid ClassicMenuClsid { get; } = new(ClassicMenuClsidText);

    /// <summary>IID de <c>IUnknown</c>.</summary>
    internal static Guid IUnknown { get; } = new("00000000-0000-0000-C000-000000000046");

    /// <summary>IID de <c>IClassFactory</c>.</summary>
    internal static Guid IClassFactory { get; } = new("00000001-0000-0000-C000-000000000046");

    /// <summary>IID de <c>IExplorerCommand</c>.</summary>
    internal static Guid IExplorerCommand { get; } = new("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9");

    /// <summary>IID de <c>IEnumExplorerCommand</c>.</summary>
    internal static Guid IEnumExplorerCommand { get; } = new("a88826f8-186f-4987-aade-ea0cef8fbfe8");

    /// <summary>IID de <c>IShellItemArray</c>.</summary>
    internal static Guid IShellItemArray { get; } = new("b63ea76d-1f85-456f-a19c-48159efa858b");
}
