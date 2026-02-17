using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;
using Notebook.Server.Models;
using Notebook.Server.Services;

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

    /// <summary>
    /// Atomically claims the next pending job, transitioning it to in_progress.
    /// Returns 204 if no jobs available.
    /// </summary>
    private static async Task<IResult> NextJob(
        Guid notebookId,
        [FromQuery(Name = "worker_id")] string workerId,
        [FromQuery(Name = "type")] string? jobType,
        IJobRepository jobRepo,
        CancellationToken ct)
    {
        await jobRepo.ReclaimTimedOutJobsAsync(notebookId, ct);

        var job = await jobRepo.ClaimNextJobAsync(notebookId, jobType, workerId, ct);
        var queueDepth = await jobRepo.CountPendingAsync(notebookId, ct);

        if (job is null)
            return Results.Ok(new { queue_depth = queueDepth });

        return Results.Ok(new JobResponse
        {
            Id = job.Id,
            JobType = job.JobType,
            Status = job.Status,
            Payload = job.Payload.RootElement,
            Created = job.Created,
            ClaimedAt = job.ClaimedAt,
            ClaimedBy = job.ClaimedBy,
            QueueDepth = queueDepth,
        });
    }

    /// <summary>
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
        var job = await jobRepo.GetJobAsync(jobId, ct);
        if (job is null)
            return Results.NotFound(new { error = $"Job {jobId} not found" });

        if (job.Status != "in_progress" || job.ClaimedBy != request.WorkerId)
            return Results.Conflict(new { error = "Job not claimed by this worker" });

        var followUpJobs = await resultProcessor.ProcessResultAsync(job, request.Result, ct);

        await jobRepo.CompleteJobAsync(jobId, request.WorkerId, request.Result, ct);

        return Results.Ok(new { status = "completed", follow_up_jobs = followUpJobs });
    }

    /// <summary>
    /// Robot reports a job failure. Retries if under max_retries, otherwise marks as failed.
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
            await jobRepo.ReturnToPendingAsync(jobId, request.Error, ct);
            return Results.Ok(new { status = "pending", retry_count = job.RetryCount + 1 });
        }
        else
        {
            await jobRepo.MarkFailedAsync(jobId, request.Error, ct);
            return Results.Ok(new { status = "failed" });
        }
    }

    /// <summary>
    /// Queue depth and processing stats per job type.
    /// </summary>
    private static async Task<IResult> JobStats(
        Guid notebookId,
        IJobRepository jobRepo,
        CancellationToken ct)
    {
        var rows = await jobRepo.GetStatsAsync(notebookId, ct);

        JobTypeStats BuildStats(string jobType) => new()
        {
            Pending = rows.FirstOrDefault(r => r.JobType == jobType && r.Status == "pending")?.Count ?? 0,
            InProgress = rows.FirstOrDefault(r => r.JobType == jobType && r.Status == "in_progress")?.Count ?? 0,
            Completed = rows.FirstOrDefault(r => r.JobType == jobType && r.Status == "completed")?.Count ?? 0,
            Failed = rows.FirstOrDefault(r => r.JobType == jobType && r.Status == "failed")?.Count ?? 0,
        };

        return Results.Ok(new JobStatsResponse
        {
            DistillClaims = BuildStats("DISTILL_CLAIMS"),
            CompareClaims = BuildStats("COMPARE_CLAIMS"),
            ClassifyTopic = BuildStats("CLASSIFY_TOPIC"),
            EmbedClaims = BuildStats("EMBED_CLAIMS"),
        });
    }
}
