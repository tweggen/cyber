using System.Text.Json;
using Notebook.Core.Types;
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

                    var claimsList = JsonSerializer.Deserialize<List<Claim>>(claims)!;
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
