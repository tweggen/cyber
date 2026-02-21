using Microsoft.EntityFrameworkCore;
using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public class MirroredContentRepository(NotebookDbContext db) : IMirroredContentRepository
{
    public async Task<MirroredClaimEntity> UpsertMirroredClaimAsync(MirroredClaimEntity claim, CancellationToken ct)
    {
        var existing = await db.MirroredClaims
            .FirstOrDefaultAsync(m => m.SubscriptionId == claim.SubscriptionId
                && m.SourceEntryId == claim.SourceEntryId, ct);

        if (existing is not null)
        {
            existing.Claims = claim.Claims;
            existing.Topic = claim.Topic;
            existing.SourceSequence = claim.SourceSequence;
            existing.Tombstoned = false;
            existing.MirroredAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        db.MirroredClaims.Add(claim);
        await db.SaveChangesAsync(ct);
        return claim;
    }

    public async Task<MirroredEntryEntity> UpsertMirroredEntryAsync(MirroredEntryEntity entry, CancellationToken ct)
    {
        var existing = await db.MirroredEntries
            .FirstOrDefaultAsync(m => m.SubscriptionId == entry.SubscriptionId
                && m.SourceEntryId == entry.SourceEntryId, ct);

        if (existing is not null)
        {
            existing.Content = entry.Content;
            existing.ContentType = entry.ContentType;
            existing.Topic = entry.Topic;
            existing.SourceSequence = entry.SourceSequence;
            existing.Tombstoned = false;
            existing.MirroredAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        db.MirroredEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task TombstoneAsync(Guid subscriptionId, Guid sourceEntryId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE mirrored_claims SET tombstoned = true WHERE subscription_id = {0} AND source_entry_id = {1}",
            [subscriptionId, sourceEntryId], ct);

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE mirrored_entries SET tombstoned = true WHERE subscription_id = {0} AND source_entry_id = {1}",
            [subscriptionId, sourceEntryId], ct);
    }

    public async Task UpdateEmbeddingAsync(Guid mirroredClaimId, double[] embedding, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE mirrored_claims SET embedding = @embedding WHERE id = @id";
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("embedding", embedding));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("id", mirroredClaimId));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> CountBySubscriptionAsync(Guid subscriptionId, CancellationToken ct)
    {
        return await db.MirroredClaims
            .CountAsync(m => m.SubscriptionId == subscriptionId && !m.Tombstoned, ct);
    }
}
