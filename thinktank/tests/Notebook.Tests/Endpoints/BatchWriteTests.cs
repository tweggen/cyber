using System.Net;
using System.Net.Http.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class BatchWriteTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    private const string AuthorHex = "abab0000abab0000abab0000abab0000abab0000abab0000abab0000abab0000";

    public BatchWriteTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    private HttpRequestMessage WithAuthor(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("X-Author-Id", AuthorHex);
        return request;
    }

    private async Task<Guid> CreateNotebook(string name)
    {
        var req = WithAuthor(HttpMethod.Post, "/notebooks",
            JsonContent.Create(new { name }));
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task BatchWrite_SingleEntry_Succeeds()
    {
        var notebookId = await CreateNotebook("batch-single-test");

        var req = WithAuthor(HttpMethod.Post, $"/notebooks/{notebookId}/batch",
            JsonContent.Create(new
            {
                entries = new[] { new { content = "hello world" } }
            }));
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<BatchWriteResponse>();
        Assert.NotNull(body);
        Assert.Single(body.Results);
        Assert.Equal(1, body.Results[0].CausalPosition);
        Assert.Equal(1, body.JobsCreated);
    }

    [Fact]
    public async Task BatchWrite_MultipleEntries_SequencesAreMonotonic()
    {
        var notebookId = await CreateNotebook("batch-multi-test");

        var req = WithAuthor(HttpMethod.Post, $"/notebooks/{notebookId}/batch",
            JsonContent.Create(new
            {
                entries = new[]
                {
                    new { content = "first" },
                    new { content = "second" },
                    new { content = "third" },
                }
            }));
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<BatchWriteResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body.Results.Count);

        // Sequences should be monotonically increasing
        Assert.Equal(1, body.Results[0].CausalPosition);
        Assert.Equal(2, body.Results[1].CausalPosition);
        Assert.Equal(3, body.Results[2].CausalPosition);

        // All entry IDs should be distinct
        var ids = body.Results.Select(r => r.EntryId).ToHashSet();
        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public async Task BatchWrite_TwoBatches_SequencesContinue()
    {
        var notebookId = await CreateNotebook("batch-continue-test");

        // First batch
        var req1 = WithAuthor(HttpMethod.Post, $"/notebooks/{notebookId}/batch",
            JsonContent.Create(new
            {
                entries = new[] { new { content = "batch1-entry1" }, new { content = "batch1-entry2" } }
            }));
        var resp1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        // Second batch — sequences should continue from where first batch left off
        var req2 = WithAuthor(HttpMethod.Post, $"/notebooks/{notebookId}/batch",
            JsonContent.Create(new
            {
                entries = new[] { new { content = "batch2-entry1" } }
            }));
        var resp2 = await _client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.Created, resp2.StatusCode);

        var body2 = await resp2.Content.ReadFromJsonAsync<BatchWriteResponse>();
        Assert.NotNull(body2);
        Assert.Equal(3, body2.Results[0].CausalPosition);
    }

    [Fact]
    public async Task BatchWrite_EntriesAppearInBrowse()
    {
        var notebookId = await CreateNotebook("batch-browse-test");

        var req = WithAuthor(HttpMethod.Post, $"/notebooks/{notebookId}/batch",
            JsonContent.Create(new
            {
                entries = new[]
                {
                    new { content = "visible entry", topic = "test/browse" },
                }
            }));
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        // Browse should show the entry
        var browseReq = WithAuthor(HttpMethod.Get, $"/notebooks/{notebookId}/browse");
        var browseResp = await _client.SendAsync(browseReq);
        Assert.Equal(HttpStatusCode.OK, browseResp.StatusCode);

        var browseJson = await browseResp.Content.ReadAsStringAsync();
        Assert.Contains("test/browse", browseJson);
    }

    [Fact]
    public async Task BatchWrite_EmptyArray_ReturnsBadRequest()
    {
        var notebookId = await CreateNotebook("batch-empty-test");

        var req = WithAuthor(HttpMethod.Post, $"/notebooks/{notebookId}/batch",
            JsonContent.Create(new { entries = Array.Empty<object>() }));
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task BatchWrite_NonexistentNotebook_Returns404()
    {
        var fakeId = Guid.NewGuid();
        var req = WithAuthor(HttpMethod.Post, $"/notebooks/{fakeId}/batch",
            JsonContent.Create(new
            {
                entries = new[] { new { content = "should fail" } }
            }));
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
