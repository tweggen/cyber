using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public interface IOrganizationRepository
{
    // Organizations
    Task<OrganizationEntity> CreateOrganizationAsync(string name, CancellationToken ct);
    Task<List<OrganizationEntity>> ListOrganizationsAsync(CancellationToken ct);
    Task<OrganizationEntity?> GetOrganizationAsync(Guid orgId, CancellationToken ct);

    // Groups
    Task<GroupEntity> CreateGroupAsync(Guid organizationId, string name, CancellationToken ct);
    Task<List<GroupEntity>> ListGroupsAsync(Guid organizationId, CancellationToken ct);
    Task<GroupEntity?> GetGroupAsync(Guid groupId, CancellationToken ct);
    Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken ct);

    // DAG edges
    Task<bool> AddEdgeAsync(Guid parentId, Guid childId, CancellationToken ct);
    Task<bool> RemoveEdgeAsync(Guid parentId, Guid childId, CancellationToken ct);
    Task<List<GroupEdgeEntity>> ListEdgesAsync(Guid organizationId, CancellationToken ct);

    // Memberships
    Task<GroupMembershipEntity> AddMemberAsync(Guid groupId, byte[] authorId, string role, byte[]? grantedBy, CancellationToken ct);
    Task<bool> RemoveMemberAsync(Guid groupId, byte[] authorId, CancellationToken ct);
    Task<List<GroupMembershipEntity>> ListMembersAsync(Guid groupId, CancellationToken ct);
    Task<List<GroupEntity>> ListGroupsForAuthorAsync(byte[] authorId, CancellationToken ct);

    // Group membership lookup (recursive, for access tier propagation)
    Task<string?> GetGroupMembershipRoleAsync(Guid owningGroupId, byte[] authorId, CancellationToken ct);

    // Notebook ownership
    Task<bool> AssignNotebookToGroupAsync(Guid notebookId, Guid groupId, byte[] requestingAuthorId, CancellationToken ct);
}
