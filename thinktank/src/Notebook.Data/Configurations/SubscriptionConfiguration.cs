using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<SubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<SubscriptionEntity> builder)
    {
        builder.ToTable("notebook_subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.SubscriberId).HasColumnName("subscriber_id");
        builder.Property(s => s.SourceId).HasColumnName("source_id");
        builder.Property(s => s.Scope).HasColumnName("scope").HasDefaultValue("catalog");
        builder.Property(s => s.TopicFilter).HasColumnName("topic_filter");
        builder.Property(s => s.ApprovedBy).HasColumnName("approved_by");
        builder.Property(s => s.SyncWatermark).HasColumnName("sync_watermark").HasDefaultValue(0L);
        builder.Property(s => s.LastSyncAt).HasColumnName("last_sync_at");
        builder.Property(s => s.SyncStatus).HasColumnName("sync_status").HasDefaultValue("idle");
        builder.Property(s => s.SyncError).HasColumnName("sync_error");
        builder.Property(s => s.MirroredCount).HasColumnName("mirrored_count").HasDefaultValue(0);
        builder.Property(s => s.DiscountFactor).HasColumnName("discount_factor").HasDefaultValue(0.3);
        builder.Property(s => s.PollIntervalSeconds).HasColumnName("poll_interval_s").HasDefaultValue(60);
        builder.Property(s => s.EmbeddingModel).HasColumnName("embedding_model");
        builder.Property(s => s.Created).HasColumnName("created").HasDefaultValueSql("NOW()");

        builder.HasOne<NotebookEntity>().WithMany().HasForeignKey(s => s.SubscriberId);
        builder.HasOne<NotebookEntity>().WithMany().HasForeignKey(s => s.SourceId);

        builder.HasIndex(s => s.SubscriberId);
        builder.HasIndex(s => s.SourceId);
        builder.HasIndex(s => new { s.SubscriberId, s.SourceId }).IsUnique();
    }
}
