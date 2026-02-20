using System.Net;
using System.Net.Http.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class OrganizationEndpointTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    private const string AuthorA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string AuthorB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string AuthorC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    public OrganizationEndpointTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    private HttpRequestMessage WithAuthor(HttpMethod method, string url, string authorHex, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("X-Author-Id", authorHex);
        return request;
    }

    [Fact]
    public async Task CreateOrg_ReturnsCreated_WithOwnerMembership()
    {
        var req = WithAuthor(HttpMethod.Post, "/organizations", AuthorA,
            JsonContent.Create(new { name = "test-org" }));
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var org = await resp.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(org);
        Assert.Equal("test-org", org.Name);
        Assert.Equal(AuthorA.ToLower(), org.Owner);

        // Verify owner membership was auto-created
        var membersReq = WithAuthor(HttpMethod.Get, $"/organizations/{org.Id}/members", AuthorA);
        var membersResp = await _client.SendAsync(membersReq);
        Assert.Equal(HttpStatusCode.OK, membersResp.StatusCode);

        var members = await membersResp.Content.ReadFromJsonAsync<ListOrgMembersResponse>();
        Assert.NotNull(members);
        Assert.Single(members.Members);
        Assert.Equal(AuthorA.ToLower(), members.Members[0].AuthorId);
        Assert.Equal("owner", members.Members[0].Role);
    }

    [Fact]
    public async Task ListOrgs_ReturnsOnlyCallersOrgs()
    {
        // AuthorA creates an org
        var req = WithAuthor(HttpMethod.Post, "/organizations", AuthorA,
            JsonContent.Create(new { name = "list-test-org" }));
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        // AuthorB should not see it
        var listReq = WithAuthor(HttpMethod.Get, "/organizations", AuthorB);
        var listResp = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var list = await listResp.Content.ReadFromJsonAsync<ListOrganizationsResponse>();
        Assert.NotNull(list);
        Assert.DoesNotContain(list.Organizations, o => o.Name == "list-test-org");
    }

    [Fact]
    public async Task AddRemoveMembers_WithRoleChecks()
    {
        // Create org
        var createReq = WithAuthor(HttpMethod.Post, "/organizations", AuthorA,
            JsonContent.Create(new { name = "member-test-org" }));
        var createResp = await _client.SendAsync(createReq);
        var org = await createResp.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(org);

        // Owner adds AuthorB as admin
        var addReq = WithAuthor(HttpMethod.Post, $"/organizations/{org.Id}/members", AuthorA,
            JsonContent.Create(new { author_id = AuthorB, role = "admin" }));
        var addResp = await _client.SendAsync(addReq);
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);

        // AuthorB (admin) adds AuthorC as member
        var addReq2 = WithAuthor(HttpMethod.Post, $"/organizations/{org.Id}/members", AuthorB,
            JsonContent.Create(new { author_id = AuthorC, role = "member" }));
        var addResp2 = await _client.SendAsync(addReq2);
        Assert.Equal(HttpStatusCode.OK, addResp2.StatusCode);

        // AuthorC (member) cannot add members — returns 404
        var addReq3 = WithAuthor(HttpMethod.Post, $"/organizations/{org.Id}/members", AuthorC,
            JsonContent.Create(new { author_id = AuthorA, role = "member" }));
        var addResp3 = await _client.SendAsync(addReq3);
        Assert.Equal(HttpStatusCode.NotFound, addResp3.StatusCode);

        // Remove AuthorC
        var removeReq = WithAuthor(HttpMethod.Delete, $"/organizations/{org.Id}/members/{AuthorC}", AuthorA);
        var removeResp = await _client.SendAsync(removeReq);
        Assert.Equal(HttpStatusCode.OK, removeResp.StatusCode);

        // Verify AuthorC is gone
        var membersReq = WithAuthor(HttpMethod.Get, $"/organizations/{org.Id}/members", AuthorA);
        var membersResp = await _client.SendAsync(membersReq);
        var members = await membersResp.Content.ReadFromJsonAsync<ListOrgMembersResponse>();
        Assert.NotNull(members);
        Assert.DoesNotContain(members.Members, m => m.AuthorId == AuthorC.ToLower());
    }

    [Fact]
    public async Task OnlyOwner_CanDeleteOrg()
    {
        // Create org
        var createReq = WithAuthor(HttpMethod.Post, "/organizations", AuthorA,
            JsonContent.Create(new { name = "delete-test-org" }));
        var createResp = await _client.SendAsync(createReq);
        var org = await createResp.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(org);

        // Add AuthorB as admin
        var addReq = WithAuthor(HttpMethod.Post, $"/organizations/{org.Id}/members", AuthorA,
            JsonContent.Create(new { author_id = AuthorB, role = "admin" }));
        await _client.SendAsync(addReq);

        // AuthorB (admin) cannot delete — DeleteAsync checks owner_id
        var deleteReq = WithAuthor(HttpMethod.Delete, $"/organizations/{org.Id}", AuthorB);
        var deleteResp = await _client.SendAsync(deleteReq);
        Assert.Equal(HttpStatusCode.NotFound, deleteResp.StatusCode);

        // Owner can delete
        var deleteReq2 = WithAuthor(HttpMethod.Delete, $"/organizations/{org.Id}", AuthorA);
        var deleteResp2 = await _client.SendAsync(deleteReq2);
        Assert.Equal(HttpStatusCode.OK, deleteResp2.StatusCode);
    }

    [Fact]
    public async Task OnlyOwner_CanRenameOrg()
    {
        var createReq = WithAuthor(HttpMethod.Post, "/organizations", AuthorA,
            JsonContent.Create(new { name = "rename-test-org" }));
        var createResp = await _client.SendAsync(createReq);
        var org = await createResp.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(org);

        // AuthorB is not owner
        var renameReq = WithAuthor(HttpMethod.Patch, $"/organizations/{org.Id}", AuthorB,
            JsonContent.Create(new { name = "new-name" }));
        var renameResp = await _client.SendAsync(renameReq);
        Assert.Equal(HttpStatusCode.NotFound, renameResp.StatusCode);

        // Owner renames
        var renameReq2 = WithAuthor(HttpMethod.Patch, $"/organizations/{org.Id}", AuthorA,
            JsonContent.Create(new { name = "renamed-org" }));
        var renameResp2 = await _client.SendAsync(renameReq2);
        Assert.Equal(HttpStatusCode.OK, renameResp2.StatusCode);

        var renamed = await renameResp2.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(renamed);
        Assert.Equal("renamed-org", renamed.Name);
    }

    [Fact]
    public async Task NonMember_Gets404_OnGetOrg()
    {
        var createReq = WithAuthor(HttpMethod.Post, "/organizations", AuthorA,
            JsonContent.Create(new { name = "private-org" }));
        var createResp = await _client.SendAsync(createReq);
        var org = await createResp.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(org);

        var getReq = WithAuthor(HttpMethod.Get, $"/organizations/{org.Id}", AuthorB);
        var getResp = await _client.SendAsync(getReq);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }
}
