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

    public async Task<List<BrowseEntry>> BrowseFilteredAsync(
        Guid notebookId, BrowseFilter filters, CancellationToken ct)
    {
        var sql = new StringBuilder(
            """
            SELECT id, topic, claims_status, max_friction, needs_review,
                   sequence, created, encode(author_id, 'hex') as author_id,
                   CASE WHEN claims != '[]'::jsonb
                        THEN jsonb_array_length(claims) ELSE 0
                   END as claim_count
            FROM entries WHERE notebook_id = @notebookId
            """);

        var parameters = new List<Npgsql.NpgsqlParameter>
        {
            new("notebookId", notebookId),
        };

        if (filters.TopicPrefix is not null)
        {
            sql.Append(" AND topic LIKE @topicPrefix || '%'");
            parameters.Add(new("topicPrefix", filters.TopicPrefix));
        }

        if (filters.ClaimsStatus is not null)
        {
            sql.Append(" AND claims_status = @claimsStatus");
            parameters.Add(new("claimsStatus", filters.ClaimsStatus));
        }

        if (filters.Author is not null)
        {
            sql.Append(" AND encode(author_id, 'hex') = @author");
            parameters.Add(new("author", filters.Author));
        }

        if (filters.SequenceMin is not null)
        {
            sql.Append(" AND sequence >= @seqMin");
            parameters.Add(new("seqMin", filters.SequenceMin.Value));
        }

        if (filters.SequenceMax is not null)
        {
            sql.Append(" AND sequence <= @seqMax");
            parameters.Add(new("seqMax", filters.SequenceMax.Value));
        }

        if (filters.FragmentOf is not null)
        {
            sql.Append(" AND fragment_of = @fragmentOf");
            parameters.Add(new("fragmentOf", filters.FragmentOf.Value));
        }

        if (filters.HasFrictionAbove is not null)
        {
            sql.Append(" AND max_friction > @frictionThreshold");
            parameters.Add(new("frictionThreshold", filters.HasFrictionAbove.Value));
        }

        if (filters.NeedsReview == true)
        {
            sql.Append(" AND needs_review = true");
        }

        sql.Append(" ORDER BY sequence DESC");

        var limit = Math.Min(filters.Limit ?? 50, 500);
        var offset = filters.Offset ?? 0;
        sql.Append(" LIMIT @limit OFFSET @offset");
        parameters.Add(new("limit", limit));
        parameters.Add(new("offset", offset));

        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = sql.ToString();
        foreach (var p in parameters)
            command.Parameters.Add(p);

        var results = new List<BrowseEntry>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new BrowseEntry
            {
                Id = reader.GetGuid(0),
                Topic = reader.IsDBNull(1) ? null : reader.GetString(1),
                ClaimsStatus = reader.GetString(2),
                MaxFriction = reader.IsDBNull(3) ? null : reader.GetDouble(3),
                NeedsReview = reader.GetBoolean(4),
                Sequence = reader.GetInt64(5),
                Created = reader.GetFieldValue<DateTimeOffset>(6),
                AuthorId = reader.IsDBNull(7) ? "" : reader.GetString(7),
                ClaimCount = reader.GetInt32(8),
            });
        }

        return results;
    }

    public async Task<List<SearchResult>> SearchEntriesAsync(
        Guid notebookId, string query, string searchIn,
        string? topicPrefix, int maxResults, CancellationToken ct)
    {
        var results = new List<SearchResult>();

        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);

        if (searchIn is "content" or "both")
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, topic,
                       substring(encode(content, 'escape') from 1 for 200) as snippet,
                       similarity(encode(content, 'escape'), @query) as score
                FROM entries
                WHERE notebook_id = @notebookId
                  AND encode(content, 'escape') % @query
                  AND (@topicPrefix::text IS NULL OR topic LIKE @topicPrefix || '%')
                ORDER BY score DESC
                LIMIT @maxResults
                """;

            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("notebookId", notebookId));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("query", query));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("topicPrefix", (object?)topicPrefix ?? DBNull.Value));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("maxResults", maxResults));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(new SearchResult
                {
                    EntryId = reader.GetGuid(0),
                    Topic = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Snippet = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    MatchLocation = "content",
                    RelevanceScore = reader.GetDouble(3),
                });
            }
        }

        if (searchIn is "claims" or "both")
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT DISTINCT e.id, e.topic,
                       c.value->>'text' as snippet,
                       similarity(c.value->>'text', @query) as score
                FROM entries e,
                     jsonb_array_elements(e.claims) c
                WHERE e.notebook_id = @notebookId
                  AND c.value->>'text' % @query
                  AND (@topicPrefix::text IS NULL OR e.topic LIKE @topicPrefix || '%')
                ORDER BY score DESC
                LIMIT @maxResults
                """;

            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("notebookId", notebookId));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("query", query));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("topicPrefix", (object?)topicPrefix ?? DBNull.Value));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("maxResults", maxResults));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(new SearchResult
                {
                    EntryId = reader.GetGuid(0),
                    Topic = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Snippet = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    MatchLocation = "claims",
                    RelevanceScore = reader.GetDouble(3),
                });
            }
        }

        return results
            .OrderByDescending(r => r.RelevanceScore)
            .Take(maxResults)
            .ToList();
    }
}
