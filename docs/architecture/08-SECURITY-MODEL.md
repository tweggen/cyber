# 08 — Security Model for Multi-Organization Classified Thinktank

**Status:** Draft — architectural proposal, not yet implemented.

## Motivation

Consider Microsoft and Boeing running a super-secret military joint venture for the French government, all using thinktank. An MS Office salesperson and a Boeing seat designer must have zero visibility. Only specific subgroups get r/w access, a slightly larger set gets r/o, and a marginally larger set (executives, officials) may know the notebook exists at all.

This demands a lattice-based security model integrated into the notebook's core — not bolted on.

## Core Principle: One Classification Per Thinktank

Each thinktank instance operates at **a single classification level**, determined by its owning node in the organizational DAG. Only content at or below that classification is permitted to enter. This mirrors how classified networks work in practice — SIPRNET (SECRET), JWICS (TOP SECRET/SCI), and NIPRNet (UNCLASSIFIED) are separate physical networks, not one network with per-packet classification.

This avoids the hardest problems that per-entry classification would introduce:
- No per-horizon entropy views — all entries are at the same level, so the entropy model remains a single truth
- No claim side channels — all claims are at the same level, no cross-level comparisons
- No comparison JOIN labels — all comparisons are within the same classification
- No "shattered identity" — each thinktank IS one coherent identity

The hard problem shifts to **inter-thinktank information flow**.

### Example Topology

```
Boeing/                              → thinktank-boeing-public       (PUBLIC)
Boeing/Defense/                      → thinktank-boeing-defense      (CONFIDENTIAL)
Boeing/Defense/F-35/                 → thinktank-boeing-f35          (SECRET)

Microsoft/                           → thinktank-ms-public           (PUBLIC)
Microsoft/Azure-Gov/                 → thinktank-ms-azgov            (CONFIDENTIAL)

FrenchMoD/DGA/                       → thinktank-dga                 (SECRET)

MS-Boeing-France/Excalibur/          → thinktank-excalibur           (TOP_SECRET, {EXCALIBUR})
```

Each thinktank belongs to exactly one node in the organizational DAG. The node's classification level IS the thinktank's classification level.

## Security Label Primitive

```
Security Label = (Level, Set<Compartment>)
```

- **Level** — ordered: `PUBLIC < INTERNAL < CONFIDENTIAL < SECRET < TOP_SECRET`
- **Compartments** — unordered set of need-to-know tags, e.g. `{EXCALIBUR, AVIONICS}`
- **Dominance** — Label A dominates Label B iff:
  - `A.level >= B.level`, AND
  - `A.compartments ⊇ B.compartments`

Standard lattice model (Bell-LaPadula). Proven, well-understood, maps directly to NATO and government classification systems.

Applied to thinktank instances:
- The thinktank carries a security label
- All entries within it are at or below that label
- All claims, comparisons, and catalog summaries inherit the thinktank's label

## Entities

### Principal

A person or service identity.

- Has a **clearance**: a security label representing their maximum access
- Has **memberships**: set of `(Organization, Group)` pairs
- Groups form a **DAG** (not tree) within each organization — matrix orgs and cross-functional teams make trees insufficient
- Authentication: existing Ed25519 identity + clearance assertion from an identity provider

### Organization

A top-level trust domain (Microsoft, Boeing, French MoD).

- Manages its own group DAG and clearance assignments
- Cross-org trust established through explicit federation agreements
- Each node in the DAG may own zero or more thinktank instances

### ThinkerAgent

A processing entity with its own security label.

```
ThinkerAgent {
    Id:                     "thinker-boeing-secure-01"
    Organization:           "Boeing"
    MaxLevel:               SECRET
    Compartments:           {EXCALIBUR}
    InfrastructureLocation: "Boeing SCF, St. Louis"
    LLMBackend:             "self-hosted, air-gapped Ollama"
}
```

Job routing rule: an agent can only process jobs from thinktanks whose label it dominates.

## Access Rules

| Operation | Requirement |
|---|---|
| **Read entry** | Principal's clearance must dominate the thinktank's label |
| **Write entry** | Principal must be a member of the owning group (or subgroup) AND clearance must dominate thinktank's label |
| **Browse catalog** | Principal's clearance must dominate the thinktank's label |
| **Know thinktank exists** | Principal's clearance must dominate the thinktank's label, OR principal is in the thinktank's visibility set (for executives/officials who may know of its existence without r/w access) |
| **Administer** | Principal must be in the owning group's admin role |

### Access Tiers

For a given thinktank, access is layered:

1. **Existence awareness** — knows the thinktank exists (executives, oversight officials)
2. **Read-only** — can browse and read entries (auditors, adjacent teams)
3. **Read-write** — can contribute entries (project members)
4. **Administrative** — can manage access, configure ThinkerAgents, approve external contributions

Each tier requires the principal's clearance to dominate the thinktank's label. The tier itself is an additional permission granted by the owning group's administrator.

## Inter-Thinktank Information Flow

This is the key design challenge. Classification boundaries exist between thinktank instances, and information flow must respect the lattice.

### Downward Flow (lower → higher classification): ALLOWED

A higher-classified thinktank can **subscribe** to a lower-classified thinktank's catalog. Entries (or their claims) flow upward as reference material.

```
thinktank-boeing-public (PUBLIC)
    ↓ subscribe
thinktank-boeing-defense (CONFIDENTIAL)
    ↓ subscribe
thinktank-boeing-f35 (SECRET)
```

The F-35 thinktank sees Boeing public marketing insights as context. Integration cost is computed considering both local entries and subscribed entries.

Implementation options:
- **Catalog subscription** — the higher thinktank periodically pulls the lower thinktank's catalog summaries and incorporates them as read-only reference entries
- **Claim mirroring** — distilled claims from the lower thinktank are mirrored into the higher one, enabling comparison cascade across the boundary
- **Entry linking** — entries in the higher thinktank can reference entries in lower thinktanks by ID (cross-thinktank references)

### Upward Flow (higher → lower classification): FORBIDDEN

Information must never leak from a higher-classified thinktank to a lower one. No exceptions.

- A SECRET thinktank's entries, claims, comparisons, and catalog summaries must never appear in a CONFIDENTIAL or PUBLIC thinktank
- A principal with SECRET clearance writing in a PUBLIC thinktank must not include SECRET-derived insights (policy enforcement, not technical — see "Classification Spillage" below)
- Subscription links are unidirectional: lower → higher only

### Lateral Flow (cross-org): EXPLICIT AGREEMENTS

The joint venture thinktank `thinktank-excalibur` may subscribe to approved subsets from `thinktank-boeing-defense` and `thinktank-ms-azgov`, but only through:

1. Explicit sharing agreement between the organizations
2. Approval from both organizations' administrators
3. Audit trail recording the agreement, approvers, and scope
4. The joint venture thinktank's label must dominate the source thinktanks' labels

## Claim Flow Within a Thinktank

Since all entries in a thinktank share the same classification, the existing claim pipeline works unchanged:

1. **DISTILL_CLAIMS** — route to any ThinkerAgent cleared for this thinktank's label
2. **Fragment chaining** — `context_claims` flow freely between fragments (same classification)
3. **COMPARE_CLAIMS** — both entries are same-level, no cross-level concerns
4. **Embedding nearest-neighbor** — search space is the entire thinktank (homogeneous classification)

### Cross-Thinktank Claim Flow (via subscriptions)

When a higher-classified thinktank subscribes to a lower one:

- Subscribed claims are available for COMPARE_CLAIMS (the lower claims flowing upward is safe)
- Subscribed claims must NEVER flow back downward via any path
- The ThinkerAgent processing the comparison must be cleared for the *higher* thinktank's label (since the result contains information about the higher thinktank's entries)

## ThinkerAgent Trust Model

Each ThinkerAgent can process jobs from any thinktank whose label it dominates:

| Thinktank level | Agent requirements |
|---|---|
| PUBLIC | Any agent, including cheap cloud LLMs |
| INTERNAL | Organization-owned agent, cloud-hosted acceptable |
| CONFIDENTIAL | Organization-owned, dedicated infrastructure |
| SECRET+ | Self-hosted, air-gapped, within org's secure facility |

Implications:
- A PUBLIC thinktank containing marketing material can use any available cheap LLM — fast, cost-effective
- A TOP_SECRET/EXCALIBUR thinktank must use a dedicated, air-gapped, organizationally-owned agent
- Job queue routing filters by agent clearance: `ClaimNextJobAsync` only returns jobs the agent is cleared for
- Agent **logs** inherit the thinktank's classification level
- An agent cleared for SECRET can also process CONFIDENTIAL, INTERNAL, and PUBLIC jobs (clearance dominance)

## Content Ingestion Gate

Every entry submitted to a thinktank passes through a classification gate:

1. **Submitter clearance check** — principal's clearance must dominate the thinktank's label
2. **Content classification assertion** — the submitter asserts the content is at or below the thinktank's classification level
3. **External contribution review** — if the submitter is not a member of the owning group, the entry enters a review queue. An authorized member must approve before the entry is integrated into the entropy model.

The review workflow must be designed so the reviewer's approval/rejection decision does not leak classified context back to the external submitter. The reviewer sees the entry content; the submitter sees only "approved" or "rejected" (no reason given for rejection, to prevent information flow).

## Audit Requirements

Classified systems require full audit trails:

- **Read audit** — who accessed what entry, when, from which thinktank
- **Write audit** — who submitted what, with what classification assertion
- **Processing audit** — which ThinkerAgent processed which job, for which thinktank
- **Subscription audit** — which thinktanks subscribe to which, approved by whom
- **Access control changes** — who was granted/revoked access, by whom
- **Access failures** — principal attempted to access a thinktank above their clearance
- **Review decisions** — who approved/rejected external contributions

## Key Challenges

### 1. Groups are DAGs, not trees

Matrix organizations, cross-functional teams, and joint ventures create directed acyclic graphs. A Boeing engineer in `Defense/F-35/Avionics` AND `Engineering/Embedded-Systems` occupies two branches that aren't in a parent-child relationship. The ownership model must handle DAG membership, not just tree ancestry.

### 2. Subscription consistency

When a higher thinktank subscribes to a lower one, the subscribed content becomes part of the higher thinktank's knowledge base for comparison purposes. But the lower thinktank evolves independently — entries are added, revised, claims re-distilled. The subscription must handle:
- Incremental sync (new entries in the lower thinktank)
- Revised entries (claims change in the lower thinktank)
- Deleted entries (removed from the lower thinktank)
- Latency (how stale can the subscription be?)

### 3. Existence is classified

Even the thinktank's ID in an API response is classified metadata. The system must handle "I can neither confirm nor deny this thinktank exists" for principals below the thinktank's classification level. This applies to all API endpoints, error messages, and logs.

### 4. LLM context contamination

For stateless inference (Ollama with no persistent state), processing a SECRET job followed by a PUBLIC job on the same instance is likely safe. But model caches, KV-cache persistence, fine-tuning, and request logs all create potential contamination vectors that must be addressed operationally. In practice, agents cleared for higher levels should run on dedicated infrastructure.

### 5. Cross-org identity federation

Boeing's identity provider asserts a user has SECRET clearance. Does the French MoD trust that assertion? The joint venture thinktank needs clearance assertions from multiple organizations. Federated identity with cross-org trust policies is a subsystem unto itself — likely requiring a shared certificate authority or mutual trust agreements.

## Open Questions

1. **Subscription granularity** — Does a higher thinktank subscribe to the *entire* lower thinktank, or can it subscribe to specific topics/clusters? Topic-level subscription would reduce noise but adds complexity.

2. **Classification spillage** — A user with SECRET clearance writes in a PUBLIC thinktank using SECRET-derived insights. The system can't prevent this technically. Needs policy + audit + "flag for classification review" workflow support.

3. **Thinktank splitting** — A project grows and needs sub-compartments. A single SECRET thinktank needs to become two: SECRET/{AVIONICS} and SECRET/{PROPULSION}. How are existing entries partitioned? Do comparisons across the boundary get deleted?

4. **Thinktank merging** — Two compartmented thinktanks merge (project scope change). The merged thinktank's label is the JOIN of both. All cross-thinktank comparisons become local comparisons. Recomputation needed?

5. **Subscription and entropy** — When a higher thinktank subscribes to a lower one, do the subscribed entries contribute to the higher thinktank's integration cost? If yes, changes in the lower thinktank ripple upward through the entropy model. If no, comparisons against subscribed content are "free" — which distorts the integration cost signal.

6. **Offline/air-gapped sync** — A TOP_SECRET air-gapped thinktank can't subscribe to a CONFIDENTIAL thinktank via network. Needs a secure, audited data diode or manual transfer process. How does the subscription model accommodate this?
