using System.Buffers.Binary;
using FolderHue.Core.Icons;
using Xunit;

namespace FolderHue.Core.Tests;

/// <summary>
/// Checks how the multi-resolution ICO container is assembled (CLAUDE.md 4.3).
/// </summary>
public sealed class IcoWriterTests
{
    private static byte[] Write(params IcoFrame[] frames)
    {
        using var stream = new MemoryStream();
        IcoWriter.Write(stream, frames);
        return stream.ToArray();
    }

    private static IcoFrame Dib(int size)
    {
        byte[] pixels = new byte[size * size * 4];
        Array.Fill(pixels, (byte)200);
        return new IcoFrame(size, size, DibFrameBuilder.Build(pixels, size, size), IsPng: false);
    }

    [Fact]
    public void Write_produces_a_valid_ICONDIR_header()
    {
        byte[] ico = Write(Dib(16), Dib(32));

        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(0, 2)));  // idReserved
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(2, 2)));  // idType
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4, 2)));  // idCount
    }

    [Fact]
    public void Write_places_consistent_offsets()
    {
        IcoFrame first = Dib(16);
        IcoFrame second = Dib(32);

        byte[] ico = Write(first, second);

        int headerSize = 6 + (16 * 2);
        int firstSize = BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(6 + 8, 4));
        int firstOffset = BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(6 + 12, 4));
        int secondOffset = BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(6 + 16 + 12, 4));

        Assert.Equal(headerSize, firstOffset);
        Assert.Equal(first.Data.Length, firstSize);
        Assert.Equal(headerSize + first.Data.Length, secondOffset);
        Assert.Equal(headerSize + first.Data.Length + second.Data.Length, ico.Length);
    }

    [Fact]
    public void Write_encodes_256_pixels_as_zero256CommeUneLargeurNulle()
    {
        // The dimension field is a single byte: 256 is encoded there as 0.
        byte[] ico = Write(Dib(256));

        Assert.Equal(0, ico[6]);
        Assert.Equal(0, ico[7]);
    }

    [Fact]
    public void Write_keeps_the_PNG_bytes_as_they_are()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02];
        var frame = new IcoFrame(256, 256, png, IsPng: true);

        byte[] ico = Write(frame);

        Assert.Equal(png, ico.AsSpan(6 + 16).ToArray());
    }

    [Fact]
    public void Write_refuses_an_empty_list()
        => Assert.Throws<ArgumentException>(() => Write());

    [Fact]
    public void Write_refuses_an_out_of_range_frame()
    {
        var frame = new IcoFrame(512, 512, [1, 2, 3], IsPng: true);

        Assert.Throws<ArgumentException>(() => Write(frame));
    }

    [Fact]
    public void Write_refuses_an_empty_frame()
    {
        var frame = new IcoFrame(32, 32, [], IsPng: false);

        Assert.Throws<ArgumentException>(() => Write(frame));
    }

    [Fact]
    public void IconSizes_covers_Details_through_Extra_large_icons()
    {
        Assert.Contains(16, IconSizes.All);
        Assert.Contains(256, IconSizes.All);
        Assert.True(IconSizes.UsePng(256));
        Assert.False(IconSizes.UsePng(128));
    }
}

/// <summary>
/// Checks how DIB frames are built: header, row order and AND mask.
/// </summary>
public sealed class DibFrameBuilderTests
{
    [Fact]
    public void Build_declares_a_doubled_height_and_32_bits32Bits()
    {
        byte[] pixels = new byte[4 * 4 * 4];

        byte[] dib = DibFrameBuilder.Build(pixels, 4, 4);

        Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(0, 4)));   // biSize
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(4, 4)));    // biWidth
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(8, 4)));    // biHeight = 2x
        Assert.Equal(32, BinaryPrimitives.ReadInt16LittleEndian(dib.AsSpan(14, 2)));  // biBitCount
    }

    [Fact]
    public void Build_reverses_the_row_order()
    {
        // 2x2, first row opaque red, second row opaque green.
        byte[] pixels =
        [
            0, 0, 255, 255,  0, 0, 255, 255,
            0, 255, 0, 255,  0, 255, 0, 255,
        ];

        byte[] dib = DibFrameBuilder.Build(pixels, 2, 2);

        // A DIB reads bottom-up: the first row stored is the image's last.
        Assert.Equal(255, dib[40 + 1]);
        Assert.Equal(0, dib[40 + 2]);
    }

    [Fact]
    public void Build_marks_transparent_pixels_in_the_AND_mask()
    {
        // 8x1: the first pixel is transparent, the others opaque.
        byte[] pixels = new byte[8 * 4];
        for (int x = 1; x < 8; x++)
        {
            pixels[(x * 4) + 3] = 255;
        }

        byte[] dib = DibFrameBuilder.Build(pixels, 8, 1);

        int andOffset = 40 + (8 * 1 * 4);
        Assert.Equal(0x80, dib[andOffset]);
    }

    [Fact]
    public void Build_aligns_the_mask_rows_on_four_bytes()
    {
        byte[] pixels = new byte[16 * 16 * 4];

        byte[] dib = DibFrameBuilder.Build(pixels, 16, 16);

        // 16 bits per row = 2 bytes, padded to 4.
        int expected = 40 + (16 * 16 * 4) + (4 * 16);
        Assert.Equal(expected, dib.Length);
    }

    [Fact]
    public void Build_refuses_an_inconsistent_buffer()
        => Assert.Throws<ArgumentException>(() => DibFrameBuilder.Build(new byte[10], 4, 4));

    [Fact]
    public void Build_refuses_an_out_of_range_size()
        => Assert.Throws<ArgumentOutOfRangeException>(() => DibFrameBuilder.Build(new byte[4], 0, 1));
}
