using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class EntryReviewConfiguration : IEntityTypeConfiguration<EntryReviewEntity>
{
    public void Configure(EntityTypeBuilder<EntryReviewEntity> builder)
    {
        builder.ToTable("entry_reviews");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(r => r.NotebookId).HasColumnName("notebook_id");
        builder.Property(r => r.EntryId).HasColumnName("entry_id");
        builder.Property(r => r.Submitter).HasColumnName("submitter");
        builder.Property(r => r.Status).HasColumnName("status").HasDefaultValue("pending");
        builder.Property(r => r.Reviewer).HasColumnName("reviewer");
        builder.Property(r => r.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(r => r.Created).HasColumnName("created").HasDefaultValueSql("NOW()");

        builder.HasOne<NotebookEntity>().WithMany().HasForeignKey(r => r.NotebookId);

        builder.HasIndex(r => r.NotebookId);
        builder.HasIndex(r => r.EntryId);
        builder.HasIndex(r => new { r.NotebookId, r.Status });
    }
}
