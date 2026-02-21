# 09a — Phase Hush: Review Notes

**Reviewer:** Claude
**Date:** 2026-02-20
**Status:** Review of 09-PHASE-HUSH.md

## Overall Assessment

The plan is thorough and well-structured. The dependency chain across 8 sub-phases is sound, the current-state gap table is honest, and security decisions (existence concealment, no rejection reasons, label dominance) are correct. The migration path is backward-compatible with sensible defaults.

## Considerations

### 1. Hush-1: Integration tests for access denial

The plan says "no schema changes needed" but share endpoints are new code with real attack surface. Add integration tests that verify 403/404 behavior before moving to Hush-2. Specifically:

- Test that unauthenticated requests are rejected
- Test that a valid token without ACL entry gets 404 (not 403)
- Test that read-only ACL prevents writes
- Test that non-owners cannot share/revoke
- Test that ThinkerAgent management endpoints reject unauthenticated requests

### 2. Hush-2: Group DAG cycle detection algorithm

Line 185 mentions "cycle detection on edge insert" but doesn't specify the algorithm. A simple BFS/DFS on insert is fine at small scale. At larger scale, consider:

- A recursive CTE in PostgreSQL (`WITH RECURSIVE`) to check reachability before insert
- A transitive closure table if read-heavy traversal becomes a bottleneck
- Document the expected group count ceiling to justify the choice

### 3. Hush-3: Clearance caching and invalidation

The middleware checks clearance on every request. Combined with the per-request ACL cache from Hush-1, this means at least one DB query per request to `principal_clearances`. Consider:

- An in-memory cache with short TTL (e.g., 30–60 seconds)
- Explicit invalidation when clearances are granted/revoked (publish event from the grant/revoke endpoint)
- Document the staleness window and whether it's acceptable that a revoked clearance could still be honored for up to N seconds

### 4. Hush-4: Two-step tier migration

Dropping `read`/`write` columns and replacing with `tier` (lines 316–322) is a breaking schema change. Recommend splitting into two migrations:

1. **015a:** Add `tier` column with default, backfill from `(read, write)` booleans
2. **015b:** Drop `read`/`write` columns

This allows rollback to 015a if the application layer has issues with the new column. It also lets you deploy the schema change and application change independently.

### 5. Hush-6: Subscription sync scaling

"Timer-per-subscription" (line 539) could become problematic with many subscriptions. Each timer holds resources and can create thundering-herd effects. Consider:

- A single polling loop that queries for subscriptions due for sync (`WHERE last_sync_at + poll_interval_s < now()`) and processes them in batches
- A configurable concurrency cap (e.g., max 10 concurrent syncs)
- Backoff on repeated errors to avoid hammering failing sources

### 6. Hush-8: Audit event drop policy

"Fire-and-forget with bounded queue" (line 766) means audit entries can be silently dropped under load. For a security audit trail, this may not be acceptable. Options:

- **Back-pressure:** Block the API response until the audit entry is queued (not written). Queue should be large enough that this never blocks in practice, but if it does, the system slows rather than loses events.
- **Overflow log:** If the queue is full, write to a local append-only file as a fallback. Process the overflow file on recovery.
- **At minimum:** Emit a metric/alert when the queue approaches capacity so operators know audit completeness is at risk.

Make an explicit design decision and document it.

### 7. Cross-cutting: Testing strategy

Each sub-phase lists files to create/modify but no test files. Given this is a security subsystem, testing is critical. Recommend adding to each sub-phase:

- **Unit tests** for pure logic (label dominance, tier comparison, cycle detection)
- **Integration tests** for access denial paths (middleware + DB)
- **Negative tests** for every authorization check (verify the system rejects what it should)
- A test matrix mapping each endpoint to the access scenarios that must be covered
