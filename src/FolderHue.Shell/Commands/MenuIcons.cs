namespace FolderHue.Shell.Commands;

/// <summary>
/// Cache des puces du menu contextuel classique.
/// </summary>
/// <remarks>
/// Le menu moderne de Windows 11 se contente d'un chemin de fichier, rendu par
/// <c>IExplorerCommand.GetIcon</c>. Le menu classique, lui, veut un <c>HBITMAP</c> deja
/// rasterise : il faut donc charger le <c>.ico</c>, le convertir et le garder.
/// <para>
/// Les bitmaps ne sont jamais liberes, volontairement. Un menu ne prend pas possession des images
/// qu'on lui pose et peut les redessiner a tout moment : les detruire apres coup exposerait a
/// dessiner un objet GDI mort dans <c>explorer.exe</c>. Le cache plafonne a treize images de
/// quelques kilo-octets, chargees une seule fois pour la duree du processus.
/// </para>
/// </remarks>
internal static class MenuIcons
{
    private static readonly Dictionary<string, IntPtr> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    /// <summary>
    /// Retourne la puce correspondant a un fichier icone.
    /// </summary>
    /// <param name="iconPath">Chemin du <c>.ico</c> pre-genere.</param>
    /// <returns>
    /// Un <c>HBITMAP</c> appartenant au cache, ou <see cref="IntPtr.Zero"/> si l'icone n'a pas pu
    /// etre chargee. L'appelant ne doit jamais le detruire.
    /// </returns>
    /// <remarks>
    /// Le premier appel lit le fichier ; les suivants ne touchent plus le disque. C'est ce qui
    /// permet de respecter l'exigence de rapidite du menu (CLAUDE.md §4.4) tout en affichant des
    /// puces : seule la toute premiere ouverture paie la lecture.
    /// </remarks>
    internal static IntPtr Get(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
        {
            return IntPtr.Zero;
        }

        lock (Gate)
        {
            if (Cache.TryGetValue(iconPath, out IntPtr cached))
            {
                return cached;
            }

            IntPtr bitmap = IntPtr.Zero;

            try
            {
                bitmap = NativeMethods.CreatePremultipliedBitmap(iconPath, NativeMethods.MenuImageSize());
            }
            catch (Exception e)
            {
                ShellServices.Log.Error($"Chargement de la puce « {iconPath} » impossible.", e);
            }

            // L'echec est memorise lui aussi : inutile de retenter une lecture disque a chaque
            // clic droit si l'icone manque.
            Cache[iconPath] = bitmap;
            return bitmap;
        }
    }
}
