using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Notebook.Server.Auth;
using Npgsql;

namespace Notebook.Server.Endpoints;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks/{notebookId}/audit", QueryNotebookAudit)
            .RequireAuthorization("CanAdmin");
        routes.MapGet("/audit", QueryGlobalAudit)
            .RequireAuthorization("CanAdmin");
    }

    /// <summary>
    /// Notebook-scoped audit log query. Returns audit entries for a specific notebook.
    /// </summary>
    private static async Task<IResult> QueryNotebookAudit(
        Guid notebookId,
        IAccessControl acl,
        IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken ct,
        string? action = null,
        int limit = 50,
        long? before = null)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireOwnerAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        limit = Math.Clamp(limit, 1, 200);

        var connectionString = configuration.GetConnectionString("Notebook");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var sql = """
            SELECT id, ts, notebook_id, author_id, action, target_type, target_id, detail, ip_address, user_agent
            FROM audit_log
            WHERE notebook_id = @notebookId
            """;

        if (action is not null)
            sql += " AND action = @action";
        if (before.HasValue)
            sql += " AND id < @before";

        sql += " ORDER BY id DESC LIMIT @limit";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("notebookId", notebookId);
        cmd.Parameters.AddWithValue("limit", limit);

        if (action is not null)
            cmd.Parameters.AddWithValue("action", action);
        if (before.HasValue)
            cmd.Parameters.AddWithValue("before", before.Value);

        var entries = await ReadAuditEntriesAsync(cmd, ct);
        return Results.Ok(new AuditResponse { Entries = entries });
    }

    /// <summary>
    /// Global audit log query. Returns audit entries across all notebooks.
    /// Supports filtering by actor, action, resource prefix, and date range.
    /// </summary>
    private static async Task<IResult> QueryGlobalAudit(
        IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken ct,
        [FromQuery] string? actor = null,
        [FromQuery] string? action = null,
        [FromQuery(Name = "resource")] string? resource = null,
        [FromQuery(Name = "from")] DateTimeOffset? from = null,
        [FromQuery(Name = "to")] DateTimeOffset? to = null,
        [FromQuery] int limit = 100,
        [FromQuery] long? before = null)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();

        limit = Math.Clamp(limit, 1, 200);

        var connectionString = configuration.GetConnectionString("Notebook");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var sql = """
            SELECT id, ts, notebook_id, author_id, action, target_type, target_id, detail, ip_address, user_agent
            FROM audit_log
            WHERE 1=1
            """;

        await using var cmd = conn.CreateCommand();

        if (actor is not null)
        {
            sql += " AND author_id = @actor";
            cmd.Parameters.AddWithValue("actor", Convert.FromHexString(actor));
        }

        if (action is not null)
        {
            sql += " AND action = @action";
            cmd.Parameters.AddWithValue("action", action);
        }

        if (resource is not null)
        {
            // resource prefix can be "notebook:{id}" or "entry", "agent", etc.
            if (resource.StartsWith("notebook:", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(resource.AsSpan(9), out var notebookGuid))
            {
                sql += " AND notebook_id = @resourceNotebookId";
                cmd.Parameters.AddWithValue("resourceNotebookId", notebookGuid);
            }
            else
            {
                // Match target_type prefix
                sql += " AND target_type LIKE @resourcePrefix";
                cmd.Parameters.AddWithValue("resourcePrefix", resource + "%");
            }
        }

        if (from.HasValue)
        {
            sql += " AND ts >= @from";
            cmd.Parameters.AddWithValue("from", from.Value);
        }

        if (to.HasValue)
        {
            sql += " AND ts <= @to";
            cmd.Parameters.AddWithValue("to", to.Value);
        }

        if (before.HasValue)
        {
            sql += " AND id < @before";
            cmd.Parameters.AddWithValue("before", before.Value);
        }

        sql += " ORDER BY id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("limit", limit);

        cmd.CommandText = sql;

        var entries = await ReadAuditEntriesAsync(cmd, ct);
        return Results.Ok(new AuditResponse { Entries = entries });
    }

    private static async Task<List<AuditLogEntry>> ReadAuditEntriesAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var entries = new List<AuditLogEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            entries.Add(new AuditLogEntry
            {
                Id = reader.GetInt64(0),
                Timestamp = reader.GetDateTime(1),
                NotebookId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                AuthorId = reader.IsDBNull(3) ? null : Convert.ToHexString((byte[])reader[3]).ToLowerInvariant(),
                Action = reader.GetString(4),
                TargetType = reader.IsDBNull(5) ? null : reader.GetString(5),
                TargetId = reader.IsDBNull(6) ? null : reader.GetString(6),
                Detail = reader.IsDBNull(7) ? null : JsonDocument.Parse(reader.GetString(7)).RootElement.Clone(),
                IpAddress = reader.IsDBNull(8) ? null : reader.GetValue(8).ToString(),
                UserAgent = reader.IsDBNull(9) ? null : reader.GetString(9),
            });
        }

        return entries;
    }
}

internal sealed record AuditLogEntry
{
    [JsonPropertyName("id")]
    public required long Id { get; init; }

    [JsonPropertyName("ts")]
    public required DateTime Timestamp { get; init; }

    [JsonPropertyName("notebook_id")]
    public Guid? NotebookId { get; init; }

    [JsonPropertyName("author_id")]
    public string? AuthorId { get; init; }

    [JsonPropertyName("action")]
    public required string Action { get; init; }

    [JsonPropertyName("target_type")]
    public string? TargetType { get; init; }

    [JsonPropertyName("target_id")]
    public string? TargetId { get; init; }

    [JsonPropertyName("detail")]
    public JsonElement? Detail { get; init; }

    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; init; }

    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; init; }
}

internal sealed record AuditResponse
{
    [JsonPropertyName("entries")]
    public required List<AuditLogEntry> Entries { get; init; }
}
