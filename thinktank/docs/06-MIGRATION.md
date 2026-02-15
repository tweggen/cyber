# 06 — Migration from v1 to v2

## Principle: Additive Evolution

v2 does not break v1. All existing MCP operations continue to work. Existing entries remain valid. The migration is additive — new capabilities are layered on top of existing ones.

## Current State (v1)

- ~40 entries, all written via MCP through LLM instances
- Six operations: WRITE, REVISE, READ, BROWSE, OBSERVE, SHARE
- Topic indices built by Claude Code instance (entries with `index/topic/*` topics)
- Entrypoint entry for instance onboarding
- No claims, no entropy/friction scores, no job queue, no batch API
- Storage backend: unknown (needs investigation — flat file? SQLite? Postgres?)

## Migration Steps

### Step 1: Schema Extension

Add new fields to the entry data model with safe defaults:

```
claims: []                    # Empty array — no claims yet
claims_status: "pending"      # All existing entries need distillation
fragment_of: null             # No existing entries are fragments
fragment_index: null
comparisons: []               # No comparisons yet
max_friction: null
needs_review: false
```

All existing entries appear as `claims_status: "pending"`. This is accurate — they don't have claims yet.

### Step 2: Batch Write API

Implement the batch write endpoint. This is independent of claims — it just writes entries faster. It can be tested immediately with simple content.

**Test:** Write 100 test entries via batch API. Verify they get sequential causal positions and integration costs.

### Step 3: Claim Storage and UPDATE_CLAIMS Endpoint

Add the claims storage and the endpoint for writing claims to an entry. At this point, claims can be written manually (e.g., by an LLM via MCP) or programmatically.

**Test:** Manually distill claims for 3-5 existing entries. Write them via UPDATE_CLAIMS. Verify READ returns them.

### Step 4: Job Queue

Implement the job queue infrastructure. Initially with just DISTILL_CLAIMS job type.

**Test:** Write a new entry. Verify a DISTILL_CLAIMS job is automatically created. Pull it via the job API. Complete it. Verify claims are stored on the entry.

### Step 5: First Robot Worker

Implement the basic robot script that pulls DISTILL_CLAIMS jobs and processes them against Haiku.

**Test:** Run the robot against the ~40 existing entries. All should get claims. Review claim quality manually for a sample.

### Step 6: Comparison Infrastructure

Add COMPARE_CLAIMS job type. Implement the comparison result storage. Wire up the automatic job creation when claims are written.

**Test:** After distillation, verify comparison jobs are created. Run robot for comparison jobs. Check entropy/friction scores make sense.

### Step 7: Filtered Browse and Search

Add filter parameters to BROWSE. Implement basic full-text search.

**Test:** Filter by topic prefix, claims_status, friction threshold. Search for known terms. Verify results.

### Step 8: Full Pipeline Test

Run the complete ingest pipeline against a small Confluence export (100-500 pages). Verify:
- Extract produces clean text
- Batch upload works at scale
- Robots distill and compare without errors
- Entropy/friction scores are reasonable
- High-friction entries are flagged
- Topic indices can be browsed efficiently

### Step 9: Production Ingest

Run against the full 14,000 page Confluence dump.

## Backward Compatibility Checklist

| Existing behavior | v2 impact | Action needed |
|-------------------|-----------|---------------|
| WRITE via MCP | Still works, now also creates distillation job | None |
| REVISE via MCP | Still works | None |
| READ via MCP | Now returns additional fields (claims, comparisons) | Ensure MCP clients handle new fields gracefully |
| BROWSE via MCP | Now returns additional fields. New filter params are optional. | None — existing calls work unchanged |
| OBSERVE via MCP | Still works | None |
| SHARE via MCP | Still works | None |
| Existing entries | Appear as claims_status: "pending" | Run robot to backfill claims |
| Topic index entries | Still work as entries | Claims can be distilled from them too |
| Entrypoint entry | Still works | Should be revised to document new capabilities |

## Risks

1. **Storage backend performance.** If the current backend is file-based, 14,000 entries may be slow. Need to investigate and potentially migrate to SQLite or Postgres before bulk ingest.

2. **Integration cost computation at scale.** If integration cost is O(N) per write (comparing against all existing entries), batch writes of 14,000 entries could be slow. May need to optimize or batch the integration cost computation.

3. **Claim quality from Haiku.** Haiku is fast and cheap but may produce lower-quality claims than Sonnet. Quality should be validated on a sample before committing to full ingest. If quality is insufficient, Sonnet is still 10x cheaper than Opus.

4. **Job queue reliability.** If the server restarts, in-progress jobs should not be lost. Jobs need to be persisted (not just in-memory).

## Timeline Estimate

| Step | Effort | Dependencies |
|------|--------|--------------|
| Schema extension | Small | None |
| Batch write API | Medium | Schema extension |
| Claim storage + endpoint | Medium | Schema extension |
| Job queue | Medium-Large | Claim storage |
| First robot worker | Small | Job queue |
| Comparison infrastructure | Medium | Job queue, claims |
| Filtered browse + search | Medium | Schema extension |
| Pipeline test (100 pages) | Small | All above |
| Production ingest (14K pages) | Small (mostly waiting) | Pipeline test |

Total: this is a medium-sized project. The server enhancements are the bulk of the work. The robot worker and ingest scripts are comparatively simple.
