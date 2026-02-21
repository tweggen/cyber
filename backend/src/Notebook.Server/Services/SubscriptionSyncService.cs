using System.Text;
using System.Text.Json;
using Notebook.Data.Entities;
using Notebook.Data.Repositories;

namespace Notebook.Server.Services;

public class SubscriptionSyncService(
    IServiceScopeFactory scopeFactory,
    IAuditService auditService,
    ILogger<SubscriptionSyncService> logger) : BackgroundService
{
    private static readonly TimeSpan LoopInterval = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _semaphore = new(10, 10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncDueSubscriptionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Subscription sync loop error");
            }

            await Task.Delay(LoopInterval, stoppingToken);
        }
    }

    private async Task SyncDueSubscriptionsAsync(CancellationToken ct)
    {
        List<SubscriptionEntity> due;
        using (var scope = scopeFactory.CreateScope())
        {
            var subRepo = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
            due = await subRepo.GetDueForSyncAsync(10, ct);
        }

        if (due.Count == 0) return;

        var tasks = due.Select(sub => SyncWithSemaphoreAsync(sub, ct));
        await Task.WhenAll(tasks);
    }

    private async Task SyncWithSemaphoreAsync(SubscriptionEntity subscription, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await SyncOneAsync(subscription, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SyncOneAsync(SubscriptionEntity subscription, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var subRepo = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var entryRepo = scope.ServiceProvider.GetRequiredService<IEntryRepository>();
        var mirroredRepo = scope.ServiceProvider.GetRequiredService<IMirroredContentRepository>();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();

        try
        {
            await subRepo.SetSyncStatusAsync(subscription.Id, "syncing", null, ct);

            // Load source entries with sequence > sync_watermark
            var sourceEntries = await LoadSourceEntriesAsync(
                entryRepo, subscription.SourceId, subscription.SyncWatermark, 100, ct);

            long maxSequence = subscription.SyncWatermark;

            foreach (var entry in sourceEntries)
            {
                if (entry.Sequence > maxSequence)
                    maxSequence = entry.Sequence;

                // Skip fragments — only mirror top-level entries
                if (entry.FragmentOf is not null)
                    continue;

                // Apply topic filter if configured
                if (subscription.TopicFilter is not null
                    && entry.Topic is not null
                    && !entry.Topic.StartsWith(subscription.TopicFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Mirror based on scope
                switch (subscription.Scope)
                {
                    case "catalog":
                        // Lightweight: topic + empty claims
                        await mirroredRepo.UpsertMirroredClaimAsync(new MirroredClaimEntity
                        {
                            Id = Guid.NewGuid(),
                            SubscriptionId = subscription.Id,
                            SourceEntryId = entry.Id,
                            NotebookId = subscription.SubscriberId,
                            Claims = JsonDocument.Parse("[]"),
                            Topic = entry.Topic,
                            SourceSequence = entry.Sequence,
                            MirroredAt = DateTimeOffset.UtcNow,
                        }, ct);
                        break;

                    case "claims":
                    case "entries":
                        // Mirror claims
                        var claimsJson = JsonSerializer.Serialize(entry.Claims);
                        var mirroredClaim = await mirroredRepo.UpsertMirroredClaimAsync(new MirroredClaimEntity
                        {
                            Id = Guid.NewGuid(),
                            SubscriptionId = subscription.Id,
                            SourceEntryId = entry.Id,
                            NotebookId = subscription.SubscriberId,
                            Claims = JsonDocument.Parse(claimsJson),
                            Topic = entry.Topic,
                            SourceSequence = entry.Sequence,
                            MirroredAt = DateTimeOffset.UtcNow,
                        }, ct);

                        // Queue EMBED_MIRRORED job for claims with content
                        if (entry.Claims.Count > 0)
                        {
                            var payload = JsonSerializer.SerializeToDocument(new
                            {
                                mirrored_claim_id = mirroredClaim.Id.ToString(),
                                notebook_id = subscription.SubscriberId.ToString(),
                                claims = entry.Claims.Select(c => new { text = c.Text, confidence = c.Confidence }),
                            });
                            await jobRepo.InsertJobAsync(subscription.SubscriberId, "EMBED_MIRRORED", payload, ct);
                        }

                        // For 'entries' scope, also mirror full content
                        if (subscription.Scope == "entries")
                        {
                            await mirroredRepo.UpsertMirroredEntryAsync(new MirroredEntryEntity
                            {
                                Id = Guid.NewGuid(),
                                SubscriptionId = subscription.Id,
                                SourceEntryId = entry.Id,
                                NotebookId = subscription.SubscriberId,
                                Content = entry.Content,
                                ContentType = entry.ContentType,
                                Topic = entry.Topic,
                                SourceSequence = entry.Sequence,
                                MirroredAt = DateTimeOffset.UtcNow,
                            }, ct);
                        }
                        break;
                }
            }

            var mirroredCount = await mirroredRepo.CountBySubscriptionAsync(subscription.Id, ct);
            await subRepo.UpdateSyncStateAsync(subscription.Id, maxSequence, mirroredCount, ct);

            logger.LogDebug("Synced subscription {SubId}: {Count} entries, watermark {Watermark}",
                subscription.Id, sourceEntries.Count, maxSequence);

            await auditService.LogAsync(new Notebook.Core.Types.AuditEvent
            {
                NotebookId = subscription.SubscriberId,
                Action = "subscription.sync",
                TargetType = "subscription",
                TargetId = subscription.Id.ToString(),
                Detail = System.Text.Json.JsonSerializer.SerializeToDocument(new
                {
                    source_id = subscription.SourceId,
                    watermark = maxSequence,
                    mirrored_count = mirroredCount,
                    entries_synced = sourceEntries.Count,
                }).RootElement.Clone(),
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing subscription {SubId}", subscription.Id);
            await subRepo.SetSyncStatusAsync(subscription.Id, "error", ex.Message, ct);

            await auditService.LogAsync(new Notebook.Core.Types.AuditEvent
            {
                NotebookId = subscription.SubscriberId,
                Action = "subscription.sync.error",
                TargetType = "subscription",
                TargetId = subscription.Id.ToString(),
                Detail = System.Text.Json.JsonSerializer.SerializeToDocument(new
                {
                    source_id = subscription.SourceId,
                    error = ex.Message,
                }).RootElement.Clone(),
            });
        }
    }

    private static async Task<List<Notebook.Core.Types.Entry>> LoadSourceEntriesAsync(
        IEntryRepository entryRepo, Guid sourceNotebookId, long afterSequence, int limit, CancellationToken ct)
    {
        return await entryRepo.GetEntriesAfterSequenceAsync(sourceNotebookId, afterSequence, limit, ct);
    }
}
