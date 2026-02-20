using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<AgentEntity>
{
    public void Configure(EntityTypeBuilder<AgentEntity> builder)
    {
        builder.ToTable("agents");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.OrganizationId).HasColumnName("organization_id");
        builder.Property(a => a.MaxLevel).HasColumnName("max_level").HasDefaultValue("INTERNAL");
        builder.Property(a => a.Compartments).HasColumnName("compartments");
        builder.Property(a => a.Infrastructure).HasColumnName("infrastructure");
        builder.Property(a => a.Registered).HasColumnName("registered").HasDefaultValueSql("NOW()");
        builder.Property(a => a.LastSeen).HasColumnName("last_seen");

        builder.HasOne<OrganizationEntity>()
            .WithMany()
            .HasForeignKey(a => a.OrganizationId);
    }
}
