using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record CreateNotebookRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("classification")]
    public string? Classification { get; init; }

    [JsonPropertyName("compartments")]
    public List<string>? Compartments { get; init; }
}

public sealed record CreateNotebookResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("owner")]
    public required string Owner { get; init; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }

    [JsonPropertyName("classification")]
    public required string Classification { get; init; }

    [JsonPropertyName("compartments")]
    public required List<string> Compartments { get; init; }
}

public sealed record RenameNotebookRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

public sealed record RenameNotebookResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

public sealed record DeleteNotebookResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

public sealed record NotebookSummaryResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("owner")]
    public required string Owner { get; init; }

    [JsonPropertyName("is_owner")]
    public required bool IsOwner { get; init; }

    [JsonPropertyName("permissions")]
    public required NotebookPermissionsResponse Permissions { get; init; }

    [JsonPropertyName("total_entries")]
    public required long TotalEntries { get; init; }

    [JsonPropertyName("total_entropy")]
    public required double TotalEntropy { get; init; }

    [JsonPropertyName("last_activity_sequence")]
    public required long LastActivitySequence { get; init; }

    [JsonPropertyName("participant_count")]
    public required long ParticipantCount { get; init; }

    [JsonPropertyName("classification")]
    public required string Classification { get; init; }

    [JsonPropertyName("compartments")]
    public required List<string> Compartments { get; init; }
}

public sealed record NotebookPermissionsResponse
{
    [JsonPropertyName("read")]
    public required bool Read { get; init; }

    [JsonPropertyName("write")]
    public required bool Write { get; init; }

    [JsonPropertyName("tier")]
    public required string Tier { get; init; }
}

public sealed record ListNotebooksResponse
{
    [JsonPropertyName("notebooks")]
    public required List<NotebookSummaryResponse> Notebooks { get; init; }
}
