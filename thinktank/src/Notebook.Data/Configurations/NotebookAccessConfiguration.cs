using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class NotebookAccessConfiguration : IEntityTypeConfiguration<NotebookAccessEntity>
{
    public void Configure(EntityTypeBuilder<NotebookAccessEntity> builder)
    {
        builder.ToTable("notebook_access");

        builder.HasKey(a => new { a.NotebookId, a.AuthorId });
        builder.Property(a => a.NotebookId).HasColumnName("notebook_id");
        builder.Property(a => a.AuthorId).HasColumnName("author_id");

        // Tell EF Core about the FK so it orders inserts correctly
        // (notebook row before notebook_access row).
        builder.HasOne<NotebookEntity>()
            .WithMany()
            .HasForeignKey(a => a.NotebookId);
        builder.Property(a => a.Read).HasColumnName("read");
        builder.Property(a => a.Write).HasColumnName("write");
        builder.Property(a => a.Granted).HasColumnName("granted").HasDefaultValueSql("NOW()");
    }
}
