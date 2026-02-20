using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;
using Notebook.Server.Auth;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/notebooks/{notebookId}/jobs")
            .RequireAuthorization("CanWrite");

        group.MapGet("/next", NextJob);
        group.MapPost("/{jobId}/complete", CompleteJob);
        group.MapPost("/{jobId}/fail", FailJob);
        group.MapGet("/stats", JobStats);
        group.MapPost("/retry-failed", RetryFailedJobs);
    }

    /// <summary>
    /// Atomically claims the next pending job, transitioning it to in_progress.
    /// Returns 204 if no jobs available.
    /// </summary>
    private static async Task<IResult> NextJob(
        Guid notebookId,
        [FromQuery(Name = "worker_id")] string workerId,
        [FromQuery(Name = "type")] string? jobType,
        IAccessControl acl,
        IJobRepository jobRepo,
        IAgentRepository agentRepo,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireWriteAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var agentId = httpContext.User.FindFirst("agent_id")?.Value;

        await jobRepo.ReclaimTimedOutJobsAsync(notebookId, ct);

        var job = await jobRepo.ClaimNextJobAsync(notebookId, jobType, workerId, agentId, ct);
        var queueDepth = await jobRepo.CountPendingAsync(notebookId, ct);

        if (job is not null && agentId is not null)
            await agentRepo.TouchLastSeenAsync(agentId, ct);

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
        IAccessControl acl,
        IJobRepository jobRepo,
        IJobResultProcessor resultProcessor,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireWriteAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var job = await jobRepo.GetJobAsync(jobId, ct);
        if (job is null)
            return Results.NotFound(new { error = $"Job {jobId} not found" });

        if (job.Status != "in_progress" || job.ClaimedBy != request.WorkerId)
            return Results.Conflict(new { error = "Job not claimed by this worker" });

        var followUpJobs = await resultProcessor.ProcessResultAsync(job, request.Result, ct);

        await jobRepo.CompleteJobAsync(jobId, request.WorkerId, request.Result, ct);

        AuditHelper.LogAction(audit, httpContext, "job.complete", notebookId,
            targetType: "job", targetId: jobId.ToString(),
            detail: new { job_type = job.JobType, follow_up_jobs = followUpJobs });

        return Results.Ok(new { status = "completed", follow_up_jobs = followUpJobs });
    }

    /// <summary>
    /// Robot reports a job failure. Retries if under max_retries, otherwise marks as failed.
    /// </summary>
    private static async Task<IResult> FailJob(
        Guid notebookId,
        Guid jobId,
        [FromBody] FailJobRequest request,
        IAccessControl acl,
        IJobRepository jobRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireWriteAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var job = await jobRepo.GetJobAsync(jobId, ct);
        if (job is null)
            return Results.NotFound(new { error = $"Job {jobId} not found" });

        if (job.ClaimedBy != request.WorkerId)
            return Results.Conflict(new { error = "Job not claimed by this worker" });

        if (job.RetryCount < job.MaxRetries)
        {
            await jobRepo.ReturnToPendingAsync(jobId, request.Error, ct);

            AuditHelper.LogAction(audit, httpContext, "job.fail", notebookId,
                targetType: "job", targetId: jobId.ToString(),
                detail: new { job_type = job.JobType, error = request.Error, retrying = true });

            return Results.Ok(new { status = "pending", retry_count = job.RetryCount + 1 });
        }
        else
        {
            await jobRepo.MarkFailedAsync(jobId, request.Error, ct);

            AuditHelper.LogAction(audit, httpContext, "job.fail", notebookId,
                targetType: "job", targetId: jobId.ToString(),
                detail: new { job_type = job.JobType, error = request.Error, retrying = false });

            return Results.Ok(new { status = "failed" });
        }
    }

    /// <summary>
    /// Resets all failed jobs back to pending so they can be retried.
    /// </summary>
    private static async Task<IResult> RetryFailedJobs(
        Guid notebookId,
        IAccessControl acl,
        IJobRepository jobRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireWriteAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var count = await jobRepo.RetryFailedJobsAsync(notebookId, ct);

        AuditHelper.LogAction(audit, httpContext, "job.retry_all", notebookId,
            detail: new { retried = count });

        return Results.Ok(new { retried = count });
    }

    /// <summary>
    /// Queue depth and processing stats per job type.
    /// </summary>
    private static async Task<IResult> JobStats(
        Guid notebookId,
        IAccessControl acl,
        IJobRepository jobRepo,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireReadAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

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
