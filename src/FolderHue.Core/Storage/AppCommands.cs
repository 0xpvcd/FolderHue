namespace FolderHue.Core.Storage;

/// <summary>
/// The <c>FolderHue.App</c> command line, as the context menu and the installer call it.
/// </summary>
/// <remarks>
/// The shell delegates to the application everything it must not do itself: generating an icon,
/// showing a dialog (CLAUDE.md 4.3, 6.5). Both projects therefore reference the same constants
/// rather than repeating the strings on either side — a one-character drift would show up as a
/// click that does nothing, with no message at all.
/// </remarks>
public static class AppCommands
{
    /// <summary>Pre-generates the whole palette, with no UI.</summary>
    public const string Pregenerate = "--pregenerate";

    /// <summary>Forces regeneration even when the files already exist.</summary>
    public const string Force = "--force";

    /// <summary>Resets every folder listed in the journal, with no UI.</summary>
    public const string ResetAll = "--reset-all";

    /// <summary>Reports how many folders the exclusion list refused.</summary>
    public const string ReportSkipped = "--report-skipped";

    /// <summary>Declares the context menu in the current user's registry.</summary>
    /// <remarks>
    /// The installer calls this switch rather than writing the keys itself, so that repair and
    /// uninstall go through exactly the same code — the code the tests cover
    /// (<see cref="ShellRegistration"/>).
    /// </remarks>
    public const string Register = "--register";

    /// <summary>Removes the keys <see cref="Register"/> wrote.</summary>
    public const string Unregister = "--unregister";

    /// <summary>Writes the brand logo, as an <c>.ico</c>, wherever it is asked to.</summary>
    /// <remarks>
    /// This serves the build: the executable and the installer both need an <c>.ico</c> before the
    /// application exists, which rules out producing it on the fly. The resulting file is versioned
    /// under <c>installer/</c> and regenerated through this switch whenever the logo changes.
    /// </remarks>
    public const string ExportIcon = "--export-icon";

    /// <summary>
    /// Regenerates whatever is missing, then applies the requested operation.
    /// </summary>
    /// <remarks>
    /// Expected form: <c>--apply &lt;color&gt; &lt;emblem&gt; &lt;folder&gt;…</c>, where
    /// <see cref="Absent"/> stands for an unspecified color or emblem.
    /// </remarks>
    public const string Apply = "--apply";

    /// <summary>
    /// Marks an unspecified argument.
    /// </summary>
    /// <remarks>
    /// An empty string would not do: <c>ProcessStartInfo.ArgumentList</c> passes it through
    /// correctly, but it is indistinguishable from a value someone forgot. A dash is explicit, and
    /// cannot be a valid color or emblem identifier.
    /// </remarks>
    public const string Absent = "-";
}
