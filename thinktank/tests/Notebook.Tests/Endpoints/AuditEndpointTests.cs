using System.Net;
using System.Net.Http.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class AuditEndpointTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    private const string ActorA = "1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a";
    private const string ActorB = "2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b";

    public AuditEndpointTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    private HttpRequestMessage WithAuthor(HttpMethod method, string url, string authorHex, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("X-Author-Id", authorHex);
        return request;
    }

    /// <summary>
    /// Polls the audit endpoint until at least <paramref name="minCount"/> entries matching
    /// the given filters appear, or the timeout expires. This accounts for the async
    /// channel-based audit writer.
    /// </summary>
    private async Task<AuditListResponse> WaitForAuditEntriesAsync(
        string queryString, int minCount, string actor = ActorA, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        AuditListResponse? last = null;

        while (DateTime.UtcNow < deadline)
        {
            var req = WithAuthor(HttpMethod.Get, $"/audit?{queryString}", actor);
            var resp = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            last = await resp.Content.ReadFromJsonAsync<AuditListResponse>();
            if (last is not null && last.Count >= minCount)
                return last;

            await Task.Delay(100);
        }

        // Return whatever we got — the caller's assertions will show what's missing
        return last ?? new AuditListResponse { Entries = [], Count = 0 };
    }

    [Fact]
    public async Task NotebookCreate_GeneratesAuditEvent()
    {
        var createReq = WithAuthor(HttpMethod.Post, "/notebooks", ActorA,
            JsonContent.Create(new { name = "audit-create-test" }));
        var createResp = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var created = await createResp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(created);

        var audit = await WaitForAuditEntriesAsync(
            $"action=notebook.create&resource=notebook:{created.Id}", minCount: 1);

        Assert.True(audit.Count >= 1, "Expected at least 1 notebook.create audit entry");
        var entry = audit.Entries.First(e => e.Resource == $"notebook:{created.Id}");
        Assert.Equal("notebook.create", entry.Action);
        Assert.Equal(ActorA.ToLower(), entry.Actor);
        Assert.Contains("audit-create-test", entry.Detail ?? "");
    }

    [Fact]
    public async Task NotebookDelete_GeneratesAuditEvent()
    {
        // Create
        var createReq = WithAuthor(HttpMethod.Post, "/notebooks", ActorA,
            JsonContent.Create(new { name = "audit-delete-test" }));
        var createResp = await _client.SendAsync(createReq);
        var created = await createResp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(created);

        // Delete
        var deleteReq = WithAuthor(HttpMethod.Delete, $"/notebooks/{created.Id}", ActorA);
        var deleteResp = await _client.SendAsync(deleteReq);
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);

        var audit = await WaitForAuditEntriesAsync(
            $"action=notebook.delete&resource=notebook:{created.Id}", minCount: 1);

        Assert.True(audit.Count >= 1, "Expected at least 1 notebook.delete audit entry");
        var entry = audit.Entries.First();
        Assert.Equal("notebook.delete", entry.Action);
        Assert.Equal(ActorA.ToLower(), entry.Actor);
    }

    [Fact]
    public async Task AccessGrant_GeneratesAuditEvent()
    {
        // Create notebook
        var createReq = WithAuthor(HttpMethod.Post, "/notebooks", ActorA,
            JsonContent.Create(new { name = "audit-grant-test" }));
        var createResp = await _client.SendAsync(createReq);
        var created = await createResp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(created);

        // Grant access to ActorB
        var shareReq = WithAuthor(HttpMethod.Post, $"/notebooks/{created.Id}/share/", ActorA,
            JsonContent.Create(new { author_id = ActorB, read = true, write = false }));
        var shareResp = await _client.SendAsync(shareReq);
        Assert.Equal(HttpStatusCode.OK, shareResp.StatusCode);

        var audit = await WaitForAuditEntriesAsync(
            $"action=access.grant&resource=notebook:{created.Id}", minCount: 1);

        Assert.True(audit.Count >= 1, "Expected at least 1 access.grant audit entry");
        var entry = audit.Entries.First();
        Assert.Equal("access.grant", entry.Action);
        Assert.Equal(ActorA.ToLower(), entry.Actor);
        Assert.Contains(ActorB.ToLower(), entry.Detail ?? "");
    }

    [Fact]
    public async Task AccessRevoke_GeneratesAuditEvent()
    {
        // Create notebook
        var createReq = WithAuthor(HttpMethod.Post, "/notebooks", ActorA,
            JsonContent.Create(new { name = "audit-revoke-test" }));
        var createResp = await _client.SendAsync(createReq);
        var created = await createResp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(created);

        // Grant then revoke
        var shareReq = WithAuthor(HttpMethod.Post, $"/notebooks/{created.Id}/share/", ActorA,
            JsonContent.Create(new { author_id = ActorB, read = true, write = true }));
        await _client.SendAsync(shareReq);

        var revokeReq = WithAuthor(HttpMethod.Delete, $"/notebooks/{created.Id}/share/{ActorB}", ActorA);
        var revokeResp = await _client.SendAsync(revokeReq);
        Assert.Equal(HttpStatusCode.OK, revokeResp.StatusCode);

        var audit = await WaitForAuditEntriesAsync(
            $"action=access.revoke&resource=notebook:{created.Id}", minCount: 1);

        Assert.True(audit.Count >= 1, "Expected at least 1 access.revoke audit entry");
        var entry = audit.Entries.First();
        Assert.Equal("access.revoke", entry.Action);
        Assert.Contains(ActorB.ToLower(), entry.Detail ?? "");
    }

    [Fact]
    public async Task AccessDenied_GeneratesAuditEvent()
    {
        // ActorA creates a notebook
        var createReq = WithAuthor(HttpMethod.Post, "/notebooks", ActorA,
            JsonContent.Create(new { name = "audit-denied-test" }));
        var createResp = await _client.SendAsync(createReq);
        var created = await createResp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(created);

        // ActorB tries to browse — gets 404 (denied)
        var browseReq = WithAuthor(HttpMethod.Get, $"/notebooks/{created.Id}/browse", ActorB);
        var browseResp = await _client.SendAsync(browseReq);
        Assert.Equal(HttpStatusCode.NotFound, browseResp.StatusCode);

        var audit = await WaitForAuditEntriesAsync(
            $"action=access.denied&resource=notebook:{created.Id}", minCount: 1);

        Assert.True(audit.Count >= 1, "Expected at least 1 access.denied audit entry");
        var entry = audit.Entries.First();
        Assert.Equal("access.denied", entry.Action);
        Assert.Equal(ActorB.ToLower(), entry.Actor);
        Assert.Contains("no_acl", entry.Detail ?? "");
    }

    [Fact]
    public async Task FilterByActor_ReturnsOnlyThatActorsEvents()
    {
        // Both actors create notebooks
        var reqA = WithAuthor(HttpMethod.Post, "/notebooks", ActorA,
            JsonContent.Create(new { name = "actor-filter-a" }));
        await _client.SendAsync(reqA);

        var reqB = WithAuthor(HttpMethod.Post, "/notebooks", ActorB,
            JsonContent.Create(new { name = "actor-filter-b" }));
        await _client.SendAsync(reqB);

        // Wait for both to flush
        await WaitForAuditEntriesAsync($"action=notebook.create&actor={ActorA}", minCount: 1);
        await WaitForAuditEntriesAsync($"action=notebook.create&actor={ActorB}", minCount: 1, actor: ActorB);

        // Query filtered by ActorA
        var auditReq = WithAuthor(HttpMethod.Get, $"/audit?actor={ActorA}", ActorA);
        var auditResp = await _client.SendAsync(auditReq);
        Assert.Equal(HttpStatusCode.OK, auditResp.StatusCode);

        var audit = await auditResp.Content.ReadFromJsonAsync<AuditListResponse>();
        Assert.NotNull(audit);
        Assert.All(audit.Entries, e => Assert.Equal(ActorA.ToLower(), e.Actor));
    }

    [Fact]
    public async Task Pagination_LimitAndOffset_Work()
    {
        // Create several notebooks to generate multiple audit events
        for (var i = 0; i < 3; i++)
        {
            var req = WithAuthor(HttpMethod.Post, "/notebooks", ActorA,
                JsonContent.Create(new { name = $"pagination-test-{i}" }));
            await _client.SendAsync(req);
        }

        // Wait for at least 3 create events from this actor
        await WaitForAuditEntriesAsync($"action=notebook.create&actor={ActorA}", minCount: 3);

        // Fetch with limit=1
        var page1Req = WithAuthor(HttpMethod.Get, $"/audit?action=notebook.create&actor={ActorA}&limit=1", ActorA);
        var page1Resp = await _client.SendAsync(page1Req);
        var page1 = await page1Resp.Content.ReadFromJsonAsync<AuditListResponse>();
        Assert.NotNull(page1);
        Assert.Equal(1, page1.Count);

        // Fetch with limit=1&offset=1 — should be a different entry
        var page2Req = WithAuthor(HttpMethod.Get, $"/audit?action=notebook.create&actor={ActorA}&limit=1&offset=1", ActorA);
        var page2Resp = await _client.SendAsync(page2Req);
        var page2 = await page2Resp.Content.ReadFromJsonAsync<AuditListResponse>();
        Assert.NotNull(page2);
        Assert.Equal(1, page2.Count);
        Assert.NotEqual(page1.Entries[0].Id, page2.Entries[0].Id);
    }

    [Fact]
    public async Task AuditEntries_AreOrderedByCreatedDescending()
    {
        // Create two notebooks in sequence
        var req1 = WithAuthor(HttpMethod.Post, "/notebooks", ActorA,
            JsonContent.Create(new { name = "order-test-first" }));
        await _client.SendAsync(req1);

        await Task.Delay(50); // ensure distinct timestamps

        var req2 = WithAuthor(HttpMethod.Post, "/notebooks", ActorA,
            JsonContent.Create(new { name = "order-test-second" }));
        await _client.SendAsync(req2);

        var audit = await WaitForAuditEntriesAsync($"action=notebook.create&actor={ActorA}", minCount: 2);

        // Most recent should be first (descending order)
        Assert.True(audit.Count >= 2);
        Assert.True(audit.Entries[0].Created >= audit.Entries[1].Created,
            "Audit entries should be ordered by created descending");
    }

    [Fact]
    public async Task FullShareWorkflow_GeneratesCompleteAuditTrail()
    {
        // Create notebook
        var createReq = WithAuthor(HttpMethod.Post, "/notebooks", ActorA,
            JsonContent.Create(new { name = "audit-workflow-test" }));
        var createResp = await _client.SendAsync(createReq);
        var created = await createResp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(created);

        var resource = $"notebook:{created.Id}";

        // Grant access
        var shareReq = WithAuthor(HttpMethod.Post, $"/notebooks/{created.Id}/share/", ActorA,
            JsonContent.Create(new { author_id = ActorB, read = true, write = true }));
        await _client.SendAsync(shareReq);

        // ActorB browses (succeeds — no denied event)
        var browseReq = WithAuthor(HttpMethod.Get, $"/notebooks/{created.Id}/browse", ActorB);
        var browseResp = await _client.SendAsync(browseReq);
        Assert.Equal(HttpStatusCode.OK, browseResp.StatusCode);

        // Revoke access
        var revokeReq = WithAuthor(HttpMethod.Delete, $"/notebooks/{created.Id}/share/{ActorB}", ActorA);
        await _client.SendAsync(revokeReq);

        // ActorB tries again — denied
        var browseReq2 = WithAuthor(HttpMethod.Get, $"/notebooks/{created.Id}/browse", ActorB);
        var browseResp2 = await _client.SendAsync(browseReq2);
        Assert.Equal(HttpStatusCode.NotFound, browseResp2.StatusCode);

        // Wait for the full trail to flush
        await WaitForAuditEntriesAsync($"resource={resource}", minCount: 4);

        // Query all events for this notebook
        var auditReq = WithAuthor(HttpMethod.Get, $"/audit?resource={resource}", ActorA);
        var auditResp = await _client.SendAsync(auditReq);
        var audit = await auditResp.Content.ReadFromJsonAsync<AuditListResponse>();
        Assert.NotNull(audit);

        var actions = audit.Entries.Select(e => e.Action).ToList();
        Assert.Contains("notebook.create", actions);
        Assert.Contains("access.grant", actions);
        Assert.Contains("access.revoke", actions);
        Assert.Contains("access.denied", actions);
    }
}
