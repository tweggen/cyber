using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Notebook.Tests.Endpoints;

public class AuditTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;

    public AuditTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task NotebookAudit_ReturnsCreateAction()
    {
        // Creating a notebook should produce an audit event
        var notebookId = await CreateNotebookAsync();

        // Give the audit consumer time to flush
        await Task.Delay(2500);

        var response = await _client.GetAsync($"/notebooks/{notebookId}/audit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = body.GetProperty("entries");
        Assert.True(entries.GetArrayLength() >= 1);

        // Should contain a notebook.create action
        var found = false;
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.GetProperty("action").GetString() == "notebook.create")
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected 'notebook.create' audit entry");
    }

    [Fact]
    public async Task NotebookAudit_FilterByAction()
    {
        var notebookId = await CreateNotebookAsync();
        await Task.Delay(2500);

        // Filter for a non-existent action
        var response = await _client.GetAsync($"/notebooks/{notebookId}/audit?action=nonexistent.action");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task EntryRead_ProducesAuditEvent()
    {
        var notebookId = await CreateNotebookAsync();

        // Write an entry
        var writeResponse = await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[] { new { content = "Audit test entry", content_type = "text/plain" } },
        });
        Assert.Equal(HttpStatusCode.Created, writeResponse.StatusCode);

        var writeBody = await writeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var entryId = writeBody.GetProperty("results")[0].GetProperty("entry_id").GetString();

        // Read the entry
        var readResponse = await _client.GetAsync($"/notebooks/{notebookId}/entries/{entryId}");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);

        // Wait for audit flush
        await Task.Delay(2500);

        // Check audit log for entry.read
        var auditResponse = await _client.GetAsync($"/notebooks/{notebookId}/audit?action=entry.read");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);

        var auditBody = await auditResponse.Content.ReadFromJsonAsync<JsonElement>();
        var entries = auditBody.GetProperty("entries");
        Assert.True(entries.GetArrayLength() >= 1, "Expected at least one 'entry.read' audit entry");

        var auditEntry = entries[0];
        Assert.Equal("entry.read", auditEntry.GetProperty("action").GetString());
        Assert.Equal("entry", auditEntry.GetProperty("target_type").GetString());
    }

    [Fact]
    public async Task EntryWrite_ProducesCorrectActionName()
    {
        var notebookId = await CreateNotebookAsync();

        await _client.PostAsJsonAsync($"/notebooks/{notebookId}/batch", new
        {
            entries = new[] { new { content = "Test", content_type = "text/plain" } },
        });

        await Task.Delay(2500);

        var response = await _client.GetAsync($"/notebooks/{notebookId}/audit?action=entry.write");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Should find entry.write (not entry.batch_write)
        Assert.True(body.GetProperty("entries").GetArrayLength() >= 1,
            "Expected 'entry.write' audit entry (renamed from 'entry.batch_write')");
    }

    [Fact]
    public async Task GlobalAudit_ReturnsEntries()
    {
        var notebookId = await CreateNotebookAsync();

        await Task.Delay(2500);

        var response = await _client.GetAsync("/audit?limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = body.GetProperty("entries");
        Assert.True(entries.GetArrayLength() >= 1, "Expected at least one audit entry in global query");
    }

    [Fact]
    public async Task GlobalAudit_FilterByAction()
    {
        await CreateNotebookAsync();
        await Task.Delay(2500);

        var response = await _client.GetAsync("/audit?action=notebook.create&limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = body.GetProperty("entries");
        Assert.True(entries.GetArrayLength() >= 1);

        foreach (var entry in entries.EnumerateArray())
        {
            Assert.Equal("notebook.create", entry.GetProperty("action").GetString());
        }
    }

    [Fact]
    public async Task GlobalAudit_FilterByResource()
    {
        var notebookId = await CreateNotebookAsync();
        await Task.Delay(2500);

        var response = await _client.GetAsync($"/audit?resource=notebook:{notebookId}&limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = body.GetProperty("entries");
        Assert.True(entries.GetArrayLength() >= 1);

        foreach (var entry in entries.EnumerateArray())
        {
            Assert.Equal(notebookId.ToString(), entry.GetProperty("notebook_id").GetString());
        }
    }

    [Fact]
    public async Task GlobalAudit_PaginationWithBefore()
    {
        // Create multiple notebooks to generate audit entries
        await CreateNotebookAsync();
        await CreateNotebookAsync();
        await CreateNotebookAsync();
        await Task.Delay(2500);

        var response1 = await _client.GetAsync("/audit?limit=2");
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var body1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        var entries1 = body1.GetProperty("entries");
        Assert.True(entries1.GetArrayLength() >= 1);

        // Use the last entry's id as cursor for next page
        var lastId = entries1[entries1.GetArrayLength() - 1].GetProperty("id").GetInt64();
        var response2 = await _client.GetAsync($"/audit?limit=2&before={lastId}");
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
    }

    [Fact]
    public async Task AccessGrant_UsesCorrectActionName()
    {
        var notebookId = await CreateNotebookAsync();

        // Share with a fake author
        var fakeAuthorId = Convert.ToHexString(new byte[32]).ToLowerInvariant();
        await _client.PostAsJsonAsync($"/notebooks/{notebookId}/share", new
        {
            author_id = fakeAuthorId,
            tier = "read",
        });

        await Task.Delay(2500);

        // Should be access.grant, not notebook.share
        var response = await _client.GetAsync($"/notebooks/{notebookId}/audit?action=access.grant");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("entries").GetArrayLength() >= 1,
            "Expected 'access.grant' audit entry (renamed from 'notebook.share')");
    }

    [Fact]
    public async Task AuditEntries_HaveExpectedFields()
    {
        var notebookId = await CreateNotebookAsync();
        await Task.Delay(2500);

        var response = await _client.GetAsync($"/notebooks/{notebookId}/audit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = body.GetProperty("entries");
        Assert.True(entries.GetArrayLength() >= 1);

        var entry = entries[0];
        Assert.True(entry.TryGetProperty("id", out _));
        Assert.True(entry.TryGetProperty("ts", out _));
        Assert.True(entry.TryGetProperty("action", out _));
        Assert.True(entry.TryGetProperty("notebook_id", out _));
    }

    // --- Helper ---
    private async Task<Guid> CreateNotebookAsync()
    {
        var response = await _client.PostAsJsonAsync("/notebooks", new
        {
            name = $"audit-test-{Guid.NewGuid():N}",
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
