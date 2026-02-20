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
    public DbSet<AuditLogEntity> AuditLog => Set<AuditLogEntity>();
    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    public DbSet<OrganizationMemberEntity> OrganizationMembers => Set<OrganizationMemberEntity>();
    public DbSet<GroupEntity> Groups => Set<GroupEntity>();
    public DbSet<GroupMemberEntity> GroupMembers => Set<GroupMemberEntity>();
    public DbSet<GroupEdgeEntity> GroupEdges => Set<GroupEdgeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotebookDbContext).Assembly);
    }
}
