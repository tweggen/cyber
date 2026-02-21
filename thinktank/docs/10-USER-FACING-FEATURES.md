# User-Facing Features & UI Gap Analysis

This document catalogs every use case exposed to end users through the Admin UI and the backend API, and identifies where the UI has gaps relative to backend capabilities.

---

## 1. Identity & Authentication

### What users can do

| Use Case | UI | API | Notes |
|----------|:--:|:---:|-------|
| Register an account (username + password) | `/auth/register` | `POST /auth/register` | Also generates a random 32-byte key and calls `POST /authors` |
| Log in | `/auth/login` | `POST /auth/token` | Cookie-based (UI) or JWT exchange (API clients) |
| Log out | Sidebar link | `POST /auth/logout` | |
| View profile (display name, author ID) | `/profile` | — | Local Identity DB |
| Update display name and email | `/profile` | — | Local Identity DB |
| Generate CLI token | `/profile` | — | Client-side JWT generation |

### UI Gaps

- **Ed25519 key generation is a placeholder.** `AuthorService` generates random bytes instead of real Ed25519 keypairs. Users cannot manage or export their cryptographic identity.
- **No password change UI.** Users cannot change their password through the admin UI.
- **No author lookup.** `GET /authors/{id}` exists in the API but the UI has no way to look up other authors by ID.

---

## 2. Notebooks (CRUD)

### What users can do

| Use Case | UI | API | Notes |
|----------|:--:|:---:|-------|
| List notebooks (with permissions, entry counts) | `/notebooks` | `GET /notebooks` | |
| Create a notebook (name, classification, compartments) | `/notebooks` (inline form) | `POST /notebooks` | |
| Rename a notebook | `/notebooks` + `/notebooks/{id}` | `PATCH /notebooks/{id}` | Inline form, owner only |
| Delete a notebook (two-step confirmation) | `/notebooks` + `/notebooks/{id}` | `DELETE /notebooks/{id}` | Owner only |

### UI Gaps

- **No classification or compartment selection at creation.** The API accepts `classification` and `compartments` in `POST /notebooks`, but the UI form only has a name field. Users cannot create classified notebooks through the UI.
- **No way to view or change notebook classification after creation.** The notebook list shows classification/compartments in the API response but the UI table doesn't display them.
- **Notebook quota enforcement missing.** `QuotaService.CanCreateNotebookAsync()` exists but is never called — users can create unlimited notebooks regardless of their `MaxNotebooks` quota.

---

## 3. Entries (Read / Write / Revise)

### What users can do

| Use Case | UI | API | Notes |
|----------|:--:|:---:|-------|
| Create an entry (content, type, topic, references) | `/notebooks/{id}/entries/new` | `POST /notebooks/{id}/batch` | UI calls a single-entry variant |
| View entry detail (content, claims, comparisons, references) | `/notebooks/{id}/entries/{eid}` | `GET /notebooks/{id}/entries/{eid}` | Rich detail page |
| Revise an entry | `/notebooks/{id}/entries/{eid}` (inline form) | `PUT /notebooks/{id}/entries/{eid}` | |
| Browse entries (topic tree, sort, search, paginate) | `/notebooks/{id}` | `GET /notebooks/{id}/observe` | Client-side filtering |
| View entry fragments, revision chain, cross-references | `/notebooks/{id}/entries/{eid}` | `GET /notebooks/{id}/entries/{eid}` | |

### UI Coverage

- ✅ **Server-side full-text search** (DONE). Search box has "Server Search" button calling `GET /notebooks/{id}/search?query=...`. Results displayed in expandable card with: Topic (clickable), Snippet (with match location), and Relevance Score. Full-text indexing via Tantivy backend.
- ⚠️ **Semantic search missing.** API has `POST /notebooks/{id}/semantic-search` for vector-based nearest-neighbor search but no UI implemented.
- ⚠️ **Browse endpoint filters not exposed.** API supports rich filtering (claims_status, friction threshold, needs_review, integration_status, author, sequence range) but UI only does client-side topic/ID filtering.
- ℹ️ **Note on entry creation fields:** Single-entry API correctly limited to content, type, topic, references. Batch API supports `classification_assertion` and `source` but these are for bulk ingest, not individual entry creation.

---

## 4. Knowledge Pipeline (Jobs)

### What users can do

| Use Case | UI | API | Notes |
|----------|:--:|:---:|-------|
| Retry all failed jobs | `/notebooks/{id}` (button) | `POST /notebooks/{id}/jobs/retry-failed` | Owner only |

### UI Coverage

- ✅ **Job queue visibility** (DONE). Notebook view includes "Job Pipeline" section with:
  - Table showing pending/in_progress/completed/failed counts per job type: DISTILL_CLAIMS, COMPARE_CLAIMS, CLASSIFY_TOPIC, EMBED_CLAIMS
  - Refresh button to reload stats on demand
  - Owner-only access
- ℹ️ **Individual job management.** API supports claiming (`GET /jobs/next`), completing (`POST /jobs/{id}/complete`), and failing (`POST /jobs/{id}/fail`) for robot workers. Individual job visibility deferred (low priority for UI; worker operations are programmatic).

---

## 5. Access Control (Sharing)

### What users can do

| Use Case | UI | API | Notes |
|----------|:--:|:---:|-------|
| View participants and their permissions | `/notebooks/{id}` (sharing section) | `GET /notebooks/{id}/participants` | Owner only |
| Share a notebook (grant read or read+write) | `/notebooks/{id}` (inline form) | `POST /notebooks/{id}/share` | Owner only |
| Revoke access | `/notebooks/{id}` (revoke button) | `DELETE /notebooks/{id}/share/{authorId}` | Owner only |

### UI Coverage

- ✅ **All four sharing tiers** (DONE). Sharing section offers 4-tier dropdown:
  - `existence` (can see notebook exists) → gray badge
  - `read` (can read entries) → green badge
  - `read_write` (can write entries) → blue badge
  - `admin` (can share & manage) → red badge
  - Participants table shows tier with color-coded badges
- ✅ **Group-based access management** (DONE). Notebook view includes "Group Assignment" section (owner only):
  - Dropdown to assign/unassign notebook from owning group
  - Shows current owning group with link to group detail page
  - Unassign option to set group to null
  - Group membership determines access tier propagation (admin → AccessTier.Admin, member → ReadWrite)

---

## 6. Organizations & Groups

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| Create an organization | ✅ `/admin/organizations` | `POST /organizations` |
| List organizations | ✅ `/admin/organizations` | `GET /organizations` |
| Create a group | ✅ `/admin/organizations/{id}` | `POST /organizations/{orgId}/groups` |
| List groups (with DAG edges) | ✅ `/admin/organizations/{id}` | `GET /organizations/{orgId}/groups` |
| Delete a group | ✅ `/admin/organizations/{id}` | `DELETE /groups/{groupId}` |
| Add/remove parent-child edges | ✅ `/admin/organizations/{id}` | `POST /organizations/{orgId}/edges`, `DELETE /groups/{parentId}/edges/{childId}` |
| Add/remove group members | ✅ `/admin/groups/{id}` | `POST /groups/{groupId}/members`, `DELETE /groups/{groupId}/members/{authorId}` |
| List group members | ✅ `/admin/groups/{id}` | `GET /groups/{groupId}/members` |
| Assign notebook to owning group | ✅ `/notebooks/{id}` | `PUT /notebooks/{notebookId}/group` |

### UI Coverage

- ✅ **Fully implemented.** Organizations list page with create/delete. Organization detail page shows hierarchical DAG tree visualization of groups with expand/collapse. Group detail page manages members with role assignment (member/admin) and displays owned notebooks. Full CRUD across all 11 API endpoints.

---

## 7. Security Clearances

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| Grant clearance to an author | ✅ `/admin/organizations/{id}` | `POST /clearances` |
| Revoke clearance | ✅ `/admin/organizations/{id}` | `DELETE /clearances/{authorId}/{orgId}` |
| List clearances for an organization | ✅ `/admin/organizations/{id}` | `GET /organizations/{orgId}/clearances` |
| Flush clearance cache | ✅ `/admin/organizations/{id}` | `POST /admin/cache/flush` |

### UI Coverage

- ✅ **Fully implemented.** Clearances section on organization detail page shows all clearances for an organization. Displays: Author (hex ID), Max Level (5 levels: PUBLIC, INTERNAL, CONFIDENTIAL, SECRET, TOP_SECRET), Compartments (comma-separated), and Granted date. Grant new clearances form with hex author ID validation, level dropdown, and compartments input. Revoke button per clearance. Cache flush button for immediate effect.

---

## 8. Agent Management (ThinkerAgents)

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| Register a ThinkerAgent | ✅ `/admin/agents` | `POST /agents` |
| List agents | ✅ `/admin/agents` | `GET /agents` |
| View agent details | ✅ `/admin/agents` | `GET /agents/{agentId}` |
| Update agent security labels | ✅ `/admin/agents` | `PUT /agents/{agentId}` |
| Delete an agent | ✅ `/admin/agents` | `DELETE /agents/{agentId}` |

### UI Coverage

- ✅ **Fully implemented.** Agent management page shows table with: ID, Organization, Max Level (5 classification levels), Compartments, Infrastructure, Registered date, Last Seen. Inline edit form for updating security labels and infrastructure. Register new agent form with agent ID, organization selection, classification level, compartments, and infrastructure. Delete with confirmation. Full CRUD across all 5 API endpoints.

---

## 9. Subscriptions (Cross-Notebook Mirroring)

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| Subscribe to another notebook | ✅ `/notebooks/{id}` | `POST /notebooks/{id}/subscriptions` |
| List subscriptions | ✅ `/notebooks/{id}` | `GET /notebooks/{id}/subscriptions` |
| View subscription status (sync watermark, error) | ✅ `/notebooks/{id}` | `GET /notebooks/{id}/subscriptions/{subId}` |
| Trigger immediate sync | ✅ `/notebooks/{id}` | `POST /notebooks/{id}/subscriptions/{subId}/sync` |
| Delete a subscription | ✅ `/notebooks/{id}` | `DELETE /notebooks/{id}/subscriptions/{subId}` |

### UI Coverage

- ✅ **Fully implemented.** Subscriptions section on notebook view page (owner only) shows table with: Source Notebook (linked), Scope (catalog/claims/entries), Status (syncing/idle/error with color badges), Watermark (sequence number), Mirrored Count, Last Sync timestamp. Sync button for immediate trigger, Delete with confirmation. Add subscription form with Source Notebook ID validation, scope dropdown, discount factor (0.1-1.0), and poll interval (seconds). Full lifecycle management across all 5 API endpoints.

---

## 10. Content Review (Ingestion Gate)

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| List pending reviews | ✅ `/notebooks/{id}` | `GET /notebooks/{id}/reviews` |
| Approve an external contribution | ✅ `/notebooks/{id}` | `POST /notebooks/{id}/reviews/{reviewId}/approve` |
| Reject an external contribution | ✅ `/notebooks/{id}` | `POST /notebooks/{id}/reviews/{reviewId}/reject` |

### UI Coverage

- ✅ **Fully implemented.** Content Reviews section on notebook view page (owner only) shows pending review count as a badge. Reviews table displays: Entry (linked to entry detail), Submitter (with author display component), Status (pending/approved/rejected with color badges), Submitted date. Approve and Reject buttons for pending reviews with inline status updates. Critical for managing external contributions to group-owned notebooks.

---

## 11. Audit Trail

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| Query notebook-scoped audit log | ✅ `/notebooks/{id}` | `GET /notebooks/{id}/audit` |
| Query global audit log (with filters) | ✅ `/admin/audit` | `GET /audit` |

### UI Coverage

- ✅ **Fully implemented per spec (Hush-8 §8.5).** Comprehensive Audit Log Viewer at `/admin/audit` with:
  - **Advanced filtering panel:** Actor (hex ID), Action (e.g., entry.write), Resource (e.g., notebook:{id}), Date range (From/To)
  - **Audit log table:** ID, Timestamp, Action (color-coded: deny/reject/revoke → red, approve/grant → green, delete/remove → yellow, other → gray), Actor (hex ID), Target (type + ID), Notebook, Detail toggle
  - **Expandable detail rows:** Full JSON detail for each entry
  - **Pagination:** Cursor-based pagination with "Load More"
  - **CSV export:** Download audit entries as CSV file
  - Full search and filter capabilities with real-time updates

---

## 12. Dashboard & Administration

### What users can do

| Use Case | UI | API | Notes |
|----------|:--:|:---:|-------|
| View system statistics (users, notebooks, entries, entropy) | ✅ `/admin/dashboard` | `GET /notebooks` | |
| Manage users (lock/unlock) | ✅ `/admin/users` | — | Local Identity DB |
| Manage user quotas | ✅ `/admin/users/{id}/quotas`, `/profile` | — | Local PostgreSQL |

### UI Coverage

- ✅ **Security events card** (DONE). Dashboard includes "Recent Security Events" card showing the last 10 `access.denied` audit entries with timestamp, actor (truncated hex), and target. Color-coded by severity.
- ✅ **Proper admin authorization** (DONE). Dashboard uses `CurrentUserService` to get authenticated user's author ID. Page uses `@rendermode InteractiveServer` with `[CascadingParameter] Task<AuthenticationState>` for proper auth context.
- ✅ **About link removed** (DONE). Placeholder "About" link removed from MainLayout.razor.
- ✅ **Quota management** (DONE). Users can view quota usage on profile page showing Notebooks (owned/max), Max Entries per Notebook, Max Entry Size, Max Total Storage. Admins can manage quotas per user at `/admin/quotas/{userId}` with update form.

---

## Gap Summary

### Completely Missing UI Pages (0% coverage)

| Domain | API Endpoints | Priority | Impact | Status |
|--------|:------------:|:--------:|--------|:------:|
| **Semantic Search** | 1 | Low | Vector-based similarity search not exposed | ⚠️ Open |

*Note: All other major UI gaps have been closed. See "Fully Implemented Features" section below.*

### Fully Implemented Features

| Feature | Location | Coverage | Notes |
|---------|----------|:--------:|-------|
| **Organizations & Groups Management** | `/admin/organizations`, `/admin/organizations/{id}`, `/admin/groups/{id}` | 100% | Full hierarchy DAG with group CRUD, member management, notebook assignment |
| **Security Clearances** | `/admin/organizations/{id}` (Clearances section) | 100% | 5 classification levels (PUBLIC, INTERNAL, CONFIDENTIAL, SECRET, TOP_SECRET) + compartments, grant/revoke |
| **Content Reviews** | `/notebooks/{id}` (Content Reviews section) | 100% | Approve/reject workflow with submitter display and status tracking |
| **Agent Management** | `/admin/agents` | 100% | Full CRUD with security label management (max_level, compartments, infrastructure) |
| **Subscriptions** | `/notebooks/{id}` (Subscriptions section) | 100% | Full lifecycle: create, view status, trigger sync, delete |
| **Audit Trail** | `/admin/audit` | 100% | Advanced filtering by actor/action/resource, date range, pagination, CSV export |
| **Search (Full-Text)** | `/notebooks/{id}` (Search section) | 100% | Server-side Tantivy search with relevance scores and snippets |
| **Job Pipeline** | `/notebooks/{id}` (Job Pipeline section) | 100% | Real-time stats (DISTILL_CLAIMS, COMPARE_CLAIMS, CLASSIFY_TOPIC, EMBED_CLAIMS) + retry controls |
| **Sharing (4-Tier)** | `/notebooks/{id}` (Sharing section) | 100% | Existence, Read, Read+Write, Admin tiers with color-coded badges |
| **Group-Based Notebook Access** | `/notebooks/{id}` (Group Assignment section) | 100% | Assign/unassign notebooks to owning groups |
| **Dashboard** | `/admin/dashboard` | 100% | System stats + recent security events (last 10 denied access attempts) |
| **Notebook Quotas** | `/admin/quotas/{userId}`, `/profile` | 100% | View and manage quotas for entries, storage, notebooks |

### Partially Covered Features

| Feature | Status | What's Missing |
|---------|:------:|---------------|
| **Notebook creation** | ⚠️ Partial | Classification and compartment selection at creation (not exposed in UI) |
| **Entry creation** | ⚠️ Partial | Classification assertion and source fields (single-entry API doesn't support these; batch API does) |
| **Browse** | ⚠️ Partial | Rich server-side filters (claims_status, friction, integration_status, etc.) not exposed |
| **Search** | ✅ DONE | Server-side full-text search fully implemented. Semantic search still missing |

### Other Issues

| Issue | Location | Severity | Status |
|-------|----------|----------|:------:|
| Ed25519 key generation is a stub | Backend `AuthorService.cs` | Medium | ⚠️ Open (backend responsibility) |
| "About" link missing | `MainLayout.razor` | Low | ✅ FIXED (link removed as not needed) |
