using System.Net;
using System.Net.Http.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class ClearanceTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;
    private const string DefaultOwner = "0000000000000000000000000000000000000000000000000000000000000000";
    private const string AuthorB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    public ClearanceTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task CreateNotebookWithClassification()
    {
        var response = await _client.PostAsJsonAsync("/notebooks", new
        {
            name = $"classified-{Guid.NewGuid():N}",
            classification = "SECRET",
            compartments = new[] { "ALPHA" },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(body);
        Assert.Equal("SECRET", body.Classification);
        Assert.Contains("ALPHA", body.Compartments);
    }

    [Fact]
    public async Task ListNotebooksIncludesClassification()
    {
        // Create a notebook with specific classification
        var createResponse = await _client.PostAsJsonAsync("/notebooks", new
        {
            name = $"classified-list-{Guid.NewGuid():N}",
            classification = "CONFIDENTIAL",
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateNotebookResponse>();

        var listResponse = await _client.GetAsync("/notebooks");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var list = await listResponse.Content.ReadFromJsonAsync<ListNotebooksResponse>();
        Assert.NotNull(list);
        var notebook = list.Notebooks.FirstOrDefault(n => n.Id == created!.Id);
        Assert.NotNull(notebook);
        Assert.Equal("CONFIDENTIAL", notebook.Classification);
    }

    [Fact]
    public async Task GrantAndRevokeClearance()
    {
        // Create org first
        var orgResponse = await _client.PostAsJsonAsync("/organizations", new { name = $"clearance-org-{Guid.NewGuid():N}" });
        orgResponse.EnsureSuccessStatusCode();
        var org = await orgResponse.Content.ReadFromJsonAsync<OrganizationResponse>();

        // Grant clearance
        var grantResponse = await _client.PostAsJsonAsync("/clearances", new
        {
            author_id = AuthorB,
            organization_id = org!.Id,
            max_level = "SECRET",
            compartments = new[] { "ALPHA" },
        });
        Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);

        var grant = await grantResponse.Content.ReadFromJsonAsync<GrantClearanceResponse>();
        Assert.NotNull(grant);
        Assert.Equal("SECRET", grant.MaxLevel);

        // List clearances
        var listResponse = await _client.GetAsync($"/organizations/{org.Id}/clearances");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<ListClearancesResponse>();
        Assert.NotNull(list);
        Assert.Single(list.Clearances);

        // Revoke clearance
        var revokeResponse = await _client.DeleteAsync($"/clearances/{AuthorB}/{org.Id}");
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        // Verify removed
        var listAfter = await _client.GetAsync($"/organizations/{org.Id}/clearances");
        var listBody = await listAfter.Content.ReadFromJsonAsync<ListClearancesResponse>();
        Assert.NotNull(listBody);
        Assert.Empty(listBody.Clearances);
    }

    [Fact]
    public async Task InsufficientClearanceDeniesAccess()
    {
        // Create org
        var orgResponse = await _client.PostAsJsonAsync("/organizations",
            new { name = $"sec-org-{Guid.NewGuid():N}" });
        orgResponse.EnsureSuccessStatusCode();
        var org = await orgResponse.Content.ReadFromJsonAsync<OrganizationResponse>();

        // Grant owner TOP_SECRET clearance (needed to operate on classified notebook after group assignment)
        var ownerClearance = await _client.PostAsJsonAsync("/clearances", new
        {
            author_id = DefaultOwner,
            organization_id = org!.Id,
            max_level = "TOP_SECRET",
            compartments = new[] { "ALPHA" },
        });
        ownerClearance.EnsureSuccessStatusCode();

        // Create group in org
        var groupResponse = await _client.PostAsJsonAsync($"/organizations/{org.Id}/groups",
            new { name = $"sec-group-{Guid.NewGuid():N}" });
        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupResponse>();

        // Create a SECRET notebook as owner
        var nbResponse = await _client.PostAsJsonAsync("/notebooks", new
        {
            name = $"secret-nb-{Guid.NewGuid():N}",
            classification = "SECRET",
            compartments = new[] { "ALPHA" },
        });
        nbResponse.EnsureSuccessStatusCode();
        var nb = await nbResponse.Content.ReadFromJsonAsync<CreateNotebookResponse>();

        // Assign notebook to the group
        var assignRequest = new HttpRequestMessage(HttpMethod.Put, $"/notebooks/{nb!.Id}/group");
        assignRequest.Content = JsonContent.Create(new { group_id = group!.Id });
        var assignResponse = await _client.SendAsync(assignRequest);
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        // Share notebook with Author B (read access)
        var shareResponse = await _client.PostAsJsonAsync($"/notebooks/{nb.Id}/share", new
        {
            author_id = AuthorB,
            permissions = new { read = true, write = false },
        });
        shareResponse.EnsureSuccessStatusCode();

        // Grant Author B only INTERNAL clearance (insufficient for SECRET)
        var clearanceResponse = await _client.PostAsJsonAsync("/clearances", new
        {
            author_id = AuthorB,
            organization_id = org.Id,
            max_level = "INTERNAL",
            compartments = Array.Empty<string>(),
        });
        clearanceResponse.EnsureSuccessStatusCode();

        // Author B tries to browse — should get 404 due to clearance failure
        var browseRequest = new HttpRequestMessage(HttpMethod.Get, $"/notebooks/{nb.Id}/browse");
        browseRequest.Headers.Add("X-Author-Id", AuthorB);
        var browseResponse = await _client.SendAsync(browseRequest);
        Assert.Equal(HttpStatusCode.NotFound, browseResponse.StatusCode);
    }

    [Fact]
    public async Task SufficientClearanceAllowsAccess()
    {
        // Create org
        var orgResponse = await _client.PostAsJsonAsync("/organizations",
            new { name = $"sec-org-ok-{Guid.NewGuid():N}" });
        orgResponse.EnsureSuccessStatusCode();
        var org = await orgResponse.Content.ReadFromJsonAsync<OrganizationResponse>();

        // Grant owner TOP_SECRET clearance (needed to operate on classified notebook after group assignment)
        var ownerClearance = await _client.PostAsJsonAsync("/clearances", new
        {
            author_id = DefaultOwner,
            organization_id = org!.Id,
            max_level = "TOP_SECRET",
            compartments = new[] { "ALPHA" },
        });
        ownerClearance.EnsureSuccessStatusCode();

        // Create group in org
        var groupResponse = await _client.PostAsJsonAsync($"/organizations/{org.Id}/groups",
            new { name = $"sec-group-ok-{Guid.NewGuid():N}" });
        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupResponse>();

        // Create a CONFIDENTIAL notebook as owner
        var nbResponse = await _client.PostAsJsonAsync("/notebooks", new
        {
            name = $"conf-nb-{Guid.NewGuid():N}",
            classification = "CONFIDENTIAL",
            compartments = new[] { "ALPHA" },
        });
        nbResponse.EnsureSuccessStatusCode();
        var nb = await nbResponse.Content.ReadFromJsonAsync<CreateNotebookResponse>();

        // Assign notebook to the group
        var assignRequest = new HttpRequestMessage(HttpMethod.Put, $"/notebooks/{nb!.Id}/group");
        assignRequest.Content = JsonContent.Create(new { group_id = group!.Id });
        var assignResponse = await _client.SendAsync(assignRequest);
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        // Share notebook with Author B (read access)
        var shareResponse = await _client.PostAsJsonAsync($"/notebooks/{nb.Id}/share", new
        {
            author_id = AuthorB,
            permissions = new { read = true, write = false },
        });
        shareResponse.EnsureSuccessStatusCode();

        // Grant Author B SECRET clearance with ALPHA compartment (dominates CONFIDENTIAL+ALPHA)
        var clearanceResponse = await _client.PostAsJsonAsync("/clearances", new
        {
            author_id = AuthorB,
            organization_id = org.Id,
            max_level = "SECRET",
            compartments = new[] { "ALPHA" },
        });
        clearanceResponse.EnsureSuccessStatusCode();

        // Author B tries to browse — should succeed
        var browseRequest = new HttpRequestMessage(HttpMethod.Get, $"/notebooks/{nb.Id}/browse");
        browseRequest.Headers.Add("X-Author-Id", AuthorB);
        var browseResponse = await _client.SendAsync(browseRequest);
        Assert.Equal(HttpStatusCode.OK, browseResponse.StatusCode);
    }

    [Fact]
    public async Task FlushCacheEndpointWorks()
    {
        var response = await _client.PostAsync("/admin/cache/flush", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
