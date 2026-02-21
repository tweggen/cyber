using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class CrawlerConfiguration : IEntityTypeConfiguration<CrawlerEntity>
{
    public void Configure(EntityTypeBuilder<CrawlerEntity> builder)
    {
        builder.ToTable("crawlers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.NotebookId)
            .IsRequired();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.SourceType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.StateProvider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.StateRefId)
            .IsRequired();

        builder.Property(c => c.IsEnabled)
            .HasDefaultValue(true);

        builder.Property(c => c.ScheduleCron)
            .HasMaxLength(255);

        builder.Property(c => c.LastSyncStatus)
            .HasMaxLength(50);

        builder.Property(c => c.LastError)
            .HasMaxLength(1000);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.Property(c => c.UpdatedAt)
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(c => c.NotebookId)
            .HasDatabaseName("idx_crawlers_notebook_id");

        builder.HasIndex(c => c.OrganizationId)
            .HasDatabaseName("idx_crawlers_organization_id");

        builder.HasIndex(c => c.LastSyncAt)
            .HasDatabaseName("idx_crawlers_last_sync_at")
            .IsDescending();

        builder.HasIndex(c => c.SourceType)
            .HasDatabaseName("idx_crawlers_source_type");

        builder.HasIndex(c => c.IsEnabled)
            .HasDatabaseName("idx_crawlers_is_enabled")
            .HasFilter("\"is_enabled\" = true");

        // Unique constraint: one crawler per notebook per source type
        builder.HasIndex(c => new { c.NotebookId, c.SourceType })
            .IsUnique()
            .HasDatabaseName("idx_crawlers_notebook_source_unique");

        // Navigation
        builder.HasMany(c => c.Runs)
            .WithOne(r => r.Crawler)
            .HasForeignKey(r => r.CrawlerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
