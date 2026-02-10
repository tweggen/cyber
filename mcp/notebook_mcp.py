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


# --- MCP Protocol handling ---

TOOLS = [
    {
        "name": "notebook_write",
        "description": "Write a new entry to the shared notebook. Returns entry_id and integration_cost showing how much this disrupted existing knowledge.",
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
        "description": "Browse the notebook catalog. Returns topics with summaries, entry counts, and cumulative integration cost (higher = more significant knowledge). Use this to understand what's in the notebook.",
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
    }
]


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
                    "tools": {}
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
