using FolderHue.Core.Storage;

namespace FolderHue.Shell.Com;

/// <summary>
/// COM identifiers used by the shell extension.
/// </summary>
/// <remarks>
/// The CLSID is <b>not</b> declared here. It belongs to
/// <see cref="ShellRegistration.ClassIdText"/>, because the same value has to be written into the
/// registry by the installer and answered by this server. Keeping one constant, referenced from
/// both sides, removes the whole class of failure where the two drift apart — which shows up as a
/// menu entry that never appears, with no error anywhere.
/// </remarks>
internal static class Guids
{
    /// <summary>CLSID of the root "FolderHue" command's COM server.</summary>
    internal const string RootCommandClsidText = ShellRegistration.ClassIdText;

    /// <summary>CLSID of the COM server, in binary form.</summary>
    internal static Guid RootCommandClsid { get; } = new(RootCommandClsidText);

    /// <summary>IID of <c>IUnknown</c>.</summary>
    internal static Guid IUnknown { get; } = new("00000000-0000-0000-C000-000000000046");

    /// <summary>IID of <c>IClassFactory</c>.</summary>
    internal static Guid IClassFactory { get; } = new("00000001-0000-0000-C000-000000000046");

    /// <summary>IID of <c>IExplorerCommand</c>.</summary>
    internal static Guid IExplorerCommand { get; } = new("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9");

    /// <summary>IID of <c>IEnumExplorerCommand</c>.</summary>
    internal static Guid IEnumExplorerCommand { get; } = new("a88826f8-186f-4987-aade-ea0cef8fbfe8");
}
