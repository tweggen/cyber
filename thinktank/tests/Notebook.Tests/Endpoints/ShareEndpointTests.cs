using System.Net;
using System.Net.Http.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class ShareEndpointTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    private const string OwnerHex = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string GuestHex = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

    public ShareEndpointTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    private HttpRequestMessage WithAuthor(HttpMethod method, string url, string authorHex, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("X-Author-Id", authorHex);
        return request;
    }

    private async Task<Guid> CreateNotebook(string name, string authorHex)
    {
        var req = WithAuthor(HttpMethod.Post, "/notebooks", authorHex,
            JsonContent.Create(new { name }));
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task ShareAndList_Works()
    {
        var notebookId = await CreateNotebook("share-test", OwnerHex);

        // Grant access
        var shareReq = WithAuthor(HttpMethod.Post, $"/notebooks/{notebookId}/share/", OwnerHex,
            JsonContent.Create(new { author_id = GuestHex, read = true, write = true }));
        var shareResp = await _client.SendAsync(shareReq);
        Assert.Equal(HttpStatusCode.OK, shareResp.StatusCode);

        // List participants
        var listReq = WithAuthor(HttpMethod.Get, $"/notebooks/{notebookId}/share/", OwnerHex);
        var listResp = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var list = await listResp.Content.ReadFromJsonAsync<ListParticipantsResponse>();
        Assert.NotNull(list);
        // Owner + guest = 2 participants
        Assert.True(list.Participants.Count >= 2);
    }

    [Fact]
    public async Task RevokeAccess_Works()
    {
        var notebookId = await CreateNotebook("revoke-test", OwnerHex);

        // Grant access
        var shareReq = WithAuthor(HttpMethod.Post, $"/notebooks/{notebookId}/share/", OwnerHex,
            JsonContent.Create(new { author_id = GuestHex, read = true, write = true }));
        await _client.SendAsync(shareReq);

        // Guest can browse
        var browseReq = WithAuthor(HttpMethod.Get, $"/notebooks/{notebookId}/browse", GuestHex);
        var browseResp = await _client.SendAsync(browseReq);
        Assert.Equal(HttpStatusCode.OK, browseResp.StatusCode);

        // Revoke access
        var revokeReq = WithAuthor(HttpMethod.Delete, $"/notebooks/{notebookId}/share/{GuestHex}", OwnerHex);
        var revokeResp = await _client.SendAsync(revokeReq);
        Assert.Equal(HttpStatusCode.OK, revokeResp.StatusCode);

        // Guest can no longer browse
        var browseReq2 = WithAuthor(HttpMethod.Get, $"/notebooks/{notebookId}/browse", GuestHex);
        var browseResp2 = await _client.SendAsync(browseReq2);
        Assert.Equal(HttpStatusCode.NotFound, browseResp2.StatusCode);
    }

    [Fact]
    public async Task NonOwner_CannotShare()
    {
        var notebookId = await CreateNotebook("non-owner-share-test", OwnerHex);

        // Grant guest read access
        var shareReq = WithAuthor(HttpMethod.Post, $"/notebooks/{notebookId}/share/", OwnerHex,
            JsonContent.Create(new { author_id = GuestHex, read = true, write = false }));
        await _client.SendAsync(shareReq);

        // Guest tries to share — should fail (not owner)
        var thirdParty = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        var guestShareReq = WithAuthor(HttpMethod.Post, $"/notebooks/{notebookId}/share/", GuestHex,
            JsonContent.Create(new { author_id = thirdParty, read = true, write = false }));
        var guestShareResp = await _client.SendAsync(guestShareReq);
        Assert.Equal(HttpStatusCode.NotFound, guestShareResp.StatusCode);
    }
}
