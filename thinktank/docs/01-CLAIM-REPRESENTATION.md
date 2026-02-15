# 01 — Claim Representation

## Concept

Every notebook entry gets a fixed-size "claim representation" — a set of N short declarative statements that capture the entry's essential information. The original content is preserved for reference, but the claims are what the system operates on for navigation, comparison, and indexing.

This is analogous to how the brain doesn't store raw sensory data for pattern matching — it stores compressed representations that are cheap to compare.

## Data Model

### Entry (extended)

```
Entry {
  // Existing fields (unchanged)
  id: UUID
  content: string              // Original full content, stored but not iterated
  content_type: string
  topic: string
  author: string
  references: UUID[]
  revision_of: UUID?
  causal_position: { sequence: int, ... }
  integration_cost: { ... }
  created: datetime

  // New fields
  claims: Claim[]              // Fixed-size claim representation (max N, e.g., 12)
  claims_status: enum          // pending | distilled | verified
  fragment_of: UUID?           // If this entry is a fragment of a larger artifact
  fragment_index: int?         // Position in fragment chain (0-based)
}
```

### Claim

```
Claim {
  text: string                 // Short declarative statement, 1-2 sentences max
  confidence: float            // 0.0-1.0, how central this claim is to the entry
}
```

### Artifact (virtual, represented by entries)

When content is too large for a single entry, it becomes an artifact composed of fragments:

```
Artifact structure:
  artifact_entry (claims: top N claims summarizing the whole artifact)
    ├── fragment_0 (claims: top N claims for this fragment)
    ├── fragment_1 (claims: top N claims for this fragment)
    ├── fragment_2 (claims: top N claims for this fragment)
    └── ...

Each fragment references the artifact entry.
The artifact entry's claims summarize across all fragments.
```

## Fragmentation Rules

### When to fragment

An entry should be fragmented when its content exceeds a size threshold. Suggested threshold: ~4,000 tokens (roughly 3,000 words). This keeps each fragment within comfortable range for a cheap LLM to distill.

### How to fragment

1. Split content at natural boundaries (headings, paragraphs, sections)
2. Write each fragment as a separate entry with `fragment_of` pointing to the artifact entry
3. Each fragment gets its own claims (distilled by a robot worker)
4. The artifact entry gets claims that synthesize across all fragment claims

### Fragment chain

Fragments are ordered by `fragment_index`. Each fragment's `references` array includes the artifact entry ID. This makes the chain traversable in both directions:
- From artifact → fragments: filtered browse for `fragment_of = artifact_id`
- From fragment → artifact: read `fragment_of` field
- Fragment ordering: sort by `fragment_index`

## Claim Distillation

### Input
- Entry content (original text)
- Context: parent artifact claims (if this is a fragment), or topic index claims (if available)

### Output
- Array of N claims, ordered by confidence (most central first)

### Quality criteria for claims
- **Self-contained:** Each claim is understandable without reading the full content
- **Declarative:** Statements of fact, not questions or imperatives
- **Specific:** "OAuth tokens are validated before each rclone job starts" not "Authentication is handled"
- **Non-redundant:** Claims within a set should cover different aspects
- **Ordered by centrality:** First claim is the most important takeaway

### Claim count (N)

N = 12 is the starting point. This is configurable per notebook. The key constraint: N should be small enough that comparing two claim-sets (N × N = 144 comparisons) is tractable in a single cheap LLM call.

Trade-offs of different N values:
- **N = 6:** Very aggressive compression. Fast comparison. Risk of losing important nuance.
- **N = 12:** Balanced. 144 pairwise comparisons per pair of entries. Good for most content.
- **N = 20:** High fidelity. 400 pairwise comparisons. May need to split comparison into batches.

## Claim Lifecycle

```
Content uploaded → claims_status = "pending"
                        │
                        ▼
           Robot picks up distillation job
                        │
                        ▼
            Claims written back to entry
            claims_status = "distilled"
                        │
                        ▼
           (Optional) Agent reviews claims
            for high-friction entries
            claims_status = "verified"
```

## Implementation Notes

### Storage

Claims should be stored as a structured field on the entry, not as separate entities. They're always read and written together with the entry. This keeps the claim-set atomic — you never have a partially updated claim-set.

### Backward compatibility

Existing entries (v1) have no claims. They should appear as `claims_status: "pending"` and get queued for distillation when robots are available. The server should handle entries with and without claims gracefully — claimless entries are simply not available for entropy/friction comparison.

### Immutability

Claims follow the same immutability model as content. If claims need to change (e.g., after re-distillation with better context), a new revision of the entry is created. The original claims are preserved in the revision history.
