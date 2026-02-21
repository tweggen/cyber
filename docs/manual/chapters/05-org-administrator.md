# Chapter 5: Organization Administrator

## Role Overview

As an **Organization Administrator**, you shape your organization's structure and security posture in Cyber. You manage organizational groups, assign security clearances, and ensure compliance with classification requirements. Your decisions enable other users to do their jobs securely.

**Key Responsibilities:**
- Design and maintain organizational hierarchy (DAG structure)
- Grant and revoke security clearances (levels + compartments)
- Manage group membership and roles
- Configure ThinkerAgents and security parameters
- Ensure Bell-LaPadula compliance
- Monitor organizational audit trails

**Required Permissions:**
- "Admin" role in your organization
- Top-secret or SECRET clearance (minimum)
- Understanding of security model fundamentals

**Typical Workflows:** 4 core workflows in this chapter

---

## Workflow 1: Creating Organizational Structure

### Overview

Design your organization's group hierarchy—who reports to whom, which teams collaborate, and how clearances flow through the organization.

**Use case:** You're setting up Cyber for a new organization or restructuring an existing team hierarchy.

**Related workflows:**
- [Managing Group Memberships](#workflow-2-managing-group-memberships) — Add users to groups after structure exists
- [Managing Clearances](#workflow-3-managing-security-clearances) — Assign clearances that respect the hierarchy

### Prerequisites

- [ ] Organization created (by system admin)
- [ ] Organization admin access
- [ ] Clear understanding of your org structure
- [ ] List of teams and reporting relationships

### Step-by-Step Instructions

#### Step 1: Access Organization Administration

**Navigate to:** Admin panel → Organizations → Select your org

1. Click the **Admin Panel** icon (gear) in top-right
2. Select **Organizations** from sidebar
3. Click your organization's name
4. You'll see the **Organization Dashboard**:

```
MyCompany Organization
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Overview] [Groups] [Members] [Audit Log] [Settings]

Group Hierarchy:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

MyCompany (root)
 ├── Engineering
 │   ├── Backend
 │   └── Infrastructure
 ├── Operations
 └── Finance
```

#### Step 2: Click "Groups" Tab

See the groups view where you manage structure:

```
Groups in MyCompany
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[+ New Group]

Group Hierarchy (DAG - Directed Acyclic Graph):

Name                 | Members | Notebooks | Actions
─────────────────────┼─────────┼───────────┼─────────
MyCompany (root)     | 45      | 3         | [Edit]
├─ Engineering       | 12      | 8         | [Edit]
│  ├─ Backend        | 5       | 4         | [Edit]
│  └─ Infrastructure | 7       | 4         | [Edit]
├─ Operations        | 15      | 5         | [Edit]
└─ Finance           | 3       | 2         | [Edit]
```

#### Step 3: Create a New Group

Click **"[+ New Group]"**:

```
Create New Group
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Group Name *
[Engineering]

Description
[Engineering teams: backend, infrastructure, security]

Parent Group(s) *
[Dropdown: Select parent(s)...]
  ☐ MyCompany (root)
  ☐ Operations
  ☐ (other options)

Classification Level (inherited from parents)
[Read-only: CONFIDENTIAL] ← Automatically set to highest
                            parent's level

Compartments (inherited from parents)
[Read-only: {Strategic Planning, Operations}]

[Create Group]  [Cancel]
```

**Key Concepts:**

| Term | Meaning |
|------|---------|
| **Parent Group** | The group above in hierarchy (can have multiple) |
| **DAG** | Directed Acyclic Graph — complex relationships allowed, but no cycles |
| **Classification Inheritance** | Child inherits the highest classification of any parent |
| **Compartment Inheritance** | Child gets union of all parent compartments |

#### Step 4: Set Classification & Compartments

Classification and compartments are **inherited** from parents and automatically elevated:

```
Example:
Parent "Engineering" = SECRET / {Operations}
Parent "Backend" = CONFIDENTIAL / {Operations, Infrastructure}

New child of both:
Inherits: SECRET / {Operations, Infrastructure}
(highest level + union of compartments)
```

You can **add more compartments** to a child group beyond what's inherited:

```
Group: Backend Team
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Inherited:    SECRET / {Operations, Infrastructure}
Add Compartment: [+ Add]
  ☑ Operations (inherited)
  ☑ Infrastructure (inherited)
  ☑ Database Access (new)
  ☐ Cryptography (not needed)

Final Classification: SECRET / {Operations, Infrastructure, Database Access}
```

#### Step 5: Verify Hierarchy (DAG)

After creating multiple groups, verify the hierarchy:

```
MyCompany Organization Structure
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

      MyCompany
      (CONFIDENTIAL / {Strategic})
      /                    \
Engineering               Operations
(SECRET / {Strategic,    (CONFIDENTIAL / {Operations})
 Operations})                 |
  /        \            ┌─────┴──────┐
Backend   Infrastructure  Incident    Admin
(SECRET/  (SECRET / {Ops, Response   (CONF / {Ops})
 {Strat,   Infra, DB})   {Ops})
 Ops,
 Infra,
 DB})
```

**Verify:**
- ✅ No cycles (Backend → Engineering → MyCompany → no cycle back)
- ✅ Classification increases or stays same going down
- ✅ Compartments accumulate as you go down

#### Step 6: Update Group (If Needed)

To modify an existing group:

1. Click **"[Edit]"** next to the group name
2. You can change:
   - Description
   - Parent relationships (add/remove parents)
   - Additional compartments
3. Click **"Save Changes"**

**What you can't change:**
- ❌ Group name (would break references)
- ❌ Remove parents (would break hierarchy)
- ❌ Reduce classification level (security violation)

### Verification

Confirm your structure is sound:

- [ ] All teams have parent groups
- [ ] No cycles exist (use the visualization)
- [ ] Classification increases or stays same going down
- [ ] Compartments accumulate correctly
- [ ] Root group exists and everyone can trace lineage to it
- [ ] Notebook owners understand their group's classification

### Tips & Tricks

#### Design Pattern: Functional + Geographic

Mix functional and geographic hierarchies:

```
Organization
├── By Function
│   ├── Engineering
│   ├── Operations
│   └── Finance
├── By Location
│   ├── North America
│   └── Europe
└── By Security Domain
    ├── Public Facing
    ├── Internal
    └── Confidential
```

A user can be in multiple groups (DAG allows this), so one engineer can be in:
- Engineering / Backend
- North America / Operations
- Internal / Security Domain

#### Classification Best Practices

Start conservative:

```
❌ Start with everything TOP_SECRET
✅ Start with CONFIDENTIAL
   Elevate groups only as needed
```

#### Compartment Naming

Use clear, consistent names:

```
✅ Good names:
   - Medical Research
   - Infrastructure Operations
   - Customer PII
   - Executive Confidential

❌ Bad names:
   - Top Secret Stuff
   - Internal
   - Secret1, Secret2
   - TBD
```

### Next Steps

After creating structure:
- [Manage group memberships](#workflow-2-managing-group-memberships) to add users
- [Assign clearances](#workflow-3-managing-security-clearances) at appropriate levels
- Create notebooks for teams (described in Chapter 6)

---

## Workflow 2: Managing Group Memberships

### Overview

Add users to groups and assign roles within those groups (member vs. admin).

**Use case:** A new engineer joins your team; you add them to the Engineering group.

**Related workflows:**
- [Creating Organizational Structure](#workflow-1-creating-organizational-structure) — Groups must exist first
- [Managing Clearances](#workflow-3-managing-security-clearances) — Clearances are independent of group membership

### Prerequisites

- [ ] Group exists (from Workflow 1)
- [ ] Users have been created in the system
- [ ] Organization admin access

### Step-by-Step Instructions

#### Step 1: Go to Group Management

**Navigate to:** Admin → Organizations → Groups → Select group

1. Click **Admin** in top-right
2. Go to **Organizations** → Your org → **Groups** tab
3. Click the group name
4. You'll see the group's member list:

```
Engineering Group
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Members (5):

Name          | Email              | Role    | Actions
──────────────┼────────────────────┼─────────┼──────────
Alice Chen    | alice@myco.com    | Admin   | [Remove]
Bob Johnson   | bob@myco.com      | Member  | [Edit Role]
Carol Davis   | carol@myco.com    | Member  | [Edit Role]
...
```

#### Step 2: Add a User

Click **"[+ Add Member]"**:

```
Add Member to Engineering
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Search for user:
[Dropdown: Start typing name/email...]

Results:
☐ David Smith (david@myco.com)
☐ Eve Wilson (eve@myco.com)
☐ Frank Brown (frank@myco.com)

Assign Role:
○ Member (can use group resources, can't manage)
◉ Admin   (can manage group, add/remove members)

[Add] [Cancel]
```

**Role Explanations:**

| Role | Can Do | Can't Do |
|------|--------|----------|
| **Member** | Use notebooks owned by group, create entries | Add/remove members, manage group settings |
| **Admin** | Everything + add/remove members, change roles | Delete group, modify classification |

#### Step 3: Bulk Add Members (Advanced)

For adding multiple people:

1. Click **"[Import Members]"**
2. Paste a list:
   ```
   alice@myco.com, admin
   bob@myco.com, member
   carol@myco.com, member
   david@myco.com, admin
   ```
3. Review mappings
4. Click **"[Confirm Import]"**

#### Step 4: Edit Member Roles

If someone's role needs to change:

1. Click **"[Edit Role]"** next to their name
2. Select new role:
   ```
   Change Role for Bob Johnson
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   Current: Member
   New:     ○ Member
            ◉ Admin
   [Save] [Cancel]
   ```
3. Click **"[Save]"**

#### Step 5: Remove a Member

Click **"[Remove]"** next to their name:

```
⚠️  Remove Alice Chen from Engineering?

This will:
  • Remove her access to Engineering-owned notebooks
  • Revoke her group admin rights (if applicable)
  • NOT delete her account or other group memberships

[Confirm Remove] [Cancel]
```

Click **"[Confirm Remove]"**.

### Verification

Confirm membership is correct:

- [ ] User appears in group member list
- [ ] User has correct role (Member or Admin)
- [ ] User can access group-owned notebooks
- [ ] User can't perform actions above their role
- [ ] Removal revoked access to group resources

### Tips & Tricks

#### Nested Admin Roles

Group admins can manage their own group but not parent/sibling groups:

```
Structure:
MyCompany (Org Admin)
├── Engineering (Group Admin: Alice)
│   ├── Backend (Group Admin: Bob)
│   └── Infrastructure (Group Admin: Carol)
└── Operations (Group Admin: David)

Permissions:
- Alice (Engineering Admin): Can manage Engineering + Backend + Infrastructure
- Bob (Backend Admin): Can manage only Backend
- Org Admin: Can manage everything
```

#### Audit Group Changes

All membership changes are logged. Check the group's audit trail:

Click **"[Audit Log]"** in the group settings.

#### Cascade Effects

When adding a user to a group, they automatically get:
- Access to all notebooks owned by that group
- Clearance requirements of that group (or higher)
- Audit trail visibility for that group

### Next Steps

After managing memberships:
- Assign appropriate clearances (Workflow 3)
- Create notebooks for the group (Chapter 6)
- Monitor group activity in audit logs

---

## Workflow 3: Managing Security Clearances

### Overview

Grant security clearances to principals (users or groups) specifying classification levels and compartments they can access.

**Use case:** A new contractor needs access to your infrastructure documentation, but only the non-sensitive parts. You grant them CONFIDENTIAL clearance without infrastructure compartments.

**Related workflows:**
- [Organizational Structure](#workflow-1-creating-organizational-structure) — Clearances work within your org structure
- [Group Membership](#workflow-2-managing-group-memberships) — Group membership affects clearance inheritance

### Prerequisites

- [ ] Group membership established
- [ ] User or group needs clearance assignment
- [ ] Organization admin access
- [ ] Understanding of Bell-LaPadula model (Chapter 2)

### Step-by-Step Instructions

#### Step 1: Access Clearance Management

**Navigate to:** Admin → Organizations → Members (or Groups)

1. Click **Admin** in top-right
2. Go to **Organizations** → Your org → **Members** tab
3. Find the user you want to grant clearance to:

```
Users in MyCompany
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[+ Invite User]

Name          | Email            | Clearance         | Actions
──────────────┼──────────────────┼───────────────────┼──────────
Alice Chen    | alice@myco.com  | SECRET / {Ops}    | [Edit]
Bob Johnson   | bob@myco.com    | CONFIDENTIAL / {} | [Edit]
Carol Davis   | carol@myco.com  | (no clearance)    | [Assign]
```

Click **"[Assign]"** or **"[Edit]"** next to the user.

#### Step 2: Set Classification Level

```
Set Clearance for Carol Davis
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Classification Level *
[Dropdown: Select level...]

○ PUBLIC       (no access restrictions)
◉ CONFIDENTIAL (internal use only)
○ SECRET       (restricted distribution)
○ TOP_SECRET   (severe impact if disclosed)

Current Group Clearance: SECRET / {Operations}
(Your clearance must be ≥ what you grant)
```

**Important Rule:** You can only grant clearance **up to your own level**. If you're CONFIDENTIAL, you can't grant SECRET.

#### Step 3: Select Compartments

Check the compartments the user needs:

```
Compartments
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

☑ Operations       (required for group membership)
☑ Database Access  (required for group membership)
☐ Executive Only   (additional, not required)
☐ Medical Records  (additional)
☐ Cryptography     (additional)

Final Clearance: CONFIDENTIAL / {Operations, Database Access}

Note: User's group requires ✓, you can add more ☐
```

**Rules:**
- ✅ User must have parent group's compartments
- ✅ You can add additional compartments
- ❌ You can't remove required compartments (from group)
- ❌ You can't grant compartments you don't have

#### Step 4: Apply Clearance

Click **"[Apply Clearance]"**:

```
✓ Clearance assigned!

Carol Davis now has: CONFIDENTIAL / {Operations, Database Access}

She can access:
  • All PUBLIC notebooks
  • All CONFIDENTIAL notebooks
  • CONFIDENTIAL entries with Operations or Database Access labels
  • NOT: SECRET or TOP_SECRET entries

Changes take effect immediately.
[OK]
```

#### Step 5: Update Clearance (When Needed)

If circumstances change (promotion, role change):

1. Click **"[Edit]"** next to the user
2. Modify level and/or compartments
3. Add reason for change:
   ```
   Clearance Change Reason:
   [Promoted to senior engineer, needs cryptography access]
   ```
4. Click **"[Update Clearance]"**

#### Step 6: Revoke Clearance (If Needed)

To revoke:

1. Click **"[Edit]"** next to the user
2. Click **"[Remove Clearance]"**
3. Confirm:
   ```
   ⚠️  Remove clearance for Carol Davis?

   She will:
     • Lose access to all classified notebooks
     • Keep PUBLIC notebook access
     • Still be in all groups (group membership unchanged)

   [Confirm] [Cancel]
   ```

### Verification

Confirm clearance is working:

- [ ] User has specified clearance level
- [ ] All required compartments are present
- [ ] User can access appropriate notebooks
- [ ] User can't access more restricted content
- [ ] Audit log shows clearance change
- [ ] User receives notification of clearance change

### Clearance Examples

#### Example 1: New Team Member

```
New Engineer (Alice)
  Assigned to: Engineering / Backend group
  Group requires: SECRET / {Operations}

Clearance to grant:
  Level:       SECRET (minimum: can't be less than group)
  Compartments: {Operations} (minimum: can't be less than group)

Full clearance: SECRET / {Operations}

Can read:
  ✅ PUBLIC anything
  ✅ CONFIDENTIAL anything
  ✅ SECRET / {Operations}
  ❌ SECRET / {Infrastructure, Cryptography}
  ❌ TOP_SECRET anything
```

#### Example 2: Contractor with Limited Access

```
Contractor (Bob)
  Short-term engagement
  Only needs documentation

Clearance to grant:
  Level:       CONFIDENTIAL (limited exposure)
  Compartments: {} (no sensitive compartments)

Full clearance: CONFIDENTIAL / {}

Can read:
  ✅ PUBLIC anything
  ✅ CONFIDENTIAL anything
  ❌ SECRET anything
  ❌ TOP_SECRET anything
```

#### Example 3: Cross-Functional Manager

```
Manager (Carol)
  Oversees Engineering AND Operations
  In both groups:
    - Engineering: SECRET / {Operations}
    - Operations: SECRET / {Operations}

Clearance to grant:
  Level:       SECRET (matches groups)
  Compartments: {Operations} (union of group requirements)

Can grant additional:
  ☑ Facilities Management (new compartment)

Full clearance: SECRET / {Operations, Facilities Management}
```

### Tips & Tricks

#### Clearance Cache

Changes take effect **immediately** in most cases, but access control caches may take up to **5 minutes** to refresh. To force immediate refresh:

Admin → Organizations → **Flush Clearance Cache**

#### Audit Clearance Changes

Track who changed what:

```
[View Clearance Audit Log]

Carol Davis Clearance History:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Jan 22, 2:30 PM - Updated by Alice Chen
  From: CONFIDENTIAL / {Operations}
  To:   CONFIDENTIAL / {Operations, Database}
  Reason: Promoted to senior engineer

Jan 15, 10:00 AM - Assigned by Admin
  Level: CONFIDENTIAL / {Operations}
  Reason: New team member
```

#### Principle of Least Privilege

Always apply minimum necessary clearance:

- ✅ Engineer working on database: Grant DATABASE compartment
- ❌ Engineer working on database: Grant all compartments
- ✅ New hire: Start with CONFIDENTIAL, promote as needed
- ❌ New hire: Give them SECRET "just in case"

### Next Steps

After assigning clearances:
- Create notebooks respecting the clearance hierarchy
- Test that access control works as expected
- Review clearances quarterly

---

## Workflow 4: Configuring ThinkerAgents

### Overview

Register AI processing workers (ThinkerAgents) with your organization, specifying their security classification and capabilities.

**Use case:** You have a background worker that processes notebook entries for embeddings and wants to register it with Cyber.

**Related workflows:**
- [Organizational Structure](#workflow-1-creating-organizational-structure) — Agents inherit org structure
- [System Administrator](08-system-administrator.md) — Platform-wide agent management

### Prerequisites

- [ ] Agent software is ready to deploy
- [ ] Organization admin access
- [ ] Understanding of agent's capabilities and security needs
- [ ] Network/infrastructure details for agent

### Step-by-Step Instructions

#### Step 1: Access Agent Management

**Navigate to:** Admin → Organizations → Agents (or Admin → Agents)

```
MyCompany Agents
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[+ Register Agent]

Name                    | Type        | Classification | Status
────────────────────────┼─────────────┼────────────────┼─────────
embedding-worker-1      | Embeddings  | CONFIDENTIAL   | Active
claims-distiller        | Claims      | SECRET         | Active
comparison-engine       | Comparison  | CONFIDENTIAL   | Idle
(none yet)
```

Click **"[+ Register Agent]"**.

#### Step 2: Enter Agent Details

```
Register New ThinkerAgent
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Agent Name *
[embedding-processor]

Agent Type *
[Dropdown: Select type...]
  • Embedding     (creates vector embeddings)
  • Claims       (extracts/distills claims)
  • Comparison   (compares entry semantics)
  • Custom       (other processing)

Description
[Processes all notebook entries to create embeddings for similarity search]

Infrastructure Location
[us-east-1-prod]
```

**Agent Types:**

| Type | Purpose | Classification |
|------|---------|-----------------|
| **Embedding** | Create vector embeddings for search/similarity | Usually CONFIDENTIAL |
| **Claims** | Extract and distill claims from entries | Usually SECRET |
| **Comparison** | Analyze semantic similarity between entries | Usually SECRET |
| **Custom** | Organization-specific processing | As needed |

#### Step 3: Set Security Classification

```
Security Classification
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Max Classification Level *
[Dropdown: Select level...]

○ CONFIDENTIAL (can process up to CONFIDENTIAL entries)
◉ SECRET       (can process up to SECRET entries)
○ TOP_SECRET   (can process TOP_SECRET entries)

Compartments *
[Multi-select: Choose compartments...]

☑ Operations
☑ Database
☐ Executive
☐ Medical

Note: Agent can process entries with any subset of these compartments.
```

**Important:** Agent's classification can't exceed your organization's clearance for agents. If your org is CONFIDENTIAL, agents can't be higher than that.

#### Step 4: Configure Capabilities

Specify what the agent can do:

```
Capabilities
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Access Control:
☑ READ entries (can read/process content)
☑ WRITE results (can store results/outputs)
☐ REVISE entries (can update entries)
☐ DELETE (not recommended for processing agents)

Job Types:
☑ DISTILL_CLAIMS (extract claims from entries)
☑ COMPARE_CLAIMS (compare entry claims)
☑ EMBED_ENTRIES (create embeddings)
☐ CUSTOM_JOB_TYPE (define custom)

Rate Limits:
Entries per minute: [100]
Concurrent jobs: [5]
```

#### Step 5: Provide Credentials

The system generates credentials for the agent:

```
Agent Credentials
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Agent ID:
embedding-worker-1-abc123

API Token:
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

⚠️  Save these credentials securely!
   They won't be shown again.
   Use in agent deployment:
     CYBER_AGENT_ID=embedding-worker-1-abc123
     CYBER_AGENT_TOKEN=eyJ...
```

Click **"[Copy Credentials]"** and securely save them.

#### Step 6: Configure Infrastructure

Specify where the agent runs:

```
Infrastructure Details
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Deployment Location:
[us-east-1-production]

Endpoint URL:
[https://agent-worker-1.mycompany.internal:8080/health]

Health Check Interval:
[Every 5 minutes]

Failover Strategy:
○ Stop on error (don't retry)
◉ Retry with backoff
○ Use backup agent
```

#### Step 7: Register Agent

Click **"[Register Agent]"**:

```
✓ Agent registered!

embedding-worker-1

ID: embedding-worker-1-abc123
Status: Pending (waiting for first heartbeat)
Next check: In 5 minutes

Next steps:
1. Deploy agent with credentials
2. Agent connects to Cyber
3. Status becomes "Active"
4. Jobs will be sent to agent

[View Agent Status] [Back]
```

#### Step 8: Verify Agent Connection

After deployment, monitor the agent's status:

```
Agent Status Dashboard
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Agent: embedding-worker-1-abc123
Status: ✅ Active (last seen: 2 minutes ago)
Uptime: 12 hours
Processed: 2,341 entries
Failed jobs: 0
Current load: 3/5 concurrent jobs
```

### Verification

Confirm the agent is working:

- [ ] Agent appears in agent list with "Active" status
- [ ] Agent credential sare securely stored
- [ ] Health check passing (✅ status)
- [ ] Jobs are being assigned to agent
- [ ] Failed jobs are logged and visible
- [ ] Agent respects security classification limits

### Agent Security Considerations

#### Important Rules

1. **Agents cannot exceed organizational classification**
   - If your org max is CONFIDENTIAL, agents can't be SECRET

2. **Agents inherit organizational structure**
   - Agent processes entries from groups in that organization
   - Subject to Bell-LaPadula rules

3. **Agent actions are audited**
   - Every job processed is logged
   - Changes made by agent are signed with agent identity

4. **Credentials must be kept secure**
   - Like API tokens, treat as passwords
   - Store in secure environment variables
   - Rotate yearly

#### Limiting Agent Scope

To limit what an agent can process:

- Restrict **compartments** — Agent only sees entries in allowed compartments
- Limit **job types** — Agent only does specific work (e.g., embedding, not revisions)
- Set **rate limits** — Prevent resource exhaustion
- Remove **WRITE capability** — Agent can read but not create/modify

### Tips & Tricks

#### Monitor Agent Health

Check agent status regularly:

```
[Agent Health Check]

Last heartbeat: ✅ 2 minutes ago
Response time: < 100ms
CPU usage: 45%
Memory: 2.1 GB / 4 GB
Errors (last hour): 0
```

#### Rotate Agent Credentials

Yearly rotation recommended:

```
[Rotate Credentials]

Current token expires: Jan 2027
Generate new token: [Generate]
Revoke old token: [Revoke after verification]
```

#### Multi-Agent Redundancy

Deploy multiple agents for fault tolerance:

```
embedding-worker-1 (us-east-1)  ✅ Active
embedding-worker-2 (us-west-1)  ✅ Active
claims-distiller-1 (us-east-1)  ✅ Active
claims-distiller-2 (us-west-1)  ✅ Backup
```

Jobs distribute across active agents.

### Next Steps

After registering agents:
- Deploy agent software to specified infrastructure
- Monitor agent health dashboard
- Create notebooks that agents process
- Review agent job logs in audit trail

---

## Summary: Quick Reference

### The 4 Workflows at a Glance

| Workflow | Purpose | Time | Frequency |
|----------|---------|------|-----------|
| **1. Org Structure** | Design group hierarchy | 30-60 min | Setup only |
| **2. Group Membership** | Add users to groups | 5-10 min | As needed |
| **3. Clearances** | Grant security access | 5-15 min | As needed |
| **4. ThinkerAgents** | Register workers | 20-30 min | Quarterly |

### Your Workflow Loop

```
1. Design Org Structure (once)
   ↓
2. Add Group Members (ongoing)
   ↓
3. Grant Clearances (ongoing)
   ↓
4. Register Agents (quarterly)
   ↓
5. Monitor & Update (continuous)
```

### Key Principles

- **Hierarchy First:** Structure before membership
- **Least Privilege:** Grant minimum necessary clearance
- **Audit Everything:** All changes are logged
- **Security by Default:** Classify conservatively
- **Inherit Down:** Classification & compartments flow down hierarchy

---

## Related Personas

Your workflows overlap with:

- **[System Administrator](08-system-administrator.md)** — Platform-wide user and agent management
- **[Notebook Owner](06-notebook-owner.md)** — Who manage notebooks within your org structure
- **[Knowledge Contributor](04-knowledge-contributor.md)** — Who use the groups and clearances you set up

---

## Troubleshooting

### "Can't Grant This Clearance"

**Cause:** You're trying to grant clearance higher than your own, or compartments you don't have.

**Solution:**
1. Check your own clearance level (Settings → Profile)
2. Request higher clearance from your organization's security officer
3. Or grant only what you have clearance for

### User Can't Access Notebook

**Cause:** User is in group, but clearance doesn't match notebook's classification.

**Solution:**
1. Check notebook's classification (Notebook Settings)
2. Check user's clearance (Admin → Members)
3. Elevate user's clearance or user's group clearance
4. Flush clearance cache (Admin → Organizations → Flush Cache)

### Agent Shows "Inactive"

**Cause:** Agent hasn't connected yet, or network issue.

**Solution:**
1. Verify agent credentials in deployment
2. Check agent logs for connection errors
3. Verify network allows agent → Cyber connection
4. Check agent's classification level (may be too high)

### Hierarchy Creates Cycle

**Cause:** DAG structure is broken (not actually a DAG).

**Solution:**
1. Review group relationships carefully
2. Use the hierarchy visualizer
3. Remove or adjust parent relationships to break cycle
4. Consult the Bell-LaPadula model (Chapter 2)

---

**Last updated:** February 21, 2026
**Chapter version:** 1.0.0 (Beta)
**Platform version:** 2.1.0
