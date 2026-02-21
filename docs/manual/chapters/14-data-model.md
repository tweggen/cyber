# Chapter 14: Data Model Deep-Dive

## Notebooks

A notebook is the primary container for knowledge.

**Properties:**
- `id` — Unique identifier (e.g., `nb_xyz789`)
- `name` — Display name (e.g., "Engineering Architecture")
- `description` — Purpose and scope
- `owner_group_id` — Group that owns this notebook
- `classification` — Security label (e.g., `SECRET / {Operations}`)
- `created_at` — Timestamp of creation
- `position` — Current causal position (highest entry position)
- `retention_policy` — How long entries are kept

**Access Tiers (per user/group):**
- `existence` — Know it exists but can't read
- `read` — Can read all entries
- `read+write` — Can read and create entries
- `admin` — Full control including access management

**Relationships:**
- Owns many entries
- Belongs to organization
- Owned by group
- Has subscriptions (to other notebooks)
- Has subscribers (other notebooks subscribe to it)

---

## Entries

An entry is an immutable unit of knowledge.

**Properties:**
- `id` — Unique identifier (e.g., `entry_abc123`)
- `position` — Causal ordering (monotonic per notebook)
- `notebook_id` — Which notebook contains this entry
- `content` — The actual knowledge (binary blob)
- `content_type` — MIME type (e.g., `text/markdown`)
- `author_id` — Hash of author's public key
- `signature` — Ed25519 cryptographic signature
- `topic` — Hierarchical topic path (e.g., `org/engineering/backend`)
- `references` — IDs of related entries (array)
- `created_at` — Timestamp
- `integration_cost` — Measure of coherence impact (0-10)
- `status` — `probation`, `integrated`, or `contested`

**Invariants:**
- Immutable once created (can only revise, not edit)
- Cryptographically signed by author
- Position never changes (causal ordering)

---

## Revisions

A revision is a new version of an entry.

**Properties:**
- `id` — Revision entry ID
- `original_entry_id` — Entry being revised
- `position` — New position (higher than original)
- `reason` — Why this revision was made
- `content` — Updated content
- `author_id` — Who made the revision

**Usage:**
```
Entry v1 (position 100): "Initial architecture"
  ↓ revised
Entry v2 (position 101): "Updated with feedback"
  ↓ revised
Entry v3 (position 102): "Added performance metrics"
```

Readers see v3 by default; history shows all versions.

---

## Causal Positions

Instead of timestamps, entries use causal positions.

**Why Causal Positions?**
- No clock synchronization needed
- Works in distributed systems
- Consistent ordering across replicas
- Immune to clock skew

**Properties:**
- Monotonically increasing per notebook
- Start at 1
- Never reused
- Immutable once assigned

**Example:**
```
Notebook "Q1 Planning" positions:

Position 1: "Goals"          (created Jan 10, 9:00 AM)
Position 2: "Budget"         (created Jan 10, 10:00 AM)
Position 3: "Resources"      (created Jan 15, 2:00 PM)
Position 4: "Timeline"       (created Jan 10, 11:00 AM) ← out of order

Order of creation:    1, 2, 4, 3
Causal order:         1, 2, 3, 4 (positions determine order, not timestamps)
```

---

## Integration Cost

Measures how well an entry aligns with existing knowledge.

**Calculation:**
1. Compare new entry against all existing entries (TF-IDF)
2. Form clusters of related entries
3. Compute coherence of clusters
4. Integration cost = disruption to coherence

**Interpretation:**
```
Cost 0-2:   Low friction, well-aligned
Cost 2-5:   Medium friction, some disagreement
Cost 5-10:  High friction, major disagreement
```

**Status Evolution:**
```
PROBATION      INTEGRATED       CONTESTED
(new)    →    (stable, low  ) → (stable, high)
              cost < 2          cost > 5
```

Computed by background jobs; retroactively updated when contradictions arise.

---

## Job Queue

Background processing system.

**Job Types:**
- `DISTILL_CLAIMS` — Extract claims from entries
- `COMPARE_CLAIMS` — Compare claims between entries
- `EMBED_ENTRIES` — Create vector embeddings
- `CLASSIFY_ENTRIES` — Assign topics/categories

**Job Lifecycle:**
```
PENDING → IN_PROGRESS → COMPLETED
           ↓ (error)
        FAILED
```

**Properties per Job:**
- `id` — Job ID
- `type` — Job type
- `entry_id` — Entry being processed
- `status` — Current state
- `started_at` — Timestamp
- `completed_at` — Timestamp
- `error` — Error message if failed

**Retry Policy:**
- Automatic retries on failure
- Exponential backoff
- Max retries: 3
- Max retry age: 24 hours

---

## Claims and Comparisons

Extracted knowledge units.

**Claim:**
```
Entry: "Database Indexing Strategy"

Extracted claims:
  • "PostgreSQL indexes improve query performance 50x"
  • "Compound indexes should match query patterns"
  • "Regular ANALYZE updates statistics"
```

**Comparison:**
```
Claim A (Entry 1): "Use Redis for caching"
Claim B (Entry 2): "Use Memcached for caching"

Comparison result: Similar (both caching solutions)
Friction: High (different approach to same problem)
Status: Contested (multiple valid approaches)
```

---

## Audit Logs

Immutable record of all operations.

**Properties:**
- `timestamp` — When operation occurred
- `actor_id` — Who performed it
- `action` — What they did (WRITE, READ, etc.)
- `resource` — What was affected
- `status` — Success or failure
- `details` — Additional context
- `signature` — Cryptographic proof

**Retention:** Permanent (7+ year minimum compliance)

---

## Subscriptions

Cross-organization data mirroring.

**Properties:**
- `source_notebook_id` — Remote notebook
- `target_organization_id` — Receiving organization
- `scope` — Catalog / Catalog+Claims / Entries
- `discount_factor` — Relevance weight (0.1-1.0)
- `polling_interval` — Sync frequency
- `watermark` — Last synced position
- `last_sync_time` — Timestamp

**Constraints:**
- Source classification ≤ Target organization classification
- No cycles (prevent circular data flow)
- Bell-LaPadula compliance enforced

---

## Organization Structure (DAG)

Directed acyclic graph of groups.

**Properties:**
- `name` — Group name
- `parent_ids` — Parent groups (can have multiple)
- `child_ids` — Child groups
- `classification` — Inherited + elevated
- `compartments` — Inherited + supplemented

**Example DAG:**
```
        MyCompany
         /      \
    Engineering  Operations
      /    \
  Backend Infrastructure
```

Users can have complex memberships (in multiple groups).

---

## Clearances

Security access specifications.

**Properties:**
- `principal_id` — User or group ID
- `level` — Classification level
- `compartments` — Array of compartment names
- `created_at` — When clearance was granted
- `expires_at` — Optional expiration date

**Dominance Check:**
```python
def clearance_dominates(clearance, label):
    return (clearance.level >= label.level and
            label.compartments.issubset(clearance.compartments))
```

---

**Last updated:** February 21, 2026
**Chapter version:** 1.0.0 (Beta)
**Platform Version:** 2.1.0
