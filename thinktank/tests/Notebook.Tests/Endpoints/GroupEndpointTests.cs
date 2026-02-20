using System.Net;
using System.Net.Http.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class GroupEndpointTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    private const string AuthorA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string AuthorB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string AuthorC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    public GroupEndpointTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    private HttpRequestMessage WithAuthor(HttpMethod method, string url, string authorHex, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("X-Author-Id", authorHex);
        return request;
    }

    private async Task<OrganizationResponse> CreateOrgAsync(string name, string author = AuthorA)
    {
        var req = WithAuthor(HttpMethod.Post, "/organizations", author,
            JsonContent.Create(new { name }));
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var org = await resp.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(org);
        return org;
    }

    private async Task<GroupResponse> CreateGroupAsync(Guid orgId, string name, string author = AuthorA)
    {
        var req = WithAuthor(HttpMethod.Post, $"/organizations/{orgId}/groups", author,
            JsonContent.Create(new { name }));
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var group = await resp.Content.ReadFromJsonAsync<GroupResponse>();
        Assert.NotNull(group);
        return group;
    }

    [Fact]
    public async Task CreateGroup_WithinOrg()
    {
        var org = await CreateOrgAsync("group-create-org");
        var group = await CreateGroupAsync(org.Id, "engineering");

        Assert.Equal("engineering", group.Name);
        Assert.Equal(org.Id, group.OrganizationId);
    }

    [Fact]
    public async Task ListGroups_ReturnsOrgGroups()
    {
        var org = await CreateOrgAsync("group-list-org");
        await CreateGroupAsync(org.Id, "alpha");
        await CreateGroupAsync(org.Id, "beta");

        var req = WithAuthor(HttpMethod.Get, $"/organizations/{org.Id}/groups", AuthorA);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var list = await resp.Content.ReadFromJsonAsync<ListGroupsResponse>();
        Assert.NotNull(list);
        Assert.Equal(2, list.Groups.Count);
    }

    [Fact]
    public async Task AddRemove_GroupMembers()
    {
        var org = await CreateOrgAsync("group-member-org");
        var group = await CreateGroupAsync(org.Id, "devs");

        // Add AuthorB to org first (needed to be a valid member)
        var addOrgReq = WithAuthor(HttpMethod.Post, $"/organizations/{org.Id}/members", AuthorA,
            JsonContent.Create(new { author_id = AuthorB, role = "member" }));
        await _client.SendAsync(addOrgReq);

        // Add AuthorB to group
        var addReq = WithAuthor(HttpMethod.Post, $"/groups/{group.Id}/members", AuthorA,
            JsonContent.Create(new { author_id = AuthorB }));
        var addResp = await _client.SendAsync(addReq);
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);

        // List group members
        var listReq = WithAuthor(HttpMethod.Get, $"/groups/{group.Id}/members", AuthorA);
        var listResp = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var members = await listResp.Content.ReadFromJsonAsync<ListGroupMembersResponse>();
        Assert.NotNull(members);
        Assert.Contains(members.Members, m => m.AuthorId == AuthorB.ToLower());

        // Remove AuthorB from group
        var removeReq = WithAuthor(HttpMethod.Delete, $"/groups/{group.Id}/members/{AuthorB}", AuthorA);
        var removeResp = await _client.SendAsync(removeReq);
        Assert.Equal(HttpStatusCode.OK, removeResp.StatusCode);

        // Verify removed
        var listReq2 = WithAuthor(HttpMethod.Get, $"/groups/{group.Id}/members", AuthorA);
        var listResp2 = await _client.SendAsync(listReq2);
        var members2 = await listResp2.Content.ReadFromJsonAsync<ListGroupMembersResponse>();
        Assert.NotNull(members2);
        Assert.DoesNotContain(members2.Members, m => m.AuthorId == AuthorB.ToLower());
    }

    [Fact]
    public async Task AddEdge_VerifyDAG()
    {
        var org = await CreateOrgAsync("dag-test-org");
        var parent = await CreateGroupAsync(org.Id, "parent");
        var child = await CreateGroupAsync(org.Id, "child");

        // Add edge parent→child
        var edgeReq = WithAuthor(HttpMethod.Post, $"/groups/{parent.Id}/edges", AuthorA,
            JsonContent.Create(new { child_group_id = child.Id }));
        var edgeResp = await _client.SendAsync(edgeReq);
        Assert.Equal(HttpStatusCode.OK, edgeResp.StatusCode);

        var edge = await edgeResp.Content.ReadFromJsonAsync<GroupEdgeResponse>();
        Assert.NotNull(edge);
        Assert.Equal(parent.Id, edge.ParentGroupId);
        Assert.Equal(child.Id, edge.ChildGroupId);

        // List edges
        var listReq = WithAuthor(HttpMethod.Get, $"/groups/{parent.Id}/edges", AuthorA);
        var listResp = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var edges = await listResp.Content.ReadFromJsonAsync<ListGroupEdgesResponse>();
        Assert.NotNull(edges);
        Assert.Single(edges.Edges);
    }

    [Fact]
    public async Task DirectCycle_IsRejected()
    {
        var org = await CreateOrgAsync("cycle-direct-org");
        var groupA = await CreateGroupAsync(org.Id, "group-a");
        var groupB = await CreateGroupAsync(org.Id, "group-b");

        // A→B succeeds
        var edgeReq1 = WithAuthor(HttpMethod.Post, $"/groups/{groupA.Id}/edges", AuthorA,
            JsonContent.Create(new { child_group_id = groupB.Id }));
        var edgeResp1 = await _client.SendAsync(edgeReq1);
        Assert.Equal(HttpStatusCode.OK, edgeResp1.StatusCode);

        // B→A should fail with 409 Conflict
        var edgeReq2 = WithAuthor(HttpMethod.Post, $"/groups/{groupB.Id}/edges", AuthorA,
            JsonContent.Create(new { child_group_id = groupA.Id }));
        var edgeResp2 = await _client.SendAsync(edgeReq2);
        Assert.Equal(HttpStatusCode.Conflict, edgeResp2.StatusCode);
    }

    [Fact]
    public async Task TransitiveCycle_IsRejected()
    {
        var org = await CreateOrgAsync("cycle-transitive-org");
        var groupA = await CreateGroupAsync(org.Id, "t-group-a");
        var groupB = await CreateGroupAsync(org.Id, "t-group-b");
        var groupC = await CreateGroupAsync(org.Id, "t-group-c");

        // A→B
        var edgeReq1 = WithAuthor(HttpMethod.Post, $"/groups/{groupA.Id}/edges", AuthorA,
            JsonContent.Create(new { child_group_id = groupB.Id }));
        var edgeResp1 = await _client.SendAsync(edgeReq1);
        Assert.Equal(HttpStatusCode.OK, edgeResp1.StatusCode);

        // B→C
        var edgeReq2 = WithAuthor(HttpMethod.Post, $"/groups/{groupB.Id}/edges", AuthorA,
            JsonContent.Create(new { child_group_id = groupC.Id }));
        var edgeResp2 = await _client.SendAsync(edgeReq2);
        Assert.Equal(HttpStatusCode.OK, edgeResp2.StatusCode);

        // C→A should fail with 409 Conflict (transitive cycle)
        var edgeReq3 = WithAuthor(HttpMethod.Post, $"/groups/{groupC.Id}/edges", AuthorA,
            JsonContent.Create(new { child_group_id = groupA.Id }));
        var edgeResp3 = await _client.SendAsync(edgeReq3);
        Assert.Equal(HttpStatusCode.Conflict, edgeResp3.StatusCode);
    }

    [Fact]
    public async Task NonOrgMember_CannotAccessGroups()
    {
        var org = await CreateOrgAsync("access-test-org");
        var group = await CreateGroupAsync(org.Id, "secret-group");

        // AuthorB is not an org member — should get 404
        var listReq = WithAuthor(HttpMethod.Get, $"/organizations/{org.Id}/groups", AuthorB);
        var listResp = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.NotFound, listResp.StatusCode);

        var getReq = WithAuthor(HttpMethod.Get, $"/groups/{group.Id}", AuthorB);
        var getResp = await _client.SendAsync(getReq);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task OrgMember_CannotCreateGroups()
    {
        var org = await CreateOrgAsync("member-create-org");

        // Add AuthorB as regular member
        var addReq = WithAuthor(HttpMethod.Post, $"/organizations/{org.Id}/members", AuthorA,
            JsonContent.Create(new { author_id = AuthorB, role = "member" }));
        await _client.SendAsync(addReq);

        // AuthorB (member) tries to create a group — should get 404
        var createReq = WithAuthor(HttpMethod.Post, $"/organizations/{org.Id}/groups", AuthorB,
            JsonContent.Create(new { name = "unauthorized-group" }));
        var createResp = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.NotFound, createResp.StatusCode);
    }

    [Fact]
    public async Task DeleteGroup_ByOrgAdmin()
    {
        var org = await CreateOrgAsync("delete-group-org");
        var group = await CreateGroupAsync(org.Id, "to-delete");

        // Add AuthorB as admin
        var addReq = WithAuthor(HttpMethod.Post, $"/organizations/{org.Id}/members", AuthorA,
            JsonContent.Create(new { author_id = AuthorB, role = "admin" }));
        await _client.SendAsync(addReq);

        // AuthorB (admin) can delete
        var deleteReq = WithAuthor(HttpMethod.Delete, $"/groups/{group.Id}", AuthorB);
        var deleteResp = await _client.SendAsync(deleteReq);
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);

        // Verify it's gone
        var getReq = WithAuthor(HttpMethod.Get, $"/groups/{group.Id}", AuthorA);
        var getResp = await _client.SendAsync(getReq);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }
}
