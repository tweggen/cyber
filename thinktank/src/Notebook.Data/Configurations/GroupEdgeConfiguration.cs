using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class GroupEdgeConfiguration : IEntityTypeConfiguration<GroupEdgeEntity>
{
    public void Configure(EntityTypeBuilder<GroupEdgeEntity> builder)
    {
        builder.ToTable("group_edges");

        builder.HasKey(e => new { e.ParentId, e.ChildId });
        builder.Property(e => e.ParentId).HasColumnName("parent_id");
        builder.Property(e => e.ChildId).HasColumnName("child_id");

        builder.HasOne<GroupEntity>()
            .WithMany()
            .HasForeignKey(e => e.ParentId);
    }
}
