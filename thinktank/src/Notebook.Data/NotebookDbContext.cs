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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotebookDbContext).Assembly);
    }
}
