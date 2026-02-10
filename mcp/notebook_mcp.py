#!/usr/bin/env python3
"""
MCP Server for Knowledge Exchange Platform

Exposes the six notebook operations as MCP tools for Claude Desktop.

Configuration via environment variables:
    NOTEBOOK_URL    Base URL of the notebook server (default: http://localhost:8723)
    NOTEBOOK_ID     UUID of the notebook to use (required)
    NOTEBOOK_TOKEN  JWT Bearer token for authentication (required)
    AUTHOR          Author name for writes (default: claude-desktop)

Generate a token from the admin panel's profile page, or via the CLI.
Tokens are Ed25519-signed JWTs with a default 60-minute expiry.

Install in Claude Desktop's claude_desktop_config.json:
{
    "mcpServers": {
        "notebook": {
            "command": "python",
            "args": ["/path/to/notebook_mcp.py"],
            "env": {
                "NOTEBOOK_URL": "http://localhost:8723",
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

NOTEBOOK_URL = os.environ.get("NOTEBOOK_URL", "http://localhost:8723")
NOTEBOOK_ID = os.environ.get("NOTEBOOK_ID", "")
NOTEBOOK_TOKEN = os.environ.get("NOTEBOOK_TOKEN", "")
AUTHOR = os.environ.get("AUTHOR", "claude-desktop")


def api_request(method: str, path: str, body: dict = None) -> dict:
    """Make HTTP request to notebook server."""
    url = f"{NOTEBOOK_URL}{path}"
    data = json.dumps(body).encode("utf-8") if body else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if NOTEBOOK_TOKEN:
        req.add_header("Authorization", f"Bearer {NOTEBOOK_TOKEN}")

    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as e:
        error_body = e.read().decode("utf-8", errors="replace")
        return {"error": f"HTTP {e.code}: {error_body}"}
    except urllib.error.URLError as e:
        return {"error": f"Connection failed: {e.reason}"}
    except Exception as e:
        return {"error": str(e)}


# --- Tool implementations ---

def tool_write(content: str, topic: str = "", references: list = None, content_type: str = "text/plain") -> dict:
    """Write a new entry to the notebook."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}
    
    result = api_request("POST", f"/notebooks/{NOTEBOOK_ID}/entries", {
        "content": content,
        "content_type": content_type,
        "topic": topic,
        "references": references or [],
        "author": AUTHOR,
    })
    return result


def tool_revise(entry_id: str, content: str, reason: str = "") -> dict:
    """Revise an existing entry."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}
    
    result = api_request("PUT", f"/notebooks/{NOTEBOOK_ID}/entries/{entry_id}", {
        "content": content,
        "reason": reason,
        "author": AUTHOR,
    })
    return result


def tool_read(entry_id: str) -> dict:
    """Read an entry and its revision history."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}
    
    result = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/entries/{entry_id}")
    return result


def tool_browse(query: str = "", max_entries: int = 20) -> dict:
    """Browse the notebook catalog, optionally filtered by query."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}
    
    params = f"?max={max_entries}"
    if query:
        params += f"&query={urllib.parse.quote(query)}"
    
    result = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/browse{params}")
    return result


def tool_observe(since_sequence: int = 0) -> dict:
    """Observe changes since a causal position."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}
    
    result = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/observe?since={since_sequence}")
    return result


def tool_share(entity: str, read: bool = True, write: bool = False) -> dict:
    """Share the notebook with another entity."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    result = api_request("POST", f"/notebooks/{NOTEBOOK_ID}/share", {
        "entity": entity,
        "read": read,
        "write": write,
    })
    return result


def tool_set_purpose(purpose: str) -> dict:
    """Set or update the notebook's purpose."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    # Check if a purpose entry already exists
    browse_result = api_request(
        "GET",
        f"/notebooks/{NOTEBOOK_ID}/browse?query={urllib.parse.quote('notebook:purpose')}&max=5",
    )

    existing_id = None
    if not browse_result.get("error"):
        for entry in browse_result.get("entries", []):
            if entry.get("topic") == "notebook:purpose":
                existing_id = entry.get("entry_id")
                break

    if existing_id:
        return tool_revise(
            entry_id=existing_id,
            content=purpose,
            reason="Updated notebook purpose",
        )
    else:
        return tool_write(
            content=purpose,
            topic="notebook:purpose",
        )


def tool_get_context() -> dict:
    """Get notebook context: purpose, open questions, and catalog summary."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    context = {}

    # Fetch purpose
    purpose_result = api_request(
        "GET",
        f"/notebooks/{NOTEBOOK_ID}/browse?query={urllib.parse.quote('notebook:purpose')}&max=5",
    )
    purpose_text = None
    if not purpose_result.get("error"):
        for entry in purpose_result.get("entries", []):
            if entry.get("topic") == "notebook:purpose":
                purpose_text = entry.get("content") or entry.get("summary")
                break
    context["purpose"] = purpose_text

    # Fetch open questions
    questions_result = api_request(
        "GET",
        f"/notebooks/{NOTEBOOK_ID}/browse?query={urllib.parse.quote('open-question')}&max=50",
    )
    questions = []
    if not questions_result.get("error"):
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
        f"/notebooks/{NOTEBOOK_ID}/browse?max=30",
    )
    if not catalog_result.get("error"):
        context["catalog"] = catalog_result
    else:
        context["catalog"] = catalog_result

    return context


# --- MCP Protocol handling ---

TOOLS = [
    {
        "name": "notebook_write",
        "description": "Write a new entry to the shared notebook. Returns entry_id and integration_cost showing how much this disrupted existing knowledge. Use topic 'open-question' for questions that invite future exploration.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "content": {
                    "type": "string",
                    "description": "The content to write"
                },
                "topic": {
                    "type": "string",
                    "description": "Topic/category for the entry (used for catalog organization)"
                },
                "references": {
                    "type": "array",
                    "items": {"type": "string"},
                    "description": "Entry IDs this content references or builds upon"
                },
                "content_type": {
                    "type": "string",
                    "description": "MIME type of content (default: text/plain)"
                }
            },
            "required": ["content"]
        }
    },
    {
        "name": "notebook_revise",
        "description": "Revise an existing entry with new content. Original is preserved; creates a new revision.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "entry_id": {
                    "type": "string",
                    "description": "ID of the entry to revise"
                },
                "content": {
                    "type": "string",
                    "description": "New content for the entry"
                },
                "reason": {
                    "type": "string",
                    "description": "Reason for the revision"
                }
            },
            "required": ["entry_id", "content"]
        }
    },
    {
        "name": "notebook_read",
        "description": "Read a specific entry and its revision history.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "entry_id": {
                    "type": "string",
                    "description": "ID of the entry to read"
                }
            },
            "required": ["entry_id"]
        }
    },
    {
        "name": "notebook_browse",
        "description": "Browse the notebook catalog. Returns topics with summaries, entry counts, and cumulative integration cost (higher = more significant knowledge). Use this to understand what's in the notebook. Special topics: 'notebook:purpose' for the notebook's guiding aim, 'open-question' for questions inviting exploration.",
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
                }
            }
        }
    },
    {
        "name": "notebook_observe",
        "description": "Observe changes since a causal position. Returns list of changes with their integration costs. Use to see what happened since you last checked.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "since_sequence": {
                    "type": "integer",
                    "description": "Sequence number to observe from (0 for all history)"
                }
            }
        }
    },
    {
        "name": "notebook_share",
        "description": "Share the notebook with another entity.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "entity": {
                    "type": "string",
                    "description": "Entity identifier to share with"
                },
                "read": {
                    "type": "boolean",
                    "description": "Grant read access (default: true)"
                },
                "write": {
                    "type": "boolean",
                    "description": "Grant write access (default: false)"
                }
            },
            "required": ["entity"]
        }
    },
    {
        "name": "notebook_set_purpose",
        "description": "Set or update the notebook's guiding purpose/aim. Stores it as an entry with topic 'notebook:purpose'. If a purpose already exists, revises it instead of creating a duplicate.",
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
        "name": "notebook_get_context",
        "description": "Get the notebook's current context for improvement work. Returns the notebook's purpose (if set), all open questions (entries with topic 'open-question'), and a catalog summary. Use this to understand what the notebook needs before writing new entries.",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    }
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
                        "Please improve this notebook. Start by calling notebook_get_context to read "
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
                        "- Write NEW entries, don't revise other authors' work\n"
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
                        "text": f"Please set this notebook's purpose to: {purpose}\n\nUse the notebook_set_purpose tool.",
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
                            "I'd like to set a purpose for this notebook. First, call notebook_get_context "
                            "to see what's already in the notebook. Then suggest a purpose based on the "
                            "existing content, or ask me what the notebook should be about if it's empty. "
                            "Once we agree, use notebook_set_purpose to save it."
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
                    "name": "notebook-mcp",
                    "version": "1.0.0"
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

        if tool_name == "notebook_write":
            result = tool_write(
                content=arguments.get("content", ""),
                topic=arguments.get("topic", ""),
                references=arguments.get("references"),
                content_type=arguments.get("content_type", "text/plain")
            )
        elif tool_name == "notebook_revise":
            result = tool_revise(
                entry_id=arguments.get("entry_id", ""),
                content=arguments.get("content", ""),
                reason=arguments.get("reason", "")
            )
        elif tool_name == "notebook_read":
            result = tool_read(entry_id=arguments.get("entry_id", ""))
        elif tool_name == "notebook_browse":
            result = tool_browse(
                query=arguments.get("query", ""),
                max_entries=arguments.get("max_entries", 20)
            )
        elif tool_name == "notebook_observe":
            result = tool_observe(since_sequence=arguments.get("since_sequence", 0))
        elif tool_name == "notebook_share":
            result = tool_share(
                entity=arguments.get("entity", ""),
                read=arguments.get("read", True),
                write=arguments.get("write", False)
            )
        elif tool_name == "notebook_set_purpose":
            result = tool_set_purpose(
                purpose=arguments.get("purpose", "")
            )
        elif tool_name == "notebook_get_context":
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
    sys.stderr.write(f"notebook-mcp starting\n")
    sys.stderr.write(f"  URL: {NOTEBOOK_URL}\n")
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
