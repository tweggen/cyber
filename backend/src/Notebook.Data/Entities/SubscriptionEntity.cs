namespace Notebook.Data.Entities;

public class SubscriptionEntity
{
    public Guid Id { get; set; }
    public Guid SubscriberId { get; set; }
    public Guid SourceId { get; set; }
    public string Scope { get; set; } = "catalog";
    public string? TopicFilter { get; set; }
    public byte[] ApprovedBy { get; set; } = null!;
    public long SyncWatermark { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public string SyncStatus { get; set; } = "idle";
    public string? SyncError { get; set; }
    public int MirroredCount { get; set; }
    public double DiscountFactor { get; set; } = 0.3;
    public int PollIntervalSeconds { get; set; } = 60;
    public string? EmbeddingModel { get; set; }
    public DateTimeOffset Created { get; set; }
}
