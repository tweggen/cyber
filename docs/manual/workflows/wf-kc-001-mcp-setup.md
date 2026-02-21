---
id: "WF-KC-001"
title: "Setting up MCP Access for Claude Desktop"
personas: ["Knowledge Contributor", "Notebook Owner"]
overlaps_with: ["WF-NO-001"]
prerequisites:
  - "Cyber account created and authenticated"
  - "Claude Desktop installed (or any MCP-compatible client)"
  - "Generate API token from Cyber"
estimated_time: "5-10 minutes"
difficulty: "Beginner"
---

# Setting up MCP Access for Claude Desktop

## Overview

This workflow enables you to use **Claude Desktop with Cyber**, allowing you to create, revise, and browse notebook entries directly from your conversations with Claude. The MCP (Model Context Protocol) integration turns Cyber into a tool that Claude can use autonomously.

**Use case:** You want to leverage Claude's writing and analysis capabilities while maintaining all entries in your Cyber notebooks with proper audit trails and security controls.

**Related workflows:**
- [Creating and organizing entries](wf-kc-002-creating-entries.md)
- [Browsing and discovering knowledge](wf-kc-003-browsing-knowledge.md)

---

## Prerequisites

Before starting, ensure you have:
- [ ] Cyber account created and authenticated
- [ ] Admin or user access to at least one notebook
- [ ] Claude Desktop installed (version 2.5.0 or later)
- [ ] Familiarity with JSON configuration files

---

## Step-by-Step Instructions

### Step 1: Generate an API Token in Cyber

**Navigate to:** Cyber UI → Your Avatar → Settings → API Tokens

1. Click the **avatar icon** in the top-right corner of Cyber
2. Select **Settings** from the dropdown menu
3. Click **API Tokens** in the left sidebar
4. Click **"+ Generate New Token"** button

You'll see this form:

```
Create API Token
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Token Name: [Claude Desktop MCP] ← Give it a descriptive name

Expiration: ○ Never  ○ 1 Month  ○ 90 Days  ⦿ 1 Year

Scopes (what Claude can do):
☑ Read notebooks and entries
☑ Write and revise entries
☑ Browse and search
☑ Observe changes
☐ Manage access control (leave unchecked)
☐ Delete entries (leave unchecked)
☐ Administer users and groups (leave unchecked)

[Generate Token]
```

**Configuration notes:**
- Choose a **1-year expiration** for convenient long-term use (you can always rotate it later)
- Only check the scopes Claude actually needs:
  - ✓ Read, Write, Revise = standard knowledge work
  - ✓ Browse and Search = discovery
  - ✓ Observe = tracking changes
- Leave admin/delete scopes unchecked for security

Click **Generate Token**.

**What you'll see:**

```
✓ Token created!

Your API Token:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

CYBER_TOKEN=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
(long string of characters)

⚠️ Save this token somewhere safe. You won't see it again.
   If you lose it, you'll need to generate a new one.

[Copy to Clipboard] [Done]
```

**Critical:** Copy this entire token string. You'll need it in the next step.

**What you'll see:**

```
✓ Token created!

Your API Token:
CYBER_TOKEN=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

⚠️ Save this token somewhere safe. You won't see it again.
```

---

### Step 2: Locate Claude Desktop Configuration Directory

**Command line:** Open a terminal and find your Claude Desktop configuration folder.

#### macOS
```bash
# Navigate to the Claude Desktop config directory
cd ~/Library/Application\ Support/Claude

# Verify you see a "claude_desktop_config.json" file
ls -la
```

**Expected output:**
```
-rw-r--r--   1 user  staff   512 Feb 21 10:30 claude_desktop_config.json
```

#### Windows (PowerShell)
```powershell
# Navigate to the config directory
cd $env:APPDATA\Claude

# List files
Get-ChildItem
```

**Expected output:**
```
Mode LastWriteTime         Length Name
---- ----              ------ ----
-a---       2/21/2026  10:30 AM    512 claude_desktop_config.json
```

#### Linux
```bash
cd ~/.config/Claude
ls -la
```

**If you don't see `claude_desktop_config.json`:** Don't panic. Claude Desktop hasn't been configured for MCP yet. You'll create it in the next step.

---

### Step 3: Edit the Configuration File

**Open your favorite text editor** (VS Code, nano, Sublime, etc.) and open the `claude_desktop_config.json` file.

**If the file exists, you'll see something like:**
```json
{
  "mcpServers": {
    "existing_server": {
      "command": "node",
      "args": ["/path/to/existing/server.js"]
    }
  }
}
```

**If the file doesn't exist, create one** with this structure:

```json
{
  "mcpServers": {}
}
```

---

### Step 4: Add Cyber MCP Configuration

Now add the Cyber MCP server entry. **Add this block inside the `"mcpServers"` object:**

```json
{
  "mcpServers": {
    "cyber": {
      "command": "python3",
      "args": ["-m", "notebook_client.mcp"],
      "env": {
        "CYBER_URL": "https://cyber.company.com",
        "CYBER_TOKEN": "PASTE_YOUR_TOKEN_HERE"
      }
    }
  }
}
```

**Replace these placeholders:**

| Placeholder | What to Use | Example |
|-------------|------------|---------|
| `https://cyber.company.com` | Your Cyber instance URL | `https://cyber.mycompany.com` or `http://localhost:8000` (development) |
| `PASTE_YOUR_TOKEN_HERE` | The token from Step 1 | `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...` |

**Full example:**
```json
{
  "mcpServers": {
    "cyber": {
      "command": "python3",
      "args": ["-m", "notebook_client.mcp"],
      "env": {
        "CYBER_URL": "https://cyber.acme.com",
        "CYBER_TOKEN": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhdXRob3JfMTIzIn0.abcdef123456"
      }
    }
  }
}
```

**Save the file.** Use Ctrl+S (Windows/Linux) or Cmd+S (macOS).

---

### Step 5: Install the Python MCP Client

The MCP server needs the **notebook_client** Python package. Install it via pip:

#### macOS / Linux
```bash
pip install notebook-client
```

#### Windows (PowerShell)
```powershell
pip install notebook-client
```

**Expected output:**
```
Successfully installed notebook-client-1.0.0
```

If you get a "command not found" error, use `pip3` instead:
```bash
pip3 install notebook-client
```

---

### Step 6: Restart Claude Desktop

Close and reopen Claude Desktop to load the new MCP configuration.

1. **Quit Claude Desktop** (Cmd+Q on macOS, Alt+F4 on Windows, or use File → Exit)
2. **Wait 5 seconds** (give it time to fully shut down)
3. **Reopen Claude Desktop** (double-click the icon or use Spotlight/Start Menu search)

**First time it starts with the new config, it may take 30 seconds to initialize the MCP server.**

---

### Step 7: Verify Connection in Claude

Once Claude Desktop restarts, start a new conversation and test the connection:

1. In a Claude conversation, type: `What notebooks can I access?`
2. Claude should respond with something like:

```
I have access to your Cyber notebooks. Here's what I can see:

Available Notebooks:
1. Q1 Planning (access: read+write)
2. R&D Roadmap (access: read)
3. Operations Incidents (access: admin)

You're cleared for CONFIDENTIAL / {Strategic Planning, Operations}
```

If Claude lists your notebooks, **the connection is working!** ✓

**If Claude says it doesn't have Cyber tools available:** Jump to [Troubleshooting](#troubleshooting) below.

---

### Step 8: Test a Simple Write Operation

Now verify that Claude can actually write to your notebooks:

**In Claude, type:**
```
Create a new entry in the "Q1 Planning" notebook with:
- Title: "Test Entry from Claude"
- Topic: "organization/testing"
- Content: "This is a test entry created via MCP integration.
  If you're reading this, the integration works!"
```

**Claude will respond:**
```
I'll create that entry for you.

✓ Entry created successfully!

Entry ID: entry_abc123
Notebook: Q1 Planning
Position: 127
Created: 2026-02-21 10:45:00 UTC
Topic: organization/testing
```

**Verify in Cyber UI:**
1. Go to Cyber in your browser
2. Navigate to the "Q1 Planning" notebook
3. Scroll to the bottom of the entry feed
4. You should see the new "Test Entry from Claude" entry

**Success!** ✓ Your MCP integration is fully functional.

---

## Verification

How to confirm the workflow completed successfully:

- [ ] API token generated and saved securely
- [ ] `claude_desktop_config.json` edited with Cyber MCP entry
- [ ] `notebook-client` Python package installed
- [ ] Claude Desktop restarted
- [ ] Claude responds with list of your notebooks
- [ ] Test entry created in Cyber and visible in UI
- [ ] Entry signed with your cryptographic key (verifiable in audit log)

---

## Tips & Tricks

### Using Environment Variables Instead of Config File

If you prefer not to store your token in `claude_desktop_config.json`, use **environment variables:**

1. **macOS/Linux:** Add to your shell profile (~/.zshrc or ~/.bash_profile):
```bash
export CYBER_URL="https://cyber.company.com"
export CYBER_TOKEN="your_token_here"
```

2. **Windows (PowerShell):** Add to your profile:
```powershell
$env:CYBER_URL = "https://cyber.company.com"
$env:CYBER_TOKEN = "your_token_here"
```

Then, simplify your config:
```json
{
  "mcpServers": {
    "cyber": {
      "command": "python3",
      "args": ["-m", "notebook_client.mcp"]
    }
  }
}
```

### Using Localhost for Development

If you're running a local Cyber instance (e.g., development):
```json
{
  "mcpServers": {
    "cyber": {
      "command": "python3",
      "args": ["-m", "notebook_client.mcp"],
      "env": {
        "CYBER_URL": "http://localhost:8000",
        "CYBER_TOKEN": "dev_token_xyz"
      }
    }
  }
}
```

### Rotating Your Token

If you think your token was compromised or want to refresh it:

1. **In Cyber UI:** Go to Settings → API Tokens
2. Find the token (look for "Claude Desktop MCP")
3. Click **Delete** or **Rotate**
4. Generate a new token following Step 1
5. Update your `claude_desktop_config.json`
6. Restart Claude Desktop

---

## Next Steps

After completing this workflow, you might want to:
- [Create and organize entries](wf-kc-002-creating-entries.md) — Write knowledge via Claude
- [Browse and discover knowledge](wf-kc-003-browsing-knowledge.md) — Search and analyze content
- [Manage revisions](wf-kc-004-managing-revisions.md) — Update entries over time

---

## Troubleshooting

### Error: "Command 'python3' not found"

**Cause:** Python isn't installed or not in your PATH

**Solution:**
1. Install Python 3.9+ from [python.org](https://python.org)
2. Verify installation:
   ```bash
   python3 --version
   # Should show: Python 3.11.x or higher
   ```
3. Restart Claude Desktop
4. If still not found, use full path to Python in config:
   ```json
   "command": "/usr/bin/python3"  # macOS
   // or
   "command": "C:\\Python311\\python.exe"  // Windows
   ```

### Error: "Connection refused" or "Cannot reach Cyber"

**Cause:** Wrong URL, Cyber server is down, or network issue

**Solution:**
1. Verify the URL in your config matches your Cyber instance
2. Test the URL in your browser:
   ```
   https://cyber.company.com/api/health
   ```
   Should return `{"status":"ok"}` or similar
3. Check network connectivity:
   ```bash
   curl https://cyber.company.com/api/health
   ```
4. If Cyber is down, contact your admin or check status page

### Error: "Invalid token"

**Cause:** Token is wrong, expired, or has insufficient scopes

**Solution:**
1. Check that you copied the **entire token string** (including `eyJ...` prefix)
2. Verify the token is still valid:
   - In Cyber UI: Settings → API Tokens
   - Look for your token; check expiration date
3. If expired or missing, generate a new token (Step 1) and update config
4. Make sure token scopes include "Read" and "Write"

### Error: "ImportError: No module named 'notebook_client'"

**Cause:** Python package not installed

**Solution:**
1. Install the package:
   ```bash
   pip3 install notebook-client
   ```
2. Verify installation:
   ```bash
   python3 -c "import notebook_client; print('OK')"
   ```
3. Restart Claude Desktop

### Claude doesn't list any notebooks

**Cause:** Connection established but Claude hasn't loaded tools yet

**Solution:**
1. **Restart Claude** — Quit completely and reopen
2. **Try again** — Sometimes tools load on first use
3. **Check Claude's activity** — Look for a "Tools" panel or section where Cyber tools should appear
4. **Check the logs** — On macOS, check `~/Library/Logs/Claude/` for errors

### "Certificate verification failed" or HTTPS warnings

**Cause:** Self-signed certificate or SSL/TLS issues (usually in dev/staging)

**Solution:**

For **development environments** with self-signed certs, you can disable verification (not recommended for production):

```json
{
  "mcpServers": {
    "cyber": {
      "command": "python3",
      "args": ["-m", "notebook_client.mcp"],
      "env": {
        "CYBER_URL": "https://localhost:8000",
        "CYBER_TOKEN": "...",
        "CYBER_SKIP_SSL_VERIFY": "true"  ← Development only!
      }
    }
  }
}
```

For **production**, ensure the Cyber server has a valid SSL certificate.

---

## Security Notes

1. **Token security:** Treat your API token like a password. Don't share it or commit it to version control.

2. **Scope limitation:** Grant only the scopes Claude needs. Avoid `admin` scopes unless necessary.

3. **Token rotation:** Rotate tokens yearly and immediately if compromised.

4. **Audit trail:** Every entry Claude creates is signed and logged with your identity. Users can see you created it.

5. **Environment variables:** Using `CYBER_TOKEN` env var is slightly more secure than hardcoding in JSON, as env vars may be excluded from file backups.

---

## API Reference

### MCP Operations Available After Setup

Once configured, Claude has access to these Cyber operations:

```
WRITE
  Create a new entry in a notebook
  Parameters: notebook_id, content, topic, references (optional)

REVISE
  Create a new revision of an existing entry
  Parameters: entry_id, content, reason (optional)

READ
  Fetch full details of an entry
  Parameters: entry_id

BROWSE
  List entries in a notebook with filters
  Parameters: notebook_id, topic (optional), status (optional), friction (optional)

SEARCH
  Full-text search across all accessible entries
  Parameters: query, notebook_id (optional)

OBSERVE
  Track changes since a causal position
  Parameters: notebook_id, since_position (optional)
```

Claude will use these automatically based on your requests in conversation.

---

**Last updated:** February 21, 2026
**Workflow ID:** WF-KC-001
**Manual version:** 1.0.0 (Beta)
**Platform version:** 2.1.0
**Overlaps with:** [Notebook Owner MCP Setup](wf-no-001-mcp-setup.md)
