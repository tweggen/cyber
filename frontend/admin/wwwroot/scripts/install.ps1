# Install notebook MCP server for Claude Code or Cursor (Windows PowerShell)
# Usage: iwr <url>/scripts/install.ps1 -OutFile install.ps1; .\install.ps1 -NotebookId <id> -Token <token>

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$NotebookId,

    [Parameter(Mandatory=$true, Position=1)]
    [string]$Token,

    [string]$Url = "https://notebook.nassau-records.de",

    [string]$ScriptsUrl = "https://cyber.nassau-records.de",

    [string]$Author = "",

    [ValidateSet("claude", "cursor")]
    [string]$Target = "claude",

    [ValidateSet("notebook", "wild")]
    [string]$Mcp = "notebook"
)

$ErrorActionPreference = "Stop"

# Default author per target if not explicitly set
if ([string]::IsNullOrEmpty($Author)) {
    $Author = switch ($Target) {
        "claude" { "claude-code" }
        "cursor" { "cursor" }
        default  { $Target }
    }
}

# Set MCP-specific variables
switch ($Mcp) {
    "wild" {
        $McpScriptName = "wild_mcp.py"
        $McpRegName = "wild-mcp"
        $UrlEnvName = "THINKTANK_URL"
    }
    "notebook" {
        $McpScriptName = "notebook_mcp.py"
        $McpRegName = "notebook-mcp"
        $UrlEnvName = "NOTEBOOK_URL"
    }
}

# Create ~/.cyber/ and download the MCP script
$CyberDir = Join-Path $HOME ".cyber"
if (-not (Test-Path $CyberDir)) {
    New-Item -ItemType Directory -Path $CyberDir | Out-Null
}

$Dest = Join-Path $CyberDir $McpScriptName
Write-Host "Downloading $McpScriptName..."
Invoke-WebRequest -Uri "$ScriptsUrl/scripts/$McpScriptName" -OutFile $Dest
Write-Host "Saved to $Dest"

# Use forward slashes in the JSON config (Python handles both on Windows)
$McpScript = "$CyberDir/$McpScriptName" -replace '\\', '/'

# Build the JSON config
$Json = @"
{"type":"stdio","command":"python","args":["$McpScript"],"env":{"$UrlEnvName":"$Url","NOTEBOOK_ID":"$NotebookId","NOTEBOOK_TOKEN":"$Token","AUTHOR":"$Author"}}
"@

function Register-Claude {
    try { claude mcp remove $McpRegName 2>$null } catch {}
    claude mcp add-json $McpRegName $Json

    Write-Host ""
    Write-Host "Done! MCP server '$McpRegName' registered with Claude Code."
}

function Register-Cursor {
    $ConfigDir = Join-Path $HOME ".cursor"
    $ConfigFile = Join-Path $ConfigDir "mcp.json"

    if (-not (Test-Path $ConfigDir)) {
        New-Item -ItemType Directory -Path $ConfigDir | Out-Null
    }

    $config = @{}
    if (Test-Path $ConfigFile) {
        $config = Get-Content $ConfigFile -Raw | ConvertFrom-Json -AsHashtable
    }
    if (-not $config.ContainsKey("mcpServers")) {
        $config["mcpServers"] = @{}
    }
    $config["mcpServers"][$McpRegName] = $Json | ConvertFrom-Json -AsHashtable

    $config | ConvertTo-Json -Depth 10 | Set-Content $ConfigFile -Encoding UTF8

    Write-Host ""
    Write-Host "Done! MCP server '$McpRegName' registered with Cursor."
    Write-Host "  Config: $ConfigFile"
}

switch ($Target) {
    "claude" { Register-Claude }
    "cursor" { Register-Cursor }
}

Write-Host "  Notebook: $NotebookId"
Write-Host "  URL:      $Url"
Write-Host "  Author:   $Author"
Write-Host "  Script:   $McpScript"
Write-Host "  MCP type: $Mcp"
