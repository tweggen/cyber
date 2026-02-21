# Step 4: Filtered Browse and Search

**Depends on:** Step 1 (Schema and Types)

## Goal

Enhance the existing BROWSE endpoint with filtering parameters. Add a new full-text search endpoint. These enable efficient navigation of notebooks with 10K+ entries.

## 4.1 — Enhanced BROWSE

### Current state

The existing BROWSE endpoint accepts an optional `query` parameter and returns a token-budgeted catalog via the entropy engine.

### New query parameters

Add these optional query parameters to the existing BROWSE endpoint:

```csharp
namespace Notebook.Server.Models;

public sealed record BrowseParams
{
    /// <summary>Existing: keyword search on topics.</summary>
    public string? Query { get; init; }

    /// <summary>Existing: max entries in catalog.</summary>
    public int? MaxEntries { get; init; }

    // --- NEW PARAMETERS ---

    /// <summary>Filter by topic prefix (e.g., "confluence/ENG/").</summary>
    public string? TopicPrefix { get; init; }

    /// <summary>Filter by claims status: "pending", "distilled", "verified".</summary>
    public string? ClaimsStatus { get; init; }

    /// <summary>Filter by author identifier.</summary>
    public string? Author { get; init; }

    /// <summary>Entries from this sequence number onward.</summary>
    public long? SequenceMin { get; init; }

    /// <summary>Entries up to this sequence number.</summary>
    public long? SequenceMax { get; init; }

    /// <summary>Fragments of a specific artifact.</summary>
    public Guid? FragmentOf { get; init; }

    /// <summary>Entries with friction score above this threshold.</summary>
    public double? HasFrictionAbove { get; init; }

    /// <summary>Entries flagged for review.</summary>
    public bool? NeedsReview { get; init; }

    /// <summary>Max results (default: 50).</summary>
    public int? Limit { get; init; }

    /// <summary>Pagination offset.</summary>
    public int? Offset { get; init; }

    /// <summary>Returns true if any v2 filter parameter is set.</summary>
    public bool HasFilters =>
        TopicPrefix is not null || ClaimsStatus is not null || Author is not null ||
        SequenceMin is not null || SequenceMax is not null || FragmentOf is not null ||
        HasFrictionAbove is not null || NeedsReview is not null ||
        Limit is not null || Offset is not null;
}
```

### Implementation approach

Two modes:
- **Unfiltered (no new params):** Existing behavior — entropy engine catalog with token budget
- **Filtered (any new param present):** Direct SQL query, returns a flat list of entry summaries with claim/friction metadata

### BrowseEntry model

```csharp
using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record BrowseEntry
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("claims_status")]
    public string ClaimsStatus { get; init; } = "pending";

    [JsonPropertyName("max_friction")]
    public double? MaxFriction { get; init; }

    [JsonPropertyName("needs_review")]
    public bool NeedsReview { get; init; }

    [JsonPropertyName("sequence")]
    public long Sequence { get; init; }

    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; init; }

    [JsonPropertyName("author_id")]
    public string AuthorId { get; init; } = "";

    [JsonPropertyName("claim_count")]
    public int ClaimCount { get; init; }
}
```

### Handler changes

Update the browse handler to detect filter parameters:

```csharp
private static async Task<IResult> Browse(
    Guid notebookId,
    [AsParameters] BrowseParams @params,
    IEntryRepository entryRepo,
    IEntropyEngine? engine,
    CancellationToken ct)
{
    if (@params.HasFilters)
    {
        // Filtered browse: direct SQL query
        var entries = await entryRepo.BrowseFilteredAsync(notebookId, @params, ct);
        return Results.Ok(new
        {
            entries,
            count = entries.Count,
        });
    }
    else
    {
        // Original catalog browse (existing behavior)
        // ... existing code unchanged ...
    }
}
```

### Repository: BrowseFilteredAsync

Add to `IEntryRepository` and implement using a dynamic query builder:

```csharp
public async Task<List<BrowseEntry>> BrowseFilteredAsync(
    Guid notebookId, BrowseParams filters, CancellationToken ct)
{
    // Build dynamic SQL with Npgsql parameters
    var sql = new StringBuilder(
        """
        SELECT id, topic, claims_status, max_friction, needs_review,
               sequence, created, encode(author_id, 'hex') as author_id,
               CASE WHEN claims != '[]'::jsonb
                    THEN jsonb_array_length(claims) ELSE 0
               END as claim_count
        FROM entries WHERE notebook_id = @notebookId
        """);

    var parameters = new List<NpgsqlParameter>
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

    // Execute with Npgsql via EF Core's raw SQL support
    await using var connection = _db.Database.GetDbConnection();
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
```

**Alternative: Dapper approach** (simpler for dynamic SQL):

```csharp
// If you add Dapper as a dependency, the dynamic query becomes cleaner:
using Dapper;

public async Task<List<BrowseEntry>> BrowseFilteredAsync(
    Guid notebookId, BrowseParams filters, CancellationToken ct)
{
    var sql = new StringBuilder(
        """
        SELECT id, topic, claims_status, max_friction, needs_review,
               sequence, created, encode(author_id, 'hex') as author_id,
               CASE WHEN claims != '[]'::jsonb
                    THEN jsonb_array_length(claims) ELSE 0
               END as claim_count
        FROM entries WHERE notebook_id = @NotebookId
        """);

    var dynParams = new DynamicParameters();
    dynParams.Add("NotebookId", notebookId);

    if (filters.TopicPrefix is not null)
    {
        sql.Append(" AND topic LIKE @TopicPrefix || '%'");
        dynParams.Add("TopicPrefix", filters.TopicPrefix);
    }
    // ... same pattern for other filters ...

    sql.Append(" ORDER BY sequence DESC");
    sql.Append(" LIMIT @Limit OFFSET @Offset");
    dynParams.Add("Limit", Math.Min(filters.Limit ?? 50, 500));
    dynParams.Add("Offset", filters.Offset ?? 0);

    await using var connection = _db.Database.GetDbConnection();
    var results = (await connection.QueryAsync<BrowseEntry>(sql.ToString(), dynParams))
        .ToList();

    return results;
}
```

## 4.2 — Search Endpoint

### Route

`GET /notebooks/{notebookId}/search`

### File: `Notebook.Server/Endpoints/SearchEndpoints.cs` (new file)

```csharp
using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;

namespace Notebook.Server.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks/{notebookId}/search", Search)
            .RequireAuthorization();
    }

    private static async Task<IResult> Search(
        Guid notebookId,
        [FromQuery] string query,
        [FromQuery(Name = "search_in")] string searchIn = "both",
        [FromQuery(Name = "topic_prefix")] string? topicPrefix = null,
        [FromQuery(Name = "max_results")] int maxResults = 20,
        IEntryRepository entryRepo = null!,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Results.BadRequest(new { error = "query is required" });

        maxResults = Math.Min(maxResults, 100);

        var results = await entryRepo.SearchEntriesAsync(
            notebookId, query, searchIn, topicPrefix, maxResults, ct);

        return Results.Ok(new SearchResponse { Results = results });
    }
}
```

### Search models

```csharp
using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record SearchResult
{
    [JsonPropertyName("entry_id")]
    public Guid EntryId { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("snippet")]
    public string Snippet { get; init; } = "";

    [JsonPropertyName("match_location")]
    public string MatchLocation { get; init; } = "";

    [JsonPropertyName("relevance_score")]
    public double RelevanceScore { get; init; }
}

public sealed record SearchResponse
{
    [JsonPropertyName("results")]
    public required List<SearchResult> Results { get; init; }
}
```

### Repository: SearchEntriesAsync

```csharp
/// <summary>Full-text search using PostgreSQL trigram matching.</summary>
public async Task<List<SearchResult>> SearchEntriesAsync(
    Guid notebookId, string query, string searchIn,
    string? topicPrefix, int maxResults, CancellationToken ct)
{
    var results = new List<SearchResult>();

    await using var connection = _db.Database.GetDbConnection();
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

        cmd.Parameters.Add(new NpgsqlParameter("notebookId", notebookId));
        cmd.Parameters.Add(new NpgsqlParameter("query", query));
        cmd.Parameters.Add(new NpgsqlParameter("topicPrefix", (object?)topicPrefix ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("maxResults", maxResults));

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

        cmd.Parameters.Add(new NpgsqlParameter("notebookId", notebookId));
        cmd.Parameters.Add(new NpgsqlParameter("query", query));
        cmd.Parameters.Add(new NpgsqlParameter("topicPrefix", (object?)topicPrefix ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("maxResults", maxResults));

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

    // Sort by relevance and limit
    return results
        .OrderByDescending(r => r.RelevanceScore)
        .Take(maxResults)
        .ToList();
}
```

### Register the endpoint

In `Program.cs`:

```csharp
app.MapSearchEndpoints();
```

## 4.3 — Alternative: Use Existing Entropy Engine Search

If the entropy engine has a Tantivy or Lucene-based search index, use it:

```csharp
// In the search handler, try engine search first:
if (engine is not null)
{
    var engineResults = await engine.SearchAsync(query, maxResults);
    if (engineResults is not null)
    {
        // Convert engine results to SearchResult
        return Results.Ok(new SearchResponse { Results = engineResults });
    }
}
// Fall back to PostgreSQL trigram search
```

This is optional for the initial implementation. PostgreSQL trigram search is simpler and works without the entropy engine being fully initialized.

## 4.4 — Tests

### BrowseFilterTests.cs

```csharp
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class BrowseFilterTests
{
    [Fact]
    public void BrowseParams_NoFilters_HasFiltersIsFalse()
    {
        var p = new BrowseParams();
        Assert.False(p.HasFilters);
    }

    [Fact]
    public void BrowseParams_WithTopicPrefix_HasFiltersIsTrue()
    {
        var p = new BrowseParams { TopicPrefix = "confluence/ENG/" };
        Assert.True(p.HasFilters);
    }

    [Fact]
    public void BrowseParams_WithMultipleFilters()
    {
        var p = new BrowseParams
        {
            TopicPrefix = "confluence/ENG/",
            ClaimsStatus = "pending",
            Limit = 20,
        };
        Assert.True(p.HasFilters);
    }
}
```

### SearchModelTests.cs

```csharp
using System.Text.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class SearchModelTests
{
    [Fact]
    public void SearchResponse_Serialize()
    {
        var response = new SearchResponse
        {
            Results =
            [
                new SearchResult
                {
                    EntryId = Guid.NewGuid(),
                    Topic = "auth/oauth",
                    Snippet = "OAuth tokens are validated...",
                    MatchLocation = "content",
                    RelevanceScore = 0.85,
                },
            ],
        };
        var json = JsonSerializer.Serialize(response);
        Assert.Contains("entry_id", json);
        Assert.Contains("relevance_score", json);
    }
}
```

## Verify

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

### Manual integration test

```bash
# Filtered browse
curl "http://localhost:5000/notebooks/$NB/browse?claims_status=pending&limit=10" \
  -H "Authorization: Bearer $TOKEN"

# Search
curl "http://localhost:5000/notebooks/$NB/search?query=authentication&search_in=both" \
  -H "Authorization: Bearer $TOKEN"
```
