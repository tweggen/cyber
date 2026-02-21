# 12 — Subscription Architecture

Technical architecture for cross-classification thinktank interaction.
Companion to [11-CLASSIFIED-INTERACTION-CONCEPT.md](11-CLASSIFIED-INTERACTION-CONCEPT.md);
replaces the sketch in [09-PHASE-HUSH.md](09-PHASE-HUSH.md) Hush-6 with
implementation-grade detail.

---

## 1. Extended Subscription Schema

Hush-6 defines the base `notebook_subscriptions` table (migration 017).
The extended schema adds operational fields required for real sync:

```sql
-- migration: 017_subscriptions.sql

CREATE TABLE notebook_subscriptions (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    subscriber_id     UUID NOT NULL REFERENCES notebooks(id),
    source_id         UUID NOT NULL REFERENCES notebooks(id),
    scope             TEXT NOT NULL DEFAULT 'catalog'
                          CHECK (scope IN ('catalog', 'claims', 'entries')),
    topic_filter      TEXT,                          -- optional topic prefix filter
    approved_by       BYTEA NOT NULL REFERENCES authors(id),

    -- Sync state
    sync_watermark    BIGINT NOT NULL DEFAULT 0,     -- last synced source sequence
    last_sync_at      TIMESTAMPTZ,                   -- wall-clock of last success
    sync_status       TEXT NOT NULL DEFAULT 'idle'
                          CHECK (sync_status IN ('idle', 'syncing', 'error', 'suspended')),
    sync_error        TEXT,                           -- last error message
    mirrored_count    INTEGER NOT NULL DEFAULT 0,     -- total mirrored items

    -- Tuning
    discount_factor   DOUBLE PRECISION NOT NULL DEFAULT 0.3
                          CHECK (discount_factor > 0 AND discount_factor <= 1.0),
    poll_interval_s   INTEGER NOT NULL DEFAULT 60
                          CHECK (poll_interval_s >= 10),
    embedding_model   TEXT,                           -- subscriber's embedding model id

    created           TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (subscriber_id, source_id)
);

CREATE INDEX idx_subscriptions_subscriber ON notebook_subscriptions(subscriber_id);
CREATE INDEX idx_subscriptions_source     ON notebook_subscriptions(source_id);
```

**Changes from Hush-6 sketch:**
- Added surrogate `id` (UUID PK) — simplifies FK references from `mirrored_claims`.
- Moved composite `(subscriber_id, source_id)` to a UNIQUE constraint.
- Added `sync_watermark`, `last_sync_at`, `sync_status`, `sync_error`,
  `mirrored_count`, `discount_factor`, `poll_interval_s`, `embedding_model`.
- Added `suspended` to `sync_status` for classification-change handling (see §8).

---

## 2. Mirrored Content Storage

### 2.1 `mirrored_claims`

Stores claims pulled from source thinktanks into the subscriber's database:

```sql
CREATE TABLE mirrored_claims (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    subscription_id   UUID NOT NULL REFERENCES notebook_subscriptions(id)
                          ON DELETE CASCADE,
    source_entry_id   UUID NOT NULL,            -- entry ID in the SOURCE notebook
    notebook_id       UUID NOT NULL              -- the SUBSCRIBER's notebook ID
                          REFERENCES notebooks(id),
    claims            JSONB NOT NULL,            -- claim list from source
    topic             TEXT,
    embedding         DOUBLE PRECISION[],        -- re-embedded in subscriber's model
    source_sequence   BIGINT NOT NULL,
    tombstoned        BOOLEAN NOT NULL DEFAULT false,
    mirrored_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (subscription_id, source_entry_id)
);

CREATE INDEX idx_mirrored_claims_notebook
    ON mirrored_claims(notebook_id)
    WHERE embedding IS NOT NULL AND NOT tombstoned;

CREATE INDEX idx_mirrored_claims_subscription
    ON mirrored_claims(subscription_id, source_sequence);
```

### 2.2 `mirrored_entries`

For `entries`-scope subscriptions. Stores full content with provenance:

```sql
CREATE TABLE mirrored_entries (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    subscription_id   UUID NOT NULL REFERENCES notebook_subscriptions(id)
                          ON DELETE CASCADE,
    source_entry_id   UUID NOT NULL,
    notebook_id       UUID NOT NULL REFERENCES notebooks(id),
    content           BYTEA NOT NULL,
    content_type      TEXT NOT NULL,
    topic             TEXT,
    source_sequence   BIGINT NOT NULL,
    tombstoned        BOOLEAN NOT NULL DEFAULT false,
    mirrored_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (subscription_id, source_entry_id)
);
```

### 2.3 Design Rationale

Mirrored content is stored in separate tables — never inserted into `entries` —
because:

- **Lifecycle**: mirrored content has no local `claims_status` progression, no
  local job chain (DISTILL → EMBED → COMPARE). Claims arrive pre-distilled.
- **Provenance**: mirrored rows carry `subscription_id` and `source_entry_id`,
  which have no meaning in the `entries` schema.
- **Deletion semantics**: tombstoning rather than deletion preserves audit trail
  without polluting the local entry lifecycle.
- **Query separation**: browse/observe on local entries remains unaffected;
  mirrored content is opt-in via explicit query parameters or UNION joins.

---

## 3. Sync Protocol

### 3.1 `SubscriptionSyncService`

A .NET `BackgroundService` that manages a sync loop per active subscription.

```
┌─────────────────────────────────────────────────────┐
│              SubscriptionSyncService                │
│                                                     │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐            │
│  │ Sub A   │  │ Sub B   │  │ Sub C   │  ...        │
│  │ timer   │  │ timer   │  │ timer   │             │
│  └────┬────┘  └────┬────┘  └────┬────┘            │
│       │            │            │                   │
│       ▼            ▼            ▼                   │
│     SyncOne()   SyncOne()   SyncOne()               │
└─────────────────────────────────────────────────────┘
```

On startup the service loads all subscriptions with `sync_status != 'suspended'`
and schedules a `Timer` per subscription at its `poll_interval_s`. Subscriptions
created or deleted at runtime are picked up via a notification channel or
periodic refresh (every 5 minutes).

### 3.2 Sync Loop (per subscription)

```
SyncOne(subscription):
  1. SET sync_status = 'syncing'
  2. GET /notebooks/{source_id}/observe?since={sync_watermark}
     - Auth: read-scoped JWT for source notebook (stored externally,
       looked up by subscription_id)
     - Timeout: 30s
  3. For each entry in response (up to batch_size=100):
     a. If scope = 'catalog':
        - Store topic + integration_cost metadata only
          (lightweight row in mirrored_claims with claims='[]')
     b. If scope = 'claims':
        - GET /notebooks/{source_id}/entries/{entry_id}
        - Extract claims, topic
        - UPSERT into mirrored_claims
     c. If scope = 'entries':
        - GET /notebooks/{source_id}/entries/{entry_id}
        - UPSERT into mirrored_entries (full content)
        - UPSERT into mirrored_claims (claims)
  4. Queue EMBED_MIRRORED jobs for new/updated mirrored_claims
     with non-empty claims
  5. UPDATE notebook_subscriptions SET
       sync_watermark = max(processed sequences),
       last_sync_at   = now(),
       mirrored_count = (SELECT count(*) FROM mirrored_claims
                         WHERE subscription_id = @id AND NOT tombstoned),
       sync_status    = 'idle'
  6. On error: SET sync_status = 'error', sync_error = message
```

### 3.3 Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Source auth** | Subscriber holds a read-scoped JWT for the source notebook | Subscriber initiates all requests — source never calls back. JWT stored in a separate credentials table or vault, never in `notebook_subscriptions`. |
| **Polling interval** | Configurable per subscription, default 60s, minimum 10s | Long-running WebSocket connections add complexity; polling with watermarks is simple, idempotent, and sufficient for non-real-time intelligence sync. |
| **Batch size** | 100 entries per sync cycle | Bounds memory and duration per cycle. If >100 entries are pending, the next cycle picks up the remainder. |
| **Idempotency** | `UNIQUE (subscription_id, source_entry_id)` + UPSERT (`ON CONFLICT ... DO UPDATE`) | Re-syncing the same entry is safe; claims and embedding are overwritten. |
| **Deletion handling** | Source entries removed → mark mirrored row `tombstoned = true` | Preserves audit trail. Tombstoned rows are excluded from neighbor search (partial index). Periodic cleanup job may hard-delete after configurable retention. |
| **Revision handling** | Source entry revised → update mirrored claims + re-embed | Detected via observe response including revision entries. The UPSERT replaces claims; a new EMBED_MIRRORED job is queued. |
| **Topic filter** | `topic_filter` column on subscription; sync skips entries where `topic NOT LIKE filter%` | Answers Open Question 1 from 08-SECURITY-MODEL.md: subscriptions can target specific topic prefixes, not just entire notebooks. |

### 3.4 Air-Gapped Sync

For TOP SECRET or otherwise network-isolated thinktanks (addresses Open
Question 6 from 08-SECURITY-MODEL.md):

**Export endpoint** (on the source):
```
GET /notebooks/{id}/export?since={seq}&scope={scope}
→ Content-Type: application/json
```

Returns a signed JSON bundle:

```json
{
  "source_notebook_id": "uuid",
  "source_notebook_name": "OSINT-Feed",
  "scope": "claims",
  "since_sequence": 1042,
  "through_sequence": 1187,
  "entries": [
    {
      "entry_id": "uuid",
      "sequence": 1043,
      "claims": [...],
      "topic": "geopolitics/trade",
      "content": null,
      "content_type": "text/plain"
    }
  ],
  "exported_at": "2026-02-19T...",
  "signature": "<base64 Ed25519 signature over canonical JSON of above fields>"
}
```

The `content` field is populated only when `scope=entries`; null otherwise.
The signature covers the canonical (sorted-key, no-whitespace) JSON of all
fields except `signature` itself, signed by the source notebook owner's key.

**Import endpoint** (on the subscriber):
```
POST /notebooks/{id}/import
Content-Type: application/json
Body: <the export bundle>
```

Processing:
1. Validate Ed25519 signature against the source notebook owner's public key
   (the subscriber must have the source's public key registered).
2. Verify `source_notebook_id` matches an existing subscription's `source_id`.
3. Process entries as in the online sync loop (step 3 above).
4. Update `sync_watermark` to `through_sequence`.

**Transfer mechanism** is out of scope — could be USB drive, data diode, or
any unidirectional transport. The bundle is a self-contained, verifiable unit.

---

## 4. Cross-Boundary Neighbor Search

### 4.1 Problem

Currently `FindNearestByEmbeddingAsync` searches only within the local
notebook's `entries` table. With subscriptions, the EMBED_CLAIMS pipeline must
also consider mirrored claims when finding neighbors.

### 4.2 Approach: UNION Query

Extend the existing cosine similarity SQL to UNION local entries with mirrored
claims. Both use `DOUBLE PRECISION[]` embeddings and the same dot-product /
L2-norm pattern already in `EntryRepository`.

```sql
-- FindNearestWithMirroredAsync
WITH neighbors AS (
    -- Local entries (existing query, unchanged)
    SELECT e.id,
           e.claims,
           (SELECT SUM(q.val * d.val)
            FROM unnest(@query) WITH ORDINALITY AS q(val, ord)
            JOIN unnest(e.embedding) WITH ORDINALITY AS d(val, ord) USING (ord))
           /
           NULLIF(
             SQRT((SELECT SUM(v.val * v.val) FROM unnest(@query) AS v(val)))
             * SQRT((SELECT SUM(v.val * v.val) FROM unnest(e.embedding) AS v(val))),
             0)
           AS similarity,
           false AS is_mirrored,
           NULL::uuid AS subscription_id
    FROM entries e
    WHERE e.notebook_id = @notebookId
      AND e.id != @entryId
      AND e.embedding IS NOT NULL
      AND e.claims_status IN ('distilled', 'verified')
      AND e.fragment_of IS NULL

    UNION ALL

    -- Mirrored claims from subscriptions
    SELECT mc.id,
           mc.claims,
           (SELECT SUM(q.val * d.val)
            FROM unnest(@query) WITH ORDINALITY AS q(val, ord)
            JOIN unnest(mc.embedding) WITH ORDINALITY AS d(val, ord) USING (ord))
           /
           NULLIF(
             SQRT((SELECT SUM(v.val * v.val) FROM unnest(@query) AS v(val)))
             * SQRT((SELECT SUM(v.val * v.val) FROM unnest(mc.embedding) AS v(val))),
             0)
           AS similarity,
           true AS is_mirrored,
           mc.subscription_id
    FROM mirrored_claims mc
    WHERE mc.notebook_id = @notebookId
      AND mc.embedding IS NOT NULL
      AND NOT mc.tombstoned
)
SELECT id, claims, similarity, is_mirrored, subscription_id
FROM neighbors
ORDER BY similarity DESC NULLS LAST
LIMIT @topK
```

### 4.3 New Repository Method

```csharp
// EntryRepository.cs
Task<List<NeighborResult>> FindNearestWithMirroredAsync(
    Guid notebookId,
    Guid excludeEntryId,
    double[] queryEmbedding,
    int topK,
    CancellationToken ct);

record NeighborResult(
    Guid Id,
    List<Claim> Claims,
    double Similarity,
    bool IsMirrored,
    Guid? SubscriptionId);
```

The existing `FindNearestByEmbeddingAsync` is kept unchanged — called by
`SemanticSearchAsync` and other paths that should not include mirrored content
unless explicitly requested.

### 4.4 Embedding Model Compatibility

The UNION query assumes local and mirrored embeddings are in the same vector
space. This is enforced by:

1. Mirrored claims are **always re-embedded** using the subscriber's embedding
   model, not the source's. The `EMBED_MIRRORED` job sends the claim text
   through the same embedding pipeline as local `EMBED_CLAIMS`.
2. The `embedding_model` column on `notebook_subscriptions` records which model
   was used. If the subscriber changes embedding models, all mirrored embeddings
   must be recomputed (same as local entries).

---

## 5. Pipeline Modifications

### 5.1 EMBED_MIRRORED Job

A new job type for embedding mirrored claims:

```sql
ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_job_type_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_job_type_check
    CHECK (job_type IN (
        'DISTILL_CLAIMS', 'COMPARE_CLAIMS',
        'CLASSIFY_TOPIC', 'EMBED_CLAIMS',
        'EMBED_MIRRORED'
    ));
```

Payload:
```json
{
  "mirrored_claim_id": "uuid",
  "notebook_id": "uuid",
  "claims": [...]
}
```

Result (same shape as EMBED_CLAIMS):
```json
{
  "mirrored_claim_id": "uuid",
  "embedding": [0.012, -0.034, ...]
}
```

Priority: 25 (between COMPARE_CLAIMS=20 and EMBED_CLAIMS=30 — embedding
mirrored claims should happen promptly but local embeddings take precedence).

The EMBED_MIRRORED result handler in `JobResultProcessor` updates
`mirrored_claims.embedding` but does **not** trigger neighbor search or
comparison jobs. Mirrored claims are passive participants — they are found as
neighbors when local entries are embedded, not the other way around.

### 5.2 JobResultProcessor — EMBED_CLAIMS Modification

The EMBED_CLAIMS handler changes from:

```
neighbors = FindNearestByEmbeddingAsync(notebookId, entryId, embedding, topK=5)
```

to:

```
neighbors = FindNearestWithMirroredAsync(notebookId, entryId, embedding, topK=5)
```

For each neighbor returned:

- **Local neighbor** (`is_mirrored = false`): queue COMPARE_CLAIMS as today.
- **Mirrored neighbor** (`is_mirrored = true`): queue COMPARE_CLAIMS with
  additional payload fields:
  ```json
  {
    "entry_id": "<local entry UUID>",
    "compare_against_id": "<mirrored_claims UUID>",
    "claims_a": [...],
    "claims_b": [...],
    "cross_boundary": true,
    "subscription_id": "<UUID>",
    "discount_factor": 0.3
  }
  ```

`expected_comparisons` counts both local and mirrored neighbors.

### 5.3 JobResultProcessor — COMPARE_CLAIMS Modification

When processing a COMPARE_CLAIMS result:

1. Read optional `cross_boundary` and `discount_factor` from the job **payload**
   (included at job creation time per §5.2 — avoids a DB lookup per comparison).
2. Compute `effective_discount = cross_boundary ? discount_factor : 1.0`.
3. Call `AppendComparisonAsync(entryId, result, effectiveDiscount, ct)`.
   The method signature gains an optional parameter:
   ```csharp
   Task<int> AppendComparisonAsync(
       Guid entryId, JsonElement comparison,
       double discountFactor = 1.0,       // NEW — default preserves existing behavior
       CancellationToken ct = default);
   ```
   Inside, after extracting `friction` from the comparison JSON:
   ```csharp
   var effectiveFriction = friction * discountFactor;
   ```
   The SQL UPDATE uses `@effectiveFriction` for the `max_friction` column:
   ```sql
   max_friction = GREATEST(COALESCE(max_friction, 0.0), @effectiveFriction)
   ```
   The raw comparison JSON is stored unmodified in the `comparisons` array
   (preserving the original friction score for audit). The `effective_friction`
   and `discount_factor` are appended as sibling fields in the stored object.
4. `needs_review` threshold (0.2) applies to effective friction.
5. Integration status transition uses effective friction.
6. If not `cross_boundary`: `discountFactor` defaults to 1.0 — behavior
   identical to today.

No new job type needed. COMPARE_CLAIMS remains stateless — `claims_a` and
`claims_b` are inline. The only additions are the three optional payload fields.

### 5.4 Agent Routing

Cross-boundary COMPARE_CLAIMS jobs are scoped to `notebook_id` (the
subscriber). Since Hush-5 filters `ClaimNextJobAsync` by the agent's clearance
level against the notebook's classification:

```sql
WHERE n.classification <= @agentMaxLevel
  AND n.compartments <@ @agentCompartments
```

...the agent processing a cross-boundary job must be cleared for the
**subscriber's** level. This is correct: the job contains the subscriber's
claims (claims_b) and the result is stored on the subscriber's entry. The
source's claims (claims_a) have already crossed the boundary via the
subscription — they are now within the subscriber's classification scope.

No special routing logic needed.

---

## 6. Subscription Management Endpoints

### 6.1 API

```
POST   /notebooks/{id}/subscriptions                — create subscription
DELETE /notebooks/{id}/subscriptions/{subId}         — remove + cascade delete mirrored data
GET    /notebooks/{id}/subscriptions                 — list (with sync status summary)
POST   /notebooks/{id}/subscriptions/{subId}/sync    — trigger immediate sync
GET    /notebooks/{id}/subscriptions/{subId}/status   — detailed sync status

GET    /notebooks/{id}/export?since={seq}&scope={scope}  — export bundle (air-gap)
POST   /notebooks/{id}/import                            — import bundle (air-gap)
```

### 6.2 Create Validation

On `POST /notebooks/{id}/subscriptions`:

```csharp
// SubscriptionService.cs — CreateAsync
async Task<Subscription> CreateAsync(Guid subscriberId, CreateSubscriptionRequest req)
{
    // 1. No self-subscription
    if (subscriberId == req.SourceId) throw Conflict("cannot subscribe to self");

    // 2. Source exists
    var source = await notebookRepo.GetAsync(req.SourceId)
        ?? throw NotFound("source notebook not found");

    // 3. Subscriber classification >= source classification
    var subscriber = await notebookRepo.GetAsync(subscriberId);
    if (subscriber.Classification < source.Classification)
        throw Forbidden("subscriber classification too low");

    // 4. Subscriber compartments ⊇ source compartments
    if (!source.Compartments.All(c => subscriber.Compartments.Contains(c)))
        throw Forbidden("subscriber missing required compartments");

    // 5. No duplicate
    if (await subscriptionRepo.ExistsAsync(subscriberId, req.SourceId))
        throw Conflict("subscription already exists");

    // 6. No cycles (see §8.1)
    if (await subscriptionRepo.WouldCreateCycleAsync(subscriberId, req.SourceId))
        throw Conflict("subscription would create a cycle");

    // 7. Requesting principal has admin tier on subscriber notebook
    // (checked by middleware/authorization filter)

    return await subscriptionRepo.CreateAsync(...);
}
```

### 6.3 Delete Behavior

Deleting a subscription cascades to:
- `mirrored_claims` rows (via FK `ON DELETE CASCADE`)
- `mirrored_entries` rows (via FK `ON DELETE CASCADE`)
- Any pending `EMBED_MIRRORED` jobs for the subscription's mirrored claims
  are cancelled (set `status = 'failed'`, `error = 'subscription deleted'`).

Comparisons already stored on local entries are **not** removed — they are
historical records and removing them would require recomputing
`max_friction` and `integration_status`.

---

## 7. Modified Browse/Search Responses

### 7.1 Browse

`GET /notebooks/{id}/browse` gains an optional query parameter:

```
?include_mirrored=true
```

When set, the response includes an additional `mirrored_topics` array alongside
the existing `topics` array:

```json
{
  "topics": [ ... ],
  "mirrored_topics": [
    {
      "topic": "geopolitics/trade",
      "source_notebook_id": "uuid",
      "subscription_id": "uuid",
      "entry_count": 42,
      "latest_sequence": 1187
    }
  ]
}
```

Default is `false` — existing behavior unchanged.

### 7.2 Semantic Search

`POST /notebooks/{id}/semantic-search` automatically includes mirrored claims
in results when the notebook has active subscriptions. Each result gains:

```json
{
  "id": "uuid",
  "claims": [...],
  "similarity": 0.87,
  "source": "mirrored",
  "subscription_id": "uuid"
}
```

Local results have `"source": "local"` and no `subscription_id`. The query uses
the same UNION pattern from §4.2.

### 7.3 Observe

`GET /notebooks/{id}/observe` is **unchanged**. It returns only local entries.
Mirrored content has its own provenance (the subscription sync) and mixing the
two streams would break causal position semantics.

---

## 8. Subscription Topology Validation

### 8.1 No Cycles

Bidirectional subscriptions (A→B and B→A) are prohibited — they would create
circular data flow that violates the information-flows-upward invariant.

Cycle detection on create:

```csharp
// Does sourceId (directly or transitively) subscribe to subscriberId?
async Task<bool> WouldCreateCycleAsync(Guid subscriberId, Guid sourceId)
{
    // BFS/DFS from sourceId following subscriber_id → source_id edges
    // If subscriberId is reachable, adding this edge would create a cycle.
    var visited = new HashSet<Guid>();
    var queue = new Queue<Guid>();
    queue.Enqueue(sourceId);

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (current == subscriberId) return true;
        if (!visited.Add(current)) continue;

        var sources = await GetSourceIdsForSubscriberAsync(current);
        foreach (var s in sources) queue.Enqueue(s);
    }
    return false;
}
```

This is O(N) in the number of subscriptions (expected to be small — tens, not
thousands) and runs only on subscription creation.

### 8.2 Transitive Subscriptions

A→B→C is allowed. A sees B's local content only. A does **not** automatically
see C's content that B mirrored. Mirrored content is stored in B's
`mirrored_claims` table, not in B's `entries` table, so it is not returned by
B's observe endpoint and therefore never synced to A.

If A wants C's content, A must subscribe to C directly (subject to
classification checks).

### 8.3 Classification Changes

If a source notebook's classification is raised above the subscriber's level:

1. A background check runs periodically (or is triggered by the classification
   change event).
2. Affected subscriptions are set to `sync_status = 'suspended'`.
3. Sync stops. Existing mirrored content is retained but no longer updated.
4. An admin notification is generated.
5. Resolution options:
   - Raise subscriber's classification → resume subscription.
   - Delete the subscription → cascade deletes mirrored content.
   - Accept stale data → leave suspended (mirrored content remains queryable
     but grows increasingly stale; `last_sync_at` signals staleness).

---

## 9. Migration Path

All schema changes are **additive** — no modifications to existing tables:

| Change | Type | Existing table affected? |
|--------|------|------------------------|
| `notebook_subscriptions` | New table | No |
| `mirrored_claims` | New table | No |
| `mirrored_entries` | New table | No |
| `EMBED_MIRRORED` job type | Constraint update on `jobs.job_type` | Yes — CHECK constraint only |
| `FindNearestWithMirroredAsync` | New repository method | No — alongside existing method |
| `JobResultProcessor` EMBED_CLAIMS mod | Code change | Backward-compatible: no subscriptions → no mirrored results → identical behavior |
| `JobResultProcessor` COMPARE_CLAIMS mod | Code change | Backward-compatible: no `cross_boundary` field → discount_factor=1.0 implicitly |
| Browse `include_mirrored` param | Code change | Backward-compatible: default false |

**Migration order** (within Hush-6, after Hush-1 through Hush-5):

1. `017_subscriptions.sql` — `notebook_subscriptions` table
2. `018_mirrored_content.sql` — `mirrored_claims`, `mirrored_entries`
3. `019_embed_mirrored_job_type.sql` — extend `jobs.job_type` CHECK constraint
4. Code changes: `SubscriptionRepository`, `SubscriptionSyncService`,
   `FindNearestWithMirroredAsync`, `JobResultProcessor` modifications,
   subscription API endpoints

**Prerequisites** (earlier Hush phases that must land first):

- **Hush-3** (migration 014 — `classification` and `compartments` columns on
  `notebooks`): required for subscription creation validation (§6.2 checks
  `subscriber.Classification >= source.Classification`).
- **Hush-4** (tiered access control on `notebook_access`): required for
  admin-tier authorization on subscription management endpoints.
- **Hush-5** (agent clearance filtering in `ClaimNextJobAsync`): must be in
  place before cross-boundary COMPARE_CLAIMS jobs are created, otherwise an
  insufficiently cleared agent could claim a job containing
  higher-classification claims.

---

## 10. Security Invariants

### 10.1 No Downward Flow

Every data path is unidirectional — information flows from lower classification
to higher, never the reverse:

| Data path | Direction | Verification |
|-----------|-----------|-------------|
| Sync (observe) | Subscriber pulls FROM source | Subscriber initiates HTTP request; source serves read-only data |
| Mirrored storage | Stored in subscriber's DB only | `mirrored_claims.notebook_id` is the subscriber's ID |
| COMPARE_CLAIMS results | Stored on subscriber's entries only | `AppendComparisonAsync` targets the local entry |
| Export bundles | Source exports; subscriber imports | Unidirectional by construction |
| Subscription metadata | Source never learns it has subscribers | No callback, no notification, no reverse FK |

### 10.2 Containment of Cross-Boundary Knowledge

When a COMPARE_CLAIMS job reveals friction between a local (higher) claim and a
mirrored (lower) claim:

- The friction score is stored on the **local** entry's `comparisons` array.
- The local entry's `needs_review` and `integration_status` may change.
- The source notebook is never notified of the contradiction.
- The source's mirrored claim is not modified.

This means the **existence of a contradiction** is itself classified at the
subscriber's level. An analyst reviewing friction in a SECRET thinktank may see
that a SECRET claim contradicts a PUBLIC claim, but that review happens entirely
within the SECRET context.

### 10.3 Classification Spillage Mitigation

Open Question 2 from 08-SECURITY-MODEL.md: a user with SECRET clearance writes
SECRET-derived insights into a PUBLIC thinktank.

This is an operational security problem, not a technical one. The architecture
mitigates but cannot prevent it:

- Subscriptions only pull data upward. A SECRET user interacting with a PUBLIC
  thinktank does so through the PUBLIC thinktank's own API, not through
  the subscription mechanism.
- The `discount_factor` ensures that public content does not dominate the
  SECRET thinktank's integration cost model, reducing the incentive to write
  back derived insights.
- Administrative controls (audit logging, write restrictions per classification)
  are the primary defense. The subscription architecture does not introduce new
  spillage vectors.

### 10.4 Credential Management

Source notebook JWTs held by subscribers:

- Must be read-scoped (no write access to source).
- Stored in a separate credentials table or external vault — **not** in
  `notebook_subscriptions`.
- Rotated on a schedule independent of the subscription lifecycle.
- Revocable by the source notebook's admin (stops sync without the subscriber
  needing to act).

---

## 11. Open Questions Resolution

Cross-reference with [08-SECURITY-MODEL.md](08-SECURITY-MODEL.md) §Open
Questions:

| # | Question | Resolution |
|---|----------|-----------|
| 1 | **Subscription granularity** — entire notebook or specific topics? | Both. `topic_filter` column enables topic-prefix filtering. `NULL` means entire notebook. (§1, §3.3) |
| 2 | **Classification spillage** — SECRET user writes in PUBLIC | Operational problem; mitigated by unidirectional flow and discount factor. No new vectors introduced. (§10.3) |
| 3 | **Thinktank splitting** — partitioning entries | Out of scope for subscription architecture. Splitting is a local administrative operation; subscriptions to the original notebook would need to be re-pointed to the relevant split target. |
| 4 | **Thinktank merging** — labels JOIN, recomputation | Out of scope for subscription architecture. After merge, subscriptions to either predecessor are migrated to the merged notebook; mirrored data from both predecessors is preserved under updated subscription IDs. |
| 5 | **Subscription and entropy** — do subscribed entries affect integration cost? | Yes, via discount factor (default 0.3). Cross-boundary friction is scaled before updating `max_friction`. (§5.3) |
| 6 | **Offline/air-gapped sync** | Export/import endpoints with Ed25519-signed bundles. (§3.4) |
