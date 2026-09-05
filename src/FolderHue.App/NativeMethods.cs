using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FolderHue.App;

/// <summary>
/// The single P/Invoke entry point of <c>FolderHue.App</c> (CLAUDE.md 7).
/// </summary>
/// <remarks>
/// These calls serve one purpose: extracting the machine's native folder icon to use as a
/// template. That is what makes a colored icon keep the Windows 10 look on Windows 10 and the
/// Windows 11 look on Windows 11.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static unsafe partial class NativeMethods
{
    /// <summary>The standard folder icon. <c>SIID_FOLDER</c>, shellapi.h.</summary>
    internal const uint SIID_FOLDER = 3;

    /// <summary>Fills <c>szPath</c> and <c>iIcon</c>. <c>SHGSI_ICONLOCATION</c>, shellapi.h.</summary>
    internal const uint SHGSI_ICONLOCATION = 0;

    /// <summary>Raw DIB colors. <c>DIB_RGB_COLORS</c>, wingdi.h.</summary>
    private const uint DIB_RGB_COLORS = 0;

    /// <summary>
    /// Information about a stock icon.
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
    /// Descriptor of a GDI bitmap.
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
    /// Header of a DIB.
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
    /// The parts an icon is made of.
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
    /// Finds the file and index of a stock icon.
    /// </summary>
    /// <remarks>
    /// Win32: <c>SHGetStockIconInfo</c>, shell32.dll, header shellapi.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shgetstockiconinfo
    /// This is the sanctioned API for locating the folder icon, rather than hard-coding
    /// "imageres.dll,-3", which varies between Windows versions.
    /// </remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHGetStockIconInfo")]
    internal static partial int SHGetStockIconInfo(uint siid, uint uFlags, ref StockIconInfo psii);

    /// <summary>
    /// Extracts icons from a file at a given size.
    /// </summary>
    /// <remarks>
    /// Win32: <c>PrivateExtractIconsW</c>, user32.dll, header winuser.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-privateextracticonsw
    /// Unlike <c>ExtractIconEx</c>, this function accepts an arbitrary size and picks the best
    /// available frame, which is what yields the template's ten resolutions.
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
    /// Retrieves the bitmaps an icon is composed of.
    /// </summary>
    /// <remarks>
    /// Win32: <c>GetIconInfo</c>, user32.dll, header winuser.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-geticoninfo
    /// The caller must release <c>MaskBitmap</c> and <c>ColorBitmap</c> with <c>DeleteObject</c>.
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetIconInfo")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetIconInfo(IntPtr hIcon, out IconInfo piconinfo);

    /// <summary>
    /// Destroys an icon.
    /// </summary>
    /// <remarks>
    /// Win32: <c>DestroyIcon</c>, user32.dll, header winuser.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-destroyicon
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "DestroyIcon")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Reads a GDI object's characteristics.
    /// </summary>
    /// <remarks>
    /// Win32: <c>GetObjectW</c>, gdi32.dll, header wingdi.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-getobject
    /// </remarks>
    [LibraryImport("gdi32.dll", EntryPoint = "GetObjectW")]
    internal static partial int GetObject(IntPtr hgdiobj, int cbBuffer, out BitmapInfoStruct lpvObject);

    /// <summary>
    /// Copies a bitmap's pixels into a buffer, in the requested format.
    /// </summary>
    /// <remarks>
    /// Win32: <c>GetDIBits</c>, gdi32.dll, header wingdi.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-getdibits
    /// A negative height in the header asks for a top-down scan, which saves flipping the image
    /// afterwards.
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
    /// Releases a GDI object.
    /// </summary>
    /// <remarks>
    /// Win32: <c>DeleteObject</c>, gdi32.dll, header wingdi.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-deleteobject
    /// </remarks>
    [LibraryImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(IntPtr hObject);

    /// <summary>
    /// Obtains a device context.
    /// </summary>
    /// <remarks>
    /// Win32: <c>GetDC</c>, user32.dll, header winuser.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getdc
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetDC")]
    internal static partial IntPtr GetDC(IntPtr hWnd);

    /// <summary>
    /// Returns a device context.
    /// </summary>
    /// <remarks>
    /// Win32: <c>ReleaseDC</c>, user32.dll, header winuser.h.
    /// Docs: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-releasedc
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "ReleaseDC")]
    internal static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    /// <summary>The <c>DIB_RGB_COLORS</c> constant for <see cref="GetDIBits"/>.</summary>
    internal static uint DibRgbColors => DIB_RGB_COLORS;
}
