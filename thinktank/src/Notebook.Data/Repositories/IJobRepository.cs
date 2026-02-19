using System.Text.Json;
using Notebook.Data.Entities;

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
    Task<List<JobStatusCount>> GetStatsAsync(Guid notebookId, CancellationToken ct);
    Task<long> CountPendingAsync(Guid notebookId, CancellationToken ct);
    Task<int> RetryFailedJobsAsync(Guid notebookId, CancellationToken ct);
}

public record JobStatusCount(string JobType, string Status, long Count);
