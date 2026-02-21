# 09 — Phase Hush: Security Model Implementation Plan

**Status:** Planned — not yet implemented.
**Depends on:** 08-SECURITY-MODEL.md (architecture), existing auth infrastructure.
**Goal:** Implement the "one classification per thinktank" security model with organizational groups, access tiers, ThinkerAgent trust, inter-thinktank subscriptions, and audit.

## Current State Assessment

Before building new security features, the current gaps must be understood:

| Capability | Status |
|---|---|
| `notebook_access` ACL table | Schema exists, NOT enforced |
| JWT scope strings (`notebook:read/write/share/admin`) | In token payload, NOT checked at endpoints |
| Share/revoke endpoints | NOT implemented in Notebook.Server |
| Entry read/browse ACL check | NOT implemented — any valid token can read any notebook |
| Batch-write ACL check | NOT implemented — any valid token can write to any notebook |
| ASP.NET Identity Roles | Table exists, completely unused |
| ThinkerAgent endpoint auth | Completely open (`/config`, `/quit`, `/models`) |
| Quota enforcement server-side | Only in Blazor UI, not in API |
| Organization/group concepts | Do not exist anywhere |
| Security labels/classification | Do not exist anywhere |
| Audit trail | Does not exist |

## Sub-Phase Overview

| Sub-Phase | Name | Description | Depends On |
|---|---|---|---|
| Hush-1 | **Close the Gates** | Enforce existing ACL + scopes, implement share endpoints, lock ThinkerAgent | — |
| Hush-2 | **Organizations & Groups** | Org/group DAG model, principal memberships | Hush-1 |
| Hush-3 | **Security Labels** | Classification levels + compartments on notebooks, clearance on principals | Hush-2 |
| Hush-4 | **Access Tiers** | Existence/read/write/admin layered access per thinktank | Hush-3 |
| Hush-5 | **Agent Trust** | ThinkerAgent security labels, label-aware job routing | Hush-3 |
| Hush-6 | **Subscriptions** | Inter-thinktank catalog/claim flow with classification boundary enforcement ([arch](12-SUBSCRIPTION-ARCHITECTURE.md)) | Hush-3, Hush-4, Hush-5 |
| Hush-7 | **Ingestion Gate** | Classification assertions, external contribution review queue | Hush-3, Hush-4 |
| Hush-8 | **Audit** | Full audit trail for reads, writes, access changes, processing | Hush-1 |

---

## Hush-1: Close the Gates

**Goal:** Enforce the security primitives that already exist in the schema but are not checked at runtime. This is prerequisite work — no new concepts, just making the existing model actually work.

### 1.1 ACL Enforcement Middleware

**Create:** `Notebook.Server/Middleware/NotebookAccessMiddleware.cs`

For every request to `/notebooks/{notebookId}/...`, verify the authenticated `author_id` has the required permission in `notebook_access`:

| HTTP Method / Endpoint Pattern | Required Permission |
|---|---|
| `GET /notebooks/{id}/entries`, `/browse`, `/observe`, `/search` | `read = true` |
| `POST /notebooks/{id}/batch`, `/entries` | `write = true` |
| `POST /notebooks/{id}/share`, `DELETE .../share/{authorId}` | owner only |
| `DELETE /notebooks/{id}`, `PUT /notebooks/{id}` | owner only |
| `GET /notebooks/{id}/jobs/next`, `POST .../jobs/{id}/complete` | `write = true` (worker acts on behalf of notebook) |

The middleware extracts `notebookId` from the route and `author_id` from the JWT `sub` claim, then queries `notebook_access`. Cache per-request to avoid repeated DB hits.

### 1.2 Scope-Based Authorization Policies

**Modify:** `Notebook.Server/Program.cs`

Define named policies:
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanRead", p => p.RequireClaim("scope", "notebook:read"));
    options.AddPolicy("CanWrite", p => p.RequireClaim("scope", "notebook:write"));
    options.AddPolicy("CanShare", p => p.RequireClaim("scope", "notebook:share"));
    options.AddPolicy("CanAdmin", p => p.RequireClaim("scope", "notebook:admin"));
});
```

Apply to endpoints: `.RequireAuthorization("CanRead")` on browse/read, `.RequireAuthorization("CanWrite")` on batch-write, etc.

### 1.3 Share/Revoke Endpoints

**Create:** `Notebook.Server/Endpoints/ShareEndpoints.cs`

```
POST   /notebooks/{id}/share          — grant access (owner only)
         Body: { "author_id": "hex", "read": true, "write": false }
DELETE /notebooks/{id}/share/{authorId} — revoke access (owner only)
GET    /notebooks/{id}/participants    — list access grants (already exists in ObserveEndpoints)
```

### 1.4 ThinkerAgent Endpoint Authentication

**Modify:** `ThinkerAgent/Program.cs`

Add authentication to ThinkerAgent's own management API. Options:
- **Simple:** shared secret in config, checked via middleware on `/config`, `/start`, `/stop`, `/quit`
- **Better:** require the same EdDSA JWT that the notebook server uses

At minimum, `/quit` and `PUT /config` MUST be authenticated — they can shut down or reconfigure the service.

### 1.5 Server-Side Quota Enforcement

**Modify:** `Notebook.Server/Endpoints/BatchEndpoints.cs`

Before inserting entries, check quota limits (notebooks per user, entries per notebook, entry size, total storage). Currently only checked in the Blazor UI layer.

### 1.6 Admin UI — Share Management

**Modify:** `Components/Pages/Notebooks/View.razor`

Add a **Participants** panel to the notebook view page (visible to notebook owners only):
- List current access grants (author, read/write flags) via `GET /notebooks/{id}/participants`
- Inline grant form: author ID input + read/write checkboxes + "Grant" button → `POST /notebooks/{id}/share`
- Revoke button per participant → `DELETE /notebooks/{id}/share/{authorId}` with confirmation

This keeps share management contextual (on the notebook page) rather than adding a separate top-level page.

### Files

| File | Change |
|---|---|
| `Notebook.Server/Middleware/NotebookAccessMiddleware.cs` | **New** — ACL enforcement |
| `Notebook.Server/Endpoints/ShareEndpoints.cs` | **New** — share/revoke |
| `Notebook.Server/Program.cs` | Add policies, register middleware |
| `Notebook.Server/Endpoints/BatchEndpoints.cs` | Add ACL + quota checks |
| `Notebook.Server/Endpoints/BrowseEndpoints.cs` | Add ACL checks |
| `Notebook.Server/Endpoints/ReadEndpoints.cs` | Add ACL checks |
| `Notebook.Server/Endpoints/ObserveEndpoints.cs` | Add ACL checks |
| `Notebook.Server/Endpoints/JobEndpoints.cs` | Add ACL checks |
| `ThinkerAgent/Program.cs` | Add endpoint auth |
| `Components/Pages/Notebooks/View.razor` | Add participants panel with grant/revoke |

### Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Middleware/NotebookAccessMiddlewareTests.cs` | **New** — ACL enforcement: unauthenticated → 401, no ACL → 404, read-only → 403 on write, scope mismatch → 403 |
| `Notebook.Server.Tests/Endpoints/ShareEndpointsTests.cs` | **New** — non-owner grant → 403, non-owner revoke → 403, happy path grant/revoke |
| `Notebook.Server.Tests/Endpoints/BatchEndpointsTests.cs` | **New** — quota exceeded → 429, write without ACL → 404 |
| `ThinkerAgent.Tests/AuthenticationTests.cs` | **New** — unauthenticated /quit → 401, unauthenticated /config → 401 |

---

## Hush-2: Organizations & Groups

**Goal:** Introduce the organizational structure that thinktanks belong to.

### 2.1 Database Schema

**Migration:** `013_organizations_and_groups.sql` *(012 is taken by integration_status)*

```sql
CREATE TABLE organizations (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT NOT NULL UNIQUE,
    created     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE groups (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organizations(id),
    name            TEXT NOT NULL,
    created         TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (organization_id, name)
);

-- DAG edges: parent → child relationships within an org
CREATE TABLE group_edges (
    parent_id   UUID NOT NULL REFERENCES groups(id),
    child_id    UUID NOT NULL REFERENCES groups(id),
    PRIMARY KEY (parent_id, child_id),
    CHECK (parent_id != child_id)
);

-- Principal memberships (many-to-many)
CREATE TABLE group_memberships (
    author_id   BYTEA(32) NOT NULL REFERENCES authors(id),
    group_id    UUID NOT NULL REFERENCES groups(id),
    role        TEXT NOT NULL DEFAULT 'member',  -- member, admin
    granted     TIMESTAMPTZ NOT NULL DEFAULT now(),
    granted_by  BYTEA(32) REFERENCES authors(id),
    PRIMARY KEY (author_id, group_id)
);

-- Link notebooks to owning groups
ALTER TABLE notebooks ADD COLUMN owning_group_id UUID REFERENCES groups(id);
```

### 2.2 Core Types

**Create:** `Notebook.Core/Types/Organization.cs`, `Group.cs`, `GroupMembership.cs`

### 2.3 Repository Layer

**Create:** `Notebook.Data/Repositories/IOrganizationRepository.cs`, `OrganizationRepository.cs`

Operations: CRUD for orgs, groups, edges, memberships. DAG traversal (ancestors, descendants).

**Cycle detection:** On `group_edges` insert, execute a recursive CTE from the proposed child walking parent edges. If the proposed parent is reachable, reject the insert.

```sql
WITH RECURSIVE ancestors AS (
    SELECT parent_id FROM group_edges WHERE child_id = @proposed_parent_id
    UNION
    SELECT ge.parent_id FROM group_edges ge
    JOIN ancestors a ON ge.child_id = a.parent_id
)
SELECT EXISTS (SELECT 1 FROM ancestors WHERE parent_id = @proposed_child_id);
```

This runs in the same transaction as the insert. At the expected scale (< 1000 groups per org), this is sufficient. If group counts grow beyond 10k, consider a materialized transitive closure table with trigger-based maintenance.

### 2.4 API Endpoints

**Create:** `Notebook.Server/Endpoints/OrganizationEndpoints.cs`

```
POST   /organizations                          — create org
GET    /organizations                          — list orgs (filtered by membership)
POST   /organizations/{id}/groups              — create group
GET    /organizations/{id}/groups              — list groups (DAG)
POST   /groups/{id}/members                    — add member
DELETE /groups/{id}/members/{authorId}          — remove member
POST   /groups/{id}/edges                      — add parent→child edge
```

All admin operations require `notebook:admin` scope + group admin role.

### Tests

| File | Covers |
|---|---|
| `Notebook.Data.Tests/Repositories/OrganizationRepositoryTests.cs` | **New** — CRUD orgs/groups, DAG edge insert, cycle detection rejection |
| `Notebook.Server.Tests/Endpoints/OrganizationEndpointsTests.cs` | **New** — non-admin create → 403, membership operations |

### 2.5 Admin UI — Organization & Group Management

Four new pages under `/admin/`:

**Page: `Components/Pages/Admin/Organizations.razor`** — `/admin/organizations`
- List all organizations (name, group count, member count)
- "Create Organization" form (name input)
- Click row → navigate to organization detail

**Page: `Components/Pages/Admin/OrganizationDetail.razor`** — `/admin/organizations/{OrgId}`
- Organization header (name, created date)
- **Groups tree** — render the DAG as an indented tree (parent → children). Each node shows group name, member count. Expand/collapse.
- "Create Group" form (name, optional parent group dropdown)
- "Add Edge" form (parent dropdown, child dropdown) for linking existing groups
- Click group → navigate to group detail

**Page: `Components/Pages/Admin/GroupDetail.razor`** — `/admin/groups/{GroupId}`
- Group header (name, organization, parent groups, child groups)
- **Members table** — author display name, author ID, role (member/admin), granted date, granted by
- "Add Member" form: user picker (dropdown of registered users) + role select
- Remove member button per row (with confirmation)
- **Owned Notebooks** list — notebooks where `owning_group_id` = this group

**Modify: `Components/Pages/Notebooks/View.razor`**
- Add "Owning Group" display field (group name, linked to group detail)
- For notebook owners/admins: "Assign to Group" dropdown to set `owning_group_id`

---

## Hush-3: Security Labels

**Goal:** Add classification levels and compartments to notebooks and principals.

### 3.1 Database Schema

**Migration:** `014_security_labels.sql`

```sql
CREATE TYPE classification_level AS ENUM (
    'PUBLIC', 'INTERNAL', 'CONFIDENTIAL', 'SECRET', 'TOP_SECRET'
);

ALTER TABLE notebooks ADD COLUMN classification classification_level NOT NULL DEFAULT 'INTERNAL';
ALTER TABLE notebooks ADD COLUMN compartments TEXT[] NOT NULL DEFAULT '{}';

-- Principal clearance (per org — Boeing may clear you to SECRET, Microsoft only to CONFIDENTIAL)
CREATE TABLE principal_clearances (
    author_id       BYTEA(32) NOT NULL REFERENCES authors(id),
    organization_id UUID NOT NULL REFERENCES organizations(id),
    max_level       classification_level NOT NULL DEFAULT 'INTERNAL',
    compartments    TEXT[] NOT NULL DEFAULT '{}',
    granted         TIMESTAMPTZ NOT NULL DEFAULT now(),
    granted_by      BYTEA(32) REFERENCES authors(id),
    PRIMARY KEY (author_id, organization_id)
);
```

### 3.2 Label Dominance Logic

**Create:** `Notebook.Core/Security/SecurityLabel.cs`

```csharp
public record SecurityLabel(ClassificationLevel Level, IReadOnlySet<string> Compartments)
{
    public bool Dominates(SecurityLabel other) =>
        Level >= other.Level && Compartments.IsSupersetOf(other.Compartments);
}
```

### 3.3 Clearance Check Integration

**Modify:** `NotebookAccessMiddleware` (from Hush-1)

After ACL check, also verify:
- Principal's clearance (from `principal_clearances` for the notebook's org) dominates the notebook's security label
- If not → 404 (not 403 — don't reveal existence)

### 3.3a Clearance Cache

To avoid a DB query per request, cache clearance lookups in `IMemoryCache` keyed by `(author_id, organization_id)` with a 30-second sliding expiration.

**Invalidation:** When `POST /clearances` or `DELETE /clearances` modifies a principal's clearance, evict the cache entry for that `(author_id, organization_id)` pair. Since the server is single-process, in-memory eviction is sufficient. Multi-instance deployments would need a pub/sub invalidation channel (Redis, PostgreSQL NOTIFY).

**Staleness contract:** A revoked clearance may still be honored for up to 30 seconds. This is an accepted trade-off. For immediate revocation (e.g., incident response), add a `POST /admin/cache/flush` endpoint that clears all cached clearances.

### 3.4 Notebook Creation with Label

**Modify:** `POST /notebooks` — accept `classification` and `compartments` fields. Validate that the creating principal's clearance dominates the requested label.

### Tests

| File | Covers |
|---|---|
| `Notebook.Core.Tests/Security/SecurityLabelTests.cs` | **New** — dominance logic: equal, higher, lower, compartment subset/superset, disjoint |
| `Notebook.Server.Tests/Middleware/ClearanceCheckTests.cs` | **New** — insufficient clearance → 404, sufficient clearance → pass-through |

### Files

| File | Change |
|---|---|
| `Notebook.Server/Services/ClearanceCacheService.cs` | **New** — IMemoryCache wrapper with eviction |
| `Notebook.Server/Middleware/NotebookAccessMiddleware.cs` | Inject ClearanceCacheService instead of direct DB query |

### 3.5 Admin UI — Classification & Clearance Management

**Modify: `Components/Pages/Notebooks/View.razor`**
- Show classification badge next to notebook name (color-coded: PUBLIC=green, INTERNAL=blue, CONFIDENTIAL=yellow, SECRET=orange, TOP_SECRET=red)
- Show compartment tags as pills
- For notebook admins: "Edit Classification" form — classification dropdown + compartments multi-input. Validation: creating user's clearance must dominate the new label.

**Modify: `Components/Pages/Notebooks/List.razor`**
- Add classification badge column to the notebook list table
- Filter dropdown to filter notebooks by classification level

**Page: `Components/Pages/Admin/Clearances.razor`** — `/admin/clearances`
- Table: principal (display name + author ID), organization, max level, compartments, granted date, granted by
- Filter by organization
- "Grant Clearance" form: user picker, organization picker, classification level dropdown, compartments multi-input
- Edit/revoke buttons per row

**Modify: `Components/Pages/Admin/OrganizationDetail.razor`**
- Add **Clearances** tab showing all principal clearances within this organization
- Inline grant clearance form scoped to the current organization

---

## Hush-4: Access Tiers

**Goal:** Implement the four-tier access model (existence, read, write, admin) tied to security labels.

### 4.1 Extend `notebook_access`

**Migration: `015a_add_access_tiers.sql`**

```sql
ALTER TABLE notebook_access ADD COLUMN tier TEXT NOT NULL DEFAULT 'read_write'
    CHECK (tier IN ('existence', 'read', 'read_write', 'admin'));

-- Backfill from existing booleans
UPDATE notebook_access SET tier = CASE
    WHEN read AND write THEN 'read_write'
    WHEN read AND NOT write THEN 'read'
    ELSE 'existence'
END;
```

Deploy application code that reads `tier` column. Verify in production. Then:

**Migration: `015b_drop_legacy_acl_booleans.sql`**

```sql
ALTER TABLE notebook_access DROP COLUMN read;
ALTER TABLE notebook_access DROP COLUMN write;
```

This two-step approach enables rollback: if the application has issues after 015a, revert application code while the old columns still exist.

### 4.2 Tier Semantics

| Tier | Can know exists | Can browse/read | Can write entries | Can manage access |
|---|---|---|---|---|
| `existence` | Yes | No | No | No |
| `read` | Yes | Yes | No | No |
| `read_write` | Yes | Yes | Yes | No |
| `admin` | Yes | Yes | Yes | Yes |

### 4.3 Group-Based Access Propagation

When a notebook is owned by a group, members of that group automatically inherit access at their membership role's tier. Members of child groups in the DAG also inherit access. Members of parent groups do NOT inherit access (information flows up, not down).

**Create:** `Notebook.Server/Services/AccessResolver.cs`

Resolves effective access for a principal to a notebook by combining:
1. Direct `notebook_access` grants
2. Group membership in the owning group + descendants
3. Clearance dominance check

### 4.4 Existence Concealment

Endpoints must return 404 (not 403) when a principal lacks existence-tier access. This prevents information leakage about what notebooks exist. Error messages must not reference the notebook ID.

### Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Services/AccessResolverTests.cs` | **New** — direct grant vs group inheritance, tier precedence, existence concealment |
| `Notebook.Data.Tests/Migrations/TierMigrationTests.cs` | **New** — backfill correctness: (true,true)→read_write, (true,false)→read, (false,false)→existence |

### 4.5 Admin UI — Access Tier Management

**Modify: `Components/Pages/Notebooks/View.razor` — Participants panel (from Hush-1)**
- Replace read/write checkboxes with tier dropdown (`existence`, `read`, `read_write`, `admin`)
- Show effective access column: tier from direct grant vs. tier from group membership (whichever is higher)
- Show "Your access" badge in the notebook header indicating the current user's effective tier

**Modify: `Components/Pages/Admin/GroupDetail.razor`**
- Members table: add "Default Tier" column — the tier group members inherit when accessing notebooks owned by this group
- Edit default tier per membership role (member → `read`, admin → `admin`, or custom)

---

## Hush-5: Agent Trust

**Goal:** ThinkerAgent instances carry security labels; job routing respects classification boundaries.

### 5.1 Agent Registration

**Migration:** `016_agent_registry.sql`

```sql
CREATE TABLE agents (
    id              TEXT PRIMARY KEY,          -- e.g. "thinker-boeing-secure-01"
    organization_id UUID NOT NULL REFERENCES organizations(id),
    max_level       classification_level NOT NULL DEFAULT 'INTERNAL',
    compartments    TEXT[] NOT NULL DEFAULT '{}',
    infrastructure  TEXT,                       -- description: "air-gapped", "cloud", etc.
    registered      TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen       TIMESTAMPTZ
);
```

### 5.2 Agent Authentication

**Modify:** ThinkerAgent token issuance

Agent tokens carry an `agent_id` claim in addition to `sub`. On job claim, the server looks up the agent's security label and verifies it dominates the notebook's label before returning a job.

### 5.3 Label-Aware Job Routing

**Modify:** `JobRepository.ClaimNextJobAsync`

Add filter: only return jobs from notebooks whose security label the claiming agent dominates.

```sql
WHERE n.classification <= agent_max_level
  AND n.compartments <@ agent_compartments
```

### 5.4 Agent Management Endpoints

**Create:** `Notebook.Server/Endpoints/AgentEndpoints.cs`

```
POST   /agents                    — register agent (admin only)
GET    /agents                    — list agents
PUT    /agents/{id}               — update agent label
DELETE /agents/{id}               — deregister agent
```

### Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Endpoints/AgentEndpointsTests.cs` | **New** — register, label update, deregister |
| `Notebook.Data.Tests/Repositories/JobRepositoryTests.cs` | **Add** — agent clearance filtering: agent below notebook level → no jobs returned |

### 5.5 Admin UI — Agent Management

**Page: `Components/Pages/Admin/Agents.razor`** — `/admin/agents`
- Table: agent ID, organization, max classification level, compartments, infrastructure description, last seen timestamp, status indicator (online/stale/offline based on `last_seen`)
- Filter by organization, classification level
- "Register Agent" form: ID, organization picker, classification level dropdown, compartments multi-input, infrastructure description
- Per-row actions: Edit label, Deregister (with confirmation)

**Page: `Components/Pages/Admin/AgentDetail.razor`** — `/admin/agents/{AgentId}`
- Agent header with full details
- **Job History** — recent jobs claimed and completed by this agent (from audit log if Hush-8 is available, otherwise from job table)
- **Accessible Notebooks** — list of notebooks whose classification this agent's label dominates (read-only, helps verify agent can reach intended notebooks)
- Edit form for classification level + compartments

---

## Hush-6: Inter-Thinktank Subscriptions

**Goal:** Higher-classified thinktanks can subscribe to lower-classified ones. Information flows up, never down.

**Full technical architecture:** [12-SUBSCRIPTION-ARCHITECTURE.md](12-SUBSCRIPTION-ARCHITECTURE.md)

### 6.1 Database Schema

Three migrations, all additive:

**Migration: `017_subscriptions.sql`**

```sql
CREATE TABLE notebook_subscriptions (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    subscriber_id     UUID NOT NULL REFERENCES notebooks(id),
    source_id         UUID NOT NULL REFERENCES notebooks(id),
    scope             TEXT NOT NULL DEFAULT 'catalog'
                          CHECK (scope IN ('catalog', 'claims', 'entries')),
    topic_filter      TEXT,                          -- optional topic prefix filter
    approved_by       BYTEA NOT NULL REFERENCES authors(id),

    -- Sync state
    sync_watermark    BIGINT NOT NULL DEFAULT 0,
    last_sync_at      TIMESTAMPTZ,
    sync_status       TEXT NOT NULL DEFAULT 'idle'
                          CHECK (sync_status IN ('idle', 'syncing', 'error', 'suspended')),
    sync_error        TEXT,
    mirrored_count    INTEGER NOT NULL DEFAULT 0,

    -- Tuning
    discount_factor   DOUBLE PRECISION NOT NULL DEFAULT 0.3
                          CHECK (discount_factor > 0 AND discount_factor <= 1.0),
    poll_interval_s   INTEGER NOT NULL DEFAULT 60
                          CHECK (poll_interval_s >= 10),
    embedding_model   TEXT,

    created           TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (subscriber_id, source_id)
);

CREATE INDEX idx_subscriptions_subscriber ON notebook_subscriptions(subscriber_id);
CREATE INDEX idx_subscriptions_source     ON notebook_subscriptions(source_id);
```

**Migration: `018_mirrored_content.sql`**

```sql
CREATE TABLE mirrored_claims (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    subscription_id   UUID NOT NULL REFERENCES notebook_subscriptions(id)
                          ON DELETE CASCADE,
    source_entry_id   UUID NOT NULL,
    notebook_id       UUID NOT NULL REFERENCES notebooks(id),
    claims            JSONB NOT NULL,
    topic             TEXT,
    embedding         DOUBLE PRECISION[],
    source_sequence   BIGINT NOT NULL,
    tombstoned        BOOLEAN NOT NULL DEFAULT false,
    mirrored_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (subscription_id, source_entry_id)
);

CREATE INDEX idx_mirrored_claims_notebook
    ON mirrored_claims(notebook_id)
    WHERE embedding IS NOT NULL AND NOT tombstoned;

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

**Migration: `019_embed_mirrored_job_type.sql`**

```sql
ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_job_type_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_job_type_check
    CHECK (job_type IN (
        'DISTILL_CLAIMS', 'COMPARE_CLAIMS',
        'CLASSIFY_TOPIC', 'EMBED_CLAIMS',
        'EMBED_MIRRORED'
    ));
```

Design rationale: mirrored content is stored in separate tables (not in `entries`) because it has a different lifecycle (no local claims_status progression, no local job chain), carries subscription provenance, and must never be confused with local content.

### 6.2 Subscription Validation

**Create:** `Notebook.Server/Services/SubscriptionService.cs`

On insert, enforce:
1. No self-subscription
2. Subscriber's classification level >= source's classification level
3. Subscriber's compartments ⊇ source's compartments
4. Both notebooks' owning organizations have a federation agreement (or same org)
5. No duplicate subscription (UNIQUE constraint)
6. No cycles — BFS from `source_id` following `subscriber_id → source_id` edges; reject if `subscriber_id` is reachable (see 12-SUBSCRIPTION-ARCHITECTURE.md §8.1)
7. Requesting principal has admin tier on subscriber notebook

### 6.3 Subscription Sync

**Create:** `Notebook.Server/Services/SubscriptionSyncService.cs` (BackgroundService)

A single polling loop replaces per-subscription timers for bounded resource usage regardless of subscription count.

**Loop (every 5 seconds):**
1. Query subscriptions due for sync:
   ```sql
   SELECT * FROM notebook_subscriptions
   WHERE sync_status != 'suspended'
     AND (last_sync_at IS NULL
          OR last_sync_at + (poll_interval_s * INTERVAL '1 second') < now())
   ORDER BY last_sync_at ASC NULLS FIRST
   LIMIT @max_concurrent - @currently_syncing;
   ```
2. Dispatch each to a bounded `SemaphoreSlim` worker pool (default max concurrency: 10, configurable via `SubscriptionSync:MaxConcurrency`)
3. Each worker executes the sync steps below

**Sync steps per subscription:**
1. Set `sync_status = 'syncing'`
2. Call source's `GET /notebooks/{sourceId}/observe?since={sync_watermark}` (auth: read-scoped JWT for source notebook)
3. For each new entry in response (batch size: 100):
   - **Catalog scope:** store topic + integration_cost metadata only (lightweight row in `mirrored_claims` with `claims = '[]'`)
   - **Claims scope:** fetch entry claims, UPSERT into `mirrored_claims`
   - **Entries scope:** fetch full entry, UPSERT into `mirrored_entries` + `mirrored_claims`
4. Queue `EMBED_MIRRORED` jobs for new/updated mirrored claims (priority 25)
5. Update `sync_watermark`, `last_sync_at`, `mirrored_count`, `sync_status = 'idle'`
6. On error: set `sync_status = 'error'`, `sync_error = message`

**Error backoff:** On consecutive errors for the same subscription, multiply `poll_interval_s` by 2^(error_count), capped at 1 hour. Reset on successful sync.

Idempotency: `UNIQUE (subscription_id, source_entry_id)` + UPSERT. Deletion handling: source entries removed → `tombstoned = true` (excluded from neighbor search via partial index). Revision handling: claims overwritten, new `EMBED_MIRRORED` job queued.

### 6.4 Air-Gapped Sync

For network-isolated thinktanks:

```
GET  /notebooks/{id}/export?since={seq}&scope={scope}  — Ed25519-signed JSON bundle
POST /notebooks/{id}/import                            — validate signature, process as sync batch
```

Bundle contains entries/claims with source notebook metadata + Ed25519 signature over canonical JSON. See 12-SUBSCRIPTION-ARCHITECTURE.md §3.4 for full bundle format.

### 6.5 Cross-Boundary Neighbor Search

**Create:** `EntryRepository.FindNearestWithMirroredAsync`

Extends the existing cosine similarity SQL with a `UNION ALL` against `mirrored_claims`. Returns `List<NeighborResult>` with `IsMirrored` and `SubscriptionId` fields. Existing `FindNearestByEmbeddingAsync` is unchanged. See 12-SUBSCRIPTION-ARCHITECTURE.md §4 for full SQL.

### 6.6 Pipeline Modifications

**EMBED_MIRRORED result handler** in `JobResultProcessor`:
- Updates `mirrored_claims.embedding` but does NOT trigger neighbor search. Mirrored claims are passive — found as neighbors when local entries are embedded.

**EMBED_CLAIMS handler modification:**
- Calls `FindNearestWithMirroredAsync` instead of `FindNearestByEmbeddingAsync`
- For mirrored neighbors: queues `COMPARE_CLAIMS` with additional payload fields `cross_boundary: true`, `subscription_id`, `discount_factor`

**COMPARE_CLAIMS handler modification:**
- Reads optional `cross_boundary` and `discount_factor` from payload
- Computes `effective_friction = friction * discount_factor` (default 1.0 when not cross-boundary)
- `AppendComparisonAsync` gains optional `discountFactor` parameter; `max_friction` update uses effective friction; raw friction preserved in stored comparison for audit

Agent routing: cross-boundary jobs are scoped to subscriber's `notebook_id`, so Hush-5 clearance filtering works automatically — no special routing needed.

### 6.7 Topology Validation

- **No cycles:** prevented at subscription creation (§6.2)
- **Transitive subscriptions:** A→B→C allowed. A sees B's local content only; mirrored content is not re-exported via observe. A must subscribe directly to C if desired.
- **Classification changes:** if source classification rises above subscriber's level, subscription is set to `sync_status = 'suspended'`. Admin must resolve (raise subscriber's level, delete subscription, or accept stale data).

### 6.8 Modified Browse/Search Responses

- `GET /notebooks/{id}/browse` gains optional `include_mirrored=true` — returns additional `mirrored_topics` array
- `POST /notebooks/{id}/semantic-search` automatically includes mirrored claims in results, tagged with `source: "mirrored"` and `subscription_id`
- `GET /notebooks/{id}/observe` unchanged — local entries only

### 6.9 API Endpoints

```
POST   /notebooks/{id}/subscriptions                — create subscription
DELETE /notebooks/{id}/subscriptions/{subId}         — remove + cascade delete mirrored data
GET    /notebooks/{id}/subscriptions                 — list (with sync status summary)
POST   /notebooks/{id}/subscriptions/{subId}/sync    — trigger immediate sync
GET    /notebooks/{id}/subscriptions/{subId}/status   — detailed sync status
GET    /notebooks/{id}/export?since={seq}&scope={scope}  — air-gapped export
POST   /notebooks/{id}/import                            — air-gapped import
```

### Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Services/SubscriptionServiceTests.cs` | **New** — validation: self-sub rejected, classification violation rejected, cycle rejected |
| `Notebook.Server.Tests/Services/SubscriptionSyncServiceTests.cs` | **New** — watermark advancement, tombstoning, error → status update, backoff |
| `Notebook.Data.Tests/Repositories/EntryRepositoryTests.cs` | **Add** — FindNearestWithMirrored returns mirrored results, respects tombstone filter |
| `Notebook.Server.Tests/Services/JobResultProcessorTests.cs` | **Add** — cross-boundary COMPARE_CLAIMS applies discount factor |

### 6.10 Admin UI — Subscription Management

**Modify: `Components/Pages/Notebooks/View.razor`**
- Add **Subscriptions** tab (visible to notebook admins):
  - **Subscribing to** — table of source notebooks: source name, scope, topic filter, sync status (`idle`/`syncing`/`error`/`suspended`), last sync time, mirrored count, watermark staleness indicator
  - **Subscribers** — table of higher-classified subscribers: subscriber name, scope, approved by
  - "Add Subscription" form: notebook picker (only shows notebooks at equal or lower classification), scope dropdown, optional topic filter, discount factor slider (0.1–1.0, default 0.3). Validates classification dominance before submission.
  - Per-row actions: "Sync Now" button (triggers immediate sync), "Unsubscribe" button (with confirmation warning about cascading deletion of mirrored data)

**Page: `Components/Pages/Admin/Subscriptions.razor`** — `/admin/subscriptions`
- Global overview: all active subscriptions across all notebooks
- Table: subscriber → source, scope, topic filter, sync status, last sync, mirrored count, discount factor
- Filter by organization, classification level, sync status
- Status indicators: green=idle, yellow=syncing, red=error, grey=suspended
- Bulk actions: "Resume All Suspended" (re-validates classification, resumes if valid)

### Files

| File | Change |
|---|---|
| `Notebook.Data/Migrations/017_subscriptions.sql` | **New** — subscription table with sync state |
| `Notebook.Data/Migrations/018_mirrored_content.sql` | **New** — mirrored_claims, mirrored_entries |
| `Notebook.Data/Migrations/019_embed_mirrored_job_type.sql` | **New** — extend job type constraint |
| `Notebook.Data/Repositories/SubscriptionRepository.cs` | **New** — CRUD, cycle detection, sync watermark updates |
| `Notebook.Data/Repositories/MirroredClaimsRepository.cs` | **New** — UPSERT, tombstone, embedding update |
| `Notebook.Data/Repositories/EntryRepository.cs` | Add `FindNearestWithMirroredAsync` (UNION query) |
| `Notebook.Server/Services/SubscriptionService.cs` | **New** — validation, create/delete logic |
| `Notebook.Server/Services/SubscriptionSyncService.cs` | **New** — BackgroundService, per-subscription sync loop |
| `Notebook.Server/Services/JobResultProcessor.cs` | Modify EMBED_CLAIMS + COMPARE_CLAIMS handlers |
| `Notebook.Server/Endpoints/SubscriptionEndpoints.cs` | **New** — CRUD + sync trigger + export/import |
| `Notebook.Core/Types/Subscription.cs` | **New** — domain types |
| `Notebook.Core/Types/NeighborResult.cs` | **New** — result record with IsMirrored flag |
| `Components/Pages/Notebooks/View.razor` | Add Subscriptions tab |
| `Components/Pages/Admin/Subscriptions.razor` | **New** — global subscription overview |

---

## Hush-7: Content Ingestion Gate

**Goal:** Enforce classification assertions on entry submission and review external contributions.

### 7.1 Classification Assertion

**Modify:** `BatchEntryRequest` — add optional `classification_assertion` field.

On write, verify:
- Submitter's clearance dominates the thinktank's label (already enforced by Hush-3)
- If `classification_assertion` is provided, it must not exceed the thinktank's label

### 7.2 External Contribution Review Queue

**Migration:** `020_review_queue.sql`

```sql
CREATE TABLE entry_reviews (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    notebook_id UUID NOT NULL REFERENCES notebooks(id),
    entry_id    UUID NOT NULL REFERENCES entries(id),
    submitter   BYTEA(32) NOT NULL REFERENCES authors(id),
    status      TEXT NOT NULL DEFAULT 'pending',   -- pending, approved, rejected
    reviewer    BYTEA(32) REFERENCES authors(id),
    reviewed_at TIMESTAMPTZ,
    created     TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE entries ADD COLUMN review_status TEXT DEFAULT 'approved';
-- 'pending' entries are excluded from entropy computation and browse
```

### 7.3 Review Workflow

- Entry from non-member enters with `review_status = 'pending'`
- Pending entries are stored but excluded from claims distillation, comparisons, and catalog
- Admin-tier user approves → `review_status = 'approved'` → DISTILL_CLAIMS job queued
- Rejection returns only "rejected" to submitter — no reason given (prevents information flow)

### 7.4 API Endpoints

```
GET    /notebooks/{id}/reviews              — list pending reviews (admin tier)
POST   /notebooks/{id}/reviews/{id}/approve — approve entry (admin tier)
POST   /notebooks/{id}/reviews/{id}/reject  — reject entry (admin tier)
```

### Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Endpoints/ReviewEndpointsTests.cs` | **New** — non-admin review → 403, approve queues DISTILL_CLAIMS, reject returns no reason |
| `Notebook.Server.Tests/Endpoints/BatchEndpointsTests.cs` | **Add** — non-member write → pending review_status, pending entry excluded from browse |

### 7.5 Admin UI — Review Queue

**Modify: `Components/Pages/Notebooks/View.razor`**
- Add pending review count badge on the notebook header (e.g., "3 pending reviews")
- Add **Reviews** tab (visible to admin-tier users):
  - List of pending entries: submitter, submission date, content preview (truncated), classification assertion if present
  - Expand row to see full entry content (read-only preview)
  - "Approve" button → confirms and queues DISTILL_CLAIMS job
  - "Reject" button → confirmation dialog (no reason field — by design, to prevent information flow)
  - Filter: pending / approved / rejected / all

**Modify: `Components/Pages/Admin/Dashboard.razor`**
- Add "Pending Reviews" summary card showing total pending reviews across all notebooks the admin can see
- Click-through to a filtered list of notebooks with pending reviews

---

## Hush-8: Audit

**Goal:** Full audit trail for all security-relevant operations.

### 8.1 Database Schema

**Migration:** `021_audit_log.sql`

```sql
CREATE TABLE audit_log (
    id          BIGSERIAL PRIMARY KEY,
    timestamp   TIMESTAMPTZ NOT NULL DEFAULT now(),
    actor       BYTEA(32),                          -- who (null for system actions)
    action      TEXT NOT NULL,                       -- 'read', 'write', 'share', 'revoke', etc.
    resource    TEXT NOT NULL,                       -- 'notebook:{id}', 'entry:{id}', 'agent:{id}'
    detail      JSONB,                               -- action-specific context
    ip_address  INET,
    user_agent  TEXT
);

CREATE INDEX idx_audit_log_timestamp ON audit_log (timestamp);
CREATE INDEX idx_audit_log_actor ON audit_log (actor);
CREATE INDEX idx_audit_log_resource ON audit_log (resource);
```

### 8.2 Audited Actions

| Action | Trigger |
|---|---|
| `entry.read` | Entry read via API |
| `entry.write` | Entry created via batch-write |
| `entry.review.approve` | External contribution approved |
| `entry.review.reject` | External contribution rejected |
| `notebook.create` | Notebook created |
| `notebook.delete` | Notebook deleted |
| `access.grant` | Share granted |
| `access.revoke` | Share revoked |
| `access.denied` | Principal attempted access above clearance |
| `agent.register` | ThinkerAgent registered |
| `agent.job.claim` | Agent claimed a job |
| `agent.job.complete` | Agent completed a job |
| `subscription.create` | Cross-thinktank subscription created |
| `subscription.delete` | Subscription removed (mirrored data cascaded) |
| `subscription.sync` | Subscription sync completed (watermark, mirrored count in detail) |
| `subscription.sync.error` | Subscription sync failed (error message in detail) |
| `subscription.suspend` | Subscription suspended due to classification change |
| `subscription.import` | Air-gapped bundle imported |
| `clearance.grant` | Principal clearance issued |
| `clearance.revoke` | Principal clearance revoked |

### 8.3 Implementation

**Create:** `Notebook.Server/Services/IAuditService.cs`, `AuditService.cs`

Injected into all endpoints and middleware.

**Write strategy: back-pressure with overflow**

1. `AuditService` maintains a `Channel<AuditEvent>` (bounded, capacity: 10,000)
2. API endpoints call `AuditService.LogAsync(event)` which writes to the channel. If the channel is full, the call blocks (back-pressure) — this slows the API rather than dropping events
3. A background consumer reads from the channel and batch-inserts into `audit_log` (batch size: 100, flush interval: 1 second, whichever comes first)
4. If the batch insert fails (DB down), events are serialized to a local append-only file (`audit-overflow-{date}.jsonl`). A recovery task replays overflow files on startup
5. Emit a metric (`audit_queue_depth`) for monitoring. Alert at 80% capacity

**Guarantee:** No audit event is silently dropped. Under sustained DB failure, the overflow file grows — operators must be alerted to restore DB connectivity.

### 8.4 Audit API

```
GET /audit?actor={authorId}&resource={prefix}&from={ts}&to={ts}&limit=100
```

Requires `notebook:admin` scope. Returns paginated audit log entries.

### Files

| File | Change |
|---|---|
| `Notebook.Server/Services/IAuditService.cs` | **New** — interface |
| `Notebook.Server/Services/AuditService.cs` | **New** — Channel + batch writer + overflow |
| `Notebook.Server/Services/AuditRecoveryService.cs` | **New** — replays overflow files on startup |

### Tests

| File | Covers |
|---|---|
| `Notebook.Server.Tests/Services/AuditServiceTests.cs` | **New** — event queued on write, back-pressure when full, overflow-to-file on DB failure, recovery replay |

### 8.5 Admin UI — Audit Log Viewer

**Page: `Components/Pages/Admin/AuditLog.razor`** — `/admin/audit`
- Paginated table: timestamp, actor (display name + author ID), action, resource, detail summary, IP address
- **Filters** (applied server-side):
  - Actor: user picker or author ID input
  - Action: multi-select dropdown (entry.read, entry.write, access.grant, access.denied, etc.)
  - Resource: text prefix input (e.g., `notebook:` to see all notebook actions)
  - Date range: from/to date pickers
- Row expansion to show full `detail` JSONB content
- Export to CSV button (for filtered results)

**Modify: `Components/Pages/Notebooks/View.razor`**
- Add **Audit** tab (visible to admin-tier users): shows audit log filtered to `resource=notebook:{id}`. Reuses the same table component as the global audit page.

**Modify: `Components/Pages/Admin/Dashboard.razor`**
- Add "Recent Security Events" card: last 10 `access.denied` events across the system. Quick indicator of blocked access attempts.

---

## Admin UI Summary

All admin UI lives in the Blazor frontend (`frontend/admin/`). The following table summarizes new pages and modified pages across all sub-phases.

### New Pages

| Page | Route | Sub-Phase | Purpose |
|---|---|---|---|
| `Admin/Organizations.razor` | `/admin/organizations` | Hush-2 | List/create organizations |
| `Admin/OrganizationDetail.razor` | `/admin/organizations/{OrgId}` | Hush-2 | Groups tree, members, clearances |
| `Admin/GroupDetail.razor` | `/admin/groups/{GroupId}` | Hush-2 | Members, child groups, owned notebooks |
| `Admin/Clearances.razor` | `/admin/clearances` | Hush-3 | Global clearance management |
| `Admin/Agents.razor` | `/admin/agents` | Hush-5 | Agent list, register, edit labels |
| `Admin/AgentDetail.razor` | `/admin/agents/{AgentId}` | Hush-5 | Agent details, job history, reachable notebooks |
| `Admin/Subscriptions.razor` | `/admin/subscriptions` | Hush-6 | Global subscription overview |
| `Admin/AuditLog.razor` | `/admin/audit` | Hush-8 | Filterable audit log viewer |

### Modified Pages

| Page | Sub-Phases | Changes |
|---|---|---|
| `Notebooks/View.razor` | Hush-1, 2, 3, 4, 6, 7, 8 | Participants panel, owning group, classification badge, access tier display, subscriptions tab (with sync status, sync-now, staleness), reviews tab, audit tab |
| `Notebooks/List.razor` | Hush-3 | Classification badge column, filter by level |
| `Admin/Dashboard.razor` | Hush-7, 8 | Pending reviews card, recent security events card |
| `Admin/OrganizationDetail.razor` | Hush-3 | Clearances tab |
| `Admin/GroupDetail.razor` | Hush-4 | Default tier column per membership |

### Shared Components

Consider extracting reusable Blazor components to reduce duplication:

| Component | Used By | Purpose |
|---|---|---|
| `ClassificationBadge.razor` | View, List, Agents, Subscriptions | Color-coded classification level pill |
| `CompartmentTags.razor` | View, Clearances, Agents | Compartment list as tag pills |
| `UserPicker.razor` | GroupDetail, Clearances, share panel | Dropdown to select a registered user |
| `AuditTable.razor` | AuditLog, View (audit tab) | Paginated/filterable audit log table |

## Implementation Order and Effort Estimates

| Sub-Phase | Risk | Notes |
|---|---|---|
| **Hush-1: Close the Gates** | Low | Mostly wiring existing schema to existing endpoints. Admin UI: participants panel on notebook view. |
| **Hush-8: Audit** | Low | Can start in parallel with Hush-1. Admin UI: audit log page + dashboard card. Useful from day one. |
| **Hush-2: Organizations & Groups** | Medium | New schema, new endpoints, DAG traversal logic. Admin UI: 3 new pages (heaviest UI work). |
| **Hush-3: Security Labels** | Medium | Core security primitive. Admin UI: clearances page, classification on notebook view/list. Must be correct — drives all downstream access decisions. |
| **Hush-4: Access Tiers** | Medium | Replaces current boolean ACL with richer model. Admin UI: upgrade share panel to tier dropdown. Must be backward-compatible during migration. |
| **Hush-5: Agent Trust** | Medium | Changes job routing. Admin UI: 2 new pages. Must handle gracefully when agents lack clearance for available jobs. |
| **Hush-7: Ingestion Gate** | Medium | New review workflow. Admin UI: reviews tab on notebook view, dashboard card. Changes entry lifecycle (pending → approved). |
| **Hush-6: Subscriptions** | High | Highest complexity: 3 migrations, sync service, pipeline mods, air-gapped export/import. Admin UI: subscriptions tab + global overview. Full architecture in [12-SUBSCRIPTION-ARCHITECTURE.md](12-SUBSCRIPTION-ARCHITECTURE.md). Requires Hush-3/4/5. Should be last. |

## Migration Path

The existing system has notebooks with no org/group/classification. The migration path:

1. **Hush-1** is purely additive — enforcing checks that currently don't happen. No schema changes needed beyond what exists.
2. **Hush-2** adds org/group tables with nullable `owning_group_id` on notebooks. Existing notebooks have `owning_group_id = NULL` (legacy mode — owner-based ACL still works).
3. **Hush-3** adds classification with default `INTERNAL`. Existing notebooks are `INTERNAL` with empty compartments. All existing principals get `INTERNAL` clearance by default.
4. **Hush-4** migrates `(read, write)` booleans to tier enum in two steps: 015a adds `tier` column and backfills (`(read=true, write=true)` → `read_write`, `(read=true, write=false)` → `read`), 015b drops old columns after verification.
5. **Hush-6** adds 3 migrations (017–019): subscription table, mirrored content tables, and EMBED_MIRRORED job type. Pipeline modifications (FindNearestWithMirroredAsync, COMPARE_CLAIMS discount factor) are backward-compatible — no subscriptions means identical behavior.
6. Subsequent phases (Hush-7, Hush-8) are purely additive (migrations 020–021).
