# Admin Panel — Draft Plan

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

### Phase 0: Admin Panel Shell

**Goal:** Create a unified admin section with its own layout and sub-navigation.

| File | Change |
|------|--------|
| `Components/Layout/AdminLayout.razor` | New layout with admin sidebar |
| `Components/Pages/Admin/Index.razor` | `/admin` landing page redirecting to dashboard |

The admin sidebar groups existing pages:

```
Admin Panel
  Dashboard        /admin/dashboard
  ─── Users ───
  Users            /admin/users
  ─── Organizations ───
  Organizations    /admin/organizations
  ─── Infrastructure ───
  Agents           /admin/agents
  Crawlers         /admin/crawlers        (new overview)
  ─── Monitoring ───
  Audit Trail      /admin/audit
  System Health    /admin/health           (new)
  ─── Cross-Org ───
  Subscriptions    /admin/subscriptions    (new)
```

**Effort:** Small — layout + nav restructuring, no new data.

### Phase 1: User Management Enhancements

**Goal:** Close the gaps in Workflow 1 (Chapter 8).

| Feature | Change |
|---------|--------|
| Search/filter users | Add search bar + status filter to `Users.razor` |
| Lock with reason | Add `LockReason` string to `ApplicationUser`, migration, dropdown in `UserDetail.razor` |
| Created/last-login dates | Add `CreatedAt`, `LastLoginAt` to `ApplicationUser`, migration, display in user list and detail |
| User type badge | Add `UserType` enum to `ApplicationUser`, migration, badge display |

**Effort:** Medium — 1 migration, model changes, UI updates.

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

## Priority Recommendation

| Priority | Phase | Rationale |
|----------|-------|-----------|
| Now | Phase 0 | Low effort, improves discoverability of existing features |
| Now | Phase 1 | User search/filter is the most impactful UX gap |
| Soon | Phase 2 | Quotas without usage data are hard to manage |
| Soon | Phase 3 | Crawler overview is a pain point at scale |
| Later | Phase 4 | Subscriptions APIs exist but the workflow is less common |
| Later | Phase 5-7 | Require backend work or new infrastructure |

---

## Out of Scope

These features are referenced in the manual but need significant platform-level work
before an admin UI can expose them:

- **Email service** (activation emails, password reset emails, alert notifications)
- **Metrics collection** (Prometheus/OTLP integration for CPU, memory, latency)
- **Bulk CSV import** (needs a server-side import endpoint with validation)
- **Report generation** (PDF export, compliance documentation)
- **Auto-scaling recommendations** (needs historical job queue metrics)
