namespace FolderHue.Core.Icons;

/// <summary>
/// Une image, deja encodee, prete a etre placee dans un conteneur ICO.
/// </summary>
/// <param name="Width">Largeur en pixels, de 1 a <see cref="IconSizes.MaxSize"/>.</param>
/// <param name="Height">Hauteur en pixels, de 1 a <see cref="IconSizes.MaxSize"/>.</param>
/// <param name="Data">
/// Les octets de l'image : un flux PNG complet si <paramref name="IsPng"/> vaut
/// <see langword="true"/>, sinon un DIB tel que produit par <see cref="DibFrameBuilder"/>.
/// </param>
/// <param name="IsPng">
/// <see langword="true"/> si <paramref name="Data"/> est un PNG. Le conteneur ICO ne fait aucune
/// difference dans son en-tete : c'est le consommateur qui reconnait la signature PNG.
/// </param>
public sealed record IcoFrame(int Width, int Height, byte[] Data, bool IsPng);
