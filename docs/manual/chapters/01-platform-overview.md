# Part I: Introduction

## Chapter 1: Platform Overview

### What is Cyber?

Cyber is an **enterprise-grade classified knowledge exchange platform** designed for organizations that must securely manage and share sensitive information across teams, departments, and organizational boundaries. Unlike general-purpose note-taking or wiki systems, Cyber is purpose-built for **compartmented information handling** with formal security controls.

At its core, Cyber enables:

1. **Persistent Knowledge Exchange** — Organizations create and manage *notebooks* (domain-specific knowledge spaces) that evolve through collaborative contributions from multiple users.

2. **Causal Time Without Clock Synchronization** — Instead of relying on wall-clock timestamps, the platform uses *causal positions* (monotonic sequence numbers per notebook) to establish a consistent order of events. This is essential in distributed, air-gapped, or high-latency environments where synchronized clocks cannot be guaranteed.

3. **Entropy-Based Knowledge Integration** — Every entry carries an *integration cost* (a measure of its "resistance to change") computed from how well the entry aligns with existing knowledge in the notebook. Over time, entries accumulate "stability" through integration with related content, providing a time arrow without external clocks.

4. **Bell-LaPadula Security Model** — Information is classified at five levels (PUBLIC → TOP_SECRET) and compartmented (restricted to specific clearance categories). The platform enforces strict *information flow control*: classified information never flows to less-classified recipients.

5. **Multiple Interfaces** — Access Cyber through a web-based **Blazor Server UI**, programmatic **MCP integration** (for Claude Desktop AI workflows), or **REST API** for custom integrations.

### Why Cyber Exists

Traditional knowledge management systems (wikis, note apps, content management systems) were designed for *open collaboration* in unclassified environments. They assume:

- All users have similar clearance levels
- Information has uniform sensitivity
- Timestamps are reliable global ordering mechanisms
- Changes propagate instantly to all participants

**Cyber rejects these assumptions.** It's built for environments where:

- **Security compartmentation is non-negotiable** — Healthcare (HIPAA), military (classified), finance (PCI-DSS), research (ITAR) all require strict separation of sensitive information.

- **Global clock synchronization is impractical** — Distributed teams, air-gapped networks, and high-latency links make wall-clock ordering unreliable. Causal ordering is more robust.

- **Knowledge integration matters** — The value of a fact in a knowledge base depends on how well it connects to related facts. Entropy metrics help identify "orphan" entries or contradictions that need human attention.

- **Compliance is mandatory** — Auditors need to see *who* accessed *what* *when*, with cryptographic proof. Every operation is logged and immutable.

### Core Concepts at a Glance

Before diving into workflows, familiarize yourself with these foundational concepts:

#### 1. **Notebooks**

A notebook is a **domain-specific knowledge space** owned by an organization or team. Think of it as a classified database with its own access control list, retention policies, and security boundaries.

**Examples:**
- "Marketing Strategic Initiatives" (PUBLIC classification)
- "R&D Cancer Research" (CONFIDENTIAL, Medical Research compartment)
- "Operations Security Incidents" (TOP_SECRET, Infrastructure compartment)

Each notebook has:
- **Owner group** — The team/department that created and manages it
- **Classification level** — Inherited from owner or explicitly set (future)
- **Compartments** — Optional security categories that further restrict access
- **Retention policy** — How long entries are kept
- **Access tiers** — Four levels of permission (existence/read/read+write/admin)

#### 2. **Entries**

An entry is a **unit of knowledge** in a notebook — equivalent to a wiki page, forum post, or document. Entries are:

- **Content-agnostic** — Store any MIME type (text, JSON, markdown, PDF, binary)
- **Immutable** — Once written, entries cannot be deleted or edited in-place. Instead, you *revise* them, creating a new version that supersedes the old.
- **Cryptographically signed** — Every entry includes an Ed25519 signature proving who created it and that it hasn't been tampered with.
- **Causally linked** — Entries reference related entries, building a directed graph of knowledge relationships. Unlike typical wikis, links can be *cyclic*, allowing for feedback loops in knowledge representation.

**Entry structure:**
```json
{
  "id": "entry_abc123",
  "position": 42,
  "notebook_id": "nb_xyz789",
  "content": "Base64-encoded or raw binary content",
  "content_type": "text/markdown; charset=utf-8",
  "author_id": "author_public_key_hash",
  "signature": "Ed25519 signature bytes",
  "topic": "organization/team/security/access-control",
  "references": ["entry_ref1", "entry_ref2"],
  "created_at": 1708501800,
  "integration_cost": 2.15,
  "status": "probation | integrated | contested"
}
```

#### 3. **Causal Positions**

Instead of relying on timestamps, Cyber uses **causal positions** — monotonically increasing sequence numbers per notebook.

**Why?** In distributed systems:
- Clocks drift, get out of sync, or are deliberately unreliable
- Different datacenters/organizations have different time references
- "First" and "last" become ambiguous in high-latency networks

Causal positions solve this: Position 42 always comes before Position 43 within a notebook, regardless of when they were actually created or if clocks are skewed.

**Practical implication:** When you query recent changes, you use causal positions, not timestamps.

#### 4. **Integration Cost & Entropy**

The platform computes an **integration cost** for each entry — a measure of how well it fits with existing knowledge.

**How it works:**

1. New entry is submitted
2. System compares it (via TF-IDF similarity) against all other entries in the notebook
3. Clusters are formed, measuring *coherence* (how similar related entries are)
4. Integration cost = measure of how much the new entry disrupts existing coherence
   - High cost: Entry is novel, contradicts existing knowledge, or is an outlier
   - Low cost: Entry naturally fits with existing related entries

**Why it matters:**

- **High-cost entries flag disagreements** — Multiple competing theories get high costs until one achieves dominance
- **Stable entries accumulate low cost** — Over time, well-integrated entries become "anchors" that new entries must align with
- **Retroactive cost propagation** — When a contradictory entry is integrated, previously-high-cost alternatives may increase in cost
- **Time without clocks** — Integration cost provides a "time arrow": entries that are more integrated are "older" (more established) in the community consensus

**Entry status values:**

| Status | Meaning |
|--------|---------|
| `probation` | New entry, cost still being calculated, not yet integrated |
| `integrated` | Stable entry with low cost, part of established knowledge |
| `contested` | High cost, contradicts other entries, multiple competing theories |

#### 5. **Security Labels & Classification**

Every organization and entry has a **security label** consisting of:

1. **Classification level** (Five-level hierarchy):
   - `PUBLIC` — No restrictions (accessible to anyone)
   - `CONFIDENTIAL` — Internal use only
   - `SECRET` — Restricted distribution
   - `TOP_SECRET` — Severe impact if disclosed
   - (Organization-defined custom levels)

2. **Compartments** (Optional security categories):
   - Examples: "Medical Research", "Infrastructure", "Strategic Planning"
   - A user must be explicitly cleared for each compartment they access
   - Information can flow only to users whose clearance dominates the classification + compartments

**Bell-LaPadula Dominance Rule:**

User clearance `C1` dominates `C2` if:
- `C1.level ≥ C2.level` AND
- `C2.compartments ⊆ C1.compartments`

**Example:** User cleared for `TOP_SECRET / {Medical, Infrastructure}` dominates:
- `SECRET / {Medical}` ✓
- `TOP_SECRET / {Infrastructure}` ✓
- `TOP_SECRET / {Medical, Infrastructure}` ✓
- `TOP_SECRET / {Medical, Infrastructure, Strategic}` ✗ (compartment mismatch)

#### 6. **Federated Identity**

Users are identified by **cryptographic public keys** (Ed25519), not usernames:

- **No central PKI** — Organizations manage their own key issuance
- **Portable identity** — Same key works across multiple Cyber instances
- **Cryptographic proof** — Every operation is signed, proving the user's identity without relying on the server

**AuthorId** = Hash of user's public key. This ensures different keys = different identities, even if they have the same name.

#### 7. **Access Tiers**

Notebooks support four access tiers for each principal (user or group):

| Tier | Can Exist | Can Read | Can Write | Can Admin |
|------|-----------|----------|-----------|-----------|
| **Existence** | ✓ | ✗ | ✗ | ✗ |
| **Read** | ✓ | ✓ | ✗ | ✗ |
| **Read+Write** | ✓ | ✓ | ✓ | ✗ |
| **Admin** | ✓ | ✓ | ✓ | ✓ |

- **Existence** tier: Principal knows the notebook exists but can't read it. Useful for "unlisted" shared notebooks.
- **Read+Write**: Can create and edit entries but not manage access or policies.
- **Admin**: Full control, including access control, retention policies, and deletion.

#### 8. **Audit Trail & Immutability**

Every operation is logged with:
- **Actor** — Who performed the action (AuthorId)
- **Action** — The operation type (WRITE, REVISE, SHARE, DELETE, etc.)
- **Resource** — Which notebook or entry was affected
- **Timestamp** — Wall-clock time (for auditing, not ordering)
- **Status** — Success or failure, with error details
- **Signature** — Cryptographic proof the log entry wasn't tampered with

Logs are **immutable**: Once written, they cannot be deleted or modified.

---

### Platform Architecture

Cyber consists of three main components:

#### **Backend (Rust)**
- Core engine written in Rust (safety, performance, minimal dependencies)
- Five interconnected crates:
  - **notebook-core** — Entry types, cryptography, domain logic
  - **notebook-entropy** — Integration cost computation, clustering, coherence metrics
  - **notebook-store** — PostgreSQL persistence, Apache AGE graph queries
  - **notebook-server** — HTTP API (six operations + management endpoints)
  - **cli** — Command-line interface
- All stored data is cryptographically signed and immutable

#### **Frontend (Blazor Server)**
- Web UI for notebook/entry management, access control, auditing
- Server-rendered (tight security boundary, easier audit)
- Responsive design for desktop and tablet
- Keyboard shortcuts for power users

#### **MCP Integration (Python)**
- Model Context Protocol server for Claude Desktop
- Exposes all six operations as Claude tools
- JWT authentication (token-based)
- Ideal for AI-assisted knowledge creation and analysis

---

### Who Should Use This Manual?

This manual is structured for **seven distinct user personas**, each with different goals and responsibilities:

1. **Knowledge Contributor** — Regular user creating and browsing notebook entries via MCP or UI
2. **Organization Administrator** — Setting up organizational structure, security clearances, and group membership
3. **Notebook Owner** — Creating notebooks, managing access, reviewing submissions, monitoring job processing
4. **Auditor/Compliance Officer** — Investigating security events, generating audit reports, ensuring compliance
5. **System Administrator** — Managing users, quotas, platform health, and global agent configuration
6. **ThinkerAgent Operator** — Deploying and managing AI processing workers that perform background jobs
7. **Cross-Organization Coordinator** — Managing knowledge sharing across organizational boundaries

**Use this guide based on your role:**
- **Just getting started?** → Go to [Chapter 3: Getting Started](#)
- **Know your role?** → Jump to the relevant chapter in Part II ([Chapters 4-10](#))
- **Need API details?** → Go to [Part III: Reference](#) ([Chapters 11-16](#))
- **Lost?** → Check [Chapter 16: Glossary & Index](#) or use your PDF reader's search feature

---

### Key Design Principles

As you work with Cyber, you'll notice these principles reflected throughout:

1. **Security by Default** — Assume information is sensitive until proven otherwise. Information flows only to authenticated, authorized users.

2. **Causal Consistency Over Instant Consistency** — Accept that replicas may lag. Use causal positions, not wall-clock times, for ordering.

3. **Immutability as Feature** — Entries cannot be deleted; only new revisions. This preserves history and enables audit trails.

4. **Entropy Reflects Reality** — The platform doesn't mandate consensus; it measures and surfaces disagreement through integration costs.

5. **Federated, Not Centralized** — Users and organizations maintain cryptographic identity. No single point of failure or control.

---

### What's Not Covered Here

This manual focuses on **user workflows and operational tasks**. For implementation details, architecture deep-dives, or extending the platform, see:
- **Developer Guide** — [backend/README.md](#)
- **Architecture Documentation** — [docs/architecture/](#)
- **Source Code** — [github.com/cyber-project](#) (Rust backend, Python client)
- **Project Roadmap** — [docs/project-plan.md](#)

---

### Moving Forward

You now understand the *why* and *what* of Cyber. The next chapter ([Chapter 2: Security Model](#)) goes deeper into classification levels, compartments, and access control rules. Then, [Chapter 3: Getting Started](#) walks you through your first login and interface orientation.

**Ready to dive in?** Turn to [Chapter 2](#) →

---

**Last updated:** February 21, 2026
**Manual version:** 1.0.0 (Beta)
**Platform version:** 2.1.0
