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

- ✅ **Server-side full-text search** (DONE). Search box has "Server Search" button calling `GET /notebooks/{id}/search?query=...`. Results displayed in expandable card with: Topic (clickable), Snippet (with match location), and Relevance Score. Full-text indexing via backend search endpoint.
- ✅ **Browse filters** (DONE). Collapsible filter panel on notebook view with basic filters (Topic Prefix, Integration Status, Claims Status, Needs Review) and advanced filters (Min Friction, Author ID, Sequence range). Filtered results table displays entries with status badges, color-coded friction levels, and pagination. Backend API fully supports all 11 filter parameters.
- ⚠️ **Semantic search** (NOT YET FULLY IMPLEMENTED). Backend has EmbeddingService configured (Ollama integration). ClaimsEndpoints use embeddings for claim similarity, but semantic search UI not exposed. Backend endpoint exists but frontend search UI for semantic queries not implemented.
- ⚠️ **Batch entry creation** (BACKEND DONE). Backend BatchEndpoints support creating multiple entries with classification_assertion and source fields. Single-entry API correctly limited to content, type, topic, references. Frontend batch UI not exposed yet.

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

## 11. Audit Trail (Phase 4: Advanced Filtering & Reporting)

### What users can do

| Use Case | UI | API |
|----------|:--:|:---:|
| Query notebook-scoped audit log | ✅ `/notebooks/{id}` | `GET /notebooks/{id}/audit` |
| Query global audit log (with advanced filters) | ✅ `/admin/audit` | `GET /audit` |

### UI Coverage

- ✅ **Fully implemented with Phase 4 enhancements.** Comprehensive Audit Log Viewer at `/admin/audit` with:
  - **Advanced filtering panel (collapsible):**
    - Date range (From/To dates)
    - Actor filtering (username/ID)
    - Action type (Create, Update, Delete, Lock, Unlock, Login, Export, Import)
    - Target type (User, Notebook, Entry, Organization)
    - Full-text search in audit details
    - Page size selector (25, 50, 100, 250)
    - Sort options (Date, Action, Actor, Target) with ascending/descending
  - **Analytics dashboard with 5 statistics cards:**
    - Total Actions count
    - Unique Actors count
    - Success Rate percentage
    - Most Common Action
    - Date Range of filtered results
  - **Audit log table:** Timestamp, Actor (hex ID), Action (color-coded badges), Target (type + ID), Details (truncated)
  - **Pagination:** Previous/Next navigation with page indicators
  - **Export functionality:**
    - CSV export with all fields (timestamp, actor_id, action, target_type, target_id, notebook_id, details)
    - JSON export with full filter context and metadata
  - Full search, filter, sort, and export capabilities with real-time updates

---

## 12. Dashboard & Administration (Phase 0-4)

### What users can do

| Use Case | UI | API | Notes | Phase |
|----------|:--:|:---:|-------|:-----:|
| View system statistics (users, notebooks, entries, entropy) | ✅ `/admin/dashboard` | `GET /notebooks` | | 0 |
| Search & filter users | ✅ `/admin/users` | — | Search by username/email/display name, filter by type/lock status | 1 |
| Manage users (lock/unlock) | ✅ `/admin/users`, `/admin/users/{id}` | — | Lock with predefined reasons, view lock history | 1 |
| View user metadata | ✅ `/admin/users` | — | Created date, last login, user type, lock reason | 1 |
| Manage user quotas | ✅ `/admin/users/{id}/quotas`, `/profile` | — | View usage, update limits, see organization defaults | 1-2 |
| Set organization quotas | ✅ `/admin/organizations/{id}` | — | Default quotas per organization, 10x user defaults | 2 |
| Import users from CSV | ✅ `/admin/users/import` | — | Upload CSV with validation, create multiple users, generate temp passwords | 3 |
| Export users to CSV | ✅ `/admin/users` | — | Download user list with quotas and metadata | 3 |
| View audit trail | ✅ `/admin/audit` | `GET /audit` | Advanced filters, statistics, CSV/JSON export | 4 |

### UI Coverage

- ✅ **Security events card** (Phase 0). Dashboard includes "Recent Security Events" card showing the last 10 `access.denied` audit entries with timestamp, actor (truncated hex), and target. Color-coded by severity.
- ✅ **Proper admin authorization** (Phase 0). Dashboard uses `CurrentUserService` to get authenticated user's author ID. Page uses `@rendermode InteractiveServer` with `[CascadingParameter] Task<AuthenticationState>` for proper auth context.
- ✅ **User search and filtering** (Phase 1). User list page at `/admin/users` with:
  - Search box for username/email/display name with real-time filtering
  - Filter dropdowns: User Type (Human, Service Account, Bot), Lock Status (Active, Locked)
  - Results table showing: Username, Display Name, Email, User Type, Created, Last Login, Lock Status
  - Click user row to view detail page with full metadata
- ✅ **User lock management** (Phase 1). User detail page with:
  - Lock button opening modal with predefined lock reasons (Account Compromise, Policy Violation, Compliance Hold, Inactive, Other)
  - Lock reason text field for custom notes
  - Unlock button for locked accounts
  - Lock history tracking (reason, timestamp)
- ✅ **Quota management & visualization** (Phase 1-2).
  - User detail page shows quota progress bars: Notebooks (owned/max), Max Entries per Notebook, Max Entry Size, Max Total Storage
  - Quota edit page at `/admin/users/{id}/quotas` shows "Current Usage" card with real-time usage stats
  - Color-coded progress bars: Green (<75%), Yellow (75-90%), Red (≥90%)
  - Organization detail page at `/admin/organizations/{id}` shows "Default Quota Limits" section with editable organization quotas
  - Quota inheritance automatically applies (User → Organization → System defaults)
- ✅ **Batch user import/export** (Phase 3).
  - Import page at `/admin/users/import` with CSV file upload
  - Pre-import validation shows row-by-row errors with line numbers
  - Successful import displays temporary passwords in secure table
  - CSV download on Users list page exports all user data with columns: username, email, display_name, user_type, author_id_hex, account_created, last_login, lock_status, lock_reason, max_notebooks, max_entries_per_notebook, max_entry_size_bytes, max_total_storage_bytes
  - CSV import supports: username (required), user_type (required), email, display_name, lock_status, lock_reason, quota fields
  - Temporary password generation with secure display and copy functionality
- ✅ **Advanced audit trail** (Phase 4). Audit page at `/admin/audit` with collapsible filter panel, statistics dashboard, pagination, and CSV/JSON export (see section 11).

---

## Gap Summary

### Architecture Note

**Backend:** .NET 10 (Notebook.Server in thinktank/src/). Contains full implementation of all planned features:
- Browse filters with all parameters (topic_prefix, claims_status, integration_status, friction, etc.)
- Batch entry creation with classification fields
- Semantic search via EmbeddingService (Ollama integration)
- Comprehensive endpoint coverage

**Frontend:** .NET Blazor Server (frontend/admin/).
- **14 feature domains fully implemented and exposed in UI** (88%)
- **2 feature domains partially covered** - backend ready, frontend UI not exposed (12%)
- Old Rust backend (notebook/) is first-generation; currently maintained for reference only

### Implementation Coverage Summary

**Total Feature Domains:** 16

| Status | Count | Percentage |
|--------|:-----:|:----------:|
| ✅ Fully Implemented (Backend + Frontend) | 14 | 88% |
| ⚠️ Partially Covered (Backend Done, Frontend Pending) | 2 | 12% |
| ❌ Not Supported | 0 | 0% |

**Partially Covered Features:** Batch entry creation, Semantic search (UI)

### Completely Missing UI Pages (0% coverage)

None at this time. All planned features have backend implementations.

### Fully Implemented Features

| Feature | Location | Coverage | Phase | Notes |
|---------|----------|:--------:|:-----:|-------|
| **Organizations & Groups Management** | `/admin/organizations`, `/admin/organizations/{id}`, `/admin/groups/{id}` | 100% | 0 | Full hierarchy DAG with group CRUD, member management, notebook assignment |
| **Security Clearances** | `/admin/organizations/{id}` (Clearances section) | 100% | 0 | 5 classification levels (PUBLIC, INTERNAL, CONFIDENTIAL, SECRET, TOP_SECRET) + compartments, grant/revoke |
| **Content Reviews** | `/notebooks/{id}` (Content Reviews section) | 100% | 0 | Approve/reject workflow with submitter display and status tracking |
| **Agent Management** | `/admin/agents` | 100% | 0 | Full CRUD with security label management (max_level, compartments, infrastructure) |
| **Subscriptions** | `/notebooks/{id}` (Subscriptions section) | 100% | 0 | Full lifecycle: create, view status, trigger sync, delete |
| **Audit Trail with Advanced Filtering & Reporting** | `/admin/audit` | 100% | 4 | Advanced filters (date, actor, action, target), statistics dashboard (total actions, unique actors, success rate, most common action, date range), pagination, CSV/JSON export |
| **Search (Full-Text)** | `/notebooks/{id}` (Search section) | 100% | 0 | Server-side Tantivy search with relevance scores and snippets |
| **Job Pipeline** | `/notebooks/{id}` (Job Pipeline section) | 100% | 0 | Real-time stats (DISTILL_CLAIMS, COMPARE_CLAIMS, CLASSIFY_TOPIC, EMBED_CLAIMS) + retry controls |
| **Sharing (4-Tier)** | `/notebooks/{id}` (Sharing section) | 100% | 0 | Existence, Read, Read+Write, Admin tiers with color-coded badges |
| **Group-Based Notebook Access** | `/notebooks/{id}` (Group Assignment section) | 100% | 0 | Assign/unassign notebooks to owning groups |
| **Browse Filters** | `/notebooks/{id}` (Browse & Filter section) | 100% | 0 | Collapsible filter panel with basic and advanced filters, color-coded status badges, friction levels, and pagination |
| **Dashboard & User Management** | `/admin/dashboard`, `/admin/users`, `/admin/users/{id}` | 100% | 1 | System stats, user search/filter, lock management with reasons, metadata tracking, user detail page |
| **Quota Management with Organization Defaults** | `/admin/users/{id}/quotas`, `/admin/organizations/{id}`, `/profile` | 100% | 1-2 | User and organization quotas, inheritance chain, usage progress bars, quota defaults |
| **Batch User Import/Export** | `/admin/users/import`, `/admin/users` | 100% | 3 | CSV import with validation and temporary password generation, CSV export with full user data and quotas |

### Partially Covered Features (Frontend Implementation Needed)

| Feature | Status | What's Missing | Notes |
|---------|:------:|---------------|-------|
| **Batch entry creation** | ⚠️ Partial | Frontend batch upload UI | Backend BatchEndpoints fully support multiple entries with classification_assertion and source fields. Single-entry UI exists; batch UI not exposed. |
| **Semantic search** | ⚠️ Partial | Frontend semantic search UI | Backend has EmbeddingService and semantic capabilities. Frontend search box only does full-text; semantic query option not exposed. |

### Other Issues

| Issue | Location | Severity | Status |
|-------|----------|----------|:------:|
| Ed25519 key generation is a stub | Backend `AuthorService.cs` | Medium | ⚠️ Open (backend responsibility) |
| "About" link missing | `MainLayout.razor` | Low | ✅ FIXED (link removed as not needed) |
| Notebook creation classification UI | `/notebooks` creation form | Low | 📋 Proposed for Phase 5+ |
