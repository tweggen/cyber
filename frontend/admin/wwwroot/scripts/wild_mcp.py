#!/usr/bin/env python3
"""
Wild MCP Server — Claims-Aware Retrieval for Thinktank

Uses claims, embeddings, and the comparison graph for intelligent retrieval
instead of brute-force full-text search. Semantic search is the primary mode.

Configuration via environment variables:
    THINKTANK_URL   Base URL of the .NET server (default: http://localhost:5281)
    NOTEBOOK_ID     UUID of the notebook to use
    NOTEBOOK_TOKEN  JWT Bearer token for authentication
    AUTHOR          Author name for writes (default: claude-desktop)

Install in Claude Desktop's claude_desktop_config.json:
{
    "mcpServers": {
        "wild": {
            "command": "python",
            "args": ["/path/to/wild_mcp.py"],
            "env": {
                "THINKTANK_URL": "http://localhost:5281",
                "NOTEBOOK_ID": "your-notebook-uuid",
                "NOTEBOOK_TOKEN": "your-jwt-token",
                "AUTHOR": "claude-desktop"
            }
        }
    }
}
"""

import os
import sys
import json
import urllib.request
import urllib.error
import urllib.parse
from typing import Any

THINKTANK_URL = os.environ.get("THINKTANK_URL", "http://localhost:5281")
NOTEBOOK_ID = os.environ.get("NOTEBOOK_ID", "")
NOTEBOOK_TOKEN = os.environ.get("NOTEBOOK_TOKEN", "")
AUTHOR = os.environ.get("AUTHOR", "claude-desktop")


def api_request(method: str, path: str, body: dict = None) -> dict:
    """Make HTTP request to the .NET Notebook.Server."""
    url = f"{THINKTANK_URL}{path}"
    data = json.dumps(body).encode("utf-8") if body else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if NOTEBOOK_TOKEN:
        req.add_header("Authorization", f"Bearer {NOTEBOOK_TOKEN}")

    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            resp_body = resp.read()
            if not resp_body:
                return {}
            return json.loads(resp_body)
    except urllib.error.HTTPError as e:
        error_body = e.read().decode("utf-8", errors="replace")
        return {"error": f"HTTP {e.code}: {error_body}"}
    except urllib.error.URLError as e:
        return {"error": f"Connection failed: {e.reason}"}
    except Exception as e:
        return {"error": str(e)}


# --- Retrieval tool implementations ---


def tool_search(
    query: str,
    mode: str = "hybrid",
    top_k: int = 10,
    topic_prefix: str = "",
    integration_status: str = "",
) -> dict:
    """Primary search tool: semantic, lexical, or hybrid retrieval."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    if mode == "semantic":
        return _semantic_search(query, top_k)
    elif mode == "lexical":
        return _lexical_search(query, top_k, topic_prefix)
    elif mode == "hybrid":
        return _hybrid_search(query, top_k, topic_prefix)
    else:
        return {"error": f"Unknown mode: {mode}. Use 'semantic', 'lexical', or 'hybrid'."}


def _semantic_search(query: str, top_k: int) -> dict:
    """Embed query and find nearest entries by cosine similarity."""
    result = api_request(
        "POST",
        f"/notebooks/{NOTEBOOK_ID}/semantic-search",
        {"query": query, "top_k": top_k, "min_similarity": 0.3},
    )
    return result


def _lexical_search(query: str, top_k: int, topic_prefix: str = "") -> dict:
    """Trigram search on content + claims."""
    params = f"?query={urllib.parse.quote(query)}&search_in=both&max_results={top_k}"
    if topic_prefix:
        params += f"&topic_prefix={urllib.parse.quote(topic_prefix)}"
    return api_request("GET", f"/notebooks/{NOTEBOOK_ID}/search{params}")


def _hybrid_search(query: str, top_k: int, topic_prefix: str = "") -> dict:
    """Run semantic + lexical, merge by reciprocal rank fusion."""
    semantic = _semantic_search(query, top_k * 2)
    lexical = _lexical_search(query, top_k * 2, topic_prefix)

    # Extract result lists
    sem_results = semantic.get("results", [])
    lex_results = lexical.get("results", [])

    if semantic.get("error") and lexical.get("error"):
        return {"error": f"Both searches failed: semantic={semantic['error']}, lexical={lexical['error']}"}

    # Reciprocal Rank Fusion (k=60)
    K = 60
    scores: dict[str, float] = {}
    entry_data: dict[str, dict] = {}

    for rank, result in enumerate(sem_results):
        eid = result.get("entry_id", "")
        scores[eid] = scores.get(eid, 0) + 1 / (K + rank)
        if eid not in entry_data:
            entry_data[eid] = result

    for rank, result in enumerate(lex_results):
        eid = result.get("entry_id", "")
        scores[eid] = scores.get(eid, 0) + 1 / (K + rank)
        if eid not in entry_data:
            entry_data[eid] = result

    # Sort by fused score, take top_k
    ranked = sorted(scores.items(), key=lambda x: -x[1])[:top_k]

    merged = []
    for eid, score in ranked:
        entry = dict(entry_data.get(eid, {}))
        entry["rrf_score"] = round(score, 6)
        merged.append(entry)

    return {"results": merged, "mode": "hybrid", "semantic_count": len(sem_results), "lexical_count": len(lex_results)}


def tool_related(entry_id: str, direction: str = "all", max_results: int = 10) -> dict:
    """Follow the comparison graph from an entry."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    # Read the entry to get its comparisons
    entry_result = api_request(
        "GET", f"/notebooks/{NOTEBOOK_ID}/entries/{entry_id}"
    )
    if entry_result.get("error"):
        return entry_result

    entry = entry_result.get("entry", {})
    comparisons = entry.get("comparisons", [])

    if not comparisons:
        return {"entry_id": entry_id, "related": [], "message": "No comparisons found for this entry"}

    # Filter by direction
    related = []
    for comp in comparisons:
        friction = comp.get("friction", 0.0)
        entropy = comp.get("entropy", 0.0)

        if direction == "similar" and friction > 0.1:
            continue
        if direction == "contradicts" and friction <= 0.1:
            continue

        related.append({
            "entry_id": comp.get("compared_against", ""),
            "entropy": entropy,
            "friction": friction,
            "contradictions": comp.get("contradictions", []),
        })

    # Sort: contradictions first (high friction), then by entropy
    related.sort(key=lambda x: (-x["friction"], -x["entropy"]))
    related = related[:max_results]

    # Batch-fetch claims for related entries
    related_ids = [r["entry_id"] for r in related if r["entry_id"]]
    if related_ids:
        claims_result = api_request(
            "POST",
            f"/notebooks/{NOTEBOOK_ID}/claims/batch",
            {"entry_ids": related_ids},
        )
        claims_map = {}
        for ce in claims_result.get("entries", []):
            claims_map[ce["id"]] = ce

        for r in related:
            eid = r["entry_id"]
            if eid in claims_map:
                r["topic"] = claims_map[eid].get("topic")
                r["claims"] = claims_map[eid].get("claims", [])
                r["integration_status"] = claims_map[eid].get("integration_status", "probation")

    return {"entry_id": entry_id, "direction": direction, "related": related}


def tool_claims(entry_ids: list) -> dict:
    """Batch-fetch claims for multiple entries."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    if not entry_ids:
        return {"error": "entry_ids is required"}

    if len(entry_ids) > 100:
        return {"error": "max 100 entry_ids per request"}

    return api_request(
        "POST",
        f"/notebooks/{NOTEBOOK_ID}/claims/batch",
        {"entry_ids": entry_ids},
    )


def tool_read(entry_id: str) -> dict:
    """Full entry read with content, claims, comparisons, references, revisions."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    return api_request("GET", f"/notebooks/{NOTEBOOK_ID}/entries/{entry_id}")


def tool_topics(prefix: str = "", min_entries: int = 0) -> dict:
    """Browse the topic hierarchy with aggregation."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    params = "?limit=500"
    if prefix:
        params += f"&topic_prefix={urllib.parse.quote(prefix)}"

    result = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/browse{params}")
    if result.get("error"):
        return result

    entries = result.get("entries", [])

    # Aggregate by topic
    topics: dict[str, dict] = {}
    for entry in entries:
        topic = entry.get("topic") or "(none)"
        if topic not in topics:
            topics[topic] = {
                "topic": topic,
                "entry_count": 0,
                "friction_sum": 0.0,
                "friction_count": 0,
                "status_counts": {},
                "integration_counts": {},
            }
        t = topics[topic]
        t["entry_count"] += 1

        mf = entry.get("max_friction")
        if mf is not None:
            t["friction_sum"] += mf
            t["friction_count"] += 1

        cs = entry.get("claims_status", "pending")
        t["status_counts"][cs] = t["status_counts"].get(cs, 0) + 1

        ist = entry.get("integration_status", "probation")
        t["integration_counts"][ist] = t["integration_counts"].get(ist, 0) + 1

    # Compute averages, filter by min_entries
    result_topics = []
    for t in topics.values():
        if t["entry_count"] < min_entries:
            continue
        avg_friction = (t["friction_sum"] / t["friction_count"]) if t["friction_count"] > 0 else None
        result_topics.append({
            "topic": t["topic"],
            "entry_count": t["entry_count"],
            "avg_friction": round(avg_friction, 4) if avg_friction is not None else None,
            "claims_status_breakdown": t["status_counts"],
            "integration_status_breakdown": t["integration_counts"],
        })

    result_topics.sort(key=lambda x: -x["entry_count"])
    return {"topics": result_topics}


def tool_friction(min_friction: float = 0.2, topic_prefix: str = "", limit: int = 20) -> dict:
    """Find controversial/contested entries."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    params = f"?has_friction_above={min_friction}&needs_review=true&limit={limit}"
    if topic_prefix:
        params += f"&topic_prefix={urllib.parse.quote(topic_prefix)}"

    result = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/browse{params}")
    if result.get("error"):
        return result

    entries = result.get("entries", [])

    # Enrich with top contradiction from each entry
    enriched = []
    for entry in entries:
        enriched.append({
            "entry_id": entry.get("id", ""),
            "topic": entry.get("topic"),
            "max_friction": entry.get("max_friction"),
            "integration_status": entry.get("integration_status", "probation"),
            "claim_count": entry.get("claim_count", 0),
        })

    enriched.sort(key=lambda x: -(x.get("max_friction") or 0))
    return {"entries": enriched, "count": len(enriched)}


def tool_recent(since_sequence: int = 0) -> dict:
    """What changed since a causal position, enriched with claims."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    params = f"?since={since_sequence}" if since_sequence > 0 else ""
    result = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/observe{params}")
    if result.get("error"):
        return result

    changes = result.get("changes", [])

    # Batch-fetch claims for changed entries
    entry_ids = [c.get("entry_id", "") for c in changes if c.get("entry_id")]
    if entry_ids:
        claims_result = api_request(
            "POST",
            f"/notebooks/{NOTEBOOK_ID}/claims/batch",
            {"entry_ids": entry_ids[:100]},
        )
        claims_map = {}
        for ce in claims_result.get("entries", []):
            claims_map[ce["id"]] = ce

        for change in changes:
            eid = change.get("entry_id", "")
            if eid in claims_map:
                change["claims"] = claims_map[eid].get("claims", [])
                change["claims_status"] = claims_map[eid].get("claims_status", "pending")
                change["integration_status"] = claims_map[eid].get("integration_status", "probation")

    return {
        "changes": changes,
        "current_sequence": result.get("current_sequence", 0),
    }


# --- Write tool implementations ---


def tool_write(entries: list, author: str = "") -> dict:
    """Write entries to the notebook."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    return api_request("POST", f"/notebooks/{NOTEBOOK_ID}/batch", {
        "entries": entries,
        "author": author or AUTHOR,
    })


def tool_revise(entry_id: str, content: str, reason: str = "") -> dict:
    """Revise an existing entry by writing a new entry that references the original."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    # Read the original to get its topic
    original = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/entries/{entry_id}")
    if original.get("error"):
        return original

    entry = original.get("entry", {})
    topic = entry.get("topic", "")

    revision_content = content
    if reason:
        revision_content = f"[Revision of {entry_id}: {reason}]\n\n{content}"

    return api_request("POST", f"/notebooks/{NOTEBOOK_ID}/batch", {
        "entries": [{
            "content": revision_content,
            "topic": topic,
            "references": [entry_id],
        }],
        "author": AUTHOR,
    })


def tool_status() -> dict:
    """Notebook health: entries, claims progress, job queue, friction areas."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    status = {}

    # Job stats
    job_stats = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/jobs/stats")
    if not job_stats.get("error"):
        status["job_queue"] = job_stats

    # Browse for total counts and integration status breakdown
    browse = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/browse?limit=500")
    if not browse.get("error"):
        entries = browse.get("entries", [])
        total = len(entries)

        claims_distilled = sum(1 for e in entries if e.get("claims_status") == "distilled")
        claims_pending = sum(1 for e in entries if e.get("claims_status") == "pending")

        integration = {}
        for e in entries:
            ist = e.get("integration_status", "probation")
            integration[ist] = integration.get(ist, 0) + 1

        high_friction = [e for e in entries if (e.get("max_friction") or 0) > 0.2]

        status["total_entries"] = total
        status["claims_distilled"] = claims_distilled
        status["claims_pending"] = claims_pending
        status["claims_distilled_pct"] = round(claims_distilled / total * 100, 1) if total > 0 else 0
        status["integration_status"] = integration
        status["high_friction_count"] = len(high_friction)

        if high_friction:
            status["top_friction"] = sorted(
                [{"entry_id": e.get("id"), "topic": e.get("topic"), "max_friction": e.get("max_friction")}
                 for e in high_friction],
                key=lambda x: -(x.get("max_friction") or 0),
            )[:5]

    return status


# --- MCP Protocol ---


TOOLS = [
    {
        "name": "wild_search",
        "description": (
            "Search the notebook using claims and embeddings. Returns ranked entries with their claims, "
            "similarity scores, and integration status. Modes: 'semantic' (embedding cosine similarity — "
            "best for meaning-based queries), 'lexical' (trigram text matching), 'hybrid' (both merged "
            "via reciprocal rank fusion — recommended default)."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "Natural language search query",
                },
                "mode": {
                    "type": "string",
                    "enum": ["semantic", "lexical", "hybrid"],
                    "description": "Search mode (default: hybrid)",
                },
                "top_k": {
                    "type": "integer",
                    "description": "Number of results to return (default: 10)",
                },
                "topic_prefix": {
                    "type": "string",
                    "description": "Scope search to entries under this topic prefix",
                },
                "integration_status": {
                    "type": "string",
                    "enum": ["probation", "integrated", "contested"],
                    "description": "Filter by integration status",
                },
            },
            "required": ["query"],
        },
    },
    {
        "name": "wild_related",
        "description": (
            "Follow the comparison graph from an entry. Shows related entries connected by "
            "entropy (novelty) and friction (contradiction) edges. Use to explore what relates "
            "to or contradicts a specific entry."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "entry_id": {
                    "type": "string",
                    "description": "UUID of the entry to explore from",
                },
                "direction": {
                    "type": "string",
                    "enum": ["similar", "contradicts", "all"],
                    "description": "Filter edges: 'similar' (low friction), 'contradicts' (high friction), or 'all' (default)",
                },
                "max_results": {
                    "type": "integer",
                    "description": "Maximum related entries to return (default: 10)",
                },
            },
            "required": ["entry_id"],
        },
    },
    {
        "name": "wild_claims",
        "description": (
            "Batch-fetch claims for multiple entries without reading full content. "
            "Returns claims with confidence scores, claims status, and integration status. "
            "Use after wild_search to get claims for search results."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "entry_ids": {
                    "type": "array",
                    "items": {"type": "string"},
                    "description": "List of entry UUIDs (max 100)",
                },
            },
            "required": ["entry_ids"],
        },
    },
    {
        "name": "wild_read",
        "description": (
            "Read a full entry with content, claims, comparisons, references, and revisions. "
            "Use to drill into a specific entry for complete details."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "entry_id": {
                    "type": "string",
                    "description": "UUID of the entry to read",
                },
            },
            "required": ["entry_id"],
        },
    },
    {
        "name": "wild_topics",
        "description": (
            "Browse the topic hierarchy. Returns topics with entry counts, average friction, "
            "claims status breakdown, and integration status distribution."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "prefix": {
                    "type": "string",
                    "description": "Topic prefix to filter (e.g., 'architecture/')",
                },
                "min_entries": {
                    "type": "integer",
                    "description": "Only show topics with at least this many entries",
                },
            },
        },
    },
    {
        "name": "wild_friction",
        "description": (
            "Find controversial entries with high friction scores. These are entries where "
            "the claim comparison system detected contradictions with other entries. "
            "Entries with integration_status='contested' failed the friction threshold."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "min_friction": {
                    "type": "number",
                    "description": "Minimum friction threshold (default: 0.2)",
                },
                "topic_prefix": {
                    "type": "string",
                    "description": "Scope to entries under this topic prefix",
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum entries to return (default: 20)",
                },
            },
        },
    },
    {
        "name": "wild_recent",
        "description": (
            "Show what changed since a causal position. Returns changes enriched with "
            "claims summaries so you can understand what's new without reading full entries."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "since_sequence": {
                    "type": "integer",
                    "description": "Sequence number to observe from (0 for all history)",
                },
            },
        },
    },
    {
        "name": "wild_write",
        "description": (
            "Write new entries to the notebook. Each entry is automatically queued for "
            "claim distillation, embedding, and comparison. Supports content filters "
            "via the 'source' field (e.g., 'wikipedia')."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "entries": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "content": {"type": "string", "description": "Entry content"},
                            "topic": {"type": "string", "description": "Topic/category (slash-separated path)"},
                            "content_type": {"type": "string", "description": "MIME type (default: text/plain)"},
                            "references": {"type": "array", "items": {"type": "string"}, "description": "Referenced entry IDs"},
                            "source": {"type": "string", "description": "Content source hint for filtering (e.g., 'wikipedia')"},
                        },
                        "required": ["content"],
                    },
                    "description": "Array of entries to write (max 100)",
                },
                "author": {
                    "type": "string",
                    "description": "Author identifier",
                },
            },
            "required": ["entries"],
        },
    },
    {
        "name": "wild_revise",
        "description": (
            "Revise an existing entry. Writes a new entry referencing the original, "
            "preserving the topic. The new entry goes through the full integration pipeline."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "entry_id": {
                    "type": "string",
                    "description": "UUID of the entry to revise",
                },
                "content": {
                    "type": "string",
                    "description": "New content for the revision",
                },
                "reason": {
                    "type": "string",
                    "description": "Reason for the revision",
                },
            },
            "required": ["entry_id", "content"],
        },
    },
    {
        "name": "wild_status",
        "description": (
            "Notebook health overview: total entries, claims distillation progress, "
            "job queue depth, integration status breakdown (probation/integrated/contested), "
            "and top friction areas."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {},
        },
    },
]


PROMPTS = [
    {
        "name": "research",
        "description": "Research a topic in the notebook using semantic search and the comparison graph.",
        "arguments": [
            {
                "name": "topic",
                "description": "The topic or question to research",
                "required": True,
            }
        ],
    },
    {
        "name": "contradictions",
        "description": "Find and analyze contradictions in the notebook's knowledge base.",
        "arguments": [
            {
                "name": "topic",
                "description": "Optional topic prefix to scope the review",
                "required": False,
            }
        ],
    },
    {
        "name": "explore",
        "description": "Explore what the notebook knows — discover the topic landscape, find clusters, and report findings.",
    },
]


def get_prompt_messages(name: str, arguments: dict) -> list:
    """Return the messages for a given prompt."""
    if name == "research":
        topic = arguments.get("topic", "")
        return [
            {
                "role": "user",
                "content": {
                    "type": "text",
                    "text": (
                        f"Research the following topic in the notebook: {topic}\n\n"
                        "1. Start with wild_search using hybrid mode to find relevant entries\n"
                        "2. Examine the top results' claims — these are pre-distilled summaries\n"
                        "3. For the most relevant entries, use wild_related to explore the comparison graph\n"
                        "4. If you find contradictions (high friction), read both sides with wild_read\n"
                        "5. Synthesize your findings, citing specific entry IDs\n\n"
                        "Focus on claims rather than raw content — they're more information-dense. "
                        "Only use wild_read for entries where you need the full source material."
                    ),
                },
            }
        ]
    elif name == "contradictions":
        topic = arguments.get("topic", "")
        topic_filter = f" within topic '{topic}'" if topic else ""
        return [
            {
                "role": "user",
                "content": {
                    "type": "text",
                    "text": (
                        f"Find and resolve contradictions{topic_filter} in the notebook.\n\n"
                        "1. Use wild_friction to find entries with high friction scores\n"
                        "2. For each flagged entry, use wild_related with direction='contradicts'\n"
                        "3. Read both contradicting entries with wild_read to understand the full context\n"
                        "4. Analyze each contradiction: genuine error, temporal update, or context difference?\n"
                        "5. Write resolution entries using wild_write, referencing both contradicting entries\n\n"
                        "Focus on entries with integration_status='contested' first — these have the "
                        "highest-severity contradictions."
                    ),
                },
            }
        ]
    elif name == "explore":
        return [
            {
                "role": "user",
                "content": {
                    "type": "text",
                    "text": (
                        "Explore what this notebook knows.\n\n"
                        "1. Use wild_status to understand the notebook's health and size\n"
                        "2. Use wild_topics to see the topic landscape and identify major areas\n"
                        "3. For the largest/most interesting topics, use wild_search to sample entries\n"
                        "4. Follow the comparison graph with wild_related to find knowledge clusters\n"
                        "5. Report your findings:\n"
                        "   - What are the major knowledge areas?\n"
                        "   - Where are the contradictions and contested areas?\n"
                        "   - What topics have good integration vs. many probationary entries?\n"
                        "   - What gaps or open questions exist?\n\n"
                        "Be thorough but concise — focus on patterns and insights rather than listing "
                        "every entry."
                    ),
                },
            }
        ]
    return []


def handle_request(request: dict) -> dict | None:
    """Handle a JSON-RPC request."""
    method = request.get("method", "")
    req_id = request.get("id")
    params = request.get("params", {})

    if method == "initialize":
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {
                "protocolVersion": "2024-11-05",
                "capabilities": {
                    "tools": {},
                    "prompts": {},
                },
                "serverInfo": {
                    "name": "wild-mcp",
                    "version": "1.0.0",
                },
            },
        }

    elif method == "notifications/initialized":
        return None

    elif method == "tools/list":
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {"tools": TOOLS},
        }

    elif method == "prompts/list":
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {"prompts": PROMPTS},
        }

    elif method == "prompts/get":
        prompt_name = params.get("name", "")
        prompt_args = params.get("arguments", {})
        messages = get_prompt_messages(prompt_name, prompt_args)
        if not messages:
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {"code": -32602, "message": f"Unknown prompt: {prompt_name}"},
            }
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {"messages": messages},
        }

    elif method == "tools/call":
        tool_name = params.get("name", "")
        arguments = params.get("arguments", {})

        if tool_name == "wild_search":
            result = tool_search(
                query=arguments.get("query", ""),
                mode=arguments.get("mode", "hybrid"),
                top_k=arguments.get("top_k", 10),
                topic_prefix=arguments.get("topic_prefix", ""),
                integration_status=arguments.get("integration_status", ""),
            )
        elif tool_name == "wild_related":
            result = tool_related(
                entry_id=arguments.get("entry_id", ""),
                direction=arguments.get("direction", "all"),
                max_results=arguments.get("max_results", 10),
            )
        elif tool_name == "wild_claims":
            result = tool_claims(
                entry_ids=arguments.get("entry_ids", []),
            )
        elif tool_name == "wild_read":
            result = tool_read(
                entry_id=arguments.get("entry_id", ""),
            )
        elif tool_name == "wild_topics":
            result = tool_topics(
                prefix=arguments.get("prefix", ""),
                min_entries=arguments.get("min_entries", 0),
            )
        elif tool_name == "wild_friction":
            result = tool_friction(
                min_friction=arguments.get("min_friction", 0.2),
                topic_prefix=arguments.get("topic_prefix", ""),
                limit=arguments.get("limit", 20),
            )
        elif tool_name == "wild_recent":
            result = tool_recent(
                since_sequence=arguments.get("since_sequence", 0),
            )
        elif tool_name == "wild_write":
            result = tool_write(
                entries=arguments.get("entries", []),
                author=arguments.get("author", ""),
            )
        elif tool_name == "wild_revise":
            result = tool_revise(
                entry_id=arguments.get("entry_id", ""),
                content=arguments.get("content", ""),
                reason=arguments.get("reason", ""),
            )
        elif tool_name == "wild_status":
            result = tool_status()
        else:
            result = {"error": f"Unknown tool: {tool_name}"}

        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps(result, indent=2),
                    }
                ]
            },
        }

    else:
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "error": {"code": -32601, "message": f"Method not found: {method}"},
        }


def main():
    """Main loop: read JSON-RPC from stdin, write responses to stdout."""
    sys.stderr.write("wild-mcp starting\n")
    sys.stderr.write(f"  URL: {THINKTANK_URL}\n")
    sys.stderr.write(f"  Notebook: {NOTEBOOK_ID or '(not configured)'}\n")
    sys.stderr.write(f"  Token: {'configured' if NOTEBOOK_TOKEN else '(not configured)'}\n")
    sys.stderr.write(f"  Author: {AUTHOR}\n")
    sys.stderr.flush()

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue

        try:
            request = json.loads(line)
        except json.JSONDecodeError as e:
            sys.stderr.write(f"Invalid JSON: {e}\n")
            continue

        response = handle_request(request)

        if response is not None:
            sys.stdout.write(json.dumps(response) + "\n")
            sys.stdout.flush()


if __name__ == "__main__":
    main()
