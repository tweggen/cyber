# Install notebook MCP server for Claude Code (Windows PowerShell)
# Usage: iwr <url>/scripts/install.ps1 -OutFile install.ps1; .\install.ps1 -NotebookId <id> -Token <token>

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$NotebookId,

    [Parameter(Mandatory=$true, Position=1)]
    [string]$Token,

    [string]$Url = "https://notebook.nassau-records.de",

    [string]$Author = "claude-code"
)

$ErrorActionPreference = "Stop"

# Create ~/.cyber/ and download the MCP script
$CyberDir = Join-Path $HOME ".cyber"
if (-not (Test-Path $CyberDir)) {
    New-Item -ItemType Directory -Path $CyberDir | Out-Null
}

$Dest = Join-Path $CyberDir "notebook_mcp.py"
Write-Host "Downloading notebook_mcp.py..."
Invoke-WebRequest -Uri "$Url/scripts/notebook_mcp.py" -OutFile $Dest
Write-Host "Saved to $Dest"

# Use forward slashes in the JSON config (Python handles both on Windows)
$McpScript = "$CyberDir/notebook_mcp.py" -replace '\\', '/'

# Build the JSON config
$Json = @"
{"type":"stdio","command":"python","args":["$McpScript"],"env":{"NOTEBOOK_URL":"$Url","NOTEBOOK_ID":"$NotebookId","NOTEBOOK_TOKEN":"$Token","AUTHOR":"$Author"}}
"@

# Register with Claude Code (remove first in case it already exists)
try { claude mcp remove notebook-mcp 2>$null } catch {}
claude mcp add-json notebook-mcp $Json

Write-Host ""
Write-Host "Done! MCP server 'notebook-mcp' registered with Claude Code."
Write-Host "  Notebook: $NotebookId"
Write-Host "  URL:      $Url"
Write-Host "  Author:   $Author"
Write-Host "  Script:   $McpScript"
