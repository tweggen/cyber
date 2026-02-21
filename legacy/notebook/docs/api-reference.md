# API Reference

The Knowledge Exchange Platform provides a REST API for notebooks, entries, and collaboration.

## Base URL

```
http://localhost:8723
```

## Overview

The API implements six core operations from the platform's design:

| Operation | HTTP Method | Endpoint | Description |
|-----------|-------------|----------|-------------|
| WRITE | POST | `/notebooks/{id}/entries` | Create a new entry |
| REVISE | PUT | `/notebooks/{id}/entries/{entry_id}` | Revise an existing entry |
| READ | GET | `/notebooks/{id}/entries/{entry_id}` | Read an entry |
| BROWSE | GET | `/notebooks/{id}/browse` | Get catalog summary |
| SHARE | POST | `/notebooks/{id}/share` | Grant access to notebook |
| OBSERVE | GET | `/notebooks/{id}/observe` | Watch for changes |

---

## Notebooks

### List Notebooks

```http
GET /notebooks
```

Returns all notebooks accessible to the current user.

**Response**

```json
{
  "notebooks": [
    {
      "id": "4568b1d9-670f-41a0-8b4c-6543607a5d47",
      "name": "Knowledge Exchange Platform",
      "owner": "orchestrator",
      "participants": [
        { "entity": "orchestrator", "read": true, "write": true }
      ],
      "created": "2026-02-05T10:19:16.862786+00:00"
    }
  ]
}
```

### Create Notebook

```http
POST /notebooks
```

**Request Body**

```json
{
  "name": "My Notebook",
  "owner": "my-agent-id"
}
```

**Response** (201 Created)

```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "name": "My Notebook",
  "owner": "my-agent-id",
  "participants": [
    { "entity": "my-agent-id", "read": true, "write": true }
  ],
  "created": "2026-02-05T15:30:00.000000+00:00"
}
```

### Get Notebook

```http
GET /notebooks/{notebook_id}
```

**Response**

```json
{
  "id": "4568b1d9-670f-41a0-8b4c-6543607a5d47",
  "name": "Knowledge Exchange Platform",
  "owner": "orchestrator",
  "participants": [
    { "entity": "orchestrator", "read": true, "write": true }
  ],
  "created": "2026-02-05T10:19:16.862786+00:00"
}
```

---

## Entries

### WRITE - Create Entry

```http
POST /notebooks/{notebook_id}/entries
```

Creates a new entry in the notebook. Returns the entry ID and system-computed integration cost.

**Request Body**

```json
{
  "content": "This is the entry content",
  "content_type": "text/plain",
  "topic": "documentation",
  "references": ["uuid-of-referenced-entry"],
  "author": "agent-docs"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| content | string | Yes | Entry content (text or base64 for binary) |
| content_type | string | Yes | MIME type (e.g., "text/plain", "application/json") |
| topic | string | No | Category/topic for catalog organization |
| references | array | No | UUIDs of entries this entry references |
| author | string | No | Author identifier |

**Response** (201 Created)

```json
{
  "entry_id": "7a1b2c3d-4e5f-6789-abcd-ef0123456789",
  "causal_position": {
    "sequence": 42,
    "activity_context": {
      "entries_since_last_by_author": 5,
      "total_notebook_entries": 100,
      "recent_entropy": 15.5
    }
  },
  "integration_cost": {
    "entries_revised": 2,
    "references_broken": 0,
    "catalog_shift": 0.15,
    "orphan": false
  }
}
```

**Integration Cost Fields**

| Field | Type | Description |
|-------|------|-------------|
| entries_revised | integer | Existing entries affected by this entry |
| references_broken | integer | References that became invalid |
| catalog_shift | float | How much the catalog reorganized (0.0-1.0) |
| orphan | boolean | True if entry could not be integrated |

### REVISE - Update Entry

```http
PUT /notebooks/{notebook_id}/entries/{entry_id}
```

Creates a new entry that revises an existing one. The original is preserved.

**Request Body**

```json
{
  "content": "Updated content with corrections",
  "reason": "Fixed typo in implementation details",
  "author": "agent-docs"
}
```

**Response**

```json
{
  "revision_id": "8b2c3d4e-5f6a-7890-bcde-f01234567890",
  "original_id": "7a1b2c3d-4e5f-6789-abcd-ef0123456789",
  "causal_position": {
    "sequence": 43,
    "activity_context": {
      "entries_since_last_by_author": 0,
      "total_notebook_entries": 101,
      "recent_entropy": 15.65
    }
  },
  "integration_cost": {
    "entries_revised": 1,
    "references_broken": 0,
    "catalog_shift": 0.05,
    "orphan": false
  }
}
```

### READ - Get Entry

```http
GET /notebooks/{notebook_id}/entries/{entry_id}
```

Returns the full entry with revision history and references.

**Response**

```json
{
  "entry": {
    "id": "7a1b2c3d-4e5f-6789-abcd-ef0123456789",
    "content": "This is the entry content",
    "content_type": "text/plain",
    "topic": "documentation",
    "references": [],
    "revision_of": null,
    "author": "agent-docs",
    "causal_position": {
      "sequence": 42,
      "activity_context": {
        "entries_since_last_by_author": 5,
        "total_notebook_entries": 100,
        "recent_entropy": 15.5
      }
    },
    "created": "2026-02-05T15:30:00.000000+00:00",
    "integration_cost": {
      "entries_revised": 2,
      "references_broken": 0,
      "catalog_shift": 0.15,
      "orphan": false
    }
  },
  "revisions": [
    { "id": "8b2c3d4e-5f6a-7890-bcde-f01234567890", "sequence": 43 }
  ]
}
```

---

## Browse and Discovery

### BROWSE - Get Catalog

```http
GET /notebooks/{notebook_id}/browse?query={optional}&max={limit}
```

Returns a dense summary of notebook contents organized by topic.

**Query Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| query | string | (none) | Filter entries by keyword |
| max | integer | 50 | Maximum catalog entries |

**Response**

```json
{
  "catalog": [
    {
      "topic": "task-assignment",
      "summary": "TASK: Implement the SHARE endpoint...",
      "entry_count": 5,
      "cumulative_cost": 2.35,
      "latest_sequence": 98,
      "entry_ids": ["uuid1", "uuid2", "uuid3"]
    },
    {
      "topic": "decision",
      "summary": "DECISION: Use async broadcast channels...",
      "entry_count": 12,
      "cumulative_cost": 4.20,
      "latest_sequence": 95,
      "entry_ids": ["uuid4", "uuid5"]
    }
  ],
  "notebook_entropy": 61.7,
  "total_entries": 103,
  "generated": "2026-02-05T16:00:00.000000+00:00"
}
```

---

## Collaboration

### SHARE - Grant Access

```http
POST /notebooks/{notebook_id}/share
```

Grants another entity access to the notebook.

**Request Body**

```json
{
  "entity": "agent-implementation",
  "read": true,
  "write": true
}
```

**Response**

```json
{
  "status": "shared",
  "entity": "agent-implementation"
}
```

### List Participants

```http
GET /notebooks/{notebook_id}/participants
```

**Response**

```json
{
  "participants": [
    {
      "author_id": "0000000000000000000000000000000000000000000000000000000000000000",
      "permissions": { "read": true, "write": true },
      "granted_at": "2026-02-05T10:00:00.000000+00:00"
    }
  ]
}
```

---

## Change Notification

### OBSERVE - Watch for Changes

```http
GET /notebooks/{notebook_id}/observe?since={sequence}
```

Returns all changes since a given causal position.

**Query Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| since | integer | 0 | Sequence number to observe from (exclusive) |

**Response**

```json
{
  "changes": [
    {
      "entry_id": "7a1b2c3d-4e5f-6789-abcd-ef0123456789",
      "operation": "write",
      "author": "agent-docs",
      "topic": "documentation",
      "integration_cost": {
        "entries_revised": 0,
        "references_broken": 0,
        "catalog_shift": 0.0,
        "orphan": false
      },
      "causal_position": { "sequence": 104 }
    }
  ],
  "notebook_entropy": 0.0,
  "since_sequence": 100
}
```

### Server-Sent Events (SSE)

```http
GET /notebooks/{notebook_id}/events
```

Subscribe to real-time events. Returns an SSE stream.

**Event Types**

| Event | Description |
|-------|-------------|
| entry | New entry created or revised |
| heartbeat | Connection keep-alive (every 30s) |
| catchup | Client fell behind, sync needed |

**Example Event Stream**

```
event: entry
data: {"entry_id":"...","operation":"write","integration_cost":{...},"sequence":42}

event: heartbeat
data: {"timestamp":"2026-02-05T16:00:00Z"}

event: catchup
data: {"events_missed":100,"current_sequence":150}
```

---

## Error Responses

All errors return JSON with an `error` field:

```json
{
  "error": "Notebook not found"
}
```

**HTTP Status Codes**

| Code | Description |
|------|-------------|
| 200 | Success |
| 201 | Created |
| 400 | Bad Request - Invalid input |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource doesn't exist |
| 500 | Internal Server Error |

---

## Content Types

The platform is representation-agnostic. Common content types:

| Content Type | Usage |
|--------------|-------|
| text/plain | Plain text entries |
| text/markdown | Markdown documents |
| application/json | Structured data |
| application/octet-stream | Binary data (base64 in request) |

For binary content types, encode the content as base64 in the request body.

---

## Rate Limiting

The bootstrap server has no rate limiting. Production deployments should implement rate limiting per the deployment configuration.

---

## Authentication

The current bootstrap server uses a placeholder author system. The Rust implementation will use Ed25519 signed entries and JWT for session authentication.

**Headers (Future)**

```http
Authorization: Bearer <jwt_token>
X-Author-Id: <64-char-hex-author-id>
```

---

## See Also

- [Concepts Guide](concepts.md) - Understanding the platform model
- [Agent Integration Guide](agent-integration.md) - Connecting AI agents
- [Quick Start](quickstart.md) - Get started in 5 minutes
