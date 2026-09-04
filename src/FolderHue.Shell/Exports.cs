using System.Runtime.InteropServices;
using FolderHue.Shell.Com;
using FolderHue.Shell.Commands;

namespace FolderHue.Shell;

/// <summary>
/// Points d'entree natifs exportes par la DLL.
/// </summary>
/// <remarks>
/// <c>[UnmanagedCallersOnly]</c> produit de vrais exports natifs dans l'image NativeAOT : c'est ce
/// qui remplace l'enregistrement par <c>RegAsm</c>, impossible en AOT (CLAUDE.md §2.1).
/// </remarks>
internal static class Exports
{
    /// <summary>
    /// Rend la fabrique de classes du CLSID demande.
    /// </summary>
    /// <param name="rclsid">CLSID demande par COM.</param>
    /// <param name="riid">Interface demandee, en pratique <c>IClassFactory</c>.</param>
    /// <param name="ppv">Recoit le pointeur d'interface.</param>
    /// <returns>Un HRESULT.</returns>
    /// <remarks>
    /// Win32 : <c>DllGetClassObject</c>, combaseapi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/combaseapi/nf-combaseapi-dllgetclassobject
    /// </remarks>
    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
    public static unsafe int DllGetClassObject(Guid* rclsid, Guid* riid, IntPtr* ppv)
    {
        if (ppv is null)
        {
            return HResult.Pointer;
        }

        *ppv = IntPtr.Zero;

        if (rclsid is null || riid is null)
        {
            return HResult.Pointer;
        }

        try
        {
            // Le serveur n'expose qu'une classe : la commande moderne. Elle est rendue par le
            // menu direct de Windows 11 comme par le menu classique.
            Func<object>? create = null;

            if (*rclsid == Guids.RootCommandClsid)
            {
                create = static () => new RootCommand();
            }

            if (create is null)
            {
                // CLSID inconnu : c'est le message que COM attend pour passer au serveur suivant.
                return unchecked((int)0x80040154); // REGDB_E_CLASSNOTREG
            }

            int hr = ShellComWrappers.GetComInterface(new ClassFactory(create), *riid, out IntPtr factory);
            *ppv = factory;
            return hr;
        }
        catch (Exception e)
        {
            // Aucune exception ne doit franchir la frontiere native : Explorer tomberait.
            ShellServices.Log.Error("DllGetClassObject a echoue.", e);
            return HResult.Fail;
        }
    }

    /// <summary>
    /// Indique si la DLL peut etre dechargee.
    /// </summary>
    /// <returns>Toujours <c>S_FALSE</c>.</returns>
    /// <remarks>
    /// Win32 : <c>DllCanUnloadNow</c>, combaseapi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/combaseapi/nf-combaseapi-dllcanunloadnow
    /// <para>
    /// Le runtime NativeAOT ne se reinitialise pas apres un dechargement : on refuse donc
    /// systematiquement. C'est le comportement recommande pour un serveur COM gere.
    /// </para>
    /// </remarks>
    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
    public static int DllCanUnloadNow() => HResult.False;
}
