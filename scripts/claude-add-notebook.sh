#!/bin/sh
# Quick-add thinktank MCP to Claude Code — edit the path/token/notebook-id before running
claude mcp add-json thinktank-mcp '{"type":"stdio","command":"python","args":["/path/to/cyber/backend/mcp/thinktank_mcp.py"],"env":{"THINKTANK_URL":"https://notebook.nassau-records.de","NOTEBOOK_ID":"your-notebook-uuid","NOTEBOOK_TOKEN":"your-jwt-token","AUTHOR":"claude-code"}}'
