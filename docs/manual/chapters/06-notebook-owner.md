# Chapter 6: Notebook Owner

## Role Overview

As a **Notebook Owner**, you are the steward of a knowledge space. You create and manage notebooks, control who can access them, review submissions, monitor background processing, and ensure the notebook stays organized and secure.

**Key Responsibilities:**
- Create notebooks with appropriate classification
- Manage access control (who can read, write, admin)
- Review and approve external contributions (if gating is enabled)
- Monitor job processing (embeddings, claims, analysis)
- Manage subscriptions to other notebooks
- Maintain notebook quality and organization

**Required Permissions:**
- "Admin" access to at least one notebook (usually yours)
- Read+Write access to create entries
- Your organization's clearance

**Typical Workflows:** 5 core workflows in this chapter

---

## Workflow 1: Creating and Configuring Notebooks

### Overview

Create a new notebook and configure its classification, ownership, and basic settings.

**Use case:** Your team needs a shared knowledge space for documenting architectural decisions. You create a notebook with appropriate security labels.

**Related workflows:**
- [Managing Access Control](#workflow-2-managing-access-control) — Grant access after creation
- [Reviewing Submissions](#workflow-3-reviewing-submissions) — Set up content review if needed

### Prerequisites

- [ ] Cyber account with Read+Write access
- [ ] Understand your team's classification level
- [ ] Clear purpose for the notebook
- [ ] Owner group identified

### Step-by-Step Instructions

#### Step 1: Go to Notebooks

**Navigate to:** Sidebar → Notebooks → [+ New Notebook]

1. Click **Notebooks** in the left sidebar
2. You'll see your existing notebooks
3. Click **"[+ New Notebook]"** button

#### Step 2: Fill in Notebook Details

```
Create New Notebook
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Name *
[Architectural Decisions]

Description
[Central repository for architecture decisions, ADRs, and design documents]

Owner Group *
[Dropdown: Select group...]
  ☐ Engineering
  ☐ Infrastructure
  ☐ Architecture Council

Classification Level (Advanced)
[Dropdown: CONFIDENTIAL] ← Usually inherits from group

Compartments (Optional)
[Tag input: Add compartment names...]
  Examples: Strategic, Infrastructure, etc.

Retention Policy
[Dropdown: Select retention...]
  ○ 1 year
  ○ 3 years (default)
  ○ 7 years
  ○ Permanent

[Create Notebook] [Preview] [Cancel]
```

**Field Explanations:**

| Field | Required | Notes |
|-------|----------|-------|
| **Name** | Yes | Concise, clear (e.g., "API Architecture", not "Stuff") |
| **Description** | No | 1-2 sentences explaining purpose |
| **Owner Group** | Yes | The team that owns this notebook |
| **Classification** | No | Inherited from group; can be more restrictive |
| **Compartments** | No | Additional security categories |
| **Retention** | No | How long entries are kept before deletion |

#### Step 3: Set Classification (If Advanced)

Classification usually inherits from the owner group:

```
Owner Group: Engineering / Backend
Group Classification: SECRET / {Operations}

Notebook Options:
  • Inherit: SECRET / {Operations} ← (automatically set)
  • More Restrictive: SECRET / {Operations, Database}
  • NOT ALLOWED: CONFIDENTIAL (lower than group)
```

You can **add compartments** but not remove or lower the level.

#### Step 4: Create Notebook

Click **"[Create Notebook]"**:

```
✓ Notebook created!

Name: Architectural Decisions
Owner: Engineering / Backend
Classification: SECRET / {Operations}
Access: You have Admin access

Next steps:
  1. Invite collaborators [Manage Access]
  2. Create first entry [Start Writing]
  3. Configure settings [Notebook Settings]

[View Notebook] [Back]
```

#### Step 5: Configure Settings (Optional)

Go to the notebook and click **Settings** tab:

```
Notebook Settings
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Notebook Name: Architectural Decisions
Owner Group: Engineering / Backend
Classification: SECRET / {Operations}

Retention Policy: 3 years (entries older than 3 years are archived)

Ingestion Gating:
☐ Require review for new entries (optional content review gate)

Notifications:
☑ Notify on new entries
☑ Notify on revisions
☐ Notify on comments

[Save Changes]
```

**Ingestion Gating:** If enabled, all new entries go to a review queue before being published.

### Verification

Confirm your notebook is set up:

- [ ] Notebook appears in your Notebooks list
- [ ] You have "Admin" access
- [ ] Name and description are correct
- [ ] Classification is appropriate for content
- [ ] Retention policy is set
- [ ] You can create an entry in it

### Tips & Tricks

#### Naming Conventions

Use consistent naming across your organization:

```
✅ Good names:
   - Team name first: "Backend / Database Queries"
   - Clear scope: "Q1 Planning"
   - Single purpose: "Security Incident Log"

❌ Bad names:
   - Vague: "Stuff", "Notes", "Temporary"
   - Redundant: "Backend Backend Things"
   - Too broad: "Everything"
```

#### Description Best Practices

Write descriptions that help people decide if they should read:

```
✅ Good:
   "Central repository for architecture decisions (ADRs) and design
    documents for the backend team. Covers database design, API specs,
    and infrastructure patterns."

❌ Bad:
   "Architecture"
   "Important stuff"
   "Read this"
```

#### Classification Strategy

Start conservative:

```
Team Classification:    SECRET / {Operations}
Notebook Options:

❌ Make it PUBLIC for accessibility
✅ Start with SECRET / {Operations}
   Restrict further only if needed
✅ Document why it's classified that way
```

### Next Steps

- [Manage Access Control](#workflow-2-managing-access-control) — Add collaborators
- [Review Submissions](#workflow-3-reviewing-submissions) — Set up review gates
- Start creating entries

---

## Workflow 2: Managing Access Control

### Overview

Grant and revoke access to your notebook for users and groups at four tiers: Existence, Read, Read+Write, Admin.

**Use case:** Your Architecture Council notebook should allow executives to read but not edit. You grant them "Read" access.

**Related workflows:**
- [Creating Notebooks](#workflow-1-creating-and-configuring-notebooks) — Access set after creation
- [Reviewing Submissions](#workflow-3-reviewing-submissions) — Admin access needed

### Prerequisites

- [ ] Notebook already created
- [ ] Admin access to the notebook
- [ ] Know who needs access and at what level

### Step-by-Step Instructions

#### Step 1: Go to Access Control Tab

**Navigate to:** Notebooks → Select notebook → Access Control tab

```
Architectural Decisions
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Entry Feed] [Settings] [Access Control] [Statistics]

Current Access List:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Principal         | Type  | Tier       | Actions
──────────────────┼───────┼────────────┼──────────
Engineering Team  | Group | Read+Write | [Edit] [Remove]
You (Jane Smith)  | User  | Admin      | (you)

[+ Add User or Group]
```

#### Step 2: Click "Add User or Group"

```
Grant Access
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Search for principal:
[Type to search...]

Results:
☐ Alice Chen (user)
☐ Bob Johnson (user)
☐ Executive Team (group)
☐ Security Council (group)

Access Tier:
○ Existence    (know it exists, but can't read)
○ Read         (can read, can't write)
◉ Read+Write   (can read and create/revise)
○ Admin        (full control)

[Grant Access] [Cancel]
```

**Access Tiers:**

| Tier | Can Read | Can Write | Can Manage | Use Case |
|------|----------|-----------|-----------|----------|
| **Existence** | ❌ | ❌ | ❌ | Secret/unlisted notebooks |
| **Read** | ✅ | ❌ | ❌ | Stakeholders, viewers |
| **Read+Write** | ✅ | ✅ | ❌ | Contributors |
| **Admin** | ✅ | ✅ | ✅ | Notebook owner, managers |

#### Step 3: Grant Access

1. Check the principal you want to grant access to
2. Select the appropriate tier
3. Click **"[Grant Access]"**

```
✓ Access granted!

Executive Team: Read access to Architectural Decisions

They can:
  • Read all entries (including restricted ones, if they have clearance)
  • See history and revisions
  ❌ Create new entries
  ❌ Manage access

[OK]
```

#### Step 4: Edit Access Levels

If you need to change someone's access:

1. Click **"[Edit]"** next to their name
2. Select new tier
3. Provide reason (optional):
   ```
   Reason for changing access:
   [Promoted to tech lead, needs write access]
   ```
4. Click **"[Save]"**

#### Step 5: Revoke Access

To remove someone's access:

1. Click **"[Remove]"** next to their name
2. Confirm:
   ```
   ⚠️  Remove Alice Chen's Read+Write access?

   She will:
     • Lose ability to read this notebook
     • Keep access through group membership (if any)
     • Audit log will record the removal

   [Confirm] [Cancel]
   ```
3. Click **"[Confirm]"**

### Access Control Scenarios

#### Scenario 1: Internal Team Notebook

```
Notebook: Backend Engineering Decisions
Owner: Backend Team (SECRET / {Operations})

Access Control:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Backend Team      | Group | Read+Write (auto via group)
Infrastructure    | Group | Read       (needs visibility)
You (Owner)       | User  | Admin      (owner)
Security Lead     | User  | Read       (compliance review)

Result:
  • 5 backend engineers: full access
  • 3 infrastructure engineers: can learn from decisions
  • 1 security lead: can audit compliance
  • Others: no access
```

#### Scenario 2: Cross-Functional Documentation

```
Notebook: API Architecture Spec
Owner: Backend Team (SECRET / {Operations})

Access Control:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Backend Team        | Group | Read+Write
Frontend Team       | Group | Read
Mobile Team         | Group | Read
Product Team        | Group | Read
Client Partnerships | Group | Existence (they know it exists)

Result:
  • Backend engineers: can update spec
  • Frontend/Mobile engineers: know about API
  • Product: understands what's possible
  • Client Partnerships: knows to reference it privately
```

#### Scenario 3: Executive Dashboard

```
Notebook: Quarterly Roadmap
Owner: Leadership Team (SECRET / {Operations, Strategic})

Access Control:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Leadership Team        | Group | Read+Write (collaborators)
Engineering Director  | User  | Admin       (co-owner)
Product Director      | User  | Admin       (co-owner)
Department Heads      | Group | Read        (visibility)
All Staff             | Group | Existence   (know it exists)

Result:
  • 3 people can write/edit roadmap
  • Department heads can read to understand direction
  • Everyone else knows it exists but can't read
```

### Verification

Confirm access is correct:

- [ ] Each principal has appropriate tier
- [ ] Owner still has Admin access
- [ ] Contributors have Read+Write, not Admin
- [ ] Viewers have Read, not Write
- [ ] Audit log shows access changes
- [ ] Removed principals can no longer access

### Tips & Tricks

#### Principle of Least Privilege

Only grant necessary access:

```
❌ "Give everyone Read+Write to be collaborative"
✅ "Give contributors Read+Write, others Read"

❌ "Make everyone Admin so they can help manage"
✅ "Keep Admin to just notebook owners"

❌ "Restrict everyone to Existence (too secretive)"
✅ "Allow appropriate tiers based on role"
```

#### Group vs. Individual Access

Prefer groups:

```
✅ Grant access to "Backend Team" group
   • Automatically includes new team members
   • Easy to update one place

❌ Grant access to individual engineers
   • Need to manually add/remove each person
   • Easy to miss people
```

#### Track Access Changes

Monitor who has what:

```
[View Access History]

Access Control Audit Trail
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Jan 22, 2:30 PM - Jane Smith granted "Alice Chen" Read access
Jan 20, 10:00 AM - Admin revoked "Carol Davis" Read+Write (left team)
Jan 15, 9:00 AM - Jane Smith created notebook, auto-granted "Backend Team" Read+Write
```

#### Cascade Access from Groups

If someone is in the owner group, they automatically get that access:

```
Backend Team = Read+Write

Alice Chen is in "Backend Team" group
→ Automatically has Read+Write access
→ Can't remove individual access (must remove from group)
```

### Next Steps

After setting access control:
- Invite people to start contributing
- Create first entry
- Set up review gates if needed

---

## Workflow 3: Reviewing Submissions

### Overview

If ingestion gating is enabled, review and approve/reject new entries before they're published to the notebook.

**Use case:** Your Architecture Council wants to ensure entries meet quality standards before publication.

**Related workflows:**
- [Creating Notebooks](#workflow-1-creating-and-configuring-notebooks) — Enable gating during setup
- [Creating Entries](04-knowledge-contributor.md#workflow-1-creating-and-organizing-entries) — The submission side

### Prerequisites

- [ ] Ingestion gating enabled on notebook (Workflow 1)
- [ ] Admin access to the notebook
- [ ] Understanding of what makes a good submission

### Step-by-Step Instructions

#### Step 1: Access Review Queue

**Navigate to:** Notebooks → Select notebook → [Review] tab

```
Architectural Decisions
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Entry Feed] [Review] [Settings] [Access Control]

Pending Submissions (3):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[✓] Database Indexing Strategy
    Submitted by: Carol Davis
    Submitted: Jan 22, 10:30 AM
    Status: Pending
    [View] [Approve] [Request Changes] [Reject]

[⏳] Caching Architecture
    Submitted by: Bob Johnson
    Submitted: Jan 22, 9:15 AM
    Status: Waiting for changes
    Last feedback: Jan 22, 10:00 AM (from Jane Smith)
    [View] [Approve] [Request Changes] [Reject]

[⏳] API Versioning Policy
    Submitted by: Alice Chen
    Submitted: Jan 21, 3:30 PM
    Status: Pending
    [View] [Approve] [Request Changes] [Reject]
```

#### Step 2: Review a Submission

Click **"[View]"** to see the entry:

```
Database Indexing Strategy (SUBMISSION #45)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Submitted by: Carol Davis
Topic: organization/engineering/database/indexing
Submitted: Jan 22, 10:30 AM
References: 3 entries

## Overview

We're implementing a new indexing strategy to improve query performance...

[Full content displayed]

---

Reviewer Panel:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Status: ⏳ Pending Review

Your actions:
  [✓ Approve] [📝 Request Changes] [❌ Reject]
```

#### Step 3: Provide Feedback

**Option A: Approve**

If the entry meets standards, click **"[✓ Approve]"**:

```
Approve Submission
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Comments (optional):
[Great job! Clear and well-referenced.]

[Approve]  [Cancel]
```

**Option B: Request Changes**

If you need revisions, click **"[📝 Request Changes]"**:

```
Request Changes
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Feedback (required):
[Please add a section on performance impact
and include benchmarks from testing.]

[Send Feedback] [Cancel]
```

The submitter gets notified and can revise.

**Option C: Reject**

If the entry doesn't fit, click **"[❌ Reject]"**:

```
Reject Submission
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Reason (required):
[Dropdown: Select reason...]
  • Out of scope for this notebook
  • Insufficient quality
  • Duplicates existing entry
  • Doesn't meet standards
  • Other

Comments:
[This topic is better suited for the Security notebook.
I'll forward them a reference.]

[Reject] [Cancel]
```

#### Step 4: Monitor Resubmissions

After requesting changes, the queue updates:

```
Pending Submissions (2):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[⏳] Caching Architecture (RESUBMISSION #2)
    Submitted by: Bob Johnson
    Originally submitted: Jan 22, 9:15 AM
    First feedback: "Needs more detail on consistency"
    Resubmitted: Jan 22, 2:00 PM
    Status: Awaiting review
    [View] [Approve] [Request Changes] [Reject]
```

View the updated submission, see what changed, and decide.

### Review Criteria Examples

#### Example 1: Architecture Decision Record (ADR)

```
✅ Good submission:
   - Clear problem statement
   - Decision and rationale
   - Consequences (positive and negative)
   - References related entries
   - Links to implementation

❌ Poor submission:
   - Vague problem description
   - No rationale for why this decision
   - Doesn't address tradeoffs
   - No related references
```

#### Example 2: Technical Specification

```
✅ Good submission:
   - Overview and motivation
   - Detailed specification with examples
   - Performance characteristics
   - Security considerations
   - API or configuration examples
   - Link to implementation

❌ Poor submission:
   - "Here's our new API"
   - No examples
   - Doesn't explain why
   - Missing security analysis
```

#### Example 3: Incident Report

```
✅ Good submission:
   - Timeline of events
   - Root cause analysis
   - Impact assessment
   - Mitigation steps taken
   - Preventive actions
   - Links to follow-up tasks

❌ Poor submission:
   - "System went down"
   - No clear timeline
   - Blame focused vs. learning focused
   - No follow-up actions
```

### Verification

Confirm review workflow is working:

- [ ] Submissions appear in review queue
- [ ] You can view submissions completely
- [ ] You can approve submissions (they're published)
- [ ] You can request changes (submitter is notified)
- [ ] You can reject submissions (recorded in audit log)
- [ ] Resubmissions after feedback are tracked

### Tips & Tricks

#### Set Review Standards

Document what you expect:

```
[Add to Notebook Description or FAQ]

Submission Standards:
  1. Clear, specific title
  2. Well-structured content (use headings)
  3. At least one reference to related entries
  4. Specific, not vague language
  5. Consider security implications
  6. No copyrighted content
```

#### Use Templates

Provide templates for common entries:

```
[Create Template Entries]

Architecture Decision Record Template:
  - Problem Statement
  - Decision
  - Rationale
  - Consequences

Incident Report Template:
  - Timeline
  - Impact
  - Root Cause
  - Remediation
```

#### Fast-Track Approvals

Don't require review for minor corrections:

```
Ingestion Gating
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

☑ Require review for new entries
☑ Require review for revisions to published entries
☐ Require review for minor fixes (typos, formatting)
```

Disable review for revisions to reduce bottlenecks.

### Next Steps

After reviewing:
- Provide constructive feedback
- Publish approved entries
- Help submitters improve rejected ones

---

## Workflow 4: Monitoring Job Pipeline

### Overview

Monitor background jobs (embeddings, claims analysis, comparisons) that process entries in your notebook. Jobs are created automatically; you just track them.

**Use case:** You want to see if background analysis is complete for entries your team just created.

**Related workflows:**
- [ThinkerAgent Configuration](05-org-administrator.md#workflow-4-configuring-thinkeragents) — Sets up agents that run these jobs

### Prerequisites

- [ ] Notebook with entries
- [ ] At least "Read" access
- [ ] Understanding of job types (embeddings, claims, etc.)

### Step-by-Step Instructions

#### Step 1: Access Job Statistics

**Navigate to:** Notebooks → Select notebook → Statistics tab

```
Architectural Decisions
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Entry Feed] [Settings] [Statistics]

Job Queue Statistics:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Overall Status: ✅ All caught up
Last updated: 5 minutes ago

Job Type           | Pending | In Progress | Completed | Failed
───────────────────┼─────────┼─────────────┼───────────┼────────
DISTILL_CLAIMS     | 0       | 0           | 1,247     | 0
COMPARE_CLAIMS     | 0       | 1           | 342       | 0
EMBED_ENTRIES      | 2       | 3           | 3,421     | 0
CLASSIFY_ENTRIES   | 0       | 0           | 4,892     | 0

Total entries processed: 9,902
Success rate: 99.97%

[Refresh Stats] [View Details] [Clear Failed]
```

**Job Types:**

| Job | Purpose | Status |
|-----|---------|--------|
| **DISTILL_CLAIMS** | Extract claims from entries | Should be completed |
| **COMPARE_CLAIMS** | Compare claims between entries | Should be completed |
| **EMBED_ENTRIES** | Create vector embeddings for search | Usually in progress |
| **CLASSIFY_ENTRIES** | Assign topics/categories | Background work |

#### Step 2: View Detailed Job Status

Click **"[View Details]"**:

```
Job Details - EMBED_ENTRIES
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Pending (2):
  • entry_abc123 - "Database Indexing Strategy" (queued 5 min ago)
  • entry_def456 - "Caching Architecture" (queued 2 min ago)

In Progress (3):
  • entry_ghi789 - "API Versioning Policy" (processing 3 min)
  • entry_jkl012 - "Transaction Handling" (processing 1 min)
  • entry_mno345 - "Error Handling Standards" (processing < 1 min)

Completed (3,421):
  [Last 5 shown]
  ✓ entry_xyz999 - "Concurrency Model" (completed 2 min ago, 8s)
  ✓ entry_aaa111 - "Monitoring Architecture" (completed 5 min ago, 12s)
  ✓ entry_bbb222 - "Testing Strategy" (completed 8 min ago, 6s)

Failed (0):
  (none)
```

#### Step 3: Understand Job Timing

Entries are processed in stages:

```
Entry Lifecycle:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

1. Entry Created
   ↓ (immediately)
2. DISTILL_CLAIMS (extract claims)
   ↓ (1-2 minutes)
3. COMPARE_CLAIMS (compare to other entries)
   ↓ (1-2 minutes)
4. EMBED_ENTRIES (create vector embeddings)
   ↓ (1-2 minutes)
5. Ready for Search & Analysis

Typical total time: 5-10 minutes per entry
```

#### Step 4: Handle Failed Jobs

If a job fails:

```
Failed (2):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

❌ entry_xyz999 - "Concurrency Model"
   Job Type: EMBED_ENTRIES
   Failed: 10 minutes ago
   Error: "Timeout: embedding service unresponsive"
   [Retry] [View Error Log] [Dismiss]

❌ entry_aaa111 - "Monitoring Architecture"
   Job Type: COMPARE_CLAIMS
   Failed: 15 minutes ago
   Error: "Out of memory in comparison engine"
   [Retry] [View Error Log] [Dismiss]

[Retry All Failed] [Clear Failed] [Contact Support]
```

Click **"[Retry]"** to rerun the job:

```
Retrying: EMBED_ENTRIES for entry_xyz999

Status: Queued (will process in order)
[Cancel Retry]
```

The job will be re-queued and run again.

#### Step 5: Monitor Completion

Jobs complete automatically. Monitor via the Statistics tab:

```
Checking every 5 minutes...

Jan 22, 3:00 PM: 5 pending → 2 pending (3 processed)
Jan 22, 3:05 PM: 2 pending → 0 pending (all complete!)

✅ All jobs complete for notebook!
```

### Performance Insights

```
Job Performance Analysis
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

EMBED_ENTRIES Performance:
  Average time: 8.2 seconds per entry
  P99 time: 15 seconds
  Bottleneck: Vector database indexing

Success rate: 99.97% (1 failure in 3,421 jobs)
Most common error: Timeout (affects 0.03%)

Recommendation:
  Bottleneck is in vector DB. Consider:
    • Increasing DB connection pool
    • Scaling embedding service
```

### Verification

Confirm job monitoring is working:

- [ ] You can see job queue statistics
- [ ] Job counts add up (pending + in progress + completed)
- [ ] You can view detailed job information
- [ ] Failed jobs can be retried
- [ ] Completion rate is tracked
- [ ] Performance metrics are available

### Tips & Tricks

#### Auto-Refresh Dashboard

Set up auto-refresh while monitoring:

```
[Auto-Refresh] [Every 5 minutes]

Or set polling interval:
  ○ Never
  ○ Every minute
  ◉ Every 5 minutes
  ○ Every 10 minutes
```

#### Understand Stalls

If jobs aren't progressing:

```
❓ Why are jobs stuck in "In Progress"?

Check:
  1. Agent status - Is the processing agent active?
  2. Agent logs - Are there errors?
  3. System health - CPU/memory/disk OK?
  4. Network - Can agent reach Cyber?
  5. Job logs - Specific error message?
```

#### Scale Based on Load

Monitor job backlog:

```
High backlog (100+ pending)?
  → You may need more agents
  → Consider parallel processing
  → Talk to System Admin about scaling

No backlog (< 5 pending)?
  → Current capacity is sufficient
  → Don't add more agents unnecessarily
```

### Next Steps

After monitoring:
- Investigate failed jobs
- Understand performance bottlenecks
- Request agent scaling if needed

---

## Workflow 5: Managing Subscriptions

### Overview

Subscribe your notebook to other notebooks to mirror entries and keep knowledge synchronized across your organization or even across organizations.

**Use case:** Your team uses insights from another team's research. You subscribe to their notebook to automatically mirror new entries.

**Related workflows:**
- [Cross-Organization Coordinator](10-cross-org-coordinator.md) — Managing subscriptions at org level

### Prerequisites

- [ ] Source notebook you want to subscribe to (you have Read access)
- [ ] Admin access to your notebook
- [ ] Understanding of subscription scope and filtering

### Step-by-Step Instructions

#### Step 1: Go to Subscriptions

**Navigate to:** Notebooks → Select notebook → Subscriptions tab

```
Architectural Decisions
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Entry Feed] [Settings] [Subscriptions]

Active Subscriptions (1):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Source] Infrastructure / Database Design
  Scope: Entries (catalog + claims + entries)
  Synced: 45 entries (last 2 hours)
  Status: ✅ Healthy
  Watermark: Position 1,247
  [View] [Sync Now] [Pause] [Edit] [Unsubscribe]

[+ Subscribe to Notebook]
```

Click **"[+ Subscribe to Notebook]"**.

#### Step 2: Select Source Notebook

```
Subscribe to Notebook
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Find source notebook:
[Search or select...] [Browse organizations]

Recent notebooks:
☐ Infrastructure / Database Design
☐ Security / Incident Response
☐ Operations / Runbooks

Organizations:
  MyCompany
    ☐ Engineering / Architecture
    ☐ Engineering / Backend
    ☐ Operations / Runbooks
  OtherCompany (partner org)
    ☐ Public / Documentation
    ☐ Public / Standards
```

Search or browse to find the notebook you want to subscribe to.

#### Step 3: Configure Subscription Scope

```
Subscription Settings
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Source Notebook: Infrastructure / Database Design

Scope *
[Dropdown: What to mirror...]

○ Catalog only     (titles and metadata)
○ Catalog + Claims (titles + extracted claims)
◉ Entries          (full entries, catalog, claims)

Discount Factor
[Slider: 100%] ← How much to weight new entries

  100% = Full relevance
  50%  = Half weight in coherence calculations
  10%  = Low relevance (reference only)

Polling Interval
[Dropdown: How often to check...]

  ○ Every hour
  ◉ Every 4 hours
  ○ Every day
  ○ Manual only

[Subscribe] [Preview] [Cancel]
```

**Scope Options:**

| Scope | What You Get | Use Case |
|-------|-------------|----------|
| **Catalog** | Entry titles, metadata, topics | Quick reference |
| **Catalog + Claims** | Above + extracted claims | Analysis, comparison |
| **Entries** | Full content + claims + metadata | Deep integration, learning |

**Discount Factor:**
- 100% = These entries are just as relevant as local ones
- 50% = These entries are somewhat relevant (external perspective)
- 10% = These entries are reference-only (not central to us)

The discount affects integration cost calculation—external entries don't override local consensus.

#### Step 4: Subscribe

Click **"[Subscribe]"**:

```
✓ Subscription created!

Source: Infrastructure / Database Design
Entries mirrored: 0 (first sync in progress...)
Status: Syncing...

Next sync: In 4 hours (or on schedule)

You can:
  [View Mirrored Entries] [Sync Now] [Manage Subscription]
```

#### Step 5: Monitor Sync Status

Your subscription dashboard shows sync progress:

```
Subscriptions Dashboard
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Infrastructure / Database Design
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Sync Status: ✅ Healthy (last sync: 2 hours ago)
Mirrored: 45 entries
Watermark: Position 1,247 (45/45 synced)
Next sync: In 2 hours

Errors (last 7 days): 0
Skipped entries: 0 (all entries accessible)

[View Mirrored Entries] [Sync Now] [Edit Settings] [Unsubscribe]
```

#### Step 6: View Mirrored Entries

Mirrored entries appear in your notebook marked as external:

```
Entry Feed
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Entry] Query Optimization Patterns
  Author: Alice Chen (Infrastructure Team) → External
  Source: Infrastructure / Database Design
  Position: [external-sync-1247]
  Integration: ⚠️ Probation (mirrored, discount 50%)
  [Read] [View Source] [Remove from Local Copy]
```

Click **"[View Source]"** to go to the original entry.

### Subscription Scenarios

#### Scenario 1: Internal Cross-Team Subscription

```
Backend team subscribes to Infrastructure team's database decisions

Scope: Entries (full details)
Discount: 100% (internal, equally relevant)
Polling: Every 4 hours

Backend engineers can:
  ✅ Learn from infrastructure decisions
  ✅ Reference infrastructure entries
  ✅ Understand database patterns
```

#### Scenario 2: Cross-Organization Research

```
Your research team subscribes to partner org's public research

Scope: Catalog + Claims (detailed)
Discount: 50% (external, useful reference)
Polling: Every day

Your team can:
  ✅ Know what partners are researching
  ✅ Avoid duplicate work
  ✅ Build on partner's findings
  ❌ Risk: Some entries may not apply to your context
```

#### Scenario 3: Regulatory Standard Reference

```
Your compliance team subscribes to standards organization's guidelines

Scope: Catalog only (just reference)
Discount: 10% (external standard, low discount)
Polling: Manual only (standards rarely change)

Compliance can:
  ✅ Reference official standards
  ✅ Link entries to compliance requirements
  ❌ Entries don't influence local coherence
```

### Verification

Confirm subscription is working:

- [ ] Subscription appears in your subscriptions list
- [ ] Mirrored entries appear in entry feed
- [ ] Entries marked as external/mirrored
- [ ] Sync status shows healthy
- [ ] Watermark is advancing (being synced)
- [ ] Can view original entry via link

### Tips & Tricks

#### Manual Sync When Urgent

Force a sync without waiting for schedule:

```
[Sync Now]

Status: ✅ Syncing...
New entries since last sync: 3
Syncing: [████████░░] 80% complete
```

#### Control Over Local Copies

After mirroring, you can edit the local copy:

```
Mirrored Entry: Query Optimization Patterns

[✎ Create Local Copy]

This creates an editable version in your notebook that:
  • Can be revised independently
  • Still links to the original
  • Appears in your search results
```

#### Selective Subscription

Subscribe to specific topics only:

```
[Advanced Settings]

Topic Filter:
[Include topics matching...]
  ☑ infrastructure/database
  ☑ infrastructure/performance
  ☐ infrastructure/security

Only mirror entries matching these topics.
```

#### Conflict Resolution

If you edit a mirrored entry and it changes in the source:

```
⚠️  Update available for "Query Optimization Patterns"

Local version: Position [local-1234]
Source version: Position [external-1247] (newer)

Differences:
  - Source added: "Include compound indexes"
  - Source changed: "Performance impact: +15%"

Options:
  [Keep Local Version] [Merge Changes] [Use Source Version]
```

### Next Steps

After subscribing:
- Review mirrored entries for relevance
- Reference them in your entries
- Periodically review subscription health

---

## Summary: Quick Reference

### The 5 Workflows at a Glance

| Workflow | Purpose | Time | Frequency |
|----------|---------|------|-----------|
| **1. Create Notebooks** | Set up knowledge space | 15-30 min | Quarterly |
| **2. Manage Access** | Grant/revoke permissions | 5-15 min | As needed |
| **3. Review Submissions** | Approve/reject entries | 10-30 min | Continuous |
| **4. Monitor Jobs** | Track background processing | 5-10 min | Daily |
| **5. Manage Subscriptions** | Mirror external notebooks | 10-20 min | As needed |

### Your Workflow Loop

```
1. Create Notebook (once)
   ↓
2. Manage Access (ongoing)
   ↓
3. Enable Review Gates (optional)
   ↓
4. Monitor Jobs (daily)
   ↓
5. Manage Subscriptions (quarterly)
   ↓
6. Back to Step 2 (continuous)
```

### Key Responsibilities

- **Security:** Classify appropriately, manage access, audit changes
- **Quality:** Review submissions, maintain standards, organize knowledge
- **Operations:** Monitor job health, fix failures, manage subscriptions
- **Collaboration:** Grant access to stakeholders, enable cross-team learning

---

## Related Personas

Your workflows overlap with:

- **[Knowledge Contributor](04-knowledge-contributor.md)** — Who create and submit entries
- **[Organization Administrator](05-org-administrator.md)** — Who set up organizational structure
- **[Auditor/Compliance Officer](07-auditor.md)** — Who review your notebook for compliance
- **[System Administrator](08-system-administrator.md)** — Who manage platform-wide agents and monitoring

---

## Troubleshooting

### Can't Create Notebook

**Cause:** You don't have "Admin" access to an owner group.

**Solution:**
1. Check your group memberships (Settings → Profile → Groups)
2. Request admin role in a group from your organization admin
3. Or create a new group with your organization admin's help

### Access Control Not Taking Effect

**Cause:** Clearance cache or permission propagation delay.

**Solution:**
1. Wait 5 minutes for changes to propagate
2. Admin → Organizations → Flush Clearance Cache
3. User logs out and back in

### Job Stuck in "In Progress"

**Cause:** Processing agent is down or overloaded.

**Solution:**
1. Check agent status in Admin → Agents
2. Check agent logs for errors
3. If agent is down, restart it
4. Retry job from the Job Details view

### Subscription Sync Failing

**Cause:** Source notebook permissions changed or source notebook deleted.

**Solution:**
1. Check you still have Read access to source notebook
2. Verify source notebook still exists
3. Check network connectivity to source org
4. Edit subscription and re-test connection

### Can't Edit Mirrored Entry

**Cause:** It's a read-only external reference.

**Solution:**
1. Click "[Create Local Copy]" to make an editable version
2. Edit the local copy (still links to original)
3. Keep both versions in sync manually or via subscription

---

**Last updated:** February 21, 2026
**Chapter version:** 1.0.0 (Beta)
**Platform version:** 2.1.0
