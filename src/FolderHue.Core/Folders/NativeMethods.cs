using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FolderHue.Core.Folders;

/// <summary>
/// Unique point d'entree P/Invoke de <c>FolderHue.Core</c>.
/// </summary>
/// <remarks>
/// CLAUDE.md §7 impose un seul fichier de P/Invoke par projet, et §3 limite <c>Core</c> aux seules
/// API shell strictement necessaires. <c>LibraryImport</c> est prefere a <c>DllImport</c> : le
/// marshalling est genere a la compilation, ce qu'exige NativeAOT.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static partial class NativeMethods
{
    /// <summary>Le contenu d'un dossier a change. <c>SHCNE_UPDATEDIR</c>, shlobj_core.h.</summary>
    private const int SHCNE_UPDATEDIR = 0x00001000;

    /// <summary>Un element a change. <c>SHCNE_UPDATEITEM</c>, shlobj_core.h.</summary>
    private const int SHCNE_UPDATEITEM = 0x00002000;

    /// <summary>Les arguments sont des chemins Unicode. <c>SHCNF_PATHW</c>, shlobj_core.h.</summary>
    private const uint SHCNF_PATHW = 0x0005;

    /// <summary>
    /// Vide la file de notifications sans attendre. <c>SHCNF_FLUSHNOWAIT</c>, shlobj_core.h.
    /// </summary>
    /// <remarks>
    /// Sans drapeau de purge, le shell regroupe les notifications et les distribue quand il le
    /// juge bon : l'icone finit par changer, mais pas tout de suite. <c>SHCNF_FLUSH</c> forcerait
    /// la distribution mais <b>bloquerait</b> l'appelant jusqu'a la fin du traitement — depuis
    /// <c>explorer.exe</c>, ou notre code tourne, c'est un interblocage potentiel.
    /// <c>SHCNF_FLUSHNOWAIT</c> purge sans attendre : c'est le seul des deux utilisable ici.
    /// </remarks>
    private const uint SHCNF_FLUSHNOWAIT = 0x3000;

    /// <summary>
    /// Notifie le shell d'un changement.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>SHChangeNotify</c>, shell32.dll, en-tete shlobj_core.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shchangenotify
    /// </remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHChangeNotify", StringMarshalling = StringMarshalling.Utf16)]
    private static partial void SHChangeNotify(int wEventId, uint uFlags, string dwItem1, IntPtr dwItem2);

    /// <summary>
    /// Recupere le chemin d'un dossier connu.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>SHGetKnownFolderPath</c>, shell32.dll, en-tete shlobj_core.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shgetknownfolderpath
    /// Le tampon retourne est alloue par COM et doit etre libere par <c>CoTaskMemFree</c>.
    /// </remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHGetKnownFolderPath")]
    private static partial int SHGetKnownFolderPath(in Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    /// <summary>
    /// Demande a l'Explorateur de rafraichir l'affichage d'un dossier.
    /// </summary>
    /// <param name="folderPath">Chemin absolu du dossier.</param>
    /// <remarks>
    /// Sans cet appel, l'icone ne change qu'apres un F5 ou un redemarrage d'Explorer
    /// (CLAUDE.md §4.1). L'appel est absorbe en cas d'echec : ce n'est qu'un rafraichissement.
    /// <para>
    /// Trois notifications, et non une seule, parce que l'icone d'un dossier n'est pas dessinee
    /// la ou on croit :
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     <c>SHCNE_UPDATEITEM</c> sur le dossier : c'est <b>la vue du dossier parent</b> qui
    ///     dessine l'icone, et c'est donc elle qu'il faut prevenir. Notifier uniquement le
    ///     dossier lui-meme laissait la vue parente afficher l'ancienne icone jusqu'a sa
    ///     prochaine reenumeration — d'ou une colorisation qui « marchait une fois sur deux ».
    ///   </description></item>
    ///   <item><description>
    ///     <c>SHCNE_UPDATEDIR</c> sur le dossier : pour une fenetre ouverte <i>sur</i> ce dossier,
    ///     dont le titre et l'icone changent aussi.
    ///   </description></item>
    ///   <item><description>
    ///     <c>SHCNE_UPDATEDIR</c> sur le parent : filet de securite pour les vues qui ne
    ///     s'abonnent qu'aux evenements de repertoire.
    ///   </description></item>
    /// </list>
    /// </remarks>
    internal static void NotifyFolderChanged(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        const uint flags = SHCNF_PATHW | SHCNF_FLUSHNOWAIT;

        try
        {
            SHChangeNotify(SHCNE_UPDATEITEM, flags, folderPath, IntPtr.Zero);
            SHChangeNotify(SHCNE_UPDATEDIR, flags, folderPath, IntPtr.Zero);

            string? parent = TryGetParentDirectory(folderPath);
            if (parent is not null)
            {
                SHChangeNotify(SHCNE_UPDATEDIR, flags, parent, IntPtr.Zero);
            }
        }
        catch (DllNotFoundException)
        {
            // Environnement sans shell : le rafraichissement n'a pas de sens.
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    /// <summary>
    /// Dossier parent d'un chemin, ou <see langword="null"/> pour une racine de volume.
    /// </summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <returns>Le chemin du parent, ou <see langword="null"/>.</returns>
    private static string? TryGetParentDirectory(string folderPath)
    {
        try
        {
            string? parent = Path.GetDirectoryName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
            return string.IsNullOrEmpty(parent) ? null : parent;
        }
        catch (Exception e) when (e is ArgumentException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// Retourne le chemin d'un dossier connu, ou <see langword="null"/> s'il n'est pas resolu.
    /// </summary>
    /// <param name="folderId">Le <c>KNOWNFOLDERID</c> recherche.</param>
    /// <returns>Le chemin absolu, ou <see langword="null"/>.</returns>
    internal static string? GetKnownFolderPath(Guid folderId)
    {
        IntPtr buffer = IntPtr.Zero;

        try
        {
            // dwFlags = 0 : on veut le chemin courant, sans creer le dossier ni forcer le defaut.
            if (SHGetKnownFolderPath(in folderId, 0, IntPtr.Zero, out buffer) != 0)
            {
                return null;
            }

            return buffer == IntPtr.Zero ? null : Marshal.PtrToStringUni(buffer);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(buffer);
            }
        }
    }
}
