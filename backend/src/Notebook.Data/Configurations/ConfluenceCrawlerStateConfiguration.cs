using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class ConfluenceCrawlerStateConfiguration : IEntityTypeConfiguration<ConfluenceCrawlerStateEntity>
{
    public void Configure(EntityTypeBuilder<ConfluenceCrawlerStateEntity> builder)
    {
        builder.ToTable("confluence_crawler_state");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.Config)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(s => s.SyncState)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(s => s.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.Property(s => s.UpdatedAt)
            .HasDefaultValueSql("now()");

        // Indexes for JSONB queries
        builder.HasIndex(s => s.Config)
            .HasMethod("GIN")
            .HasDatabaseName("idx_confluence_state_config");

        builder.HasIndex("SyncState")
            .HasMethod("GIN")
            .HasDatabaseName("idx_confluence_state_sync_state");
    }
}
