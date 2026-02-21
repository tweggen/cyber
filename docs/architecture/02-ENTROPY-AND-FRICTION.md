# 02 — Entropy and Friction

## Concept

When the notebook absorbs new information, two questions matter:
1. **How much is genuinely new?** (Entropy)
2. **How much contradicts what we already know?** (Friction)

These are computed by comparing claim-sets between entries. They're semantic measures — they require understanding language, not just pattern matching — but they don't require deep reasoning. A cheap LLM (Haiku-class) can detect that two claims contradict without needing to resolve the contradiction.

This mirrors how the brain handles incoming information: most perception is cheap filtering. Only when something contradicts expectations (prediction error) does expensive conscious processing engage.

## Definitions

### Entropy (novelty)

Given two claim-sets A and B, entropy measures how much new ground B covers relative to A.

For each claim in B, compare against all claims in A:
- **Different ground** (no semantic overlap): contributes entropy. This claim is about something A doesn't address.
- **Same ground, same conclusion** (semantically equivalent): contributes zero entropy. This is redundant information.
- **Same ground, different conclusion** (contradiction): contributes zero entropy for novelty purposes, but contributes friction (see below). The topic is not new — only the conclusion differs.

**Entropy score** = (number of B's claims that cover different ground) / N

Range: [0.0, 1.0]
- 0.0 = B adds nothing new relative to A
- 1.0 = B is entirely about topics A doesn't cover

### Friction (contradiction)

Given two claim-sets A and B, friction measures how much B contradicts A.

For each claim in B, compare against all claims in A:
- **Contradicts** a claim in A: contributes friction.
- **Aligns with** or **is orthogonal to** all claims in A: contributes zero friction.

**Friction score** = (number of B's claims that contradict at least one claim in A) / N

Range: [0.0, 1.0]
- 0.0 = no contradictions
- 1.0 = every claim in B contradicts something in A

**Critical property:** Alignment cannot cancel out contradiction. If 3 of 12 claims contradict and 9 align, friction = 0.25. The 9 aligning claims don't reduce friction. This is intentional — contradictions require attention regardless of how much else agrees.

## The Four Quadrants

| | Low Friction | High Friction |
|---|---|---|
| **High Entropy** | New knowledge, no conflicts. File it. | New knowledge that also contradicts existing knowledge. Most disruptive — highest priority for review. |
| **Low Entropy** | Redundant information. Candidate for deduplication or skipping. | Contradictory information about the same topics. Needs resolution. Flag for expensive LLM. |

### Processing implications

| Quadrant | Action | Processor |
|----------|--------|-----------|
| High entropy, low friction | Store, update topic index | Server (automatic) |
| Low entropy, low friction | Store, mark as redundant, consider dedup | Server (automatic) |
| Low entropy, high friction | Flag for contradiction resolution | Agent (Sonnet/Opus) |
| High entropy, high friction | Flag as high-priority review | Agent (Opus) |

## Comparison Mechanics

### What gets compared

When a new entry's claims are distilled, the server needs to compute entropy and friction against *something*. Not against every existing entry (that's O(N) and grows linearly). Instead:

**Compare against the relevant topic index's claims.** The topic index entry is itself a claim-set that summarizes the known state of that topic. Comparing new claims against the topic index gives a good approximation of novelty and contradiction for that domain.

If the entry doesn't clearly belong to a topic (high entropy against all topic indices), compare against the 3-5 nearest topic indices by keyword overlap to find the best fit.

### Comparison job format

```
ComparisonJob {
  entry_id: UUID              // The new entry
  compare_against: UUID       // The topic index entry (or other entry)
  claims_a: Claim[]           // Claims from compare_against
  claims_b: Claim[]           // Claims from entry
}
```

### Comparison result format

```
ComparisonResult {
  entry_id: UUID
  compared_against: UUID
  entropy: float              // 0.0 - 1.0
  friction: float             // 0.0 - 1.0
  contradictions: [            // Detail for high-friction pairs
    {
      claim_a: string,         // The existing claim
      claim_b: string,         // The new contradicting claim
      severity: float          // How directly they contradict (0.0 - 1.0)
    }
  ]
  timestamp: datetime
}
```

### The comparison prompt (for robot workers)

The robot sends a prompt to a Haiku-class model like:

```
You are comparing two sets of claims to measure novelty and contradiction.

EXISTING CLAIMS (from the topic index):
1. [claim text]
2. [claim text]
...

NEW CLAIMS (from the incoming entry):
1. [claim text]
2. [claim text]
...

For each new claim, classify it as one of:
- NOVEL: covers ground that no existing claim addresses
- REDUNDANT: semantically equivalent to an existing claim (cite which one)
- CONTRADICTS: contradicts an existing claim (cite which one)

Respond as JSON:
{
  "classifications": [
    { "new_claim": 1, "type": "NOVEL" },
    { "new_claim": 2, "type": "REDUNDANT", "matches": 4 },
    { "new_claim": 3, "type": "CONTRADICTS", "conflicts_with": 7, "severity": 0.8 }
  ]
}
```

This gives both the scores (count NOVEL/N = entropy, count CONTRADICTS/N = friction) and the detail needed for contradiction resolution by an expensive agent later.

## Entropy and Friction on the Index

### Topic index claims

Each topic index entry has its own claim-set, summarizing the known state of that topic. When a new entry is added to a topic and compared, the topic index's claims may need updating:

- If entropy is high: the topic index is missing something. Queue a re-distillation of the topic index.
- If friction is high: the topic index may contain outdated claims. Flag for agent review.

### Aggregate metrics

The server can track aggregate entropy and friction per topic:
- **Topic entropy trend:** Is this topic still accumulating novel information, or is it stabilizing?
- **Topic friction count:** How many unresolved contradictions exist in this topic?

These metrics help agents and humans prioritize where to spend attention.

## Context for Comparison

### The context problem

A claim like "deployments go to staging first" means different things in different contexts. Without knowing which system or team the claim is about, comparison produces false contradictions.

### Context propagation

Claims inherit context from their source:
- Fragment claims inherit context from the artifact's claims
- Artifact claims inherit context from their topic classification
- The distillation prompt should include the topic name and parent artifact summary to ground the claims

This doesn't eliminate all ambiguity, but it reduces false friction from context mismatch.

## Open Design Questions

1. **Asymmetry of comparison.** Entropy(A→B) ≠ Entropy(B→A). Should we compute both directions? For new entries being ingested, comparing new-against-existing is sufficient. For index maintenance, bidirectional comparison might reveal that the index is missing coverage.

2. **Friction decay.** Should friction scores decay over time if not resolved? Old contradictions that nobody has addressed might be less relevant than fresh ones. Or they might be more important because they've been ignored. This is a policy question.

3. **Transitivity.** If A contradicts B and B contradicts C, does A align with C? Not necessarily (contradiction isn't transitive). But the system should be able to surface these chains for agent review.

4. **Threshold tuning.** What friction threshold triggers expensive review? Starting suggestion: friction > 0.2 (more than ~2 out of 12 claims contradict). This should be tunable per notebook and per topic.
