# Admin Panel Implementation Plan

**Status:** Phase 0 ✅ COMPLETE | Phase 1 ✅ COMPLETE | Phase 2-7 🔮 PLANNED

## Motivation

The user manual (chapters 05, 07, 08, 10, 12) describes a cohesive **Admin Panel** as the
central hub for system administration. The current frontend has individual admin pages
reachable from the main nav sidebar, but lacks:

- A unified `/admin` entry point
- Several workflows described in the manual
- Dedicated sections for subscriptions, compliance, and system monitoring

This document maps every admin feature in the manual to what exists today, identifies gaps,
and proposes a phased implementation plan.

---

## Current State

### Implemented Pages

| Page | Route | Status |
|------|-------|--------|
| Dashboard | `/admin/dashboard` | Stats, activity, notebooks, security events, jobs |
| Users | `/admin/users` | List with lock/unlock, edit link |
| User Detail | `/admin/users/{id}` | Profile edit, notebooks, activity, status, password reset, delete |
| Quota Management | `/admin/users/{id}/quotas` | Edit per-user quota limits |
| Organizations | `/admin/organizations` | List/create orgs |
| Organization Detail | `/admin/organizations/{id}` | Groups, edges, clearances |
| Group Detail | `/admin/groups/{id}` | Members, notebook assignments |
| Agents | `/admin/agents` | Register, edit, delete agents |
| Audit | `/admin/audit` | Search/filter/export audit logs |
| Crawler Config | `/crawlers/configure/{id}` | Per-notebook crawler setup |
| Crawler Runs | `/crawlers/runs/{id}` | Run history per notebook |

### Navigation

Admin pages appear as individual items in the main sidebar (Users, Organizations, Agents,
Audit, Crawlers). There is no grouped "Admin" section or sub-navigation.

---

## Gap Analysis by Manual Chapter

### Chapter 8 — System Administrator

#### Workflow 1: User Management (partial)

| Feature | Status | Notes |
|---------|--------|-------|
| Create user accounts | Exists | Via `/auth/register` |
| Search/filter users | **Missing** | No search bar or filters on user list |
| User type selection (Human/Service/Bot) | **Missing** | Not stored on `ApplicationUser` — needs model change |
| Lock with reason dropdown | **Missing** | Current lock has no reason field |
| Silent lock option | **Missing** | No silent-lock flag |
| Send activation/password emails | **Missing** | No email service configured |
| Bulk import via CSV | **Missing** | No import UI or endpoint |
| Last login / created date display | **Missing** | Not tracked on `ApplicationUser` |
| Quota usage monitoring (bars/percentages) | **Missing** | Quotas exist but no actual-usage aggregation |

#### Workflow 2: Quota Management (partial)

| Feature | Status | Notes |
|---------|--------|-------|
| Per-user quota editing | Exists | QuotaManagement.razor |
| Default organization quotas | **Missing** | No org-level quota defaults |
| Quota usage progress bars | **Missing** | Need usage data from notebook API |
| Projected exhaustion | **Missing** | Need usage trends |
| Enforcement policies (warn/block/lock) | **Missing** | QuotaService checks but no policy config UI |
| Archive old entries | **Missing** | No archive API |

#### Workflow 3: System Monitoring (mostly missing)

| Feature | Status | Notes |
|---------|--------|-------|
| Uptime / response time / error rate | **Missing** | Dashboard has health text, no metrics |
| Server status (API, DB, cache, jobs) | **Missing** | Job stats exist; no CPU/mem/replication stats |
| Performance trends (charts) | **Missing** | No charting library integrated |
| Alert configuration | **Missing** | No alert model or notification system |
| Incident tracking | **Missing** | No incident model |

#### Workflow 4: Agent Management (partial)

| Feature | Status | Notes |
|---------|--------|-------|
| Register / edit / delete agents | Exists | Agents.razor |
| Agent health dashboard (status, heartbeat) | **Missing** | Agent model has `LastSeen` but no health card |
| Performance metrics (CPU, jobs, avg time) | **Missing** | Not exposed by notebook API |
| Fleet management / load balancing view | **Missing** | No fleet overview |
| Credential rotation | **Missing** | No rotate-credentials endpoint |
| Pause / resume agents | **Missing** | No pause state |

#### Workflow 5: Crawler Infrastructure (partial)

| Feature | Status | Notes |
|---------|--------|-------|
| Per-notebook crawler config | Exists | ConfigureCrawler.razor |
| Run history | Exists | CrawlerRuns.razor |
| Fleet-wide crawler health dashboard | **Missing** | No cross-notebook crawler overview |
| Failure monitoring / alerts | **Missing** | Need aggregated failure view |
| DB growth monitoring | **Missing** | No storage stats API |

### Chapter 5 — Organization Administrator

| Feature | Status | Notes |
|---------|--------|-------|
| Group hierarchy (DAG) | Exists | OrganizationDetail.razor |
| Member management | Exists | GroupDetail.razor |
| Clearance management | Exists | OrganizationDetail.razor |
| Agent config per org | Partial | Agents page is global, not org-scoped |
| Confluence crawler config | Exists | ConfigureCrawler.razor |

### Chapter 7 — Auditor / Compliance Officer

| Feature | Status | Notes |
|---------|--------|-------|
| Global audit search with filters | Exists | Audit.razor |
| CSV export | Exists | Audit.razor |
| Status filter (Success/Failure/Denied) | **Missing** | API may not expose this field |
| Generate compliance reports | **Missing** | No report builder |
| Per-organization audit scoping | **Missing** | Audit page is global only |

### Chapter 10 — Cross-Organization Coordinator

| Feature | Status | Notes |
|---------|--------|-------|
| Create subscriptions | **Missing** | API exists (`CreateSubscriptionAsync`) but no admin UI |
| Subscription dashboard | **Missing** | No subscription list/monitor page |
| Sync health monitoring | **Missing** | No watermark/sync status display |
| Force sync / advance watermark | **Missing** | API exists but no UI |
| Classification compliance matrix | **Missing** | No compliance visualization |
| Compliance report export | **Missing** | No report generation |

### Chapter 12 — UI Reference

| Feature | Status | Notes |
|---------|--------|-------|
| Dedicated Admin Panel entry point | **Missing** | Admin pages in main nav, no `/admin` hub |
| Admin sub-navigation / sidebar | **Missing** | Uses global nav |
| Recommended Actions widget | Exists | Dashboard.razor |
| Security Events widget | Exists | Dashboard.razor |

---

## Proposed Implementation

### Phase 0: Admin Panel Shell ✅ COMPLETE

**Goal:** Create a unified admin section with its own layout and sub-navigation.

**Status:** Completed Feb 22, 2026 — Commit 7f67107

| File | Status | Notes |
|------|--------|-------|
| `Components/Layout/AdminLayout.razor` | ✅ Complete | Unified admin layout with sidebar navigation |
| `Components/Pages/Admin/Index.razor` | ✅ Complete | `/admin` landing page |

**Implemented Navigation:**

```
Admin Panel
  Dashboard        /admin/dashboard
  ─── Users ───
  Users            /admin/users
  ─── Organizations ───
  Organizations    /admin/organizations
  ─── Infrastructure ───
  Agents           /admin/agents
  Crawlers         /admin/crawlers
  ─── Monitoring ───
  Audit Trail      /admin/audit
  ─── Cross-Org ───
  Subscriptions    /admin/subscriptions
```

**Effort:** Small (2 hours) — layout + nav restructuring, no new data.

### Phase 1: User Management Enhancements ✅ COMPLETE

**Goal:** Close the gaps in Workflow 1 (Chapter 8) — User Management.

**Status:** Completed Feb 22, 2026 — Commit e66265c

| Feature | Status | Notes |
|---------|--------|-------|
| Search/filter users | ✅ Complete | Real-time search + 3 independent filters |
| Lock with reason | ✅ Complete | Modal with predefined reasons + notes |
| Created/last-login dates | ✅ Complete | Displayed in list, detail, and sortable |
| User type badge | ✅ Complete | Color-coded badges (blue/purple/orange) |
| Quota usage visualization | ✅ Complete | Progress bars with color-coded utilization |
| Database migration | ✅ Complete | Migration 20260222085328 with indexes |

**Database Changes:**
- `CreatedAt` (timestamp with UTC default)
- `LastLoginAt` (nullable timestamp, updated on successful auth)
- `LockReason` (text field for audit/compliance)
- `UserType` (varchar(50) with values: user, service_account, bot)
- Indexes on all four columns for efficient filtering/sorting

**UI Enhancements:**
- **Users List:** Search by username/email/display name, filter by type/status, sort by date/login/name
- **User Detail:** Display created date, last login, edit user type, show lock reason in status card
- **Lock Modal:** Predefined reasons (Security violation, Policy violation, Inactive, Suspicious, User request, Admin hold, Other), optional notes, silent lock option
- **Quota Card:** Real-time aggregation from Notebook API, progress bars, color-coded (green <75%, yellow 75-90%, red ≥90%)

**Service Layer:**
- New `UsageAggregationService` — aggregates quota usage from API
- Returns `UserUsageStats` record with notebook count, total entries, estimated storage

**Effort:** Medium (~6 hours) — 1 migration, model changes, UI updates, new service, filtering/sorting logic.

### Phase 2: Quota Monitoring

**Goal:** Show actual usage alongside limits.

| Feature | Change |
|---------|--------|
| Usage aggregation | New `QuotaUsageService` calling `ListNotebooksAsync` to count notebooks/entries/storage |
| Progress bars | Add usage bars to `UserDetail.razor` quota card and `QuotaManagement.razor` |
| Org-level defaults | Add `OrganizationQuota` model, migration, UI in `OrganizationDetail.razor` |

**Effort:** Medium — new service, 1 migration, UI additions.

### Phase 3: Crawler Overview

**Goal:** Fleet-wide crawler health dashboard.

| File | Change |
|------|--------|
| `Components/Pages/Admin/Crawlers.razor` | New `/admin/crawlers` page listing all notebooks with crawlers, aggregated stats |

Data available from existing API: `GetCrawlerConfigAsync`, `GetCrawlerRunsAsync` per notebook.
Need to iterate all notebooks and aggregate.

**Effort:** Medium — new page, aggregation logic, no backend changes.

### Phase 4: Subscriptions Management

**Goal:** Implement the Cross-Org Coordinator workflows (Chapter 10).

| File | Change |
|------|--------|
| `Components/Pages/Admin/Subscriptions.razor` | New `/admin/subscriptions` — list all subscriptions across notebooks |
| `Components/Pages/Admin/SubscriptionDetail.razor` | New — create/edit subscription, monitor sync health |

API methods already exist: `ListSubscriptionsAsync`, `CreateSubscriptionAsync`,
`TriggerSyncAsync`, `DeleteSubscriptionAsync`.

**Effort:** Medium — 2 new pages, no backend changes.

### Phase 5: System Health Monitoring

**Goal:** Implement Workflow 3 (Chapter 8).

| Feature | Dependency |
|---------|------------|
| API/DB health endpoint | Needs new backend endpoint exposing server metrics |
| Performance charts | Needs a JS charting library (Chart.js or similar) |
| Alert configuration | Needs alert model, migration, notification dispatch |

**Effort:** Large — requires backend work, JS integration, new infrastructure.

### Phase 6: Agent Fleet Management

**Goal:** Extend agent monitoring per Workflow 4 (Chapter 8).

| Feature | Dependency |
|---------|------------|
| Agent health cards (heartbeat, uptime) | Needs agent heartbeat endpoint in notebook API |
| Performance metrics (CPU, job throughput) | Needs metrics endpoint |
| Credential rotation | Needs rotate-credentials API |
| Pause/resume | Needs agent state management API |

**Effort:** Large — mostly blocked on backend API additions.

### Phase 7: Compliance & Reporting

**Goal:** Implement auditor compliance workflows (Chapter 7, 10).

| Feature | Dependency |
|---------|------------|
| Compliance matrix | Visualization of classification flows across orgs |
| Report generation | PDF/CSV export of compliance audits |
| Per-org audit scoping | Filter audit by organization membership |

**Effort:** Large — significant new UI, possibly a reporting library.

---

## Implementation Timeline

| Status | Phase | Target | Rationale |
|--------|-------|--------|-----------|
| ✅ Done | Phase 0 | Feb 22, 2026 | Unified admin layout with navigation |
| ✅ Done | Phase 1 | Feb 22, 2026 | User search, filters, metadata tracking, quota visualization |
| 🔮 Planned | Phase 2 | Q1 2026 | Org-level quota defaults, enforcement policies |
| 🔮 Planned | Phase 3 | Q2 2026 | Fleet-wide crawler health dashboard |
| 🔮 Planned | Phase 4 | Q2 2026 | Subscriptions management (Cross-Org Coordinator workflows) |
| 🔮 Planned | Phase 5 | Q3 2026 | System health monitoring (requires backend metrics) |
| 🔮 Planned | Phase 6 | Q3 2026 | Agent fleet management (requires backend API additions) |
| 🔮 Planned | Phase 7 | Q4 2026 | Compliance & reporting tools |

---

## Out of Scope

These features are referenced in the manual but need significant platform-level work
before an admin UI can expose them:

- **Email service** (activation emails, password reset emails, alert notifications)
- **Metrics collection** (Prometheus/OTLP integration for CPU, memory, latency)
- **Bulk CSV import** (needs a server-side import endpoint with validation)
- **Report generation** (PDF export, compliance documentation)
- **Auto-scaling recommendations** (needs historical job queue metrics)
