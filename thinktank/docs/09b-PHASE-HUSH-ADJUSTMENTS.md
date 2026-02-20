# 09b — Phase Hush: Proposed Plan Adjustments

**Based on:** 09a-PHASE-HUSH-REVIEW.md
**Date:** 2026-02-20
**Purpose:** Concrete changes to incorporate into 09-PHASE-HUSH.md

---

## Adjustment 1: Add test file lists to every sub-phase

**Applies to:** All sub-phases
**Reason:** Security subsystem with no testing strategy specified

Add a **Tests** section to each sub-phase alongside the existing **Files** table. Each test file targets specific denial paths.

### Hush-1 — Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Middleware/NotebookAccessMiddlewareTests.cs` | **New** — ACL enforcement: unauthenticated → 401, no ACL → 404, read-only → 403 on write, scope mismatch → 403 |
| `Notebook.Server.Tests/Endpoints/ShareEndpointsTests.cs` | **New** — non-owner grant → 403, non-owner revoke → 403, happy path grant/revoke |
| `Notebook.Server.Tests/Endpoints/BatchEndpointsTests.cs` | **New** — quota exceeded → 429, write without ACL → 404 |
| `ThinkerAgent.Tests/AuthenticationTests.cs` | **New** — unauthenticated /quit → 401, unauthenticated /config → 401 |

### Hush-2 — Tests

| File | Covers |
|---|---|
| `Notebook.Data.Tests/Repositories/OrganizationRepositoryTests.cs` | **New** — CRUD orgs/groups, DAG edge insert, cycle detection rejection |
| `Notebook.Server.Tests/Endpoints/OrganizationEndpointsTests.cs` | **New** — non-admin create → 403, membership operations |

### Hush-3 — Tests

| File | Covers |
|---|---|
| `Notebook.Core.Tests/Security/SecurityLabelTests.cs` | **New** — dominance logic: equal, higher, lower, compartment subset/superset, disjoint |
| `Notebook.Server.Tests/Middleware/ClearanceCheckTests.cs` | **New** — insufficient clearance → 404, sufficient clearance → pass-through |

### Hush-4 — Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Services/AccessResolverTests.cs` | **New** — direct grant vs group inheritance, tier precedence, existence concealment |
| `Notebook.Data.Tests/Migrations/TierMigrationTests.cs` | **New** — backfill correctness: (true,true)→read_write, (true,false)→read, (false,false)→existence |

### Hush-5 — Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Endpoints/AgentEndpointsTests.cs` | **New** — register, label update, deregister |
| `Notebook.Data.Tests/Repositories/JobRepositoryTests.cs` | **Add** — agent clearance filtering: agent below notebook level → no jobs returned |

### Hush-6 — Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Services/SubscriptionServiceTests.cs` | **New** — validation: self-sub rejected, classification violation rejected, cycle rejected |
| `Notebook.Server.Tests/Services/SubscriptionSyncServiceTests.cs` | **New** — watermark advancement, tombstoning, error → status update |
| `Notebook.Data.Tests/Repositories/EntryRepositoryTests.cs` | **Add** — FindNearestWithMirrored returns mirrored results, respects tombstone filter |
| `Notebook.Server.Tests/Services/JobResultProcessorTests.cs` | **Add** — cross-boundary COMPARE_CLAIMS applies discount factor |

### Hush-7 — Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Endpoints/ReviewEndpointsTests.cs` | **New** — non-admin review → 403, approve queues DISTILL_CLAIMS, reject returns no reason |
| `Notebook.Server.Tests/Endpoints/BatchEndpointsTests.cs` | **Add** — non-member write → pending review_status, pending entry excluded from browse |

### Hush-8 — Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Services/AuditServiceTests.cs` | **New** — event queued on write, queue-full behavior matches chosen policy, API filter/pagination |

---

## Adjustment 2: Specify cycle detection algorithm for Hush-2

**Applies to:** Hush-2, section 2.3 (Repository Layer)

Replace the line "Cycle detection on edge insert" with:

> **Cycle detection:** On `group_edges` insert, execute a recursive CTE from the proposed child walking parent edges. If the proposed parent is reachable, reject the insert.
>
> ```sql
> WITH RECURSIVE ancestors AS (
>     SELECT parent_id FROM group_edges WHERE child_id = @proposed_parent_id
>     UNION
>     SELECT ge.parent_id FROM group_edges ge
>     JOIN ancestors a ON ge.child_id = a.parent_id
> )
> SELECT EXISTS (SELECT 1 FROM ancestors WHERE parent_id = @proposed_child_id);
> ```
>
> This runs in the same transaction as the insert. At the expected scale (< 1000 groups per org), this is sufficient. If group counts grow beyond 10k, consider a materialized transitive closure table with trigger-based maintenance.

---

## Adjustment 3: Add clearance caching to Hush-3

**Applies to:** Hush-3, section 3.3 (Clearance Check Integration)

Add after the middleware description:

> ### 3.3a Clearance Cache
>
> To avoid a DB query per request, cache clearance lookups in `IMemoryCache` keyed by `(author_id, organization_id)` with a 30-second sliding expiration.
>
> **Invalidation:** When `POST /clearances` or `DELETE /clearances` modifies a principal's clearance, evict the cache entry for that `(author_id, organization_id)` pair. Since the server is single-process, in-memory eviction is sufficient. Multi-instance deployments would need a pub/sub invalidation channel (Redis, PostgreSQL NOTIFY).
>
> **Staleness contract:** A revoked clearance may still be honored for up to 30 seconds. Document this in the security model as an accepted trade-off. For immediate revocation (e.g., incident response), add a `POST /admin/cache/flush` endpoint that clears all cached clearances.
>
> **Files:**
>
> | File | Change |
> |---|---|
> | `Notebook.Server/Services/ClearanceCacheService.cs` | **New** — IMemoryCache wrapper with eviction |
> | `Notebook.Server/Middleware/NotebookAccessMiddleware.cs` | Inject ClearanceCacheService instead of direct DB query |

---

## Adjustment 4: Split Hush-4 migration into two steps

**Applies to:** Hush-4, section 4.1 (Extend `notebook_access`)

Replace the single migration `015_access_tiers.sql` with:

> **Migration: `015a_add_access_tiers.sql`**
>
> ```sql
> ALTER TABLE notebook_access ADD COLUMN tier TEXT NOT NULL DEFAULT 'read_write'
>     CHECK (tier IN ('existence', 'read', 'read_write', 'admin'));
>
> -- Backfill from existing booleans
> UPDATE notebook_access SET tier = CASE
>     WHEN read AND write THEN 'read_write'
>     WHEN read AND NOT write THEN 'read'
>     ELSE 'existence'
> END;
> ```
>
> Deploy application code that reads `tier` column. Verify in production. Then:
>
> **Migration: `015b_drop_legacy_acl_booleans.sql`**
>
> ```sql
> ALTER TABLE notebook_access DROP COLUMN read;
> ALTER TABLE notebook_access DROP COLUMN write;
> ```
>
> This enables rollback: if the application has issues after 015a, revert application code while the old columns still exist.

---

## Adjustment 5: Replace timer-per-subscription with polling loop in Hush-6

**Applies to:** Hush-6, section 6.3 (Subscription Sync)

Replace the timer-per-subscription model with:

> ### 6.3 Subscription Sync (Revised)
>
> **Create:** `Notebook.Server/Services/SubscriptionSyncService.cs` (BackgroundService)
>
> A single polling loop replaces per-subscription timers.
>
> **Loop (every 5 seconds):**
> 1. Query subscriptions due for sync:
>    ```sql
>    SELECT * FROM notebook_subscriptions
>    WHERE sync_status != 'suspended'
>      AND (last_sync_at IS NULL
>           OR last_sync_at + (poll_interval_s * INTERVAL '1 second') < now())
>    ORDER BY last_sync_at ASC NULLS FIRST
>    LIMIT @max_concurrent - @currently_syncing;
>    ```
> 2. Dispatch each to a bounded `SemaphoreSlim` worker pool (default max concurrency: 10, configurable via `SubscriptionSync:MaxConcurrency`)
> 3. Each worker executes the sync loop from the original plan (steps 1–6)
>
> **Error backoff:** On consecutive errors for the same subscription, multiply `poll_interval_s` by 2^(error_count), capped at 1 hour. Reset on successful sync.
>
> **Advantages over timer-per-subscription:**
> - Bounded resource usage regardless of subscription count
> - No thundering herd — staggered by `last_sync_at` ordering
> - Single query to find due work vs N timers

---

## Adjustment 6: Define audit event guarantee for Hush-8

**Applies to:** Hush-8, section 8.3 (Implementation)

Replace "fire-and-forget with bounded queue" with:

> ### 8.3 Implementation (Revised)
>
> **Create:** `Notebook.Server/Services/IAuditService.cs`, `AuditService.cs`
>
> **Write strategy: back-pressure with overflow**
>
> 1. `AuditService` maintains a `Channel<AuditEvent>` (bounded, capacity: 10,000)
> 2. API endpoints call `AuditService.LogAsync(event)` which writes to the channel. If the channel is full, the call blocks (back-pressure) — this slows the API rather than dropping events
> 3. A background consumer reads from the channel and batch-inserts into `audit_log` (batch size: 100, flush interval: 1 second, whichever comes first)
> 4. If the batch insert fails (DB down), events are serialized to a local append-only file (`audit-overflow-{date}.jsonl`). A recovery task replays overflow files on startup
> 5. Emit a metric (`audit_queue_depth`) for monitoring. Alert at 80% capacity
>
> **Guarantee:** No audit event is silently dropped. Under sustained DB failure, the overflow file grows — operators must be alerted to restore DB connectivity.
>
> **Files:**
>
> | File | Change |
> |---|---|
> | `Notebook.Server/Services/IAuditService.cs` | **New** — interface |
> | `Notebook.Server/Services/AuditService.cs` | **New** — Channel + batch writer + overflow |
> | `Notebook.Server/Services/AuditRecoveryService.cs` | **New** — replays overflow files on startup |

---

## Summary of file changes to 09-PHASE-HUSH.md

| Section | Change |
|---|---|
| All sub-phases | Add **Tests** section with test file table |
| Hush-2 §2.3 | Replace "cycle detection on edge insert" with recursive CTE specification |
| Hush-3 | Add §3.3a Clearance Cache (new service, invalidation strategy, staleness contract) |
| Hush-4 §4.1 | Split `015_access_tiers.sql` into `015a` (add + backfill) and `015b` (drop old columns) |
| Hush-6 §6.3 | Replace timer-per-subscription with single polling loop + bounded worker pool |
| Hush-8 §8.3 | Replace fire-and-forget with back-pressure + overflow guarantee |
| **New files total** | +3 (ClearanceCacheService, AuditRecoveryService, test files across all phases) |
