using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class AgentTrustTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    public AgentTrustTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task RegisterAndListAgents()
    {
        var orgId = await CreateOrgAsync();
        var agentId = $"agent-{Guid.NewGuid():N}";

        // Register
        var registerResponse = await _client.PostAsJsonAsync("/agents", new
        {
            id = agentId,
            organization_id = orgId,
            max_level = "SECRET",
            compartments = new[] { "ALPHA" },
            infrastructure = "gpu-cluster-1",
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var agent = await registerResponse.Content.ReadFromJsonAsync<AgentResponse>();
        Assert.NotNull(agent);
        Assert.Equal(agentId, agent.Id);
        Assert.Equal("SECRET", agent.MaxLevel);
        Assert.Contains("ALPHA", agent.Compartments);
        Assert.Equal("gpu-cluster-1", agent.Infrastructure);

        // List
        var listResponse = await _client.GetAsync("/agents");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<ListAgentsResponse>();
        Assert.NotNull(list);
        Assert.Contains(list.Agents, a => a.Id == agentId);
    }

    [Fact]
    public async Task GetAndUpdateAgent()
    {
        var orgId = await CreateOrgAsync();
        var agentId = $"agent-{Guid.NewGuid():N}";

        await _client.PostAsJsonAsync("/agents", new
        {
            id = agentId,
            organization_id = orgId,
            max_level = "INTERNAL",
        });

        // Get
        var getResponse = await _client.GetAsync($"/agents/{agentId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var agent = await getResponse.Content.ReadFromJsonAsync<AgentResponse>();
        Assert.NotNull(agent);
        Assert.Equal("INTERNAL", agent.MaxLevel);

        // Update
        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/agents/{agentId}")
        {
            Content = JsonContent.Create(new
            {
                max_level = "TOP_SECRET",
                compartments = new[] { "ALPHA", "BRAVO" },
                infrastructure = "upgraded-cluster",
            }),
        };
        var updateResponse = await _client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<AgentResponse>();
        Assert.NotNull(updated);
        Assert.Equal("TOP_SECRET", updated.MaxLevel);
        Assert.Contains("BRAVO", updated.Compartments);
    }

    [Fact]
    public async Task DeleteAgent()
    {
        var orgId = await CreateOrgAsync();
        var agentId = $"agent-{Guid.NewGuid():N}";

        await _client.PostAsJsonAsync("/agents", new
        {
            id = agentId,
            organization_id = orgId,
        });

        var deleteResponse = await _client.DeleteAsync($"/agents/{agentId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Verify gone
        var getResponse = await _client.GetAsync($"/agents/{agentId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DuplicateRegistrationReturnsConflict()
    {
        var orgId = await CreateOrgAsync();
        var agentId = $"agent-{Guid.NewGuid():N}";

        var first = await _client.PostAsJsonAsync("/agents", new
        {
            id = agentId,
            organization_id = orgId,
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/agents", new
        {
            id = agentId,
            organization_id = orgId,
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task AgentWithSufficientClearanceClaimsJob()
    {
        var orgId = await CreateOrgAsync();
        var notebookId = await CreateClassifiedNotebookAsync("CONFIDENTIAL", ["ALPHA"]);
        var agentId = $"agent-{Guid.NewGuid():N}";

        // Register agent with SECRET clearance + ALPHA compartment (dominates CONFIDENTIAL+ALPHA)
        await _client.PostAsJsonAsync("/agents", new
        {
            id = agentId,
            organization_id = orgId,
            max_level = "SECRET",
            compartments = new[] { "ALPHA" },
        });

        // Insert a job into the notebook
        await InsertJobAsync(notebookId);

        // Claim job as the agent
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/notebooks/{notebookId}/jobs/next?worker_id=worker-1&type=DISTILL_CLAIMS");
        request.Headers.Add("X-Agent-Id", agentId);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Should have a claimed job (has "id" field)
        Assert.True(body.TryGetProperty("id", out _), "Expected a job to be returned");
    }

    [Fact]
    public async Task AgentWithInsufficientClearanceGetsNoJob()
    {
        var orgId = await CreateOrgAsync();
        var notebookId = await CreateClassifiedNotebookAsync("SECRET", ["ALPHA"]);
        var agentId = $"agent-{Guid.NewGuid():N}";

        // Register agent with only INTERNAL clearance (insufficient for SECRET)
        await _client.PostAsJsonAsync("/agents", new
        {
            id = agentId,
            organization_id = orgId,
            max_level = "INTERNAL",
            compartments = Array.Empty<string>(),
        });

        // Insert a job into the notebook
        await InsertJobAsync(notebookId);

        // Try to claim job as the agent — should get empty response (no job)
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/notebooks/{notebookId}/jobs/next?worker_id=worker-1&type=DISTILL_CLAIMS");
        request.Headers.Add("X-Agent-Id", agentId);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Should NOT have a job (only queue_depth)
        Assert.False(body.TryGetProperty("id", out _), "Expected no job to be returned");
        Assert.True(body.TryGetProperty("queue_depth", out _));
    }

    [Fact]
    public async Task AgentMissingCompartmentGetsNoJob()
    {
        var orgId = await CreateOrgAsync();
        var notebookId = await CreateClassifiedNotebookAsync("CONFIDENTIAL", ["ALPHA", "BRAVO"]);
        var agentId = $"agent-{Guid.NewGuid():N}";

        // Register agent with SECRET level but only ALPHA compartment (missing BRAVO)
        await _client.PostAsJsonAsync("/agents", new
        {
            id = agentId,
            organization_id = orgId,
            max_level = "SECRET",
            compartments = new[] { "ALPHA" },
        });

        await InsertJobAsync(notebookId);

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/notebooks/{notebookId}/jobs/next?worker_id=worker-1&type=DISTILL_CLAIMS");
        request.Headers.Add("X-Agent-Id", agentId);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.TryGetProperty("id", out _), "Expected no job due to missing compartment");
    }

    [Fact]
    public async Task LegacyCallerWithoutAgentIdStillClaimsJobs()
    {
        var notebookId = await CreateClassifiedNotebookAsync("SECRET", ["ALPHA"]);

        await InsertJobAsync(notebookId);

        // No X-Agent-Id header — legacy caller should bypass clearance check
        var response = await _client.GetAsync(
            $"/notebooks/{notebookId}/jobs/next?worker_id=legacy-worker&type=DISTILL_CLAIMS");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out _), "Legacy caller should still get jobs");
    }

    [Fact]
    public async Task AgentLastSeenUpdatedOnJobClaim()
    {
        var orgId = await CreateOrgAsync();
        var notebookId = await CreateClassifiedNotebookAsync("INTERNAL", []);
        var agentId = $"agent-{Guid.NewGuid():N}";

        await _client.PostAsJsonAsync("/agents", new
        {
            id = agentId,
            organization_id = orgId,
            max_level = "SECRET",
        });

        // Verify last_seen is null initially
        var getResponse = await _client.GetAsync($"/agents/{agentId}");
        var agent = await getResponse.Content.ReadFromJsonAsync<AgentResponse>();
        Assert.NotNull(agent);
        Assert.Null(agent.LastSeen);

        // Insert and claim a job
        await InsertJobAsync(notebookId);
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/notebooks/{notebookId}/jobs/next?worker_id=worker-1");
        request.Headers.Add("X-Agent-Id", agentId);
        var claimResponse = await _client.SendAsync(request);
        claimResponse.EnsureSuccessStatusCode();

        var body = await claimResponse.Content.ReadFromJsonAsync<JsonElement>();
        if (body.TryGetProperty("id", out _))
        {
            // Job was claimed, last_seen should be updated
            var agentAfter = await _client.GetAsync($"/agents/{agentId}");
            var updated = await agentAfter.Content.ReadFromJsonAsync<AgentResponse>();
            Assert.NotNull(updated);
            Assert.NotNull(updated.LastSeen);
        }
    }

    // --- Helpers ---

    private async Task<Guid> CreateOrgAsync()
    {
        var response = await _client.PostAsJsonAsync("/organizations",
            new { name = $"agent-org-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        var org = await response.Content.ReadFromJsonAsync<OrganizationResponse>();
        return org!.Id;
    }

    private async Task<Guid> CreateClassifiedNotebookAsync(string classification, string[] compartments)
    {
        var response = await _client.PostAsJsonAsync("/notebooks", new
        {
            name = $"nb-{Guid.NewGuid():N}",
            classification,
            compartments,
        });
        response.EnsureSuccessStatusCode();
        var nb = await response.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        return nb!.Id;
    }

    private async Task InsertJobAsync(Guid notebookId)
    {
        // Write an entry via batch to trigger a DISTILL_CLAIMS job
        var response = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "Test content for job generation", content_type = "text/plain" },
            },
        });
        response.EnsureSuccessStatusCode();
    }
}
