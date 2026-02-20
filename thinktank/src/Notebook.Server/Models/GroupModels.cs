using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public sealed record CreateGroupRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
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
}

public sealed record AddGroupMemberRequest
{
    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }
}

public sealed record GroupMemberResponse
{
    [JsonPropertyName("group_id")]
    public required Guid GroupId { get; init; }

    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("joined")]
    public required DateTimeOffset Joined { get; init; }
}

public sealed record ListGroupMembersResponse
{
    [JsonPropertyName("members")]
    public required List<GroupMemberResponse> Members { get; init; }
}

public sealed record AddEdgeRequest
{
    [JsonPropertyName("child_group_id")]
    public required Guid ChildGroupId { get; init; }
}

public sealed record GroupEdgeResponse
{
    [JsonPropertyName("parent_group_id")]
    public required Guid ParentGroupId { get; init; }

    [JsonPropertyName("child_group_id")]
    public required Guid ChildGroupId { get; init; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }
}

public sealed record ListGroupEdgesResponse
{
    [JsonPropertyName("edges")]
    public required List<GroupEdgeResponse> Edges { get; init; }
}
