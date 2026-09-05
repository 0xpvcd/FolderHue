using System.Globalization;
using System.Resources;

namespace FolderHue.Core.Resources;

/// <summary>
/// Access to the project's localised strings.
/// </summary>
/// <remarks>
/// Every string shown to the user goes through here, context menu titles included: no hard-coded
/// literal in the UI, and none in <c>GetTitle()</c> (CLAUDE.md 7).
/// <para>
/// English is the neutral language, embedded in the assembly itself; other languages ship as
/// satellites. Both survive NativeAOT compilation, so the menu follows the Windows display
/// language just as the settings window does.
/// </para>
/// </remarks>
public static class Loc
{
    private static readonly ResourceManager Manager =
        new("FolderHue.Core.Resources.Strings", typeof(Loc).Assembly);

    /// <summary>Returns the string bound to a key.</summary>
    /// <param name="key">Resource name, for instance <c>Menu_Root</c>.</param>
    /// <returns>
    /// The string in the current UI culture. When the key is missing, the key itself is returned:
    /// an odd-looking label beats an exception inside <c>explorer.exe</c>.
    /// </returns>
    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        try
        {
            return Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }
        catch (MissingManifestResourceException)
        {
            return key;
        }
    }

    /// <summary>Returns a formatted string.</summary>
    /// <param name="key">Resource name whose value contains <c>{0}</c> placeholders.</param>
    /// <param name="arguments">The values to inject.</param>
    /// <returns>The formatted string in the current UI culture.</returns>
    public static string Format(string key, params object?[] arguments)
    {
        string template = Get(key);

        try
        {
            return string.Format(CultureInfo.CurrentUICulture, template, arguments);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
