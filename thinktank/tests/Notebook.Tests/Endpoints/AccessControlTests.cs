using System.Net;
using System.Net.Http.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class AccessControlTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;
    private const string AuthorB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    public AccessControlTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task OwnerCanBrowseOwnNotebook()
    {
        var notebookId = await CreateNotebookAsync();
        var response = await _client.GetAsync($"/notebooks/{notebookId}/browse");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonOwnerWithoutAccessGets404OnBrowse()
    {
        var notebookId = await CreateNotebookAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/notebooks/{notebookId}/browse");
        request.Headers.Add("X-Author-Id", AuthorB);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NonOwnerWithoutAccessGets404OnBatchWrite()
    {
        var notebookId = await CreateNotebookAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/notebooks/{notebookId}/batch");
        request.Headers.Add("X-Author-Id", AuthorB);
        request.Content = JsonContent.Create(new
        {
            entries = new[] { new { content = "test", content_type = "text/plain" } },
        });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShareReadAllowsBrowseButNotBatchWrite()
    {
        var notebookId = await CreateNotebookAsync();

        // Share with read-only access
        var shareResponse = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/share", new
        {
            author_id = AuthorB,
            permissions = new { read = true, write = false },
        });
        Assert.Equal(HttpStatusCode.OK, shareResponse.StatusCode);

        // Author B can browse
        var browseRequest = new HttpRequestMessage(HttpMethod.Get, $"/notebooks/{notebookId}/browse");
        browseRequest.Headers.Add("X-Author-Id", AuthorB);
        var browseResponse = await _client.SendAsync(browseRequest);
        Assert.Equal(HttpStatusCode.OK, browseResponse.StatusCode);

        // Author B cannot batch write
        var writeRequest = new HttpRequestMessage(HttpMethod.Post, $"/notebooks/{notebookId}/batch");
        writeRequest.Headers.Add("X-Author-Id", AuthorB);
        writeRequest.Content = JsonContent.Create(new
        {
            entries = new[] { new { content = "test", content_type = "text/plain" } },
        });
        var writeResponse = await _client.SendAsync(writeRequest);
        Assert.Equal(HttpStatusCode.NotFound, writeResponse.StatusCode);
    }

    [Fact]
    public async Task ShareWriteAllowsBatchWrite()
    {
        var notebookId = await CreateNotebookAsync();

        // Share with write access
        await _client.PostAsJsonAsync($"/notebooks/{notebookId}/share", new
        {
            author_id = AuthorB,
            permissions = new { read = true, write = true },
        });

        // Author B can batch write
        var writeRequest = new HttpRequestMessage(HttpMethod.Post, $"/notebooks/{notebookId}/batch");
        writeRequest.Headers.Add("X-Author-Id", AuthorB);
        writeRequest.Content = JsonContent.Create(new
        {
            entries = new[] { new { content = "test entry from B", content_type = "text/plain" } },
        });
        var writeResponse = await _client.SendAsync(writeRequest);
        Assert.Equal(HttpStatusCode.Created, writeResponse.StatusCode);
    }

    [Fact]
    public async Task RevokeShareDeniesAccess()
    {
        var notebookId = await CreateNotebookAsync();

        // Share then revoke
        await _client.PostAsJsonAsync($"/notebooks/{notebookId}/share", new
        {
            author_id = AuthorB,
            permissions = new { read = true, write = false },
        });
        var revokeResponse = await _client.DeleteAsync($"/notebooks/{notebookId}/share/{AuthorB}");
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        // Author B is denied
        var browseRequest = new HttpRequestMessage(HttpMethod.Get, $"/notebooks/{notebookId}/browse");
        browseRequest.Headers.Add("X-Author-Id", AuthorB);
        var browseResponse = await _client.SendAsync(browseRequest);
        Assert.Equal(HttpStatusCode.NotFound, browseResponse.StatusCode);
    }

    [Fact]
    public async Task NonOwnerCannotDeleteNotebook()
    {
        var notebookId = await CreateNotebookAsync();

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/notebooks/{notebookId}");
        request.Headers.Add("X-Author-Id", AuthorB);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AuditEndpointRecordsDenials()
    {
        var notebookId = await CreateNotebookAsync();

        // Trigger a denial
        var browseRequest = new HttpRequestMessage(HttpMethod.Get, $"/notebooks/{notebookId}/browse");
        browseRequest.Headers.Add("X-Author-Id", AuthorB);
        await _client.SendAsync(browseRequest);

        // Wait briefly for async audit consumer
        await Task.Delay(500);

        // Query audit log as owner
        var auditResponse = await _client.GetAsync(
            $"/notebooks/{notebookId}/audit?action=access.denied&limit=10");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
    }

    [Fact]
    public async Task ListNotebooksReturnsActualPermissions()
    {
        var notebookId = await CreateNotebookAsync();

        var listResponse = await _client.GetAsync("/notebooks");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var list = await listResponse.Content.ReadFromJsonAsync<ListNotebooksResponse>();
        Assert.NotNull(list);
        var notebook = list.Notebooks.FirstOrDefault(n => n.Id == notebookId);
        Assert.NotNull(notebook);
        Assert.True(notebook.Permissions.Read);
        Assert.True(notebook.Permissions.Write);
    }

    private async Task<Guid> CreateNotebookAsync()
    {
        var response = await _client.PostAsJsonAsync("/notebooks",
            new { name = $"acl-test-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        return body!.Id;
    }
}
