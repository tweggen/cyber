using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record GrantClearanceRequest
{
    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("organization_id")]
    public required Guid OrganizationId { get; init; }

    [JsonPropertyName("max_level")]
    public required string MaxLevel { get; init; }

    [JsonPropertyName("compartments")]
    public List<string> Compartments { get; init; } = [];
}

public sealed record GrantClearanceResponse
{
    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("organization_id")]
    public required Guid OrganizationId { get; init; }

    [JsonPropertyName("max_level")]
    public required string MaxLevel { get; init; }

    [JsonPropertyName("compartments")]
    public required List<string> Compartments { get; init; }

    [JsonPropertyName("granted")]
    public required DateTimeOffset Granted { get; init; }
}

public sealed record RevokeClearanceResponse
{
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

public sealed record ClearanceSummaryResponse
{
    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("organization_id")]
    public required Guid OrganizationId { get; init; }

    [JsonPropertyName("max_level")]
    public required string MaxLevel { get; init; }

    [JsonPropertyName("compartments")]
    public required List<string> Compartments { get; init; }

    [JsonPropertyName("granted")]
    public required DateTimeOffset Granted { get; init; }
}

public sealed record ListClearancesResponse
{
    [JsonPropertyName("clearances")]
    public required List<ClearanceSummaryResponse> Clearances { get; init; }
}
