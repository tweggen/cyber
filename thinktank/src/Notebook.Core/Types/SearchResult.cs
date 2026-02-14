using System.Text.Json.Serialization;

namespace Notebook.Core.Types;

/// <summary>
/// A single search result from full-text or claim search.
/// </summary>
public sealed record SearchResult
{
    [JsonPropertyName("entry_id")]
    public Guid EntryId { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("snippet")]
    public string Snippet { get; init; } = "";

    [JsonPropertyName("match_location")]
    public string MatchLocation { get; init; } = "";

    [JsonPropertyName("relevance_score")]
    public double RelevanceScore { get; init; }
}
