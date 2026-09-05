using System.Text;

namespace FolderHue.Core.Folders;

/// <summary>
/// Model of a <c>desktop.ini</c> file, preserving order, comments and formatting.
/// </summary>
/// <remarks>
/// Many folders already have a <c>desktop.ini</c> carrying <c>FolderType</c>,
/// <c>LocalizedResourceName</c> or a custom view. We <b>merge</b>, we never overwrite
/// (CLAUDE.md 6.1). Hence a line-based model rather than a dictionary: everything we do not
/// change is written back exactly as it was.
/// </remarks>
public sealed class DesktopIni
{
    /// <summary>Standard section carrying the icon customisation.</summary>
    public const string ShellClassInfoSection = ".ShellClassInfo";

    /// <summary>Key naming the folder icon.</summary>
    public const string IconResourceKey = "IconResource";

    private readonly List<Line> _lines;

    private DesktopIni(List<Line> lines) => _lines = lines;

    /// <summary>Creates an empty file.</summary>
    public DesktopIni()
        : this([])
    {
    }

    /// <summary>Parses the textual content of a <c>desktop.ini</c>.</summary>
    /// <param name="text">The file content. <see langword="null"/> is treated as empty.</param>
    /// <returns>The matching model.</returns>
    public static DesktopIni Parse(string? text)
    {
        var lines = new List<Line>();

        if (string.IsNullOrEmpty(text))
        {
            return new DesktopIni(lines);
        }

        string? currentSection = null;

        foreach (string raw in SplitLines(text))
        {
            string trimmed = raw.Trim();

            if (trimmed.Length == 0 || trimmed[0] == ';' || trimmed[0] == '#')
            {
                lines.Add(Line.MakeOther(raw, currentSection));
                continue;
            }

            if (trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
            {
                currentSection = trimmed[1..^1].Trim();
                lines.Add(Line.MakeSection(raw, currentSection));
                continue;
            }

            int separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                lines.Add(Line.MakeOther(raw, currentSection));
                continue;
            }

            string key = trimmed[..separator].Trim();
            string value = trimmed[(separator + 1)..].Trim();
            lines.Add(Line.MakeKeyValue(raw, currentSection, key, value));
        }

        return new DesktopIni(lines);
    }

    /// <summary>Indicates whether the file holds no section and no key at all.</summary>
    public bool IsEmpty
    {
        get
        {
            foreach (Line line in _lines)
            {
                if (line.Kind != LineKind.Other)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Reads a value.</summary>
    /// <param name="section">Section name, without brackets.</param>
    /// <param name="key">Key name.</param>
    /// <returns>The value, or <see langword="null"/> when the key is absent.</returns>
    public string? GetValue(string section, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(section);
        ArgumentException.ThrowIfNullOrEmpty(key);

        foreach (Line line in _lines)
        {
            if (line.Matches(section, key))
            {
                return line.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Writes a value, creating the section or the key when needed.
    /// </summary>
    /// <param name="section">Section name, without brackets.</param>
    /// <param name="key">Key name.</param>
    /// <param name="value">The value to write.</param>
    /// <remarks>
    /// When the key already exists only its value changes: its original casing and its position in
    /// the file are preserved. Otherwise the key is appended to the end of its section, and the
    /// section is created at the end of the file when it does not exist.
    /// </remarks>
    public void SetValue(string section, string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(section);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        for (int i = 0; i < _lines.Count; i++)
        {
            if (_lines[i].Matches(section, key))
            {
                _lines[i] = _lines[i].WithValue(value);
                return;
            }
        }

        int insertAt = FindEndOfSection(section);

        if (insertAt < 0)
        {
            if (_lines.Count > 0)
            {
                _lines.Add(Line.MakeOther(string.Empty, section));
            }

            _lines.Add(Line.MakeSection("[" + section + "]", section));
            _lines.Add(Line.MakeKeyValue(key + "=" + value, section, key, value));
            return;
        }

        _lines.Insert(insertAt, Line.MakeKeyValue(key + "=" + value, section, key, value));
    }

    /// <summary>Removes a key.</summary>
    /// <param name="section">Section name, without brackets.</param>
    /// <param name="key">Key name.</param>
    /// <returns><see langword="true"/> when the key existed.</returns>
    public bool RemoveValue(string section, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(section);
        ArgumentException.ThrowIfNullOrEmpty(key);

        for (int i = 0; i < _lines.Count; i++)
        {
            if (_lines[i].Matches(section, key))
            {
                _lines.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>Removes a section when it no longer holds any key.</summary>
    /// <param name="section">Section name, without brackets.</param>
    /// <returns><see langword="true"/> when the section was removed.</returns>
    public bool RemoveSectionIfEmpty(string section)
    {
        ArgumentException.ThrowIfNullOrEmpty(section);

        int headerIndex = -1;

        for (int i = 0; i < _lines.Count; i++)
        {
            Line line = _lines[i];

            if (line.Kind == LineKind.Section && SameName(line.Section, section))
            {
                headerIndex = i;
                continue;
            }

            if (line.Kind == LineKind.KeyValue && SameName(line.Section, section))
            {
                return false;
            }
        }

        if (headerIndex < 0)
        {
            return false;
        }

        _lines.RemoveAt(headerIndex);
        return true;
    }

    /// <summary>
    /// Indicates whether every key in the file belongs to us.
    /// </summary>
    /// <param name="ownedKeys">The section / key pairs we write.</param>
    /// <returns>
    /// <see langword="true"/> when the file holds no foreign key. A file with no key at all
    /// returns <see langword="true"/>.
    /// </returns>
    /// <remarks>
    /// This test is what allows deleting <c>desktop.ini</c> outright on a reset. Otherwise the
    /// file is kept, lightened of our keys only (CLAUDE.md 6.3).
    /// </remarks>
    public bool ContainsOnlyKeys(IReadOnlyCollection<(string Section, string Key)> ownedKeys)
    {
        ArgumentNullException.ThrowIfNull(ownedKeys);

        foreach (Line line in _lines)
        {
            if (line.Kind != LineKind.KeyValue)
            {
                continue;
            }

            bool owned = false;
            foreach ((string section, string key) in ownedKeys)
            {
                if (line.Matches(section, key))
                {
                    owned = true;
                    break;
                }
            }

            if (!owned)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Renders the file back to text.</summary>
    /// <returns>The complete text, lines separated by CRLF.</returns>
    /// <remarks>
    /// CRLF and not LF: <c>desktop.ini</c> is read by legacy Win32 APIs that cope badly with Unix
    /// line endings.
    /// </remarks>
    public string ToText()
    {
        var builder = new StringBuilder();

        foreach (Line line in _lines)
        {
            builder.Append(line.Raw).Append("\r\n");
        }

        return builder.ToString();
    }

    private int FindEndOfSection(string section)
    {
        int headerIndex = -1;
        int lastKeyIndex = -1;

        for (int i = 0; i < _lines.Count; i++)
        {
            Line line = _lines[i];

            if (line.Kind == LineKind.Section && SameName(line.Section, section))
            {
                headerIndex = i;
                lastKeyIndex = i;
                continue;
            }

            if (headerIndex >= 0 && line.Kind == LineKind.Section)
            {
                break;
            }

            if (headerIndex >= 0 && line.Kind == LineKind.KeyValue)
            {
                lastKeyIndex = i;
            }
        }

        return headerIndex < 0 ? -1 : lastKeyIndex + 1;
    }

    private static bool SameName(string? left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitLines(string text)
    {
        // Files found in the wild sometimes mix CRLF and LF: split by hand rather than trusting a
        // single separator.
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n' && text[i] != '\r')
            {
                continue;
            }

            yield return text[start..i];

            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }

            start = i + 1;
        }

        if (start < text.Length)
        {
            yield return text[start..];
        }
    }

    private enum LineKind
    {
        Other,
        Section,
        KeyValue,
    }

    private readonly record struct Line(LineKind Kind, string Raw, string? Section, string? Key, string? Value)
    {
        internal static Line MakeOther(string raw, string? section) => new(LineKind.Other, raw, section, null, null);

        internal static Line MakeSection(string raw, string name) => new(LineKind.Section, raw, name, null, null);

        internal static Line MakeKeyValue(string raw, string? section, string key, string value)
            => new(LineKind.KeyValue, raw, section, key, value);

        internal bool Matches(string section, string key)
            => Kind == LineKind.KeyValue && SameName(Section, section) && SameName(Key, key);

        internal Line WithValue(string value) => this with { Raw = Key + "=" + value, Value = value };
    }
}
