using System.Text.Json.Serialization;

namespace Notebook.Core.Types;

/// <summary>
/// Summary of an entry returned by the filtered browse endpoint.
/// </summary>
public sealed record BrowseEntry
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("claims_status")]
    public string ClaimsStatus { get; init; } = "pending";

    [JsonPropertyName("max_friction")]
    public double? MaxFriction { get; init; }

    [JsonPropertyName("needs_review")]
    public bool NeedsReview { get; init; }

    [JsonPropertyName("sequence")]
    public long Sequence { get; init; }

    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; init; }

    [JsonPropertyName("author_id")]
    public string AuthorId { get; init; } = "";

    [JsonPropertyName("claim_count")]
    public int ClaimCount { get; init; }
}
