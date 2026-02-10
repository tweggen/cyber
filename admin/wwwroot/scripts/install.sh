#!/usr/bin/env bash
set -euo pipefail

# Install notebook MCP server for Claude Code
# Usage: curl -fsSL https://cyber.nassau-records.de/scripts/install.sh | bash -s -- <notebook-id> <token> [options]

DEFAULT_URL="https://notebook.nassau-records.de"
DEFAULT_SCRIPTS_URL="https://cyber.nassau-records.de"
DEFAULT_AUTHOR="claude-code"

usage() {
    echo "Usage: curl -fsSL <url>/scripts/install.sh | bash -s -- <notebook-id> <token> [--url <url>] [--author <author>]"
    echo ""
    echo "Arguments:"
    echo "  notebook-id   UUID of the notebook"
    echo "  token         JWT Bearer token for authentication"
    echo ""
    echo "Options:"
    echo "  --url <url>       Server URL (default: $DEFAULT_URL)"
    echo "  --author <author> Author name (default: $DEFAULT_AUTHOR)"
    exit 1
}

if [ $# -lt 2 ]; then
    usage
fi

NOTEBOOK_ID="$1"
TOKEN="$2"
shift 2

URL="$DEFAULT_URL"
AUTHOR="$DEFAULT_AUTHOR"

while [ $# -gt 0 ]; do
    case "$1" in
        --url)
            URL="$2"
            shift 2
            ;;
        --author)
            AUTHOR="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            usage
            ;;
    esac
done

# Detect python
if command -v python3 &>/dev/null; then
    PYTHON="python3"
elif command -v python &>/dev/null; then
    PYTHON="python"
else
    echo "Error: python3 or python not found in PATH"
    exit 1
fi

# Create ~/.cyber/ and download the MCP script
CYBER_DIR="$HOME/.cyber"
mkdir -p "$CYBER_DIR"
echo "Downloading notebook_mcp.py..."
curl -fsSL "$DEFAULT_SCRIPTS_URL/scripts/notebook_mcp.py" -o "$CYBER_DIR/notebook_mcp.py"
echo "Saved to $CYBER_DIR/notebook_mcp.py"

# Build the JSON config
MCP_SCRIPT="$CYBER_DIR/notebook_mcp.py"
JSON=$(cat <<EOF
{"type":"stdio","command":"$PYTHON","args":["$MCP_SCRIPT"],"env":{"NOTEBOOK_URL":"$URL","NOTEBOOK_ID":"$NOTEBOOK_ID","NOTEBOOK_TOKEN":"$TOKEN","AUTHOR":"$AUTHOR"}}
EOF
)

# Register with Claude Code
claude mcp add-json notebook-mcp "$JSON"

echo ""
echo "Done! MCP server 'notebook-mcp' registered with Claude Code."
echo "  Notebook: $NOTEBOOK_ID"
echo "  URL:      $URL"
echo "  Author:   $AUTHOR"
echo "  Python:   $PYTHON"
echo "  Script:   $MCP_SCRIPT"
