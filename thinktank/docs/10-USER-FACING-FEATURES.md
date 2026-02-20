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

### UI Gaps

- **Search is client-side only.** The search box filters on topic and entry ID prefix locally. It does not call `GET /notebooks/{id}/browse?query=...` for server-side full-text search. The placeholder text "Search topic or content..." is misleading — content is not searched.
- **No semantic search UI.** The API has `POST /notebooks/{id}/semantic-search` for vector-based nearest-neighbor search, but there is no UI for it.
- **No batch claims retrieval UI.** `POST /notebooks/{id}/claims/batch` exists for fetching claims for up to 100 entries but is only used programmatically.
- **No classification assertion in entry creation.** The API supports `classification_assertion` on batch entries, but the entry creation form doesn't expose it.
- **No source/provenance field in entry creation.** The API supports a `source` field for content provenance (Wikipedia, Confluence, etc.), but the UI doesn't expose it.
- **Browse endpoint filters not exposed.** The API's `GET /notebooks/{id}/browse` supports rich filtering (claims_status, friction threshold, needs_review, integration_status, author, sequence range), but the UI only does client-side topic/ID filtering on the observe endpoint.

---

## 4. Knowledge Pipeline (Jobs)

### What users can do

| Use Case | UI | API | Notes |
|----------|:--:|:---:|-------|
| Retry all failed jobs | `/notebooks/{id}` (button) | `POST /notebooks/{id}/jobs/retry-failed` | Owner only |

### UI Gaps

- **No job queue visibility.** `GET /notebooks/{id}/jobs/stats` returns pending/in_progress/completed/failed counts per job type (DISTILL_CLAIMS, COMPARE_CLAIMS, CLASSIFY_TOPIC, EMBED_CLAIMS, EMBED_MIRRORED), but the UI has no dashboard or table showing this.
- **No individual job management.** The API supports claiming (`GET /jobs/next`), completing (`POST /jobs/{id}/complete`), and failing (`POST /jobs/{id}/fail`) individual jobs — these are for robot workers, but admins have no visibility into individual job status.

---

## 5. Access Control (Sharing)

### What users can do

| Use Case | UI | API | Notes |
|----------|:--:|:---:|-------|
| View participants and their permissions | `/notebooks/{id}` (sharing section) | `GET /notebooks/{id}/participants` | Owner only |
| Share a notebook (grant read or read+write) | `/notebooks/{id}` (inline form) | `POST /notebooks/{id}/share` | Owner only |
| Revoke access | `/notebooks/{id}` (revoke button) | `DELETE /notebooks/{id}/share/{authorId}` | Owner only |

### UI Gaps

- **Only two permission options exposed.** The UI offers "Read only" and "Read + Write", but the API supports four tiers: `existence`, `read`, `read_write`, `admin`. Users cannot grant `existence` or `admin` tier through the UI.
- **No group-based access management.** Notebooks can be assigned to owning groups (which auto-propagates read_write to group members), but the UI has no way to assign a notebook to a group or see which group owns it.

---

## 6. Organizations & Groups

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| Create an organization | — | `POST /organizations` |
| List organizations | — | `GET /organizations` |
| Create a group | — | `POST /organizations/{orgId}/groups` |
| List groups (with DAG edges) | — | `GET /organizations/{orgId}/groups` |
| Delete a group | — | `DELETE /groups/{groupId}` |
| Add/remove parent-child edges | — | `POST /organizations/{orgId}/edges`, `DELETE /groups/{parentId}/edges/{childId}` |
| Add/remove group members | — | `POST /groups/{groupId}/members`, `DELETE /groups/{groupId}/members/{authorId}` |
| List group members | — | `GET /groups/{groupId}/members` |
| Assign notebook to owning group | — | `PUT /notebooks/{notebookId}/group` |

### UI Gaps

- **No UI at all.** The entire Organizations & Groups system (11 API endpoints) has zero UI coverage. Users can only manage organizations, groups, memberships, and notebook-group assignment via direct API calls. This is the largest gap.

---

## 7. Security Clearances

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| Grant clearance to an author | — | `POST /clearances` |
| Revoke clearance | — | `DELETE /clearances/{authorId}/{orgId}` |
| List clearances for an organization | — | `GET /organizations/{orgId}/clearances` |
| Flush clearance cache | — | `POST /admin/cache/flush` |

### UI Gaps

- **No UI at all.** Classification and clearance management (4 API endpoints) is entirely API-only. Users cannot view or manage who has clearance to access classified notebooks through the admin UI.

---

## 8. Agent Management (ThinkerAgents)

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| Register a ThinkerAgent | — | `POST /agents` |
| List agents | — | `GET /agents` |
| View agent details | — | `GET /agents/{agentId}` |
| Update agent security labels | — | `PUT /agents/{agentId}` |
| Delete an agent | — | `DELETE /agents/{agentId}` |

### UI Gaps

- **No UI at all.** Agent registration and management (5 API endpoints) has zero UI. Users must use the API directly to register robot workers, set their max classification levels, and manage their compartments.

---

## 9. Subscriptions (Cross-Notebook Mirroring)

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| Subscribe to another notebook | — | `POST /notebooks/{id}/subscriptions` |
| List subscriptions | — | `GET /notebooks/{id}/subscriptions` |
| View subscription status (sync watermark, error) | — | `GET /notebooks/{id}/subscriptions/{subId}` |
| Trigger immediate sync | — | `POST /notebooks/{id}/subscriptions/{subId}/sync` |
| Delete a subscription | — | `DELETE /notebooks/{id}/subscriptions/{subId}` |

### UI Gaps

- **No UI at all.** Subscription management (5 API endpoints) is entirely API-only. Users cannot set up cross-notebook knowledge mirroring, monitor sync status, or manage subscription lifecycles through the admin UI.

---

## 10. Content Review (Ingestion Gate)

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| List pending reviews | — | `GET /notebooks/{id}/reviews` |
| Approve an external contribution | — | `POST /notebooks/{id}/reviews/{reviewId}/approve` |
| Reject an external contribution | — | `POST /notebooks/{id}/reviews/{reviewId}/reject` |

### UI Gaps

- **No UI at all.** The review queue (3 API endpoints) has no admin page. Notebook owners cannot see, approve, or reject external contributor submissions through the UI. This is critical for notebooks with owning groups — external contributions are silently blocked with no way to release them through the UI.

---

## 11. Audit Trail

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| Query notebook-scoped audit log | — | `GET /notebooks/{id}/audit` |
| Query global audit log (with filters) | — | `GET /audit` |

### UI Gaps

- **No UI at all.** The audit trail (2 API endpoints) has no viewer page. The spec (Hush-8 §8.5) explicitly calls for an Audit Log Viewer page at `/admin/audit` with filters, row expansion, and CSV export.

---

## 12. Dashboard & Administration

### What users can do

| Use Case | UI | API | Notes |
|----------|:--:|:---:|-------|
| View system statistics (users, notebooks, entries, entropy) | `/admin/dashboard` | `GET /notebooks` | |
| Manage users (lock/unlock) | `/admin/users` | — | Local Identity DB |
| Manage user quotas | `/admin/users/{id}/quotas` | — | Local PostgreSQL |

### UI Gaps

- **Dashboard has no security events.** The spec calls for a "Recent Security Events" card showing the last 10 `access.denied` events. The current dashboard only shows aggregate counts.
- **Dashboard uses zero-author admin mode.** It calls `GET /notebooks` with `000...000` as author, which is a workaround rather than proper admin authorization.
- **About link is a placeholder.** The top-bar "About" links to Microsoft ASP.NET Core docs — should be removed or replaced.

---

## Gap Summary

### Completely Missing UI Pages (0% coverage)

| Domain | API Endpoints | Priority | Impact |
|--------|:------------:|:--------:|--------|
| **Organizations & Groups** | 11 | High | Users cannot manage the identity hierarchy that powers group-based access propagation |
| **Security Clearances** | 4 | High | Users cannot assign who can access classified notebooks |
| **Content Reviews** | 3 | High | External contributions pile up with no way to approve/reject them |
| **Agent Management** | 5 | Medium | Robot workers must be managed via API |
| **Subscriptions** | 5 | Medium | Cross-notebook mirroring is invisible and unmanageable |
| **Audit Trail** | 2 | Medium | No visibility into security-relevant events |

### Partially Covered Features

| Feature | What's Missing |
|---------|---------------|
| **Notebook creation** | Classification and compartment selection |
| **Entry creation** | Classification assertion, source/provenance field |
| **Sharing** | `existence` and `admin` tiers, group assignment |
| **Search** | Server-side full-text search, semantic search |
| **Browse** | Rich server-side filters (claims_status, friction, integration_status) |
| **Job pipeline** | Queue stats dashboard, per-job-type visibility |
| **Dashboard** | Security events card, proper admin authorization |

### Other Issues

| Issue | Location | Severity |
|-------|----------|----------|
| Ed25519 key generation is a stub | `AuthorService.cs` | Medium |
| Notebook quota not enforced on create | `List.razor` | Low |
| Profile notebook count error silently swallowed | `Profile.razor` | Low |
| Search placeholder text misleading | `View.razor` | Low |
| "About" link is a template placeholder | `MainLayout.razor` | Low |
