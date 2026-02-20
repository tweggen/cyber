using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public interface IOrganizationRepository
{
    Task<OrganizationEntity> CreateAsync(string name, byte[] ownerId, CancellationToken ct);
    Task<OrganizationEntity?> GetAsync(Guid orgId, CancellationToken ct);
    Task<List<OrganizationEntity>> ListByAuthorAsync(byte[] authorId, CancellationToken ct);
    Task<bool> DeleteAsync(Guid orgId, byte[] requestingAuthorId, CancellationToken ct);
    Task<OrganizationEntity?> RenameAsync(Guid orgId, string newName, byte[] requestingAuthorId, CancellationToken ct);

    Task<OrganizationMemberEntity> AddMemberAsync(Guid orgId, byte[] authorId, string role, CancellationToken ct);
    Task<bool> RemoveMemberAsync(Guid orgId, byte[] authorId, CancellationToken ct);
    Task<List<OrganizationMemberEntity>> ListMembersAsync(Guid orgId, CancellationToken ct);
    Task<OrganizationMemberEntity?> GetMemberAsync(Guid orgId, byte[] authorId, CancellationToken ct);
}
