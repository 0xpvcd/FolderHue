using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace FolderHue.Shell.Com;

/// <summary>
/// The shell's COM declarations, projected by the .NET 8 source generators.
/// </summary>
/// <remarks>
/// <c>[GeneratedComInterface]</c> emits a <c>ComWrappers</c> at compile time: the only
/// NativeAOT-compatible route, <c>[ComVisible]</c> and <c>RegAsm</c> being out of the question
/// (CLAUDE.md 2.1). Every method carries <c>[PreserveSig]</c>: a shell extension must own its
/// HRESULTs and never let an exception escape inside <c>explorer.exe</c> (6.5).
/// </remarks>
internal static class HResult
{
    /// <summary>Success.</summary>
    internal const int Ok = 0;

    /// <summary>Success, but nothing to produce - end of enumeration.</summary>
    internal const int False = 1;

    /// <summary>Generic failure.</summary>
    internal const int Fail = unchecked((int)0x80004005);

    /// <summary>Function not implemented.</summary>
    internal const int NotImplemented = unchecked((int)0x80004001);

    /// <summary>Interface not supported.</summary>
    internal const int NoInterface = unchecked((int)0x80004002);

    /// <summary>Invalid argument.</summary>
    internal const int InvalidArg = unchecked((int)0x80070057);

    /// <summary>Invalid pointer.</summary>
    internal const int Pointer = unchecked((int)0x80004003);

    /// <summary>COM aggregation is not supported.</summary>
    internal const int ClassNoAggregation = unchecked((int)0x80040110);
}

/// <summary>Flags returned by <see cref="IExplorerCommand.GetFlags"/>.</summary>
/// <remarks><c>EXPCMDFLAGS</c>, shobjidl_core.h.</remarks>
[Flags]
internal enum ExplorerCommandFlags : uint
{
    /// <summary>An ordinary command.</summary>
    Default = 0,

    /// <summary>The command opens a submenu, enumerated by <c>IEnumExplorerCommand</c>.</summary>
    HasSubCommands = 0x1,

    /// <summary>The command is a visual separator.</summary>
    IsSeparator = 0x8,
}

/// <summary>State returned by <see cref="IExplorerCommand.GetState"/>.</summary>
/// <remarks><c>EXPCMDSTATE</c>, shobjidl_core.h.</remarks>
[Flags]
internal enum ExplorerCommandState : uint
{
    /// <summary>The command is enabled.</summary>
    Enabled = 0,

    /// <summary>The command is visible but greyed out.</summary>
    Disabled = 0x1,

    /// <summary>The command is absent from the menu.</summary>
    Hidden = 0x2,
}

/// <summary>
/// A command in Explorer's context menu.
/// </summary>
/// <remarks>
/// Win32: <c>IExplorerCommand</c>, shobjidl_core.h.
/// Docs: https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-iexplorercommand
/// </remarks>
[GeneratedComInterface]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9")]
internal partial interface IExplorerCommand
{
    /// <summary>The label shown in the menu.</summary>
    /// <param name="psiItemArray">Current selection, possibly <see cref="IntPtr.Zero"/>.</param>
    /// <param name="ppszName">Receives a string allocated with <c>CoTaskMemAlloc</c>.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetTitle(IntPtr psiItemArray, out IntPtr ppszName);

    /// <summary>The icon shown next to the label.</summary>
    /// <param name="psiItemArray">Current selection.</param>
    /// <param name="ppszIcon">Receives a "file,index" string allocated with <c>CoTaskMemAlloc</c>.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetIcon(IntPtr psiItemArray, out IntPtr ppszIcon);

    /// <summary>Tooltip. Not used.</summary>
    /// <param name="psiItemArray">Current selection.</param>
    /// <param name="ppszInfotip">Receives the tooltip string.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetToolTip(IntPtr psiItemArray, out IntPtr ppszInfotip);

    /// <summary>The command's canonical name. Not used.</summary>
    /// <param name="pguidCommandName">Receives the command's GUID.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetCanonicalName(out Guid pguidCommandName);

    /// <summary>Decides whether the command should appear, and in what state.</summary>
    /// <param name="psiItemArray">Current selection.</param>
    /// <param name="fOkToBeSlow">Permits expensive work, disk access included.</param>
    /// <param name="pCmdState">Receives an <see cref="ExplorerCommandState"/>.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetState(IntPtr psiItemArray, [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow, out uint pCmdState);

    /// <summary>Runs the command on the selection.</summary>
    /// <param name="psiItemArray">Current selection.</param>
    /// <param name="pbc">Bind context, ignored.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int Invoke(IntPtr psiItemArray, IntPtr pbc);

    /// <summary>The command's flags.</summary>
    /// <param name="pFlags">Receives an <see cref="ExplorerCommandFlags"/>.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetFlags(out uint pFlags);

    /// <summary>Enumerates the subcommands.</summary>
    /// <param name="ppEnum">Receives a native <c>IEnumExplorerCommand</c>.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int EnumSubCommands(out IntPtr ppEnum);
}

/// <summary>
/// Enumerator of subcommands.
/// </summary>
/// <remarks>
/// Win32: <c>IEnumExplorerCommand</c>, shobjidl_core.h.
/// Docs: https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-ienumexplorercommand
/// </remarks>
[GeneratedComInterface]
[Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8")]
internal partial interface IEnumExplorerCommand
{
    /// <summary>Returns the next commands.</summary>
    /// <param name="celt">How many commands are requested.</param>
    /// <param name="pUICommand">Native array receiving the <c>IExplorerCommand</c> pointers.</param>
    /// <param name="pceltFetched">
    /// Pointer receiving how many commands were produced. <b>May be NULL</b> when
    /// <paramref name="celt"/> is 1.
    /// </param>
    /// <returns><see cref="HResult.Ok"/> while commands remain, otherwise <see cref="HResult.False"/>.</returns>
    /// <remarks>
    /// ⚠️ This parameter is a raw <see cref="IntPtr"/>, not an <c>out uint</c>. The COM enumerator
    /// convention lets the caller pass NULL as soon as it asks for a single element, and Explorer
    /// uses that: it calls <c>Next(1, &amp;cmd, NULL)</c>. With an <c>out uint</c> the generated
    /// stub writes to that pointer unconditionally, the write fails, <c>Next</c> returns an error
    /// and the shell abandons the submenu - with no message anywhere.
    /// <para>
    /// The defect stayed invisible for as long as the DLL was hosted out of process: the
    /// marshalling proxy always materialises a valid pointer. It only appears in-process, that is,
    /// only since the classic registration.
    /// </para>
    /// </remarks>
    [PreserveSig]
    int Next(uint celt, IntPtr pUICommand, IntPtr pceltFetched);

    /// <summary>Skips commands.</summary>
    /// <param name="celt">How many commands to skip.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int Skip(uint celt);

    /// <summary>Returns to the start of the enumeration.</summary>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int Reset();

    /// <summary>Duplicates the enumerator.</summary>
    /// <param name="ppenum">Receives the new enumerator.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int Clone(out IntPtr ppenum);
}

/// <summary>
/// COM class factory.
/// </summary>
/// <remarks>
/// Win32: <c>IClassFactory</c>, unknwn.h.
/// Docs: https://learn.microsoft.com/windows/win32/api/unknwnbase/nn-unknwnbase-iclassfactory
/// </remarks>
[GeneratedComInterface]
[Guid("00000001-0000-0000-C000-000000000046")]
internal partial interface IClassFactory
{
    /// <summary>Creates an instance of the class.</summary>
    /// <param name="pUnkOuter">Aggregation, not supported.</param>
    /// <param name="riid">Interface requested.</param>
    /// <param name="ppvObject">Receives the interface pointer.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int CreateInstance(IntPtr pUnkOuter, in Guid riid, out IntPtr ppvObject);

    /// <summary>Locks the server in memory.</summary>
    /// <param name="fLock"><see langword="true"/> to lock.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}

/// <summary>
/// An array of shell items: the selection the user clicked.
/// </summary>
/// <remarks>
/// Win32: <c>IShellItemArray</c>, shobjidl_core.h.
/// Docs: https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-ishellitemarray
/// </remarks>
[GeneratedComInterface]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
internal partial interface IShellItemArray
{
    /// <summary>Not used.</summary>
    /// <param name="pbc">Bind context.</param>
    /// <param name="bhid">Handler identifier.</param>
    /// <param name="riid">Interface requested.</param>
    /// <param name="ppvOut">Receives the interface pointer.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IntPtr ppvOut);

    /// <summary>Not used.</summary>
    /// <param name="flags">Flags.</param>
    /// <param name="riid">Interface requested.</param>
    /// <param name="ppv">Receives the interface pointer.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetPropertyStore(uint flags, in Guid riid, out IntPtr ppv);

    /// <summary>Not used.</summary>
    /// <param name="keyType">Key type.</param>
    /// <param name="riid">Interface requested.</param>
    /// <param name="ppv">Receives the interface pointer.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetPropertyDescriptionList(IntPtr keyType, in Guid riid, out IntPtr ppv);

    /// <summary>Combines the attributes of every item in the selection.</summary>
    /// <param name="attribFlags">How to combine them, for instance a logical AND.</param>
    /// <param name="sfgaoMask">Attributes to query.</param>
    /// <param name="psfgaoAttribs">Receives the combined attributes.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetAttributes(uint attribFlags, uint sfgaoMask, out uint psfgaoAttribs);

    /// <summary>How many items are selected.</summary>
    /// <param name="pdwNumItems">Receives the item count.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetCount(out uint pdwNumItems);

    /// <summary>Returns one item of the selection.</summary>
    /// <param name="dwIndex">Zero-based index.</param>
    /// <param name="ppsi">Receives the item.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetItemAt(uint dwIndex, out IShellItem ppsi);

    /// <summary>Not used.</summary>
    /// <param name="ppenumShellItems">Receives the enumerator.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int EnumItems(out IntPtr ppenumShellItems);
}

/// <summary>
/// A shell item: here, a selected folder.
/// </summary>
/// <remarks>
/// Win32: <c>IShellItem</c>, shobjidl_core.h.
/// Docs: https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-ishellitem
/// </remarks>
[GeneratedComInterface]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
internal partial interface IShellItem
{
    /// <summary>Not used.</summary>
    /// <param name="pbc">Bind context.</param>
    /// <param name="bhid">Handler identifier.</param>
    /// <param name="riid">Interface requested.</param>
    /// <param name="ppv">Receives the interface pointer.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IntPtr ppv);

    /// <summary>Not used.</summary>
    /// <param name="ppsi">Receives the parent item.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetParent(out IShellItem ppsi);

    /// <summary>Returns one of the item's names.</summary>
    /// <param name="sigdnName">The form requested, for instance <c>SIGDN_FILESYSPATH</c>.</param>
    /// <param name="ppszName">Receives a string allocated with <c>CoTaskMemAlloc</c>.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetDisplayName(uint sigdnName, out IntPtr ppszName);

    /// <summary>Not used.</summary>
    /// <param name="sfgaoMask">Attributes to query.</param>
    /// <param name="psfgaoAttribs">Receives the attributes.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

    /// <summary>Not used.</summary>
    /// <param name="psi">The item to compare against.</param>
    /// <param name="hint">Comparison criterion.</param>
    /// <param name="piOrder">Receives the comparison result.</param>
    /// <returns>An HRESULT.</returns>
    [PreserveSig]
    int Compare(IShellItem psi, uint hint, out int piOrder);
}

