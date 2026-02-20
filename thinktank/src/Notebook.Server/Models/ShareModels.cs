using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record ShareRequest
{
    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("permissions")]
    public required SharePermissionsDto Permissions { get; init; }
}

public sealed record SharePermissionsDto
{
    [JsonPropertyName("read")]
    public bool Read { get; init; }

    [JsonPropertyName("write")]
    public bool Write { get; init; }
}

public sealed record ShareResponse
{
    [JsonPropertyName("notebook_id")]
    public required Guid NotebookId { get; init; }

    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("permissions")]
    public required SharePermissionsDto Permissions { get; init; }
}

public sealed record RevokeResponse
{
    [JsonPropertyName("notebook_id")]
    public required Guid NotebookId { get; init; }

    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
