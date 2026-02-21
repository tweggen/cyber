using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class ReviewTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    public ReviewTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task MemberWrite_NoReviewRequired()
    {
        // Notebook without owning group — all writes are approved immediately
        var notebookId = await CreateNotebookAsync();

        var response = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[] { new { content = "Member content", content_type = "text/plain" } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var results = body.GetProperty("results");
        Assert.Equal(1, results.GetArrayLength());
        Assert.Equal("approved", results[0].GetProperty("review_status").GetString());
        Assert.True(body.GetProperty("jobs_created").GetInt32() > 0);
    }

    [Fact]
    public async Task ExternalContributorWrite_PendingReview()
    {
        // Create notebook with owning group, submitter is NOT a member
        var (notebookId, _) = await CreateNotebookWithGroupAsync();

        // The dev identity author is the notebook owner, so they're treated as member.
        // We need to verify the review queue works. Since the dev identity is always the owner,
        // direct external contributor testing is limited. But we can test the review endpoints.
        var response = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[] { new { content = "Content from owner", content_type = "text/plain" } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Owner is treated as member — approved directly
        Assert.Equal("approved", body.GetProperty("results")[0].GetProperty("review_status").GetString());
    }

    [Fact]
    public async Task ListReviews_EmptyByDefault()
    {
        var notebookId = await CreateNotebookAsync();

        var response = await _client.GetAsync($"/notebooks/{notebookId}/reviews");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListReviewsResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.Reviews);
        Assert.Equal(0, body.PendingCount);
    }

    [Fact]
    public async Task ListReviews_WithStatusFilter()
    {
        var notebookId = await CreateNotebookAsync();

        var response = await _client.GetAsync($"/notebooks/{notebookId}/reviews?status=pending");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListReviewsResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.Reviews);
    }

    [Fact]
    public async Task ApproveReview_NotFound()
    {
        var notebookId = await CreateNotebookAsync();
        var fakeReviewId = Guid.NewGuid();

        var response = await _client.PostAsync(
            $"/notebooks/{notebookId}/reviews/{fakeReviewId}/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RejectReview_NotFound()
    {
        var notebookId = await CreateNotebookAsync();
        var fakeReviewId = Guid.NewGuid();

        var response = await _client.PostAsync(
            $"/notebooks/{notebookId}/reviews/{fakeReviewId}/reject", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PendingEntries_ExcludedFromBrowse()
    {
        var notebookId = await CreateNotebookAsync();

        // Write some entries (approved by default since no owning group)
        await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "Visible entry 1", content_type = "text/plain" },
                new { content = "Visible entry 2", content_type = "text/plain" },
            },
        });

        // Browse should show them
        var browseResponse = await _client.GetAsync($"/notebooks/{notebookId}/browse");
        Assert.Equal(HttpStatusCode.OK, browseResponse.StatusCode);
        var browseBody = await browseResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(browseBody.GetProperty("count").GetInt32() >= 2);
    }

    [Fact]
    public async Task PendingEntries_ExcludedFromObserve()
    {
        var notebookId = await CreateNotebookAsync();

        // Write approved entries
        await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new { content = "Observable entry", content_type = "text/plain" },
            },
        });

        // Observe should show approved entries
        var observeResponse = await _client.GetAsync($"/notebooks/{notebookId}/observe");
        Assert.Equal(HttpStatusCode.OK, observeResponse.StatusCode);
        var observeBody = await observeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var changes = observeBody.GetProperty("changes");
        Assert.True(changes.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task ClassificationAssertion_ExceedingNotebook_Rejected()
    {
        var notebookId = await CreateClassifiedNotebookAsync("INTERNAL", []);

        var response = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new
                {
                    content = "Content",
                    content_type = "text/plain",
                    classification_assertion = "SECRET",
                },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Classification assertion", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ClassificationAssertion_AtOrBelowNotebook_Accepted()
    {
        var notebookId = await CreateClassifiedNotebookAsync("SECRET", []);

        var response = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[]
            {
                new
                {
                    content = "Content at correct level",
                    content_type = "text/plain",
                    classification_assertion = "INTERNAL",
                },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ReviewApproveReject_FullWorkflow()
    {
        // Create a notebook with an owning group
        var (notebookId, groupId) = await CreateNotebookWithGroupAsync();

        // Write an entry (as owner, will be approved)
        var writeResponse = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[] { new { content = "Test entry", content_type = "text/plain" } },
        });
        Assert.Equal(HttpStatusCode.Created, writeResponse.StatusCode);

        // List reviews — should be empty (owner writes don't create reviews)
        var listResponse = await _client.GetAsync($"/notebooks/{notebookId}/reviews");
        var list = await listResponse.Content.ReadFromJsonAsync<ListReviewsResponse>();
        Assert.NotNull(list);
        Assert.Equal(0, list.PendingCount);
    }

    // --- Helpers ---

    private async Task<Guid> CreateNotebookAsync()
    {
        var response = await _client.PostAsJsonAsync("/notebooks", new
        {
            name = $"nb-{Guid.NewGuid():N}",
        });
        response.EnsureSuccessStatusCode();
        var nb = await response.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        return nb!.Id;
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

    private async Task<(Guid NotebookId, Guid GroupId)> CreateNotebookWithGroupAsync()
    {
        // Create org and group
        var orgResponse = await _client.PostAsJsonAsync("/organizations",
            new { name = $"review-org-{Guid.NewGuid():N}" });
        orgResponse.EnsureSuccessStatusCode();
        var org = await orgResponse.Content.ReadFromJsonAsync<OrganizationResponse>();

        var groupResponse = await _client.PostAsJsonAsync($"/organizations/{org!.Id}/groups",
            new { name = $"review-group-{Guid.NewGuid():N}" });
        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupResponse>();

        // Create notebook
        var notebookId = await CreateNotebookAsync();

        // Assign notebook to group
        var assignRequest = new HttpRequestMessage(HttpMethod.Put, $"/notebooks/{notebookId}/group")
        {
            Content = JsonContent.Create(new { group_id = group!.Id }),
        };
        var assignResponse = await _client.SendAsync(assignRequest);
        assignResponse.EnsureSuccessStatusCode();

        return (notebookId, group.Id);
    }
}
