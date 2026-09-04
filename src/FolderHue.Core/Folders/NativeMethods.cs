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

    /// <summary>La personnalisation porte sur le fichier d'icone. <c>FCSM_ICONFILE</c>, shlobj_core.h.</summary>
    private const uint FCSM_ICONFILE = 0x00000010;

    /// <summary>Ecrire sans relire l'existant. <c>FCS_FORCEWRITE</c>, shlobj_core.h.</summary>
    private const uint FCS_FORCEWRITE = 0x00000002;

    /// <summary>
    /// Personnalisation d'un dossier. <c>SHFOLDERCUSTOMSETTINGS</c>, shlobj_core.h.
    /// </summary>
    /// <remarks>
    /// La disposition sequentielle par defaut reproduit celle du C en 64 bits. Seuls
    /// <c>dwSize</c>, <c>dwMask</c>, <c>pszIconFile</c>, <c>cchIconFile</c> et <c>iIconIndex</c>
    /// nous concernent ; les autres champs restent a zero et sont ignores grace au masque.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    private struct FolderCustomSettings
    {
        internal uint Size;
        internal uint Mask;
        internal IntPtr ViewId;
        internal IntPtr WebViewTemplate;
        internal uint WebViewTemplateLength;
        internal IntPtr WebViewTemplateVersion;
        internal IntPtr InfoTip;
        internal uint InfoTipLength;
        internal IntPtr Clsid;
        internal uint Flags;
        internal IntPtr IconFile;
        internal uint IconFileLength;
        internal int IconIndex;
        internal IntPtr Logo;
        internal uint LogoLength;
    }

    /// <summary>
    /// Lit ou ecrit la personnalisation d'un dossier.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>SHGetSetFolderCustomSettings</c>, shell32.dll, en-tete shlobj_core.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shgetsetfoldercustomsettings
    /// C'est l'API qu'emploie l'Explorateur lui-meme pour
    /// <i>Proprietes > Personnaliser > Changer d'icone</i>.
    /// </remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHGetSetFolderCustomSettings", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHGetSetFolderCustomSettings(ref FolderCustomSettings pfcs, string pszPath, uint dwReadWrite);

    /// <summary>
    /// Reecrit l'icone d'un dossier par l'API officielle de personnalisation.
    /// </summary>
    /// <param name="folderPath">Chemin du dossier.</param>
    /// <param name="iconFile">Chemin du fichier icone.</param>
    /// <param name="iconIndex">Index de l'icone dans ce fichier.</param>
    /// <returns><see langword="true"/> si l'appel a abouti.</returns>
    /// <remarks>
    /// <b>C'est cet appel, et lui seul, qui rafraichit une vue deja ouverte.</b> Mesure a l'ecran,
    /// sur une fenetre ouverte sur le dossier parent : ecrire <c>desktop.ini</c> nous-memes puis
    /// notifier le shell ne repeint <b>jamais</b> l'icone — ni avec <c>SHCNE_UPDATEITEM</c>,
    /// <c>SHCNE_UPDATEDIR</c>, <c>SHCNE_ATTRIBUTES</c>, <c>SHCNE_RENAMEFOLDER</c>,
    /// <c>SHCNE_UPDATEIMAGE</c> ou <c>SHCNE_ASSOCCHANGED</c>, en chemin comme en PIDL, avec ou
    /// sans <c>SHCNF_FLUSH</c> ; ni apres un F5 ; ni apres un aller-retour de navigation. Seule
    /// une fenetre nouvellement ouverte montrait la bonne couleur. Le meme changement passe par
    /// cette fonction repeint l'icone dans la seconde.
    /// <para>
    /// L'ecriture de <c>desktop.ini</c> reste la notre : c'est elle qui fusionne les cles
    /// existantes et gere la sauvegarde (CLAUDE.md §6.1). Cet appel vient ensuite reposer la meme
    /// valeur par le chemin officiel, ce qui declenche l'invalidation de cache interne que
    /// <c>SHChangeNotify</c> ne declenche pas.
    /// </para>
    /// </remarks>
    internal static bool SetFolderIcon(string folderPath, string iconFile, int iconIndex)
    {
        if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(iconFile))
        {
            return false;
        }

        IntPtr buffer = IntPtr.Zero;

        try
        {
            buffer = Marshal.StringToHGlobalUni(iconFile);

            var settings = new FolderCustomSettings
            {
                Size = (uint)Marshal.SizeOf<FolderCustomSettings>(),
                Mask = FCSM_ICONFILE,
                IconFile = buffer,
                IconFileLength = (uint)(iconFile.Length + 1),
                IconIndex = iconIndex,
            };

            return SHGetSetFolderCustomSettings(ref settings, folderPath, FCS_FORCEWRITE) >= 0;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            // Environnement sans shell : la colorisation reste correcte sur disque.
            return false;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

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
