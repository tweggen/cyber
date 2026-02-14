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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotebookDbContext).Assembly);
    }
}
