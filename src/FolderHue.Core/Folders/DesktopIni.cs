using System.Text;

namespace FolderHue.Core.Folders;

/// <summary>
/// Modele d'un fichier <c>desktop.ini</c> preservant l'ordre, les commentaires et la mise en forme.
/// </summary>
/// <remarks>
/// Beaucoup de dossiers ont deja un <c>desktop.ini</c> portant <c>FolderType</c>,
/// <c>LocalizedResourceName</c> ou une vue personnalisee. On <b>fusionne</b>, jamais on n'ecrase
/// (CLAUDE.md §6.1). D'ou ce modele fonde sur les lignes plutot que sur un dictionnaire : tout ce
/// que nous ne modifions pas est restitue tel quel.
/// </remarks>
public sealed class DesktopIni
{
    /// <summary>Section standard portant la personnalisation d'icone.</summary>
    public const string ShellClassInfoSection = ".ShellClassInfo";

    /// <summary>Cle designant l'icone du dossier.</summary>
    public const string IconResourceKey = "IconResource";

    private readonly List<Line> _lines;

    private DesktopIni(List<Line> lines) => _lines = lines;

    /// <summary>Cree un fichier vide.</summary>
    public DesktopIni()
        : this([])
    {
    }

    /// <summary>Analyse le contenu textuel d'un <c>desktop.ini</c>.</summary>
    /// <param name="text">Le contenu du fichier. <see langword="null"/> est traite comme vide.</param>
    /// <returns>Le modele correspondant.</returns>
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

    /// <summary>Indique si le fichier ne contient aucune section ni aucune cle.</summary>
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

    /// <summary>Lit une valeur.</summary>
    /// <param name="section">Nom de la section, sans crochets.</param>
    /// <param name="key">Nom de la cle.</param>
    /// <returns>La valeur, ou <see langword="null"/> si la cle est absente.</returns>
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
    /// Ecrit une valeur, en creant la section ou la cle si necessaire.
    /// </summary>
    /// <param name="section">Nom de la section, sans crochets.</param>
    /// <param name="key">Nom de la cle.</param>
    /// <param name="value">La valeur a ecrire.</param>
    /// <remarks>
    /// Si la cle existe deja, seule sa valeur change : sa casse d'origine et sa position dans le
    /// fichier sont conservees. Sinon la cle est ajoutee a la fin de sa section, et la section est
    /// creee en fin de fichier si elle n'existe pas.
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

    /// <summary>Supprime une cle.</summary>
    /// <param name="section">Nom de la section, sans crochets.</param>
    /// <param name="key">Nom de la cle.</param>
    /// <returns><see langword="true"/> si la cle existait.</returns>
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

    /// <summary>Supprime une section si elle ne contient plus aucune cle.</summary>
    /// <param name="section">Nom de la section, sans crochets.</param>
    /// <returns><see langword="true"/> si la section a ete supprimee.</returns>
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
    /// Indique si toutes les cles du fichier nous appartiennent.
    /// </summary>
    /// <param name="ownedKeys">Les couples section / cle que nous ecrivons.</param>
    /// <returns>
    /// <see langword="true"/> si le fichier ne contient aucune cle etrangere. Un fichier sans
    /// aucune cle retourne <see langword="true"/>.
    /// </returns>
    /// <remarks>
    /// C'est ce test qui autorise la suppression complete de <c>desktop.ini</c> lors d'une
    /// reinitialisation. Sinon le fichier est conserve, allege de nos seules cles (CLAUDE.md §6.3).
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

    /// <summary>Restitue le contenu textuel du fichier.</summary>
    /// <returns>Le texte complet, lignes separees par CRLF.</returns>
    /// <remarks>
    /// CRLF et non LF : <c>desktop.ini</c> est lu par des API Win32 historiques qui s'accommodent
    /// mal des fins de ligne Unix.
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
        // Les fichiers rencontres melangent parfois CRLF et LF : on decoupe a la main plutot que
        // de se fier a un separateur unique.
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
