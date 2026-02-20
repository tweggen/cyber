using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMemberEntity>
{
    public void Configure(EntityTypeBuilder<GroupMemberEntity> builder)
    {
        builder.ToTable("group_members");

        builder.HasKey(m => new { m.GroupId, m.AuthorId });
        builder.Property(m => m.GroupId).HasColumnName("group_id");
        builder.Property(m => m.AuthorId).HasColumnName("author_id");
        builder.Property(m => m.Joined).HasColumnName("joined").HasDefaultValueSql("NOW()");

        builder.HasOne<GroupEntity>()
            .WithMany()
            .HasForeignKey(m => m.GroupId);
    }
}
