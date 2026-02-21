using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<GroupEntity>
{
    public void Configure(EntityTypeBuilder<GroupEntity> builder)
    {
        builder.ToTable("groups");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.OrganizationId).HasColumnName("organization_id");
        builder.Property(g => g.Name).HasColumnName("name");
        builder.Property(g => g.Created).HasColumnName("created").HasDefaultValueSql("NOW()");

        builder.HasOne<OrganizationEntity>()
            .WithMany()
            .HasForeignKey(g => g.OrganizationId);
    }
}
