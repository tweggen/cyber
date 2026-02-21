# Chapter 8: System Administrator

## Role Overview

As a **System Administrator**, you manage the Cyber platform itself—users, quotas, system health, and global agent configuration. You're responsible for keeping the system running smoothly and enforcing usage policies.

**Key Responsibilities:**
- Manage user accounts globally (create, lock, unlock, delete)
- Set and enforce usage quotas
- Monitor system health and performance
- Manage ThinkerAgent deployment and configuration
- Review platform-wide security settings
- Handle user support and account issues

**Required Permissions:**
- "Admin" role (platform-level, not organization-level)
- ROOT or SUPERUSER clearance (highest available)
- Understanding of system architecture and operations

**Typical Workflows:** 4 core workflows in this chapter

---

## Workflow 1: User Management

### Overview

Create user accounts, manage permissions, lock/unlock accounts, and handle user lifecycle.

**Use case:** New employee joins; you create their account and set initial permissions. Later, they leave and you deactivate their account.

**Related workflows:**
- [Quota Management](#workflow-2-quota-management) — Set usage limits
- [Agent Management](#workflow-4-agent-management) — Grant agent access

### Prerequisites

- [ ] System admin access (ROOT/SUPERUSER clearance)
- [ ] User information (email, organization, initial clearance)
- [ ] Clear policy for account creation

### Step-by-Step Instructions

#### Step 1: Access User Management

**Navigate to:** Admin Panel → Users (or Settings → System → Users)

```
User Management
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[+ Create User] [Import Users] [Export Users]

Active Users: 147

Search/Filter:
  [Search by email...] [Organization ▼] [Status ▼]

User List:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Email                    | Org       | Clearance      | Status  | Actions
─────────────────────────┼───────────┼────────────────┼─────────┼──────────
alice@company.com        | MyCompany | SECRET/{Ops}   | Active  | [Edit]
bob@company.com          | MyCompany | CONF/{Ops}     | Active  | [Edit]
carol@partner.org        | Partner   | CONF/{}        | Active  | [Edit]
david@company.com        | MyCompany | SECRET/{Ops}   | Locked  | [Unlock]
```

Click **"[+ Create User]"**.

#### Step 2: Create User Account

```
Create User Account
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Basic Information:
  Email *: [new.user@company.com]
  Full Name: [Jane Smith]
  Organization *: [Dropdown: Select...]
    ○ MyCompany
    ○ Partner Org
    ○ Contractor Org

Initial Clearance:
  Level *: [CONFIDENTIAL ▼]
  Compartments: [Select compartments...]
    ☐ Operations
    ☐ Strategic Planning
    ☐ Database Access

User Type:
  ○ Human User (standard)
  ◉ Service Account (for automation)
  ○ Bot/Agent (see Agent Management)

Sending Options:
  ☑ Send activation email
  ☑ Send temporary password (if password auth)

[Create Account] [Cancel]
```

#### Step 3: Manage Active User

To view or edit an existing user, click **"[Edit]"**:

```
Edit User: Jane Smith
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Profile:
  Email: jane@company.com
  Full Name: Jane Smith
  Organization: MyCompany
  Created: Jan 1, 2026
  Last Login: Jan 31, 2026, 2:30 PM

Clearance:
  Current: SECRET / {Operations, Database}
  Update: [Change Clearance ▼]

Account Status:
  ☑ Active (can log in)
  ☐ Locked (cannot log in, but account exists)
  ☐ Disabled (account deleted, data archived)

Quota Usage:
  Notebooks Created: 3/5 (60%)
  Entries Written: 245/1000 (24.5%)
  Storage Used: 12.5 MB / 1 GB (1.25%)
  [View Detail] [Reset Quotas]

Actions:
  [Lock Account] [Unlock Account] [Reset Password]
  [View Audit Trail] [Delete Account]

[Save Changes] [Cancel]
```

#### Step 4: Lock/Unlock Account

If a user forgets their password or has security issues:

```
[Lock Account]

Reason for locking:
[Dropdown: Select...]
  • User forgot password
  • Security incident investigation
  • Account compromise suspicion
  • User on leave
  • Other (specify)

Notify user?
  ☑ Send notification that account was locked
  ☐ Silent lock (for security incidents)

[Confirm Lock]
```

User can't log in but account/data remain intact.

To reactivate:

```
[Unlock Account]

Reason for unlocking:
[Dropdown: Select...]
  • Password reset complete
  • Investigation cleared
  • User returned from leave
  • Other

Send temporary password?
  ☑ Send new temporary password
  ☐ User will use existing password

[Confirm Unlock]
```

#### Step 5: Bulk User Management

Import multiple users at once:

```
[Import Users]

File Format: CSV
[Upload file: users_batch_jan2026.csv]

Preview:
  email,organization,clearance_level,compartments,user_type
  alice@company.com,MyCompany,SECRET,Operations;Database,human
  bob@company.com,MyCompany,CONFIDENTIAL,Operations,human
  carol@company.com,MyCompany,CONFIDENTIAL,,human

Validation:
  ✓ 3 rows ready to import
  ✓ All emails valid
  ✓ All organizations exist
  ✓ All clearances valid

[Preview Changes] [Import] [Cancel]
```

### Verification

Confirm user management is working:

- [ ] New users can log in
- [ ] Clearances are correct
- [ ] Quotas are initialized
- [ ] Locked accounts can't access
- [ ] Unlock restores access
- [ ] Audit trail records all changes

---

## Workflow 2: Quota Management

### Overview

Set and monitor per-user quotas for notebooks, entries, and storage to prevent resource exhaustion.

**Use case:** A heavy user is approaching their storage quota. You increase it to prevent disruption.

**Related workflows:**
- [User Management](#workflow-1-user-management) — Create users with quotas
- [System Monitoring](#workflow-3-system-monitoring) — Monitor quota usage

### Prerequisites

- [ ] System admin access
- [ ] User to update quotas for
- [ ] Clear policy for quota limits

### Step-by-Step Instructions

#### Step 1: Access Quota Management

**Navigate to:** Admin → Quotas (or Users → [User] → Quotas)

```
Quota Management
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Default Organization Quotas:
  Notebooks per user: 10
  Entries per notebook: 10,000
  Storage per user: 1 GB
  API calls per day: 10,000

[+ Create Custom Quota] [Reset to Defaults]

User Custom Quotas:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Search user: [alice@company.com]

[Edit Quotas]
```

#### Step 2: Set User Quotas

```
Quotas for: Alice Chen
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Default (for all other users):
  Notebooks: 10
  Entries per notebook: 10,000
  Storage: 1 GB

Alice's Custom Quotas:
  Notebooks:
    Default: 10    Adjusted: [25] (she manages multiple teams)

  Entries per notebook:
    Default: 10,000   Adjusted: [50,000] (large-scale project)

  Storage:
    Default: 1 GB     Adjusted: [5 GB] (research data)

  API calls per day:
    Default: 10,000   Adjusted: [50,000] (integrations)

Effective Quotas (after changes):
  ✓ Notebooks: 25
  ✓ Entries: 50,000 per notebook
  ✓ Storage: 5 GB total
  ✓ API: 50,000 calls/day

Justification:
[Alice manages 8 cross-functional projects requiring
 heavy data management and automated integrations.]

[Save Quotas] [Cancel] [Reset to Defaults]
```

#### Step 3: Monitor Quota Usage

View current usage for a user:

```
Quota Usage: Alice Chen
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Notebooks:
  Used: 18/25 (72%) ▓▓▓▓▓▓▓░░ [Close to limit]
  Recent: Created "Marketing Q2" on Jan 31
  Action: [Warn User] [Increase Quota]

Entries per Notebook:
  Max Used (across notebooks): 8,247/50,000 (16%)
  Notebook: "Q1 Planning" has 8,247 entries
  Action: [No action needed]

Storage:
  Used: 4.2 GB / 5 GB (84%) ▓▓▓▓▓▓▓▓░ [Close to limit]
  Recent uploads: 450 MB in last 7 days
  Projected: Will exceed limit in ~3 days
  Action: [Increase Quota] [Warn User] [Archive Entries]

API Calls:
  Used (today): 32,456 / 50,000 (65%)
  Average daily: 28,000
  Peak: 47,000 (Jan 29)
  Action: [No action needed]

[Adjust Quotas] [Notify User] [Archive Entries]
```

#### Step 4: Enforce Quotas

When quotas are exceeded:

```
Quota Exceeded: Alice Chen

Storage limit reached (5 GB / 5 GB)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Actions available:
  [Increase Quota] (recommended for active users)
  [Archive Old Entries] (compress and move to archive)
  [Delete Entries] (permanent, unreversible)
  [Lock Account] (last resort, prevent more writes)

Current enforcement: Warning (writes still allowed)
Options:
  ○ Warning only (user can still write)
  ◉ Block new writes (force action)
  ○ Lock account (emergency)

[Apply] [Notify User] [Cancel]
```

### Verification

Confirm quota management is working:

- [ ] Default quotas set appropriately
- [ ] Custom quotas applied to heavy users
- [ ] Usage monitored and alerted
- [ ] Exceeded quotas enforced
- [ ] Users notified of limits

---

## Workflow 3: System Monitoring

### Overview

Monitor platform health, performance, and usage. Review dashboards and metrics to ensure system stability.

**Use case:** You notice slow performance; you check the dashboard and find a ThinkerAgent is down, causing job backlog.

**Related workflows:**
- [Agent Management](#workflow-4-agent-management) — Restart agents
- [Quota Management](#workflow-2-quota-management) — Monitor resource usage

### Prerequisites

- [ ] System admin access
- [ ] Understanding of system metrics
- [ ] Access to alerting system

### Step-by-Step Instructions

#### Step 1: Access Dashboard

**Navigate to:** Admin → Dashboard (or Home → System Status)

```
System Dashboard
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

System Health: ✅ All Systems Operational
Last Updated: 31 Jan 2026, 3:30 PM

Overall Metrics:
  Uptime: 99.97% (last 30 days)
  Response Time: 145ms average
  Error Rate: 0.003% (< 1 per 100,000 operations)

Active Users: 147 (69 in last 24 hours)
Notebooks: 389 (12 created this week)
Entries: 18,392 (145 added today)

Server Status:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

API Server:     ✅ Healthy (98% CPU, 72% Memory)
Database:       ✅ Healthy (replication lag: 50ms)
Cache Layer:    ✅ Healthy (hit rate: 94%)
Job Queue:      ⚠️  SLOW (backlog: 234 jobs, 12 failed)
Search Index:   ✅ Healthy (updated 5 minutes ago)
```

#### Step 2: Investigate Issues

Click on the ⚠️ symbol for more details:

```
Job Queue - Performance Issue
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Status: ⚠️  DEGRADED (high backlog)

Job Statistics:
  Pending: 234 jobs (normal: 5-10)
  In Progress: 0 jobs (agents not processing!)
  Completed: 12,847
  Failed: 12 (since midnight)

Failed Jobs Detail:
  EMBED_ENTRIES: 8 failures
    Error: "Agent unreachable: embedding-worker-1"
  DISTILL_CLAIMS: 4 failures
    Error: "Timeout waiting for agent response"

Agent Status:
  embedding-worker-1: ❌ OFFLINE (last seen: 2 hours ago)
  embedding-worker-2: ✅ ONLINE (processing 5 jobs)
  claims-distiller-1: ✅ ONLINE (processing 3 jobs)
  comparison-engine: ✅ ONLINE (idle)

Recommended Actions:
  1. [Investigate Agent] - Check why embedding-worker-1 went offline
  2. [Restart Agent] - Attempt graceful restart
  3. [Failover] - Redirect jobs to embedding-worker-2
  4. [Alert Team] - Notify SRE team

[Take Action] [View Logs] [Contact Support]
```

#### Step 3: Review Performance Metrics

Monitor key metrics over time:

```
Performance Trends (Last 7 Days)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Response Time:
  Average: 145ms ▃▃▃▃▄▄▅ (trending up slightly)
  P99: 1,200ms
  Max: 5,432ms (Jan 29, 2 PM - database maintenance)

Error Rate:
  0.003% ▁▁▁▁▁▁▁ (very stable, low)
  Errors: 347 (mostly timeout errors)

Job Processing Time:
  Average: 8.5 seconds per entry
  Bottleneck: Vector embeddings (7.2s per entry)
  P99: 22 seconds

Uptime:
  99.97% (2 incidents: Jan 29 maintenance, Jan 15 database failover)
  Target: 99.95% ✓ Exceeded
```

#### Step 4: Set Up Alerts

Configure notifications for issues:

```
[Create Alert]

Alert Name: Job Queue Backlog Critical

Trigger Condition:
  Pending jobs > 100 AND In-progress jobs < 2
  (indicates agent/worker failure)

Condition Evaluation: Every 5 minutes

Action:
  ☑ Send Slack notification to #incidents
  ☑ Email sre-team@company.com
  ☑ Create incident ticket
  ☐ Auto-restart agents (risky)

Escalation:
  If unresolved after 30 minutes, page on-call SRE

[Save Alert] [Test Alert]
```

### Verification

Confirm system monitoring is effective:

- [ ] Dashboard shows current health
- [ ] Issues are detected quickly
- [ ] Alerts are configured and working
- [ ] Performance trends are visible
- [ ] You can drill down into problems
- [ ] Recommended actions are clear

---

## Workflow 4: Agent Management

### Overview

Register, configure, and manage ThinkerAgents globally. Monitor agent health and handle agent-related incidents.

**Use case:** You need to deploy a new embeddings agent for faster search indexing. You register it, monitor startup, and ensure it begins processing jobs.

**Related workflows:**
- [Organization Administrator](05-org-administrator.md#workflow-4-configuring-thinkeragents) — Org-level agent configuration
- [System Monitoring](#workflow-3-system-monitoring) — Monitor agent health

### Prerequisites

- [ ] System admin access
- [ ] Agent software deployed or ready to deploy
- [ ] Understanding of agent types and capabilities

### Step-by-Step Instructions

#### Step 1: Access Agent Management

**Navigate to:** Admin → Agents

```
Agent Management
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[+ Register Agent] [Import Agents]

Active Agents: 6

Agent Fleet Status:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Name                       | Type       | Org      | Status  | Load
──────────────────────────┼────────────┼──────────┼─────────┼──────
embedding-worker-1        | Embedding  | Global   | ✅ Online | 4/5
embedding-worker-2        | Embedding  | Global   | ✅ Online | 2/5
claims-distiller-1        | Claims     | Global   | ✅ Online | 3/5
comparison-engine         | Comparison | Global   | ✅ Online | 1/5
custom-processor-acme     | Custom     | ACME Inc | ✅ Online | 0/3
research-embedder         | Embedding  | Research | ⏳ Starting| N/A
```

#### Step 2: Register New Agent

Click **"[+ Register Agent]"**:

```
Register New ThinkerAgent
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Agent Details:
  Name *: [research-embedder]
  Type *: [Embedding ▼]
  Organization *: [Research ▼]

  Description:
  [High-performance embedding service for research notebooks]

Deployment:
  Infrastructure Location: [us-west-2-prod]
  Health Check Endpoint: [https://agent.research.internal:8080/health]
  Max Concurrent Jobs: [10]

Security:
  Max Classification: [CONFIDENTIAL ▼]
  Compartments: [Select...]
    ☑ Research
    ☐ Executive
    ☐ Operations

Credentials:
  [Generate Credentials]

  Agent ID: (will be generated)
  Token: (will be shown once)

[Register & Generate Credentials] [Cancel]
```

#### Step 3: Deploy Agent

After registration, get credentials:

```
✓ Agent Registered!

research-embedder

Agent ID: research-embedder-abc123
Token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

⚠️  Deploy agent with these credentials:

Environment Variables:
  export CYBER_AGENT_ID=research-embedder-abc123
  export CYBER_AGENT_TOKEN=eyJ...
  export CYBER_SERVER=https://cyber.company.com

Deployment Steps:
  1. Copy credentials to agent's deployment environment
  2. Start agent process/container
  3. Agent will connect and report health status
  4. Status will change to "Online" when healthy

Monitor Deployment:
  [Refresh Status] [View Agent Logs]
```

#### Step 4: Monitor Agent Health

```
Agent: research-embedder
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Status: ⏳ Starting (2 minutes since registration)
Last Heartbeat: Never (agent hasn't connected yet)
Uptime: N/A

Expected in next 5 minutes:
  • Agent connects and sends first heartbeat
  • Status changes from "Starting" to "Online"
  • Agent becomes eligible for job assignments

Actions:
  [Refresh Status] [View Deployment Logs]
  [Check Network Connectivity] [Force Health Check]

If still not online after 10 minutes:
  [Investigate] [Restart Agent] [Rollback Deployment]
```

Once online:

```
Agent: research-embedder
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Status: ✅ Online (healthy)
Last Heartbeat: 30 seconds ago
Uptime: 8 minutes

Performance:
  CPU: 45%
  Memory: 2.1 GB / 4 GB
  Jobs Processed: 12
  Failed Jobs: 0
  Average Job Time: 7.8 seconds

Current Load:
  In Progress: 2/10 jobs
  Queue Wait: 0 (processing immediately)

Recent Jobs:
  [View Last 10] [Export Job Log]

Actions:
  [Update Config] [Rotate Credentials]
  [Set Performance Limits] [Pause Agent] [Deregister]
```

#### Step 5: Manage Agent Fleet

For multiple agents, manage them together:

```
Agent Fleet Management
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Load Balancing:
  Total Jobs Pending: 47
  Distribution (auto-balancing):
    embedding-worker-1: 2/5 (40%)
    embedding-worker-2: 2/5 (40%)
    research-embedder: 1/10 (10%)

Scaling Recommendations:
  ✓ Current capacity is sufficient (70% avg utilization)
  ⚠️ If load increases 50%+, add another embedding agent

Alerts & Policies:
  [Max concurrent jobs per agent: 10]
  [Min agents per job type: 1] (prevent single point of failure)
  [Auto-restart on failure: Enabled]
  [Credential rotation: Every 90 days]

[Edit Policies] [Scale Fleet] [View Metrics]
```

### Verification

Confirm agent management is working:

- [ ] New agent registers successfully
- [ ] Credentials are securely issued
- [ ] Agent connects and comes online
- [ ] Health checks pass
- [ ] Jobs are being assigned
- [ ] Failed jobs are handled appropriately

---

## Summary: Quick Reference

### The 4 Workflows at a Glance

| Workflow | Purpose | Time | Frequency |
|----------|---------|------|-----------|
| **1. User Management** | Create/manage accounts | 10-20 min | As needed |
| **2. Quota Management** | Set usage limits | 10-15 min | Quarterly |
| **3. System Monitoring** | Health & performance | 5-10 min | Daily |
| **4. Agent Management** | Deploy/manage agents | 20-30 min | Quarterly |

---

## Related Personas

Your workflows overlap with:

- **[Organization Administrator](05-org-administrator.md)** — Who manage organization-level settings
- **[ThinkerAgent Operator](09-thinker-operator.md)** — Who deploy agents operationally
- **[Auditor](07-auditor.md)** — Who review your admin actions

---

**Last updated:** February 21, 2026
**Chapter version:** 1.0.0 (Beta)
**Platform version:** 2.1.0
