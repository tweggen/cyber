# Install notebook MCP server for Claude Code (Windows PowerShell)
# Usage: .\install.ps1 -NotebookId <id> -Token <token> [-Url <url>] [-Author <author>]

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$NotebookId,

    [Parameter(Mandatory=$true, Position=1)]
    [string]$Token,

    [string]$Url = "https://cyber.nassau-records.de",

    [string]$Author = "claude-code"
)

$ErrorActionPreference = "Stop"

# Find the directory this script lives in
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Create ~/.cyber/ and copy the MCP script
$CyberDir = Join-Path $HOME ".cyber"
if (-not (Test-Path $CyberDir)) {
    New-Item -ItemType Directory -Path $CyberDir | Out-Null
}

$Source = Join-Path $ScriptDir "mcp\notebook_mcp.py"
$Dest = Join-Path $CyberDir "notebook_mcp.py"
Copy-Item -Path $Source -Destination $Dest -Force
Write-Host "Copied notebook_mcp.py to $CyberDir\"

# Use forward slashes in the JSON config (Python handles both on Windows)
$McpScript = "$CyberDir/notebook_mcp.py" -replace '\\', '/'

# Build the JSON config
$Json = @"
{"type":"stdio","command":"python","args":["$McpScript"],"env":{"NOTEBOOK_URL":"$Url","NOTEBOOK_ID":"$NotebookId","NOTEBOOK_TOKEN":"$Token","AUTHOR":"$Author"}}
"@

# Register with Claude Code
claude mcp add-json notebook-mcp $Json

Write-Host ""
Write-Host "Done! MCP server 'notebook-mcp' registered with Claude Code."
Write-Host "  Notebook: $NotebookId"
Write-Host "  URL:      $Url"
Write-Host "  Author:   $Author"
Write-Host "  Script:   $McpScript"
