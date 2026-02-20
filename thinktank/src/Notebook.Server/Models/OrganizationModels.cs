using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record CreateOrganizationRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

public sealed record RenameOrganizationRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

public sealed record OrganizationResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("owner")]
    public required string Owner { get; init; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }
}

public sealed record ListOrganizationsResponse
{
    [JsonPropertyName("organizations")]
    public required List<OrganizationResponse> Organizations { get; init; }
}

public sealed record AddOrgMemberRequest
{
    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }
}

public sealed record OrgMemberResponse
{
    [JsonPropertyName("organization_id")]
    public required Guid OrganizationId { get; init; }

    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("joined")]
    public required DateTimeOffset Joined { get; init; }
}

public sealed record ListOrgMembersResponse
{
    [JsonPropertyName("members")]
    public required List<OrgMemberResponse> Members { get; init; }
}
