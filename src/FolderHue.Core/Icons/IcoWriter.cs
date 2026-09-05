namespace FolderHue.Core.Icons;

/// <summary>
/// Writes a multi-resolution ICO container.
/// </summary>
/// <remarks>
/// No graphics library can write an ICO holding both DIB frames and a PNG-encoded 256 px frame,
/// which is what the shell requires for large thumbnails (CLAUDE.md 4.3). This writer therefore
/// assembles the container byte by byte.
/// <para>
/// Layout: a 6-byte <c>ICONDIR</c>, then one 16-byte <c>ICONDIRENTRY</c> per frame, then the frame
/// data in the same order.
/// </para>
/// </remarks>
public static class IcoWriter
{
    private const int IconDirSize = 6;
    private const int IconDirEntrySize = 16;

    /// <summary>Writes the frames to a stream, in ICO format.</summary>
    /// <param name="destination">A writable stream. This method does not close it.</param>
    /// <param name="frames">The frames, in the order they should appear.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The list is empty, or a frame is invalid.</exception>
    public static void Write(Stream destination, IReadOnlyList<IcoFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Count == 0)
        {
            throw new ArgumentException("An ICO file must hold at least one frame.", nameof(frames));
        }

        if (frames.Count > ushort.MaxValue)
        {
            throw new ArgumentException("An ICO file cannot hold that many frames.", nameof(frames));
        }

        foreach (IcoFrame frame in frames)
        {
            Validate(frame);
        }

        byte[] header = new byte[IconDirSize + (IconDirEntrySize * frames.Count)];
        Span<byte> span = header;

        BinaryLittleEndian.WriteInt16(span[..2], 0);                    // idReserved
        BinaryLittleEndian.WriteInt16(span.Slice(2, 2), 1);             // idType: 1 = icon
        BinaryLittleEndian.WriteInt16(span.Slice(4, 2), (short)frames.Count);

        int offset = header.Length;
        for (int i = 0; i < frames.Count; i++)
        {
            IcoFrame frame = frames[i];
            Span<byte> entry = span.Slice(IconDirSize + (i * IconDirEntrySize), IconDirEntrySize);

            // 256 px is encoded as a zero: the field is a single byte.
            entry[0] = ToDimensionByte(frame.Width);
            entry[1] = ToDimensionByte(frame.Height);
            entry[2] = 0;                                               // bColorCount: 0 = true color
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

    /// <summary>Writes the frames to a file, in ICO format.</summary>
    /// <param name="path">Path of the file to create or replace.</param>
    /// <param name="frames">The frames, in the order they should appear.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The list is empty, or a frame is invalid.</exception>
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
                $"Dimensions out of range: {frame.Width}x{frame.Height}. An ICO accepts 1 to {IconSizes.MaxSize} px.",
                nameof(frame));
        }

        if (frame.Data is null || frame.Data.Length == 0)
        {
            throw new ArgumentException("A frame cannot be empty.", nameof(frame));
        }
    }

    private static byte ToDimensionByte(int size) => size >= IconSizes.MaxSize ? (byte)0 : (byte)size;
}
