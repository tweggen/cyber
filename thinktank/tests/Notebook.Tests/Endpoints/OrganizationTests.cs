using System.Net;
using System.Net.Http.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class OrganizationTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    public OrganizationTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task CreateOrganization_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/organizations",
            new { name = $"test-org-{Guid.NewGuid():N}" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
    }

    [Fact]
    public async Task ListOrganizations_ReturnsCreatedOrg()
    {
        var orgName = $"list-org-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/organizations", new { name = orgName });

        var response = await _client.GetAsync("/organizations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListOrganizationsResponse>();
        Assert.NotNull(body);
        Assert.Contains(body.Organizations, o => o.Name == orgName);
    }

    [Fact]
    public async Task CreateGroup_ReturnsCreated()
    {
        var orgId = await CreateOrgAsync();

        var response = await _client.PostAsJsonAsync($"/organizations/{orgId}/groups",
            new { name = "engineering" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GroupResponse>();
        Assert.NotNull(body);
        Assert.Equal("engineering", body.Name);
        Assert.Equal(orgId, body.OrganizationId);
    }

    [Fact]
    public async Task ListGroups_IncludesEdges()
    {
        var orgId = await CreateOrgAsync();
        var parentId = await CreateGroupAsync(orgId, "parent");
        var childId = await CreateGroupAsync(orgId, "child");

        await _client.PostAsJsonAsync($"/organizations/{orgId}/edges",
            new { parent_id = parentId, child_id = childId });

        var response = await _client.GetAsync($"/organizations/{orgId}/groups");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListGroupsResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Groups.Count);
        Assert.Single(body.Edges);
        Assert.Equal(parentId, body.Edges[0].ParentId);
        Assert.Equal(childId, body.Edges[0].ChildId);
    }

    [Fact]
    public async Task AddEdge_RejectsCycle()
    {
        var orgId = await CreateOrgAsync();
        var a = await CreateGroupAsync(orgId, "a");
        var b = await CreateGroupAsync(orgId, "b");
        var c = await CreateGroupAsync(orgId, "c");

        // a → b → c
        await _client.PostAsJsonAsync($"/organizations/{orgId}/edges",
            new { parent_id = a, child_id = b });
        await _client.PostAsJsonAsync($"/organizations/{orgId}/edges",
            new { parent_id = b, child_id = c });

        // c → a would create a cycle
        var response = await _client.PostAsJsonAsync($"/organizations/{orgId}/edges",
            new { parent_id = c, child_id = a });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddEdge_RejectsSelfEdge()
    {
        var orgId = await CreateOrgAsync();
        var a = await CreateGroupAsync(orgId, "self");

        var response = await _client.PostAsJsonAsync($"/organizations/{orgId}/edges",
            new { parent_id = a, child_id = a });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddAndRemoveMember()
    {
        var orgId = await CreateOrgAsync();
        var groupId = await CreateGroupAsync(orgId, "team");
        var memberHex = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        // Add member
        var addResponse = await _client.PostAsJsonAsync($"/groups/{groupId}/members",
            new { author_id = memberHex, role = "member" });
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        var member = await addResponse.Content.ReadFromJsonAsync<MemberResponse>();
        Assert.NotNull(member);
        Assert.Equal(memberHex, member.AuthorId);
        Assert.Equal("member", member.Role);

        // List members
        var listResponse = await _client.GetAsync($"/groups/{groupId}/members");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var members = await listResponse.Content.ReadFromJsonAsync<ListMembersResponse>();
        Assert.NotNull(members);
        Assert.Single(members.Members);

        // Remove member
        var removeResponse = await _client.DeleteAsync($"/groups/{groupId}/members/{memberHex}");
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);

        // Verify removed
        var listAfter = await _client.GetAsync($"/groups/{groupId}/members");
        var membersAfter = await listAfter.Content.ReadFromJsonAsync<ListMembersResponse>();
        Assert.NotNull(membersAfter);
        Assert.Empty(membersAfter.Members);
    }

    [Fact]
    public async Task AssignNotebookToGroup()
    {
        var orgId = await CreateOrgAsync();
        var groupId = await CreateGroupAsync(orgId, "owners");

        // Create a notebook
        var nbResponse = await _client.PostAsJsonAsync("/notebooks",
            new { name = $"nb-{Guid.NewGuid():N}" });
        nbResponse.EnsureSuccessStatusCode();
        var nb = await nbResponse.Content.ReadFromJsonAsync<CreateNotebookResponse>();

        // Assign to group
        var assignResponse = await _client.PutAsJsonAsync($"/notebooks/{nb!.Id}/group",
            new { group_id = groupId });
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteGroup_Succeeds()
    {
        var orgId = await CreateOrgAsync();
        var groupId = await CreateGroupAsync(orgId, "to-delete");

        var response = await _client.DeleteAsync($"/groups/{groupId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify gone from list
        var listResponse = await _client.GetAsync($"/organizations/{orgId}/groups");
        var body = await listResponse.Content.ReadFromJsonAsync<ListGroupsResponse>();
        Assert.NotNull(body);
        Assert.DoesNotContain(body.Groups, g => g.Id == groupId);
    }

    // ── Helpers ──

    private async Task<Guid> CreateOrgAsync()
    {
        var response = await _client.PostAsJsonAsync("/organizations",
            new { name = $"org-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<OrganizationResponse>();
        return body!.Id;
    }

    private async Task<Guid> CreateGroupAsync(Guid orgId, string name)
    {
        var response = await _client.PostAsJsonAsync($"/organizations/{orgId}/groups",
            new { name = $"{name}-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GroupResponse>();
        return body!.Id;
    }
}
