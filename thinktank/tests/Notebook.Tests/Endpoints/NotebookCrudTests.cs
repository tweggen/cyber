using System.Net;
using System.Net.Http.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class NotebookCrudTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    public NotebookCrudTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task CreateNotebook_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/notebooks", new { name = "test-notebook" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(body);
        Assert.Equal("test-notebook", body.Name);
        Assert.NotEqual(Guid.Empty, body.Id);
    }

    [Fact]
    public async Task CreateAndDeleteNotebook_Succeeds()
    {
        // Create
        var createResponse = await _client.PostAsJsonAsync("/notebooks", new { name = "to-delete" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateNotebookResponse>();
        Assert.NotNull(created);

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/notebooks/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var deleted = await deleteResponse.Content.ReadFromJsonAsync<DeleteNotebookResponse>();
        Assert.NotNull(deleted);
        Assert.Equal(created.Id, deleted.Id);

        // Verify it's gone — listing should not include it
        var listResponse = await _client.GetAsync("/notebooks");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var list = await listResponse.Content.ReadFromJsonAsync<ListNotebooksResponse>();
        Assert.NotNull(list);
        Assert.DoesNotContain(list.Notebooks, n => n.Id == created.Id);
    }
}
