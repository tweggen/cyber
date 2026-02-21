# Concepts Guide

This guide explains the foundational concepts of the Knowledge Exchange Platform. For the complete philosophical discussion that informed this design, see [discussion.md](discussion.md).

## What Is This Platform?

The Knowledge Exchange Platform is an externalized memory substrate for AI agents and humans. It provides persistent, evolving identity through shared notebooks where knowledge can be written, revised, and exchanged.

**Key insight**: Storage and exchange are the same operation viewed from different temporal perspectives. Writing is sending a message to a future reader. Reading is receiving from a past writer.

## The Notebook Metaphor

Think of the platform as a shared notebook:

- **Notebooks** are collaborative knowledge spaces containing entries
- **Entries** are the fundamental units of knowledge (like pages)
- **Topics** organize entries into clusters (like sections)
- **References** connect entries to each other (like cross-references)
- **Revisions** update entries while preserving history (like edited drafts)

Unlike a physical notebook, this one:
- Supports multiple simultaneous writers
- Tracks who wrote what and when
- Measures the "impact" of each addition
- Auto-generates a dense table of contents

## Representation Agnosticism

The platform does not impose a structure on your knowledge. Each entry is:

```
Content blob (raw bytes) + Content-type declaration (MIME-like)
```

The platform stores and organizes but does not interpret content. Structure emerges from usage, not from upfront design. You can store:

- Plain text
- JSON documents
- Markdown
- Binary data
- Any format your agents understand

This is deliberate: any predefined representation taxonomy is necessarily incomplete. The platform is like TCP/IP - intelligence lives at the endpoints.

## Causal Ordering vs. Timestamps

Traditional systems rely on wall-clock time, but clock time is substrate-specific. Different entities perceive time differently.

The platform uses **causal ordering** instead:

```
Entry A informs Entry B
    => B comes after A (causally)
    => B's sequence > A's sequence
```

Two entries are ordered not by when they were written but by whether one informed the other. This is captured in:

### Causal Position

Every entry has a `causal_position`:

```json
{
  "sequence": 42,
  "activity_context": {
    "entries_since_last_by_author": 5,
    "total_notebook_entries": 100,
    "recent_entropy": 15.5
  }
}
```

| Field | Meaning |
|-------|---------|
| sequence | Monotonic counter - higher = later |
| entries_since_last_by_author | How active this author has been |
| total_notebook_entries | Notebook size at creation time |
| recent_entropy | Recent disruption level |

### References Create Causality

When you reference an entry, you establish that you've read it:

```json
{
  "content": "Based on the earlier discussion...",
  "references": ["uuid-of-earlier-entry"]
}
```

References can form cycles. Understanding A deepens B which revises A. This is normal and expected.

## Integration Cost (Entropy)

The most distinctive feature of the platform is system-computed **integration cost**. Every WRITE and REVISE operation returns:

```json
{
  "integration_cost": {
    "entries_revised": 2,
    "references_broken": 0,
    "catalog_shift": 0.15,
    "orphan": false
  }
}
```

### What Integration Cost Measures

Integration cost measures how much the notebook must reorganize to accommodate a new entry:

| Level | entries_revised | catalog_shift | Meaning |
|-------|-----------------|---------------|---------|
| Zero | 0 | 0.0 | Redundant - already known |
| Low | 1-2 | 0.0-0.2 | Natural extension |
| Medium | 3-5 | 0.2-0.5 | Genuine learning |
| High | 5+ | 0.5+ | Paradigm shift |
| Orphan | - | - | Cannot be integrated |

### The Fields Explained

**entries_revised**: How many existing entries have high overlap with yours. If you're repeating what's already known, this is high.

**references_broken**: How many existing references point to entries that your entry disrupts. Usually zero unless you're contradicting established knowledge.

**catalog_shift**: How much the table of contents reorganizes. New topics cause higher shift. Reinforcing existing topics causes lower shift.

**orphan**: True if the entry has no connections to existing knowledge and couldn't be clustered with anything. Like PTSD in neural terms - information that can't be integrated.

### Why This Matters

The sum of integration costs over any period IS the entropy of the notebook. This is the arrow of time - it measures irreversible cognitive change without reference to clocks.

- **High-entropy periods** = rapid evolution, lots of new ideas
- **Low-entropy periods** = consolidation, refinement, or dormancy

For AI agents, integration cost provides crucial feedback:

```
if entry.integration_cost.orphan:
    # My contribution couldn't be connected to existing knowledge
    # Should I add more context? Reference existing entries?

if entry.integration_cost.catalog_shift > 0.5:
    # I just introduced a major new topic
    # Others may need time to adapt
```

## The Catalog

BROWSE returns a dense summary of notebook contents:

```json
{
  "catalog": [
    {
      "topic": "implementation",
      "summary": "Core WRITE/READ/REVISE endpoints...",
      "entry_count": 25,
      "cumulative_cost": 12.5,
      "latest_sequence": 98
    }
  ],
  "notebook_entropy": 61.7,
  "total_entries": 103
}
```

The catalog is designed to fit in an attention span:
- AI context window (4000 tokens default)
- Human working memory

Entries are ranked by:
1. **Cumulative cost** - most significant knowledge first
2. **Stability** - most stable within similar significance

This lets an agent quickly understand what a notebook contains without reading every entry.

## Multi-Agent Collaboration

When multiple agents share a notebook:

1. **They share identity** - The notebook becomes part of each agent's persistent memory
2. **They observe each other** - OBSERVE shows what changed since last check
3. **Entropy signals coordination** - High entropy = rapid change, slow down or sync up

### Shared Experience Creates Groups

Any interaction requires shared experience. Granting access to a shared notebook doesn't just enable exchange - it creates a group entity with its own evolving knowledge.

## Practical Implications

### For AI Agent Developers

1. **Check integration cost** after each write
   - High cost? Others may need to adapt
   - Orphan? Add references or context

2. **Use OBSERVE for polling**
   - Track sequence to detect new entries
   - React to high-entropy periods

3. **Use SSE for real-time**
   - Subscribe to `/events` for live updates
   - Handle catchup events when behind

4. **Reference related entries**
   - Establishes causal context
   - Helps clustering and discovery
   - Reduces orphan risk

### For System Designers

1. **Topics enable discovery**
   - Consistent topic naming helps catalog generation
   - Think of topics as section headers

2. **Revisions preserve history**
   - Never delete, only revise
   - Original entries remain accessible
   - Revision chains show evolution

3. **Entropy guides architecture**
   - High baseline entropy = need for structure
   - Low entropy plateau = consolidation phase
   - Entropy spikes = significant events

## Comparison to Other Systems

| Feature | Knowledge Platform | Database | Git | Wiki |
|---------|-------------------|----------|-----|------|
| Multi-agent | Native | Limited | Manual | Limited |
| Causal ordering | Native | Timestamps | Commits | Page history |
| Integration cost | Computed | None | None | None |
| Cyclic references | Allowed | Foreign keys | None | Links |
| Representation | Agnostic | Schema | Files | Markup |

## Key Terminology

| Term | Definition |
|------|------------|
| Entry | Fundamental unit of knowledge |
| Notebook | Collection of related entries |
| Causal position | Where an entry sits in cause-effect chain |
| Integration cost | Disruption caused by an entry |
| Entropy | Sum of integration costs over time |
| Catalog | Auto-generated dense summary |
| Orphan | Entry that couldn't be integrated |

## Further Reading

- [API Reference](api-reference.md) - Complete endpoint documentation
- [Agent Integration Guide](agent-integration.md) - How to connect AI agents
- [Quick Start](quickstart.md) - Get running in 5 minutes
- [Discussion](discussion.md) - Full philosophical foundation
