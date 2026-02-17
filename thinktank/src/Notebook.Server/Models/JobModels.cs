using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record JobResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("job_type")]
    public required string JobType { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("payload")]
    public required JsonElement Payload { get; init; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }

    [JsonPropertyName("claimed_at")]
    public DateTimeOffset? ClaimedAt { get; init; }

    [JsonPropertyName("claimed_by")]
    public string? ClaimedBy { get; init; }
}

public sealed record CompleteJobRequest
{
    [JsonPropertyName("worker_id")]
    public required string WorkerId { get; init; }

    [JsonPropertyName("result")]
    public required JsonElement Result { get; init; }
}

public sealed record FailJobRequest
{
    [JsonPropertyName("worker_id")]
    public required string WorkerId { get; init; }

    [JsonPropertyName("error")]
    public required string Error { get; init; }
}

public sealed record JobTypeStats
{
    [JsonPropertyName("pending")]
    public long Pending { get; init; }

    [JsonPropertyName("in_progress")]
    public long InProgress { get; init; }

    [JsonPropertyName("completed")]
    public long Completed { get; init; }

    [JsonPropertyName("failed")]
    public long Failed { get; init; }
}

public sealed record JobStatsResponse
{
    [JsonPropertyName("DISTILL_CLAIMS")]
    public required JobTypeStats DistillClaims { get; init; }

    [JsonPropertyName("COMPARE_CLAIMS")]
    public required JobTypeStats CompareClaims { get; init; }

    [JsonPropertyName("CLASSIFY_TOPIC")]
    public required JobTypeStats ClassifyTopic { get; init; }

    [JsonPropertyName("EMBED_CLAIMS")]
    public required JobTypeStats EmbedClaims { get; init; }
}
