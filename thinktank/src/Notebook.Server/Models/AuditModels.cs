using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record AuditEntryResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("actor")]
    public required string Actor { get; init; }

    [JsonPropertyName("action")]
    public required string Action { get; init; }

    [JsonPropertyName("resource")]
    public required string Resource { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("ip")]
    public string? Ip { get; init; }

    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; init; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }
}

public sealed record AuditListResponse
{
    [JsonPropertyName("entries")]
    public required List<AuditEntryResponse> Entries { get; init; }

    [JsonPropertyName("count")]
    public required int Count { get; init; }
}
