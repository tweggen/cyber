using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Data.Entities;

namespace Notebook.Data.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<JobEntity>
{
    public void Configure(EntityTypeBuilder<JobEntity> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(j => j.NotebookId).HasColumnName("notebook_id");
        builder.Property(j => j.JobType).HasColumnName("job_type");
        builder.Property(j => j.Status).HasColumnName("status").HasDefaultValue("pending");
        builder.Property(j => j.Payload).HasColumnName("payload").HasColumnType("jsonb");
        builder.Property(j => j.Result).HasColumnName("result").HasColumnType("jsonb");
        builder.Property(j => j.Error).HasColumnName("error");
        builder.Property(j => j.Created).HasColumnName("created").HasDefaultValueSql("NOW()");
        builder.Property(j => j.ClaimedAt).HasColumnName("claimed_at");
        builder.Property(j => j.ClaimedBy).HasColumnName("claimed_by");
        builder.Property(j => j.CompletedAt).HasColumnName("completed_at");
        builder.Property(j => j.TimeoutSeconds).HasColumnName("timeout_seconds").HasDefaultValue(120);
        builder.Property(j => j.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
        builder.Property(j => j.MaxRetries).HasColumnName("max_retries").HasDefaultValue(3);
        builder.Property(j => j.Priority).HasColumnName("priority").HasDefaultValue(0);
    }
}
