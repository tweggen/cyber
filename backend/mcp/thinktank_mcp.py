#!/usr/bin/env python3
"""
MCP Server for Thinktank (.NET Notebook.Server)

Exposes notebook CRUD, batch writes, browse, search, and job monitoring
as MCP tools for Claude Desktop.

Configuration via environment variables:
    THINKTANK_URL   Base URL of the .NET server (default: http://localhost:5281)
    NOTEBOOK_ID     UUID of the notebook to use (optional for CRUD tools)
    NOTEBOOK_TOKEN  JWT Bearer token for authentication (required)
    AUTHOR          Author name for writes (default: claude-desktop)

Install in Claude Desktop's claude_desktop_config.json:
{
    "mcpServers": {
        "thinktank": {
            "command": "python",
            "args": ["/path/to/thinktank_mcp.py"],
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


# --- Tool implementations ---

# Notebook CRUD (no NOTEBOOK_ID guard)

def tool_list_notebooks() -> dict:
    """List all notebooks accessible to the authenticated user."""
    return api_request("GET", "/notebooks")


def tool_create_notebook(name: str) -> dict:
    """Create a new notebook."""
    return api_request("POST", "/notebooks", {"name": name})


def tool_delete_notebook(notebook_id: str) -> dict:
    """Delete a notebook by ID."""
    return api_request("DELETE", f"/notebooks/{notebook_id}")


def tool_rename_notebook(notebook_id: str, name: str) -> dict:
    """Rename a notebook."""
    return api_request("PATCH", f"/notebooks/{notebook_id}", {"name": name})


# Entry operations (require NOTEBOOK_ID)

def tool_batch_write(entries: list, author: str = "") -> dict:
    """Write multiple entries in a single batch."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    return api_request("POST", f"/notebooks/{NOTEBOOK_ID}/batch", {
        "entries": entries,
        "author": author or AUTHOR,
    })


def tool_browse(
    query: str = "",
    max_entries: int = 20,
    topic_prefix: str = "",
    claims_status: str = "",
    author: str = "",
    sequence_min: int = None,
    sequence_max: int = None,
    fragment_of: str = "",
    has_friction_above: float = None,
    needs_review: bool = None,
    limit: int = None,
    offset: int = None,
) -> dict:
    """Browse the notebook catalog with optional filters."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    params = f"?max_entries={max_entries}"
    if query:
        params += f"&query={urllib.parse.quote(query)}"
    if topic_prefix:
        params += f"&topic_prefix={urllib.parse.quote(topic_prefix)}"
    if claims_status:
        params += f"&claims_status={claims_status}"
    if author:
        params += f"&author={urllib.parse.quote(author)}"
    if sequence_min is not None:
        params += f"&sequence_min={sequence_min}"
    if sequence_max is not None:
        params += f"&sequence_max={sequence_max}"
    if fragment_of:
        params += f"&fragment_of={fragment_of}"
    if has_friction_above is not None:
        params += f"&has_friction_above={has_friction_above}"
    if needs_review is not None:
        params += f"&needs_review={'true' if needs_review else 'false'}"
    if limit is not None:
        params += f"&limit={limit}"
    if offset is not None:
        params += f"&offset={offset}"

    return api_request("GET", f"/notebooks/{NOTEBOOK_ID}/browse{params}")


def tool_search(query: str, search_in: str = "both", topic_prefix: str = "", max_results: int = 20) -> dict:
    """Search notebook entries by content or claims."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    params = f"?query={urllib.parse.quote(query)}&search_in={search_in}&max_results={max_results}"
    if topic_prefix:
        params += f"&topic_prefix={urllib.parse.quote(topic_prefix)}"

    return api_request("GET", f"/notebooks/{NOTEBOOK_ID}/search{params}")


def tool_job_stats() -> dict:
    """Get job queue statistics."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    return api_request("GET", f"/notebooks/{NOTEBOOK_ID}/jobs/stats")


# Composite tools

def tool_set_purpose(purpose: str) -> dict:
    """Set or update the notebook's purpose. Always writes a new entry."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    # Check if a purpose entry already exists
    browse_result = api_request(
        "GET",
        f"/notebooks/{NOTEBOOK_ID}/browse?topic_prefix={urllib.parse.quote('notebook:purpose')}&limit=5",
    )

    # Always write a new entry (no revise endpoint on .NET server)
    return tool_batch_write(
        entries=[{
            "content": purpose,
            "topic": "notebook:purpose",
        }],
    )


def tool_get_context() -> dict:
    """Get notebook context: purpose, open questions, and catalog summary."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    context = {}

    # Fetch purpose
    purpose_result = api_request(
        "GET",
        f"/notebooks/{NOTEBOOK_ID}/browse?topic_prefix={urllib.parse.quote('notebook:purpose')}&limit=5",
    )
    if purpose_result.get("error"):
        return {"error": purpose_result["error"]}

    purpose_text = None
    for entry in purpose_result.get("entries", []):
        if entry.get("topic") == "notebook:purpose":
            purpose_text = entry.get("content") or entry.get("summary")
            break
    context["purpose"] = purpose_text

    # Fetch open questions
    questions_result = api_request(
        "GET",
        f"/notebooks/{NOTEBOOK_ID}/browse?topic_prefix={urllib.parse.quote('open-question')}&limit=50",
    )
    if questions_result.get("error"):
        return {"error": questions_result["error"]}

    questions = []
    for entry in questions_result.get("entries", []):
        if entry.get("topic") == "open-question":
            questions.append({
                "entry_id": entry.get("entry_id"),
                "content": entry.get("content") or entry.get("summary"),
                "integration_cost": entry.get("integration_cost"),
            })
    context["open_questions"] = questions

    # Fetch full catalog summary
    catalog_result = api_request(
        "GET",
        f"/notebooks/{NOTEBOOK_ID}/browse?max_entries=30",
    )
    if catalog_result.get("error"):
        return {"error": catalog_result["error"]}

    context["catalog"] = catalog_result

    return context


# --- MCP Protocol handling ---

TOOLS = [
    {
        "name": "thinktank_list_notebooks",
        "description": "List all notebooks accessible to the authenticated user. Returns notebook IDs, names, entry counts, and permissions.",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
    {
        "name": "thinktank_create_notebook",
        "description": "Create a new notebook. Returns the new notebook's ID, name, and owner.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {
                    "type": "string",
                    "description": "Name for the new notebook"
                }
            },
            "required": ["name"]
        }
    },
    {
        "name": "thinktank_delete_notebook",
        "description": "Delete a notebook by ID. Only the owner can delete a notebook.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "notebook_id": {
                    "type": "string",
                    "description": "UUID of the notebook to delete"
                }
            },
            "required": ["notebook_id"]
        }
    },
    {
        "name": "thinktank_rename_notebook",
        "description": "Rename a notebook. Only the owner can rename a notebook.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "notebook_id": {
                    "type": "string",
                    "description": "UUID of the notebook to rename"
                },
                "name": {
                    "type": "string",
                    "description": "New name for the notebook"
                }
            },
            "required": ["notebook_id", "name"]
        }
    },
    {
        "name": "thinktank_batch_write",
        "description": "Write multiple entries in a single batch. Each entry is automatically queued for claim distillation. Max 100 entries per call.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "entries": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "content": {"type": "string", "description": "Entry content"},
                            "topic": {"type": "string", "description": "Topic/category"},
                            "content_type": {"type": "string", "description": "MIME type (default: text/plain)"},
                            "references": {"type": "array", "items": {"type": "string"}, "description": "Referenced entry IDs"},
                            "fragment_of": {"type": "string", "description": "Parent artifact entry ID (for fragments)"},
                            "fragment_index": {"type": "integer", "description": "Position in fragment chain (0-based)"},
                        },
                        "required": ["content"],
                    },
                    "description": "Array of entries to write (max 100)",
                },
                "author": {
                    "type": "string",
                    "description": "Author identifier for the batch",
                },
            },
            "required": ["entries"],
        },
    },
    {
        "name": "thinktank_browse",
        "description": "Browse the notebook catalog. Returns entries with summaries, topics, and integration cost. Supports filtering by topic prefix, author, sequence range, fragment parent, claims status, friction threshold, and review flag.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "Optional search query to filter entries"
                },
                "max_entries": {
                    "type": "integer",
                    "description": "Maximum catalog entries to return (default: 20)"
                },
                "topic_prefix": {
                    "type": "string",
                    "description": "Filter by topic prefix (e.g., 'confluence/ENG/')"
                },
                "claims_status": {
                    "type": "string",
                    "enum": ["pending", "distilled", "verified"],
                    "description": "Filter by claims processing status"
                },
                "author": {
                    "type": "string",
                    "description": "Filter by author identifier"
                },
                "sequence_min": {
                    "type": "integer",
                    "description": "Only entries at or after this causal position"
                },
                "sequence_max": {
                    "type": "integer",
                    "description": "Only entries at or before this causal position"
                },
                "fragment_of": {
                    "type": "string",
                    "description": "Only fragments of this parent entry ID"
                },
                "has_friction_above": {
                    "type": "number",
                    "description": "Only entries with friction score above this threshold"
                },
                "needs_review": {
                    "type": "boolean",
                    "description": "Only entries flagged for review"
                },
                "limit": {
                    "type": "integer",
                    "description": "Max results for filtered browse (default: 50)"
                },
                "offset": {
                    "type": "integer",
                    "description": "Pagination offset for filtered browse"
                }
            }
        }
    },
    {
        "name": "thinktank_search",
        "description": "Search notebook entries by content text or claim text. Returns matching entries with relevance scores and snippets.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "Search terms",
                },
                "search_in": {
                    "type": "string",
                    "enum": ["content", "claims", "both"],
                    "description": "What to search (default: both)",
                },
                "topic_prefix": {
                    "type": "string",
                    "description": "Scope search to entries under this topic prefix",
                },
                "max_results": {
                    "type": "integer",
                    "description": "Maximum results to return (default: 20)",
                },
            },
            "required": ["query"],
        },
    },
    {
        "name": "thinktank_job_stats",
        "description": "Get job queue statistics. Shows pending, in_progress, completed, and failed counts for each job type (DISTILL_CLAIMS, COMPARE_CLAIMS, CLASSIFY_TOPIC). Useful for monitoring bulk ingest progress.",
        "inputSchema": {
            "type": "object",
            "properties": {},
        },
    },
    {
        "name": "thinktank_set_purpose",
        "description": "Set or update the notebook's guiding purpose/aim. Writes a new entry with topic 'notebook:purpose'.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "purpose": {
                    "type": "string",
                    "description": "The notebook's purpose — a guiding statement of what this notebook is about and what it aims to explore or achieve"
                }
            },
            "required": ["purpose"]
        }
    },
    {
        "name": "thinktank_get_context",
        "description": "Get the notebook's current context for improvement work. Returns the notebook's purpose (if set), all open questions (entries with topic 'open-question'), and a catalog summary. Use this to understand what the notebook needs before writing new entries.",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    },
]

PROMPTS = [
    {
        "name": "improve-notebook",
        "description": "Analyze the notebook and improve it: answer an open question, deepen an existing topic, or identify gaps — guided by the notebook's purpose.",
    },
    {
        "name": "set-purpose",
        "description": "Set or update what this notebook is about and what it aims to explore.",
        "arguments": [
            {
                "name": "purpose",
                "description": "The notebook's guiding purpose (leave empty to be asked interactively)",
                "required": False,
            }
        ],
    },
    {
        "name": "review-friction",
        "description": "Review and resolve high-friction entries — contradictions detected by the claim comparison system.",
        "arguments": [
            {
                "name": "topic",
                "description": "Optional topic prefix to scope the review",
                "required": False,
            }
        ],
    },
    {
        "name": "ingest-progress",
        "description": "Check the progress of a bulk ingest operation.",
    },
]


def get_prompt_messages(name: str, arguments: dict) -> list:
    """Return the messages for a given prompt."""
    if name == "improve-notebook":
        return [
            {
                "role": "user",
                "content": {
                    "type": "text",
                    "text": (
                        "Please improve this notebook. Start by calling thinktank_get_context to read "
                        "the notebook's purpose, open questions, and catalog summary.\n\n"
                        "Then pick ONE of these actions (in priority order):\n"
                        "1. If there are open questions, pick the most interesting one and write a "
                        "thoughtful answer. Reference the question's entry_id.\n"
                        "2. If the catalog reveals a gap relative to the purpose, write a new entry "
                        "addressing it.\n"
                        "3. If the notebook is thin, pick an existing entry and deepen it with a "
                        "new entry that references the original.\n\n"
                        "After writing your contribution, leave 1-2 new entries with topic "
                        "'open-question' that arise naturally from your work. Good questions are "
                        "specific, non-trivial, and connected to the notebook's purpose.\n\n"
                        "Important:\n"
                        "- Use thinktank_batch_write to write all entries in a single call\n"
                        "- Always set references to connect your entries to existing knowledge\n"
                        "- Prefer depth over breadth — one well-developed idea beats three shallow ones"
                    ),
                },
            }
        ]
    elif name == "set-purpose":
        purpose = arguments.get("purpose", "")
        if purpose:
            return [
                {
                    "role": "user",
                    "content": {
                        "type": "text",
                        "text": f"Please set this notebook's purpose to: {purpose}\n\nUse the thinktank_set_purpose tool.",
                    },
                }
            ]
        else:
            return [
                {
                    "role": "user",
                    "content": {
                        "type": "text",
                        "text": (
                            "I'd like to set a purpose for this notebook. First, call thinktank_get_context "
                            "to see what's already in the notebook. Then suggest a purpose based on the "
                            "existing content, or ask me what the notebook should be about if it's empty. "
                            "Once we agree, use thinktank_set_purpose to save it."
                        ),
                    },
                }
            ]
    elif name == "review-friction":
        topic = arguments.get("topic", "")
        topic_filter = f" for topic '{topic}'" if topic else ""
        return [
            {
                "role": "user",
                "content": {
                    "type": "text",
                    "text": (
                        f"Please review high-friction entries{topic_filter} in the notebook.\n\n"
                        "1. Use thinktank_browse with needs_review=true to find entries flagged for review.\n"
                        "2. For each flagged entry, use thinktank_search to find related contradictions.\n"
                        "3. Analyze each contradiction: is it a genuine error, a temporal update, or a context difference?\n"
                        "4. Write a resolution entry using thinktank_batch_write, referencing both contradicting entries.\n"
                        "5. Focus on the highest-severity contradictions first.\n\n"
                        "Be thorough but pragmatic — not every contradiction needs resolution."
                    ),
                },
            }
        ]
    elif name == "ingest-progress":
        return [
            {
                "role": "user",
                "content": {
                    "type": "text",
                    "text": (
                        "Check the current ingest progress by calling thinktank_job_stats.\n"
                        "Report:\n"
                        "- How many distillation jobs are pending vs completed\n"
                        "- How many comparison jobs are pending vs completed\n"
                        "- Any failed jobs\n"
                        "- Estimated time remaining (based on completion rate)"
                    ),
                },
            }
        ]
    return []


def handle_request(request: dict) -> dict:
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
                    "prompts": {}
                },
                "serverInfo": {
                    "name": "thinktank-mcp",
                    "version": "2.0.0"
                }
            }
        }

    elif method == "notifications/initialized":
        return None

    elif method == "tools/list":
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {
                "tools": TOOLS
            }
        }

    elif method == "prompts/list":
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {
                "prompts": PROMPTS
            }
        }

    elif method == "prompts/get":
        prompt_name = params.get("name", "")
        prompt_args = params.get("arguments", {})
        messages = get_prompt_messages(prompt_name, prompt_args)
        if not messages:
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {
                    "code": -32602,
                    "message": f"Unknown prompt: {prompt_name}"
                }
            }
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {
                "messages": messages
            }
        }

    elif method == "tools/call":
        tool_name = params.get("name", "")
        arguments = params.get("arguments", {})

        if tool_name == "thinktank_list_notebooks":
            result = tool_list_notebooks()
        elif tool_name == "thinktank_create_notebook":
            result = tool_create_notebook(
                name=arguments.get("name", ""),
            )
        elif tool_name == "thinktank_delete_notebook":
            result = tool_delete_notebook(
                notebook_id=arguments.get("notebook_id", ""),
            )
        elif tool_name == "thinktank_rename_notebook":
            result = tool_rename_notebook(
                notebook_id=arguments.get("notebook_id", ""),
                name=arguments.get("name", ""),
            )
        elif tool_name == "thinktank_batch_write":
            result = tool_batch_write(
                entries=arguments.get("entries", []),
                author=arguments.get("author", ""),
            )
        elif tool_name == "thinktank_browse":
            result = tool_browse(
                query=arguments.get("query", ""),
                max_entries=arguments.get("max_entries", 20),
                topic_prefix=arguments.get("topic_prefix", ""),
                claims_status=arguments.get("claims_status", ""),
                author=arguments.get("author", ""),
                sequence_min=arguments.get("sequence_min"),
                sequence_max=arguments.get("sequence_max"),
                fragment_of=arguments.get("fragment_of", ""),
                has_friction_above=arguments.get("has_friction_above"),
                needs_review=arguments.get("needs_review"),
                limit=arguments.get("limit"),
                offset=arguments.get("offset"),
            )
        elif tool_name == "thinktank_search":
            result = tool_search(
                query=arguments.get("query", ""),
                search_in=arguments.get("search_in", "both"),
                topic_prefix=arguments.get("topic_prefix", ""),
                max_results=arguments.get("max_results", 20),
            )
        elif tool_name == "thinktank_job_stats":
            result = tool_job_stats()
        elif tool_name == "thinktank_set_purpose":
            result = tool_set_purpose(
                purpose=arguments.get("purpose", ""),
            )
        elif tool_name == "thinktank_get_context":
            result = tool_get_context()
        else:
            result = {"error": f"Unknown tool: {tool_name}"}

        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps(result, indent=2)
                    }
                ]
            }
        }

    else:
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "error": {
                "code": -32601,
                "message": f"Method not found: {method}"
            }
        }


def main():
    """Main loop: read JSON-RPC from stdin, write responses to stdout."""
    sys.stderr.write(f"thinktank-mcp starting\n")
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
