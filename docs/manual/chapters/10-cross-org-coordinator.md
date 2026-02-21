# Chapter 10: Cross-Organization Coordinator

## Role Overview

As a **Cross-Organization Coordinator**, you manage knowledge sharing between your organization and external partners. You set up subscriptions to external knowledge, monitor information flows, and ensure compliance with security boundaries.

**Key Responsibilities:**
- Create subscriptions to external notebooks
- Monitor cross-organization data flows
- Ensure Bell-LaPadula compliance (information flow rules)
- Manage inter-org security policies
- Prevent subscription cycles
- Audit cross-org access patterns

**Required Permissions:**
- "Admin" access to your organization
- Access to partner organizations (with appropriate clearance)
- Understanding of Bell-LaPadula model (Chapter 2)

**Typical Workflows:** 3 core workflows in this chapter

---

## Workflow 1: Setting Up Subscriptions

### Overview

Create and manage subscriptions to external notebooks, configuring scope and filtering.

**Use case:** Your research team wants to stay informed on competitor research. You subscribe to a partner's "Public Research" notebook and mirror entries weekly.

**Related workflows:**
- [Notebook Subscriptions](06-notebook-owner.md#workflow-5-managing-subscriptions) — Notebook-level subscriptions
- [Monitoring Flows](#workflow-2-monitoring-cross-organization-flows) — Track synced data

### Prerequisites

- [ ] Partner organization and notebook identified
- [ ] Read access to partner's notebook
- [ ] Organization admin access (for org-level subscriptions)
- [ ] Clear purpose for subscription

### Step-by-Step Instructions

#### Step 1: Find External Notebook

**Navigate to:** Admin → Organizations → Subscriptions

```
Cross-Organization Subscriptions
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Active Subscriptions (2):
  Partner A - "Public Research"   (47 entries synced)
  Partner B - "Industry Standards" (12 entries synced)

[+ Create Subscription]
```

Click **"[+ Create Subscription]"**.

#### Step 2: Select Source Organization

```
Create Cross-Organization Subscription
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Source Organization *
[Search or select...]

Available Partner Organizations:
  ☐ ResearchCorp (10 public notebooks)
  ☐ TechPartners (5 public notebooks)
  ☐ StandardsBody (3 public notebooks)

Your Organization: MyCompany
  (Your current organization)
```

Select a partner organization.

#### Step 3: Select Source Notebook

```
Select Notebook from ResearchCorp
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Public Notebooks (you have Read access):
  ☐ Research / AI Trends
  ☐ Research / Competitive Analysis
  ☑ Research / Public Research (currently selected)
  ☐ Standards / Industry Guidelines

Classification: PUBLIC / {}
Owner: ResearchCorp / Research Team

You can subscribe to this notebook.
```

Select the notebook.

#### Step 4: Configure Subscription

```
Subscription Settings
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Source: ResearchCorp / Research / Public Research
Target Organization: MyCompany

Subscription Scope *
[Select what to mirror...]

  ○ Catalog only (titles, metadata, topics)
  ◉ Catalog + Claims (above + extracted claims)
  ○ Entries (full content, claims, metadata)

Discount Factor *
[Adjust relevance weight...]

  100% = These entries are equally relevant locally
  50%  = These entries are supplementary/reference
  10%  = These entries are minimal relevance

Polling Configuration:
  Interval: [Every 4 hours ▼]
  Auto-subscribe to new entries: ☑

Topic Filter (optional):
  [Include topics matching...]
  Examples: research/ai, research/ml
  (Leave blank to subscribe to all topics)

Information Flow Verification:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Checking Bell-LaPadula Compliance...
  Source classification: PUBLIC / {}
  Your organization min: CONFIDENTIAL / {}
  ✓ COMPLIANT (PUBLIC can flow to higher)

  Potential cycles: None detected
  ✓ NO CYCLES

[Subscribe] [Cancel]
```

#### Step 5: Activate Subscription

```
✓ Subscription Created!

ResearchCorp / Research / Public Research
→ MyCompany (org-level subscription)

Status: Syncing (initial sync in progress)
Scope: Catalog + Claims
Discount: 50%
Polling: Every 4 hours

Mirroring Progress:
  Copied: 47/47 entries
  Synced: 34/47 claims
  Status: 95% complete (ETA 5 minutes)

Next Steps:
  1. Initial sync will complete in ~5 minutes
  2. Check entries appear in your notebooks
  3. Verify access and permissions
  4. Monitor sync health

[View Progress] [Manage Subscription] [Done]
```

### Verification

Confirm subscription is working:

- [ ] Subscription appears in your subscriptions list
- [ ] Initial sync completed
- [ ] Entries are visible in destination notebooks
- [ ] Sync status shows "Healthy"
- [ ] No security violations detected
- [ ] Access is restricted appropriately

---

## Workflow 2: Monitoring Cross-Organization Flows

### Overview

Track what data is being synced, monitor sync health, and investigate issues.

**Use case:** One of your org's subscriptions hasn't synced in 24 hours. You check the status and find the partner org's notebook was reclassified, breaking the subscription agreement.

**Related workflows:**
- [Setting Up Subscriptions](#workflow-1-setting-up-subscriptions) — Create subscriptions
- [Compliance](#workflow-3-ensuring-classification-compliance) — Verify policy compliance

### Prerequisites

- [ ] Subscriptions already created
- [ ] Access to subscription status dashboard
- [ ] Understanding of expected sync patterns

### Step-by-Step Instructions

#### Step 1: View Subscription Dashboard

**Navigate to:** Admin → Organizations → Subscriptions

```
Subscriptions Dashboard
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Active Subscriptions: 3

ResearchCorp / Public Research
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Status: ✅ Healthy (last sync: 1 hour ago)
Mirrored: 47 entries (34 claims)
Watermark: Position 1,247 (all caught up)
Next sync: In 3 hours

Sync History:
  Last 7 days: 42 successful syncs, 0 failed
  Average time: 8 minutes
  Reliability: 100%

[View Mirrored Entries] [Sync Now] [Edit] [Unsubscribe]

---

TechPartners / Industry Standards
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Status: ⚠️  SLOW (last sync: 24 hours ago)
Mirrored: 12 entries
Watermark: Position 384 (lagging by 8 positions)
Next sync: In 2 hours (overdue)

Last Sync Error:
  "Classification changed: PUBLIC → SECRET"
  "Subscription violates information flow rule"
  "Source is now more classified than allowed"

Sync History:
  Last 7 days: 4 successful, 3 failed
  Average time: 15 minutes
  Reliability: 57% ⚠️

Actions Needed:
  [Review Classification] [Contact Partner] [Pause] [Unsubscribe]

---

StandardsBody / Guidelines
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Status: ✅ Healthy
Mirrored: 89 entries (all at position 2,156)
Last sync: 4 hours ago
[Details...]
```

#### Step 2: Investigate Sync Failures

Click on the failing subscription for details:

```
Subscription Issue: TechPartners / Industry Standards
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Problem:
  Sync Status: FAILED
  Error: "Classification Conflict"
  Last Successful Sync: 24 hours ago
  Failed Attempts: 3 (automatic retries exhausted)

Root Cause:
  The source notebook classification changed:
    Was: PUBLIC / {} (allowed to sync to our org)
    Now: SECRET / {Industry} (MORE RESTRICTED)

  Bell-LaPadula Rule Violation:
    Information cannot flow DOWN in classification
    (We can't receive SECRET data in a PUBLIC subscription)

  Options:
    1. Request access to SECRET / {Industry} label
    2. Cancel subscription
    3. Wait for source to revert classification

Timeline:
  24-Jan 4:00 PM: Last successful sync (47 entries)
  25-Jan 10:30 AM: Classification changed by TechPartners
  25-Jan 10:35 AM: Sync failed (detected immediately)
  25-Jan 10:45 AM: Automatic retry failed
  25-Jan 11:00 AM: 2nd retry failed

[Contact Partner] [Review Policy] [Request Upgrade] [Cancel]
```

#### Step 3: Manage Watermark

The watermark tracks sync progress:

```
Watermark Management
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Current Watermark: Position 384
Source Notebook Position: Position 392
Behind By: 8 entries

Entries Not Yet Synced:
  Position 385: "Q1 Forecast" (created 2 hours ago)
  Position 386: "Competitor Analysis" (created 1 hour ago)
  ... (6 more entries)

When subscription is fixed:
  1. Sync will retry from position 384
  2. All 8 pending entries will be processed
  3. Watermark will advance to 392

Manual Watermark Adjustment (advanced):
  Current: 384
  New value: [____________] (careful, can skip entries)

  WARNING: Manually advancing watermark will skip entries!
  Only do this if you're certain you don't want them.

[Advance Watermark] [Reset to Last Good] [Cancel]
```

#### Step 4: Manually Sync if Needed

Force an immediate sync:

```
[Sync Now]

Starting sync for: TechPartners / Industry Standards

Status: Attempting sync...
  • Connecting to TechPartners
  • Verifying subscription authorization
  • Checking classification compliance
  • Fetching new entries (since position 384)

Note: May still fail if underlying issue (classification conflict)
isn't resolved first.

[View Live Log] [Cancel Sync]
```

### Verification

Confirm monitoring is effective:

- [ ] All subscriptions show healthy status
- [ ] Failed syncs are detected immediately
- [ ] Watermark is advancing regularly
- [ ] Sync logs are accessible
- [ ] You can manually trigger syncs
- [ ] Issues can be diagnosed

---

## Workflow 3: Ensuring Classification Compliance

### Overview

Verify that information flows comply with Bell-LaPadula rules and organizational policies.

**Use case:** You need to verify that all your cross-org subscriptions comply with security policy before a compliance audit.

**Related workflows:**
- [Setting Up Subscriptions](#workflow-1-setting-up-subscriptions) — Create subscriptions
- [Monitoring Flows](#workflow-2-monitoring-cross-organization-flows) — Track syncs

### Prerequisites

- [ ] Understanding of Bell-LaPadula model (Chapter 2)
- [ ] Clear organizational policy for cross-org sharing
- [ ] Access to subscription and classification data

### Step-by-Step Instructions

#### Step 1: Review Classification Rules

Verify Bell-LaPadula compliance:

```
Bell-LaPadula Compliance Check
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Rule: Information flows only UPWARD in classification
      (Public → Confidential → Secret → Top Secret)

Your Organization Level: CONFIDENTIAL
  • Can subscribe to: PUBLIC or CONFIDENTIAL sources
  • Cannot subscribe to: SECRET or TOP_SECRET sources

Subscription Compliance Matrix:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Source Organization | Notebook Classification | Policy
───────────────────┼────────────────────────┼─────────
ResearchCorp       | PUBLIC / {}            | ✓ OK
TechPartners       | CONFIDENTIAL / {}      | ✓ OK
StandardsBody      | PUBLIC / {}            | ✓ OK
CompetitorA        | SECRET / {}            | ❌ VIOLATION
GovernmentB        | TOP_SECRET / {Mil}     | ❌ VIOLATION

Violations Found: 2
  1. CompetitorA subscription is TOO HIGH (SECRET)
     Action: [Review] [Remove Subscription] [Request Upgrade]

  2. GovernmentB subscription is TOO HIGH (TOP_SECRET)
     Action: [Review] [Remove Subscription] [Request Upgrade]

[Take Corrective Action]
```

#### Step 2: Document Information Flows

Create a flow diagram:

```
Information Flow Documentation
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

MyCompany (CONFIDENTIAL)
     ↑         ↑         ↑
     |         |         |
  PUBLIC    PUBLIC    CONFIDENTIAL
     |         |         |
ResearchCorp  StandardsBody  TechPartners
 (PUBLIC)      (PUBLIC)      (CONFIDENTIAL)

✓ Compliant: All flows are UPWARD or SAME level
✓ No cycles detected
✓ No information downgrade risk

Export for Compliance Report:
  [Generate Diagram] [Export PDF] [Email Auditors]
```

#### Step 3: Audit Access Controls

Verify authorized access:

```
Cross-Organization Access Audit
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Question: Who in MyCompany has access to external data?

Research Team (5 people):
  ✓ Access to ResearchCorp / Public Research
  ✓ Access to StandardsBody / Guidelines
  ✗ Access to TechPartners / Industry Standards (should they?)

Executive Leadership (3 people):
  ✓ Access to all public notebooks
  ✗ Access to competitor data (appropriate restriction)

Database Team (7 people):
  ✓ Access to StandardsBody / Guidelines
  ✗ Need explicit access for TechPartners subscription

Recommendations:
  1. Grant Research Team → TechPartners / Industry Standards (Read)
  2. Document why Executive Leadership restricted from competitor data
  3. Grant Database Team → TechPartners / Industry Standards (Read)

[Implement Recommendations] [Document Decision] [Audit Log]
```

#### Step 4: Policy Compliance Report

Generate compliance documentation:

```
Cross-Organization Subscription Compliance Report
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Organization: MyCompany
Audit Date: January 31, 2026
Auditor: Alice Chen (Compliance Officer)

EXECUTIVE SUMMARY
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Compliance Status: ✓ COMPLIANT
  • 3/3 active subscriptions comply with Bell-LaPadula
  • 0 policy violations found
  • All information flows are appropriate
  • No cycles or downgrade risks detected

DETAILED FINDINGS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Subscriptions Reviewed:
  1. ResearchCorp / Public Research
     Classification: PUBLIC / {}
     Target: PUBLIC / {} (same level) ✓
     Access: 15 users
     Compliance: ✓ PASS

  2. TechPartners / Industry Standards
     Classification: CONFIDENTIAL / {}
     Target: CONFIDENTIAL / {} (same level) ✓
     Access: 7 users
     Compliance: ✓ PASS

  3. StandardsBody / Guidelines
     Classification: PUBLIC / {}
     Target: PUBLIC / {} (same level) ✓
     Access: 45 users
     Compliance: ✓ PASS

RECOMMENDATIONS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

1. Implement quarterly compliance audits (currently ad-hoc)
2. Document business justification for each subscription
3. Set up automated Bell-LaPadula compliance alerts
4. Review access controls semi-annually

SIGN-OFF
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Auditor: Alice Chen
Date: January 31, 2026
Signature: [Digital signature]

[Download PDF] [Email Stakeholders] [Archive]
```

### Verification

Confirm compliance is documented:

- [ ] All subscriptions reviewed
- [ ] Information flows are compliant
- [ ] No cycles exist
- [ ] Access controls are appropriate
- [ ] Compliance report is generated
- [ ] Issues are documented

---

## Summary: Quick Reference

### The 3 Workflows at a Glance

| Workflow | Purpose | Time | Frequency |
|----------|---------|------|-----------|
| **1. Set Up Subscriptions** | Connect to external notebooks | 15-30 min | As needed |
| **2. Monitor Flows** | Track sync health | 10-20 min | Weekly |
| **3. Compliance** | Verify Bell-LaPadula compliance | 20-40 min | Quarterly |

### Key Principles

- **Information Flows Upward:** Can subscribe to less-classified data only
- **No Cycles:** Prevent circular data flow
- **Access Control:** Restrict access within org appropriately
- **Audit Trail:** Document all subscriptions and changes

---

## Related Personas

Your workflows overlap with:

- **[Organization Administrator](05-org-administrator.md)** — Set org classification levels
- **[Auditor](07-auditor.md)** — Audit cross-org flows
- **[Notebook Owner](06-notebook-owner.md)** — Manage individual subscriptions

---

**Last updated:** February 21, 2026
**Chapter version:** 1.0.0 (Beta)
**Platform version:** 2.1.0
