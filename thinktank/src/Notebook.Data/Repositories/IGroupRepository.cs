using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public interface IGroupRepository
{
    Task<GroupEntity> CreateAsync(Guid orgId, string name, CancellationToken ct);
    Task<GroupEntity?> GetAsync(Guid groupId, CancellationToken ct);
    Task<List<GroupEntity>> ListByOrgAsync(Guid orgId, CancellationToken ct);
    Task<bool> DeleteAsync(Guid groupId, CancellationToken ct);

    Task<GroupMemberEntity> AddMemberAsync(Guid groupId, byte[] authorId, CancellationToken ct);
    Task<bool> RemoveMemberAsync(Guid groupId, byte[] authorId, CancellationToken ct);
    Task<List<GroupMemberEntity>> ListMembersAsync(Guid groupId, CancellationToken ct);

    Task<GroupEdgeEntity> AddEdgeAsync(Guid parentId, Guid childId, CancellationToken ct);
    Task<bool> RemoveEdgeAsync(Guid parentId, Guid childId, CancellationToken ct);
    Task<List<GroupEdgeEntity>> ListEdgesAsync(Guid orgId, CancellationToken ct);
    Task<bool> WouldCreateCycleAsync(Guid parentId, Guid childId, CancellationToken ct);
}
