using System.Text.Json;
using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public class JobRepository(NotebookDbContext db) : IJobRepository
{
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

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job.Id;
    }
}
