using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public class JobRepository(NotebookDbContext db) : IJobRepository
{
    private static readonly Dictionary<string, int> JobTypePriority = new()
    {
        ["EMBED_CLAIMS"] = 30,    // Fast, unblocks comparisons
        ["COMPARE_CLAIMS"] = 20,  // Produces user-visible friction scores
        ["CLASSIFY_TOPIC"] = 10,  // Produces topic labels for navigation
        ["DISTILL_CLAIMS"] = 0,   // Background intake, can wait
    };

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
            Priority = JobTypePriority.GetValueOrDefault(jobType, 0),
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job.Id;
    }

    public async Task<int> ReclaimTimedOutJobsAsync(Guid notebookId, CancellationToken ct)
    {
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

    public async Task<JobEntity?> ClaimNextJobAsync(
        Guid notebookId, string? jobType, string workerId, CancellationToken ct)
    {
        // FOR UPDATE SKIP LOCKED requires raw SQL
        var jobTypeParam = jobType ?? (object)DBNull.Value;

        var jobs = await db.Jobs.FromSqlRaw(
            """
            UPDATE jobs SET status = 'in_progress', claimed_at = NOW(), claimed_by = {0}
            WHERE id = (
                SELECT id FROM jobs
                WHERE notebook_id = {1}
                  AND status = 'pending'
                  AND ({2}::text IS NULL OR job_type = {2})
                ORDER BY priority DESC, created ASC
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            RETURNING *
            """,
            workerId, notebookId, jobTypeParam)
            .ToListAsync(ct);

        return jobs.FirstOrDefault();
    }

    public async Task<JobEntity?> GetJobAsync(Guid jobId, CancellationToken ct)
    {
        return await db.Jobs.FindAsync([jobId], ct);
    }

    public async Task CompleteJobAsync(
        Guid jobId, string workerId, JsonElement result, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE jobs
            SET status = 'completed', result = {0}::jsonb, completed_at = NOW()
            WHERE id = {1} AND claimed_by = {2} AND status = 'in_progress'
            """,
            [JsonSerializer.Serialize(result), jobId, workerId],
            ct);
    }

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

    public async Task<List<JobStatusCount>> GetStatsAsync(Guid notebookId, CancellationToken ct)
    {
        return await db.Jobs
            .Where(j => j.NotebookId == notebookId)
            .GroupBy(j => new { j.JobType, j.Status })
            .Select(g => new JobStatusCount(g.Key.JobType, g.Key.Status, g.LongCount()))
            .ToListAsync(ct);
    }

    public async Task<long> CountPendingAsync(Guid notebookId, CancellationToken ct)
    {
        return await db.Jobs
            .Where(j => j.NotebookId == notebookId && j.Status == "pending")
            .LongCountAsync(ct);
    }
}
