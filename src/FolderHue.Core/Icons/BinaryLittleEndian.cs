using System.Buffers.Binary;

namespace FolderHue.Core.Icons;

/// <summary>
/// Ecritures petit-boutistes utilisees par les formats ICO et DIB.
/// </summary>
/// <remarks>
/// Ces deux formats sont petit-boutistes par specification, quelle que soit l'architecture : on ne
/// peut donc pas se reposer sur <see cref="BitConverter"/>.
/// </remarks>
internal static class BinaryLittleEndian
{
    internal static void WriteInt16(Span<byte> destination, short value)
        => BinaryPrimitives.WriteInt16LittleEndian(destination, value);

    internal static void WriteInt32(Span<byte> destination, int value)
        => BinaryPrimitives.WriteInt32LittleEndian(destination, value);
}
