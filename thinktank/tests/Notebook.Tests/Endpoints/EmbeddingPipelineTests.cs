using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Notebook.Core.Types;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

/// <summary>
/// Integration tests for the embedding-based nearest-neighbor comparison pipeline.
/// Verifies: DISTILL_CLAIMS → EMBED_CLAIMS → COMPARE_CLAIMS flow.
/// Requires a running PostgreSQL instance.
/// </summary>
public class EmbeddingPipelineTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    public EmbeddingPipelineTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    private async Task<Guid> CreateNotebook(string name)
    {
        var response = await _client.PostAsJsonAsync("/notebooks", new { name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task DistillClaims_CreatesEmbedClaimsJob_NotCompareClaimsDirectly()
    {
        var notebookId = await CreateNotebook("embed-pipeline-test");

        // Write an entry to trigger DISTILL_CLAIMS
        var batchResponse = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "Pigeons in Austria are common in urban areas.", content_type = (string?)null, topic = "test/pigeons" }
            }
        });
        batchResponse.EnsureSuccessStatusCode();

        // Claim the DISTILL_CLAIMS job
        var nextJobResponse = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
        Assert.Equal(HttpStatusCode.OK, nextJobResponse.StatusCode);

        var job = await nextJobResponse.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(job);
        Assert.Equal("DISTILL_CLAIMS", job.JobType);

        // Complete with mock claims
        var mockClaims = new[]
        {
            new { text = "Pigeons are common in Austrian urban areas", confidence = 0.9 },
            new { text = "Urban pigeon populations thrive in Austria", confidence = 0.85 }
        };

        var completeResponse = await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{job.Id}/complete",
            new { worker_id = "test-worker", result = new { claims = mockClaims } });
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var completeBody = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var followUpJobs = completeBody.GetProperty("follow_up_jobs").GetInt32();
        Assert.Equal(1, followUpJobs); // Should create EMBED_CLAIMS, not COMPARE_CLAIMS

        // The follow-up should be EMBED_CLAIMS
        var embedJobResponse = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=EMBED_CLAIMS");
        Assert.Equal(HttpStatusCode.OK, embedJobResponse.StatusCode);

        var embedJob = await embedJobResponse.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(embedJob);
        Assert.Equal("EMBED_CLAIMS", embedJob.JobType);

        // Verify payload contains claim_texts
        var claimTexts = embedJob.Payload.GetProperty("claim_texts");
        Assert.Equal(2, claimTexts.GetArrayLength());
    }

    [Fact]
    public async Task EmbedClaims_StoresEmbedding_CreatesCompareClaimsAgainstNeighbors()
    {
        var notebookId = await CreateNotebook("embed-neighbor-test");

        // 1. Create two entries with embeddings already set (simulating prior pipeline runs)
        //    We'll do this by writing entries, distilling, and completing EMBED_CLAIMS for them.
        var entry1Response = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "Cats are popular pets worldwide.", content_type = (string?)null, topic = "test/cats" }
            }
        });
        entry1Response.EnsureSuccessStatusCode();
        var entry1Result = await entry1Response.Content.ReadFromJsonAsync<BatchWriteResponse>();
        var entry1Id = entry1Result!.Results[0].EntryId;

        var entry2Response = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "Dogs are loyal companions.", content_type = (string?)null, topic = "test/dogs" }
            }
        });
        entry2Response.EnsureSuccessStatusCode();
        var entry2Result = await entry2Response.Content.ReadFromJsonAsync<BatchWriteResponse>();
        var entry2Id = entry2Result!.Results[0].EntryId;

        // Distill claims for both entries
        for (var i = 0; i < 2; i++)
        {
            var nextJob = await _client.GetAsync(
                $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
            Assert.Equal(HttpStatusCode.OK, nextJob.StatusCode);
            var distillJob = await nextJob.Content.ReadFromJsonAsync<JobResponse>();

            var claims = i == 0
                ? new[] { new { text = "Cats are popular pets", confidence = 0.9 } }
                : new[] { new { text = "Dogs are loyal companions", confidence = 0.9 } };

            await _client.PostAsJsonAsync(
                $"/notebooks/{notebookId}/jobs/{distillJob!.Id}/complete",
                new { worker_id = "test-worker", result = new { claims } });
        }

        // Complete EMBED_CLAIMS for both entries with known embeddings
        // Entry 1: embedding [1, 0, 0] (unit vector)
        // Entry 2: embedding [0, 1, 0] (orthogonal)
        for (var i = 0; i < 2; i++)
        {
            var nextEmbed = await _client.GetAsync(
                $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=EMBED_CLAIMS");
            Assert.Equal(HttpStatusCode.OK, nextEmbed.StatusCode);
            var embedJob = await nextEmbed.Content.ReadFromJsonAsync<JobResponse>();

            var entryIdStr = embedJob!.Payload.GetProperty("entry_id").GetString();
            double[] embedding = entryIdStr == entry1Id.ToString()
                ? [1.0, 0.0, 0.0]
                : [0.0, 1.0, 0.0];

            await _client.PostAsJsonAsync(
                $"/notebooks/{notebookId}/jobs/{embedJob.Id}/complete",
                new { worker_id = "test-worker", result = new { embedding } });
        }

        // 2. Now write a third entry that should match entry1 better
        var entry3Response = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "Felines make great household pets.", content_type = (string?)null, topic = "test/felines" }
            }
        });
        entry3Response.EnsureSuccessStatusCode();

        // Distill entry3
        var distillJob3 = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
        var dj3 = await distillJob3.Content.ReadFromJsonAsync<JobResponse>();
        await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{dj3!.Id}/complete",
            new { worker_id = "test-worker", result = new { claims = new[] { new { text = "Felines are great pets", confidence = 0.92 } } } });

        // Complete entry3's EMBED_CLAIMS with embedding close to entry1: [0.9, 0.1, 0]
        var embedJob3 = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=EMBED_CLAIMS");
        Assert.Equal(HttpStatusCode.OK, embedJob3.StatusCode);
        var ej3 = await embedJob3.Content.ReadFromJsonAsync<JobResponse>();

        var completeEmbed3 = await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{ej3!.Id}/complete",
            new { worker_id = "test-worker", result = new { embedding = new[] { 0.9, 0.1, 0.0 } } });
        Assert.Equal(HttpStatusCode.OK, completeEmbed3.StatusCode);

        var embed3Body = await completeEmbed3.Content.ReadFromJsonAsync<JsonElement>();
        var compareJobs = embed3Body.GetProperty("follow_up_jobs").GetInt32();

        // Should have created COMPARE_CLAIMS jobs against neighbors (entry1 and entry2)
        Assert.Equal(2, compareJobs);

        // Verify COMPARE_CLAIMS jobs exist (1 from entry2's embed + 2 from entry3's embed = 3 total)
        var statsResponse = await _client.GetAsync($"/notebooks/{notebookId}/jobs/stats");
        var stats = await statsResponse.Content.ReadFromJsonAsync<JobStatsResponse>();
        Assert.NotNull(stats);
        Assert.Equal(3, stats.CompareClaims.Pending);
    }

    [Fact]
    public async Task FragmentDistill_DoesNotCreateEmbedClaims()
    {
        var notebookId = await CreateNotebook("fragment-no-embed-test");

        // Create large content to trigger fragmentation
        var section1 = "# Section One\n\n" + new string('A', 9000) + "\n\n";
        var section2 = "# Section Two\n\n" + new string('B', 9000) + "\n\n";
        var largeMarkdown = section1 + section2;

        var batchResponse = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = largeMarkdown, content_type = "text/markdown", topic = "test/fragment" }
            }
        });
        batchResponse.EnsureSuccessStatusCode();

        // Claim fragment 0's DISTILL_CLAIMS job
        var nextJob = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
        Assert.Equal(HttpStatusCode.OK, nextJob.StatusCode);
        var job = await nextJob.Content.ReadFromJsonAsync<JobResponse>();

        // Complete fragment 0 — should chain to fragment 1, NOT create EMBED_CLAIMS
        var completeResponse = await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{job!.Id}/complete",
            new
            {
                worker_id = "test-worker",
                result = new { claims = new[] { new { text = "Section one content", confidence = 0.9 } } }
            });
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var body = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("follow_up_jobs").GetInt32()); // Chains to next fragment

        // Next job should be DISTILL_CLAIMS (for fragment 1), NOT EMBED_CLAIMS
        var nextJob2 = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
        Assert.Equal(HttpStatusCode.OK, nextJob2.StatusCode);
        var job2 = await nextJob2.Content.ReadFromJsonAsync<JobResponse>();
        Assert.Equal("DISTILL_CLAIMS", job2!.JobType);

        // No EMBED_CLAIMS should exist yet — response is 200 with just queue_depth, no "id"
        var embedCheck = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=EMBED_CLAIMS");
        Assert.Equal(HttpStatusCode.OK, embedCheck.StatusCode);
        var embedBody = await embedCheck.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(embedBody.TryGetProperty("id", out _), "Expected no job, but got one");
    }

    [Fact]
    public async Task EntriesWithoutEmbeddings_NotIncludedAsNeighbors()
    {
        var notebookId = await CreateNotebook("no-embedding-skip-test");

        // Write an entry but DON'T complete its embed job — it should have no embedding
        var entry1Response = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "Entry without embedding.", content_type = (string?)null, topic = "test/no-embed" }
            }
        });
        entry1Response.EnsureSuccessStatusCode();

        // Distill it but don't complete EMBED_CLAIMS
        var distillJob = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
        var dj = await distillJob.Content.ReadFromJsonAsync<JobResponse>();
        await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{dj!.Id}/complete",
            new { worker_id = "test-worker", result = new { claims = new[] { new { text = "No embedding entry", confidence = 0.9 } } } });

        // Leave the EMBED_CLAIMS job pending (don't complete it)

        // Write a second entry and complete the full pipeline
        var entry2Response = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "Entry with embedding.", content_type = (string?)null, topic = "test/with-embed" }
            }
        });
        entry2Response.EnsureSuccessStatusCode();

        // Distill entry2
        var distillJob2 = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
        var dj2 = await distillJob2.Content.ReadFromJsonAsync<JobResponse>();
        await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{dj2!.Id}/complete",
            new { worker_id = "test-worker", result = new { claims = new[] { new { text = "Has embedding", confidence = 0.9 } } } });

        // Complete entry2's EMBED_CLAIMS — should find 0 neighbors (entry1 has no embedding)
        // First skip entry1's embed job by claiming it
        var embedJob1 = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=EMBED_CLAIMS");
        var ej1 = await embedJob1.Content.ReadFromJsonAsync<JobResponse>();

        // Claim entry2's embed job
        var embedJob2 = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=EMBED_CLAIMS");
        Assert.Equal(HttpStatusCode.OK, embedJob2.StatusCode);
        var ej2 = await embedJob2.Content.ReadFromJsonAsync<JobResponse>();

        var completeEmbed = await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{ej2!.Id}/complete",
            new { worker_id = "test-worker", result = new { embedding = new[] { 1.0, 0.0, 0.0 } } });
        Assert.Equal(HttpStatusCode.OK, completeEmbed.StatusCode);

        var body = await completeEmbed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("follow_up_jobs").GetInt32()); // No neighbors with embeddings
    }

    [Fact]
    public async Task CosineSimilarity_OrdersNeighborsCorrectly()
    {
        var notebookId = await CreateNotebook("cosine-order-test");

        // Create 3 entries with specific embeddings
        var entries = new[]
        {
            ("Close match", new[] { 0.95, 0.05, 0.0 }),   // Very similar to query
            ("Medium match", new[] { 0.5, 0.5, 0.0 }),    // Moderate similarity
            ("Far match", new[] { 0.0, 0.0, 1.0 }),       // Orthogonal
        };

        var entryIds = new List<Guid>();
        foreach (var (content, _) in entries)
        {
            var resp = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
            {
                entries = new[] { new { content, content_type = (string?)null, topic = "test/cosine" } }
            });
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<BatchWriteResponse>();
            entryIds.Add(result!.Results[0].EntryId);
        }

        // Distill all 3
        for (var i = 0; i < 3; i++)
        {
            var nextJob = await _client.GetAsync(
                $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
            var job = await nextJob.Content.ReadFromJsonAsync<JobResponse>();
            await _client.PostAsJsonAsync(
                $"/notebooks/{notebookId}/jobs/{job!.Id}/complete",
                new { worker_id = "test-worker", result = new { claims = new[] { new { text = $"Claim {i}", confidence = 0.9 } } } });
        }

        // Complete EMBED_CLAIMS for all 3 with known embeddings
        for (var i = 0; i < 3; i++)
        {
            var nextEmbed = await _client.GetAsync(
                $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=EMBED_CLAIMS");
            var embedJob = await nextEmbed.Content.ReadFromJsonAsync<JobResponse>();
            var entryIdStr = embedJob!.Payload.GetProperty("entry_id").GetString();
            var idx = entryIds.FindIndex(id => id.ToString() == entryIdStr);
            var (_, embedding) = entries[idx];

            await _client.PostAsJsonAsync(
                $"/notebooks/{notebookId}/jobs/{embedJob.Id}/complete",
                new { worker_id = "test-worker", result = new { embedding } });
        }

        // Now add a 4th entry with embedding [1, 0, 0] — should find close > medium > far
        var queryResp = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[] { new { content = "Query entry", content_type = (string?)null, topic = "test/query" } }
        });
        queryResp.EnsureSuccessStatusCode();

        // Distill
        var distillQuery = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
        var dq = await distillQuery.Content.ReadFromJsonAsync<JobResponse>();
        await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{dq!.Id}/complete",
            new { worker_id = "test-worker", result = new { claims = new[] { new { text = "Query claim", confidence = 0.9 } } } });

        // Complete EMBED_CLAIMS for query entry
        var queryEmbed = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=EMBED_CLAIMS");
        var qe = await queryEmbed.Content.ReadFromJsonAsync<JobResponse>();

        var completeQuery = await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{qe!.Id}/complete",
            new { worker_id = "test-worker", result = new { embedding = new[] { 1.0, 0.0, 0.0 } } });
        Assert.Equal(HttpStatusCode.OK, completeQuery.StatusCode);

        var queryBody = await completeQuery.Content.ReadFromJsonAsync<JsonElement>();
        // Should have 3 COMPARE_CLAIMS jobs (one against each neighbor)
        Assert.Equal(3, queryBody.GetProperty("follow_up_jobs").GetInt32());
    }

    [Fact]
    public async Task EmbedClaims_AppearsInJobStats()
    {
        var notebookId = await CreateNotebook("embed-stats-test");

        // Write entry to generate pipeline
        await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "Stats test entry.", content_type = (string?)null, topic = "test/stats" }
            }
        });

        // Distill
        var distillJob = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
        var dj = await distillJob.Content.ReadFromJsonAsync<JobResponse>();
        await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{dj!.Id}/complete",
            new { worker_id = "test-worker", result = new { claims = new[] { new { text = "Stats claim", confidence = 0.9 } } } });

        // Check stats — should show EMBED_CLAIMS pending
        var statsResponse = await _client.GetAsync($"/notebooks/{notebookId}/jobs/stats");
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);

        var stats = await statsResponse.Content.ReadFromJsonAsync<JobStatsResponse>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats.EmbedClaims.Pending);
    }
}
