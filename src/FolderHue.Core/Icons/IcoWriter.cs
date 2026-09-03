namespace FolderHue.Core.Icons;

/// <summary>
/// Ecrit un conteneur ICO multi-resolution.
/// </summary>
/// <remarks>
/// Aucune bibliotheque graphique n'est capable d'ecrire un ICO contenant a la fois des trames DIB
/// et une trame 256 px encodee en PNG, ce que le shell exige pour les grandes vignettes
/// (CLAUDE.md §4.3). Ce writer assemble donc le conteneur octet par octet.
/// <para>
/// Structure : un <c>ICONDIR</c> de 6 octets, puis un <c>ICONDIRENTRY</c> de 16 octets par trame,
/// puis les donnees des trames dans le meme ordre.
/// </para>
/// </remarks>
public static class IcoWriter
{
    private const int IconDirSize = 6;
    private const int IconDirEntrySize = 16;

    /// <summary>Ecrit les trames dans un flux, au format ICO.</summary>
    /// <param name="destination">Flux ouvert en ecriture. Il n'est pas ferme par cette methode.</param>
    /// <param name="frames">Les trames, dans l'ordre ou elles doivent apparaitre.</param>
    /// <exception cref="ArgumentNullException">Un argument vaut <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">La liste est vide, ou une trame est invalide.</exception>
    public static void Write(Stream destination, IReadOnlyList<IcoFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Count == 0)
        {
            throw new ArgumentException("Un fichier ICO doit contenir au moins une trame.", nameof(frames));
        }

        if (frames.Count > ushort.MaxValue)
        {
            throw new ArgumentException("Un fichier ICO ne peut pas contenir autant de trames.", nameof(frames));
        }

        foreach (IcoFrame frame in frames)
        {
            Validate(frame);
        }

        byte[] header = new byte[IconDirSize + (IconDirEntrySize * frames.Count)];
        Span<byte> span = header;

        BinaryLittleEndian.WriteInt16(span[..2], 0);                    // idReserved
        BinaryLittleEndian.WriteInt16(span.Slice(2, 2), 1);             // idType : 1 = icone
        BinaryLittleEndian.WriteInt16(span.Slice(4, 2), (short)frames.Count);

        int offset = header.Length;
        for (int i = 0; i < frames.Count; i++)
        {
            IcoFrame frame = frames[i];
            Span<byte> entry = span.Slice(IconDirSize + (i * IconDirEntrySize), IconDirEntrySize);

            // 256 px se code par un 0 : le champ ne fait qu'un octet.
            entry[0] = ToDimensionByte(frame.Width);
            entry[1] = ToDimensionByte(frame.Height);
            entry[2] = 0;                                               // bColorCount : 0 = vraies couleurs
            entry[3] = 0;                                               // bReserved
            BinaryLittleEndian.WriteInt16(entry.Slice(4, 2), 1);        // wPlanes
            BinaryLittleEndian.WriteInt16(entry.Slice(6, 2), 32);       // wBitCount
            BinaryLittleEndian.WriteInt32(entry.Slice(8, 4), frame.Data.Length);
            BinaryLittleEndian.WriteInt32(entry.Slice(12, 4), offset);

            offset += frame.Data.Length;
        }

        destination.Write(header, 0, header.Length);

        foreach (IcoFrame frame in frames)
        {
            destination.Write(frame.Data, 0, frame.Data.Length);
        }
    }

    /// <summary>Ecrit les trames dans un fichier, au format ICO.</summary>
    /// <param name="path">Chemin du fichier a creer ou remplacer.</param>
    /// <param name="frames">Les trames, dans l'ordre ou elles doivent apparaitre.</param>
    /// <exception cref="ArgumentNullException">Un argument vaut <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">La liste est vide, ou une trame est invalide.</exception>
    public static void WriteFile(string path, IReadOnlyList<IcoFrame> frames)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using FileStream stream = File.Create(path);
        Write(stream, frames);
    }

    private static void Validate(IcoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.Width is < 1 or > IconSizes.MaxSize || frame.Height is < 1 or > IconSizes.MaxSize)
        {
            throw new ArgumentException(
                $"Dimensions hors limites : {frame.Width}x{frame.Height}. Un ICO accepte 1 a {IconSizes.MaxSize} px.",
                nameof(frame));
        }

        if (frame.Data is null || frame.Data.Length == 0)
        {
            throw new ArgumentException("Une trame ne peut pas etre vide.", nameof(frame));
        }
    }

    private static byte ToDimensionByte(int size) => size >= IconSizes.MaxSize ? (byte)0 : (byte)size;
}
