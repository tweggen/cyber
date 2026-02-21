# 03 — Server Enhancements

## Scope

The existing notebook server exposes six MCP operations: WRITE, REVISE, READ, BROWSE, OBSERVE, SHARE. All continue to work unchanged. This document specifies new capabilities the server needs to support bulk ingest, cheap comparison, and robot workers.

## Enhancement 1: Batch Write API

### Purpose
Write many entries in a single call without routing through an LLM.

### Interface

```
POST /notebooks/{id}/batch

Request:
{
  "entries": [
    {
      "content": "string",
      "topic": "string",
      "content_type": "text/plain",     // optional, default text/plain
      "references": ["uuid", ...],       // optional
      "fragment_of": "uuid",             // optional, for artifact fragments
      "fragment_index": 0                // optional, ordering within artifact
    },
    ...
  ],
  "author": "string"
}

Response:
{
  "results": [
    {
      "entry_id": "uuid",
      "causal_position": { "sequence": 42 },
      "integration_cost": { ... },
      "claims_status": "pending"
    },
    ...
  ],
  "jobs_created": 15    // Number of distillation jobs queued
}
```

### Behavior
- Each entry gets a sequential causal position (in order of the array)
- Integration cost is computed per entry as usual
- Every entry is created with `claims_status: "pending"`
- A distillation job is automatically queued for each entry
- Batch size limit: 100 entries per call (caller loops for larger batches)

### Also expose as MCP tool
`notebook_batch_write(entries: [...], author: string)` — for Claude instances doing bulk work within a conversation.

## Enhancement 2: Claim Storage

### Purpose
Store and retrieve the fixed-size claim representation alongside entry content.

### Schema additions to Entry

```
claims: [
  { "text": "string", "confidence": 0.95 },
  ...
]
claims_status: "pending" | "distilled" | "verified"
```

### API changes

**READ** — returns claims and claims_status alongside existing fields.

**New: UPDATE_CLAIMS** — write claims to an existing entry. Used by robot workers.

```
POST /notebooks/{id}/entries/{entry_id}/claims

Request:
{
  "claims": [
    { "text": "OAuth tokens are validated before each rclone job", "confidence": 0.95 },
    { "text": "Validation uses the provider's token refresh endpoint", "confidence": 0.82 },
    ...
  ],
  "author": "robot-haiku-1"
}

Response:
{
  "entry_id": "uuid",
  "claims_status": "distilled",
  "comparison_jobs_created": 3   // Jobs to compare against relevant topic indices
}
```

### Behavior
- Writing claims transitions `claims_status` from `pending` to `distilled`
- Server automatically creates comparison jobs for the new claims against relevant topic indices
- Claims are immutable once written. Re-distillation creates a revision.

**BROWSE** — add `claims_status` to catalog entries so callers can filter for entries needing distillation.

## Enhancement 3: Filtered Browse

### Purpose
Navigate the notebook without loading the entire catalog.

### Additional parameters on BROWSE

```
notebook_browse(
  query?: string,                // existing — keyword search on topics
  topic_prefix?: string,         // NEW — e.g., "index/topic/" or "confluence/eng/"
  claims_status?: string,        // NEW — filter by "pending", "distilled", "verified"
  author?: string,               // NEW — filter by author
  sequence_min?: int,            // NEW — entries from sequence N onward
  sequence_max?: int,            // NEW — entries up to sequence N
  fragment_of?: UUID,            // NEW — fragments of a specific artifact
  has_friction_above?: float,    // NEW — entries with friction score above threshold
  limit?: int,                   // NEW — max results (default: 50)
  offset?: int                   // NEW — pagination
)
```

### Behavior
- Filters are AND-combined
- Returns the same catalog format as existing browse, with added claim/friction fields
- Results ordered by sequence (descending) unless otherwise specified

## Enhancement 4: Full-Text Search

### Purpose
Find entries by content without reading them one by one.

### Interface

```
notebook_search(
  query: string,                 // search terms
  search_in: "content" | "claims" | "both",  // what to search, default "both"
  topic_prefix?: string,         // scope search to a topic subtree
  max_results?: int              // default 20
)

Response:
{
  "results": [
    {
      "entry_id": "uuid",
      "topic": "string",
      "snippet": "...matched text with context...",
      "match_location": "content" | "claims",
      "relevance_score": 0.85
    },
    ...
  ]
}
```

### Implementation options (in order of complexity)

1. **Substring / keyword match** — simplest, works for exact terms. Adequate for v2.0.
2. **BM25 / TF-IDF** — standard information retrieval. Better ranking. Moderate implementation effort.
3. **Embedding-based semantic search** — best recall for fuzzy queries. Requires embedding storage. Consider for v2.1+.

Recommendation: start with option 1 or 2. The claim representation already provides the semantic layer — full-text search just needs to find the right entries to compare claims against.

## Enhancement 5: Job Queue

### Purpose
The server manages a queue of work items for robot workers. Robots pull jobs, execute them against a cheap LLM, and push results back.

### Job types

```
JobType:
  DISTILL_CLAIMS    — Extract claims from entry content
  COMPARE_CLAIMS    — Compare two claim-sets, produce entropy/friction
  CLASSIFY_TOPIC    — Assign entry to a topic cluster
```

### Job data model

```
Job {
  id: UUID
  type: JobType
  status: "pending" | "in_progress" | "completed" | "failed"
  created: datetime
  claimed_at: datetime?          // When a robot picked it up
  claimed_by: string?            // Robot identifier
  timeout_seconds: int           // Auto-return to pending if not completed

  // Type-specific payload
  payload: {
    // For DISTILL_CLAIMS:
    entry_id: UUID
    content: string              // The content to distill
    context_claims: Claim[]?     // Parent artifact claims, if available

    // For COMPARE_CLAIMS:
    entry_id: UUID
    compare_against_id: UUID
    claims_a: Claim[]
    claims_b: Claim[]

    // For CLASSIFY_TOPIC:
    entry_id: UUID
    claims: Claim[]
    available_topics: string[]   // Existing topic names to choose from
  }

  // Result (filled by robot)
  result: object?                // Type-specific result data
}
```

### Job queue API

```
# Robot pulls next available job
GET /notebooks/{id}/jobs/next?type=DISTILL_CLAIMS&worker_id=robot-1

Response: Job (with status changed to "in_progress")

# Robot pushes result
POST /notebooks/{id}/jobs/{job_id}/complete

Request:
{
  "worker_id": "robot-1",
  "result": { ... }             // Type-specific result
}

# Robot reports failure
POST /notebooks/{id}/jobs/{job_id}/fail

Request:
{
  "worker_id": "robot-1",
  "error": "string"
}
```

### Automatic job creation

The server creates jobs automatically:
- **On entry write (single or batch):** Create DISTILL_CLAIMS job
- **On claims written:** Create COMPARE_CLAIMS job(s) against relevant topic indices
- **On high entropy detected:** Create CLASSIFY_TOPIC job if entry doesn't have a clear topic

### Job queue properties
- Jobs have a timeout. If a robot claims a job but doesn't complete it within the timeout, the job returns to pending.
- Multiple robots can work in parallel — they each claim different jobs.
- Jobs are processed in FIFO order within each type.
- The server exposes queue depth and processing stats for observability.

## Enhancement 6: Entropy/Friction Storage

### Purpose
Store comparison results as first-class data on entries.

### Schema additions

```
Entry (additional fields):
  comparisons: [
    {
      compared_against: UUID,    // The entry this was compared to
      entropy: float,            // 0.0 - 1.0
      friction: float,           // 0.0 - 1.0
      contradictions: [          // Detail for friction > 0
        { claim_a: string, claim_b: string, severity: float }
      ],
      computed_at: datetime,
      computed_by: string        // Robot identifier
    }
  ]
  max_friction: float            // Highest friction score across all comparisons
  needs_review: boolean          // True if max_friction > threshold
```

### Behavior
- Comparison results are appended (an entry can be compared against multiple indices)
- `max_friction` and `needs_review` are computed server-side when comparisons are added
- `needs_review` threshold is configurable per notebook (default: 0.2)
- BROWSE includes `max_friction` and `needs_review` in catalog entries

## Summary of New API Surface

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/notebooks/{id}/batch` | POST | Batch write entries |
| `/notebooks/{id}/entries/{eid}/claims` | POST | Write claims to entry |
| `/notebooks/{id}/search` | GET | Full-text search |
| `/notebooks/{id}/jobs/next` | GET | Robot pulls next job |
| `/notebooks/{id}/jobs/{jid}/complete` | POST | Robot submits result |
| `/notebooks/{id}/jobs/{jid}/fail` | POST | Robot reports failure |
| BROWSE (enhanced) | - | Additional filter parameters |
| READ (enhanced) | - | Returns claims, comparisons |

All existing MCP operations remain unchanged. New operations are also exposed as MCP tools for Claude instances that want to use them.
