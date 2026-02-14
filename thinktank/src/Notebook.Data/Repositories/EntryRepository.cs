using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Notebook.Core.Types;

namespace Notebook.Data.Repositories;

public class EntryRepository(NotebookDbContext db) : IEntryRepository
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct)
        => db.Database.BeginTransactionAsync(ct);

    public Task<bool> NotebookExistsAsync(Guid notebookId, CancellationToken ct)
        => db.Notebooks.AnyAsync(n => n.Id == notebookId, ct);

    public async Task<Entry> InsertEntryAsync(
        Guid notebookId, byte[] authorId, NewEntry newEntry, CancellationToken ct)
    {
        // Atomically increment the notebook's causal sequence counter
        var sequence = await db.Database
            .SqlQuery<long>(
                $"""
                UPDATE notebooks SET current_sequence = current_sequence + 1
                WHERE id = {notebookId}
                RETURNING current_sequence
                """)
            .SingleAsync(ct);

        var entry = new Entry
        {
            Id = Guid.NewGuid(),
            NotebookId = notebookId,
            Content = Encoding.UTF8.GetBytes(newEntry.Content),
            ContentType = newEntry.ContentType,
            Topic = newEntry.Topic,
            AuthorId = authorId,
            Signature = [], // Batch writes don't carry Ed25519 signatures
            References = newEntry.References,
            FragmentOf = newEntry.FragmentOf,
            FragmentIndex = newEntry.FragmentIndex,
            Sequence = sequence,
            Created = DateTimeOffset.UtcNow,
            IntegrationCost = new IntegrationCost
            {
                EntriesRevised = 0,
                ReferencesBroken = 0,
                CatalogShift = 0.0,
                Orphan = newEntry.References.Count == 0,
            },
        };

        db.Entries.Add(entry);
        await db.SaveChangesAsync(ct);

        return entry;
    }

    public async Task<bool> UpdateEntryClaimsAsync(
        Guid entryId, Guid notebookId, List<Claim> claims, CancellationToken ct)
    {
        var claimsJson = JsonSerializer.Serialize(claims);

        // Atomic update: only succeeds if claims_status is still 'pending'
        var rowsAffected = await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE entries SET claims = {0}::jsonb, claims_status = 'distilled'
            WHERE id = {1} AND notebook_id = {2} AND claims_status = 'pending'
            """,
            [claimsJson, entryId, notebookId],
            ct);

        return rowsAffected > 0;
    }

    public async Task<List<(Guid Id, List<Claim> Claims)>> FindTopicIndicesAsync(
        Guid notebookId, CancellationToken ct)
    {
        var rows = await db.Entries
            .Where(e => e.NotebookId == notebookId
                && e.Topic != null
                && e.Topic.StartsWith("index/topic/")
                && (e.ClaimsStatus == ClaimsStatus.Distilled || e.ClaimsStatus == ClaimsStatus.Verified)
                && e.Claims.Count > 0)
            .Select(e => new { e.Id, e.Claims })
            .ToListAsync(ct);

        return rows.Select(r => (r.Id, r.Claims)).ToList();
    }

    public async Task AppendComparisonAsync(Guid entryId, JsonElement comparison, CancellationToken ct)
    {
        var friction = comparison.TryGetProperty("friction", out var f) ? f.GetDouble() : 0.0;
        var comparisonJson = JsonSerializer.Serialize(comparison);

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE entries SET
              comparisons = comparisons || jsonb_build_array({0}::jsonb),
              max_friction = GREATEST(COALESCE(max_friction, 0.0), {1}),
              needs_review = (GREATEST(COALESCE(max_friction, 0.0), {1}) > 0.2)
            WHERE id = {2}
            """,
            [comparisonJson, friction, entryId],
            ct);
    }

    public async Task UpdateEntryTopicAsync(Guid entryId, string topic, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE entries SET topic = {0} WHERE id = {1}",
            [topic, entryId],
            ct);
    }
}
