# Step 5: Robot Workers

**Depends on:** Steps 2 (Batch Write & Claims API) and 3 (Job Queue)

## Goal

Implement Python robot worker scripts that pull jobs from the server, process them with a cheap LLM (Haiku), and push results back. These are stateless, parallelizable workers.

> **Note:** Robot workers are language-agnostic HTTP clients — they work identically regardless of whether the server is Rust or C# ASP.NET Core. The API contract is the same.

## 5.1 — Directory Structure

Create `notebook/robots/` alongside the existing `notebook/python/`:

```
notebook/robots/
    robot.py              # Main worker script
    prompts.py            # LLM prompt templates
    requirements.txt      # Dependencies
    README.md             # Usage instructions
```

## 5.2 — Dependencies

`notebook/robots/requirements.txt`:

```
anthropic>=0.40.0
requests>=2.31.0
```

## 5.3 — Main Worker Script

`notebook/robots/robot.py`:

```python
#!/usr/bin/env python3
"""Stateless robot worker for notebook claim processing.

Pulls jobs from the notebook server's job queue, processes them
with a cheap LLM (Haiku-class), and pushes results back.

Usage:
    python robot.py --server http://localhost:5000 \
                    --notebook <uuid> \
                    --worker-id robot-haiku-1 \
                    --token <jwt-token> \
                    [--job-type DISTILL_CLAIMS] \
                    [--model claude-haiku-4-5-20251001] \
                    [--poll-interval 5]
"""

import argparse
import json
import logging
import sys
import time
from typing import Optional

import anthropic
import requests

from prompts import (
    build_distill_prompt,
    build_compare_prompt,
    build_classify_prompt,
    parse_distill_result,
    parse_compare_result,
    parse_classify_result,
)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s [%(name)s] %(message)s",
)
logger = logging.getLogger("robot")


def pull_job(
    session: requests.Session,
    server: str,
    notebook_id: str,
    worker_id: str,
    job_type: Optional[str] = None,
) -> Optional[dict]:
    """Pull next available job from the server."""
    params = {"worker_id": worker_id}
    if job_type:
        params["type"] = job_type

    resp = session.get(
        f"{server}/notebooks/{notebook_id}/jobs/next",
        params=params,
    )

    if resp.status_code == 200:
        data = resp.json()
        if data is None or data == {}:
            return None
        return data
    elif resp.status_code == 204:
        return None
    else:
        logger.warning("Failed to pull job: %s %s", resp.status_code, resp.text)
        return None


def complete_job(
    session: requests.Session,
    server: str,
    notebook_id: str,
    job_id: str,
    worker_id: str,
    result: dict,
) -> bool:
    """Submit completed job result to the server."""
    resp = session.post(
        f"{server}/notebooks/{notebook_id}/jobs/{job_id}/complete",
        json={"worker_id": worker_id, "result": result},
    )
    if resp.status_code == 200:
        return True
    else:
        logger.error("Failed to complete job %s: %s %s", job_id, resp.status_code, resp.text)
        return False


def fail_job(
    session: requests.Session,
    server: str,
    notebook_id: str,
    job_id: str,
    worker_id: str,
    error: str,
) -> bool:
    """Report job failure to the server."""
    resp = session.post(
        f"{server}/notebooks/{notebook_id}/jobs/{job_id}/fail",
        json={"worker_id": worker_id, "error": error},
    )
    return resp.status_code == 200


def process_job(client: anthropic.Anthropic, model: str, job: dict) -> dict:
    """Process a job by calling the LLM and parsing the response."""
    job_type = job["job_type"]
    payload = job["payload"]

    if job_type == "DISTILL_CLAIMS":
        prompt = build_distill_prompt(payload)
        response = call_llm(client, model, prompt)
        return parse_distill_result(response)

    elif job_type == "COMPARE_CLAIMS":
        prompt = build_compare_prompt(payload)
        response = call_llm(client, model, prompt)
        return parse_compare_result(response, payload)

    elif job_type == "CLASSIFY_TOPIC":
        prompt = build_classify_prompt(payload)
        response = call_llm(client, model, prompt)
        return parse_classify_result(response)

    else:
        raise ValueError(f"Unknown job type: {job_type}")


def call_llm(client: anthropic.Anthropic, model: str, prompt: str) -> str:
    """Call the LLM and return the text response."""
    message = client.messages.create(
        model=model,
        max_tokens=2048,
        messages=[{"role": "user", "content": prompt}],
    )
    return message.content[0].text


def run_worker(args: argparse.Namespace):
    """Main worker loop."""
    client = anthropic.Anthropic()
    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {args.token}"
    session.headers["Content-Type"] = "application/json"

    logger.info(
        "Starting robot worker: id=%s model=%s server=%s notebook=%s",
        args.worker_id, args.model, args.server, args.notebook,
    )

    jobs_completed = 0
    jobs_failed = 0
    consecutive_empty = 0

    while True:
        try:
            job = pull_job(
                session, args.server, args.notebook,
                args.worker_id, args.job_type,
            )

            if job is None:
                consecutive_empty += 1
                if consecutive_empty % 12 == 1:  # Log every minute at 5s interval
                    logger.debug("No jobs available, waiting...")
                time.sleep(args.poll_interval)
                continue

            consecutive_empty = 0
            job_id = job["id"]
            job_type = job["job_type"]
            logger.info("Processing job %s (type=%s)", job_id, job_type)

            try:
                result = process_job(client, args.model, job)
                if complete_job(
                    session, args.server, args.notebook,
                    job_id, args.worker_id, result,
                ):
                    jobs_completed += 1
                    logger.info(
                        "Job %s completed (total: %d completed, %d failed)",
                        job_id, jobs_completed, jobs_failed,
                    )
                else:
                    jobs_failed += 1

            except Exception as e:
                logger.error("Job %s failed: %s", job_id, e)
                fail_job(
                    session, args.server, args.notebook,
                    job_id, args.worker_id, str(e),
                )
                jobs_failed += 1

        except KeyboardInterrupt:
            logger.info(
                "Shutting down. Completed: %d, Failed: %d",
                jobs_completed, jobs_failed,
            )
            break
        except Exception as e:
            logger.error("Unexpected error: %s", e)
            time.sleep(args.poll_interval)


def main():
    parser = argparse.ArgumentParser(description="Notebook robot worker")
    parser.add_argument("--server", required=True, help="Notebook server URL")
    parser.add_argument("--notebook", required=True, help="Notebook UUID")
    parser.add_argument("--worker-id", required=True, help="Worker identifier")
    parser.add_argument("--token", required=True, help="JWT Bearer token")
    parser.add_argument(
        "--job-type", default=None,
        choices=["DISTILL_CLAIMS", "COMPARE_CLAIMS", "CLASSIFY_TOPIC"],
        help="Only process this job type (default: all)",
    )
    parser.add_argument(
        "--model", default="claude-haiku-4-5-20251001",
        help="Anthropic model to use",
    )
    parser.add_argument(
        "--poll-interval", type=float, default=5.0,
        help="Seconds between poll attempts when no jobs available",
    )
    args = parser.parse_args()
    run_worker(args)


if __name__ == "__main__":
    main()
```

## 5.4 — Prompt Templates

`notebook/robots/prompts.py`:

```python
"""LLM prompt templates for robot worker job types."""

import json
from typing import Any


def build_distill_prompt(payload: dict) -> str:
    """Build the claim distillation prompt."""
    content = payload["content"]
    max_claims = payload.get("max_claims", 12)
    context_claims = payload.get("context_claims")

    context_section = ""
    if context_claims:
        claims_text = "\n".join(f"- {c['text']}" for c in context_claims)
        context_section = f"""
CONTEXT — this document is part of a larger collection about:
{claims_text}

Focus on claims that ADD to this context, not repeat it.
"""

    return f"""You are distilling a document into its top {max_claims} factual claims.
{context_section}
DOCUMENT:
{content}

Extract the top {max_claims} most important factual claims from this document.
Each claim should be:
- A single declarative sentence
- Self-contained (understandable without the document)
- Specific (not vague or generic)
- Non-redundant with other claims in your list

Order by importance (most central claim first).

Respond as JSON only, no other text:
{{
  "claims": [
    {{ "text": "...", "confidence": 0.95 }},
    ...
  ]
}}"""


def build_compare_prompt(payload: dict) -> str:
    """Build the claim comparison prompt."""
    claims_a = payload["claims_a"]
    claims_b = payload["claims_b"]

    existing = "\n".join(
        f"A{i+1}. {c['text']}" for i, c in enumerate(claims_a)
    )
    new = "\n".join(
        f"B{i+1}. {c['text']}" for i, c in enumerate(claims_b)
    )

    return f"""You are comparing two sets of claims to measure novelty and contradiction.

EXISTING CLAIMS:
{existing}

NEW CLAIMS:
{new}

For each NEW claim (B1, B2, ...), classify it as:
- NOVEL: covers a topic that no existing claim addresses
- REDUNDANT: semantically equivalent to an existing claim
- CONTRADICTS: makes a statement that conflicts with an existing claim

Be precise about contradiction vs. mere difference:
- "The earth is flat" vs "The earth is round" = CONTRADICTS (opposite conclusions)
- "The earth is round" vs "Mars is red" = NOVEL (different topics)
- "The earth is round" vs "The earth is approximately spherical" = REDUNDANT

Respond as JSON only, no other text:
{{
  "classifications": [
    {{ "new_claim": 1, "type": "NOVEL" }},
    {{ "new_claim": 2, "type": "REDUNDANT", "matches_existing": 4 }},
    {{ "new_claim": 3, "type": "CONTRADICTS", "conflicts_with": 7, "severity": 0.8 }}
  ]
}}"""


def build_classify_prompt(payload: dict) -> str:
    """Build the topic classification prompt."""
    claims = payload["claims"]
    available_topics = payload.get("available_topics", [])

    claims_text = "\n".join(f"- {c['text']}" for c in claims)
    topics_text = "\n".join(f"- {t}" for t in available_topics)

    return f"""Given these claims from a document:
{claims_text}

Which of these topics does this document best belong to?
{topics_text}

If none fit well, suggest a new topic name.

Respond as JSON only, no other text:
{{
  "primary_topic": "topic-name",
  "secondary_topics": [],
  "new_topic": null
}}"""


def parse_distill_result(response: str) -> dict:
    """Parse distillation LLM response into structured result."""
    try:
        data = _extract_json(response)
        claims = data.get("claims", [])
        # Validate structure
        for claim in claims:
            assert isinstance(claim.get("text"), str), "claim missing text"
            assert isinstance(claim.get("confidence"), (int, float)), "claim missing confidence"
        return {"claims": claims}
    except (json.JSONDecodeError, AssertionError, KeyError) as e:
        raise ValueError(f"Failed to parse distill result: {e}\nResponse: {response[:500]}")


def parse_compare_result(response: str, payload: dict) -> dict:
    """Parse comparison LLM response into entropy/friction scores."""
    try:
        data = _extract_json(response)
        classifications = data.get("classifications", [])

        claims_b = payload["claims_b"]
        claims_a = payload["claims_a"]
        n = len(claims_b)

        novel_count = sum(1 for c in classifications if c.get("type") == "NOVEL")
        contradict_count = sum(1 for c in classifications if c.get("type") == "CONTRADICTS")

        entropy = novel_count / n if n > 0 else 0.0
        friction = contradict_count / n if n > 0 else 0.0

        contradictions = []
        for c in classifications:
            if c.get("type") == "CONTRADICTS":
                new_idx = c.get("new_claim", 1) - 1
                existing_idx = c.get("conflicts_with", 1) - 1
                contradictions.append({
                    "claim_a": claims_a[existing_idx]["text"] if existing_idx < len(claims_a) else "?",
                    "claim_b": claims_b[new_idx]["text"] if new_idx < len(claims_b) else "?",
                    "severity": c.get("severity", 0.5),
                })

        return {
            "entropy": round(entropy, 4),
            "friction": round(friction, 4),
            "contradictions": contradictions,
        }
    except (json.JSONDecodeError, KeyError, IndexError) as e:
        raise ValueError(f"Failed to parse compare result: {e}\nResponse: {response[:500]}")


def parse_classify_result(response: str) -> dict:
    """Parse classification LLM response."""
    try:
        data = _extract_json(response)
        return {
            "primary_topic": data.get("primary_topic", ""),
            "secondary_topics": data.get("secondary_topics", []),
            "new_topic": data.get("new_topic"),
        }
    except (json.JSONDecodeError, KeyError) as e:
        raise ValueError(f"Failed to parse classify result: {e}\nResponse: {response[:500]}")


def _extract_json(text: str) -> dict:
    """Extract JSON from LLM response, handling markdown code blocks."""
    text = text.strip()
    # Strip markdown code block if present
    if text.startswith("```"):
        lines = text.split("\n")
        # Remove first line (```json or ```) and last line (```)
        lines = [l for l in lines if not l.strip().startswith("```")]
        text = "\n".join(lines)
    return json.loads(text)
```

## 5.5 — Running Workers

### Single worker

```bash
cd notebook/robots
pip install -r requirements.txt

python robot.py \
  --server http://localhost:5000 \
  --notebook 2f00ed6c-4fa0-475d-a762-f29309ec2304 \
  --worker-id robot-haiku-1 \
  --token "$JWT_TOKEN" \
  --model claude-haiku-4-5-20251001
```

### Parallel workers (4 instances)

```bash
for i in 1 2 3 4; do
  python robot.py \
    --server http://localhost:5000 \
    --notebook 2f00ed6c-4fa0-475d-a762-f29309ec2304 \
    --worker-id "robot-haiku-$i" \
    --token "$JWT_TOKEN" &
done
wait
```

### Specialized workers

```bash
# One worker for distillation only
python robot.py --job-type DISTILL_CLAIMS ...

# One worker for comparison only
python robot.py --job-type COMPARE_CLAIMS ...
```

## 5.6 — Tests

Create `notebook/robots/test_prompts.py`:

```python
"""Tests for prompt templates and result parsing."""

import json

import pytest
from prompts import (
    build_distill_prompt,
    build_compare_prompt,
    build_classify_prompt,
    parse_distill_result,
    parse_compare_result,
    parse_classify_result,
    _extract_json,
)


def test_build_distill_prompt_basic():
    payload = {"content": "OAuth tokens are validated.", "max_claims": 6}
    prompt = build_distill_prompt(payload)
    assert "OAuth tokens" in prompt
    assert "top 6" in prompt
    assert "JSON" in prompt


def test_build_distill_prompt_with_context():
    payload = {
        "content": "Details about validation.",
        "max_claims": 12,
        "context_claims": [{"text": "System uses OAuth", "confidence": 0.9}],
    }
    prompt = build_distill_prompt(payload)
    assert "System uses OAuth" in prompt
    assert "CONTEXT" in prompt


def test_build_compare_prompt():
    payload = {
        "claims_a": [{"text": "Earth is round", "confidence": 0.9}],
        "claims_b": [{"text": "Earth is flat", "confidence": 0.8}],
    }
    prompt = build_compare_prompt(payload)
    assert "A1. Earth is round" in prompt
    assert "B1. Earth is flat" in prompt


def test_parse_distill_result():
    response = '{"claims": [{"text": "Test claim", "confidence": 0.9}]}'
    result = parse_distill_result(response)
    assert len(result["claims"]) == 1
    assert result["claims"][0]["text"] == "Test claim"


def test_parse_distill_result_code_block():
    response = '```json\n{"claims": [{"text": "Test", "confidence": 0.9}]}\n```'
    result = parse_distill_result(response)
    assert len(result["claims"]) == 1


def test_parse_compare_result():
    response = '{"classifications": [{"new_claim": 1, "type": "NOVEL"}]}'
    payload = {
        "claims_a": [{"text": "A", "confidence": 0.9}],
        "claims_b": [{"text": "B", "confidence": 0.9}],
    }
    result = parse_compare_result(response, payload)
    assert result["entropy"] == 1.0
    assert result["friction"] == 0.0


def test_parse_compare_result_with_contradiction():
    response = json.dumps({
        "classifications": [
            {"new_claim": 1, "type": "CONTRADICTS", "conflicts_with": 1, "severity": 0.9}
        ]
    })
    payload = {
        "claims_a": [{"text": "Earth is round", "confidence": 0.9}],
        "claims_b": [{"text": "Earth is flat", "confidence": 0.8}],
    }
    result = parse_compare_result(response, payload)
    assert result["entropy"] == 0.0
    assert result["friction"] == 1.0
    assert len(result["contradictions"]) == 1


def test_parse_classify_result():
    response = '{"primary_topic": "devops", "secondary_topics": [], "new_topic": null}'
    result = parse_classify_result(response)
    assert result["primary_topic"] == "devops"


def test_extract_json_plain():
    assert _extract_json('{"key": "value"}') == {"key": "value"}


def test_extract_json_code_block():
    text = '```json\n{"key": "value"}\n```'
    assert _extract_json(text) == {"key": "value"}
```

### Run tests

```bash
cd notebook/robots
pip install -r requirements.txt
pip install pytest
pytest test_prompts.py -v
```

## Verify

The robot worker is verified by the end-to-end test:

1. Start notebook server (`dotnet run --project src/Notebook.Server`)
2. Write an entry (creates DISTILL_CLAIMS job)
3. Start robot worker
4. Robot pulls job, calls Haiku, pushes result
5. Verify entry now has claims (READ entry, check claims field)
6. Verify COMPARE_CLAIMS jobs were created (check job stats)
7. Robot picks up comparison job, processes it
8. Verify entry has comparison results (READ entry, check comparisons field)
