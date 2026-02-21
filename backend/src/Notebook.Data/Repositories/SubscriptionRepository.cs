using Microsoft.EntityFrameworkCore;
using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public class SubscriptionRepository(NotebookDbContext db) : ISubscriptionRepository
{
    public async Task<SubscriptionEntity> CreateAsync(SubscriptionEntity subscription, CancellationToken ct)
    {
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);
        return subscription;
    }

    public async Task<SubscriptionEntity?> GetAsync(Guid subscriptionId, CancellationToken ct)
    {
        return await db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
    }

    public async Task<List<SubscriptionEntity>> ListBySubscriberAsync(Guid subscriberNotebookId, CancellationToken ct)
    {
        return await db.Subscriptions
            .Where(s => s.SubscriberId == subscriberNotebookId)
            .OrderByDescending(s => s.Created)
            .ToListAsync(ct);
    }

    public async Task<List<SubscriptionEntity>> ListBySourceAsync(Guid sourceNotebookId, CancellationToken ct)
    {
        return await db.Subscriptions
            .Where(s => s.SourceId == sourceNotebookId)
            .OrderByDescending(s => s.Created)
            .ToListAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid subscriptionId, CancellationToken ct)
    {
        var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
        if (sub is null) return false;

        db.Subscriptions.Remove(sub);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task UpdateSyncStateAsync(Guid subscriptionId, long watermark, int mirroredCount, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE notebook_subscriptions
            SET sync_watermark = {0}, mirrored_count = {1}, last_sync_at = NOW(), sync_status = 'idle', sync_error = NULL
            WHERE id = {2}
            """,
            [watermark, mirroredCount, subscriptionId],
            ct);
    }

    public async Task SetSyncStatusAsync(Guid subscriptionId, string status, string? error, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync(
            $"UPDATE notebook_subscriptions SET sync_status = {status}, sync_error = {error} WHERE id = {subscriptionId}",
            ct);
    }

    public async Task<List<SubscriptionEntity>> GetDueForSyncAsync(int limit, CancellationToken ct)
    {
        return await db.Subscriptions.FromSqlRaw(
            """
            SELECT * FROM notebook_subscriptions
            WHERE sync_status != 'suspended'
              AND (last_sync_at IS NULL OR last_sync_at + poll_interval_s * interval '1 second' < NOW())
            ORDER BY last_sync_at ASC NULLS FIRST
            LIMIT {0}
            """,
            limit).ToListAsync(ct);
    }

    public async Task<bool> WouldCreateCycleAsync(Guid subscriberId, Guid sourceId, CancellationToken ct)
    {
        // BFS: starting from sourceId, follow subscriber_id -> source_id edges.
        // If we reach subscriberId, adding subscriberId->sourceId would create a cycle.
        var visited = new HashSet<Guid> { sourceId };
        var queue = new Queue<Guid>();
        queue.Enqueue(sourceId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            // What does `current` subscribe to? (current is subscriber_id, get source_ids)
            var sources = await db.Subscriptions
                .Where(s => s.SubscriberId == current)
                .Select(s => s.SourceId)
                .ToListAsync(ct);

            foreach (var src in sources)
            {
                if (src == subscriberId)
                    return true; // cycle detected

                if (visited.Add(src))
                    queue.Enqueue(src);
            }
        }

        return false;
    }

    public async Task<bool> ExistsAsync(Guid subscriberId, Guid sourceId, CancellationToken ct)
    {
        return await db.Subscriptions
            .AnyAsync(s => s.SubscriberId == subscriberId && s.SourceId == sourceId, ct);
    }
}
