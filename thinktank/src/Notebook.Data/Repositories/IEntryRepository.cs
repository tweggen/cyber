using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using Notebook.Core.Types;

namespace Notebook.Data.Repositories;

public interface IEntryRepository
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
    Task<bool> NotebookExistsAsync(Guid notebookId, CancellationToken ct);
    Task<Entry> InsertEntryAsync(Guid notebookId, byte[] authorId, NewEntry entry, CancellationToken ct);
    Task<bool> UpdateEntryClaimsAsync(Guid entryId, Guid notebookId, List<Claim> claims, CancellationToken ct);
    Task<int> AppendComparisonAsync(Guid entryId, JsonElement comparison, double discountFactor = 1.0, CancellationToken ct = default);
    Task UpdateEntryEmbeddingAsync(Guid entryId, Guid notebookId, double[] embedding, CancellationToken ct);
    Task UpdateExpectedComparisonsAsync(Guid entryId, Guid notebookId, int count, CancellationToken ct);
    Task UpdateIntegrationStatusAsync(Guid entryId, IntegrationStatus status, CancellationToken ct);
    Task<List<(Guid Id, List<Claim> Claims, double Similarity)>> FindNearestByEmbeddingAsync(
        Guid notebookId, Guid excludeEntryId, double[] query, int topK, CancellationToken ct);
    Task UpdateEntryTopicAsync(Guid entryId, string topic, CancellationToken ct);
    Task<List<BrowseEntry>> BrowseFilteredAsync(Guid notebookId, BrowseFilter filters, CancellationToken ct);
    Task<List<SearchResult>> SearchEntriesAsync(Guid notebookId, string query, string searchIn, string? topicPrefix, int maxResults, CancellationToken ct);

    // Semantic search
    Task<List<SemanticSearchResult>> SemanticSearchAsync(
        Guid notebookId, double[] queryEmbedding, int topK, double minSimilarity, CancellationToken ct);
    Task<List<ClaimsBatchEntry>> GetClaimsBatchAsync(
        Guid notebookId, List<Guid> entryIds, CancellationToken ct);

    // Cross-boundary neighbor search (includes mirrored claims)
    Task<List<NeighborResult>> FindNearestWithMirroredAsync(
        Guid notebookId, Guid excludeEntryId, double[] query, int topK, CancellationToken ct);

    // Sync support
    Task<List<Entry>> GetEntriesAfterSequenceAsync(Guid notebookId, long afterSequence, int limit, CancellationToken ct);

    // Fragment queries
    Task<Entry?> GetEntryAsync(Guid entryId, Guid notebookId, CancellationToken ct);
    Task<Entry?> GetFragmentAsync(Guid notebookId, Guid fragmentOf, int fragmentIndex, CancellationToken ct);
    Task<List<Claim>> GetFragmentClaimsUpToAsync(Guid notebookId, Guid fragmentOf, int upToIndex, CancellationToken ct);
    Task<int> GetFragmentCountAsync(Guid notebookId, Guid fragmentOf, CancellationToken ct);
}
