namespace FolderHue.Core.Folders;

/// <summary>
/// Outcome of an operation on a folder.
/// </summary>
/// <remarks>
/// <see cref="FolderCustomizer"/> operations never throw: they are called from
/// <c>explorer.exe</c>, where an unhandled exception would bring Explorer down (CLAUDE.md 6.5).
/// Failure is therefore a return value, carrying a displayable resource key.
/// </remarks>
/// <param name="Success"><see langword="true"/> when the operation succeeded.</param>
/// <param name="ReasonKey">
/// Key in <c>Strings.resx</c> describing the failure, or <see langword="null"/> on success.
/// </param>
/// <param name="Detail">
/// Technical detail meant for the log, never shown to the user as-is.
/// </param>
public readonly record struct OperationResult(bool Success, string? ReasonKey, string? Detail = null)
{
    /// <summary>A successful operation.</summary>
    public static OperationResult Ok { get; } = new(true, null);

    /// <summary>Builds a failure.</summary>
    /// <param name="reasonKey">Resource key describing the cause.</param>
    /// <param name="detail">Technical detail for the log.</param>
    /// <returns>The matching result.</returns>
    public static OperationResult Failed(string reasonKey, string? detail = null) => new(false, reasonKey, detail);
}
