# Step 2: Batch Write and Claims API

**Depends on:** Step 1 (Schema and Types)

## Goal

Add two new endpoints:
1. **Batch write** — write up to 100 entries in a single call
2. **Update claims** — write claims to an existing entry (used by robot workers)

## 2.1 — Batch Write Endpoint

### Route

`POST /notebooks/{id}/batch`

### File: `Notebook.Server/Endpoints/BatchEndpoints.cs` (new file)

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Notebook.Core.Types;
using Notebook.Data.Repositories;

namespace Notebook.Server.Endpoints;

public static class BatchEndpoints
{
    public static void MapBatchEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/notebooks/{notebookId}/batch", BatchWrite)
            .RequireAuthorization();
    }

    /// <summary>
    /// Write up to 100 entries in a single transactional call.
    /// Each entry is automatically queued for claim distillation.
    /// </summary>
    private static async Task<IResult> BatchWrite(
        Guid notebookId,
        [FromBody] BatchWriteRequest request,
        IEntryRepository entryRepo,
        IJobRepository jobRepo,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Validate batch size
        if (request.Entries is not { Count: > 0 })
            return Results.BadRequest(new { error = "entries array is empty" });

        if (request.Entries.Count > 100)
            return Results.BadRequest(new { error = "batch size exceeds limit of 100 entries" });

        // Validate notebook exists
        var notebook = await entryRepo.GetNotebookAsync(notebookId, ct);
        if (notebook is null)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });

        var results = new List<BatchEntryResult>(request.Entries.Count);
        var jobsCreated = 0;

        // Wrap in a transaction
        await using var transaction = await entryRepo.BeginTransactionAsync(ct);

        foreach (var batchEntry in request.Entries)
        {
            // 1. Assign causal position
            // 2. Compute integration cost
            // 3. Insert entry with claims_status = "pending"
            //    (follow same pattern as single-entry create)
            var entry = await entryRepo.InsertEntryAsync(notebookId, new NewEntry
            {
                Content = batchEntry.Content,
                ContentType = batchEntry.ContentType ?? "text/plain",
                Topic = batchEntry.Topic,
                References = batchEntry.References ?? [],
                FragmentOf = batchEntry.FragmentOf,
                FragmentIndex = batchEntry.FragmentIndex,
            }, ct);

            // 4. Create DISTILL_CLAIMS job
            var payload = JsonSerializer.SerializeToDocument(new
            {
                entry_id = entry.Id.ToString(),
                content = batchEntry.Content,
                context_claims = (object?)null,
                max_claims = 12,
            });

            await jobRepo.InsertJobAsync(notebookId, "DISTILL_CLAIMS", payload, ct);
            jobsCreated++;

            results.Add(new BatchEntryResult
            {
                EntryId = entry.Id,
                CausalPosition = entry.CausalPosition,
                IntegrationCost = entry.IntegrationCost,
                ClaimsStatus = ClaimsStatus.Pending,
            });
        }

        await transaction.CommitAsync(ct);

        return Results.Created("", new BatchWriteResponse
        {
            Results = results,
            JobsCreated = jobsCreated,
        });
    }
}
```

### Request/Response models

Add to `Notebook.Server/Models/BatchModels.cs`:

```csharp
using System.Text.Json.Serialization;
using Notebook.Core.Types;

namespace Notebook.Server.Models;

public sealed record BatchEntryRequest
{
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("content_type")]
    public string? ContentType { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("references")]
    public List<Guid>? References { get; init; }

    [JsonPropertyName("fragment_of")]
    public Guid? FragmentOf { get; init; }

    [JsonPropertyName("fragment_index")]
    public int? FragmentIndex { get; init; }
}

public sealed record BatchWriteRequest
{
    [JsonPropertyName("entries")]
    public required List<BatchEntryRequest> Entries { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }
}

public sealed record BatchEntryResult
{
    [JsonPropertyName("entry_id")]
    public required Guid EntryId { get; init; }

    [JsonPropertyName("causal_position")]
    public required long CausalPosition { get; init; }

    [JsonPropertyName("integration_cost")]
    public required double IntegrationCost { get; init; }

    [JsonPropertyName("claims_status")]
    public required ClaimsStatus ClaimsStatus { get; init; }
}

public sealed record BatchWriteResponse
{
    [JsonPropertyName("results")]
    public required List<BatchEntryResult> Results { get; init; }

    [JsonPropertyName("jobs_created")]
    public required int JobsCreated { get; init; }
}
```

### Key implementation details

1. **Author resolution:** The batch request uses an `author` string field. Resolve this to an `AuthorId` — either look up by name in the authors table, or use the JWT identity from the request. For bulk imports the caller is typically a script, so using the JWT identity (from the authentication middleware) is simplest. The `author` field in the request body is informational metadata.

2. **Sequential causal positions:** Each entry in the batch gets a sequential causal position. Call the causal position service for each entry in order.

3. **Job creation:** After inserting each entry, insert a row into the `jobs` table with job type `DISTILL_CLAIMS` and payload containing the entry ID, content, and max_claims.

4. **Transaction:** Wrap the entire batch in a database transaction via `IDbContextTransaction`. If any entry fails, roll back all.

5. **Integration cost:** For bulk imports, computing full integration cost per entry may be slow. Consider using zero for batch writes and computing costs in a background pass. This is a pragmatic trade-off — the design doc acknowledges this risk.

### Register the endpoint

In `Program.cs`:

```csharp
app.MapBatchEndpoints();
```

## 2.2 — Update Claims Endpoint

### Route

`POST /notebooks/{notebookId}/entries/{entryId}/claims`

### File: `Notebook.Server/Endpoints/ClaimsEndpoints.cs` (new file)

```csharp
using Microsoft.AspNetCore.Mvc;
using Notebook.Core.Types;
using Notebook.Data.Repositories;

namespace Notebook.Server.Endpoints;

public static class ClaimsEndpoints
{
    public static void MapClaimsEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/notebooks/{notebookId}/entries/{entryId}/claims", UpdateClaims)
            .RequireAuthorization();
    }

    /// <summary>
    /// Write claims to an entry (called by robot workers after distillation).
    /// Immutable: rejects if claims are already written.
    /// </summary>
    private static async Task<IResult> UpdateClaims(
        Guid notebookId,
        Guid entryId,
        [FromBody] UpdateClaimsRequest request,
        IEntryRepository entryRepo,
        IJobRepository jobRepo,
        CancellationToken ct)
    {
        // 1. Validate claims
        if (request.Claims is not { Count: > 0 })
            return Results.BadRequest(new { error = "claims array is empty" });

        if (request.Claims.Count > 20)
            return Results.BadRequest(new { error = "claims array exceeds maximum of 20" });

        // 2. Update the entry's claims and claims_status
        //    This will fail if claims_status != "pending" (immutability)
        var updated = await entryRepo.UpdateEntryClaimsAsync(
            entryId, notebookId, request.Claims, ct);

        if (!updated)
            return Results.Conflict(new { error = "claims already set or entry not found" });

        // 3. Create COMPARE_CLAIMS jobs against relevant topic indices
        var topicIndices = await entryRepo.FindTopicIndicesAsync(notebookId, ct);
        var comparisonJobsCreated = 0;

        foreach (var (indexId, indexClaims) in topicIndices)
        {
            var payload = System.Text.Json.JsonSerializer.SerializeToDocument(new
            {
                entry_id = entryId.ToString(),
                compare_against_id = indexId.ToString(),
                claims_a = indexClaims,
                claims_b = request.Claims,
            });

            await jobRepo.InsertJobAsync(notebookId, "COMPARE_CLAIMS", payload, ct);
            comparisonJobsCreated++;
        }

        return Results.Ok(new UpdateClaimsResponse
        {
            EntryId = entryId,
            ClaimsStatus = ClaimsStatus.Distilled,
            ComparisonJobsCreated = comparisonJobsCreated,
        });
    }
}
```

### Request/Response models

Add to `Notebook.Server/Models/ClaimsModels.cs`:

```csharp
using System.Text.Json.Serialization;
using Notebook.Core.Types;

namespace Notebook.Server.Models;

public sealed record UpdateClaimsRequest
{
    [JsonPropertyName("claims")]
    public required List<Claim> Claims { get; init; }

    /// <summary>Identifier of the worker that produced these claims.</summary>
    [JsonPropertyName("author")]
    public required string Author { get; init; }
}

public sealed record UpdateClaimsResponse
{
    [JsonPropertyName("entry_id")]
    public required Guid EntryId { get; init; }

    [JsonPropertyName("claims_status")]
    public required ClaimsStatus ClaimsStatus { get; init; }

    [JsonPropertyName("comparison_jobs_created")]
    public required int ComparisonJobsCreated { get; init; }
}
```

### Key implementation details

1. **Immutability:** Once claims are written, they can't be overwritten. If claims_status is already "distilled" or "verified", reject with 409 Conflict. Re-distillation should go through the REVISE path.

2. **Comparison job creation:** When claims are written, find all relevant topic index entries (entries with `topic LIKE 'index/topic/%'` that have distilled claims) and create a COMPARE_CLAIMS job for each pair. The payload includes both claim-sets so the robot worker doesn't need DB access.

### Register the endpoint

In `Program.cs`:

```csharp
app.MapClaimsEndpoints();
```

## 2.3 — Repository Methods

### IEntryRepository additions

```csharp
public interface IEntryRepository
{
    // ... existing methods ...

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
    Task<Entry> InsertEntryAsync(Guid notebookId, NewEntry entry, CancellationToken ct);
    Task<bool> UpdateEntryClaimsAsync(Guid entryId, Guid notebookId, List<Claim> claims, CancellationToken ct);
    Task<List<(Guid Id, List<Claim> Claims)>> FindTopicIndicesAsync(Guid notebookId, CancellationToken ct);
}
```

### EntryRepository implementation

```csharp
public async Task<bool> UpdateEntryClaimsAsync(
    Guid entryId, Guid notebookId, List<Claim> claims, CancellationToken ct)
{
    var claimsJson = JsonSerializer.Serialize(claims);

    var rowsAffected = await _db.Database.ExecuteSqlRawAsync(
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
    var rows = await _db.Entries
        .Where(e => e.NotebookId == notebookId
            && e.Topic != null
            && e.Topic.StartsWith("index/topic/")
            && (e.ClaimsStatus == ClaimsStatus.Distilled || e.ClaimsStatus == ClaimsStatus.Verified)
            && e.Claims.Count > 0)
        .Select(e => new { e.Id, e.Claims })
        .ToListAsync(ct);

    return rows.Select(r => (r.Id, r.Claims)).ToList();
}
```

### IJobRepository

```csharp
public interface IJobRepository
{
    Task<Guid> InsertJobAsync(Guid notebookId, string jobType, JsonDocument payload, CancellationToken ct);
}
```

### JobRepository implementation

```csharp
public async Task<Guid> InsertJobAsync(
    Guid notebookId, string jobType, JsonDocument payload, CancellationToken ct)
{
    var job = new JobEntity
    {
        Id = Guid.NewGuid(),
        NotebookId = notebookId,
        JobType = jobType,
        Status = "pending",
        Payload = payload,
        Created = DateTimeOffset.UtcNow,
    };

    _db.Jobs.Add(job);
    await _db.SaveChangesAsync(ct);
    return job.Id;
}
```

## 2.4 — Update READ Response

The READ endpoint should now include the new fields in its response. Update the entry response DTO:

```csharp
// Add to the existing entry response model:
[JsonPropertyName("claims")]
public List<Claim> Claims { get; init; } = [];

[JsonPropertyName("claims_status")]
public ClaimsStatus ClaimsStatus { get; init; }

[JsonPropertyName("fragment_of")]
public Guid? FragmentOf { get; init; }

[JsonPropertyName("fragment_index")]
public int? FragmentIndex { get; init; }

[JsonPropertyName("comparisons")]
public List<ClaimComparison> Comparisons { get; init; } = [];

[JsonPropertyName("max_friction")]
public double? MaxFriction { get; init; }

[JsonPropertyName("needs_review")]
public bool NeedsReview { get; init; }
```

Update the entry-to-response mapping to populate these fields.

## 2.5 — Tests

### BatchModelTests.cs

```csharp
using System.Text.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class BatchModelTests
{
    [Fact]
    public void BatchWriteRequest_Deserialize()
    {
        var json = """
        {
            "entries": [
                {"content": "entry 1", "topic": "test"},
                {"content": "entry 2", "content_type": "text/markdown"}
            ],
            "author": "bulk-import"
        }
        """;

        var request = JsonSerializer.Deserialize<BatchWriteRequest>(json)!;
        Assert.Equal(2, request.Entries.Count);
        Assert.Null(request.Entries[0].ContentType); // no default in deserialization
        Assert.Equal("text/markdown", request.Entries[1].ContentType);
    }

    [Fact]
    public void UpdateClaimsRequest_Deserialize()
    {
        var json = """
        {
            "claims": [
                {"text": "OAuth tokens validated before jobs", "confidence": 0.95},
                {"text": "Validation uses refresh endpoint", "confidence": 0.82}
            ],
            "author": "robot-haiku-1"
        }
        """;

        var request = JsonSerializer.Deserialize<UpdateClaimsRequest>(json)!;
        Assert.Equal(2, request.Claims.Count);
        Assert.Equal(0.95, request.Claims[0].Confidence);
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
# Start server and test batch write:
curl -X POST http://localhost:5000/notebooks/$NOTEBOOK_ID/batch \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "entries": [
      {"content": "Test entry 1", "topic": "test/batch"},
      {"content": "Test entry 2", "topic": "test/batch"}
    ],
    "author": "test-script"
  }'

# Should return 201 with results array and jobs_created count
```
