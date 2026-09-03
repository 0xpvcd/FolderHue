namespace FolderHue.Core.Folders;

/// <summary>
/// Issue d'une operation sur un dossier.
/// </summary>
/// <remarks>
/// Les operations de <see cref="FolderCustomizer"/> ne levent pas : elles sont appelees depuis
/// <c>explorer.exe</c>, ou une exception non geree ferait tomber l'Explorateur (CLAUDE.md §6.5).
/// L'echec est donc une valeur de retour, assortie d'une cle de ressource affichable.
/// </remarks>
/// <param name="Success"><see langword="true"/> si l'operation a abouti.</param>
/// <param name="ReasonKey">
/// Cle de <c>Strings.resx</c> decrivant l'echec, ou <see langword="null"/> en cas de succes.
/// </param>
/// <param name="Detail">
/// Complement technique destine au journal, jamais affiche tel quel a l'utilisateur.
/// </param>
public readonly record struct OperationResult(bool Success, string? ReasonKey, string? Detail = null)
{
    /// <summary>Une operation reussie.</summary>
    public static OperationResult Ok { get; } = new(true, null);

    /// <summary>Construit un echec.</summary>
    /// <param name="reasonKey">Cle de ressource decrivant la cause.</param>
    /// <param name="detail">Complement technique pour le journal.</param>
    /// <returns>Le resultat correspondant.</returns>
    public static OperationResult Failed(string reasonKey, string? detail = null) => new(false, reasonKey, detail);
}
