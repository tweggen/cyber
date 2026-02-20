using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public interface ISubscriptionRepository
{
    Task<SubscriptionEntity> CreateAsync(SubscriptionEntity subscription, CancellationToken ct);
    Task<SubscriptionEntity?> GetAsync(Guid subscriptionId, CancellationToken ct);
    Task<List<SubscriptionEntity>> ListBySubscriberAsync(Guid subscriberNotebookId, CancellationToken ct);
    Task<List<SubscriptionEntity>> ListBySourceAsync(Guid sourceNotebookId, CancellationToken ct);
    Task<bool> DeleteAsync(Guid subscriptionId, CancellationToken ct);
    Task UpdateSyncStateAsync(Guid subscriptionId, long watermark, int mirroredCount, CancellationToken ct);
    Task SetSyncStatusAsync(Guid subscriptionId, string status, string? error, CancellationToken ct);
    Task<List<SubscriptionEntity>> GetDueForSyncAsync(int limit, CancellationToken ct);
    Task<bool> WouldCreateCycleAsync(Guid subscriberId, Guid sourceId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid subscriberId, Guid sourceId, CancellationToken ct);
}
