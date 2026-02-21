using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record RegisterAgentRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("organization_id")]
    public required Guid OrganizationId { get; init; }

    [JsonPropertyName("max_level")]
    public string MaxLevel { get; init; } = "INTERNAL";

    [JsonPropertyName("compartments")]
    public List<string> Compartments { get; init; } = [];

    [JsonPropertyName("infrastructure")]
    public string? Infrastructure { get; init; }
}

public sealed record UpdateAgentRequest
{
    [JsonPropertyName("max_level")]
    public required string MaxLevel { get; init; }

    [JsonPropertyName("compartments")]
    public List<string> Compartments { get; init; } = [];

    [JsonPropertyName("infrastructure")]
    public string? Infrastructure { get; init; }
}

public sealed record AgentResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("organization_id")]
    public required Guid OrganizationId { get; init; }

    [JsonPropertyName("max_level")]
    public required string MaxLevel { get; init; }

    [JsonPropertyName("compartments")]
    public required List<string> Compartments { get; init; }

    [JsonPropertyName("infrastructure")]
    public string? Infrastructure { get; init; }

    [JsonPropertyName("registered")]
    public required DateTimeOffset Registered { get; init; }

    [JsonPropertyName("last_seen")]
    public DateTimeOffset? LastSeen { get; init; }
}

public sealed record ListAgentsResponse
{
    [JsonPropertyName("agents")]
    public required List<AgentResponse> Agents { get; init; }
}
