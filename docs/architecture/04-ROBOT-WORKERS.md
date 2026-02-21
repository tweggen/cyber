# 04 — Robot Workers

## Concept

Robots are stateless, cheap-LLM workers that pull jobs from the notebook server's job queue, execute them against a Haiku-class model, and push results back. They don't understand the notebook's structure, history, or purpose. They just process claims.

Think of them as the thalamic filter — fast, cheap perception that handles the 90% of work that doesn't require conscious attention.

## Properties

- **Stateless.** A robot has no memory between jobs. It receives everything it needs in the job payload.
- **Parallelizable.** Run 1 robot or 20. They each claim different jobs. No coordination needed.
- **Replaceable.** Swap Haiku for a different cheap model. The robot script changes; the server and agents don't.
- **Observable.** Each robot identifies itself (`worker_id`). The server tracks which robot did what work.

## Robot Implementation

A robot is a simple Python script running in a loop:

```python
# Pseudocode — actual implementation will vary
import anthropic
import requests

SERVER = "http://localhost:3000"
NOTEBOOK_ID = "2f00ed6c-4fa0-475d-a762-f29309ec2304"
WORKER_ID = "robot-haiku-1"
MODEL = "claude-haiku-4-5-20251001"

client = anthropic.Anthropic()

while True:
    # Pull next job
    job = requests.get(
        f"{SERVER}/notebooks/{NOTEBOOK_ID}/jobs/next",
        params={"worker_id": WORKER_ID}
    ).json()

    if job is None:
        sleep(5)  # No work available
        continue

    try:
        if job["type"] == "DISTILL_CLAIMS":
            result = distill_claims(job["payload"], client, MODEL)
        elif job["type"] == "COMPARE_CLAIMS":
            result = compare_claims(job["payload"], client, MODEL)
        elif job["type"] == "CLASSIFY_TOPIC":
            result = classify_topic(job["payload"], client, MODEL)

        # Push result
        requests.post(
            f"{SERVER}/notebooks/{NOTEBOOK_ID}/jobs/{job['id']}/complete",
            json={"worker_id": WORKER_ID, "result": result}
        )
    except Exception as e:
        requests.post(
            f"{SERVER}/notebooks/{NOTEBOOK_ID}/jobs/{job['id']}/fail",
            json={"worker_id": WORKER_ID, "error": str(e)}
        )
```

## Job Type: DISTILL_CLAIMS

### Input
```json
{
  "entry_id": "uuid",
  "content": "The full text of the entry...",
  "context_claims": [                    // Optional: parent artifact or topic claims
    { "text": "claim text", "confidence": 0.9 }
  ],
  "max_claims": 12
}
```

### Prompt template

```
You are distilling a document into its top {max_claims} factual claims.

{if context_claims}
CONTEXT — this document is part of a larger collection about:
{for claim in context_claims}
- {claim.text}
{endfor}

Focus on claims that ADD to this context, not repeat it.
{endif}

DOCUMENT:
{content}

Extract the top {max_claims} most important factual claims from this document.
Each claim should be:
- A single declarative sentence
- Self-contained (understandable without the document)
- Specific (not vague or generic)
- Non-redundant with other claims in your list

Order by importance (most central claim first).

Respond as JSON:
{
  "claims": [
    { "text": "...", "confidence": 0.95 },
    ...
  ]
}
```

### Output
```json
{
  "claims": [
    { "text": "OAuth tokens are validated against the provider before each rclone backup job starts", "confidence": 0.95 },
    { "text": "Token validation uses the provider-specific refresh endpoint", "confidence": 0.88 },
    ...
  ]
}
```

## Job Type: COMPARE_CLAIMS

### Input
```json
{
  "entry_id": "uuid",
  "compare_against_id": "uuid",
  "claims_a": [
    { "text": "existing claim 1", "confidence": 0.9 },
    ...
  ],
  "claims_b": [
    { "text": "new claim 1", "confidence": 0.9 },
    ...
  ]
}
```

### Prompt template

```
You are comparing two sets of claims to measure novelty and contradiction.

EXISTING CLAIMS:
{for i, claim in enumerate(claims_a)}
A{i+1}. {claim.text}
{endfor}

NEW CLAIMS:
{for i, claim in enumerate(claims_b)}
B{i+1}. {claim.text}
{endfor}

For each NEW claim (B1, B2, ...), classify it as:
- NOVEL: covers a topic that no existing claim addresses
- REDUNDANT: semantically equivalent to an existing claim
- CONTRADICTS: makes a statement that conflicts with an existing claim

Be precise about contradiction vs. mere difference:
- "The earth is not a sphere" vs "The earth is not a plane" = CONTRADICTS (opposite conclusions about earth's shape)
- "The earth is round" vs "Mars is red" = NOVEL (different topics entirely)
- "The earth is round" vs "The earth is approximately spherical" = REDUNDANT (same claim, different words)

Respond as JSON:
{
  "classifications": [
    { "new_claim": 1, "type": "NOVEL" },
    { "new_claim": 2, "type": "REDUNDANT", "matches_existing": 4 },
    { "new_claim": 3, "type": "CONTRADICTS", "conflicts_with": 7, "severity": 0.8 }
  ]
}
```

### Output processing

The robot computes:
- `entropy` = count(NOVEL) / len(claims_b)
- `friction` = count(CONTRADICTS) / len(claims_b)
- `contradictions` = list of {claim_a, claim_b, severity} for CONTRADICTS items

```json
{
  "entropy": 0.58,
  "friction": 0.17,
  "contradictions": [
    {
      "claim_a": "Deployments go to staging first",
      "claim_b": "Production deployments skip staging",
      "severity": 0.9
    }
  ]
}
```

## Job Type: CLASSIFY_TOPIC

### Input
```json
{
  "entry_id": "uuid",
  "claims": [
    { "text": "claim text", "confidence": 0.9 },
    ...
  ],
  "available_topics": [
    "karawan", "backer", "geoffrey", "aihao", "notebook-meta", "collaboration"
  ]
}
```

### Prompt template

```
Given these claims from a document:
{for claim in claims}
- {claim.text}
{endfor}

Which of these topics does this document best belong to?
{for topic in available_topics}
- {topic}
{endfor}

If none fit well, suggest a new topic name.

Respond as JSON:
{
  "primary_topic": "backer",
  "secondary_topics": ["karawan"],    // if it spans topics
  "new_topic": null                   // or "new-topic-name" if suggesting
}
```

## Scaling

### Single machine
One robot script handles most workloads. At 14,000 pages, distillation takes ~14,000 Haiku calls. At ~0.5 seconds per call, that's ~2 hours sequential. Running 4 robot instances in parallel: ~30 minutes.

### Cost estimate for 14,000 pages
- Distillation: 14,000 calls × ~500 input tokens + ~300 output tokens ≈ 11.2M tokens → ~$3 at Haiku rates
- Comparison: 14,000 calls × ~800 tokens ≈ 11.2M tokens → ~$3
- Classification: 14,000 calls × ~400 tokens ≈ 5.6M tokens → ~$1.50
- **Total: ~$7.50** (vs. >$1,000 routing everything through Opus)

### Monitoring

The server's job queue exposes:
- Queue depth per job type
- Jobs in progress
- Jobs completed / failed (with rates)
- Average processing time per job type

A simple dashboard or CLI command can show ingest progress.

## Error Handling

- **Timeout:** If a robot claims a job but doesn't complete within the timeout (default: 60 seconds), the job returns to pending for another robot to pick up.
- **Failure:** If a robot reports failure, the job is marked failed with the error message. A retry policy (e.g., retry up to 3 times) can be configured per job type.
- **Bad output:** If the LLM returns unparseable JSON or missing fields, the robot should retry once with a "please respond as valid JSON" nudge, then fail if still broken.
- **Rate limiting:** Robots should respect Anthropic API rate limits. A simple backoff strategy (exponential with jitter) handles this.
