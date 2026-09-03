using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FolderHue.Shell;

/// <summary>
/// Unique point d'entree P/Invoke de <c>FolderHue.Shell</c> (CLAUDE.md §7).
/// </summary>
/// <remarks>
/// Les interfaces COM ne passent pas par ici : elles sont projetees par les generateurs de source
/// dans <c>Com/ComInterop.cs</c>.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static unsafe partial class NativeMethods
{
    /// <summary>Le nom passe est en fait une adresse. <c>GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS</c>, libloaderapi.h.</summary>
    private const uint GetModuleHandleExFlagFromAddress = 0x00000004;

    /// <summary>Ne pas incrementer le compteur de references du module. <c>..._UNCHANGED_REFCOUNT</c>, libloaderapi.h.</summary>
    private const uint GetModuleHandleExFlagUnchangedRefcount = 0x00000002;

    /// <summary>
    /// Retrouve le module contenant une adresse donnee.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetModuleHandleExW</c>, kernel32.dll, en-tete libloaderapi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/libloaderapi/nf-libloaderapi-getmodulehandleexw
    /// </remarks>
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleExW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetModuleHandleEx(uint dwFlags, IntPtr lpModuleName, out IntPtr phModule);

    /// <summary>
    /// Retourne le chemin complet du fichier d'un module charge.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetModuleFileNameW</c>, kernel32.dll, en-tete libloaderapi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/libloaderapi/nf-libloaderapi-getmodulefilenamew
    /// </remarks>
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", SetLastError = true)]
    private static partial uint GetModuleFileName(IntPtr hModule, char* lpFilename, uint nSize);

    /// <summary>Element de menu textuel. <c>MF_STRING</c>, winuser.h.</summary>
    internal const uint MfString = 0x00000000;

    /// <summary>Sous-menu. <c>MF_POPUP</c>, winuser.h.</summary>
    internal const uint MfPopup = 0x00000010;

    /// <summary>Separateur. <c>MF_SEPARATOR</c>, winuser.h.</summary>
    internal const uint MfSeparator = 0x00000800;

    /// <summary>Position et non identifiant. <c>MF_BYPOSITION</c>, winuser.h.</summary>
    internal const uint MfByPosition = 0x00000400;

    /// <summary>
    /// Cree un menu deroulant vide.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>CreatePopupMenu</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-createpopupmenu
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "CreatePopupMenu", SetLastError = true)]
    internal static partial IntPtr CreatePopupMenu();

    /// <summary>
    /// Ajoute un element a la fin d'un menu.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>AppendMenuW</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-appendmenuw
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    /// <summary>
    /// Insere un element a une position donnee.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>InsertMenuW</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-insertmenuw
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "InsertMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    /// <summary>L'element porte une image. <c>MIIM_BITMAP</c>, winuser.h.</summary>
    private const uint MiimBitmap = 0x00000080;

    /// <summary>Cote d'une petite icone systeme. <c>SM_CXSMICON</c>, winuser.h.</summary>
    private const int SmCxSmIcon = 49;

    /// <summary>La ressource demandee est une icone. <c>IMAGE_ICON</c>, winuser.h.</summary>
    private const uint ImageIcon = 1;

    /// <summary>Charger depuis un fichier et non depuis un module. <c>LR_LOADFROMFILE</c>, winuser.h.</summary>
    private const uint LrLoadFromFile = 0x00000010;

    /// <summary>Les couleurs du DIB sont litterales. <c>DIB_RGB_COLORS</c>, wingdi.h.</summary>
    private const uint DibRgbColors = 0;

    /// <summary>Bitmap non compresse. <c>BI_RGB</c>, wingdi.h.</summary>
    private const uint BiRgb = 0;

    /// <summary>
    /// Description d'un element de menu. <c>MENUITEMINFOW</c>, winuser.h.
    /// </summary>
    /// <remarks>
    /// La disposition sequentielle par defaut reproduit exactement celle du C en 64 bits :
    /// les cinq premiers champs de 4 octets, puis un alignement a 8 avant <c>hSubMenu</c>.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    private struct MenuItemInfo
    {
        internal uint Size;
        internal uint Mask;
        internal uint Type;
        internal uint State;
        internal uint Id;
        internal IntPtr SubMenu;
        internal IntPtr CheckedBitmap;
        internal IntPtr UncheckedBitmap;
        internal IntPtr ItemData;
        internal IntPtr TypeData;
        internal uint Capacity;
        internal IntPtr ItemBitmap;
    }

    /// <summary>Les deux bitmaps composant une icone. <c>ICONINFO</c>, winuser.h.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        internal int IsIcon;
        internal uint HotspotX;
        internal uint HotspotY;
        internal IntPtr MaskBitmap;
        internal IntPtr ColorBitmap;
    }

    /// <summary>En-tete d'un DIB. <c>BITMAPINFOHEADER</c>, wingdi.h.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        internal uint Size;
        internal int Width;
        internal int Height;
        internal ushort Planes;
        internal ushort BitCount;
        internal uint Compression;
        internal uint SizeImage;
        internal int PixelsPerMeterX;
        internal int PixelsPerMeterY;
        internal uint ColorsUsed;
        internal uint ColorsImportant;
    }

    /// <summary>
    /// En-tete d'un DIB suivi de sa table de couleurs. <c>BITMAPINFO</c>, wingdi.h.
    /// </summary>
    /// <remarks>
    /// En 32 bits par pixel la table n'est pas consultee, mais le systeme lit une structure
    /// <c>BITMAPINFO</c> complete : la reserver evite de lui faire lire la pile au-dela.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        internal BitmapInfoHeader Header;
        internal uint FirstColor;
    }

    /// <summary>
    /// Modifie un element de menu deja cree.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>SetMenuItemInfoW</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setmenuiteminfow
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "SetMenuItemInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetMenuItemInfo(
        IntPtr hMenu, uint item, [MarshalAs(UnmanagedType.Bool)] bool fByPosition, in MenuItemInfo lpmii);

    /// <summary>
    /// Charge une image, ici une icone lue dans un fichier.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>LoadImageW</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-loadimagew
    /// Les dimensions demandees selectionnent la trame la mieux adaptee du conteneur ICO.
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr LoadImage(
        IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    /// <summary>
    /// Retourne les bitmaps de couleur et de masque d'une icone.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetIconInfo</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-geticoninfo
    /// Les deux bitmaps rendus appartiennent a l'appelant, qui doit les detruire.
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetIconInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetIconInfo(IntPtr hIcon, out IconInfo pIconInfo);

    /// <summary>
    /// Detruit une icone.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>DestroyIcon</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-destroyicon
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Retourne une metrique du systeme.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetSystemMetrics</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getsystemmetrics
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetSystemMetrics")]
    private static partial int GetSystemMetrics(int nIndex);

    /// <summary>
    /// Obtient le contexte de peripherique de l'ecran.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetDC</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getdc
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetDC")]
    private static partial IntPtr GetDC(IntPtr hWnd);

    /// <summary>
    /// Rend un contexte de peripherique obtenu par <c>GetDC</c>.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>ReleaseDC</c>, user32.dll, en-tete winuser.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-releasedc
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "ReleaseDC")]
    private static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    /// <summary>
    /// Cree un bitmap dont l'appelant peut adresser directement les pixels.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>CreateDIBSection</c>, gdi32.dll, en-tete wingdi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-createdibsection
    /// </remarks>
    [LibraryImport("gdi32.dll", EntryPoint = "CreateDIBSection")]
    private static partial IntPtr CreateDIBSection(
        IntPtr hdc, BitmapInfo* pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

    /// <summary>
    /// Recopie les pixels d'un bitmap dans un tampon au format demande.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>GetDIBits</c>, gdi32.dll, en-tete wingdi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-getdibits
    /// </remarks>
    [LibraryImport("gdi32.dll", EntryPoint = "GetDIBits")]
    private static partial int GetDIBits(
        IntPtr hdc, IntPtr hbm, uint start, uint lines, void* bits, BitmapInfo* lpbmi, uint usage);

    /// <summary>
    /// Detruit un objet GDI.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>DeleteObject</c>, gdi32.dll, en-tete wingdi.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-deleteobject
    /// </remarks>
    [LibraryImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr ho);

    /// <summary>Cote, en pixels, des images a poser dans un menu.</summary>
    /// <returns>La taille d'une petite icone systeme, jamais inferieure a 16.</returns>
    internal static int MenuImageSize()
    {
        try
        {
            int size = GetSystemMetrics(SmCxSmIcon);
            return size >= 16 ? size : 16;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return 16;
        }
    }

    /// <summary>
    /// Pose une image sur un element de menu.
    /// </summary>
    /// <param name="menu">Le menu contenant l'element.</param>
    /// <param name="position">Position de l'element, a partir de zero.</param>
    /// <param name="bitmap">Le bitmap a poser. <see cref="IntPtr.Zero"/> ne fait rien.</param>
    /// <remarks>
    /// Le menu ne prend pas possession du bitmap : celui-ci doit rester valide tant que le menu
    /// peut etre affiche, d'ou le cache permanent de <see cref="Commands.MenuIcons"/>.
    /// </remarks>
    internal static void SetMenuItemBitmap(IntPtr menu, uint position, IntPtr bitmap)
    {
        if (menu == IntPtr.Zero || bitmap == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var info = new MenuItemInfo
            {
                Size = (uint)sizeof(MenuItemInfo),
                Mask = MiimBitmap,
                ItemBitmap = bitmap,
            };

            SetMenuItemInfo(menu, position, fByPosition: true, in info);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            // Une puce manquante n'est pas une raison de priver l'utilisateur du menu.
        }
    }

    /// <summary>
    /// Charge un <c>.ico</c> et le convertit en bitmap 32 bits a alpha premultiplie.
    /// </summary>
    /// <param name="iconPath">Chemin du fichier icone.</param>
    /// <param name="size">Cote demande, en pixels.</param>
    /// <returns>
    /// Un <c>HBITMAP</c> dont l'appelant devient proprietaire, ou <see cref="IntPtr.Zero"/> si
    /// l'icone est introuvable, illisible, ou depourvue de canal alpha.
    /// </returns>
    /// <remarks>
    /// Les menus Windows attendent un bitmap 32 bits <b>a alpha premultiplie</b>. On ne passe donc
    /// pas par <c>DrawIconEx</c>, dont le resultat depend de la maniere dont GDI compose : on lit
    /// les pixels de l'icone tels quels avec <c>GetDIBits</c> et on premultiplie soi-meme.
    /// </remarks>
    internal static IntPtr CreatePremultipliedBitmap(string iconPath, int size)
    {
        if (string.IsNullOrEmpty(iconPath) || size <= 0)
        {
            return IntPtr.Zero;
        }

        IntPtr icon = IntPtr.Zero;
        IntPtr screen = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IconInfo info = default;
        bool success = false;

        try
        {
            icon = LoadImage(IntPtr.Zero, iconPath, ImageIcon, size, size, LrLoadFromFile);
            if (icon == IntPtr.Zero || !GetIconInfo(icon, out info) || info.ColorBitmap == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            BitmapInfo description = DescribeTopDown(size);
            bitmap = CreateDIBSection(IntPtr.Zero, &description, DibRgbColors, out IntPtr pixels, IntPtr.Zero, 0);

            if (bitmap == IntPtr.Zero || pixels == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            screen = GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // La hauteur negative de l'en-tete demande une ecriture de haut en bas.
            BitmapInfo request = DescribeTopDown(size);
            if (GetDIBits(screen, info.ColorBitmap, 0, (uint)size, (void*)pixels, &request, DibRgbColors) == 0)
            {
                return IntPtr.Zero;
            }

            success = Premultiply((byte*)pixels, size * size);
            return success ? bitmap : IntPtr.Zero;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return IntPtr.Zero;
        }
        finally
        {
            if (screen != IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, screen);
            }

            if (info.MaskBitmap != IntPtr.Zero)
            {
                DeleteObject(info.MaskBitmap);
            }

            if (info.ColorBitmap != IntPtr.Zero)
            {
                DeleteObject(info.ColorBitmap);
            }

            if (icon != IntPtr.Zero)
            {
                DestroyIcon(icon);
            }

            if (!success && bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }
        }
    }

    /// <summary>Decrit un DIB 32 bits carre, oriente de haut en bas.</summary>
    /// <param name="size">Cote, en pixels.</param>
    /// <returns>La description a passer aux fonctions GDI.</returns>
    private static BitmapInfo DescribeTopDown(int size) => new()
    {
        Header = new BitmapInfoHeader
        {
            Size = (uint)sizeof(BitmapInfoHeader),
            Width = size,

            // Hauteur negative : origine en haut a gauche, comme le reste du projet.
            Height = -size,
            Planes = 1,
            BitCount = 32,
            Compression = BiRgb,
        },
    };

    /// <summary>
    /// Premultiplie les composantes de couleur par l'alpha, sur place.
    /// </summary>
    /// <param name="pixels">Tampon BGRA, non premultiplie.</param>
    /// <param name="count">Nombre de pixels.</param>
    /// <returns>
    /// <see langword="false"/> si l'icone n'a aucun canal alpha : le bitmap serait alors
    /// entierement transparent une fois pose dans un menu, et mieux vaut aucune puce.
    /// </returns>
    private static bool Premultiply(byte* pixels, int count)
    {
        bool hasAlpha = false;

        for (int i = 0; i < count; i++)
        {
            if (pixels[(i * 4) + 3] != 0)
            {
                hasAlpha = true;
                break;
            }
        }

        if (!hasAlpha)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            byte* pixel = pixels + (i * 4);
            uint alpha = pixel[3];

            pixel[0] = (byte)(((pixel[0] * alpha) + 127) / 255);
            pixel[1] = (byte)(((pixel[1] * alpha) + 127) / 255);
            pixel[2] = (byte)(((pixel[2] * alpha) + 127) / 255);
        }

        return true;
    }

    /// <summary>
    /// Construit un <c>IShellItemArray</c> a partir de l'objet de donnees d'une selection.
    /// </summary>
    /// <remarks>
    /// Win32 : <c>SHCreateShellItemArrayFromDataObject</c>, shobjidl_core.h.
    /// Doc : https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-shcreateshellitemarrayfromdataobject
    /// C'est ce qui permet au handler herite de reutiliser exactement le meme code de lecture de
    /// selection que la commande moderne.
    /// </remarks>
    [LibraryImport("shell32.dll", EntryPoint = "SHCreateShellItemArrayFromDataObject")]
    internal static partial int SHCreateShellItemArrayFromDataObject(IntPtr pdo, in Guid riid, out IntPtr ppv);

    /// <summary>
    /// Dossier contenant <c>FolderHue.Shell.dll</c>.
    /// </summary>
    /// <returns>Le dossier de la DLL, ou <see langword="null"/> s'il n'a pas pu etre determine.</returns>
    /// <remarks>
    /// <c>AppContext.BaseDirectory</c> ne convient pas : charge dans un processus hote, il rend le
    /// dossier de <b>l'hote</b> — <c>C:\Windows\System32</c> en pratique — et non celui de la DLL.
    /// On remonte donc au module a partir de l'adresse d'une de nos propres fonctions.
    /// </remarks>
    internal static string? GetModuleDirectory()
    {
        try
        {
            // Une fonction exportee de ce module sert de point de repere.
            delegate* unmanaged<int> anchor = &Exports.DllCanUnloadNow;

            if (!GetModuleHandleEx(
                    GetModuleHandleExFlagFromAddress | GetModuleHandleExFlagUnchangedRefcount,
                    (IntPtr)anchor,
                    out IntPtr module)
                || module == IntPtr.Zero)
            {
                return null;
            }

            const int capacity = 32768; // longueur maximale d'un chemin etendu
            char[] buffer = new char[capacity];

            fixed (char* pointer = buffer)
            {
                uint length = GetModuleFileName(module, pointer, capacity);

                if (length == 0 || length >= capacity)
                {
                    return null;
                }

                return Path.GetDirectoryName(new string(pointer, 0, (int)length));
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }
}
