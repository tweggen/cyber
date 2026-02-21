# Chapter 3: Getting Started

### Creating Your Account

Cyber uses **federated identity** based on cryptographic keys rather than traditional usernames and passwords. This section walks you through first-time setup.

#### Step 1: Get Access

Your organization administrator will provide you with:
- **Instance URL** — Where to access Cyber (e.g., `https://cyber.company.com`)
- **Authentication method** — OIDC integration, SAML, or custom (depends on your org)
- **Initial clearance level** — Your starting security clearance (e.g., `CONFIDENTIAL / {}`)

If you don't have these, contact your organization's Cyber administrator.

#### Step 2: First Login

1. Navigate to your Cyber instance URL in a web browser
2. Click **"Sign In"** or **"Create Account"**
3. Follow your organization's authentication flow:
   - **OIDC:** Use your company SSO (Google, Okta, Azure AD)
   - **SAML:** Authenticate through your enterprise identity provider
   - **Email/Password:** Verify your email address
4. On first login, you'll be prompted to generate a cryptographic key pair

#### Step 3: Cryptographic Key Generation

Cyber uses **Ed25519 keys** for signing all operations. On first login:

1. You'll see a dialog: **"Generate Your Signing Key"**
2. Click **"Generate New Key"** — The browser will create a public/private key pair locally
3. Your **private key will be saved in browser storage** (encrypted with your password)
4. Your **public key is registered with the server** and used to verify your identity

**Important security notes:**
- Your private key never leaves your browser (unless you export it)
- Lose your key? You'll need to generate a new one (old entries remain but you can't sign new ones)
- Backup your key via the **Profile → Security** page if you want to restore it on another device

#### Step 4: Set Your Profile

After key generation, you'll be prompted to complete your profile:

- **Full Name** — Display name for audit logs and collaboration
- **Email** — Contact info for notifications and password resets
- **Avatar** — Optional profile picture
- **Organization** — Which organization you belong to
- **Department/Team** — For group membership and organization charts

Once completed, you'll see the **Dashboard**.

---

### The Dashboard

The Dashboard is your home page after logging in. It provides an at-a-glance view of your activity and the platform's health.

#### Dashboard Sections

```
┌─────────────────────────────────────────────────────────────┐
│  Dashboard                                      [User Menu]  │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  📊 System Status                                             │
│  • Notebooks: 42 total, 8 new this week                      │
│  • Entries: 2,341 total, 156 added today                     │
│  • Pending Jobs: 3 DISTILL_CLAIMS, 2 COMPARE_CLAIMS         │
│  • Health: ✓ All systems nominal                             │
│                                                               │
│  📋 Your Recent Activity                                      │
│  • Jan 21 - You revised "API Architecture" entry             │
│  • Jan 20 - You created 3 new entries in Q1 Planning         │
│  • Jan 19 - Project Oversight group added you as member     │
│                                                               │
│  📂 Your Notebooks (Quick Access)                             │
│  ┌────────────────────┬──────────┬─────────┐               │
│  │ Name               │ Entries  │ Access  │               │
│  ├────────────────────┼──────────┼─────────┤               │
│  │ Q1 Planning        │ 45       │ Admin   │               │
│  │ R&D Notes          │ 128      │ Read+W  │               │
│  │ Strategic Roadmap  │ 12       │ Read    │               │
│  └────────────────────┴──────────┴─────────┘               │
│                                                               │
│  🔴 Security Events (Last 7 days)                             │
│  • 2 Access Denials — Jan 20, IP 192.168.1.50               │
│  • 0 Failed Auth Attempts                                    │
│  • 1 Clearance Change — Jan 19, added SECRET level          │
│                                                               │
│  ⚡ Recommended Actions                                       │
│  • Set up MCP access for Claude Desktop                      │
│  • Review pending group invitations (1 pending)             │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

**Key metrics:**

| Widget | Shows You |
|--------|-----------|
| **System Status** | Platform health, total notebooks/entries, pending background jobs |
| **Recent Activity** | Actions you took (created/revised entries, group changes, etc.) |
| **Your Notebooks** | Quick access to notebooks you own or have access to |
| **Security Events** | Access denials, login failures, clearance changes |
| **Recommended Actions** | Setup tasks, invitations, pending reviews |

#### Navigation Sidebar

On the left side of every page:

```
Cyber (logo)
━━━━━━━━━━━━━━━━━
📊 Dashboard
📂 Notebooks
📝 Entries
📚 Explore
🔍 Search
[Divider]
⚙️ Settings
👤 Profile
🔐 Security
📋 Audit Log
[Divider]
🚀 Admin Panel (if you're an admin)
```

---

### Understanding Your Permissions

On your **Profile** page (`/profile`), you'll see three key pieces of information:

#### 1. Your Clearance

```
🔐 Your Clearance Level

Current: CONFIDENTIAL / {Strategic Planning, Operations}

What this means:
  ✓ You can read any PUBLIC or CONFIDENTIAL notebook
  ✓ You can read CONFIDENTIAL entries in Strategic Planning and Operations
  ✗ You cannot access SECRET, TOP_SECRET, or other compartments

Request a clearance upgrade: [Contact Admin]
```

Your clearance determines what information you can *read*. If you need access to a more-restricted notebook, contact your organization administrator.

#### 2. Your Group Memberships

```
👥 Groups

You are a member of:
  • Engineering Team (Member role)
  • Project Alpha (Member role)
  • Executive Council (Admin role)  ← You can add members to this group
```

Groups affect:
- Which notebooks you automatically have access to
- Your administrative responsibilities
- Your audit permissions (group admins see group-related events)

#### 3. Your Authentication Keys

```
🔑 Signing Keys

Active: Ed25519 public key 0x8a2f... (created Jan 18, 2026)
Backup keys: None

Export private key (for backup/restore): [Download]
```

Manage your cryptographic keys here. You need at least one active key to sign new entries.

---

### Creating Your First Notebook

Now that you're set up, let's create your first notebook:

#### Step 1: Go to Notebooks Page

1. Click **Notebooks** in the left sidebar
2. Click **"+ New Notebook"** button
3. You'll see a form:

```
Create New Notebook
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Name *
[Text field: "Q1 Project Planning"]

Description
[Large text field: "Central hub for Q1 priorities, milestones, and team coordination"]

Owner Group *
[Dropdown: "Select a group..."]
    → Engineering Team
    → Project Oversight
    → Strategic Planning

Classification Level (Advanced)
[Dropdown: "CONFIDENTIAL"]  ← Inherited from owner group

Compartments (Optional)
[Tag field: + Add compartments...]
    Examples: Strategic Planning, Medical Research, etc.

[Create Notebook]  [Cancel]
```

#### Step 2: Fill in Details

- **Name:** Concise, clear (e.g., "Q1 Planning", "Patient Records", "R&D Roadmap")
- **Description:** 1-2 sentences explaining the notebook's purpose
- **Owner Group:** The group responsible for this notebook
  - Only group admins can manage the notebook
  - All group members get at least "Read" access
- **Classification:** Usually inherited from group, but can be more restrictive
- **Compartments:** Optional security categories (e.g., if it contains sensitive personal data)

#### Step 3: Create & Configure Access

Click **"Create Notebook"**. You'll be redirected to the notebook's **Settings** page:

```
Notebook: Q1 Project Planning
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📋 Entry Feed  ⚙️ Settings  🔐 Access Control  📊 Statistics

[Settings Tab Active]

Classification: CONFIDENTIAL / {}
Entry Retention: 7 years (default)
Status: Active

Access Control
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Current Members:

Name           | Role    | Tier       | Actions
──────────────┼─────────┼────────────┼──────────
Engineering   | Group   | Read+Write | Remove
(4 members)   |         |            |

You (Jane)    | Owner   | Admin      | (You)

[+ Add User or Group]
```

By default:
- Your owner group has **Read+Write** access
- You have **Admin** access
- Others can be added individually

#### Step 4: Add Collaborators (Optional)

To give other users access:

1. Click **"+ Add User or Group"**
2. Search for a user or group by name
3. Select the **access tier** (Existence / Read / Read+Write / Admin)
4. Click **"Add"**

The user will see the notebook in their **Notebooks** page and can start reading/contributing.

---

### Reading Your First Entry

Once a notebook exists, you can start reading entries. Here's the **Entry Feed** view:

```
Q1 Project Planning
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Entry Feed | Settings | Access Control | Statistics

Filter & Search:
[Topic dropdown: All] [Status: All] [Friction: All] [Search box: ___________]

📌 Pinned Entries (0)

Recent Entries:
────────────────────────────────────────────────────────────

[Entry Card]
📝 Title: "Q1 Goals and Priorities"
Author: Jane Smith (Jan 22, 2026)
Integration Status: ✓ Integrated (low friction)
Topic: organization/planning/goals
References: 3 entries

Quick view: [Read] [History] [Compare]

[Entry Card]
📝 Title: "Team Resource Allocation"
Author: Bob Johnson (Jan 21, 2026)
Integration Status: ⚠️ Probation (calculating friction)
Topic: organization/planning/resources
References: 2 entries

Quick view: [Read] [History] [Compare]
```

**To read an entry:**

1. Click on an entry card
2. The full entry opens in a side panel:

```
═════════════════════════════════════════════════════════════
  Q1 Goals and Priorities                         [Close ×]
═════════════════════════════════════════════════════════════

Content:
────────────────────────────────────────────────────────────

# Q1 Goals and Priorities

For Q1 2026, we're focusing on three strategic pillars:

1. **Customer Experience** — Reduce support ticket response time
   by 50% and increase satisfaction scores above 4.5/5.0

2. **Infrastructure Reliability** — Zero critical incidents,
   99.99% uptime SLA

3. **Team Development** — Complete certifications for 100% of
   engineering team

---

Entry Metadata:
────────────────────────────────────────────────────────────

Author:         Jane Smith
Created:        Jan 22, 2026, 10:30 AM
Position:       42 (causal ordering)
Integration:    Integrated
Friction Score: 0.21 (low — well aligned with existing entries)
Topic:          organization/planning/goals
References:     [Q1 Budget] [Engineering Roadmap] [Team Charter]

[Revise] [Compare with Other Versions] [View History]
```

**Key elements:**

| Element | What It Means |
|---------|---------------|
| **Position** | Causal order (42 = 42nd entry in this notebook) |
| **Integration** | Status: probation (new), integrated (stable), or contested (contradictory) |
| **Friction Score** | 0-10: How much this entry disrupts existing knowledge (0 = perfectly aligned, 10 = major disagreement) |
| **Topic** | Hierarchical classification (e.g., org/planning/goals) |
| **References** | Related entries this one links to |

---

### Searching and Browsing

#### Full-Text Search

Use the search box at the top of any page:

1. Type your query (e.g., "budget allocation")
2. Press Enter or click **Search**
3. Results appear sorted by relevance

**Search syntax:**

```
Query Type              | Example
───────────────────────┼──────────────────────────────
Simple keyword          | budget
Exact phrase            | "Q1 budget"
Author                  | author:Jane
Classification level    | level:SECRET
Topic filter            | topic:planning
Friction threshold      | friction:>5
Combination             | "budget" author:Jane friction:<3
```

#### Browsing by Topic

1. Go to **Explore** in the sidebar
2. You'll see a hierarchical topic tree:

```
📚 Explore Notebooks
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

You have access to X notebooks across these topics:

📁 organization/
   📁 planning/
      📄 goals
      📄 budget
      📄 roadmap
   📁 operations/
      📄 incidents
      📄 runbooks

📁 projects/
   📁 alpha/
      📄 architecture
      📄 schedule
```

Click any topic to see all entries under that category.

---

### Interface Overview

#### Key Pages

| Page | URL | Purpose |
|------|-----|---------|
| Dashboard | `/` | Home page, system status, recommendations |
| Notebooks | `/notebooks` | List of all notebooks you have access to |
| Entries | `/entries` | Global entry search and filtering |
| Explore | `/explore` | Browse by topic hierarchy |
| Search | `/search` | Advanced full-text search |
| Profile | `/profile` | Your account, clearance, groups, keys |
| Settings | `/settings` | Personal preferences, notifications, API tokens |
| Admin Panel | `/admin` | User management, audit logs, system config (admins only) |

#### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `?` | Show this help menu |
| `/` | Focus search box |
| `n` | New entry/notebook |
| `e` | Enter/exit edit mode |
| `s` | Save |
| `Esc` | Close modals, exit edit |
| `g` `d` | Go to Dashboard |
| `g` `n` | Go to Notebooks |
| `g` `e` | Go to Entries |

(Disabled in text input fields to avoid conflicts)

---

### Generating API Tokens

If you plan to use the **MCP integration** or **REST API** programmatically:

#### Step 1: Go to Settings

1. Click your avatar in the top-right corner
2. Select **Settings** → **API Tokens**

#### Step 2: Create a New Token

```
API Tokens
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Active Tokens:
(none yet)

[+ Generate New Token]
```

Click **"+ Generate New Token"**:

```
Create API Token
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Token Name: [Claude Desktop MCP]

Expiration: ○ Never  ○ 1 Month  ○ 90 Days  ⦿ 1 Year

Scopes (what this token can do):
☑ Read notebooks and entries
☑ Write and revise entries
☑ Manage access control
☑ View audit logs
☐ Delete entries
☐ Administer users and groups

[Generate Token]
```

#### Step 3: Copy and Store Securely

Once generated, you'll see:

```
✓ Token created!

CYBER_TOKEN=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

⚠️ Save this token somewhere safe. You won't see it again.
   If you lose it, generate a new one.

[Copy to Clipboard] [Done]
```

**Security notes:**
- Treat this token like a password
- Use environment variables to store it (not in code)
- Rotate tokens yearly
- Delete tokens you no longer use

---

### Accessibility Features

Cyber is designed for accessibility:

| Feature | Use Case |
|---------|----------|
| **High contrast mode** | Settings → Appearance → High Contrast |
| **Large fonts** | Settings → Appearance → Font Size |
| **Dark mode** | Settings → Appearance → Dark Mode |
| **Screen reader support** | All UI elements have ARIA labels |
| **Keyboard navigation** | Use `Tab` to navigate, `Enter` to activate |
| **Text-to-speech** | [Select text and right-click "Read Aloud"] |

---

### Next Steps

Congratulations! You've completed the basic setup. Now it's time to get to work:

- **Creating entries?** → Go to [Part II, Your Role](#)
- **Setting up MCP?** → [Workflow: MCP Setup for Knowledge Contributors](#)
- **Exploring security?** → [Chapter 2: Security Model](#)
- **Need help?** → [Chapter 15: Troubleshooting](#)

---

**Last updated:** February 21, 2026
**Manual version:** 1.0.0 (Beta)
**Platform version:** 2.1.0
