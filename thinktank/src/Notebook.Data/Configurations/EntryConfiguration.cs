using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Core.Types;

namespace Notebook.Data.Configurations;

public class EntryConfiguration : IEntityTypeConfiguration<Entry>
{
    public void Configure(EntityTypeBuilder<Entry> builder)
    {
        builder.ToTable("entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.NotebookId).HasColumnName("notebook_id");
        builder.Property(e => e.Content).HasColumnName("content");
        builder.Property(e => e.ContentType).HasColumnName("content_type");
        builder.Property(e => e.Topic).HasColumnName("topic");
        builder.Property(e => e.AuthorId).HasColumnName("author_id");
        builder.Property(e => e.Signature).HasColumnName("signature");
        builder.Property(e => e.RevisionOf).HasColumnName("revision_of");
        builder.Property(e => e.Sequence).HasColumnName("sequence");
        builder.Property(e => e.Created).HasColumnName("created");

        // UUID[] — Npgsql maps List<Guid> natively
        builder.Property(e => e.References)
            .HasColumnName("references");

        // JSONB via ValueConverter
        builder.Property(e => e.IntegrationCost)
            .HasColumnName("integration_cost")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<IntegrationCost>(v, (JsonSerializerOptions?)null));

        // ── v2 columns ──

        builder.Property(e => e.Claims)
            .HasColumnName("claims")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Claim>>(v, (JsonSerializerOptions?)null) ?? new List<Claim>());

        builder.Property(e => e.ClaimsStatus)
            .HasColumnName("claims_status")
            .HasDefaultValue(ClaimsStatus.Pending)
            .HasConversion<string>();

        builder.Property(e => e.FragmentOf)
            .HasColumnName("fragment_of");

        builder.Property(e => e.FragmentIndex)
            .HasColumnName("fragment_index");

        builder.Property(e => e.Comparisons)
            .HasColumnName("comparisons")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ClaimComparison>>(v, (JsonSerializerOptions?)null) ?? new List<ClaimComparison>());

        builder.Property(e => e.MaxFriction)
            .HasColumnName("max_friction");

        builder.Property(e => e.NeedsReview)
            .HasColumnName("needs_review")
            .HasDefaultValue(false);

        builder.Property(e => e.OriginalContentType)
            .HasColumnName("original_content_type");

        // double precision[] — Npgsql maps double[] natively
        builder.Property(e => e.Embedding)
            .HasColumnName("embedding");
    }
}
