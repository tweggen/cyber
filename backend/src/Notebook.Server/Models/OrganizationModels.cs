using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

// ── Organizations ──

public sealed record CreateOrganizationRequest
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

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }
}

public sealed record ListOrganizationsResponse
{
    [JsonPropertyName("organizations")]
    public required List<OrganizationResponse> Organizations { get; init; }
}

// ── Groups ──

public sealed record CreateGroupRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("parent_id")]
    public Guid? ParentId { get; init; }
}

public sealed record GroupResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("organization_id")]
    public required Guid OrganizationId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }
}

public sealed record ListGroupsResponse
{
    [JsonPropertyName("groups")]
    public required List<GroupResponse> Groups { get; init; }

    [JsonPropertyName("edges")]
    public required List<EdgeResponse> Edges { get; init; }
}

public sealed record EdgeResponse
{
    [JsonPropertyName("parent_id")]
    public required Guid ParentId { get; init; }

    [JsonPropertyName("child_id")]
    public required Guid ChildId { get; init; }
}

// ── Edges ──

public sealed record AddEdgeRequest
{
    [JsonPropertyName("parent_id")]
    public required Guid ParentId { get; init; }

    [JsonPropertyName("child_id")]
    public required Guid ChildId { get; init; }
}

// ── Memberships ──

public sealed record AddMemberRequest
{
    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("role")]
    public string Role { get; init; } = "member";
}

public sealed record MemberResponse
{
    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("group_id")]
    public required Guid GroupId { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("granted")]
    public required DateTimeOffset Granted { get; init; }

    [JsonPropertyName("granted_by")]
    public string? GrantedBy { get; init; }
}

public sealed record ListMembersResponse
{
    [JsonPropertyName("members")]
    public required List<MemberResponse> Members { get; init; }
}

// ── Notebook Assignment ──

public sealed record AssignGroupRequest
{
    [JsonPropertyName("group_id")]
    public required Guid GroupId { get; init; }
}
