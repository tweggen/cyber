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
        // AsAsyncEnumerable() required because UPDATE...RETURNING is non-composable SQL in EF Core 10
        var sequence = await db.Database
            .SqlQuery<long>(
                $"""
                UPDATE notebooks SET current_sequence = current_sequence + 1
                WHERE id = {notebookId}
                RETURNING current_sequence
                """)
            .AsAsyncEnumerable()
            .SingleAsync(ct);

        var entry = new Entry
        {
            Id = Guid.NewGuid(),
            NotebookId = notebookId,
            Content = Encoding.UTF8.GetBytes(newEntry.Content),
            ContentType = newEntry.ContentType,
            Topic = newEntry.Topic,
            AuthorId = authorId,
            Signature = new byte[64], // 64-byte zero placeholder (DB requires octet_length=64)
            References = newEntry.References,
            FragmentOf = newEntry.FragmentOf,
            FragmentIndex = newEntry.FragmentIndex,
            OriginalContentType = newEntry.OriginalContentType,
            Source = newEntry.Source,
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

    public async Task UpdateEntryEmbeddingAsync(
        Guid entryId, Guid notebookId, double[] embedding, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE entries SET embedding = @embedding WHERE id = @entryId AND notebook_id = @notebookId";
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("embedding", embedding));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("entryId", entryId));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("notebookId", notebookId));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<(Guid Id, List<Claim> Claims, double Similarity)>> FindNearestByEmbeddingAsync(
        Guid notebookId, Guid excludeEntryId, double[] query, int topK, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT e.id, e.claims,
              (SELECT SUM(q.val * d.val)
               FROM unnest(@query) WITH ORDINALITY AS q(val, ord)
               JOIN unnest(e.embedding) WITH ORDINALITY AS d(val, ord) USING (ord))
              /
              NULLIF(
                SQRT((SELECT SUM(v.val * v.val) FROM unnest(@query) AS v(val)))
                * SQRT((SELECT SUM(v.val * v.val) FROM unnest(e.embedding) AS v(val))),
                0)
              AS similarity
            FROM entries e
            WHERE e.notebook_id = @notebookId
              AND e.id != @entryId
              AND e.embedding IS NOT NULL
              AND e.claims_status IN ('distilled', 'verified')
              AND e.fragment_of IS NULL
            ORDER BY similarity DESC NULLS LAST
            LIMIT @topK
            """;

        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("query", query));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("notebookId", notebookId));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("entryId", excludeEntryId));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("topK", topK));

        var results = new List<(Guid Id, List<Claim> Claims, double Similarity)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var claimsJson = reader.IsDBNull(1) ? "[]" : reader.GetString(1);
            var claims = JsonSerializer.Deserialize<List<Claim>>(claimsJson) ?? [];
            var similarity = reader.IsDBNull(2) ? 0.0 : reader.GetDouble(2);
            results.Add((id, claims, similarity));
        }

        return results;
    }

    public async Task<int> AppendComparisonAsync(Guid entryId, JsonElement comparison, CancellationToken ct)
    {
        var friction = comparison.TryGetProperty("friction", out var f) ? f.GetDouble() : 0.0;
        var comparisonJson = JsonSerializer.Serialize(comparison);

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE entries SET
              comparisons = comparisons || jsonb_build_array(@comparison::jsonb),
              max_friction = GREATEST(COALESCE(max_friction, 0.0), @friction),
              needs_review = (GREATEST(COALESCE(max_friction, 0.0), @friction) > 0.2)
            WHERE id = @entryId
            RETURNING jsonb_array_length(comparisons)
            """;
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("comparison", comparisonJson));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("friction", friction));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("entryId", entryId));

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int count ? count : Convert.ToInt32(result);
    }

    public async Task UpdateExpectedComparisonsAsync(
        Guid entryId, Guid notebookId, int count, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE entries SET expected_comparisons = {0} WHERE id = {1} AND notebook_id = {2}",
            [count, entryId, notebookId],
            ct);
    }

    public async Task UpdateIntegrationStatusAsync(Guid entryId, IntegrationStatus status, CancellationToken ct)
    {
        var statusStr = status.ToString().ToLowerInvariant();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE entries SET integration_status = {0} WHERE id = {1}",
            [statusStr, entryId],
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
                   END as claim_count,
                   integration_status
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

        if (filters.IntegrationStatus is not null)
        {
            sql.Append(" AND integration_status = @integrationStatus");
            parameters.Add(new("integrationStatus", filters.IntegrationStatus));
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
                IntegrationStatus = reader.IsDBNull(9) ? "probation" : reader.GetString(9),
            });
        }

        return results;
    }

    public async Task<List<SemanticSearchResult>> SemanticSearchAsync(
        Guid notebookId, double[] queryEmbedding, int topK, double minSimilarity, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT e.id, e.topic, e.claims, e.claims_status, e.max_friction, e.integration_status,
              (SELECT SUM(q.val * d.val)
               FROM unnest(@query) WITH ORDINALITY AS q(val, ord)
               JOIN unnest(e.embedding) WITH ORDINALITY AS d(val, ord) USING (ord))
              /
              NULLIF(
                SQRT((SELECT SUM(v.val * v.val) FROM unnest(@query) AS v(val)))
                * SQRT((SELECT SUM(v.val * v.val) FROM unnest(e.embedding) AS v(val))),
                0)
              AS similarity
            FROM entries e
            WHERE e.notebook_id = @notebookId
              AND e.embedding IS NOT NULL
              AND e.fragment_of IS NULL
            HAVING (SELECT SUM(q.val * d.val)
               FROM unnest(@query) WITH ORDINALITY AS q(val, ord)
               JOIN unnest(e.embedding) WITH ORDINALITY AS d(val, ord) USING (ord))
              /
              NULLIF(
                SQRT((SELECT SUM(v.val * v.val) FROM unnest(@query) AS v(val)))
                * SQRT((SELECT SUM(v.val * v.val) FROM unnest(e.embedding) AS v(val))),
                0) >= @minSimilarity
            ORDER BY similarity DESC NULLS LAST
            LIMIT @topK
            """;

        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("query", queryEmbedding));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("notebookId", notebookId));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("minSimilarity", minSimilarity));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("topK", topK));

        var results = new List<SemanticSearchResult>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var claimsJson = reader.IsDBNull(2) ? "[]" : reader.GetString(2);
            var claims = JsonSerializer.Deserialize<List<Claim>>(claimsJson) ?? [];

            results.Add(new SemanticSearchResult
            {
                EntryId = reader.GetGuid(0),
                Topic = reader.IsDBNull(1) ? null : reader.GetString(1),
                Claims = claims,
                ClaimsStatus = reader.IsDBNull(3) ? "pending" : reader.GetString(3),
                MaxFriction = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                IntegrationStatus = reader.IsDBNull(5) ? "probation" : reader.GetString(5),
                Similarity = reader.IsDBNull(6) ? 0.0 : reader.GetDouble(6),
            });
        }

        return results;
    }

    public async Task<List<ClaimsBatchEntry>> GetClaimsBatchAsync(
        Guid notebookId, List<Guid> entryIds, CancellationToken ct)
    {
        if (entryIds.Count == 0)
            return [];

        var entries = await db.Entries
            .Where(e => e.NotebookId == notebookId && entryIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Topic, e.Claims, e.ClaimsStatus, e.IntegrationStatus })
            .ToListAsync(ct);

        return entries.Select(e => new ClaimsBatchEntry
        {
            Id = e.Id,
            Topic = e.Topic,
            Claims = e.Claims,
            ClaimsStatus = e.ClaimsStatus.ToString().ToLowerInvariant(),
            IntegrationStatus = e.IntegrationStatus.ToString().ToLowerInvariant(),
        }).ToList();
    }

    public async Task<Entry?> GetEntryAsync(Guid entryId, Guid notebookId, CancellationToken ct)
    {
        return await db.Entries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.NotebookId == notebookId, ct);
    }

    public async Task<Entry?> GetFragmentAsync(Guid notebookId, Guid fragmentOf, int fragmentIndex, CancellationToken ct)
    {
        return await db.Entries
            .FirstOrDefaultAsync(e => e.NotebookId == notebookId
                && e.FragmentOf == fragmentOf
                && e.FragmentIndex == fragmentIndex, ct);
    }

    public async Task<List<Claim>> GetFragmentClaimsUpToAsync(Guid notebookId, Guid fragmentOf, int upToIndex, CancellationToken ct)
    {
        var fragments = await db.Entries
            .Where(e => e.NotebookId == notebookId
                && e.FragmentOf == fragmentOf
                && e.FragmentIndex != null
                && e.FragmentIndex <= upToIndex)
            .OrderBy(e => e.FragmentIndex)
            .Select(e => e.Claims)
            .ToListAsync(ct);

        return fragments.SelectMany(c => c).ToList();
    }

    public async Task<int> GetFragmentCountAsync(Guid notebookId, Guid fragmentOf, CancellationToken ct)
    {
        return await db.Entries
            .CountAsync(e => e.NotebookId == notebookId && e.FragmentOf == fragmentOf, ct);
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
