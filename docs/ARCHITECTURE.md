# System Architecture Overview

High-level overview of the Cyber knowledge exchange platform architecture.

## Core Concept

**Cyber** builds externalized memory substrates that enable persistent, evolving identity through shared entries with entropy-based knowledge integration metrics.

**Key Insight:** Integration cost (resistance to change) IS entropy, providing a time arrow without clock synchronization.

## System Layers

```
┌──────────────────────────────────────────────────┐
│  Admin UI (.NET Blazor Server)                   │
│  - Notebook management, filtering, search        │
│  - Organization & group hierarchy                │
│  - Access control, audit trails                  │
│  - Agent & security management                   │
├──────────────────────────────────────────────────┤
│  Backend API (.NET 10)                           │
│  - RESTful API (entries, notebooks, sharing)     │
│  - Full-text search via Tantivy                  │
│  - Batch operations, filtered browse             │
│  - Job queue for workers                         │
│  - Security, authorization, audit               │
├──────────────────────────────────────────────────┤
│  PostgreSQL + Apache AGE Graph Database          │
│  - Entry storage with metadata                   │
│  - Graph for cross-references (cyclic OK)        │
│  - Job queue, audit log, organizations          │
│  - Claims, comparisons, integration costs        │
└──────────────────────────────────────────────────┘
```

## Key Features

### 📔 Notebooks
Persistent knowledge collections with:
- Shared access via 4-tier permission model (existence, read, read+write, admin)
- Entry ownership & timestamps
- Integration cost tracking
- Full-text search indexing
- Topic-based hierarchical browsing

### 🔍 Filtered Browse
Rich server-side filtering for entries:
- **Topic prefix** — hierarchical navigation (e.g., `confluence/ENG/`)
- **Claims status** — pending, distilled, verified
- **Integration status** — probation, integrated, contested
- **Friction threshold** — filter by resistance to change
- **Author filter** — find entries by creator
- **Sequence range** — browse by insertion order
- **Needs review** — flagged items requiring action
- **Pagination** — load 50+ entries at a time

### 🔎 Full-Text Search
Tantivy-powered semantic indexing:
- Search across all entry content
- Relevance scoring
- Snippet extraction with context

### 📊 Entropy & Integration Metrics
Every entry carries:
- **Integration cost** — resistance to change (entropy proxy)
- **Friction score** — how contradictory an entry is
- **Claims** — extracted key statements
- **Causal position** — monotonic sequence counter (no clock needed)
- **Cryptographic authorship** — Ed25519 signatures

### 🔐 Security & Access Control
- **Classification levels** — PUBLIC, INTERNAL, CONFIDENTIAL, SECRET, TOP_SECRET
- **Compartments** — fine-grained data compartmentalization
- **Clearances** — grant access to specific classification levels
- **Audit trail** — complete action history with actor/action/target filtering
- **Organizations & Groups** — hierarchical team management with DAG structure

### 👥 Organizations & Groups
Hierarchical team management:
- Create organizations
- Build group hierarchies (DAG structure with cycles allowed)
- Manage group members with roles (member/admin)
- Assign notebooks to owning groups
- Propagate access tiers through hierarchy

### 📋 Content Review (Ingestion Gate)
Approve or reject external contributions:
- Pending review queue
- Approval/rejection workflow
- Track submitter and review status
- Critical for managing group-owned notebooks

### 🤖 Agent Management
Register and manage ThinkerAgents:
- Security label assignments (max_level, compartments)
- Infrastructure tracking
- Last seen timestamps
- Full CRUD operations

### 📝 Subscriptions (Cross-Notebook Mirroring)
Mirror entries between notebooks:
- Subscribe to another notebook
- Choose scope: catalog, claims, or entries
- Automatic sync with configurable intervals
- Sync status tracking and error reporting
- Trigger immediate sync on demand

### 📊 Job Queue & Workers
Asynchronous processing pipeline:
- Job types: DISTILL_CLAIMS, COMPARE_CLAIMS, CLASSIFY_TOPIC, EMBED_CLAIMS
- Statistics dashboard (pending/in-progress/completed/failed)
- Retry mechanism for failed jobs
- Worker pool integration

## Data Model

### Core Types

**Entry**
```
- id: UUID
- notebook_id: UUID
- content: JSONB (representation-agnostic)
- content_type: String (MIME type)
- topic: String (hierarchical path)
- author_id: Ed25519 hash
- sequence: Monotonic counter
- created: Timestamp
- [other metadata]
```

**Claim**
```
- id: UUID
- entry_id: UUID
- text: String
- status: pending | distilled | verified
- created: Timestamp
```

**ClaimComparison**
```
- claim1_id: UUID
- claim2_id: UUID
- entropy_contribution: Float (0.0-1.0)
- friction_score: Float (0.0-1.0)
```

**Job**
```
- id: UUID
- type: DISTILL_CLAIMS | COMPARE_CLAIMS | CLASSIFY_TOPIC | EMBED_CLAIMS
- target_id: UUID (entry/claim)
- status: pending | in_progress | completed | failed
- result: JSONB
```

## API Operations

**Six Core Operations:**
1. **WRITE** — Create entry with claims and references
2. **REVISE** — Update existing entry
3. **READ** — Retrieve full entry with metadata
4. **BROWSE** — List entries with filtering and pagination
5. **OBSERVE** — Get change feed since sequence N
6. **SHARE** — Grant/revoke access to notebook

**Additional Operations:**
- **Batch Write** — Create multiple entries atomically
- **Filtered Browse** — Rich filtering on entries (implemented UI)
- **Search** — Full-text search with relevance
- **Job Queue** — Submit and track async jobs
- **Claims** — Store and manage claim statements
- **Audit** — Query action history with filters

## Feature Coverage

**Current Status:** 13/16 feature domains fully implemented (81%)

| Status | Count | Examples |
|--------|:-----:|----------|
| ✅ Fully Implemented | 13 | Organizations, Security, Audit Trail, Browse Filters, Search, Sharing |
| ⚠️ Partially Covered | 3 | Batch Entry UI, Semantic Search UI, Notebook Classification |
| ❌ Not Supported | 0 | — |

See [10-USER-FACING-FEATURES.md](architecture/10-USER-FACING-FEATURES.md) for complete feature matrix.

## Deployment Architecture

```
┌─────────────────────────┐
│   Client/Browser        │
├─────────────────────────┤
│   Frontend (Blazor)     │
│   Port 5000             │
├─────────────────────────┤
│   Backend API (.NET)    │
│   Port 5201             │
├─────────────────────────┤
│   PostgreSQL            │
│   Port 5432             │
│   + Apache AGE          │
└─────────────────────────┘
```

**Docker Compose** orchestrates the stack with automatic initialization.

## Technology Stack

- **Frontend:** .NET 10, Blazor Server, Bootstrap CSS
- **Backend:** .NET 10, ASP.NET Core, Entity Framework Core
- **Database:** PostgreSQL with Apache AGE for graph queries
- **Search:** Tantivy full-text indexing
- **Testing:** xUnit, FluentAssertions
- **Authentication:** JWT Bearer tokens, Ed25519 signatures
- **Serialization:** System.Text.Json

## Design Principles

1. **Representation-agnostic content** — No interpretation; entries carry MIME-type blobs
2. **Causal positions over timestamps** — Monotonic sequence counters, no clock synchronization
3. **Integration cost as entropy** — System measures resistance to change as time arrow
4. **Federated identity** — Ed25519 crypto, AuthorId from public key hash
5. **Backward compatibility** — All v1 operations remain unchanged
6. **Security by default** — Classification levels, compartments, clearance-based access

## Next Steps

1. **[Setup Development Environment](SETUP.md)** — Get running locally
2. **[Review Detailed Architecture](architecture/)** — Deep-dive into specific systems
3. **[Check Roadmap](roadmap/)** — See what's in progress and planned
4. **[Review Feature Status](architecture/10-USER-FACING-FEATURES.md)** — Current implementation coverage

---

For complete architectural documentation, see the `architecture/` directory.
For development setup, see [SETUP.md](SETUP.md).
