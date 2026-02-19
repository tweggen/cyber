# 11 — Classified Interaction: How Thinktanks at Different Levels Create Shared Value

**Status:** Concept draft.
**Builds on:** 08-SECURITY-MODEL.md (lattice model, subscriptions), 02-ENTROPY-AND-FRICTION.md (integration cost), 09-PHASE-HUSH.md (implementation plan).

## The Core Idea

A single thinktank is a coherent identity — it absorbs knowledge, detects novelty and contradiction, and builds an ever-richer picture of its domain. But real knowledge doesn't live in isolation. An aerospace defense program benefits from open academic research. A product team benefits from competitor intelligence. A joint venture benefits from both parent organizations' expertise.

The classification lattice makes this possible **without compromising security**: information flows upward through subscriptions, never downward. The result is a **federated knowledge lattice** where each thinktank sees a different horizon of the same underlying reality, and higher-classified thinktanks always hold the richer picture.

This document explores what that looks like in practice — the interaction patterns, the emergent properties, and the concrete value that a multi-level topology produces.

## Reference Topology

Throughout this document we use a concrete scenario involving two organizations collaborating on a defense program:

```
                         ┌────────────────────────────┐
                         │ thinktank-excalibur         │
                         │ TOP_SECRET {EXCALIBUR}      │
                         │ Joint venture: MS + Boeing  │
                         └──────┬─────────┬───────────┘
                     subscribes │         │ subscribes
                   ┌────────────┘         └──────────────┐
                   ▼                                     ▼
    ┌──────────────────────────┐       ┌──────────────────────────┐
    │ thinktank-boeing-defense │       │ thinktank-ms-azgov       │
    │ CONFIDENTIAL             │       │ CONFIDENTIAL             │
    │ Boeing Defense division  │       │ Microsoft Azure Gov      │
    └──────────┬───────────────┘       └──────────┬───────────────┘
    subscribes │                       subscribes │
               ▼                                  ▼
    ┌──────────────────────────┐       ┌──────────────────────────┐
    │ thinktank-boeing-public  │       │ thinktank-ms-public      │
    │ PUBLIC                   │       │ Microsoft public research │
    │ Boeing open publications │       │ PUBLIC                   │
    └──────────────────────────┘       └──────────────────────────┘
```

Each arrow represents a subscription — the higher-classified thinktank pulls knowledge from the lower one. The arrows are strictly upward in classification. No information flows back down.

## Subscription Scopes: Three Depths of Interaction

Subscriptions come in three scopes (defined in Hush-6). Each represents a fundamentally different relationship between thinktanks.

### Catalog Scope — "I know what you know about"

The subscriber receives the source thinktank's **catalog summaries** — topic clusters with their labels, entry counts, and integration cost. No individual claims or entry content crosses the boundary.

**What it enables:**
- **Situational awareness.** The SECRET thinktank knows that the PUBLIC thinktank has a growing cluster on "composite material fatigue testing" with high entropy (lots of novel research pouring in). A human analyst or agent at the SECRET level can decide whether to dig deeper.
- **Gap detection.** If the subscriber's catalog has no cluster matching a major cluster in the source, that's a signal. The defense thinktank may lack coverage on a topic where open research is active.
- **Trend monitoring.** Integration cost trends in the source's catalog reveal which topics are stabilizing (low new entropy) and which are in flux (high entropy, high friction). This is intelligence about the state of knowledge in the lower tier, without seeing the knowledge itself.

**Cost:** Minimal. Catalog summaries are token-budgeted (default ~53 summaries). Sync is periodic and lightweight.

**Example:** `thinktank-boeing-defense` subscribes to `thinktank-boeing-public` at catalog scope. A defense analyst sees that the public thinktank's "autonomous navigation" cluster has spiked in friction — contradictory claims are accumulating around sensor fusion approaches. This tells the defense team that the open research community hasn't converged yet, which is relevant context for their own classified sensor work.

### Claims Scope — "I know what you believe"

The subscriber receives the source thinktank's **distilled claims** — the atomic propositions extracted from entries. This is richer than catalog summaries but still doesn't include raw entry content.

**What it enables:**
- **Cross-boundary contradiction detection.** This is the most valuable interaction pattern. The subscriber's entropy engine can run COMPARE_CLAIMS jobs between its own claims and the subscribed claims. When a PUBLIC claim contradicts a SECRET claim, the SECRET thinktank detects it — but the PUBLIC thinktank never learns that a contradiction exists. (See "Asymmetric Contradiction Detection" below.)
- **Novelty assessment.** New claims in the source can be scored for entropy against the subscriber's existing knowledge. A claim that's novel at the PUBLIC level might be redundant at the SECRET level (they already know this), or it might genuinely be new even to the classified audience.
- **Embedding-based discovery.** Subscribed claims participate in the subscriber's nearest-neighbor search. When a new SECRET entry is written, its claims are compared against both local and subscribed claims, potentially surfacing relevant open-source context that a human author wouldn't have thought to look for.

**Cost:** Moderate. Claim sets are larger than catalog summaries, and cross-boundary COMPARE_CLAIMS jobs consume agent time. But claims are compact (fixed-size, as defined in 01-CLAIM-REPRESENTATION.md), so storage and sync overhead is manageable.

**Example:** `thinktank-ms-azgov` subscribes to `thinktank-ms-public` at claims scope. A researcher in Azure Gov writes an entry about edge-compute latency requirements for government workloads. The entropy engine finds that three of the entry's claims contradict claims from the public thinktank's "Azure edge performance benchmarks" cluster. The contradiction is surfaced at the CONFIDENTIAL level — the gov-specific requirements diverge from the public benchmarks. This triggers a review by a cleared analyst who can reconcile the two perspectives.

### Entries Scope — "I have read your work"

The subscriber receives **full entry content** from the source. This is the deepest level of integration — the source's entries become part of the subscriber's knowledge base, marked as external reference material.

**What it enables:**
- **Deep integration.** Subscribed entries participate fully in the subscriber's entropy model. New local entries are compared against both local and subscribed content, with the full context of the original entry available (not just extracted claims).
- **Re-distillation.** The subscriber can run its own DISTILL_CLAIMS on subscribed entries, potentially extracting different claims than the source did — because the subscriber operates in a different context and may see significance that the source missed.
- **Synthesis across boundaries.** An agent at the subscriber's level can synthesize across local and subscribed content, producing new entries that combine open and classified knowledge. The synthesis itself exists only at the subscriber's level.

**Cost:** Highest. Full content sync, storage, and processing. Use this scope only when the source thinktank's content is directly relevant and the subscriber needs deep access.

**Example:** `thinktank-excalibur` (the TOP_SECRET joint venture) subscribes to both `thinktank-boeing-defense` and `thinktank-ms-azgov` at entries scope. An Opus-class agent at the TOP_SECRET level can read Boeing's CONFIDENTIAL defense analyses alongside Microsoft's CONFIDENTIAL gov-cloud architecture docs, and synthesize insights that neither organization could produce alone — because neither can see the other's CONFIDENTIAL material. Only the joint venture, cleared for both, can combine them.

## Interaction Patterns

### Pattern 1: Asymmetric Contradiction Detection

The most distinctive capability of the federated lattice. When a lower-tier thinktank publishes a claim that contradicts a higher-tier claim, only the higher tier sees the contradiction.

```
thinktank-boeing-public publishes:
  Claim: "Titanium alloy Ti-6Al-4V shows fatigue failure at 10^7 cycles
          under 500 MPa stress in salt-spray environments"

thinktank-boeing-defense holds (CONFIDENTIAL):
  Claim: "Modified Ti-6Al-4V with treatment X shows no fatigue failure
          at 10^7 cycles under 500 MPa stress in salt-spray environments"

The defense thinktank detects friction:
  → The public data contradicts what defense knows about the modified alloy.
  → This is valuable: it confirms the treatment's effectiveness by contrast.
  → The public thinktank never learns that a contradiction was detected.
  → The public thinktank never learns that "treatment X" exists.
```

The asymmetry is the feature. The higher-classified thinktank gains insight from the contradiction. The lower-classified thinktank's view of reality is unchanged. No information leaks downward.

**Processing rule:** Cross-boundary COMPARE_CLAIMS jobs must be routed to an agent cleared for the *subscriber's* level. The comparison result contains information about the subscriber's claims and must never be exposed at the source's level.

### Pattern 2: The Enrichment Pipeline

Multiple classification levels form a pipeline where knowledge becomes progressively more specialized and contextualized as it flows upward.

```
PUBLIC                INTERNAL               CONFIDENTIAL            SECRET
Academic papers  →    Applied research   →   Product engineering →   Defense application
Open datasets        Internal experiments    Proprietary methods     Classified requirements
Community forums     Team retrospectives     Customer-specific data  Threat assessments
```

Each level subscribes to the one below. At each stage:
- **Entropy decreases** — the higher level already knows much of what the lower level contributes (it's been reading it). Novel findings are rarer but more significant when they appear.
- **Friction becomes more meaningful** — when an open-source paper contradicts internal applied research, that's a signal worth investigating. When internal research contradicts product engineering assumptions, that's a potential bug.
- **Context deepens** — the same base fact (say, a benchmark result) acquires additional classified context at each level, producing richer understanding.

This is not just aggregation. Each thinktank is an independent identity with its own entropy model. The defense thinktank doesn't become a superset of the public one — it develops its own perspective, informed by but not subservient to the lower tiers.

### Pattern 3: Cross-Pollination Through a Common Base

Two compartmented thinktanks that cannot see each other both subscribe to the same lower-tier thinktank.

```
       ┌─────────────────────┐     ┌──────────────────────────┐
       │ SECRET {AVIONICS}   │     │ SECRET {PROPULSION}      │
       │ Flight control team │     │ Engine design team        │
       └──────────┬──────────┘     └──────────┬───────────────┘
       subscribes │                subscribes │
                  ▼                           ▼
       ┌──────────────────────────────────────────────────┐
       │ CONFIDENTIAL (no compartments)                   │
       │ Boeing Defense — general engineering knowledge   │
       └──────────────────────────────────────────────────┘
```

The avionics team and propulsion team cannot see each other's work (compartmented). But both benefit from the shared CONFIDENTIAL engineering base. When the CONFIDENTIAL thinktank absorbs a new materials science finding, both SECRET thinktanks learn of it through their subscriptions — and each evaluates it independently against their own specialized knowledge.

**Emergent property:** The CONFIDENTIAL layer acts as a **knowledge commons** — a shared foundation that benefits all compartments above it without creating cross-compartment information flow. This is enormously valuable in large organizations where teams work in silos but share fundamental technical foundations.

### Pattern 4: The Federation Nexus

A joint-venture thinktank subscribes to multiple organizations' thinktanks, creating a **nexus** where knowledge from different organizational silos converges for the first time.

```
thinktank-excalibur (TOP_SECRET {EXCALIBUR})
    ├── subscribes → thinktank-boeing-defense (CONFIDENTIAL)
    ├── subscribes → thinktank-ms-azgov (CONFIDENTIAL)
    └── subscribes → thinktank-dga (SECRET)    [French MoD]
```

At the nexus:
- Boeing's defense manufacturing knowledge meets Microsoft's cloud architecture knowledge meets French MoD operational requirements.
- No single parent organization can see the full picture. Only the joint venture, cleared for all sources, has the combined view.
- The nexus thinktank's entropy engine compares claims across all three sources. Contradictions between Boeing's assumptions and French MoD requirements surface automatically.
- Synthesis agents at the TOP_SECRET level can produce insights that are genuinely novel — they emerge from the *combination* of sources, not from any single one.

**The federation agreement** is critical here. Each parent organization must explicitly approve what flows into the nexus (scope, topic filters), and an audit trail records every subscription and its approvers.

### Pattern 5: Compliance and Oversight

A specialized thinktank at a higher level monitors lower-level thinktanks for policy compliance, consistency, or risk.

```
       ┌──────────────────────────────────┐
       │ CONFIDENTIAL — Compliance Office │
       └──┬────────────┬────────────┬─────┘
          │            │            │
          ▼            ▼            ▼
    ┌──────────┐ ┌──────────┐ ┌──────────┐
    │INTERNAL  │ │INTERNAL  │ │INTERNAL  │
    │Team A    │ │Team B    │ │Team C    │
    └──────────┘ └──────────┘ └──────────┘
```

The compliance thinktank subscribes (claims scope) to all departmental thinktanks. It detects:
- **Cross-team contradictions:** Team A claims deployments go to staging first; Team B's processes skip staging. The individual teams don't see each other's claims, but compliance sees both and flags the friction.
- **Policy drift:** The compliance thinktank holds policy claims. When a team's operational claims diverge from policy, friction scores rise.
- **Risk accumulation:** A topic cluster across multiple teams shows high unresolved friction — nobody agrees on the approach. This is organizational risk, visible only at the oversight level.

## Entropy Across Boundaries

### The Integration Cost Question

Open question from 08-SECURITY-MODEL: do subscribed entries contribute to the subscriber's integration cost?

**Proposed answer: yes, but at discounted weight.**

The reasoning: integration cost represents how much a thinktank's worldview must change to accommodate new information. Subscribed content from a lower tier is external context — it informs but doesn't define the thinktank's identity. A full-weight contribution would mean the subscriber's entropy model is dominated by the source's volume (public thinktanks tend to be much larger). A zero contribution would mean cross-boundary contradictions have no entropy signal.

A **discount factor** (configurable per subscription, default 0.3) scales the integration cost of subscribed content. This means:
- Cross-boundary contradictions register, but don't dominate
- The subscriber's own content remains the primary driver of its identity
- Removing a subscription doesn't shatter the subscriber's entropy model

The discount factor is itself meaningful: a subscription with discount 0.1 says "this is background context." A subscription with discount 0.8 says "this source is almost as authoritative as our own work."

### Entropy Gradient Across the Lattice

An interesting emergent property: entropy tends to behave differently at each classification level.

| Level | Typical Entropy Pattern | Reason |
|---|---|---|
| PUBLIC | High entropy, moderate friction | Many contributors, diverse perspectives, active disagreement |
| INTERNAL | Moderate entropy, lower friction | Fewer contributors, organizational alignment reduces contradiction |
| CONFIDENTIAL | Lower entropy, targeted friction | Specialized domain, new information is rarer but contradictions are more significant |
| SECRET+ | Lowest entropy, highest-significance friction | Very few contributors, highly focused. Any contradiction demands attention. |

This gradient means that upward subscriptions act as **noise filters**. The PUBLIC thinktank's high-entropy, high-churn content is distilled as it flows upward — the CONFIDENTIAL subscriber only sees what's novel *relative to what it already knows*. By the time information reaches the SECRET level through two subscription hops, only genuinely significant signals survive.

### Ripple Propagation

When the source thinktank changes, those changes propagate upward through subscriptions:

```
1. Public thinktank: new entry arrives with novel claims
2. Public thinktank: DISTILL_CLAIMS runs, claims extracted
3. Public thinktank: integration cost computed locally
4. Subscription sync: new claims propagate to CONFIDENTIAL subscriber
5. CONFIDENTIAL thinktank: COMPARE_CLAIMS runs against local + subscribed claims
6. CONFIDENTIAL thinktank: friction detected — new public claim contradicts local claim
7. CONFIDENTIAL thinktank: integration cost updated (discounted)
8. If CONFIDENTIAL thinktank has its own subscribers: ripple continues upward
```

This propagation is **asynchronous and eventually consistent**. There is no global transaction across thinktanks. Each level processes at its own pace, using its own agents. The causal ordering within each thinktank is preserved; cross-thinktank ordering is best-effort.

Latency is acceptable because the value is analytical, not operational. Whether the SECRET thinktank learns of a PUBLIC contradiction in 5 minutes or 5 hours rarely matters.

## What a Participant Sees

Different principals experience the lattice differently, depending on their clearance and access tier.

### A PUBLIC contributor

Sees only `thinktank-boeing-public`. Writes entries, sees the catalog, sees entropy/friction for their contributions relative to the public knowledge base. Has no awareness that higher-classified thinktanks exist or subscribe. Their experience is identical to a single-thinktank deployment.

### A CONFIDENTIAL analyst at Boeing Defense

Sees `thinktank-boeing-defense` and (through its subscriptions) is aware of topics and claims from `thinktank-boeing-public`. Can browse the defense thinktank's catalog, which includes both local entries and subscribed content (marked as external). Sees friction scores that account for cross-boundary contradictions. Cannot see `thinktank-excalibur` or `thinktank-ms-azgov` (lacking clearance or compartment access).

### A TOP_SECRET engineer on Excalibur

Sees `thinktank-excalibur` with subscriptions to Boeing Defense and MS Azure Gov. The catalog shows a unified view: local entries, Boeing's CONFIDENTIAL claims, Microsoft's CONFIDENTIAL claims, and (transitively, through those subscriptions' own subscriptions) awareness of public research trends. Contradictions between Boeing and Microsoft assumptions are visible. This person has the richest picture of anyone in the lattice.

### An organizational administrator

Doesn't necessarily have clearance to read any thinktank's content, but has existence-tier awareness of all thinktanks in their organization. Can see the subscription topology, audit trail, and aggregate metrics (how many entries, how many unresolved contradictions) without seeing the entries themselves. This supports governance without requiring content access.

## Operational Considerations

### Subscription Topology as Organizational Knowledge Architecture

The subscription graph is itself a design artifact that encodes organizational knowledge-sharing strategy. Choosing what subscribes to what, at which scope, is an architectural decision:

- **Broad catalog subscriptions** (many sources, catalog scope) create wide situational awareness with low overhead.
- **Targeted claims subscriptions** (few sources, claims scope) create deep contradiction detection with moderate overhead.
- **Selective entries subscriptions** (very few sources, entries scope) create full integration for critical knowledge flows.

A well-designed subscription topology mirrors the organization's actual information needs, not its org chart.

### The Stale Subscription Problem

When a source thinktank is actively evolving and the subscriber hasn't synced recently, the subscriber's view is stale. This is generally acceptable (see "Ripple Propagation" above), but there are edge cases:

- A source thinktank resolves a major contradiction. The subscriber still sees the old friction until sync catches up.
- A source thinktank deletes entries (rare, but possible). The subscriber holds orphaned references.

Mitigation: subscriptions carry a **watermark** (the source's causal position at last sync). The subscriber can display staleness: "Last synced at position 4,721 of 4,850" — making the lag visible to analysts.

### Air-Gapped Thinktanks

A TOP_SECRET air-gapped thinktank cannot subscribe to a CONFIDENTIAL thinktank via network. The subscription model must accommodate offline sync:

- **Data diode export:** The source thinktank exports a signed, encrypted bundle of claims/entries matching the subscription scope and topic filter.
- **Physical transfer:** The bundle is moved to the air-gapped network via approved media.
- **Import:** The air-gapped thinktank ingests the bundle, validates signatures, and processes it as subscription sync.
- **Audit:** Both sides log the export/import with timestamps and content hashes.

This is operationally expensive but mirrors how classified environments already handle cross-network transfers. The subscription model provides the logical framework; the physical transfer is an operational concern.

## Summary

The multi-classification thinktank lattice is more than access control — it is a **knowledge architecture** where:

1. **Each thinktank is a coherent identity** with its own entropy model and worldview.
2. **Subscriptions create upward information flow** that enriches higher-classified thinktanks without compromising lower ones.
3. **Cross-boundary contradiction detection** surfaces insights that no single thinktank could discover alone.
4. **The entropy gradient** naturally filters noise as information flows upward.
5. **Nexus thinktanks** at the intersection of multiple organizations produce genuinely novel synthesis.
6. **The subscription topology** is itself a strategic design decision that encodes how knowledge should flow through the organization.

The result is a system where classification boundaries — usually seen as barriers to knowledge sharing — become **productive membranes** that filter, contextualize, and add value to information as it crosses them.
