using System.Text.Json;
using System.Text.Json.Serialization;
using Notebook.Server.Auth;
using Npgsql;

namespace Notebook.Server.Endpoints;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks/{notebookId}/audit", QueryAudit)
            .RequireAuthorization("CanAdmin");
    }

    private static async Task<IResult> QueryAudit(
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

        return Results.Ok(new AuditResponse { Entries = entries });
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
