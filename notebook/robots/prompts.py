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
