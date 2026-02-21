# Chapter 17: Architecture & Workflow Diagrams

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     CYBER PLATFORM                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────────────┐  ┌──────────────────────┐             │
│  │   Web UI             │  │   MCP Integration    │             │
│  │  (Blazor Server)     │  │  (Claude Desktop)    │             │
│  │  • Notebooks         │  │  • WRITE/REVISE      │             │
│  │  • Entries           │  │  • READ/BROWSE       │             │
│  │  • Settings          │  │  • SEARCH/OBSERVE    │             │
│  └──────────┬───────────┘  └──────────┬───────────┘             │
│             │                         │                         │
│             └────────────┬────────────┘                         │
│                          │                                      │
│                  ┌───────▼────────┐                            │
│                  │   HTTP API     │                            │
│                  │  (Axum Server) │                            │
│                  │  6 Operations  │                            │
│                  └───────┬────────┘                            │
│                          │                                      │
│        ┌─────────────────┼─────────────────┐                  │
│        │                 │                 │                  │
│   ┌────▼────┐     ┌─────▼─────┐    ┌─────▼──────┐           │
│   │PostgreSQL│     │ In-Memory │    │ Tantivy    │           │
│   │  (Data) │     │  Cache    │    │  (Search)  │           │
│   └────┬────┘     └───────────┘    └────────────┘           │
│        │                                                       │
│   ┌────▼────────────────┐                                    │
│   │ Apache AGE          │                                    │
│   │ (Graph Traversal)   │                                    │
│   └─────────────────────┘                                    │
│                                                               │
│  ┌────────────────────────────────────┐                     │
│  │  Entropy Engine                    │                     │
│  │ • TF-IDF Similarity                │                     │
│  │ • Agglomerative Clustering         │                     │
│  │ • Integration Cost Computation     │                     │
│  └────────────────────────────────────┘                     │
│                                                               │
│  ┌────────────────────────────────────┐                     │
│  │  ThinkerAgent Processor            │                     │
│  │ • Job Queue (DISTILL, COMPARE...)  │                     │
│  │ • Ollama Integration               │                     │
│  │ • Background Jobs                  │                     │
│  └────────────────────────────────────┘                     │
│                                                               │
└─────────────────────────────────────────────────────────────────┘
```

## Persona Ecosystem

```
                    Knowledge Exchange Platform
                              │
                ┌─────────────┼─────────────┐
                │             │             │
         ┌──────▼────┐  ┌────▼──────┐  ┌──▼────────┐
         │   Users   │  │   Org     │  │  Auditors │
         │(Create)   │  │  Admin    │  │(Monitor)  │
         └──────┬────┘  └────┬──────┘  └──┬────────┘
                │             │             │
         ┌──────▼────┐  ┌────▼──────┐  ┌──▼────────┐
         │Knowledge  │  │Organization│ │Auditor/   │
         │Contributor│  │Administrator│Compliance │
         └──────┬────┘  └────┬──────┘  └──┬────────┘
                │             │             │
    ┌───────────┼─────────────┼─────────────┼──────────┐
    │           │             │             │          │
    ▼           ▼             ▼             ▼          ▼
  Create    Manage         Review        Audit     Monitor
  Entries   Groups       Submissions    Access     Health
    │       Clearances   Grant Access   Events     &Events
    │       Agents       Quotas        Security
    │                                  Compliance
    │
┌───┴──────────────────────────────────────────────────────┐
│         Notebook Owner (Orchestrator)                     │
│  • Create Notebooks                                       │
│  • Manage Access Control                                  │
│  • Monitor Jobs & Subscriptions                           │
│  • Review Submissions                                     │
└────────────────────────────────────────────────────────────┘
    │
    └─────────┬──────────────┬──────────────┬──────────────┐
              │              │              │              │
         ┌────▼───┐    ┌─────▼──┐   ┌─────▼──┐   ┌─────▼──┐
         │System  │    │ThinkerAgent│ │Cross-Org│   │MCP User│
         │Admin   │    │Operator    │ │Coordinator│(Claude) │
         └────────┘    └────────────┘ └──────────┘ └─────────┘
```

## Bell-LaPadula Security Model

```
Classification Hierarchy (Information Flows ↑ Only)

                    TOP_SECRET
                        △
                        │
                        │ (can read)
                        │
                      SECRET
                        △
                        │
                        │ (can read)
                        │
                   CONFIDENTIAL
                        △
                        │
                        │ (can read)
                        │
                      PUBLIC


Clearance Dominance Example:

User Clearance: TOP_SECRET / {Ops, Sec}
                     ▲
                     │ dominates
                     ▼

Entry Labels:
  ✓ PUBLIC / {}
  ✓ CONFIDENTIAL / {}
  ✓ SECRET / {Ops}
  ✓ TOP_SECRET / {Ops}
  ✗ TOP_SECRET / {Ops, Sec, Finance}  ← Missing Finance compartment
  ✗ TOP_SECRET / {Sec}                ← Missing Ops compartment


Information Flow Direction:

PUBLIC
  ↓ (can flow to)
CONFIDENTIAL (approved systems can read)
  ↓ (can flow to)
SECRET (restricted users)
  ↓ (can flow to)
TOP_SECRET (highest restriction)

Rule: Information ONLY flows UP or SAME level
      Never flows DOWN (no demotion)
```

## Entry Lifecycle & Integration

```
Entry Creation → Processing → Integration

1. WRITE Operation (User Creates Entry)
   ┌─────────────────────────────────┐
   │ New Entry Submitted              │
   │ • Content                        │
   │ • Topic                          │
   │ • References                     │
   │ • Author Signature               │
   └────────────┬────────────────────┘
                │
                ▼
   ┌─────────────────────────────────┐
   │ Status: PROBATION                │
   │ • Stored in database             │
   │ • Assigned position (causal)     │
   │ • Background jobs queued         │
   └────────────┬────────────────────┘
                │
                ▼
2. Background Processing (ThinkerAgents)

   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
   │DISTILL_CLAIMS│  │COMPARE_CLAIMS│  │EMBED_ENTRIES │
   │Extract claims│  │vs. other     │  │Create vectors│
   │from content  │  │entries       │  │for search    │
   └──────────────┘  └──────────────┘  └──────────────┘
            │                │                  │
            └────────────────┼──────────────────┘
                             │
                             ▼
   ┌─────────────────────────────────┐
   │ Compute Integration Cost (TF-IDF)│
   │ • Similarity to related entries  │
   │ • Cluster coherence analysis     │
   │ • Assign friction score (0-10)   │
   └────────────┬────────────────────┘
                │
                ▼
3. Status Transition

   Cost 0-2          Cost 2-5          Cost 5-10
   (Low Friction)    (Medium Friction) (High Friction)
        │                  │                │
        ▼                  ▼                ▼
   ┌─────────┐    ┌──────────┐    ┌─────────────┐
   │INTEGRATED│    │CONTESTED │    │CONTESTED    │
   │(Stable)  │    │(Evolving)│    │(Controversial)
   └─────────┘    └──────────┘    └─────────────┘
```

## Workflow Decision Trees

### Knowledge Contributor Decision Tree

```
Start: I want to do something in Cyber
          │
          ├─→ Create new knowledge
          │    └─→ [WF-KC-001: Creating Entries]
          │
          ├─→ Find existing knowledge
          │    ├─→ Within one notebook?
          │    │    └─→ [WF-KC-002: Browsing]
          │    │
          │    └─→ Across all notebooks?
          │         └─→ [WF-KC-003: Searching]
          │
          ├─→ Update information
          │    └─→ [WF-KC-004: Managing Revisions]
          │
          └─→ Stay informed of changes
               └─→ [WF-KC-005: Observing Changes]
```

### Organization Administrator Decision Tree

```
Start: Setting up organization structure
          │
          ├─→ First time setup?
          │    └─→ [WF-OA-001: Creating Structure]
          │         └─→ Add teams/groups (DAG)
          │         └─→ Set classification levels
          │         └─→ Define compartments
          │
          ├─→ Adding people to organization?
          │    ├─→ [WF-OA-002: Group Membership]
          │    │    └─→ Add to groups
          │    │    └─→ Assign roles
          │    │
          │    └─→ [WF-OA-003: Security Clearances]
          │         └─→ Set clearance level
          │         └─→ Assign compartments
          │
          └─→ Setting up background processing?
               └─→ [WF-OA-004: Configure Agents]
                    └─→ Register ThinkerAgents
                    └─→ Set security labels
```

## Organization Structure (DAG Example)

```
                        MyCompany (Root)
                      CONFIDENTIAL / {}
                            │
           ┌────────────────┼────────────────┐
           │                │                │
      ┌────▼────┐    ┌─────▼──────┐   ┌────▼──────┐
      │Engineering│  │Operations  │   │Finance    │
      │SECRET/{Ops}│ │CONFIDENTIAL│   │CONFIDENTIAL│
      │           │  │   / {Ops}  │   │    / {}    │
      └────┬──────┘  └─────┬──────┘   └────────────┘
           │                │
      ┌────┴──────┐    ┌────┴─────┐
      │  Backend  │    │Incident  │
      │SECRET/{Op,│    │Response  │
      │  Infra}   │    │CONF/{Ops}│
      └───────────┘    └──────────┘

Key Properties:
  • Classification inherits upward (child ≥ parent level)
  • Compartments accumulate downward (child = parent ∪ new)
  • No cycles (DAG property)
  • Users can belong to multiple groups
```

## Cross-Organization Subscription Flow

```
Source Organization          Sync Process          Target Organization
(ResearchCorp)                                     (MyCompany)

Public Research         Subscription Config
Notebook              ┌──────────────────────┐
                      │ Scope: Entries       │
                      │ Discount: 50%        │
                      │ Poll: Every 4 hours  │
                      │ Filter: topics...    │
                      └──────────┬───────────┘
        │                        │
        │ Position 1247          │
        │ Entry: "Q1 Results"    ▼ FETCH
        │ Entry: "Analysis"   ┌──────┐
        │ Entry: "Conclusions"│Cyber │
        ├──────────────────→  │API   │
        │                     └──┬───┘
        │                        │
        │ NEW: Position 1248     ▼
        │ Entry: "Forecast"  ┌─────────────┐
        │ ───────────────→   │Verify:      │
        │                    │• Clearance  │
        │                    │• Level      │
        │                    │• Compartments
        │                    └──────┬──────┘
        │                           │
        │                    (Bell-LaPadula Check)
        │                           │
        │                    Compliant?
        │                    ├─→ Yes: Mirror entries
        │                    │         Update watermark
        │                    │         Advance position
        │                    │
        │                    └─→ No: Pause sync
        │                            Alert admin
        │
        │ ← ← ← ← ← ← ← ← ← ← ← ← ←
        │
        │ Mirrored entries appear in MyCompany
        │ (marked as external/lower discount)
        │
        └──→ MyCompany users can:
             • Read mirrored entries
             • Reference them in their work
             • See they're external (lower weight)
```

## Job Processing Pipeline

```
User Creates Entry
       │
       ▼
┌─────────────────────────────────┐
│ Entry stored with Status:PROBATION│
│ Background jobs queued           │
└────────────┬────────────────────┘
             │
      ┌──────┴──────┬──────────┬──────────┐
      │             │          │          │
      ▼             ▼          ▼          ▼
  Job#1        Job#2      Job#3      Job#4
  DISTILL_   EMBED_     CLASSIFY_  COMPARE_
  CLAIMS     ENTRIES    ENTRIES    CLAIMS
  Extract    Create     Assign     Compare
  claims     vectors    topics     vs others
      │             │          │          │
      │  Agent 1 (Worker A)    │          │
      ├──────────────────→ Processing    │
      │                        │          │
      │         Agent 2 (Worker B)       │
      │              ├─────────────→ Processing
      │              │
      │   Completed Jobs Stream
      │       │  │  │  │
      │       ▼  ▼  ▼  ▼
      │    ┌──────────────────┐
      └──→ │ Compute Costs &  │
           │ Update Status    │
           └────────┬─────────┘
                    │
                    ▼
           Entry Status: INTEGRATED
           (or CONTESTED if high friction)

Agent Health Monitoring:
  ┌─────────────┐
  │ Heartbeat   │
  │ every 30s   │
  └────┬────────┘
       │
       ▼
  ┌─────────────┐
  │ Job Queue   │
  │ Dashboard   │
  └─────────────┘
```

## Notebook Owner's Responsibilities

```
Notebook Creation & Lifecycle

Create Notebook
      │
      ├─→ Set Name & Classification
      │
      ├─→ Assign Owner Group
      │
      ├─→ Configure Settings
      │    ├─→ Retention policy
      │    └─→ Ingestion gating (optional)
      │
      ├─→ Grant Access
      │    ├─→ Add users/groups
      │    └─→ Set access tiers (read/write/admin)
      │
      └─→ Monitor & Maintain
           ├─→ Review submissions (if gating enabled)
           ├─→ Monitor job pipeline
           ├─→ Manage subscriptions
           └─→ Audit compliance


Access Control Tiers:

Existence ← → Read ← → Read+Write ← → Admin
(Know it)   (View)    (Create/Edit)   (Full Control)
  │           │            │              │
  └─→ Typical contributor: Read+Write
  └─→ Typical manager: Admin
  └─→ Typical viewer: Read or Existence
```

## Security Review Process

```
Entry Submission (if gating enabled)

User Submits Entry
       │
       ▼
  ┌─────────────────────────┐
  │ Status: PENDING REVIEW  │
  │ Appears in review queue │
  └────────┬────────────────┘
           │
      Notebook Owner Reviews
           │
      ┌────┴────┬───────────┬──────────┐
      │         │           │          │
      ▼         ▼           ▼          ▼
    APPROVE REQUEST  REJECT OTHER
             CHANGES
      │         │           │          │
      ▼         ▼           ▼          ▼
  Published Feedback Denied Handled
  (ready)    Request  (deleted) (depends)
            (waiting)
```

---

**Last updated:** February 21, 2026
**Chapter version:** 1.0.0 (Beta)
**Platform Version:** 2.1.0
