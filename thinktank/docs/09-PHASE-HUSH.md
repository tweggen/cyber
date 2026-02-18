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
| Hush-6 | **Subscriptions** | Inter-thinktank catalog/claim flow with classification boundary enforcement | Hush-4, Hush-5 |
| Hush-7 | **Ingestion Gate** | Classification assertions, external contribution review queue | Hush-4 |
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

Operations: CRUD for orgs, groups, edges, memberships. DAG traversal (ancestors, descendants). Cycle detection on edge insert.

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

### 3.4 Notebook Creation with Label

**Modify:** `POST /notebooks` — accept `classification` and `compartments` fields. Validate that the creating principal's clearance dominates the requested label.

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

**Migration:** `015_access_tiers.sql`

```sql
ALTER TABLE notebook_access ADD COLUMN tier TEXT NOT NULL DEFAULT 'read_write';
-- Tiers: 'existence', 'read', 'read_write', 'admin'
-- Replaces the current (read, write) booleans

ALTER TABLE notebook_access DROP COLUMN read;
ALTER TABLE notebook_access DROP COLUMN write;
```

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

### 6.1 Database Schema

**Migration:** `017_subscriptions.sql`

```sql
CREATE TABLE notebook_subscriptions (
    subscriber_id   UUID NOT NULL REFERENCES notebooks(id),  -- higher classification
    source_id       UUID NOT NULL REFERENCES notebooks(id),  -- lower classification
    scope           TEXT NOT NULL DEFAULT 'catalog',          -- 'catalog', 'claims', 'entries'
    topic_filter    TEXT,                                      -- optional topic prefix filter
    approved_by     BYTEA(32) REFERENCES authors(id),
    created         TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (subscriber_id, source_id)
);
```

### 6.2 Subscription Validation

On insert, enforce:
- Subscriber's classification level >= source's classification level
- Subscriber's compartments ⊇ source's compartments
- Both notebooks' owning organizations have a federation agreement (or same org)
- Approved by an admin of the subscriber notebook

### 6.3 Subscription Sync

**Create:** `Notebook.Server/Services/SubscriptionSyncService.cs` (BackgroundService)

Periodically pulls from source notebooks:
- **Catalog scope:** pulls catalog summaries, stores as read-only reference entries
- **Claims scope:** mirrors distilled claims, available for COMPARE_CLAIMS
- **Entries scope:** mirrors full entries (most permissive)

Sync uses the source notebook's OBSERVE endpoint with a stored causal position watermark.

### 6.4 Comparison Cascade Across Subscriptions

When embedding nearest-neighbor finds a subscribed entry, the COMPARE_CLAIMS job is routed to an agent cleared for the *subscriber's* level (which dominates the source's level by construction).

### 6.5 API Endpoints

```
POST   /notebooks/{id}/subscriptions              — subscribe to source
DELETE /notebooks/{id}/subscriptions/{sourceId}    — unsubscribe
GET    /notebooks/{id}/subscriptions               — list subscriptions
```

### 6.6 Admin UI — Subscription Management

**Modify: `Components/Pages/Notebooks/View.razor`**
- Add **Subscriptions** tab (visible to notebook admins):
  - **Subscribing to** — table of source notebooks this notebook pulls from: source name, scope (catalog/claims/entries), topic filter, sync status (last sync time, entry count)
  - **Subscribers** — table of higher-classified notebooks that subscribe to this notebook: subscriber name, scope, approved by
  - "Add Subscription" form: notebook picker (only shows notebooks at equal or lower classification), scope dropdown, optional topic filter. Validates classification dominance before submission.
  - Unsubscribe button per row (with confirmation)

**Page: `Components/Pages/Admin/Subscriptions.razor`** — `/admin/subscriptions`
- Global overview: all active subscriptions across all notebooks
- Table: subscriber → source, scope, topic filter, last sync, entry count
- Filter by organization, classification level
- Useful for admins to audit cross-thinktank information flow

---

## Hush-7: Content Ingestion Gate

**Goal:** Enforce classification assertions on entry submission and review external contributions.

### 7.1 Classification Assertion

**Modify:** `BatchEntryRequest` — add optional `classification_assertion` field.

On write, verify:
- Submitter's clearance dominates the thinktank's label (already enforced by Hush-3)
- If `classification_assertion` is provided, it must not exceed the thinktank's label

### 7.2 External Contribution Review Queue

**Migration:** `018_review_queue.sql`

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

**Migration:** `019_audit_log.sql`

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
| `clearance.grant` | Principal clearance issued |
| `clearance.revoke` | Principal clearance revoked |

### 8.3 Implementation

**Create:** `Notebook.Server/Services/IAuditService.cs`, `AuditService.cs`

Injected into all endpoints and middleware. Writes async (fire-and-forget with bounded queue) to avoid latency impact on API responses.

### 8.4 Audit API

```
GET /audit?actor={authorId}&resource={prefix}&from={ts}&to={ts}&limit=100
```

Requires `notebook:admin` scope. Returns paginated audit log entries.

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
| `Notebooks/View.razor` | Hush-1, 2, 3, 4, 6, 7, 8 | Participants panel, owning group, classification badge, access tier display, subscriptions tab, reviews tab, audit tab |
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
| **Hush-6: Subscriptions** | High | Highest complexity. Admin UI: subscriptions tab on notebook view + global overview page. Cross-thinktank data flow, sync consistency, classification boundary enforcement. Should be last. |

## Migration Path

The existing system has notebooks with no org/group/classification. The migration path:

1. **Hush-1** is purely additive — enforcing checks that currently don't happen. No schema changes needed beyond what exists.
2. **Hush-2** adds org/group tables with nullable `owning_group_id` on notebooks. Existing notebooks have `owning_group_id = NULL` (legacy mode — owner-based ACL still works).
3. **Hush-3** adds classification with default `INTERNAL`. Existing notebooks are `INTERNAL` with empty compartments. All existing principals get `INTERNAL` clearance by default.
4. **Hush-4** migrates `(read, write)` booleans to tier enum. `(read=true, write=true)` → `read_write`. `(read=true, write=false)` → `read`.
5. Subsequent phases are purely additive.
