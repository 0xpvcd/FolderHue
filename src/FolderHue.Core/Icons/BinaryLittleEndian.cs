using System.Buffers.Binary;

namespace FolderHue.Core.Icons;

/// <summary>
/// Little-endian writes used by the ICO and DIB formats.
/// </summary>
/// <remarks>
/// Both formats are little-endian by specification, whatever the architecture, so
/// <see cref="BitConverter"/> cannot be relied upon.
/// </remarks>
internal static class BinaryLittleEndian
{
    internal static void WriteInt16(Span<byte> destination, short value)
        => BinaryPrimitives.WriteInt16LittleEndian(destination, value);

    internal static void WriteInt32(Span<byte> destination, int value)
        => BinaryPrimitives.WriteInt32LittleEndian(destination, value);
}
