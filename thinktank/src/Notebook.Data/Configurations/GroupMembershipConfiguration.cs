using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class GroupMembershipConfiguration : IEntityTypeConfiguration<GroupMembershipEntity>
{
    public void Configure(EntityTypeBuilder<GroupMembershipEntity> builder)
    {
        builder.ToTable("group_memberships");

        builder.HasKey(m => new { m.AuthorId, m.GroupId });
        builder.Property(m => m.AuthorId).HasColumnName("author_id");
        builder.Property(m => m.GroupId).HasColumnName("group_id");
        builder.Property(m => m.Role).HasColumnName("role").HasDefaultValue("member");
        builder.Property(m => m.Granted).HasColumnName("granted").HasDefaultValueSql("NOW()");
        builder.Property(m => m.GrantedBy).HasColumnName("granted_by");

        builder.HasOne<GroupEntity>()
            .WithMany()
            .HasForeignKey(m => m.GroupId);
    }
}
