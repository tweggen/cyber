using System.Text;
using System.Text.Json;
using Notebook.Core.Types;
using Notebook.Data.Entities;
using Notebook.Data.Repositories;

namespace Notebook.Server.Services;

public class JobResultProcessor(
    IEntryRepository entryRepo,
    IJobRepository jobRepo,
    IMirroredContentRepository mirroredRepo) : IJobResultProcessor
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
                        // embed claims for semantic nearest-neighbor comparison
                        if (claimsList.Count > 0)
                            followUpJobs += await CreateEmbedClaimsJob(job.NotebookId, entryId, claimsList, ct);
                    }
                    break;
                }

            case "EMBED_CLAIMS":
                {
                    var entryId = Guid.Parse(job.Payload.RootElement.GetProperty("entry_id").GetString()!);
                    var embedding = JsonSerializer.Deserialize<double[]>(result.GetProperty("embedding"))!;

                    await entryRepo.UpdateEntryEmbeddingAsync(entryId, job.NotebookId, embedding, ct);

                    // Use cross-boundary search that includes mirrored claims
                    var neighbors = await entryRepo.FindNearestWithMirroredAsync(
                        job.NotebookId, entryId, embedding, 5, ct);

                    // Set expected comparisons for integration status tracking
                    await entryRepo.UpdateExpectedComparisonsAsync(
                        entryId, job.NotebookId, neighbors.Count, ct);

                    if (neighbors.Count == 0)
                    {
                        // Nothing to contradict — auto-integrate
                        await entryRepo.UpdateIntegrationStatusAsync(
                            entryId, IntegrationStatus.Integrated, ct);
                    }
                    else
                    {
                        // Get the entry's claims to pass into comparison jobs
                        var entry = await entryRepo.GetEntryAsync(entryId, job.NotebookId, ct);
                        var entryClaims = entry?.Claims ?? [];

                        foreach (var neighbor in neighbors)
                        {
                            if (neighbor.IsMirrored)
                            {
                                // Cross-boundary comparison with discount factor
                                var comparePayload = JsonSerializer.SerializeToDocument(new
                                {
                                    entry_id = entryId.ToString(),
                                    compare_against_id = neighbor.Id.ToString(),
                                    claims_a = neighbor.Claims,
                                    claims_b = entryClaims,
                                    cross_boundary = true,
                                    subscription_id = neighbor.SubscriptionId?.ToString(),
                                    discount_factor = neighbor.DiscountFactor,
                                });
                                await jobRepo.InsertJobAsync(job.NotebookId, "COMPARE_CLAIMS", comparePayload, ct);
                            }
                            else
                            {
                                var comparePayload = JsonSerializer.SerializeToDocument(new
                                {
                                    entry_id = entryId.ToString(),
                                    compare_against_id = neighbor.Id.ToString(),
                                    claims_a = neighbor.Claims,
                                    claims_b = entryClaims,
                                });
                                await jobRepo.InsertJobAsync(job.NotebookId, "COMPARE_CLAIMS", comparePayload, ct);
                            }
                            followUpJobs++;
                        }
                    }
                    break;
                }

            case "COMPARE_CLAIMS":
                {
                    var entryId = Guid.Parse(job.Payload.RootElement.GetProperty("entry_id").GetString()!);

                    // Read optional cross-boundary discount factor from payload
                    var discountFactor = 1.0;
                    if (job.Payload.RootElement.TryGetProperty("discount_factor", out var df))
                        discountFactor = df.GetDouble();

                    var comparisonCount = await entryRepo.AppendComparisonAsync(entryId, result, discountFactor, ct);

                    // Check if all expected comparisons are complete → transition integration status
                    var entry = await entryRepo.GetEntryAsync(entryId, job.NotebookId, ct);
                    if (entry?.ExpectedComparisons is not null && comparisonCount >= entry.ExpectedComparisons)
                    {
                        var status = (entry.MaxFriction ?? 0.0) > 0.2
                            ? IntegrationStatus.Contested
                            : IntegrationStatus.Integrated;
                        await entryRepo.UpdateIntegrationStatusAsync(entryId, status, ct);
                    }
                    break;
                }

            case "CLASSIFY_TOPIC":
                {
                    var entryId = Guid.Parse(job.Payload.RootElement.GetProperty("entry_id").GetString()!);
                    var topic = result.GetProperty("primary_topic").GetString()!;
                    await entryRepo.UpdateEntryTopicAsync(entryId, topic, ct);
                    break;
                }

            case "EMBED_MIRRORED":
                {
                    var mirroredClaimId = Guid.Parse(job.Payload.RootElement.GetProperty("mirrored_claim_id").GetString()!);
                    var embedding = JsonSerializer.Deserialize<double[]>(result.GetProperty("embedding"))!;
                    await mirroredRepo.UpdateEmbeddingAsync(mirroredClaimId, embedding, ct);
                    // No follow-up jobs — mirrored claims are passive
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
                context_claims = contextClaims.Select(c => new { text = c.Text, confidence = c.Confidence }).ToList(),
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
                context_claims = allClaims.Select(c => new { text = c.Text, confidence = c.Confidence }).ToList(),
                max_claims = 12,
            });

            await jobRepo.InsertJobAsync(notebookId, "DISTILL_CLAIMS", payload, ct);
            return 1;
        }

        return 0;
    }

    private async Task<int> CreateEmbedClaimsJob(
        Guid notebookId, Guid entryId, List<Claim> claims, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToDocument(new
        {
            entry_id = entryId.ToString(),
            claim_texts = claims.Select(c => c.Text).ToList(),
        });
        await jobRepo.InsertJobAsync(notebookId, "EMBED_CLAIMS", payload, ct);
        return 1;
    }
}
