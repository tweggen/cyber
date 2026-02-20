using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMemberEntity>
{
    public void Configure(EntityTypeBuilder<OrganizationMemberEntity> builder)
    {
        builder.ToTable("organization_members");

        builder.HasKey(m => new { m.OrganizationId, m.AuthorId });
        builder.Property(m => m.OrganizationId).HasColumnName("organization_id");
        builder.Property(m => m.AuthorId).HasColumnName("author_id");
        builder.Property(m => m.Role).HasColumnName("role");
        builder.Property(m => m.Joined).HasColumnName("joined").HasDefaultValueSql("NOW()");

        builder.HasOne<OrganizationEntity>()
            .WithMany()
            .HasForeignKey(m => m.OrganizationId);
    }
}
