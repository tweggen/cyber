namespace Notebook.Core.Types;

using System.Text.Json.Serialization;

/// <summary>
/// Integration cost metrics matching the Rust IntegrationCost struct.
/// Measures how much a new entry disrupts existing knowledge.
/// </summary>
public sealed record IntegrationCost
{
    /// <summary>Number of existing entries that needed revision.</summary>
    [JsonPropertyName("entries_revised")]
    public required uint EntriesRevised { get; init; }

    /// <summary>Number of references that were broken.</summary>
    [JsonPropertyName("references_broken")]
    public required uint ReferencesBroken { get; init; }

    /// <summary>How much the catalog shifted (0.0 to 1.0).</summary>
    [JsonPropertyName("catalog_shift")]
    public required double CatalogShift { get; init; }

    /// <summary>Whether this entry is an orphan (no references to or from).</summary>
    [JsonPropertyName("orphan")]
    public required bool Orphan { get; init; }
}
