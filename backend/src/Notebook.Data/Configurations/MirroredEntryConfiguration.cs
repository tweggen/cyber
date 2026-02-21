using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class MirroredEntryConfiguration : IEntityTypeConfiguration<MirroredEntryEntity>
{
    public void Configure(EntityTypeBuilder<MirroredEntryEntity> builder)
    {
        builder.ToTable("mirrored_entries");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(m => m.SubscriptionId).HasColumnName("subscription_id");
        builder.Property(m => m.SourceEntryId).HasColumnName("source_entry_id");
        builder.Property(m => m.NotebookId).HasColumnName("notebook_id");
        builder.Property(m => m.Content).HasColumnName("content");
        builder.Property(m => m.ContentType).HasColumnName("content_type").HasDefaultValue("text/plain");
        builder.Property(m => m.Topic).HasColumnName("topic");
        builder.Property(m => m.SourceSequence).HasColumnName("source_sequence").HasDefaultValue(0L);
        builder.Property(m => m.Tombstoned).HasColumnName("tombstoned").HasDefaultValue(false);
        builder.Property(m => m.MirroredAt).HasColumnName("mirrored_at").HasDefaultValueSql("NOW()");

        builder.HasOne<SubscriptionEntity>().WithMany().HasForeignKey(m => m.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<NotebookEntity>().WithMany().HasForeignKey(m => m.NotebookId);

        builder.HasIndex(m => m.SubscriptionId);
        builder.HasIndex(m => m.NotebookId);
        builder.HasIndex(m => new { m.SubscriptionId, m.SourceEntryId }).IsUnique();
    }
}
