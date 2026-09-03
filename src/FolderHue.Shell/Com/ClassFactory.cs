using System.Runtime.InteropServices.Marshalling;

using FolderHue.Shell.Commands;

namespace FolderHue.Shell.Com;

/// <summary>
/// Fabrique la commande racine pour le compte de COM.
/// </summary>
/// <remarks>
/// C'est l'objet que <c>DllGetClassObject</c> rend a l'Explorateur. Il ne fait rien d'autre
/// qu'appeler la fabrique qu'on lui a confiee : aucun travail couteux au chargement de la DLL.
/// Le serveur expose deux classes — la commande moderne et le handler herite — d'ou le
/// parametrage plutot qu'un type code en dur.
/// </remarks>
[GeneratedComClass]
internal sealed partial class ClassFactory(Func<object> create) : IClassFactory
{
    private readonly Func<object> _create = create;

    /// <inheritdoc/>
    public int CreateInstance(IntPtr pUnkOuter, in Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;

        // L'agregation COM n'a pas de sens pour une extension de menu contextuel.
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
            ShellServices.Log.Error("CreateInstance a echoue.", e);
            return HResult.Fail;
        }
    }

    /// <inheritdoc/>
    public int LockServer(bool fLock)
    {
        // La DLL reste chargee tant qu'Explorer le decide : DllCanUnloadNow retourne toujours
        // S_FALSE, il n'y a donc aucun compteur a tenir ici.
        return HResult.Ok;
    }
}
