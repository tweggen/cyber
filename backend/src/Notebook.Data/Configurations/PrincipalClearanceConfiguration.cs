using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class PrincipalClearanceConfiguration : IEntityTypeConfiguration<PrincipalClearanceEntity>
{
    public void Configure(EntityTypeBuilder<PrincipalClearanceEntity> builder)
    {
        builder.ToTable("principal_clearances");

        builder.HasKey(c => new { c.AuthorId, c.OrganizationId });
        builder.Property(c => c.AuthorId).HasColumnName("author_id");
        builder.Property(c => c.OrganizationId).HasColumnName("organization_id");
        builder.Property(c => c.MaxLevel).HasColumnName("max_level").HasDefaultValue("INTERNAL");
        builder.Property(c => c.Compartments).HasColumnName("compartments");
        builder.Property(c => c.Granted).HasColumnName("granted").HasDefaultValueSql("NOW()");
        builder.Property(c => c.GrantedBy).HasColumnName("granted_by");

        builder.HasOne<OrganizationEntity>()
            .WithMany()
            .HasForeignKey(c => c.OrganizationId);
    }
}
