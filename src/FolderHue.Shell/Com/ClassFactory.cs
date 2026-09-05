using System.Runtime.InteropServices.Marshalling;

using FolderHue.Shell.Commands;

namespace FolderHue.Shell.Com;

/// <summary>
/// Creates the root command on COM's behalf.
/// </summary>
/// <remarks>
/// This is the object <c>DllGetClassObject</c> hands to Explorer. It does nothing but call the
/// factory it was given: no expensive work when the DLL loads. The server exposes a single class
/// today, but the factory stays parameterised rather than hard-coding a type — that is what made
/// removing the second class a one-line change.
/// </remarks>
[GeneratedComClass]
internal sealed partial class ClassFactory(Func<object> create) : IClassFactory
{
    private readonly Func<object> _create = create;

    /// <inheritdoc/>
    public int CreateInstance(IntPtr pUnkOuter, in Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;

        // COM aggregation makes no sense for a context menu extension.
        if (pUnkOuter != IntPtr.Zero)
        {
            return HResult.ClassNoAggregation;
        }

        try
        {
            return ShellComWrappers.GetComInterface(_create(), in riid, out ppvObject);
        }
        catch (Exception e)
        {
            ShellServices.Log.Error("CreateInstance failed.", e);
            return HResult.Fail;
        }
    }

    /// <inheritdoc/>
    public int LockServer(bool fLock)
    {
        // The DLL stays loaded for as long as Explorer decides: DllCanUnloadNow always returns
        // S_FALSE, so there is no counter to keep here.
        return HResult.Ok;
    }
}
