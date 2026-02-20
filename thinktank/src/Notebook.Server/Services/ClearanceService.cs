using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Notebook.Core.Security;
using Notebook.Data;

namespace Notebook.Server.Services;

public class ClearanceService(IServiceScopeFactory scopeFactory, IMemoryCache cache) : IClearanceService
{
    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromSeconds(30),
    };

    public async Task<SecurityLabel> GetClearanceAsync(byte[] authorId, Guid organizationId, CancellationToken ct)
    {
        var key = CacheKey(authorId, organizationId);
        if (cache.TryGetValue<SecurityLabel>(key, out var cached) && cached is not null)
            return cached;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotebookDbContext>();

        var clearance = await db.PrincipalClearances.AsNoTracking()
            .FirstOrDefaultAsync(c => c.AuthorId == authorId && c.OrganizationId == organizationId, ct);

        var label = clearance is not null
            ? new SecurityLabel(
                ClassificationLevelExtensions.ParseClassification(clearance.MaxLevel),
                clearance.Compartments.ToHashSet())
            : SecurityLabel.Default;

        cache.Set(key, label, CacheOptions);
        return label;
    }

    public void EvictCache(byte[] authorId, Guid organizationId)
        => cache.Remove(CacheKey(authorId, organizationId));

    public void FlushAll()
    {
        if (cache is MemoryCache mc)
            mc.Compact(1.0);
    }

    private static string CacheKey(byte[] authorId, Guid organizationId)
        => $"clearance:{Convert.ToHexString(authorId)}:{organizationId}";
}
