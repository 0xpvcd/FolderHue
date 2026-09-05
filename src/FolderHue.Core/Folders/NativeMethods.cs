using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FolderHue.Core.Folders;

/// <summary>
/// The single P/Invoke entry point of <c>FolderHue.Core</c>.
/// </summary>
/// <remarks>
/// CLAUDE.md 7 mandates one P/Invoke file per project, and 3 limits <c>Core</c> to the shell APIs
/// that are strictly necessary. <c>LibraryImport</c> is preferred over <c>DllImport</c>: the
/// marshalling is generated at compile time, which NativeAOT requires.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static partial class NativeMethods
{
    /// <summary>A folder's contents changed. <c>SHCNE_UPDATEDIR</c>, shlobj_core.h.</summary>
    private const int SHCNE_UPDATEDIR = 0x00001000;

    /// <summary>An item changed. <c>SHCNE_UPDATEITEM</c>, shlobj_core.h.</summary>
    private const int SHCNE_UPDATEITEM = 0x00002000;

    /// <summary>The arguments are Unicode paths. <c>SHCNF_PATHW</c>, shlobj_core.h.</summary>
    private const uint SHCNF_PATHW = 0x0005;

    /// <summary>
    /// Flushes the notification queue without waiting. <c>SHCNF_FLUSHNOWAIT</c>, shlobj_core.h.
    /// </summary>
    /// <remarks>
    /// Without a flush flag the shell batches notifications and delivers them when it sees fit:
    /// the icon does change eventually, but not right away. <c>SHCNF_FLUSH</c> would force
    /// delivery but would <b>block</b> the caller until processing ends — from inside
    /// <c>explorer.exe</c>, where our code runs, that is a potential deadlock.
    /// <c>SHCNF_FLUSHNOWAIT</c> flushes without waiting: it is the only one of the two usable
    /// here.
    /// </remarks>
    private const uint SHCNF_FLUSHNOWAIT = 0x3000;

    /// <summary>
    /// Notifies the shell of a change.
    /// </summary>
    /// <remarks>
    /// Win32: <c>SHChangeNotify</c>, shell32.dll, header shlobj_core.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shchangenotify
    /// </remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHChangeNotify", StringMarshalling = StringMarshalling.Utf16)]
    private static partial void SHChangeNotify(int wEventId, uint uFlags, string dwItem1, IntPtr dwItem2);

    /// <summary>
    /// Retrieves the path of a known folder.
    /// </summary>
    /// <remarks>
    /// Win32: <c>SHGetKnownFolderPath</c>, shell32.dll, header shlobj_core.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shgetknownfolderpath
    /// The returned buffer is COM-allocated and must be released with <c>CoTaskMemFree</c>.
    /// </remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHGetKnownFolderPath")]
    private static partial int SHGetKnownFolderPath(in Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    /// <summary>The customisation concerns the icon file. <c>FCSM_ICONFILE</c>, shlobj_core.h.</summary>
    private const uint FCSM_ICONFILE = 0x00000010;

    /// <summary>Write without re-reading what is there. <c>FCS_FORCEWRITE</c>, shlobj_core.h.</summary>
    private const uint FCS_FORCEWRITE = 0x00000002;

    /// <summary>
    /// A folder's customisation. <c>SHFOLDERCUSTOMSETTINGS</c>, shlobj_core.h.
    /// </summary>
    /// <remarks>
    /// The default sequential layout matches C's on 64-bit. Only <c>dwSize</c>, <c>dwMask</c>,
    /// <c>pszIconFile</c>, <c>cchIconFile</c> and <c>iIconIndex</c> concern us; the other fields
    /// stay at zero and the mask makes them ignored.
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
    /// Reads or writes a folder's customisation.
    /// </summary>
    /// <remarks>
    /// Win32: <c>SHGetSetFolderCustomSettings</c>, shell32.dll, header shlobj_core.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shgetsetfoldercustomsettings
    /// This is the API Explorer itself uses for
    /// <i>Properties > Customize > Change icon</i>.
    /// </remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHGetSetFolderCustomSettings", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHGetSetFolderCustomSettings(ref FolderCustomSettings pfcs, string pszPath, uint dwReadWrite);

    /// <summary>
    /// Rewrites a folder's icon through the official customisation API.
    /// </summary>
    /// <param name="folderPath">Path of the folder.</param>
    /// <param name="iconFile">Path of the icon file.</param>
    /// <param name="iconIndex">Index of the icon within that file.</param>
    /// <returns><see langword="true"/> when the call succeeded.</returns>
    /// <remarks>
    /// <b>This call, and this call alone, refreshes an already-open view.</b> Measured on screen,
    /// on a window open on the parent folder: writing <c>desktop.ini</c> ourselves and then
    /// notifying the shell <b>never</b> repaints the icon — not with <c>SHCNE_UPDATEITEM</c>,
    /// <c>SHCNE_UPDATEDIR</c>, <c>SHCNE_ATTRIBUTES</c>, <c>SHCNE_RENAMEFOLDER</c>,
    /// <c>SHCNE_UPDATEIMAGE</c> or <c>SHCNE_ASSOCCHANGED</c>, by path or by PIDL, with or without
    /// <c>SHCNF_FLUSH</c>; not after F5; not after navigating away and back. Only a newly opened
    /// window showed the right color. The same change routed through this function repaints the
    /// icon within a second.
    /// <para>
    /// Writing <c>desktop.ini</c> stays ours: it is what merges existing keys and handles the
    /// backup (CLAUDE.md 6.1). This call then re-writes the same value through the official path,
    /// which triggers the internal cache invalidation <c>SHChangeNotify</c> does not.
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
            // No shell in this environment: the coloring is still correct on disk.
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
    /// Asks Explorer to refresh a folder's display.
    /// </summary>
    /// <param name="folderPath">Absolute path of the folder.</param>
    /// <remarks>
    /// Without this call the icon only changes after an F5 or an Explorer restart (CLAUDE.md 4.1).
    /// Failure is swallowed: this is only a refresh.
    /// <para>
    /// Three notifications rather than one, because a folder's icon is not drawn where one would
    /// think:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     <c>SHCNE_UPDATEITEM</c> on the folder: <b>the parent folder's view</b> is what draws
    ///     the icon, so that is what has to be told. Notifying only the folder itself left the
    ///     parent view showing the old icon until its next re-enumeration — hence a coloring that
    ///     "worked every other time".
    ///   </description></item>
    ///   <item><description>
    ///     <c>SHCNE_UPDATEDIR</c> on the folder: for a window open <i>on</i> that folder, whose
    ///     title and icon change too.
    ///   </description></item>
    ///   <item><description>
    ///     <c>SHCNE_UPDATEDIR</c> on the parent: a safety net for views that only subscribe to
    ///     directory events.
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
            // No shell in this environment: refreshing is meaningless.
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    /// <summary>
    /// Parent folder of a path, or <see langword="null"/> for a volume root.
    /// </summary>
    /// <param name="folderPath">Path of the folder.</param>
    /// <returns>The parent path, or <see langword="null"/>.</returns>
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
    /// Returns a known folder's path, or <see langword="null"/> when it does not resolve.
    /// </summary>
    /// <param name="folderId">The <c>KNOWNFOLDERID</c> to look up.</param>
    /// <returns>The absolute path, or <see langword="null"/>.</returns>
    internal static string? GetKnownFolderPath(Guid folderId)
    {
        IntPtr buffer = IntPtr.Zero;

        try
        {
            // dwFlags = 0: we want the current path, without creating the folder or forcing the default.
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
