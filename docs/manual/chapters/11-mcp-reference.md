# Chapter 11: MCP Integration Reference

## Overview

The **Model Context Protocol (MCP)** allows Claude and other AI systems to interact with Cyber programmatically. This chapter documents all six Cyber operations available via MCP and REST API.

## Installing the MCP Server

```bash
# Install from Python package
pip install notebook-client[mcp]

# Or run from source
git clone https://github.com/cyber/notebook-client
cd notebook-client
pip install -e ".[mcp]"
```

## Configuration

Set environment variables:

```bash
export CYBER_URL="https://cyber.company.com"
export CYBER_TOKEN="your_jwt_token_here"
export CYBER_SKIP_SSL_VERIFY="false"  # Only for dev
```

Or configure via `~/.claude/claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "cyber": {
      "command": "python3",
      "args": ["-m", "notebook_client.mcp"],
      "env": {
        "CYBER_URL": "https://cyber.company.com",
        "CYBER_TOKEN": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
      }
    }
  }
}
```

## Operation Reference

### WRITE - Create New Entry

**Purpose:** Create a new entry in a notebook

**Parameters:**
```json
{
  "notebook_id": "nb_xyz789",
  "content": "Entry content (markdown, text, etc.)",
  "content_type": "text/markdown; charset=utf-8",
  "topic": "organization/engineering/architecture",
  "references": ["entry_abc123", "entry_def456"]
}
```

**Response:**
```json
{
  "entry_id": "entry_new123",
  "position": 1247,
  "notebook_id": "nb_xyz789",
  "author_id": "author_hash",
  "created_at": "2026-01-31T15:30:00Z",
  "integration_cost": 2.15,
  "status": "probation"
}
```

**Error Codes:**
- `403 Forbidden` — No write access to notebook
- `404 NotFound` — Notebook doesn't exist
- `400 BadRequest` — Invalid topic or references

### REVISE - Update Entry

**Purpose:** Create a new revision of an existing entry

**Parameters:**
```json
{
  "entry_id": "entry_abc123",
  "content": "Updated content",
  "reason": "Fixed typo and updated timeline"
}
```

**Response:**
```json
{
  "entry_id": "entry_new456",
  "position": 1248,
  "original_entry_id": "entry_abc123",
  "reason": "Fixed typo and updated timeline"
}
```

### READ - Get Entry Details

**Purpose:** Fetch full details of an entry

**Parameters:**
```json
{
  "entry_id": "entry_abc123"
}
```

**Response:**
```json
{
  "entry_id": "entry_abc123",
  "position": 1247,
  "notebook_id": "nb_xyz789",
  "content": "Full entry content",
  "content_type": "text/markdown",
  "author_id": "author_hash",
  "topic": "organization/engineering",
  "references": ["entry_def456"],
  "created_at": "2026-01-31T15:30:00Z",
  "integration_cost": 1.2,
  "status": "integrated",
  "revision_history": [
    {
      "position": 1248,
      "author_id": "author_hash2",
      "reason": "Updated timeline"
    }
  ]
}
```

### BROWSE - List Entries

**Purpose:** List entries in a notebook with filters

**Parameters:**
```json
{
  "notebook_id": "nb_xyz789",
  "topic": "organization/engineering",
  "status": "integrated",
  "friction_min": 0,
  "friction_max": 5,
  "limit": 50,
  "offset": 0
}
```

**Response:**
```json
{
  "total": 247,
  "returned": 50,
  "entries": [
    {
      "entry_id": "entry_123",
      "title": "API Architecture",
      "author_id": "author_hash",
      "created_at": "2026-01-31T15:30:00Z",
      "integration_cost": 0.8,
      "status": "integrated",
      "topic": "organization/engineering/architecture",
      "preview": "The API is structured as..."
    }
  ]
}
```

### SEARCH - Full-Text Search

**Purpose:** Search across all accessible notebooks

**Parameters:**
```json
{
  "query": "kubernetes migration",
  "notebook_id": "nb_xyz789",
  "topic": "organization/infrastructure",
  "limit": 20
}
```

**Response:**
```json
{
  "results": [
    {
      "entry_id": "entry_abc123",
      "title": "Kubernetes Migration Plan",
      "notebook_id": "nb_xyz789",
      "score": 0.98,
      "preview": "We are planning a phased migration to Kubernetes over 3 months...",
      "matches": [
        {
          "field": "content",
          "text": "...Kubernetes migration...",
          "offset": 145
        }
      ]
    }
  ]
}
```

### OBSERVE - Track Changes

**Purpose:** Get entries added since a position

**Parameters:**
```json
{
  "notebook_id": "nb_xyz789",
  "since_position": 1200
}
```

**Response:**
```json
{
  "current_position": 1250,
  "since_position": 1200,
  "entries": [
    {
      "position": 1201,
      "entry_id": "entry_xyz",
      "title": "New Architecture Decision",
      "created_at": "2026-01-31T16:00:00Z",
      "author_id": "author_hash"
    }
  ]
}
```

### SHARE - Grant Access

**Purpose:** Grant access to a notebook for a user/group

**Parameters:**
```json
{
  "notebook_id": "nb_xyz789",
  "principal_id": "user_or_group_id",
  "access_tier": "read"
}
```

**Access Tiers:**
- `existence` — Know it exists, can't read
- `read` — Can read entries
- `read+write` — Can read and create entries
- `admin` — Full control

---

## REST API Endpoints

All operations also available as REST endpoints:

```bash
# WRITE
POST /api/notebooks/{notebook_id}/entries
  -H "Authorization: Bearer TOKEN"
  -H "Content-Type: application/json"
  -d '{...}'

# REVISE
POST /api/entries/{entry_id}/revisions
  -H "Authorization: Bearer TOKEN"
  -d '{...}'

# READ
GET /api/entries/{entry_id}
  -H "Authorization: Bearer TOKEN"

# BROWSE
GET /api/notebooks/{notebook_id}/entries?status=integrated&limit=50
  -H "Authorization: Bearer TOKEN"

# SEARCH
GET /api/search?query=kubernetes%20migration&limit=20
  -H "Authorization: Bearer TOKEN"

# OBSERVE
GET /api/notebooks/{notebook_id}/changes?since=1200
  -H "Authorization: Bearer TOKEN"

# SHARE
POST /api/notebooks/{notebook_id}/access
  -H "Authorization: Bearer TOKEN"
  -d '{...}'
```

## Authentication

**Bearer Token (Recommended):**
```bash
curl -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  https://cyber.company.com/api/notebooks
```

**Session Cookie (Web Only):**
```bash
curl -b "session=abc123..." https://cyber.company.com/api/notebooks
```

## Error Responses

All errors follow this format:

```json
{
  "error": "access_denied",
  "message": "User does not have read access to this notebook",
  "details": {
    "notebook_id": "nb_xyz789",
    "user_clearance": "CONFIDENTIAL / {}",
    "required_clearance": "SECRET / {Operations}"
  }
}
```

**Common Error Codes:**
- `400 BadRequest` — Invalid parameters
- `401 Unauthorized` — Missing or invalid token
- `403 Forbidden` — Insufficient permissions
- `404 NotFound` — Resource doesn't exist
- `429 TooManyRequests` — Rate limit exceeded
- `500 InternalServerError` — Server error

---

**Last updated:** February 21, 2026
**API Version:** 2.0
**Platform Version:** 2.1.0
