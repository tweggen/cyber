using Notebook.Core.Security;

namespace Notebook.Server.Services;

public interface IClearanceService
{
    Task<SecurityLabel> GetClearanceAsync(byte[] authorId, Guid organizationId, CancellationToken ct);
    void EvictCache(byte[] authorId, Guid organizationId);
    void FlushAll();
}
