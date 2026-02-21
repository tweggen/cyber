using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Notebook.Core.Types;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

/// <summary>
/// Integration tests for the server-side normalization, fragmentation, and
/// sequential distillation chaining pipeline.
/// Requires a running PostgreSQL instance.
/// </summary>
public class NormalizationPipelineTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    public NormalizationPipelineTests(NotebookApiFixture fixture)
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
    public async Task HtmlEntry_NormalizedToMarkdown()
    {
        var notebookId = await CreateNotebook("html-normalize-test");

        var html = "<html><body><h1>Title</h1><p>Some paragraph with <strong>bold</strong> text.</p></body></html>";

        var batchResponse = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = html, content_type = "text/html", topic = "test/html" }
            }
        });

        Assert.Equal(HttpStatusCode.Created, batchResponse.StatusCode);

        var batchResult = await batchResponse.Content.ReadFromJsonAsync<BatchWriteResponse>();
        Assert.NotNull(batchResult);
        Assert.Single(batchResult.Results);
        Assert.NotEqual(Guid.Empty, batchResult.Results[0].EntryId);

        // Verify stored entry via browse — should have topic "test/html"
        var browseResponse = await _client.GetAsync(
            $"/notebooks/{notebookId}/browse?topic_prefix=test/html");
        Assert.Equal(HttpStatusCode.OK, browseResponse.StatusCode);

        var browseBody = await browseResponse.Content.ReadFromJsonAsync<JsonElement>();
        var entries = browseBody.GetProperty("entries");
        Assert.True(entries.GetArrayLength() >= 1);

        // Verify a DISTILL_CLAIMS job was created
        Assert.Equal(1, batchResult.JobsCreated);
    }

    [Fact]
    public async Task PlainTextEntry_PassesThroughUnchanged()
    {
        var notebookId = await CreateNotebook("plaintext-passthrough-test");

        var batchResponse = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "Just plain text.", content_type = (string?)null, topic = "test/plain" }
            }
        });

        Assert.Equal(HttpStatusCode.Created, batchResponse.StatusCode);

        var batchResult = await batchResponse.Content.ReadFromJsonAsync<BatchWriteResponse>();
        Assert.NotNull(batchResult);
        Assert.Single(batchResult.Results);
        Assert.Equal(1, batchResult.JobsCreated);
    }

    [Fact]
    public async Task LargeMarkdown_CreatesArtifactAndFragments()
    {
        var notebookId = await CreateNotebook("fragmentation-test");

        // Create markdown content >16K chars with multiple heading sections
        var section1 = "# Section One\n\n" + new string('A', 9000) + "\n\n";
        var section2 = "# Section Two\n\n" + new string('B', 9000) + "\n\n";
        var largeMarkdown = section1 + section2;

        var batchResponse = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = largeMarkdown, content_type = "text/markdown", topic = "test/large" }
            }
        });

        Assert.Equal(HttpStatusCode.Created, batchResponse.StatusCode);

        var batchResult = await batchResponse.Content.ReadFromJsonAsync<BatchWriteResponse>();
        Assert.NotNull(batchResult);
        Assert.Single(batchResult.Results); // Response is 1:1 with input

        var artifactId = batchResult.Results[0].EntryId;
        Assert.NotEqual(Guid.Empty, artifactId);

        // Only 1 DISTILL_CLAIMS job should be created (for fragment 0)
        Assert.Equal(1, batchResult.JobsCreated);

        // Browse for fragments of this artifact
        var fragmentsResponse = await _client.GetAsync(
            $"/notebooks/{notebookId}/browse?fragment_of={artifactId}");
        Assert.Equal(HttpStatusCode.OK, fragmentsResponse.StatusCode);

        var fragmentsBody = await fragmentsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var fragments = fragmentsBody.GetProperty("entries");
        Assert.True(fragments.GetArrayLength() >= 2,
            $"Expected at least 2 fragments, got {fragments.GetArrayLength()}");
    }

    [Fact]
    public async Task LargeHtml_NormalizedThenFragmented()
    {
        var notebookId = await CreateNotebook("html-fragment-test");

        // Create large HTML with multiple sections
        var html = "<html><body>"
            + "<h1>Part One</h1><p>" + new string('X', 9000) + "</p>"
            + "<h1>Part Two</h1><p>" + new string('Y', 9000) + "</p>"
            + "</body></html>";

        var batchResponse = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = html, content_type = "text/html", topic = "test/html-large" }
            }
        });

        Assert.Equal(HttpStatusCode.Created, batchResponse.StatusCode);

        var batchResult = await batchResponse.Content.ReadFromJsonAsync<BatchWriteResponse>();
        Assert.NotNull(batchResult);
        Assert.Single(batchResult.Results);

        var artifactId = batchResult.Results[0].EntryId;

        // Should have fragments
        var fragmentsResponse = await _client.GetAsync(
            $"/notebooks/{notebookId}/browse?fragment_of={artifactId}");
        var fragmentsBody = await fragmentsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var fragments = fragmentsBody.GetProperty("entries");
        Assert.True(fragments.GetArrayLength() >= 2,
            $"Expected at least 2 fragments from large HTML, got {fragments.GetArrayLength()}");

        // Only 1 job for fragment 0
        Assert.Equal(1, batchResult.JobsCreated);
    }

    [Fact]
    public async Task SmallContent_NotFragmented()
    {
        var notebookId = await CreateNotebook("no-fragment-test");

        var batchResponse = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "# Short\n\nJust a short note.", content_type = "text/markdown", topic = "test/small" }
            }
        });

        Assert.Equal(HttpStatusCode.Created, batchResponse.StatusCode);

        var batchResult = await batchResponse.Content.ReadFromJsonAsync<BatchWriteResponse>();
        Assert.NotNull(batchResult);

        var entryId = batchResult.Results[0].EntryId;

        // Should NOT have fragments
        var fragmentsResponse = await _client.GetAsync(
            $"/notebooks/{notebookId}/browse?fragment_of={entryId}");
        var fragmentsBody = await fragmentsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var fragments = fragmentsBody.GetProperty("entries");
        Assert.Equal(0, fragments.GetArrayLength());

        // 1 job directly for the entry
        Assert.Equal(1, batchResult.JobsCreated);
    }

    [Fact]
    public async Task FragmentChaining_SimulateDistillation()
    {
        var notebookId = await CreateNotebook("chaining-test");

        // Write large content to trigger fragmentation
        var section1 = "# Alpha\n\n" + new string('A', 9000) + "\n\n";
        var section2 = "# Beta\n\n" + new string('B', 9000) + "\n\n";
        var largeMarkdown = section1 + section2;

        var batchResponse = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = largeMarkdown, content_type = "text/markdown", topic = "test/chain" }
            }
        });
        batchResponse.EnsureSuccessStatusCode();
        var batchResult = (await batchResponse.Content.ReadFromJsonAsync<BatchWriteResponse>())!;
        var artifactId = batchResult.Results[0].EntryId;

        // 1. Claim the first DISTILL_CLAIMS job (fragment 0)
        var nextJobResponse = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
        Assert.Equal(HttpStatusCode.OK, nextJobResponse.StatusCode);

        var job = await nextJobResponse.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(job);
        Assert.Equal("DISTILL_CLAIMS", job.JobType);

        // The payload should contain fragment 0's content
        var payloadEntryId = job.Payload.GetProperty("entry_id").GetString();
        Assert.NotNull(payloadEntryId);

        // context_claims should be null (first fragment has no context)
        var contextClaims = job.Payload.GetProperty("context_claims");
        Assert.Equal(JsonValueKind.Null, contextClaims.ValueKind);

        // 2. Complete fragment 0's job with mock claims
        var mockClaims = new[]
        {
            new { text = "Alpha section discusses topic A", confidence = 0.9 },
            new { text = "Alpha contains repeated character A", confidence = 0.85 }
        };

        var completeResponse = await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{job.Id}/complete",
            new
            {
                worker_id = "test-worker",
                result = new { claims = mockClaims }
            });
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var completeBody = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var followUpJobs = completeBody.GetProperty("follow_up_jobs").GetInt32();
        Assert.Equal(1, followUpJobs); // Should chain to next fragment

        // 3. Claim the next job — should be DISTILL_CLAIMS for fragment 1 with context_claims
        var nextJobResponse2 = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
        Assert.Equal(HttpStatusCode.OK, nextJobResponse2.StatusCode);

        var job2 = await nextJobResponse2.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(job2);
        Assert.Equal("DISTILL_CLAIMS", job2.JobType);

        // context_claims should now contain fragment 0's claims
        var contextClaims2 = job2.Payload.GetProperty("context_claims");
        Assert.NotEqual(JsonValueKind.Null, contextClaims2.ValueKind);
        Assert.True(contextClaims2.GetArrayLength() >= 2,
            $"Expected context_claims from fragment 0, got {contextClaims2.GetArrayLength()} claims");

        // 4. Complete fragment 1's job
        var mockClaims2 = new[]
        {
            new { text = "Beta section discusses topic B", confidence = 0.88 }
        };

        var completeResponse2 = await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{job2.Id}/complete",
            new
            {
                worker_id = "test-worker",
                result = new { claims = mockClaims2 }
            });
        Assert.Equal(HttpStatusCode.OK, completeResponse2.StatusCode);

        var completeBody2 = await completeResponse2.Content.ReadFromJsonAsync<JsonElement>();
        var followUpJobs2 = completeBody2.GetProperty("follow_up_jobs").GetInt32();
        Assert.Equal(1, followUpJobs2); // Should chain to artifact distillation

        // 5. Claim the artifact distillation job
        var nextJobResponse3 = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=test-worker&type=DISTILL_CLAIMS");
        Assert.Equal(HttpStatusCode.OK, nextJobResponse3.StatusCode);

        var job3 = await nextJobResponse3.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(job3);
        Assert.Equal("DISTILL_CLAIMS", job3.JobType);

        // The entry_id should be the artifact entry
        var artifactJobEntryId = job3.Payload.GetProperty("entry_id").GetString();
        Assert.Equal(artifactId.ToString(), artifactJobEntryId);

        // context_claims should contain ALL fragment claims (from both fragments)
        var artifactContext = job3.Payload.GetProperty("context_claims");
        Assert.NotEqual(JsonValueKind.Null, artifactContext.ValueKind);
        Assert.True(artifactContext.GetArrayLength() >= 3,
            $"Expected all fragment claims as context, got {artifactContext.GetArrayLength()}");

        // 6. No more DISTILL_CLAIMS jobs should be pending
        var mockArtifactClaims = new[]
        {
            new { text = "Document covers topics A and B", confidence = 0.92 }
        };

        var completeResponse3 = await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/jobs/{job3.Id}/complete",
            new
            {
                worker_id = "test-worker",
                result = new { claims = mockArtifactClaims }
            });
        completeResponse3.EnsureSuccessStatusCode();

        // Verify job stats
        var statsResponse = await _client.GetAsync($"/notebooks/{notebookId}/jobs/stats");
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);

        var stats = await statsResponse.Content.ReadFromJsonAsync<JobStatsResponse>();
        Assert.NotNull(stats);
        Assert.Equal(0, stats.DistillClaims.Pending);
        Assert.Equal(3, stats.DistillClaims.Completed); // fragment0, fragment1, artifact
    }
}
