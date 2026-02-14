using System.Text.Json.Serialization;
using Notebook.Core.Types;

namespace Notebook.Server.Models;

public sealed record BatchEntryRequest
{
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("content_type")]
    public string? ContentType { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("references")]
    public List<Guid>? References { get; init; }

    [JsonPropertyName("fragment_of")]
    public Guid? FragmentOf { get; init; }

    [JsonPropertyName("fragment_index")]
    public int? FragmentIndex { get; init; }
}

public sealed record BatchWriteRequest
{
    [JsonPropertyName("entries")]
    public required List<BatchEntryRequest> Entries { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }
}

public sealed record BatchEntryResult
{
    [JsonPropertyName("entry_id")]
    public required Guid EntryId { get; init; }

    [JsonPropertyName("causal_position")]
    public required long CausalPosition { get; init; }

    [JsonPropertyName("integration_cost")]
    public required double IntegrationCost { get; init; }

    [JsonPropertyName("claims_status")]
    public required ClaimsStatus ClaimsStatus { get; init; }
}

public sealed record BatchWriteResponse
{
    [JsonPropertyName("results")]
    public required List<BatchEntryResult> Results { get; init; }

    [JsonPropertyName("jobs_created")]
    public required int JobsCreated { get; init; }
}
