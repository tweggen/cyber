using System.Text.Json.Serialization;
using Notebook.Core.Types;

namespace Notebook.Server.Models;

public sealed record SearchResponse
{
    [JsonPropertyName("results")]
    public required List<SearchResult> Results { get; init; }
}

public sealed record SemanticSearchRequest
{
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("top_k")]
    public int TopK { get; init; } = 10;

    [JsonPropertyName("min_similarity")]
    public double MinSimilarity { get; init; } = 0.3;
}

public sealed record SemanticSearchResponse
{
    [JsonPropertyName("results")]
    public required List<SemanticSearchResult> Results { get; init; }
}

public sealed record ClaimsBatchRequest
{
    [JsonPropertyName("entry_ids")]
    public required List<Guid> EntryIds { get; init; }
}

public sealed record ClaimsBatchResponse
{
    [JsonPropertyName("entries")]
    public required List<ClaimsBatchEntry> Entries { get; init; }
}
