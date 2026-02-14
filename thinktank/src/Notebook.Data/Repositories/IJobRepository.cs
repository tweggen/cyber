using System.Text.Json;

namespace Notebook.Data.Repositories;

public interface IJobRepository
{
    Task<Guid> InsertJobAsync(Guid notebookId, string jobType, JsonDocument payload, CancellationToken ct);
}
