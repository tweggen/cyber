using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class NotebookConfiguration : IEntityTypeConfiguration<NotebookEntity>
{
    public void Configure(EntityTypeBuilder<NotebookEntity> builder)
    {
        builder.ToTable("notebooks");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.Name).HasColumnName("name");
        builder.Property(n => n.OwnerId).HasColumnName("owner_id");
        builder.Property(n => n.Created).HasColumnName("created").HasDefaultValueSql("NOW()");
        builder.Property(n => n.CurrentSequence).HasColumnName("current_sequence").HasDefaultValue(0L);
        builder.Property(n => n.OwningGroupId).HasColumnName("owning_group_id");

        builder.HasOne<GroupEntity>()
            .WithMany()
            .HasForeignKey(n => n.OwningGroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
