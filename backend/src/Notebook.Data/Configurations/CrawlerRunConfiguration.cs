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
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.CrawlerId)
            .IsRequired();

        builder.Property(r => r.StartedAt)
            .IsRequired();

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("running");

        builder.Property(r => r.ErrorMessage)
            .HasMaxLength(1000);

        builder.Property(r => r.Stats)
            .HasColumnType("jsonb");

        builder.Property(r => r.CreatedAt)
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
