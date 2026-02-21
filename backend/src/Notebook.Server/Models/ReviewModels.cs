using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record ReviewResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("notebook_id")]
    public required Guid NotebookId { get; init; }

    [JsonPropertyName("entry_id")]
    public required Guid EntryId { get; init; }

    [JsonPropertyName("submitter")]
    public required string Submitter { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("reviewer")]
    public string? Reviewer { get; init; }

    [JsonPropertyName("reviewed_at")]
    public DateTimeOffset? ReviewedAt { get; init; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }
}

public sealed record ListReviewsResponse
{
    [JsonPropertyName("reviews")]
    public required List<ReviewResponse> Reviews { get; init; }

    [JsonPropertyName("pending_count")]
    public required int PendingCount { get; init; }
}
