# Step 3: Job Queue

**Depends on:** Step 1 (Schema and Types)

## Goal

Implement the job queue infrastructure that robot workers use to pull and complete work. The `jobs` table was created in Step 1. This step adds the API endpoints and the server-side logic for job management.

## 3.1 — Job Queue Endpoints

### File: `Notebook.Server/Endpoints/JobEndpoints.cs` (new file)

Four endpoints:

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/notebooks/{id}/jobs/next` | Robot pulls next available job |
| POST | `/notebooks/{id}/jobs/{jobId}/complete` | Robot submits result |
| POST | `/notebooks/{id}/jobs/{jobId}/fail` | Robot reports failure |
| GET | `/notebooks/{id}/jobs/stats` | Queue depth and processing stats |

### Endpoint registration

```csharp
using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;

namespace Notebook.Server.Endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/notebooks/{notebookId}/jobs")
            .RequireAuthorization();

        group.MapGet("/next", NextJob);
        group.MapPost("/{jobId}/complete", CompleteJob);
        group.MapPost("/{jobId}/fail", FailJob);
        group.MapGet("/stats", JobStats);
    }

    // ... handlers below
}
```

### Request/Response models

Add to `Notebook.Server/Models/JobModels.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

// --- Pull next job ---

public sealed record JobResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("job_type")]
    public required string JobType { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("payload")]
    public required JsonElement Payload { get; init; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }

    [JsonPropertyName("claimed_at")]
    public DateTimeOffset? ClaimedAt { get; init; }

    [JsonPropertyName("claimed_by")]
    public string? ClaimedBy { get; init; }
}

// --- Complete job ---

public sealed record CompleteJobRequest
{
    [JsonPropertyName("worker_id")]
    public required string WorkerId { get; init; }

    [JsonPropertyName("result")]
    public required JsonElement Result { get; init; }
}

// --- Fail job ---

public sealed record FailJobRequest
{
    [JsonPropertyName("worker_id")]
    public required string WorkerId { get; init; }

    [JsonPropertyName("error")]
    public required string Error { get; init; }
}

// --- Stats ---

public sealed record JobTypeStats
{
    [JsonPropertyName("pending")]
    public long Pending { get; init; }

    [JsonPropertyName("in_progress")]
    public long InProgress { get; init; }

    [JsonPropertyName("completed")]
    public long Completed { get; init; }

    [JsonPropertyName("failed")]
    public long Failed { get; init; }
}

public sealed record JobStatsResponse
{
    [JsonPropertyName("DISTILL_CLAIMS")]
    public required JobTypeStats DistillClaims { get; init; }

    [JsonPropertyName("COMPARE_CLAIMS")]
    public required JobTypeStats CompareClaims { get; init; }

    [JsonPropertyName("CLASSIFY_TOPIC")]
    public required JobTypeStats ClassifyTopic { get; init; }
}
```

### Handler: Pull next job

```csharp
/// <summary>
/// GET /notebooks/{notebookId}/jobs/next?worker_id=robot-1&type=DISTILL_CLAIMS
///
/// Atomically claims the next pending job, transitioning it to in_progress.
/// Returns null/204 if no jobs available.
/// </summary>
private static async Task<IResult> NextJob(
    Guid notebookId,
    [FromQuery(Name = "worker_id")] string workerId,
    [FromQuery(Name = "type")] string? jobType,
    IJobRepository jobRepo,
    CancellationToken ct)
{
    // First, reclaim timed-out jobs
    await jobRepo.ReclaimTimedOutJobsAsync(notebookId, ct);

    // Atomically claim the next pending job
    var job = await jobRepo.ClaimNextJobAsync(notebookId, jobType, workerId, ct);

    if (job is null)
        return Results.NoContent();

    return Results.Ok(new JobResponse
    {
        Id = job.Id,
        JobType = job.JobType,
        Status = job.Status,
        Payload = job.Payload.RootElement,
        Created = job.Created,
        ClaimedAt = job.ClaimedAt,
        ClaimedBy = job.ClaimedBy,
    });
}
```

### Handler: Complete job

```csharp
/// <summary>
/// POST /notebooks/{notebookId}/jobs/{jobId}/complete
///
/// Robot submits the result of a completed job.
/// The server processes the result based on job type.
/// </summary>
private static async Task<IResult> CompleteJob(
    Guid notebookId,
    Guid jobId,
    [FromBody] CompleteJobRequest request,
    IJobRepository jobRepo,
    IJobResultProcessor resultProcessor,
    CancellationToken ct)
{
    // 1. Verify the job exists, is in_progress, and is claimed by this worker
    var job = await jobRepo.GetJobAsync(jobId, ct);
    if (job is null)
        return Results.NotFound(new { error = $"Job {jobId} not found" });

    if (job.Status != "in_progress" || job.ClaimedBy != request.WorkerId)
        return Results.Conflict(new { error = "Job not claimed by this worker" });

    // 2. Process the result based on job type
    var followUpJobs = await resultProcessor.ProcessResultAsync(job, request.Result, ct);

    // 3. Mark job complete
    await jobRepo.CompleteJobAsync(jobId, request.WorkerId, request.Result, ct);

    return Results.Ok(new { status = "completed", follow_up_jobs = followUpJobs });
}
```

### Handler: Fail job

```csharp
/// <summary>
/// POST /notebooks/{notebookId}/jobs/{jobId}/fail
/// </summary>
private static async Task<IResult> FailJob(
    Guid notebookId,
    Guid jobId,
    [FromBody] FailJobRequest request,
    IJobRepository jobRepo,
    CancellationToken ct)
{
    var job = await jobRepo.GetJobAsync(jobId, ct);
    if (job is null)
        return Results.NotFound(new { error = $"Job {jobId} not found" });

    if (job.ClaimedBy != request.WorkerId)
        return Results.Conflict(new { error = "Job not claimed by this worker" });

    if (job.RetryCount < job.MaxRetries)
    {
        // Return to pending for retry
        await jobRepo.ReturnToPendingAsync(jobId, request.Error, ct);
        return Results.Ok(new { status = "pending", retry_count = job.RetryCount + 1 });
    }
    else
    {
        // Mark as permanently failed
        await jobRepo.MarkFailedAsync(jobId, request.Error, ct);
        return Results.Ok(new { status = "failed" });
    }
}
```

### Handler: Stats

```csharp
/// <summary>
/// GET /notebooks/{notebookId}/jobs/stats
/// </summary>
private static async Task<IResult> JobStats(
    Guid notebookId,
    IJobRepository jobRepo,
    CancellationToken ct)
{
    var stats = await jobRepo.GetStatsAsync(notebookId, ct);
    return Results.Ok(stats);
}
```

### Register the endpoint

In `Program.cs`:

```csharp
app.MapJobEndpoints();
```

## 3.2 — Repository Layer

### IJobRepository (extended)

```csharp
using System.Text.Json;
using Notebook.Data.Entities;
using Notebook.Server.Models;

namespace Notebook.Data.Repositories;

public interface IJobRepository
{
    Task<Guid> InsertJobAsync(Guid notebookId, string jobType, JsonDocument payload, CancellationToken ct);
    Task<int> ReclaimTimedOutJobsAsync(Guid notebookId, CancellationToken ct);
    Task<JobEntity?> ClaimNextJobAsync(Guid notebookId, string? jobType, string workerId, CancellationToken ct);
    Task<JobEntity?> GetJobAsync(Guid jobId, CancellationToken ct);
    Task CompleteJobAsync(Guid jobId, string workerId, JsonElement result, CancellationToken ct);
    Task ReturnToPendingAsync(Guid jobId, string error, CancellationToken ct);
    Task MarkFailedAsync(Guid jobId, string error, CancellationToken ct);
    Task<JobStatsResponse> GetStatsAsync(Guid notebookId, CancellationToken ct);
}
```

### JobRepository implementation

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notebook.Data.Entities;
using Notebook.Server.Models;

namespace Notebook.Data.Repositories;

public class JobRepository(NotebookDbContext db) : IJobRepository
{
    /// <summary>Reclaim timed-out jobs (return them to pending).</summary>
    public async Task<int> ReclaimTimedOutJobsAsync(Guid notebookId, CancellationToken ct)
    {
        // Raw SQL because EF Core doesn't support interval arithmetic easily
        var rowsAffected = await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE jobs
            SET status = 'pending', claimed_at = NULL, claimed_by = NULL,
                retry_count = retry_count + 1
            WHERE status = 'in_progress'
              AND notebook_id = {0}
              AND claimed_at + make_interval(secs => timeout_seconds) < NOW()
              AND retry_count < max_retries
            """,
            [notebookId],
            ct);

        return rowsAffected;
    }

    /// <summary>
    /// Claim the next pending job. Uses FOR UPDATE SKIP LOCKED for concurrency.
    /// </summary>
    public async Task<JobEntity?> ClaimNextJobAsync(
        Guid notebookId, string? jobType, string workerId, CancellationToken ct)
    {
        // Must use raw SQL for FOR UPDATE SKIP LOCKED
        var jobTypeParam = jobType ?? (object)DBNull.Value;

        var jobs = await db.Jobs.FromSqlRaw(
            """
            UPDATE jobs SET status = 'in_progress', claimed_at = NOW(), claimed_by = {0}
            WHERE id = (
                SELECT id FROM jobs
                WHERE notebook_id = {1}
                  AND status = 'pending'
                  AND ({2}::text IS NULL OR job_type = {2})
                ORDER BY created ASC
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            RETURNING *
            """,
            workerId, notebookId, jobTypeParam)
            .ToListAsync(ct);

        return jobs.FirstOrDefault();
    }

    /// <summary>Get a job by ID.</summary>
    public async Task<JobEntity?> GetJobAsync(Guid jobId, CancellationToken ct)
    {
        return await db.Jobs.FindAsync([jobId], ct);
    }

    /// <summary>Mark a job as completed with its result.</summary>
    public async Task CompleteJobAsync(
        Guid jobId, string workerId, JsonElement result, CancellationToken ct)
    {
        var resultDoc = JsonSerializer.SerializeToDocument(result);

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE jobs
            SET status = 'completed', result = {0}::jsonb, completed_at = NOW()
            WHERE id = {1} AND claimed_by = {2} AND status = 'in_progress'
            """,
            [JsonSerializer.Serialize(result), jobId, workerId],
            ct);
    }

    /// <summary>Return a failed job to pending for retry.</summary>
    public async Task ReturnToPendingAsync(Guid jobId, string error, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE jobs
            SET status = 'pending', claimed_at = NULL, claimed_by = NULL,
                retry_count = retry_count + 1, error = {0}
            WHERE id = {1}
            """,
            [error, jobId],
            ct);
    }

    /// <summary>Mark a job as permanently failed.</summary>
    public async Task MarkFailedAsync(Guid jobId, string error, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE jobs SET status = 'failed', error = {0}, completed_at = NOW()
            WHERE id = {1}
            """,
            [error, jobId],
            ct);
    }

    /// <summary>Get job stats grouped by type and status.</summary>
    public async Task<JobStatsResponse> GetStatsAsync(Guid notebookId, CancellationToken ct)
    {
        var rows = await db.Jobs
            .Where(j => j.NotebookId == notebookId)
            .GroupBy(j => new { j.JobType, j.Status })
            .Select(g => new { g.Key.JobType, g.Key.Status, Count = g.LongCount() })
            .ToListAsync(ct);

        JobTypeStats BuildStats(string jobType) => new()
        {
            Pending = rows.FirstOrDefault(r => r.JobType == jobType && r.Status == "pending")?.Count ?? 0,
            InProgress = rows.FirstOrDefault(r => r.JobType == jobType && r.Status == "in_progress")?.Count ?? 0,
            Completed = rows.FirstOrDefault(r => r.JobType == jobType && r.Status == "completed")?.Count ?? 0,
            Failed = rows.FirstOrDefault(r => r.JobType == jobType && r.Status == "failed")?.Count ?? 0,
        };

        return new JobStatsResponse
        {
            DistillClaims = BuildStats("DISTILL_CLAIMS"),
            CompareClaims = BuildStats("COMPARE_CLAIMS"),
            ClassifyTopic = BuildStats("CLASSIFY_TOPIC"),
        };
    }
}
```

## 3.3 — Result Processing Logic

The `complete_job` handler needs to process results differently based on job type. Create a dedicated service:

### IJobResultProcessor interface

```csharp
using System.Text.Json;
using Notebook.Data.Entities;

namespace Notebook.Server.Services;

public interface IJobResultProcessor
{
    Task<int> ProcessResultAsync(JobEntity job, JsonElement result, CancellationToken ct);
}
```

### JobResultProcessor implementation

```csharp
using System.Text.Json;
using Notebook.Data.Entities;
using Notebook.Data.Repositories;

namespace Notebook.Server.Services;

public class JobResultProcessor(
    IEntryRepository entryRepo,
    IJobRepository jobRepo) : IJobResultProcessor
{
    public async Task<int> ProcessResultAsync(
        JobEntity job, JsonElement result, CancellationToken ct)
    {
        var followUpJobs = 0;

        switch (job.JobType)
        {
            case "DISTILL_CLAIMS":
            {
                var entryId = Guid.Parse(job.Payload.RootElement.GetProperty("entry_id").GetString()!);
                var claims = result.GetProperty("claims");

                // Write claims to entry
                var claimsList = JsonSerializer.Deserialize<List<Notebook.Core.Types.Claim>>(claims)!;
                await entryRepo.UpdateEntryClaimsAsync(entryId, job.NotebookId, claimsList, ct);

                // Create comparison jobs against topic indices
                var indices = await entryRepo.FindTopicIndicesAsync(job.NotebookId, ct);
                foreach (var (indexId, indexClaims) in indices)
                {
                    var comparePayload = JsonSerializer.SerializeToDocument(new
                    {
                        entry_id = entryId.ToString(),
                        compare_against_id = indexId.ToString(),
                        claims_a = indexClaims,
                        claims_b = claimsList,
                    });
                    await jobRepo.InsertJobAsync(job.NotebookId, "COMPARE_CLAIMS", comparePayload, ct);
                    followUpJobs++;
                }
                break;
            }

            case "COMPARE_CLAIMS":
            {
                var entryId = Guid.Parse(job.Payload.RootElement.GetProperty("entry_id").GetString()!);

                // Append comparison result to the entry
                await entryRepo.AppendComparisonAsync(entryId, result, ct);
                break;
            }

            case "CLASSIFY_TOPIC":
            {
                var entryId = Guid.Parse(job.Payload.RootElement.GetProperty("entry_id").GetString()!);
                var topic = result.GetProperty("primary_topic").GetString()!;

                await entryRepo.UpdateEntryTopicAsync(entryId, topic, ct);
                break;
            }

            default:
                throw new InvalidOperationException($"Unknown job type: {job.JobType}");
        }

        return followUpJobs;
    }
}
```

### Register the service

In `Program.cs`:

```csharp
builder.Services.AddScoped<IJobResultProcessor, JobResultProcessor>();
```

## 3.4 — Additional Repository Methods

Add to `IEntryRepository` and `EntryRepository`:

```csharp
/// <summary>Append a comparison result to an entry.</summary>
public async Task AppendComparisonAsync(Guid entryId, JsonElement comparison, CancellationToken ct)
{
    var friction = comparison.TryGetProperty("friction", out var f) ? f.GetDouble() : 0.0;
    var comparisonJson = JsonSerializer.Serialize(comparison);

    await _db.Database.ExecuteSqlRawAsync(
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

/// <summary>Update an entry's topic.</summary>
public async Task UpdateEntryTopicAsync(Guid entryId, string topic, CancellationToken ct)
{
    await _db.Database.ExecuteSqlRawAsync(
        "UPDATE entries SET topic = {0} WHERE id = {1}",
        [topic, entryId],
        ct);
}
```

## 3.5 — Tests

### JobModelTests.cs

```csharp
using System.Text.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class JobModelTests
{
    [Fact]
    public void CompleteJobRequest_Deserialize()
    {
        var json = """
        {
            "worker_id": "robot-1",
            "result": {
                "claims": [
                    {"text": "Test claim", "confidence": 0.9}
                ]
            }
        }
        """;

        var request = JsonSerializer.Deserialize<CompleteJobRequest>(json)!;
        Assert.Equal("robot-1", request.WorkerId);
        Assert.True(request.Result.TryGetProperty("claims", out _));
    }

    [Fact]
    public void FailJobRequest_Deserialize()
    {
        var json = """{"worker_id": "robot-1", "error": "LLM returned invalid JSON"}""";
        var request = JsonSerializer.Deserialize<FailJobRequest>(json)!;
        Assert.Equal("LLM returned invalid JSON", request.Error);
    }

    [Fact]
    public void JobStatsResponse_Serialize()
    {
        var stats = new JobStatsResponse
        {
            DistillClaims = new JobTypeStats { Pending = 5, InProgress = 2, Completed = 10, Failed = 1 },
            CompareClaims = new JobTypeStats(),
            ClassifyTopic = new JobTypeStats(),
        };
        var json = JsonSerializer.Serialize(stats);
        Assert.Contains("DISTILL_CLAIMS", json);
        Assert.Contains("COMPARE_CLAIMS", json);
    }
}
```

## Verify

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

### Manual integration test flow

```bash
# 1. Write an entry (creates a DISTILL_CLAIMS job)
curl -X POST http://localhost:5000/notebooks/$NB/entries \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"content": "Test content", "content_type": "text/plain"}'

# 2. Check job stats
curl http://localhost:5000/notebooks/$NB/jobs/stats \
  -H "Authorization: Bearer $TOKEN"

# 3. Pull a job
curl "http://localhost:5000/notebooks/$NB/jobs/next?worker_id=test&type=DISTILL_CLAIMS" \
  -H "Authorization: Bearer $TOKEN"

# 4. Complete the job
curl -X POST http://localhost:5000/notebooks/$NB/jobs/$JOB_ID/complete \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "worker_id": "test",
    "result": {
      "claims": [{"text": "Test claim", "confidence": 0.9}]
    }
  }'
```
