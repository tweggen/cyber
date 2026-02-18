#!/usr/bin/env bash
set -euo pipefail

# Install notebook MCP server for Claude Code or Cursor (Windows Git Bash)
# Usage: curl -fsSL https://cyber.nassau-records.de/scripts/install-gitbash.sh | bash -s -- <notebook-id> <token> [options]
#
# Same as install.sh but converts Unix-style paths (/c/Users/...)
# to Windows-style paths (C:\Users\...) for the MCP JSON config.

DEFAULT_URL="https://notebook.nassau-records.de"
DEFAULT_SCRIPTS_URL="https://cyber.nassau-records.de"
DEFAULT_AUTHOR=""
DEFAULT_TARGET="claude"
DEFAULT_MCP="notebook"

usage() {
    echo "Usage: curl -fsSL <url>/scripts/install-gitbash.sh | bash -s -- <notebook-id> <token> [--url <url>] [--author <author>] [--target <target>] [--mcp <type>]"
    echo ""
    echo "Arguments:"
    echo "  notebook-id   UUID of the notebook"
    echo "  token         JWT Bearer token for authentication"
    echo ""
    echo "Options:"
    echo "  --url <url>             Server URL (default: $DEFAULT_URL)"
    echo "  --scripts-url <url>    URL to download MCP scripts from (default: $DEFAULT_SCRIPTS_URL)"
    echo "  --author <author>      Author name (default: claude-code or cursor, per target)"
    echo "  --target <target>      Registration target: claude or cursor (default: claude)"
    echo "  --mcp <type>           MCP type: notebook (Rust server) or wild (Thinktank .NET) (default: notebook)"
    exit 1
}

if [ $# -lt 2 ]; then
    usage
fi

NOTEBOOK_ID="$1"
TOKEN="$2"
shift 2

URL="$DEFAULT_URL"
SCRIPTS_URL="$DEFAULT_SCRIPTS_URL"
AUTHOR="$DEFAULT_AUTHOR"
TARGET="$DEFAULT_TARGET"
MCP_TYPE="$DEFAULT_MCP"

while [ $# -gt 0 ]; do
    case "$1" in
        --url)
            URL="$2"
            shift 2
            ;;
        --scripts-url)
            SCRIPTS_URL="$2"
            shift 2
            ;;
        --author)
            AUTHOR="$2"
            shift 2
            ;;
        --target)
            TARGET="$2"
            shift 2
            ;;
        --mcp)
            MCP_TYPE="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            usage
            ;;
    esac
done

# Default author per target if not explicitly set
if [ -z "$AUTHOR" ]; then
    case "$TARGET" in
        claude) AUTHOR="claude-code" ;;
        cursor) AUTHOR="cursor" ;;
        *)      AUTHOR="$TARGET" ;;
    esac
fi

# Detect python
if command -v python3 &>/dev/null; then
    PYTHON="python3"
elif command -v python &>/dev/null; then
    PYTHON="python"
else
    echo "Error: python3 or python not found in PATH"
    exit 1
fi

# Set MCP-specific variables based on type
case "$MCP_TYPE" in
    wild)
        MCP_SCRIPT_NAME="wild_mcp.py"
        MCP_REG_NAME="wild-mcp"
        URL_ENV_NAME="THINKTANK_URL"
        ;;
    notebook)
        MCP_SCRIPT_NAME="notebook_mcp.py"
        MCP_REG_NAME="notebook-mcp"
        URL_ENV_NAME="NOTEBOOK_URL"
        ;;
    *)
        echo "Error: unknown MCP type '$MCP_TYPE'. Supported: notebook, wild"
        exit 1
        ;;
esac

# Create ~/.cyber/ and download the MCP script
CYBER_DIR="$HOME/.cyber"
mkdir -p "$CYBER_DIR"
echo "Downloading $MCP_SCRIPT_NAME..."
curl -fsSL "$SCRIPTS_URL/scripts/$MCP_SCRIPT_NAME" -o "$CYBER_DIR/$MCP_SCRIPT_NAME"
echo "Saved to $CYBER_DIR/$MCP_SCRIPT_NAME"

# Convert Git Bash paths to Windows paths for the JSON config
MCP_SCRIPT=$(cygpath -w "$CYBER_DIR/$MCP_SCRIPT_NAME")
PYTHON_WIN=$(cygpath -w "$(which $PYTHON)")

JSON=$(cat <<EOF
{"type":"stdio","command":"$PYTHON_WIN","args":["$MCP_SCRIPT"],"env":{"$URL_ENV_NAME":"$URL","NOTEBOOK_ID":"$NOTEBOOK_ID","NOTEBOOK_TOKEN":"$TOKEN","AUTHOR":"$AUTHOR"}}
EOF
)

register_claude() {
    claude mcp remove "$MCP_REG_NAME" 2>/dev/null || true
    claude mcp add-json "$MCP_REG_NAME" "$JSON"
    echo ""
    echo "Done! MCP server '$MCP_REG_NAME' registered with Claude Code."
}

register_cursor() {
    local CONFIG_DIR
    CONFIG_DIR=$(cygpath -w "$HOME/.cursor")
    local CONFIG_FILE="$CONFIG_DIR\\mcp.json"
    mkdir -p "$HOME/.cursor"

    $PYTHON -c "
import json, sys, os
config_file = sys.argv[1]
server_json = json.loads(sys.argv[2])
mcp_name = sys.argv[3]
config = {}
if os.path.exists(config_file):
    with open(config_file) as f:
        config = json.load(f)
if 'mcpServers' not in config:
    config['mcpServers'] = {}
config['mcpServers'][mcp_name] = server_json
with open(config_file, 'w') as f:
    json.dump(config, f, indent=2)
" "$CONFIG_FILE" "$JSON" "$MCP_REG_NAME"

    echo ""
    echo "Done! MCP server '$MCP_REG_NAME' registered with Cursor."
    echo "  Config: $CONFIG_FILE"
}

case "$TARGET" in
    claude)
        register_claude
        ;;
    cursor)
        register_cursor
        ;;
    *)
        echo "Error: unknown target '$TARGET'. Supported: claude, cursor"
        exit 1
        ;;
esac

echo "  Notebook: $NOTEBOOK_ID"
echo "  URL:      $URL"
echo "  Author:   $AUTHOR"
echo "  Python:   $PYTHON_WIN"
echo "  Script:   $MCP_SCRIPT"
echo "  MCP type: $MCP_TYPE"
