using System.Text.Json.Serialization;

namespace Notebook.Core.Types;

public sealed record SemanticSearchResult
{
    [JsonPropertyName("entry_id")]
    public Guid EntryId { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("similarity")]
    public double Similarity { get; init; }

    [JsonPropertyName("claims")]
    public List<Claim> Claims { get; init; } = [];

    [JsonPropertyName("claims_status")]
    public string ClaimsStatus { get; init; } = "pending";

    [JsonPropertyName("max_friction")]
    public double? MaxFriction { get; init; }

    [JsonPropertyName("integration_status")]
    public string IntegrationStatus { get; init; } = "probation";
}

public sealed record ClaimsBatchEntry
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("claims")]
    public List<Claim> Claims { get; init; } = [];

    [JsonPropertyName("claims_status")]
    public string ClaimsStatus { get; init; } = "pending";

    [JsonPropertyName("integration_status")]
    public string IntegrationStatus { get; init; } = "probation";
}
