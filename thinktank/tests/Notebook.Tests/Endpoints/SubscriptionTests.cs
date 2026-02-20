using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class SubscriptionTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    public SubscriptionTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task CreateSubscription_ReturnsCreated()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("CONFIDENTIAL", ["ALPHA"]);
        var sourceId = await CreateClassifiedNotebookAsync("INTERNAL", []);

        var response = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = sourceId, scope = "claims", discount_factor = 0.3 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubscriptionResponse>();
        Assert.NotNull(body);
        Assert.Equal(sourceId, body.SourceId);
        Assert.Equal(subscriberId, body.SubscriberId);
        Assert.Equal("claims", body.Scope);
        Assert.Equal("idle", body.SyncStatus);
        Assert.Equal(0.3, body.DiscountFactor);
    }

    [Fact]
    public async Task CreateSubscription_SameClassification_Succeeds()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("SECRET", []);
        var sourceId = await CreateClassifiedNotebookAsync("SECRET", []);

        var response = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = sourceId, scope = "catalog" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateSubscription_SubscriberLowerThanSource_Rejected()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("INTERNAL", []);
        var sourceId = await CreateClassifiedNotebookAsync("SECRET", []);

        var response = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = sourceId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("classification", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CreateSubscription_MissingCompartments_Rejected()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("SECRET", ["ALPHA"]);
        var sourceId = await CreateClassifiedNotebookAsync("INTERNAL", ["ALPHA", "BRAVO"]);

        var response = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = sourceId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("compartments", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CreateSubscription_SelfSubscription_Rejected()
    {
        var notebookId = await CreateClassifiedNotebookAsync("INTERNAL", []);

        var response = await _client.PostAsJsonAsync(
            $"/notebooks/{notebookId}/subscriptions",
            new { source_id = notebookId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("self", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CreateSubscription_Duplicate_ReturnsConflict()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("SECRET", []);
        var sourceId = await CreateClassifiedNotebookAsync("INTERNAL", []);

        var first = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = sourceId });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = sourceId });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CreateSubscription_CycleDetection_Rejected()
    {
        // A -> B already exists, now try B -> A (which would create a cycle)
        var a = await CreateClassifiedNotebookAsync("SECRET", []);
        var b = await CreateClassifiedNotebookAsync("SECRET", []);

        var firstSub = await _client.PostAsJsonAsync(
            $"/notebooks/{a}/subscriptions",
            new { source_id = b });
        Assert.Equal(HttpStatusCode.Created, firstSub.StatusCode);

        var secondSub = await _client.PostAsJsonAsync(
            $"/notebooks/{b}/subscriptions",
            new { source_id = a });
        Assert.Equal(HttpStatusCode.BadRequest, secondSub.StatusCode);
        var body = await secondSub.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("cycle", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ListSubscriptions_ReturnsAll()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("TOP_SECRET", []);
        var source1 = await CreateClassifiedNotebookAsync("INTERNAL", []);
        var source2 = await CreateClassifiedNotebookAsync("CONFIDENTIAL", []);

        await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = source1 });
        await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = source2, scope = "entries" });

        var response = await _client.GetAsync($"/notebooks/{subscriberId}/subscriptions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListSubscriptionsResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Subscriptions.Count);
    }

    [Fact]
    public async Task GetSubscription_ReturnsDetail()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("SECRET", []);
        var sourceId = await CreateClassifiedNotebookAsync("INTERNAL", []);

        var createResponse = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = sourceId, scope = "entries", discount_factor = 0.5, poll_interval_s = 30 });
        var created = await createResponse.Content.ReadFromJsonAsync<SubscriptionResponse>();
        Assert.NotNull(created);

        var response = await _client.GetAsync(
            $"/notebooks/{subscriberId}/subscriptions/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<SubscriptionResponse>();
        Assert.NotNull(body);
        Assert.Equal("entries", body.Scope);
        Assert.Equal(0.5, body.DiscountFactor);
        Assert.Equal(30, body.PollIntervalSeconds);
    }

    [Fact]
    public async Task DeleteSubscription_Succeeds()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("SECRET", []);
        var sourceId = await CreateClassifiedNotebookAsync("INTERNAL", []);

        var createResponse = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = sourceId });
        var created = await createResponse.Content.ReadFromJsonAsync<SubscriptionResponse>();
        Assert.NotNull(created);

        var deleteResponse = await _client.DeleteAsync(
            $"/notebooks/{subscriberId}/subscriptions/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Verify it's gone
        var getResponse = await _client.GetAsync(
            $"/notebooks/{subscriberId}/subscriptions/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteSubscription_NotFound_Returns404()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("SECRET", []);
        var fakeSubId = Guid.NewGuid();

        var response = await _client.DeleteAsync(
            $"/notebooks/{subscriberId}/subscriptions/{fakeSubId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TriggerSync_Succeeds()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("SECRET", []);
        var sourceId = await CreateClassifiedNotebookAsync("INTERNAL", []);

        var createResponse = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = sourceId });
        var created = await createResponse.Content.ReadFromJsonAsync<SubscriptionResponse>();
        Assert.NotNull(created);

        var syncResponse = await _client.PostAsync(
            $"/notebooks/{subscriberId}/subscriptions/{created.Id}/sync", null);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
    }

    [Fact]
    public async Task CreateSubscription_InvalidScope_Rejected()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("SECRET", []);
        var sourceId = await CreateClassifiedNotebookAsync("INTERNAL", []);

        var response = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = sourceId, scope = "invalid" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSubscription_InvalidDiscountFactor_Rejected()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("SECRET", []);
        var sourceId = await CreateClassifiedNotebookAsync("INTERNAL", []);

        var response = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = sourceId, discount_factor = 0.0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSubscription_SourceNotFound_Returns404()
    {
        var subscriberId = await CreateClassifiedNotebookAsync("SECRET", []);
        var fakeSourceId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync(
            $"/notebooks/{subscriberId}/subscriptions",
            new { source_id = fakeSourceId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Helpers ---

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
}
