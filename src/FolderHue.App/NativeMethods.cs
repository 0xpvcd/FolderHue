using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FolderHue.App;

/// <summary>
/// Unique point d'entree P/Invoke de <c>FolderHue.App</c> (CLAUDE.md §7).
/// </summary>
/// <remarks>
/// Ces appels servent a une seule chose : extraire l'icone de dossier native de la machine pour
/// s'en servir de gabarit. C'est ce qui fait qu'une icone colorisee garde l'aspect de Windows 10
/// sur Windows 10 et de Windows 11 sur Windows 11.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static unsafe partial class NativeMethods
{
    /// <summary>Icone de dossier standard. <c>SIID_FOLDER</c>, shellapi.h.</summary>
    internal const uint SIID_FOLDER = 3;

    /// <summary>Remplit <c>szPath</c> et <c>iIcon</c>. <c>SHGSI_ICONLOCATION</c>, shellapi.h.</summary>
    internal const uint SHGSI_ICONLOCATION = 0;

    /// <summary>Couleurs brutes du DIB. <c>DIB_RGB_COLORS</c>, wingdi.h.</summary>
    private const uint DIB_RGB_COLORS = 0;

    /// <summary>
    /// Informations sur une icone systeme.
    /// </summary>
    /// <remarks><c>SHSTOCKICONINFO</c>, shellapi.h.</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct StockIconInfo
    {
        internal uint CbSize;
        internal IntPtr HIcon;
        internal int SysImageIndex;
        internal int IconIndex;
        internal fixed char Path[260];
    }

    /// <summary>
    /// Descripteur d'un bitmap GDI.
    /// </summary>
    /// <remarks><c>BITMAP</c>, wingdi.h.</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoStruct
    {
        internal int Type;
        internal int Width;
        internal int Height;
        internal int WidthBytes;
        internal ushort Planes;
        internal ushort BitsPixel;
        internal IntPtr Bits;
    }

    /// <summary>
    /// En-tete d'un DIB.
    /// </summary>
    /// <remarks><c>BITMAPINFOHEADER</c>, wingdi.h.</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        internal int Size;
        internal int Width;
        internal int Height;
        internal ushort Planes;
        internal ushort BitCount;
        internal uint Compression;
        internal uint SizeImage;
        internal int XPelsPerMeter;
        internal int YPelsPerMeter;
        internal uint ClrUsed;
        internal uint ClrImportant;
    }

    /// <summary>
    /// Composants d'une icone.
    /// </summary>
    /// <remarks><c>ICONINFO</c>, winuser.h.</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct IconInfo
    {
        internal int IsIcon;
        internal int XHotspot;
        internal int YHotspot;
        internal IntPtr MaskBitmap;
        internal IntPtr ColorBitmap;
    }

    /// <summary>
    /// Retrouve le fichier et l'index d'une icone systeme.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>SHGetStockIconInfo</c>, shell32.dll, en-tete shellapi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shgetstockiconinfo
    /// C'est l'API sanctionnee pour localiser l'icone de dossier, plutot que de coder en dur
    /// « imageres.dll,-3 » qui varie selon les versions de Windows.
    /// </remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHGetStockIconInfo")]
    internal static partial int SHGetStockIconInfo(uint siid, uint uFlags, ref StockIconInfo psii);

    /// <summary>
    /// Extrait des icones d'un fichier a une taille donnee.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>PrivateExtractIconsW</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-privateextracticonsw
    /// Contrairement a <c>ExtractIconEx</c>, cette fonction accepte une taille arbitraire et
    /// choisit la meilleure trame disponible, ce qui donne les dix resolutions du gabarit.
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "PrivateExtractIconsW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint PrivateExtractIcons(
        string szFileName,
        int nIconIndex,
        int cxIcon,
        int cyIcon,
        IntPtr* phicon,
        uint* piconid,
        uint nIcons,
        uint flags);

    /// <summary>
    /// Recupere les bitmaps composant une icone.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetIconInfo</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-geticoninfo
    /// L'appelant doit liberer <c>MaskBitmap</c> et <c>ColorBitmap</c> avec <c>DeleteObject</c>.
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetIconInfo")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetIconInfo(IntPtr hIcon, out IconInfo piconinfo);

    /// <summary>
    /// Detruit une icone.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>DestroyIcon</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-destroyicon
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "DestroyIcon")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Lit les caracteristiques d'un objet GDI.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetObjectW</c>, gdi32.dll, en-tete wingdi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-getobject
    /// </remarks>
    [LibraryImport("gdi32.dll", EntryPoint = "GetObjectW")]
    internal static partial int GetObject(IntPtr hgdiobj, int cbBuffer, out BitmapInfoStruct lpvObject);

    /// <summary>
    /// Copie les pixels d'un bitmap dans un tampon, au format demande.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetDIBits</c>, gdi32.dll, en-tete wingdi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-getdibits
    /// Une hauteur negative dans l'en-tete demande un balayage de haut en bas, ce qui evite
    /// d'avoir a retourner l'image ensuite.
    /// </remarks>
    [LibraryImport("gdi32.dll", EntryPoint = "GetDIBits")]
    internal static partial int GetDIBits(
        IntPtr hdc,
        IntPtr hbm,
        uint start,
        uint cLines,
        byte* lpvBits,
        ref BitmapInfoHeader lpbmi,
        uint usage);

    /// <summary>
    /// Libere un objet GDI.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>DeleteObject</c>, gdi32.dll, en-tete wingdi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-deleteobject
    /// </remarks>
    [LibraryImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(IntPtr hObject);

    /// <summary>
    /// Obtient un contexte de peripherique.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetDC</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getdc
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetDC")]
    internal static partial IntPtr GetDC(IntPtr hWnd);

    /// <summary>
    /// Rend un contexte de peripherique.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>ReleaseDC</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-releasedc
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "ReleaseDC")]
    internal static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    /// <summary>Constante <c>DIB_RGB_COLORS</c> pour <see cref="GetDIBits"/>.</summary>
    internal static uint DibRgbColors => DIB_RGB_COLORS;
}
