using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace FolderHue.Shell.Com;

/// <summary>
/// Declarations COM du shell, projetees par les generateurs de source de .NET 8.
/// </summary>
/// <remarks>
/// <c>[GeneratedComInterface]</c> produit un <c>ComWrappers</c> a la compilation : c'est la seule
/// voie compatible NativeAOT, <c>[ComVisible]</c> et <c>RegAsm</c> etant hors jeu (CLAUDE.md §2.1).
/// Toutes les methodes portent <c>[PreserveSig]</c> : une extension shell doit maitriser ses
/// HRESULT et ne jamais laisser echapper d'exception dans <c>explorer.exe</c> (§6.5).
/// </remarks>
internal static class HResult
{
    /// <summary>Succes.</summary>
    internal const int Ok = 0;

    /// <summary>Succes, mais rien a produire — fin d'enumeration.</summary>
    internal const int False = 1;

    /// <summary>Echec generique.</summary>
    internal const int Fail = unchecked((int)0x80004005);

    /// <summary>Fonction non implementee.</summary>
    internal const int NotImplemented = unchecked((int)0x80004001);

    /// <summary>Interface non prise en charge.</summary>
    internal const int NoInterface = unchecked((int)0x80004002);

    /// <summary>Argument invalide.</summary>
    internal const int InvalidArg = unchecked((int)0x80070057);

    /// <summary>Pointeur invalide.</summary>
    internal const int Pointer = unchecked((int)0x80004003);

    /// <summary>L'agregation COM n'est pas prise en charge.</summary>
    internal const int ClassNoAggregation = unchecked((int)0x80040110);
}

/// <summary>Drapeaux retournes par <see cref="IExplorerCommand.GetFlags"/>.</summary>
/// <remarks><c>EXPCMDFLAGS</c>, shobjidl_core.h.</remarks>
[Flags]
internal enum ExplorerCommandFlags : uint
{
    /// <summary>Commande ordinaire.</summary>
    Default = 0,

    /// <summary>La commande ouvre un sous-menu, enumere par <c>IEnumExplorerCommand</c>.</summary>
    HasSubCommands = 0x1,

    /// <summary>La commande est un separateur visuel.</summary>
    IsSeparator = 0x8,
}

/// <summary>Etat retourne par <see cref="IExplorerCommand.GetState"/>.</summary>
/// <remarks><c>EXPCMDSTATE</c>, shobjidl_core.h.</remarks>
[Flags]
internal enum ExplorerCommandState : uint
{
    /// <summary>Commande active.</summary>
    Enabled = 0,

    /// <summary>Commande visible mais grisee.</summary>
    Disabled = 0x1,

    /// <summary>Commande absente du menu.</summary>
    Hidden = 0x2,
}

/// <summary>
/// Commande du menu contextuel de l'Explorateur.
/// </summary>
/// <remarks>
/// Win32 : <c>IExplorerCommand</c>, shobjidl_core.h.
/// Doc : https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-iexplorercommand
/// </remarks>
[GeneratedComInterface]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9")]
internal partial interface IExplorerCommand
{
    /// <summary>Libelle affiche dans le menu.</summary>
    /// <param name="psiItemArray">Selection courante, eventuellement <see cref="IntPtr.Zero"/>.</param>
    /// <param name="ppszName">Recoit une chaine allouee par <c>CoTaskMemAlloc</c>.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetTitle(IntPtr psiItemArray, out IntPtr ppszName);

    /// <summary>Icone affichee a cote du libelle.</summary>
    /// <param name="psiItemArray">Selection courante.</param>
    /// <param name="ppszIcon">Recoit une chaine « fichier,index » allouee par <c>CoTaskMemAlloc</c>.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetIcon(IntPtr psiItemArray, out IntPtr ppszIcon);

    /// <summary>Infobulle. Non utilisee.</summary>
    /// <param name="psiItemArray">Selection courante.</param>
    /// <param name="ppszInfotip">Recoit la chaine d'infobulle.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetToolTip(IntPtr psiItemArray, out IntPtr ppszInfotip);

    /// <summary>Nom canonique de la commande. Non utilise.</summary>
    /// <param name="pguidCommandName">Recoit le GUID de la commande.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetCanonicalName(out Guid pguidCommandName);

    /// <summary>Determine si la commande doit apparaitre, et dans quel etat.</summary>
    /// <param name="psiItemArray">Selection courante.</param>
    /// <param name="fOkToBeSlow">Autorise les operations couteuses, acces disque compris.</param>
    /// <param name="pCmdState">Recoit un <see cref="ExplorerCommandState"/>.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetState(IntPtr psiItemArray, [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow, out uint pCmdState);

    /// <summary>Execute la commande sur la selection.</summary>
    /// <param name="psiItemArray">Selection courante.</param>
    /// <param name="pbc">Contexte de liaison, ignore.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int Invoke(IntPtr psiItemArray, IntPtr pbc);

    /// <summary>Drapeaux de la commande.</summary>
    /// <param name="pFlags">Recoit un <see cref="ExplorerCommandFlags"/>.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetFlags(out uint pFlags);

    /// <summary>Enumere les sous-commandes.</summary>
    /// <param name="ppEnum">Recoit un <c>IEnumExplorerCommand</c> natif.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int EnumSubCommands(out IntPtr ppEnum);
}

/// <summary>
/// Enumerateur de sous-commandes.
/// </summary>
/// <remarks>
/// Win32 : <c>IEnumExplorerCommand</c>, shobjidl_core.h.
/// Doc : https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-ienumexplorercommand
/// </remarks>
[GeneratedComInterface]
[Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8")]
internal partial interface IEnumExplorerCommand
{
    /// <summary>Retourne les commandes suivantes.</summary>
    /// <param name="celt">Nombre de commandes demandees.</param>
    /// <param name="pUICommand">Tableau natif recevant les pointeurs <c>IExplorerCommand</c>.</param>
    /// <param name="pceltFetched">Recoit le nombre de commandes effectivement produites.</param>
    /// <returns><see cref="HResult.Ok"/> tant qu'il reste des commandes, sinon <see cref="HResult.False"/>.</returns>
    [PreserveSig]
    int Next(uint celt, IntPtr pUICommand, out uint pceltFetched);

    /// <summary>Saute des commandes.</summary>
    /// <param name="celt">Nombre de commandes a sauter.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int Skip(uint celt);

    /// <summary>Revient au debut de l'enumeration.</summary>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int Reset();

    /// <summary>Duplique l'enumerateur.</summary>
    /// <param name="ppenum">Recoit le nouvel enumerateur.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int Clone(out IntPtr ppenum);
}

/// <summary>
/// Fabrique de classes COM.
/// </summary>
/// <remarks>
/// Win32 : <c>IClassFactory</c>, unknwn.h.
/// Doc : https://learn.microsoft.com/windows/win32/api/unknwnbase/nn-unknwnbase-iclassfactory
/// </remarks>
[GeneratedComInterface]
[Guid("00000001-0000-0000-C000-000000000046")]
internal partial interface IClassFactory
{
    /// <summary>Cree une instance de la classe.</summary>
    /// <param name="pUnkOuter">Agregation, non prise en charge.</param>
    /// <param name="riid">Interface demandee.</param>
    /// <param name="ppvObject">Recoit le pointeur d'interface.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int CreateInstance(IntPtr pUnkOuter, in Guid riid, out IntPtr ppvObject);

    /// <summary>Verrouille le serveur en memoire.</summary>
    /// <param name="fLock"><see langword="true"/> pour verrouiller.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}

/// <summary>
/// Tableau d'elements du shell : la selection sur laquelle l'utilisateur a clique.
/// </summary>
/// <remarks>
/// Win32 : <c>IShellItemArray</c>, shobjidl_core.h.
/// Doc : https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-ishellitemarray
/// </remarks>
[GeneratedComInterface]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
internal partial interface IShellItemArray
{
    /// <summary>Non utilise.</summary>
    /// <param name="pbc">Contexte de liaison.</param>
    /// <param name="bhid">Identifiant du gestionnaire.</param>
    /// <param name="riid">Interface demandee.</param>
    /// <param name="ppvOut">Recoit le pointeur d'interface.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IntPtr ppvOut);

    /// <summary>Non utilise.</summary>
    /// <param name="flags">Drapeaux.</param>
    /// <param name="riid">Interface demandee.</param>
    /// <param name="ppv">Recoit le pointeur d'interface.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetPropertyStore(uint flags, in Guid riid, out IntPtr ppv);

    /// <summary>Non utilise.</summary>
    /// <param name="keyType">Type de cle.</param>
    /// <param name="riid">Interface demandee.</param>
    /// <param name="ppv">Recoit le pointeur d'interface.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetPropertyDescriptionList(IntPtr keyType, in Guid riid, out IntPtr ppv);

    /// <summary>Combine les attributs de tous les elements de la selection.</summary>
    /// <param name="attribFlags">Mode de combinaison, par exemple ET logique.</param>
    /// <param name="sfgaoMask">Attributs a interroger.</param>
    /// <param name="psfgaoAttribs">Recoit les attributs combines.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetAttributes(uint attribFlags, uint sfgaoMask, out uint psfgaoAttribs);

    /// <summary>Nombre d'elements selectionnes.</summary>
    /// <param name="pdwNumItems">Recoit le nombre d'elements.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetCount(out uint pdwNumItems);

    /// <summary>Retourne un element de la selection.</summary>
    /// <param name="dwIndex">Index, a partir de zero.</param>
    /// <param name="ppsi">Recoit l'element.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetItemAt(uint dwIndex, out IShellItem ppsi);

    /// <summary>Non utilise.</summary>
    /// <param name="ppenumShellItems">Recoit l'enumerateur.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int EnumItems(out IntPtr ppenumShellItems);
}

/// <summary>
/// Un element du shell : ici, un dossier selectionne.
/// </summary>
/// <remarks>
/// Win32 : <c>IShellItem</c>, shobjidl_core.h.
/// Doc : https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-ishellitem
/// </remarks>
[GeneratedComInterface]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
internal partial interface IShellItem
{
    /// <summary>Non utilise.</summary>
    /// <param name="pbc">Contexte de liaison.</param>
    /// <param name="bhid">Identifiant du gestionnaire.</param>
    /// <param name="riid">Interface demandee.</param>
    /// <param name="ppv">Recoit le pointeur d'interface.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IntPtr ppv);

    /// <summary>Non utilise.</summary>
    /// <param name="ppsi">Recoit l'element parent.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetParent(out IShellItem ppsi);

    /// <summary>Retourne un nom de l'element.</summary>
    /// <param name="sigdnName">Forme demandee, par exemple <c>SIGDN_FILESYSPATH</c>.</param>
    /// <param name="ppszName">Recoit une chaine allouee par <c>CoTaskMemAlloc</c>.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetDisplayName(uint sigdnName, out IntPtr ppszName);

    /// <summary>Non utilise.</summary>
    /// <param name="sfgaoMask">Attributs a interroger.</param>
    /// <param name="psfgaoAttribs">Recoit les attributs.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

    /// <summary>Non utilise.</summary>
    /// <param name="psi">Element a comparer.</param>
    /// <param name="hint">Critere de comparaison.</param>
    /// <param name="piOrder">Recoit le resultat de la comparaison.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int Compare(IShellItem psi, uint hint, out int piOrder);
}

/// <summary>
/// Initialisation d'une extension shell heritee.
/// </summary>
/// <remarks>
/// Win32 : <c>IShellExtInit</c>, shobjidl_core.h.
/// Doc : https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-ishellextinit
/// C'est par cette interface que le shell transmet la selection a un handler herite, sous la
/// forme d'un <c>IDataObject</c>.
/// </remarks>
[GeneratedComInterface]
[Guid("000214e8-0000-0000-c000-000000000046")]
internal partial interface IShellExtInit
{
    /// <summary>Recoit la selection sur laquelle le menu va etre construit.</summary>
    /// <param name="pidlFolder">Dossier parent, souvent nul.</param>
    /// <param name="pdtobj">Objet de donnees portant la selection.</param>
    /// <param name="hkeyProgID">Cle de registre du type, ignoree.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int Initialize(IntPtr pidlFolder, IntPtr pdtobj, IntPtr hkeyProgID);
}

/// <summary>
/// Menu contextuel herite.
/// </summary>
/// <remarks>
/// Win32 : <c>IContextMenu</c>, shobjidl_core.h.
/// Doc : https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-icontextmenu
/// <para>
/// Cette interface double <c>IExplorerCommand</c> : elle sert le menu classique, seul menu de
/// Windows 10 et seul menu visible sur Windows 11 lorsque l'utilisateur a restaure l'ancien
/// comportement.
/// </para>
/// </remarks>
[GeneratedComInterface]
[Guid("000214e4-0000-0000-c000-000000000046")]
internal partial interface IContextMenu
{
    /// <summary>Ajoute les entrees au menu fourni par le shell.</summary>
    /// <param name="hmenu">Handle du menu a completer.</param>
    /// <param name="indexMenu">Position ou inserer.</param>
    /// <param name="idCmdFirst">Premier identifiant de commande utilisable.</param>
    /// <param name="idCmdLast">Dernier identifiant de commande utilisable.</param>
    /// <param name="uFlags">Drapeaux <c>CMF_*</c>.</param>
    /// <returns>Un HRESULT dont la partie basse porte le nombre d'identifiants consommes.</returns>
    [PreserveSig]
    int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

    /// <summary>Execute la commande choisie.</summary>
    /// <param name="pici">Pointeur sur une structure <c>CMINVOKECOMMANDINFO</c>.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int InvokeCommand(IntPtr pici);

    /// <summary>Retourne le nom canonique ou l'aide d'une commande.</summary>
    /// <param name="idCmd">Decalage de la commande.</param>
    /// <param name="uType">Type d'information demandee.</param>
    /// <param name="pReserved">Reserve.</param>
    /// <param name="pszName">Tampon de sortie.</param>
    /// <param name="cchMax">Taille du tampon.</param>
    /// <returns>Un HRESULT.</returns>
    [PreserveSig]
    int GetCommandString(IntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
}
