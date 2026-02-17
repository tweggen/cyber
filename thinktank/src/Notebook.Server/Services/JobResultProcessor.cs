using System.Text;
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

                    // Check if this entry is a fragment — if so, chain the next fragment
                    var entry = await entryRepo.GetEntryAsync(entryId, job.NotebookId, ct);
                    if (entry?.FragmentOf is not null && entry.FragmentIndex is not null)
                    {
                        followUpJobs += await ChainFragmentDistillation(
                            job.NotebookId, entry.FragmentOf.Value, entry.FragmentIndex.Value, ct);
                    }
                    else
                    {
                        // Non-fragment entry (or artifact after all fragments distilled):
                        // create comparison jobs against topic indices
                        followUpJobs += await CreateComparisonJobs(job.NotebookId, entryId, claimsList, ct);
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

    /// <summary>
    /// After a fragment's claims are distilled, chain the next fragment or finalize the artifact.
    /// </summary>
    private async Task<int> ChainFragmentDistillation(
        Guid notebookId, Guid artifactId, int currentFragmentIndex, CancellationToken ct)
    {
        var nextFragment = await entryRepo.GetFragmentAsync(notebookId, artifactId, currentFragmentIndex + 1, ct);

        if (nextFragment is not null)
        {
            // Gather claims from fragments 0..current as context
            var contextClaims = await entryRepo.GetFragmentClaimsUpToAsync(
                notebookId, artifactId, currentFragmentIndex, ct);

            var content = Encoding.UTF8.GetString(nextFragment.Content);
            var payload = JsonSerializer.SerializeToDocument(new
            {
                entry_id = nextFragment.Id.ToString(),
                content,
                context_claims = contextClaims.Select(c => new { c.Text, c.Confidence }).ToList(),
                max_claims = 12,
            });

            await jobRepo.InsertJobAsync(notebookId, "DISTILL_CLAIMS", payload, ct);
            return 1;
        }

        // Last fragment — gather all fragment claims and distill the artifact entry
        var allClaims = await entryRepo.GetFragmentClaimsUpToAsync(
            notebookId, artifactId, currentFragmentIndex, ct);

        var artifact = await entryRepo.GetEntryAsync(artifactId, notebookId, ct);
        if (artifact is not null)
        {
            var artifactContent = Encoding.UTF8.GetString(artifact.Content);
            var payload = JsonSerializer.SerializeToDocument(new
            {
                entry_id = artifactId.ToString(),
                content = artifactContent,
                context_claims = allClaims.Select(c => new { c.Text, c.Confidence }).ToList(),
                max_claims = 12,
            });

            await jobRepo.InsertJobAsync(notebookId, "DISTILL_CLAIMS", payload, ct);
            return 1;
        }

        return 0;
    }

    private async Task<int> CreateComparisonJobs(
        Guid notebookId, Guid entryId, List<Claim> claimsList, CancellationToken ct)
    {
        var followUpJobs = 0;
        var indices = await entryRepo.FindTopicIndicesAsync(notebookId, ct);
        foreach (var (indexId, indexClaims) in indices)
        {
            var comparePayload = JsonSerializer.SerializeToDocument(new
            {
                entry_id = entryId.ToString(),
                compare_against_id = indexId.ToString(),
                claims_a = indexClaims,
                claims_b = claimsList,
            });
            await jobRepo.InsertJobAsync(notebookId, "COMPARE_CLAIMS", comparePayload, ct);
            followUpJobs++;
        }
        return followUpJobs;
    }
}
