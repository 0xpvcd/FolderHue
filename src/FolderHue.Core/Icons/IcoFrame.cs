namespace FolderHue.Core.Icons;

/// <summary>
/// An already-encoded image, ready to be placed inside an ICO container.
/// </summary>
/// <param name="Width">Width in pixels, from 1 to <see cref="IconSizes.MaxSize"/>.</param>
/// <param name="Height">Height in pixels, from 1 to <see cref="IconSizes.MaxSize"/>.</param>
/// <param name="Data">
/// The image bytes: a complete PNG stream when <paramref name="IsPng"/> is
/// <see langword="true"/>, otherwise a DIB as produced by <see cref="DibFrameBuilder"/>.
/// </param>
/// <param name="IsPng">
/// <see langword="true"/> when <paramref name="Data"/> holds a PNG. The ICO container makes no
/// distinction in its header: the consumer recognises the PNG signature instead.
/// </param>
public sealed record IcoFrame(int Width, int Height, byte[] Data, bool IsPng);
