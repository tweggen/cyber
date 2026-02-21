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
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.NotebookId)
            .HasColumnName("notebook_id")
            .IsRequired();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.SourceType)
            .HasColumnName("source_type")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.StateProvider)
            .HasColumnName("state_provider")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.StateRefId)
            .HasColumnName("state_ref_id")
            .IsRequired();

        builder.Property(c => c.IsEnabled)
            .HasColumnName("is_enabled")
            .HasDefaultValue(true);

        builder.Property(c => c.ScheduleCron)
            .HasColumnName("schedule_cron")
            .HasMaxLength(255);

        builder.Property(c => c.LastSyncAt)
            .HasColumnName("last_sync_at");

        builder.Property(c => c.LastSyncStatus)
            .HasColumnName("last_sync_status")
            .HasMaxLength(50);

        builder.Property(c => c.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(1000);

        builder.Property(c => c.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(c => c.OrganizationId)
            .HasColumnName("organization_id");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
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
