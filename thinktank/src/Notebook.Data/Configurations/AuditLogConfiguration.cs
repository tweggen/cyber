using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.Actor).HasColumnName("actor");
        builder.Property(a => a.Action).HasColumnName("action");
        builder.Property(a => a.Resource).HasColumnName("resource");
        builder.Property(a => a.Detail).HasColumnName("detail").HasColumnType("jsonb");
        builder.Property(a => a.Ip).HasColumnName("ip");
        builder.Property(a => a.UserAgent).HasColumnName("user_agent");
        builder.Property(a => a.Created).HasColumnName("created").HasDefaultValueSql("NOW()");
    }
}
