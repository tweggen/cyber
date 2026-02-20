using System.Net;
using System.Net.Http.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class AccessControlTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    // Two distinct 32-byte author IDs (64 hex chars)
    private const string AuthorA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string AuthorB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    public AccessControlTests(NotebookApiFixture fixture)
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
    public async Task AuthorB_CannotAccess_AuthorA_Notebook()
    {
        // Author A creates a notebook
        var createReq = WithAuthor(HttpMethod.Post, "/notebooks", AuthorA,
            JsonContent.Create(new { name = "private-notebook" }));
        var createResp = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var created = await createResp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(created);

        // Author B tries to browse it — should get 404 (existence concealment)
        var browseReq = WithAuthor(HttpMethod.Get, $"/notebooks/{created.Id}/browse", AuthorB);
        var browseResp = await _client.SendAsync(browseReq);
        Assert.Equal(HttpStatusCode.NotFound, browseResp.StatusCode);

        // Author B tries to write to it — should get 404
        var batchReq = WithAuthor(HttpMethod.Post, $"/notebooks/{created.Id}/batch", AuthorB,
            JsonContent.Create(new { entries = new[] { new { content = "sneaky write" } } }));
        var batchResp = await _client.SendAsync(batchReq);
        Assert.Equal(HttpStatusCode.NotFound, batchResp.StatusCode);
    }

    [Fact]
    public async Task Owner_Always_HasAccess()
    {
        // Author A creates a notebook
        var createReq = WithAuthor(HttpMethod.Post, "/notebooks", AuthorA,
            JsonContent.Create(new { name = "owner-access-test" }));
        var createResp = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var created = await createResp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(created);

        // Author A can browse their own notebook
        var browseReq = WithAuthor(HttpMethod.Get, $"/notebooks/{created.Id}/browse", AuthorA);
        var browseResp = await _client.SendAsync(browseReq);
        Assert.Equal(HttpStatusCode.OK, browseResp.StatusCode);

        // Author A can view job stats on their own notebook
        var statsReq = WithAuthor(HttpMethod.Get, $"/notebooks/{created.Id}/jobs/stats", AuthorA);
        var statsResp = await _client.SendAsync(statsReq);
        Assert.Equal(HttpStatusCode.OK, statsResp.StatusCode);
    }

    [Fact]
    public async Task GrantedReadAccess_AllowsBrowse_DeniesWrite()
    {
        // Author A creates a notebook
        var createReq = WithAuthor(HttpMethod.Post, "/notebooks", AuthorA,
            JsonContent.Create(new { name = "shared-read-only" }));
        var createResp = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var created = await createResp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(created);

        // Author A grants Author B read-only access
        var shareReq = WithAuthor(HttpMethod.Post, $"/notebooks/{created.Id}/share/", AuthorA,
            JsonContent.Create(new { author_id = AuthorB, read = true, write = false }));
        var shareResp = await _client.SendAsync(shareReq);
        Assert.Equal(HttpStatusCode.OK, shareResp.StatusCode);

        // Author B can now browse
        var browseReq = WithAuthor(HttpMethod.Get, $"/notebooks/{created.Id}/browse", AuthorB);
        var browseResp = await _client.SendAsync(browseReq);
        Assert.Equal(HttpStatusCode.OK, browseResp.StatusCode);

        // Author B cannot write (read-only)
        var batchReq = WithAuthor(HttpMethod.Post, $"/notebooks/{created.Id}/batch", AuthorB,
            JsonContent.Create(new { entries = new[] { new { content = "read-only user write" } } }));
        var batchResp = await _client.SendAsync(batchReq);
        Assert.Equal(HttpStatusCode.NotFound, batchResp.StatusCode);
    }

    [Fact]
    public async Task NonexistentNotebook_Returns404()
    {
        var fakeId = Guid.NewGuid();
        var browseReq = WithAuthor(HttpMethod.Get, $"/notebooks/{fakeId}/browse", AuthorA);
        var browseResp = await _client.SendAsync(browseReq);
        Assert.Equal(HttpStatusCode.NotFound, browseResp.StatusCode);
    }

    [Fact]
    public async Task ListNotebooks_ShowsCorrectPermissions()
    {
        // Author A creates a notebook
        var createReq = WithAuthor(HttpMethod.Post, "/notebooks", AuthorA,
            JsonContent.Create(new { name = "permissions-test" }));
        var createResp = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var created = await createResp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(created);

        // Grant Author B read-only access
        var shareReq = WithAuthor(HttpMethod.Post, $"/notebooks/{created.Id}/share/", AuthorA,
            JsonContent.Create(new { author_id = AuthorB, read = true, write = false }));
        await _client.SendAsync(shareReq);

        // Author B lists notebooks — should see the shared one with read-only permissions
        var listReq = WithAuthor(HttpMethod.Get, "/notebooks", AuthorB);
        var listResp = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var list = await listResp.Content.ReadFromJsonAsync<ListNotebooksResponse>();
        Assert.NotNull(list);

        var sharedNotebook = list.Notebooks.FirstOrDefault(n => n.Id == created.Id);
        Assert.NotNull(sharedNotebook);
        Assert.False(sharedNotebook.IsOwner);
        Assert.True(sharedNotebook.Permissions.Read);
        Assert.False(sharedNotebook.Permissions.Write);
    }
}
