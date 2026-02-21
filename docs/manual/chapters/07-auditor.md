# Chapter 7: Auditor/Compliance Officer

## Role Overview

As an **Auditor or Compliance Officer**, you ensure that Cyber usage complies with organizational policies, legal requirements, and security standards. You investigate incidents, generate audit reports, and help prevent unauthorized access or policy violations.

**Key Responsibilities:**
- Query and analyze audit logs
- Investigate security events and access denials
- Monitor compliance with security policies
- Generate audit reports for regulators
- Track data retention and classification compliance
- Review cross-organization information flows

**Required Permissions:**
- "Admin" access to audit logs
- Your organization's clearance (at least SECRET recommended)
- Understanding of security model and compliance requirements

**Typical Workflows:** 3 core workflows in this chapter

---

## Workflow 1: Querying Global Audit Logs

### Overview

Access and filter the organization-wide audit log to see who did what, when, and to which resources.

**Use case:** Your compliance team needs to generate a quarterly audit report showing all access to sensitive data.

**Related workflows:**
- [Investigating Security Events](#workflow-2-investigating-security-events) — Deep dive into specific incidents
- [Notebook-Scoped Auditing](#workflow-3-notebook-scoped-auditing) — Focused audits on specific notebooks

### Prerequisites

- [ ] Audit admin role in your organization
- [ ] Clear understanding of what you're looking for
- [ ] Time range for audit query
- [ ] Optional: Specific users/resources to filter

### Step-by-Step Instructions

#### Step 1: Access Audit Log

**Navigate to:** Admin Panel → Audit Log (or Admin → Organizations → [Your Org] → Audit Log)

```
Global Audit Log
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Filters] [Search] [Export to CSV] [Generate Report]

Filters:
  Actor:        [All users ▼]
  Action:       [All actions ▼]
  Resource:     [All resources ▼]
  Date Range:   [Jan 1 - Jan 31, 2026]
  Status:       ☑ Success  ☑ Failure  ☑ Denied

Results: 1,247 events

Entry Feed (sorted by newest first):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Timestamp      | Actor    | Action    | Resource         | Status
───────────────┼──────────┼───────────┼──────────────────┼────────
Jan 31, 2:30 PM | Jane S.  | WRITE     | nb_xyz/entry_123 | ✓ OK
Jan 31, 2:15 PM | Bob J.   | READ      | nb_abc/entry_456 | ✓ OK
Jan 31, 1:45 PM | Alice C. | REVISE    | nb_xyz/entry_789 | ✓ OK
Jan 31, 1:30 PM | Carol D. | READ      | nb_secret/...    | ✗ DENIED
Jan 31, 1:00 PM | Eve W.   | SHARE     | nb_xyz           | ✓ OK
```

#### Step 2: Apply Filters

**Actor Filter:** Search for specific users

```
Actor Filter:
[Type name or select...] ▼

Results:
  ☐ Alice Chen (alice@company.com)
  ☐ Bob Johnson (bob@company.com)
  ☐ Carol Davis (carol@company.com)
  ☐ David Smith (david@company.com)
  ☐ All ThinkerAgents
  ☐ System (internal actions)
```

Check one or more users to filter logs.

**Action Filter:** Filter by operation type

```
Action Type:
  ☑ WRITE (create entries)
  ☑ REVISE (update entries)
  ☑ READ (view entries)
  ☑ SHARE (grant access)
  ☐ DELETE (remove entries)
  ☐ ADMIN (manage notebook)
```

**Resource Filter:** Filter by notebook/entry

```
Resource:
[Type notebook name...]

Results:
  ☐ Engineering / Architecture (nb_eng_arch)
  ☐ Operations / Runbooks (nb_ops_runbooks)
  ☐ Security / Incidents (nb_sec_incidents)
```

**Date Range:** Set audit period

```
From: [Jan 1, 2026] To: [Jan 31, 2026]

Presets:
  ○ Last 7 days
  ○ Last 30 days (default)
  ◉ Custom range
```

**Status Filter:** Include/exclude results

```
☑ Success (operations that succeeded)
☑ Failure (operations that failed for technical reasons)
☑ Denied (access control denials)

Example: Uncheck "Denied" to see only successful operations
```

#### Step 3: Examine Audit Events

Each audit event shows:

```
Jan 31, 2:30 PM - Jane Smith accessed Engineering/Architecture

Action:     WRITE
Status:     ✓ Success
Resource:   Notebook: nb_eng_arch, Entry: entry_abc123
Actor:      Jane Smith (auth_hash_xyz)
Timestamp:  Jan 31, 2026, 2:30 PM UTC
IP Address: 192.168.1.50
User Agent: Chrome 120.0 / macOS
Location:   San Francisco, US (GeoIP)

Details:
  Entry Title: "Microservices Architecture Decision"
  Entry Topic: organization/engineering/architecture
  Signature:   Valid (Ed25519 signature verified)
  Clearance Used: SECRET / {Operations}

[View Entry] [Related Events for Jane Smith] [View Similar Actions]
```

#### Step 4: Investigate Anomalies

Look for suspicious patterns:

```
Anomaly Indicators:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

⚠️  Carol Davis accessed 47 entries in 5 minutes
    (normal: 2-3 per hour)
    Action: [Investigate] [Allowlist Pattern]

⚠️  System (internal) failed to embed 12 entries
    (retry pattern detected)
    Action: [View Error Details] [Notify Admin]

❌ Alice Chen denied access to TOP_SECRET notebook 3 times
    (clearance mismatch)
    Action: [Review Clearance] [Contact Alice]
```

### Verification

Confirm your audit query is complete:

- [ ] Correct date range selected
- [ ] Filters applied appropriately
- [ ] All relevant events retrieved
- [ ] No suspicious patterns missed
- [ ] Audit trail is uninterrupted (no gaps)

### Tips & Tricks

#### Export for Reporting

Click **"[Export to CSV]"** to download results:

```
audit_log_2026-01_31.csv

timestamp,actor,action,resource,status,ip_address,location
2026-01-31T14:30:00Z,Jane Smith,WRITE,nb_eng_arch/entry_abc123,success,192.168.1.50,San Francisco
2026-01-31T14:15:00Z,Bob Johnson,READ,nb_abc/entry_456,success,10.0.0.5,New York
...
```

Use in Excel/Google Sheets for further analysis.

#### Generate Compliance Report

Click **"[Generate Report]"** for automated output:

```
Compliance Audit Report - January 2026
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Executive Summary:
  Total Events: 47,329
  Success Rate: 99.2%
  Denied Accesses: 384 (0.8%)
  Critical Incidents: 0

Access Denial Analysis:
  Reason: Clearance Insufficient - 287
  Reason: Entry Not Found - 64
  Reason: Notebook Access Denied - 33

Top Accessed Resources:
  1. Engineering/Architecture: 12,483 accesses
  2. Operations/Runbooks: 8,923 accesses
  3. Security/Incidents: 4,521 accesses

Recommendations:
  • Review Carol Davis's clearance (accessing 15% of all entries)
  • Investigate 12 failed embedding jobs in EMBED_ENTRIES
  • Verify access to TOP_SECRET entries (47 accesses, 3 denials)
```

#### Real-Time Monitoring

Set up continuous monitoring for specific patterns:

```
[Create Alert]

Alert Name: Unusual Access Pattern

Trigger Condition:
  Same user accesses > 50 entries in < 1 hour
  AND entries are in different topics
  AND user's normal pattern is 5-10 per hour

Actions:
  ☑ Send notification
  ☑ Log to compliance queue
  ☐ Automatically disable account (don't recommend)

[Save Alert]
```

---

## Workflow 2: Investigating Security Events

### Overview

Deep-dive investigation when you detect access denials, unusual patterns, or suspected policy violations.

**Use case:** Multiple failed access attempts to a TOP_SECRET notebook. You investigate to determine if it's a misconfiguration or a security incident.

**Related workflows:**
- [Querying Global Audit Logs](#workflow-1-querying-global-audit-logs) — Find the events
- [Notebook-Scoped Auditing](#workflow-3-notebook-scoped-auditing) — Focused audit

### Prerequisites

- [ ] Audit admin role
- [ ] Specific event or pattern to investigate
- [ ] Access to security logs and incident reporting system

### Step-by-Step Instructions

#### Step 1: Identify Suspicious Events

From the audit log, find events matching one of these patterns:

```
❌ Red Flags:
  • Multiple DENIED events from one user (attempted breach?)
  • Unusual volume (Carol accessed 100 entries in 30 min)
  • Off-hours access (access at 3 AM on Sunday)
  • Access to mismatched topics (why is developer accessing HR files?)
  • Privilege escalation (user suddenly accessing TOP_SECRET)
  • Failed operations (500+ failed embeds in one hour)
```

#### Step 2: View Detailed Event

Click on a suspicious event for full details:

```
Investigation: Access Denial - TOP_SECRET Data

Primary Event:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Timestamp: Jan 31, 2:15 PM
Actor: Alice Chen (alice@company.com)
Action: READ
Resource: Notebook "Security / TOP_SECRET Planning"
Status: ✗ DENIED

Denial Reason:
  Clearance Insufficient
  Required: TOP_SECRET / {Operations, Strategic}
  User has: SECRET / {Operations}
  Gap: Missing TOP_SECRET level + Strategic compartment

User Context:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Groups: Engineering, Backend Team, Project Alpha
Clearance: SECRET / {Operations}
Last clearance change: 3 months ago
Previous denied accesses: 0 (first time)

IP/Session Context:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

IP Address: 192.168.1.200
Location: San Francisco, US (matches normal location)
Device: Chrome 120 / macOS (matches normal device)
Session: New session (5 minutes old)
VPN: Not detected

Related Events:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[View all events for Alice Chen in last 7 days]
[View all accesses to this notebook in last 7 days]
[View all failed accesses to TOP_SECRET resources]
```

#### Step 3: Make Determination

Based on investigation, determine incident classification:

```
Incident Classification
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Incident Type:
  ○ False Positive (legitimate, permission issue)
  ○ Policy Violation (user bypassed or exceeded permissions)
  ○ Misconfiguration (system assigned wrong clearance)
  ○ Security Incident (unauthorized access attempt)
  ○ Suspicious Activity (needs investigation)

Severity (if applicable):
  ○ Low (informational)
  ○ Medium (policy question)
  ○ High (confirmed violation)
  ○ Critical (security breach)

Root Cause Analysis:
[Alice was recently promoted but clearance wasn't updated.
 She tried to access materials for her new role.]

Recommendation:
[Promote Alice to TOP_SECRET / {Operations, Strategic}
 clearance and notify her of successful access.]

[Log Finding] [Close Incident] [Escalate to Security]
```

#### Step 4: Take Action

Based on determination, take appropriate action:

**If False Positive:**
```
[✓ Resolve Incident - Permission Issue]

Actions taken:
  • Grant Alice TOP_SECRET clearance
  • Flush clearance cache
  • Verify access now works
  • Log resolution for compliance

Next: Verify access works, close incident.
```

**If Policy Violation:**
```
[✓ Resolve Incident - Policy Violation]

Actions taken:
  • Document violation in policy log
  • Notify user's manager
  • Review similar events
  • Update access controls if needed

Next: Follow up with manager.
```

**If Security Incident:**
```
[⚠️ ESCALATE - Security Incident]

Actions:
  • Lock user account (require immediate review)
  • Notify Security Team immediately
  • Preserve all related logs
  • Initiate incident response

Next: Contact Security Operations Center.
```

### Verification

Confirm investigation is thorough:

- [ ] Identified root cause
- [ ] Checked for related events
- [ ] Verified user context (location, device, patterns)
- [ ] Determined if isolated or pattern
- [ ] Documented findings
- [ ] Took appropriate action

---

## Workflow 3: Notebook-Scoped Auditing

### Overview

Audit a specific notebook to verify compliance with its policies, review access patterns, and track data handling.

**Use case:** Quarterly compliance review of the "TOP_SECRET Strategic Planning" notebook. You need to verify who accessed it, what they did, and if any policy violations occurred.

**Related workflows:**
- [Querying Global Audit Logs](#workflow-1-querying-global-audit-logs) — Org-wide audits
- [Investigating Security Events](#workflow-2-investigating-security-events) — Deep investigation

### Prerequisites

- [ ] At least "Read" access to the notebook
- [ ] Audit or admin role
- [ ] Clear compliance requirements

### Step-by-Step Instructions

#### Step 1: Access Notebook Audit Trail

**Navigate to:** Notebooks → Select notebook → Audit tab

```
Engineering / Architecture Notebook
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Entry Feed] [Settings] [Audit] [Statistics]

Notebook Audit Trail
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Classification: SECRET / {Operations}
Owner: Engineering / Backend Team
Created: Jan 1, 2026, 9:00 AM (by Alice Chen)
Last Modified: Jan 31, 2026, 2:30 PM

Access Summary (Last 30 days):
  Total reads: 1,247
  Total writes: 89
  Total revisions: 23
  Total admin actions: 12
  Access denials: 0

Detailed Audit Log:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Filters] [Export] [Generate Report]

Timestamp      | Actor      | Action     | Details           | Status
───────────────┼────────────┼────────────┼───────────────────┼────────
Jan 31, 2:30 PM | Jane S.    | WRITE      | New entry created | ✓
Jan 31, 2:15 PM | Bob J.     | READ       | 15 entries read   | ✓
Jan 31, 1:45 PM | Alice C.   | REVISE     | Entry updated     | ✓
Jan 31, 1:30 PM | Carol D.   | ADMIN      | Access granted    | ✓
Jan 31, 1:00 PM | Eve W.     | SHARE      | Group added       | ✓
```

#### Step 2: Review Access Control Changes

Track who has access and when it changed:

```
Access Control Changes (Last 30 days):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Jan 31, 1:30 PM - Carol Davis granted "Operations Team" Read+Write access
  Granted by: Alice Chen
  Reason: Team needs visibility for incident response

Jan 28, 10:00 AM - Contractor "David Smith" removed from Read+Write
  Removed by: Alice Chen
  Reason: Contract ended

Jan 15, 2:00 PM - "Executive Council" granted Read access
  Granted by: Alice Chen
  Reason: Quarterly review attendance

Changes Summary:
  ✓ All changes documented with reasons
  ✓ All grantors are notebook admins
  ✓ No orphaned access (all removals justified)
  ✓ Access levels appropriate for roles
```

#### Step 3: Review Data Lifecycle

Track entries created, modified, and retained:

```
Entry Lifecycle Audit
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Entries Created: 89 (month-to-date)
  Average per day: 2.9
  Range: 1-7 entries per day
  Busiest day: Jan 21 (7 entries)

Entries Revised: 23
  Revision rate: 25.8% of entries (1 in 4 has revision)
  Average revisions per entry: 1.3
  Longest history: 4 revisions

Entries Deleted: 0
  Retention policy: 7 years
  Next purge eligible: None

Data Classification Compliance:
  ✓ 100% of entries labeled SECRET / {Operations}
  ✓ 0 entries with inconsistent classification
  ✓ 0 entries with higher classification (not breached)
  ✓ All entries have external references checked
```

#### Step 4: Generate Compliance Report

Click **"[Generate Report]"**:

```
Notebook Compliance Report - January 2026
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Notebook: Engineering / Architecture
Classification: SECRET / {Operations}
Report Period: January 1-31, 2026
Generated: Jan 31, 2026, 3:00 PM

EXECUTIVE SUMMARY
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Compliance Status: ✓ COMPLIANT
  • All entries properly classified
  • Access control is appropriate
  • No policy violations detected
  • All changes documented

DETAILED FINDINGS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Access Control:
  Approved Users: 12
  Approved Groups: 3
  Denied Accesses: 0
  Clearance Mismatches: 0
  ✓ PASSED

Data Classification:
  Total Entries: 89
  Correct Classification: 89/89 (100%)
  Misclassified: 0
  ✓ PASSED

Entry Lifecycle:
  Retention Policy: 7 years
  Eligible for Purge: 0 entries
  Average Version Count: 1.3
  ✓ PASSED

RECOMMENDATIONS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

1. Continue current access practices (working well)
2. Monitor revision patterns (stable at 25.8%)
3. Review contractor removals monthly (currently quarterly)

SIGN-OFF
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Auditor: Jane Smith (Compliance Officer)
Date: January 31, 2026
Signature: [Digital signature verified]

[Download PDF] [Email Report] [Acknowledge Audit]
```

### Verification

Confirm notebook audit is complete:

- [ ] Reviewed all access control changes
- [ ] Verified data classification
- [ ] Checked entry lifecycle
- [ ] Examined revision patterns
- [ ] Generated compliance report
- [ ] Documented findings

### Tips & Tricks

#### Automate Compliance Reviews

Set up recurring audits:

```
[Schedule Recurring Audit]

Notebook: Engineering / Architecture
Frequency: Monthly (last day of month)
Recipients: compliance@company.com
Report Type: Abbreviated (key metrics only)

[Save Schedule]
```

#### Compare Year-Over-Year

Track trends:

```
Access Pattern Trends
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Total Reads per Month:
  Jan 2025: 847   Jan 2026: 1,247 (+47%)
  Feb 2025: 921   Feb 2026: (projected 1,300+)

Entry Creation Rate:
  Jan 2025: 42 entries   Jan 2026: 89 entries (+112%)

Revision Rate:
  Jan 2025: 18% of entries
  Jan 2026: 26% of entries (more collaborative)

Interpretation: Notebook growing in usage and collaboration.
Recommendation: Consider archiving to separate "historical" notebook.
```

#### Bulk Export for Compliance

Export all audit logs for external auditors:

```
[Bulk Export - Last 12 Months]

Format: CSV
Period: Jan 1 - Dec 31, 2025
File: notebook_audit_2025.csv (2.3 MB)

Columns included:
  - timestamp, actor, action, resource, status
  - ip_address, location, clearance_used
  - entry_classification, entry_topic
  - signature_valid, details

[Download] [Email to Auditor] [Encrypt & Send]
```

---

## Summary: Quick Reference

### The 3 Workflows at a Glance

| Workflow | Purpose | Time | Frequency |
|----------|---------|------|-----------|
| **1. Query Logs** | Find audit events | 15-30 min | Quarterly |
| **2. Investigate** | Deep dive on incidents | 30-60 min | As needed |
| **3. Notebook Audit** | Compliance review | 20-40 min | Monthly |

### Your Audit Loop

```
1. Query Global Logs (baseline)
   ↓
2. Find Anomalies
   ↓
3. Investigate if needed
   ↓
4. Generate Reports
   ↓
5. Follow up on findings
```

### Audit Focus Areas

- **Access Control:** Who has access? Is it appropriate?
- **Classification:** Are entries labeled correctly?
- **Lifecycle:** Are entries retained/purged per policy?
- **Changes:** Are all modifications authorized and logged?
- **Incidents:** Are security incidents handled properly?

---

## Related Personas

Your workflows overlap with:

- **[System Administrator](08-system-administrator.md)** — Who manage platform-wide security
- **[Knowledge Contributor](04-knowledge-contributor.md)** — Whose access you audit
- **[Notebook Owner](06-notebook-owner.md)** — Who manage notebooks you audit

---

## Troubleshooting

### Can't Access Audit Logs

**Cause:** Don't have audit admin role

**Solution:**
1. Request audit admin role from your organization admin
2. Verify you're in the right organization
3. Check if role is limited to specific notebooks

### Audit Logs Show Gaps

**Cause:** Log rotation or system maintenance

**Solution:**
1. Check system status page for known outages
2. Verify your date range is correct
3. Contact admin if gaps are suspicious

### Export File Too Large

**Cause:** Exporting too much data at once

**Solution:**
1. Narrow date range
2. Filter by specific actor/resource
3. Use CSV format (smaller than JSON)
4. Export in batches by date

---

**Last updated:** February 21, 2026
**Chapter version:** 1.0.0 (Beta)
**Platform version:** 2.1.0
