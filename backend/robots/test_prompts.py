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
        "claims": [{"text": "OAuth is used for auth", "confidence": 0.9}],
        "available_topics": ["auth", "devops", "frontend"],
    }
    prompt = build_classify_prompt(payload)
    assert "OAuth is used for auth" in prompt
    assert "auth" in prompt
    assert "devops" in prompt


def test_parse_distill_result():
    response = '{"claims": [{"text": "Test claim", "confidence": 0.9}]}'
    result = parse_distill_result(response)
    assert len(result["claims"]) == 1
    assert result["claims"][0]["text"] == "Test claim"


def test_parse_distill_result_code_block():
    response = '```json\n{"claims": [{"text": "Test", "confidence": 0.9}]}\n```'
    result = parse_distill_result(response)
    assert len(result["claims"]) == 1


def test_parse_distill_result_invalid():
    with pytest.raises(ValueError):
        parse_distill_result("not json at all")


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
    assert result["contradictions"][0]["claim_a"] == "Earth is round"
    assert result["contradictions"][0]["claim_b"] == "Earth is flat"
    assert result["contradictions"][0]["severity"] == 0.9


def test_parse_compare_result_mixed():
    response = json.dumps({
        "classifications": [
            {"new_claim": 1, "type": "NOVEL"},
            {"new_claim": 2, "type": "REDUNDANT", "matches_existing": 1},
            {"new_claim": 3, "type": "CONTRADICTS", "conflicts_with": 1, "severity": 0.7},
        ]
    })
    payload = {
        "claims_a": [{"text": "A1", "confidence": 0.9}],
        "claims_b": [
            {"text": "B1", "confidence": 0.9},
            {"text": "B2", "confidence": 0.8},
            {"text": "B3", "confidence": 0.7},
        ],
    }
    result = parse_compare_result(response, payload)
    assert result["entropy"] == round(1 / 3, 4)
    assert result["friction"] == round(1 / 3, 4)
    assert len(result["contradictions"]) == 1


def test_parse_classify_result():
    response = '{"primary_topic": "devops", "secondary_topics": [], "new_topic": null}'
    result = parse_classify_result(response)
    assert result["primary_topic"] == "devops"
    assert result["secondary_topics"] == []
    assert result["new_topic"] is None


def test_extract_json_plain():
    assert _extract_json('{"key": "value"}') == {"key": "value"}


def test_extract_json_code_block():
    text = '```json\n{"key": "value"}\n```'
    assert _extract_json(text) == {"key": "value"}


def test_extract_json_code_block_no_lang():
    text = '```\n{"key": "value"}\n```'
    assert _extract_json(text) == {"key": "value"}
