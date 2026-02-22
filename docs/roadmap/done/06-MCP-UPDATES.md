# Step 6: MCP Server Updates

**Depends on:** Steps 2 (Batch Write & Claims), 3 (Job Queue), 4 (Filtered Browse & Search)

## Goal

Expose the new v2 operations through the MCP server so Claude Desktop instances can use batch write, filtered browse, search, and job stats. The current MCP server for .NET v2 backend is at `backend/mcp/thinktank_mcp.py`.

> **Note:** The MCP server is a Python HTTP client — it works identically regardless of whether the backend is Rust or C# ASP.NET Core. The API contract is the same. The only change is the default server port (5000 for Kestrel instead of 3000 for Axum).
>
> **Legacy Reference:** The original Rust backend MCP is at `legacy/notebook/mcp/notebook_mcp.py` (no longer in active development).

## 6.1 — New MCP Tools

Add these tool implementations and definitions to `backend/mcp/thinktank_mcp.py`.

### tool_batch_write

```python
def tool_batch_write(entries: list, author: str = "") -> dict:
    """Write multiple entries in a single batch."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    result = api_request("POST", f"/notebooks/{NOTEBOOK_ID}/batch", {
        "entries": entries,
        "author": author or AUTHOR,
    })
    return result
```

### tool_search

```python
def tool_search(query: str, search_in: str = "both", topic_prefix: str = "", max_results: int = 20) -> dict:
    """Search notebook entries by content or claims."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    params = f"?query={urllib.parse.quote(query)}&search_in={search_in}&max_results={max_results}"
    if topic_prefix:
        params += f"&topic_prefix={urllib.parse.quote(topic_prefix)}"

    result = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/search{params}")
    return result
```

### tool_job_stats

```python
def tool_job_stats() -> dict:
    """Get job queue statistics (pending, in_progress, completed, failed counts)."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    result = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/jobs/stats")
    return result
```

### Enhanced tool_browse

Update the existing `tool_browse` to support the new filter parameters:

```python
def tool_browse(
    query: str = "",
    max_entries: int = 20,
    topic_prefix: str = "",
    claims_status: str = "",
    has_friction_above: float = None,
    needs_review: bool = None,
    limit: int = None,
    offset: int = None,
) -> dict:
    """Browse the notebook catalog with optional filters."""
    if not NOTEBOOK_ID:
        return {"error": "NOTEBOOK_ID not configured"}

    params = f"?max={max_entries}"
    if query:
        params += f"&query={urllib.parse.quote(query)}"
    if topic_prefix:
        params += f"&topic_prefix={urllib.parse.quote(topic_prefix)}"
    if claims_status:
        params += f"&claims_status={claims_status}"
    if has_friction_above is not None:
        params += f"&has_friction_above={has_friction_above}"
    if needs_review is not None:
        params += f"&needs_review={'true' if needs_review else 'false'}"
    if limit is not None:
        params += f"&limit={limit}"
    if offset is not None:
        params += f"&offset={offset}"

    result = api_request("GET", f"/notebooks/{NOTEBOOK_ID}/browse{params}")
    return result
```

## 6.2 — Tool Definitions

Add to the `TOOLS` list in `backend/mcp/thinktank_mcp.py`:

```python
{
    "name": "notebook_batch_write",
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
    "name": "notebook_search",
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
    "name": "notebook_job_stats",
    "description": "Get job queue statistics. Shows pending, in_progress, completed, and failed counts for each job type (DISTILL_CLAIMS, COMPARE_CLAIMS, CLASSIFY_TOPIC). Useful for monitoring bulk ingest progress.",
    "inputSchema": {
        "type": "object",
        "properties": {},
    },
},
```

### Update existing browse tool definition

Add the new parameters to the existing `notebook_browse` tool schema:

```python
{
    "name": "notebook_browse",
    "description": "Browse the notebook catalog. Returns topics with summaries, entry counts, and cumulative integration cost. Supports filtering by topic prefix, claims status, friction threshold, and more.",
    "inputSchema": {
        "type": "object",
        "properties": {
            "query": {
                "type": "string",
                "description": "Optional search query to filter entries",
            },
            "max_entries": {
                "type": "integer",
                "description": "Maximum catalog entries to return (default: 20)",
            },
            "topic_prefix": {
                "type": "string",
                "description": "Filter by topic prefix (e.g., 'confluence/ENG/')",
            },
            "claims_status": {
                "type": "string",
                "enum": ["pending", "distilled", "verified"],
                "description": "Filter by claims processing status",
            },
            "has_friction_above": {
                "type": "number",
                "description": "Only entries with friction score above this threshold",
            },
            "needs_review": {
                "type": "boolean",
                "description": "Only entries flagged for review",
            },
            "limit": {
                "type": "integer",
                "description": "Max results for filtered browse (default: 50)",
            },
            "offset": {
                "type": "integer",
                "description": "Pagination offset for filtered browse",
            },
        },
    },
},
```

## 6.3 — Wire Up Tool Calls

Update the `tools/call` handler in `handle_request()`:

```python
elif tool_name == "notebook_batch_write":
    result = tool_batch_write(
        entries=arguments.get("entries", []),
        author=arguments.get("author", ""),
    )
elif tool_name == "notebook_search":
    result = tool_search(
        query=arguments.get("query", ""),
        search_in=arguments.get("search_in", "both"),
        topic_prefix=arguments.get("topic_prefix", ""),
        max_results=arguments.get("max_results", 20),
    )
elif tool_name == "notebook_job_stats":
    result = tool_job_stats()
```

Also update the existing `notebook_browse` call to pass the new parameters:

```python
elif tool_name == "notebook_browse":
    result = tool_browse(
        query=arguments.get("query", ""),
        max_entries=arguments.get("max_entries", 20),
        topic_prefix=arguments.get("topic_prefix", ""),
        claims_status=arguments.get("claims_status", ""),
        has_friction_above=arguments.get("has_friction_above"),
        needs_review=arguments.get("needs_review"),
        limit=arguments.get("limit"),
        offset=arguments.get("offset"),
    )
```

## 6.4 — New MCP Prompts

Add review-oriented prompts that leverage the new capabilities:

```python
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
```

And their message generators:

```python
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
                    "1. Use notebook_browse with needs_review=true to find entries flagged for review.\n"
                    "2. For each flagged entry, use notebook_read to see its claims and contradictions.\n"
                    "3. Analyze each contradiction: is it a genuine error, a temporal update, or a context difference?\n"
                    "4. Write a resolution entry explaining your analysis, referencing both the contradicting entries.\n"
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
                    "Check the current ingest progress by calling notebook_job_stats.\n"
                    "Report:\n"
                    "- How many distillation jobs are pending vs completed\n"
                    "- How many comparison jobs are pending vs completed\n"
                    "- Any failed jobs\n"
                    "- Estimated time remaining (based on completion rate)"
                ),
            },
        }
    ]
```

## 6.5 — Update Version

Update the server info version:

```python
"serverInfo": {
    "name": "notebook-mcp",
    "version": "2.0.0"
}
```

## Verify

### Test manually with a mock MCP call

```bash
# Send a tools/list request to verify new tools appear:
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | \
  NOTEBOOK_URL=http://localhost:5000 \
  NOTEBOOK_ID=test \
  NOTEBOOK_TOKEN=test \
  python backend/mcp/thinktank_mcp.py 2>/dev/null | \
  python -m json.tool

# Should list all original tools plus notebook_batch_write, notebook_search, notebook_job_stats
```

### Integration test with Claude Desktop

1. Update `claude_desktop_config.json` to use the new MCP server
2. Ask Claude: "What's in the job queue?" — should call `notebook_job_stats`
3. Ask Claude: "Search for entries about authentication" — should call `notebook_search`
4. Ask Claude: "Show me entries that need review" — should call `notebook_browse` with `needs_review=true`

## Summary of Changes to `backend/mcp/thinktank_mcp.py`

| Change | Type |
|--------|------|
| `tool_batch_write()` | New function |
| `tool_search()` | New function |
| `tool_job_stats()` | New function |
| `tool_browse()` | Updated signature (new filter params) |
| `TOOLS` list | 3 new tool definitions, 1 updated |
| `handle_request()` | 3 new tool dispatch cases, 1 updated |
| `PROMPTS` list | 2 new prompts |
| `get_prompt_messages()` | 2 new prompt message generators |
| Server version | Updated to 2.0.0 |
