using System.Text.Json;
using Notebook.Data.Entities;

namespace Notebook.Server.Services;

public interface IJobResultProcessor
{
    Task<int> ProcessResultAsync(JobEntity job, JsonElement result, CancellationToken ct);
}
