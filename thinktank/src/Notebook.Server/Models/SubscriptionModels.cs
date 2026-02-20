using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record CreateSubscriptionRequest
{
    [JsonPropertyName("source_id")]
    public required Guid SourceId { get; init; }

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = "catalog";

    [JsonPropertyName("topic_filter")]
    public string? TopicFilter { get; init; }

    [JsonPropertyName("discount_factor")]
    public double DiscountFactor { get; init; } = 0.3;

    [JsonPropertyName("poll_interval_s")]
    public int PollIntervalSeconds { get; init; } = 60;
}

public sealed record SubscriptionResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("subscriber_id")]
    public required Guid SubscriberId { get; init; }

    [JsonPropertyName("source_id")]
    public required Guid SourceId { get; init; }

    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    [JsonPropertyName("topic_filter")]
    public string? TopicFilter { get; init; }

    [JsonPropertyName("sync_status")]
    public required string SyncStatus { get; init; }

    [JsonPropertyName("sync_watermark")]
    public required long SyncWatermark { get; init; }

    [JsonPropertyName("last_sync_at")]
    public DateTimeOffset? LastSyncAt { get; init; }

    [JsonPropertyName("sync_error")]
    public string? SyncError { get; init; }

    [JsonPropertyName("mirrored_count")]
    public required int MirroredCount { get; init; }

    [JsonPropertyName("discount_factor")]
    public required double DiscountFactor { get; init; }

    [JsonPropertyName("poll_interval_s")]
    public required int PollIntervalSeconds { get; init; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }
}

public sealed record ListSubscriptionsResponse
{
    [JsonPropertyName("subscriptions")]
    public required List<SubscriptionResponse> Subscriptions { get; init; }
}
