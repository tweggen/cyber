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


def test_build_classify_prompt():
    payload = {
        "claims": [{"text": "System architecture", "confidence": 0.9}],
        "available_topics": ["devops", "security", "architecture"],
    }
    prompt = build_classify_prompt(payload)
    assert "System architecture" in prompt
    assert "devops" in prompt
    assert "architecture" in prompt


def test_parse_distill_result():
    response = '{"claims": [{"text": "Test claim", "confidence": 0.9}]}'
    result = parse_distill_result(response)
    assert len(result["claims"]) == 1
    assert result["claims"][0]["text"] == "Test claim"


def test_parse_distill_result_code_block():
    response = '```json\n{"claims": [{"text": "Test", "confidence": 0.9}]}\n```'
    result = parse_distill_result(response)
    assert len(result["claims"]) == 1
    assert result["claims"][0]["text"] == "Test"


def test_parse_distill_result_multiple_claims():
    response = json.dumps({
        "claims": [
            {"text": "First claim", "confidence": 0.95},
            {"text": "Second claim", "confidence": 0.88},
            {"text": "Third claim", "confidence": 0.72},
        ]
    })
    result = parse_distill_result(response)
    assert len(result["claims"]) == 3
    assert result["claims"][0]["confidence"] == 0.95


def test_parse_distill_result_invalid_json():
    response = 'not valid json'
    with pytest.raises(ValueError, match="Failed to parse distill result"):
        parse_distill_result(response)


def test_parse_compare_result():
    response = '{"classifications": [{"new_claim": 1, "type": "NOVEL"}]}'
    payload = {
        "claims_a": [{"text": "A", "confidence": 0.9}],
        "claims_b": [{"text": "B", "confidence": 0.9}],
    }
    result = parse_compare_result(response, payload)
    assert result["entropy"] == 1.0
    assert result["friction"] == 0.0
    assert len(result["contradictions"]) == 0


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
    assert result["contradictions"][0]["claim_a"] == "Earth is round"
    assert result["contradictions"][0]["claim_b"] == "Earth is flat"
    assert result["contradictions"][0]["severity"] == 0.9


def test_parse_compare_result_mixed():
    response = json.dumps({
        "classifications": [
            {"new_claim": 1, "type": "NOVEL"},
            {"new_claim": 2, "type": "REDUNDANT", "matches_existing": 1},
            {"new_claim": 3, "type": "CONTRADICTS", "conflicts_with": 2, "severity": 0.7},
        ]
    })
    payload = {
        "claims_a": [
            {"text": "System is secure", "confidence": 0.9},
            {"text": "OAuth is used", "confidence": 0.85},
        ],
        "claims_b": [
            {"text": "New feature added", "confidence": 0.9},
            {"text": "OAuth is used", "confidence": 0.88},
            {"text": "System is insecure", "confidence": 0.75},
        ],
    }
    result = parse_compare_result(response, payload)
    assert result["entropy"] == round(1/3, 4)  # 1 novel out of 3
    assert result["friction"] == round(1/3, 4)  # 1 contradiction out of 3
    assert len(result["contradictions"]) == 1


def test_parse_classify_result():
    response = '{"primary_topic": "devops", "secondary_topics": [], "new_topic": null}'
    result = parse_classify_result(response)
    assert result["primary_topic"] == "devops"
    assert result["secondary_topics"] == []
    assert result["new_topic"] is None


def test_parse_classify_result_with_secondary():
    response = json.dumps({
        "primary_topic": "architecture",
        "secondary_topics": ["security", "devops"],
        "new_topic": None
    })
    result = parse_classify_result(response)
    assert result["primary_topic"] == "architecture"
    assert len(result["secondary_topics"]) == 2
    assert "security" in result["secondary_topics"]


def test_parse_classify_result_with_new_topic():
    response = json.dumps({
        "primary_topic": None,
        "secondary_topics": [],
        "new_topic": "machine-learning"
    })
    result = parse_classify_result(response)
    assert result["new_topic"] == "machine-learning"


def test_extract_json_plain():
    assert _extract_json('{"key": "value"}') == {"key": "value"}


def test_extract_json_code_block():
    text = '```json\n{"key": "value"}\n```'
    assert _extract_json(text) == {"key": "value"}


def test_extract_json_code_block_no_lang():
    text = '```\n{"key": "value"}\n```'
    assert _extract_json(text) == {"key": "value"}


def test_extract_json_with_whitespace():
    text = '  ```json\n  {"key": "value"}\n  ```  '
    assert _extract_json(text) == {"key": "value"}


def test_extract_json_complex_object():
    text = json.dumps({
        "nested": {
            "array": [1, 2, 3],
            "string": "test"
        }
    })
    result = _extract_json(text)
    assert result["nested"]["array"] == [1, 2, 3]
    assert result["nested"]["string"] == "test"
