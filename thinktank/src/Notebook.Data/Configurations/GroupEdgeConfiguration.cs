using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class GroupEdgeConfiguration : IEntityTypeConfiguration<GroupEdgeEntity>
{
    public void Configure(EntityTypeBuilder<GroupEdgeEntity> builder)
    {
        builder.ToTable("group_edges");

        builder.HasKey(e => new { e.ParentGroupId, e.ChildGroupId });
        builder.Property(e => e.ParentGroupId).HasColumnName("parent_group_id");
        builder.Property(e => e.ChildGroupId).HasColumnName("child_group_id");
        builder.Property(e => e.Created).HasColumnName("created").HasDefaultValueSql("NOW()");

        builder.HasOne<GroupEntity>()
            .WithMany()
            .HasForeignKey(e => e.ParentGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
