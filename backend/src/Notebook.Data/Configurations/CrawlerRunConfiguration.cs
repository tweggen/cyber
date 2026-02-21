using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class CrawlerRunConfiguration : IEntityTypeConfiguration<CrawlerRunEntity>
{
    public void Configure(EntityTypeBuilder<CrawlerRunEntity> builder)
    {
        builder.ToTable("crawler_runs");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.CrawlerId)
            .HasColumnName("crawler_id")
            .IsRequired();

        builder.Property(r => r.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(r => r.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("running");

        builder.Property(r => r.EntriesCreated)
            .HasColumnName("entries_created");

        builder.Property(r => r.EntriesUpdated)
            .HasColumnName("entries_updated");

        builder.Property(r => r.EntriesUnchanged)
            .HasColumnName("entries_unchanged");

        builder.Property(r => r.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(1000);

        builder.Property(r => r.Stats)
            .HasColumnName("stats")
            .HasColumnType("jsonb");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(r => r.CrawlerId)
            .HasDatabaseName("idx_crawler_runs_crawler_id");

        builder.HasIndex(r => r.StartedAt)
            .HasDatabaseName("idx_crawler_runs_started_at")
            .IsDescending();

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("idx_crawler_runs_status")
            .HasFilter("\"status\" IN ('running', 'failed')");

        // Foreign key
        builder.HasOne(r => r.Crawler)
            .WithMany(c => c.Runs)
            .HasForeignKey(r => r.CrawlerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
