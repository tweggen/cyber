using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public interface IMirroredContentRepository
{
    Task<MirroredClaimEntity> UpsertMirroredClaimAsync(MirroredClaimEntity claim, CancellationToken ct);
    Task<MirroredEntryEntity> UpsertMirroredEntryAsync(MirroredEntryEntity entry, CancellationToken ct);
    Task TombstoneAsync(Guid subscriptionId, Guid sourceEntryId, CancellationToken ct);
    Task UpdateEmbeddingAsync(Guid mirroredClaimId, double[] embedding, CancellationToken ct);
    Task<int> CountBySubscriptionAsync(Guid subscriptionId, CancellationToken ct);
}
