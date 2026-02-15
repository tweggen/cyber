# Step 1: Schema Extension and C# Types

## Goal

Add the foundational data model for claims, fragments, comparisons, and jobs. This step changes no behavior — it only adds types and a DB migration. Everything builds on this.

## 1.1 — Database Migration

Create the EF Core migration or raw SQL file. The SQL is identical regardless of language — it targets PostgreSQL.

### Option A: Raw SQL migration

Create `notebook/migrations/007_claims_and_jobs.sql`:

```sql
-- Migration 007: Claims, fragments, comparisons, and job queue for thinktank v2

-- ==========================================================================
-- Extend entries table with claim and fragment fields
-- ==========================================================================

-- Claims: fixed-size claim representation (JSON array of {text, confidence})
ALTER TABLE entries ADD COLUMN IF NOT EXISTS claims JSONB NOT NULL DEFAULT '[]'::jsonb;

-- Claims processing status
ALTER TABLE entries ADD COLUMN IF NOT EXISTS claims_status TEXT NOT NULL DEFAULT 'pending'
    CHECK (claims_status IN ('pending', 'distilled', 'verified'));

-- Fragment support: link fragments to their parent artifact
ALTER TABLE entries ADD COLUMN IF NOT EXISTS fragment_of UUID REFERENCES entries(id);
ALTER TABLE entries ADD COLUMN IF NOT EXISTS fragment_index INTEGER;

-- Comparison results: stored as JSON array on the entry
ALTER TABLE entries ADD COLUMN IF NOT EXISTS comparisons JSONB NOT NULL DEFAULT '[]'::jsonb;

-- Precomputed max friction across all comparisons (for fast filtering)
ALTER TABLE entries ADD COLUMN IF NOT EXISTS max_friction DOUBLE PRECISION;

-- Whether this entry needs expensive LLM review
ALTER TABLE entries ADD COLUMN IF NOT EXISTS needs_review BOOLEAN NOT NULL DEFAULT false;

-- Fragment ordering constraint
ALTER TABLE entries ADD CONSTRAINT fragment_index_requires_parent
    CHECK ((fragment_of IS NULL AND fragment_index IS NULL) OR
           (fragment_of IS NOT NULL AND fragment_index IS NOT NULL));

-- ==========================================================================
-- Indexes for new fields
-- ==========================================================================

-- Find all fragments of an artifact
CREATE INDEX IF NOT EXISTS idx_entries_fragment_of
    ON entries(fragment_of)
    WHERE fragment_of IS NOT NULL;

-- Filter by claims_status (for job queue: find entries needing distillation)
CREATE INDEX IF NOT EXISTS idx_entries_claims_status
    ON entries(notebook_id, claims_status);

-- Filter by needs_review (for agents: find entries needing attention)
CREATE INDEX IF NOT EXISTS idx_entries_needs_review
    ON entries(notebook_id, needs_review)
    WHERE needs_review = true;

-- Filter by max_friction (for browsing high-friction entries)
CREATE INDEX IF NOT EXISTS idx_entries_max_friction
    ON entries(notebook_id, max_friction)
    WHERE max_friction IS NOT NULL;

-- ==========================================================================
-- Job queue table
-- ==========================================================================

CREATE TABLE IF NOT EXISTS jobs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    notebook_id UUID NOT NULL REFERENCES notebooks(id),
    job_type TEXT NOT NULL CHECK (job_type IN ('DISTILL_CLAIMS', 'COMPARE_CLAIMS', 'CLASSIFY_TOPIC')),
    status TEXT NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'in_progress', 'completed', 'failed')),
    payload JSONB NOT NULL,
    result JSONB,
    error TEXT,

    created TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    claimed_at TIMESTAMPTZ,
    claimed_by TEXT,
    completed_at TIMESTAMPTZ,
    timeout_seconds INTEGER NOT NULL DEFAULT 120,
    retry_count INTEGER NOT NULL DEFAULT 0,
    max_retries INTEGER NOT NULL DEFAULT 3
);

COMMENT ON TABLE jobs IS 'Work queue for robot workers (claim distillation, comparison, classification)';

-- Pull next pending job by type (FIFO order)
CREATE INDEX IF NOT EXISTS idx_jobs_pending
    ON jobs(notebook_id, job_type, created)
    WHERE status = 'pending';

-- Find in-progress jobs (for timeout checks)
CREATE INDEX IF NOT EXISTS idx_jobs_in_progress
    ON jobs(claimed_at)
    WHERE status = 'in_progress';

-- Stats per notebook and job type
CREATE INDEX IF NOT EXISTS idx_jobs_notebook_type_status
    ON jobs(notebook_id, job_type, status);
```

### Option B: EF Core migration

```bash
dotnet ef migrations add AddClaimsAndJobs --project src/Notebook.Data --startup-project src/Notebook.Server
```

The migration should produce the same schema as the raw SQL above.

### Verify

```bash
# Apply migration (requires running Postgres):
psql $DATABASE_URL -f notebook/migrations/007_claims_and_jobs.sql

# Or via EF Core:
dotnet ef database update --project src/Notebook.Data --startup-project src/Notebook.Server
```

## 1.2 — C# Core Types

Edit or create files in `Notebook.Core/Types/`.

### Claim.cs

```csharp
namespace Notebook.Core.Types;

using System.Text.Json.Serialization;

/// <summary>
/// A single factual claim extracted from an entry's content.
/// Claims are the fixed-size representation used for comparison,
/// navigation, and indexing.
/// </summary>
public sealed record Claim
{
    /// <summary>Short declarative statement (1-2 sentences).</summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>How central this claim is to the entry (0.0 to 1.0).</summary>
    [JsonPropertyName("confidence")]
    public required double Confidence { get; init; }
}

/// <summary>Processing status of an entry's claims.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClaimsStatus>))]
public enum ClaimsStatus
{
    [JsonStringEnumMemberName("pending")]
    Pending,

    [JsonStringEnumMemberName("distilled")]
    Distilled,

    [JsonStringEnumMemberName("verified")]
    Verified,
}
```

### ClaimComparison.cs

```csharp
namespace Notebook.Core.Types;

using System.Text.Json.Serialization;

/// <summary>
/// Result of comparing two claim-sets for entropy (novelty) and friction (contradiction).
/// </summary>
public sealed record ClaimComparison
{
    /// <summary>The entry this was compared against.</summary>
    [JsonPropertyName("compared_against")]
    public required Guid ComparedAgainst { get; init; }

    /// <summary>Novelty score: fraction of claims covering new ground (0.0 to 1.0).</summary>
    [JsonPropertyName("entropy")]
    public required double Entropy { get; init; }

    /// <summary>Contradiction score: fraction of claims that contradict existing claims (0.0 to 1.0).</summary>
    [JsonPropertyName("friction")]
    public required double Friction { get; init; }

    /// <summary>Details for each contradiction found.</summary>
    [JsonPropertyName("contradictions")]
    public required List<Contradiction> Contradictions { get; init; }

    /// <summary>When this comparison was computed.</summary>
    [JsonPropertyName("computed_at")]
    public required DateTimeOffset ComputedAt { get; init; }

    /// <summary>Which robot worker computed this.</summary>
    [JsonPropertyName("computed_by")]
    public required string ComputedBy { get; init; }
}

/// <summary>A specific contradiction between two claims.</summary>
public sealed record Contradiction
{
    /// <summary>The existing claim that is contradicted.</summary>
    [JsonPropertyName("claim_a")]
    public required string ClaimA { get; init; }

    /// <summary>The new claim that contradicts it.</summary>
    [JsonPropertyName("claim_b")]
    public required string ClaimB { get; init; }

    /// <summary>How directly they contradict (0.0 to 1.0).</summary>
    [JsonPropertyName("severity")]
    public required double Severity { get; init; }
}
```

### Job.cs

```csharp
namespace Notebook.Core.Types;

using System.Text.Json.Serialization;

/// <summary>Type of work for robot workers.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JobType>))]
public enum JobType
{
    [JsonStringEnumMemberName("DISTILL_CLAIMS")]
    DistillClaims,

    [JsonStringEnumMemberName("COMPARE_CLAIMS")]
    CompareClaims,

    [JsonStringEnumMemberName("CLASSIFY_TOPIC")]
    ClassifyTopic,
}

/// <summary>Status of a job in the queue.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JobStatus>))]
public enum JobStatus
{
    [JsonStringEnumMemberName("pending")]
    Pending,

    [JsonStringEnumMemberName("in_progress")]
    InProgress,

    [JsonStringEnumMemberName("completed")]
    Completed,

    [JsonStringEnumMemberName("failed")]
    Failed,
}
```

### Extend the Entry class

Add the new properties to the existing `Entry` class. Add these after `IntegrationCost`:

```csharp
/// <summary>Fixed-size claim representation extracted from content.</summary>
[JsonPropertyName("claims")]
public List<Claim> Claims { get; set; } = [];

/// <summary>Processing status of the claims.</summary>
[JsonPropertyName("claims_status")]
public ClaimsStatus ClaimsStatus { get; set; } = ClaimsStatus.Pending;

/// <summary>If this entry is a fragment of a larger artifact.</summary>
[JsonPropertyName("fragment_of")]
public Guid? FragmentOf { get; set; }

/// <summary>Position in fragment chain (0-based).</summary>
[JsonPropertyName("fragment_index")]
public int? FragmentIndex { get; set; }

/// <summary>Results of comparing this entry's claims against other entries.</summary>
[JsonPropertyName("comparisons")]
public List<ClaimComparison> Comparisons { get; set; } = [];

/// <summary>Highest friction score across all comparisons.</summary>
[JsonPropertyName("max_friction")]
public double? MaxFriction { get; set; }

/// <summary>True if max_friction exceeds the notebook's review threshold.</summary>
[JsonPropertyName("needs_review")]
public bool NeedsReview { get; set; }
```

## 1.3 — EF Core Entity Configuration

### EntryConfiguration.cs (in `Notebook.Data/`)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notebook.Core.Types;

namespace Notebook.Data;

public class EntryConfiguration : IEntityTypeConfiguration<Entry>
{
    public void Configure(EntityTypeBuilder<Entry> builder)
    {
        // ... existing configuration ...

        // New columns
        builder.Property(e => e.Claims)
            .HasColumnName("claims")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

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
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(e => e.MaxFriction)
            .HasColumnName("max_friction");

        builder.Property(e => e.NeedsReview)
            .HasColumnName("needs_review")
            .HasDefaultValue(false);

        // Fragment constraint is enforced at DB level via CHECK constraint
    }
}
```

### JobEntity.cs

```csharp
using System.Text.Json;

namespace Notebook.Data.Entities;

public class JobEntity
{
    public Guid Id { get; set; }
    public Guid NotebookId { get; set; }
    public string JobType { get; set; } = null!;
    public string Status { get; set; } = "pending";
    public JsonDocument Payload { get; set; } = null!;
    public JsonDocument? Result { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
}
```

### JobConfiguration.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Notebook.Data;

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
    }
}
```

### Important: backward compatibility

When reading entries written before this migration, the new columns have default values (empty arrays, "pending", null, false). The C# code handles these gracefully via default property values — `List<Claim>` defaults to `[]`, `ClaimsStatus` defaults to `Pending`, nullable types default to `null`.

## 1.4 — Tests

Add unit tests in `Notebook.Tests/Types/`:

### ClaimTypeTests.cs

```csharp
using System.Text.Json;
using Notebook.Core.Types;

namespace Notebook.Tests.Types;

public class ClaimTypeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Claim_Roundtrip()
    {
        var claim = new Claim { Text = "The sky is blue", Confidence = 0.95 };
        var json = JsonSerializer.Serialize(claim);
        var parsed = JsonSerializer.Deserialize<Claim>(json)!;
        Assert.Equal(claim.Text, parsed.Text);
        Assert.Equal(claim.Confidence, parsed.Confidence);
    }

    [Theory]
    [InlineData(ClaimsStatus.Pending, "\"pending\"")]
    [InlineData(ClaimsStatus.Distilled, "\"distilled\"")]
    [InlineData(ClaimsStatus.Verified, "\"verified\"")]
    public void ClaimsStatus_Roundtrip(ClaimsStatus status, string expectedJson)
    {
        var json = JsonSerializer.Serialize(status);
        Assert.Equal(expectedJson, json);
        var parsed = JsonSerializer.Deserialize<ClaimsStatus>(json);
        Assert.Equal(status, parsed);
    }

    [Fact]
    public void ClaimComparison_Roundtrip()
    {
        var comp = new ClaimComparison
        {
            ComparedAgainst = Guid.NewGuid(),
            Entropy = 0.58,
            Friction = 0.17,
            Contradictions =
            [
                new Contradiction
                {
                    ClaimA = "Deploys go to staging",
                    ClaimB = "Production skips staging",
                    Severity = 0.9,
                }
            ],
            ComputedAt = DateTimeOffset.UtcNow,
            ComputedBy = "robot-haiku-1",
        };
        var json = JsonSerializer.Serialize(comp);
        var parsed = JsonSerializer.Deserialize<ClaimComparison>(json)!;
        Assert.Equal(comp.Entropy, parsed.Entropy);
        Assert.Equal(comp.Friction, parsed.Friction);
        Assert.Single(parsed.Contradictions);
    }

    [Fact]
    public void JobType_Serde()
    {
        var jt = JobType.DistillClaims;
        var json = JsonSerializer.Serialize(jt);
        Assert.Equal("\"DISTILL_CLAIMS\"", json);
        var parsed = JsonSerializer.Deserialize<JobType>(json);
        Assert.Equal(jt, parsed);
    }

    [Fact]
    public void Entry_WithClaims_Roundtrip()
    {
        // Assuming Entry has a constructor or builder pattern
        var entry = new Entry
        {
            // ... required fields ...
            Claims = [new Claim { Text = "Test claim", Confidence = 0.9 }],
            ClaimsStatus = ClaimsStatus.Distilled,
        };
        var json = JsonSerializer.Serialize(entry);
        var parsed = JsonSerializer.Deserialize<Entry>(json)!;
        Assert.Single(parsed.Claims);
        Assert.Equal(ClaimsStatus.Distilled, parsed.ClaimsStatus);
    }
}
```

## Verify

```bash
dotnet build
dotnet test --filter "FullyQualifiedName~ClaimTypeTests"
dotnet format --verify-no-changes
```

## What This Enables

After this step, the data model supports everything v2 needs. The actual API endpoints, job processing, and robot workers are built in subsequent steps.
