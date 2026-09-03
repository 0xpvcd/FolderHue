using System.Text.Json.Serialization;

namespace FolderHue.Core.Storage;

/// <summary>
/// Contenu serialise de <c>applied.json</c>.
/// </summary>
public sealed class AppliedJournalData
{
    /// <summary>Version du schema, pour permettre une migration ulterieure.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Les dossiers actuellement colorises.</summary>
    public List<AppliedEntry> Entries { get; set; } = [];
}

/// <summary>
/// Contexte de serialisation genere a la compilation.
/// </summary>
/// <remarks>
/// La serialisation par reflexion de <c>System.Text.Json</c> n'est pas compatible NativeAOT : le
/// shell, qui lit ce journal depuis <c>explorer.exe</c>, exige un contexte source-genere
/// (CLAUDE.md §2.1).
/// </remarks>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppliedJournalData))]
internal sealed partial class AppliedJournalJsonContext : JsonSerializerContext;
