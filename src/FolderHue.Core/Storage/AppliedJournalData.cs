using System.Text.Json.Serialization;

namespace FolderHue.Core.Storage;

/// <summary>
/// Serialised contents of <c>applied.json</c>.
/// </summary>
public sealed class AppliedJournalData
{
    /// <summary>Schema version, so that a later migration remains possible.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The folders currently colored.</summary>
    public List<AppliedEntry> Entries { get; set; } = [];
}

/// <summary>
/// Serialisation context generated at compile time.
/// </summary>
/// <remarks>
/// The reflection-based serialisation of <c>System.Text.Json</c> is not NativeAOT-compatible, and
/// the shell reads this journal from inside <c>explorer.exe</c>: a source-generated context is
/// required (CLAUDE.md 2.1).
/// </remarks>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppliedJournalData))]
internal sealed partial class AppliedJournalJsonContext : JsonSerializerContext;
