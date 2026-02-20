using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record ShareRequest
{
    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("read")]
    public required bool Read { get; init; }

    [JsonPropertyName("write")]
    public required bool Write { get; init; }
}

public sealed record ShareResponse
{
    [JsonPropertyName("notebook_id")]
    public required Guid NotebookId { get; init; }

    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("read")]
    public required bool Read { get; init; }

    [JsonPropertyName("write")]
    public required bool Write { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

public sealed record RevokeShareResponse
{
    [JsonPropertyName("notebook_id")]
    public required Guid NotebookId { get; init; }

    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

public sealed record ParticipantResponse
{
    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("read")]
    public required bool Read { get; init; }

    [JsonPropertyName("write")]
    public required bool Write { get; init; }

    [JsonPropertyName("granted")]
    public required DateTimeOffset Granted { get; init; }
}

public sealed record ListParticipantsResponse
{
    [JsonPropertyName("participants")]
    public required List<ParticipantResponse> Participants { get; init; }
}
