using Microsoft.EntityFrameworkCore;
using Notebook.Core.Types;
using Notebook.Data.Entities;

namespace Notebook.Data;

public class NotebookDbContext : DbContext
{
    public NotebookDbContext(DbContextOptions<NotebookDbContext> options)
        : base(options)
    {
    }

    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<JobEntity> Jobs => Set<JobEntity>();
    public DbSet<NotebookEntity> Notebooks => Set<NotebookEntity>();
    public DbSet<NotebookAccessEntity> NotebookAccess => Set<NotebookAccessEntity>();
    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    public DbSet<GroupEntity> Groups => Set<GroupEntity>();
    public DbSet<GroupEdgeEntity> GroupEdges => Set<GroupEdgeEntity>();
    public DbSet<GroupMembershipEntity> GroupMemberships => Set<GroupMembershipEntity>();
    public DbSet<PrincipalClearanceEntity> PrincipalClearances => Set<PrincipalClearanceEntity>();
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();
    public DbSet<SubscriptionEntity> Subscriptions => Set<SubscriptionEntity>();
    public DbSet<MirroredClaimEntity> MirroredClaims => Set<MirroredClaimEntity>();
    public DbSet<MirroredEntryEntity> MirroredEntries => Set<MirroredEntryEntity>();
    public DbSet<EntryReviewEntity> EntryReviews => Set<EntryReviewEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotebookDbContext).Assembly);
    }
}
