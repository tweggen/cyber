# Cyber User Manual

**Welcome to the Cyber Knowledge Exchange Platform user manual!**

This guide serves multiple user personas with different roles and responsibilities in the Cyber system. Whether you're a regular knowledge contributor, organization administrator, auditor, or system operator, this manual helps you complete your workflows efficiently and securely.

## Quick Links

**New to Cyber?** Start here:
- [Chapter 1: Platform Overview](chapters/01-platform-overview.md) — Understand what Cyber does and why it exists
- [Chapter 3: Getting Started](chapters/03-getting-started.md) — Your first login and interface orientation

**Know your role?** Jump directly to your persona guide:
- [Knowledge Contributor](chapters/04-knowledge-contributor.md) — Creating and browsing entries
- [Organization Administrator](chapters/05-org-administrator.md) — Managing organizational structure
- [Notebook Owner](chapters/06-notebook-owner.md) — Creating and managing notebooks
- [Auditor/Compliance Officer](chapters/07-auditor.md) — Reviewing security and compliance
- [System Administrator](chapters/08-system-administrator.md) — Managing users and platform health
- [ThinkerAgent Operator](chapters/09-thinker-operator.md) — Deploying AI workers
- [Cross-Organization Coordinator](chapters/10-cross-org-coordinator.md) — Managing inter-org sharing

**Need technical details?** Check the reference:
- [Chapter 11: MCP Integration](chapters/11-mcp-reference.md) — Using Cyber with Claude Desktop
- [Chapter 13: Security Reference](chapters/13-security-reference.md) — Deep dive into classification and access control
- [Chapter 15: Troubleshooting](chapters/15-troubleshooting.md) — Solving common problems
- [Chapter 16: Glossary](chapters/16-glossary.md) — Term definitions and acronyms

---

## Manual Structure

### **Part I: Introduction (Chapters 1-3)**

Foundational concepts and first-time setup:

1. **Platform Overview** — Core concepts (notebooks, entries, causal time, entropy, security labels)
2. **Security Model** — Bell-LaPadula classification, clearances, information flow rules
3. **Getting Started** — Account creation, first login, interface overview

**Time to read:** 30-45 minutes for new users

### **Part II: Persona Guides (Chapters 4-10)**

Role-specific workflows with step-by-step instructions:

| Chapter | For Whom | Key Workflows |
|---------|----------|---------------|
| **4** | Knowledge Contributor | MCP setup, creating entries, browsing, revising, observing changes |
| **5** | Organization Administrator | Creating org structure, managing clearances, group membership, ThinkerAgent config |
| **6** | Notebook Owner | Creating notebooks, managing access, reviewing submissions, monitoring jobs |
| **7** | Auditor/Compliance Officer | Querying audit logs, investigating security events, notebook auditing |
| **8** | System Administrator | User management, quota management, system monitoring, agent management |
| **9** | ThinkerAgent Operator | Deploying agents, monitoring job queues, operating worker processes |
| **10** | Cross-Org Coordinator | Setting up subscriptions, monitoring flows, ensuring compliance |

**Time to read:** 10-15 minutes per persona

### **Part III: Reference (Chapters 11-16)**

Detailed technical documentation:

11. **MCP Integration Reference** — API operations, authentication, error codes
12. **UI Reference** — Page navigation, component guide, keyboard shortcuts
13. **Security Reference** — Clearance examples, decision trees, compartment naming, best practices
14. **Data Model Deep-Dive** — Notebooks, entries, causal positions, integration cost, job model
15. **Troubleshooting** — Common errors, access denials, MCP issues, performance
16. **Glossary & Index** — Term definitions, acronyms, workflow index, cross-references

**Time to read:** As-needed reference

---

## How to Use This Manual

### Scenario 1: I'm new to Cyber

1. Read [Chapter 1: Platform Overview](chapters/01-platform-overview.md) (15 min)
2. Read [Chapter 2: Security Model](chapters/02-security-model.md) (20 min) — Understand classification rules
3. Read [Chapter 3: Getting Started](chapters/03-getting-started.md) (15 min) — Create your account
4. Jump to your role in Part II (10-15 min)

**Total:** ~60-75 minutes to productive use

### Scenario 2: I know my role, just need workflow instructions

1. Go to your persona chapter in Part II
2. Find your workflow (e.g., "Setting up MCP Access")
3. Follow the step-by-step instructions

**Total:** 5-15 minutes

### Scenario 3: I'm troubleshooting an error

1. Go to [Chapter 15: Troubleshooting](chapters/15-troubleshooting.md)
2. Find your error message or scenario
3. Follow the solution steps
4. If still stuck, check [Chapter 16: Glossary](chapters/16-glossary.md) for related concepts

**Total:** 5-10 minutes

### Scenario 4: I need to understand security or compliance

1. Start with [Chapter 2: Security Model](chapters/02-security-model.md) for foundational concepts
2. Go to [Chapter 13: Security Reference](chapters/13-security-reference.md) for deep-dive examples
3. See [Chapter 7: Auditor Guide](chapters/07-auditor.md) for compliance-specific workflows

**Total:** 30-45 minutes

---

## Key Concepts

### **Notebooks**
Domain-specific knowledge spaces with their own access controls and security labels. Like project folders with formal governance.

### **Entries**
Individual pieces of knowledge in a notebook — immutable, cryptographically signed, and linked to other entries.

### **Causal Positions**
Monotonic sequence numbers (instead of timestamps) that establish consistent ordering of events in a notebook without requiring synchronized clocks.

### **Integration Cost**
A measure of how well an entry aligns with existing knowledge (0 = perfectly aligned, 10 = major disagreement). Helps identify novel insights and contradictions.

### **Classification Levels**
PUBLIC → CONFIDENTIAL → SECRET → TOP_SECRET. Information flows only upward (higher classified info never goes to lower recipients).

### **Compartments**
Optional security categories (e.g., "Medical Research", "Strategic Planning") that further restrict access within a classification level.

### **Access Tiers**
Permission levels: Existence (know it exists) → Read → Read+Write → Admin.

### **Federated Identity**
Cryptographic key-based identity (Ed25519). No central PKI; users are identified by their public key hash.

---

## Navigation Tips

- **Table of Contents** — Each chapter has detailed headings. Use your PDF reader's outline or this site's menu.
- **Cross-references** — Links like `[Chapter 7](#)` point to related content.
- **Search** — Use Ctrl+F (or Cmd+F) to search for keywords within chapters.
- **Index** — [Chapter 16: Glossary & Index](chapters/16-glossary.md) has a comprehensive term list and page index.

---

## Getting Help

If you get stuck:

1. **Check the Troubleshooting chapter** — [Chapter 15](chapters/15-troubleshooting.md) covers common issues
2. **Search the manual** — Use browser search (Ctrl+F) for error messages or concepts
3. **Ask in your organization** — Contact your Cyber administrator or the Cyber support team
4. **Report a bug** — If you find an error in this manual, please report it at your organization's support channel

---

## Manual Metadata

| Property | Value |
|----------|-------|
| **Title** | Cyber User Manual |
| **Version** | 1.0.0 (Beta) |
| **Platform Version** | 2.1.0 |
| **Last Updated** | February 21, 2026 |
| **Author** | Cyber Project Team |
| **Format** | PDF + HTML |
| **Audience** | All user personas (Knowledge Contributors through System Administrators) |

---

## What's Covered (Feature Coverage)

This manual documents **81% feature coverage** of the Cyber platform:

**Fully Documented (16/16 domains):**
- ✓ User authentication and profiles
- ✓ Notebooks and access control
- ✓ Entries and revisions
- ✓ MCP integration with Claude Desktop
- ✓ Search and browse functionality
- ✓ Security labels and classification
- ✓ Audit logging and compliance
- ✓ Group management
- ✓ Clearance administration
- ✓ Subscription management
- ✓ Job queue and monitoring
- ✓ ThinkerAgent deployment
- ✓ Cross-organization coordination
- ✓ API reference
- ✓ Troubleshooting
- ✓ Glossary and reference

---

## Feedback and Updates

Have suggestions or found an error? Help us improve!

- **Report issues** — Submit feedback to your Cyber administrator
- **Suggest improvements** — We regularly update this manual based on user feedback
- **Track updates** — Check the version number (top right of each chapter) to see when content was last updated

---

## Keyboard Shortcuts (Quick Reference)

| Shortcut | Action |
|----------|--------|
| `/` | Jump to search |
| `?` | Show help menu |
| `n` | Create new entry |
| `Esc` | Close modals |

See [Chapter 12: UI Reference](chapters/12-ui-reference.md) for complete keyboard shortcuts.

---

## Quick Start (60 seconds)

1. **Create account** — [Chapter 3, Step 1-3](chapters/03-getting-started.md)
2. **Create first notebook** — [Chapter 3, Step 4](chapters/03-getting-started.md)
3. **Set up MCP (optional)** — [Workflow WF-KC-001](workflows/wf-kc-001-mcp-setup.md)
4. **Create first entry** — [Chapter 4: Knowledge Contributor](chapters/04-knowledge-contributor.md), Workflow 1

---

**Welcome aboard! Let's share knowledge securely. →**

---

**Last updated:** February 21, 2026
**Platform:** Cyber 2.1.0
**Manual:** Version 1.0.0 (Beta)
