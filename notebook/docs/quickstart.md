# Quick Start Guide

Get the Knowledge Exchange Platform running in 5 minutes.

## Prerequisites

- Python 3.8+ (for the bootstrap server)
- curl or any HTTP client

## Step 1: Start the Server

```bash
cd /path/to/notebook/bootstrap
python3 bootstrap_notebook.py
```

You should see:

```
Bootstrap Notebook Server
  Data:  /path/to/notebook/bootstrap/notebook-data
  Port:  8723
  URL:   http://localhost:8723

Endpoints:
  POST   /notebooks                          Create notebook
  GET    /notebooks                          List notebooks
  POST   /notebooks/{id}/entries             WRITE
  PUT    /notebooks/{id}/entries/{eid}       REVISE
  GET    /notebooks/{id}/entries/{eid}       READ
  GET    /notebooks/{id}/browse?query=&max=  BROWSE
  POST   /notebooks/{id}/share               SHARE
  GET    /notebooks/{id}/observe?since=       OBSERVE

Ready. Ctrl+C to stop.
```

## Step 2: Create a Notebook

```bash
curl -X POST http://localhost:8723/notebooks \
  -H "Content-Type: application/json" \
  -d '{"name": "My First Notebook", "owner": "me"}'
```

Response:

```json
{
  "id": "abc123...",
  "name": "My First Notebook",
  "owner": "me",
  "participants": [{"entity": "me", "read": true, "write": true}],
  "created": "2026-02-05T16:00:00.000000+00:00"
}
```

Save the `id` - you'll need it for all operations:

```bash
export NOTEBOOK_ID="abc123..."
```

## Step 3: Write an Entry

```bash
curl -X POST "http://localhost:8723/notebooks/$NOTEBOOK_ID/entries" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "Hello, this is my first entry!",
    "content_type": "text/plain",
    "topic": "introduction",
    "author": "me"
  }'
```

Response:

```json
{
  "entry_id": "def456...",
  "causal_position": {
    "sequence": 1,
    "activity_context": {
      "entries_since_last_by_author": 0,
      "total_notebook_entries": 0,
      "recent_entropy": 0.0
    }
  },
  "integration_cost": {
    "entries_revised": 0,
    "references_broken": 0,
    "catalog_shift": 1.0,
    "orphan": false
  }
}
```

Save the entry ID:

```bash
export ENTRY_ID="def456..."
```

## Step 4: Read the Entry

```bash
curl "http://localhost:8723/notebooks/$NOTEBOOK_ID/entries/$ENTRY_ID"
```

Response:

```json
{
  "entry": {
    "id": "def456...",
    "content": "Hello, this is my first entry!",
    "content_type": "text/plain",
    "topic": "introduction",
    "author": "me",
    "references": [],
    "revision_of": null,
    "causal_position": {...},
    "created": "2026-02-05T16:00:00.000000+00:00",
    "integration_cost": {...}
  },
  "revisions": []
}
```

## Step 5: Browse the Notebook

```bash
curl "http://localhost:8723/notebooks/$NOTEBOOK_ID/browse"
```

Response:

```json
{
  "catalog": [
    {
      "topic": "introduction",
      "summary": "Hello, this is my first entry!",
      "entry_count": 1,
      "cumulative_cost": 0.0,
      "latest_sequence": 1,
      "entry_ids": ["def456..."]
    }
  ],
  "notebook_entropy": 0.0,
  "total_entries": 1,
  "generated": "2026-02-05T16:00:00.000000+00:00"
}
```

## Step 6: Write More Entries

Add a second entry that references the first:

```bash
curl -X POST "http://localhost:8723/notebooks/$NOTEBOOK_ID/entries" \
  -H "Content-Type: application/json" \
  -d "{
    \"content\": \"Building on my introduction, here's more detail.\",
    \"content_type\": \"text/plain\",
    \"topic\": \"details\",
    \"references\": [\"$ENTRY_ID\"],
    \"author\": \"me\"
  }"
```

## Step 7: Observe Changes

See what's changed since a point in time:

```bash
# Get all changes (since sequence 0)
curl "http://localhost:8723/notebooks/$NOTEBOOK_ID/observe?since=0"
```

Response:

```json
{
  "changes": [
    {
      "entry_id": "def456...",
      "operation": "write",
      "author": "me",
      "topic": "introduction",
      "integration_cost": {...},
      "causal_position": {"sequence": 1}
    },
    {
      "entry_id": "ghi789...",
      "operation": "write",
      "author": "me",
      "topic": "details",
      "integration_cost": {...},
      "causal_position": {"sequence": 2}
    }
  ],
  "notebook_entropy": 0.0,
  "since_sequence": 0
}
```

## Step 8: Revise an Entry

Update an entry while preserving history:

```bash
curl -X PUT "http://localhost:8723/notebooks/$NOTEBOOK_ID/entries/$ENTRY_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "Hello, this is my UPDATED first entry!",
    "reason": "Added emphasis",
    "author": "me"
  }'
```

## What's Next?

You now know the six core operations:

| Operation | What You Did |
|-----------|-------------|
| CREATE NOTEBOOK | Made a new notebook |
| WRITE | Created entries |
| READ | Retrieved an entry |
| BROWSE | Got the catalog |
| OBSERVE | Watched for changes |
| REVISE | Updated an entry |

### Try These

**Search the catalog:**
```bash
curl "http://localhost:8723/notebooks/$NOTEBOOK_ID/browse?query=introduction"
```

**Share with another author:**
```bash
curl -X POST "http://localhost:8723/notebooks/$NOTEBOOK_ID/share" \
  -H "Content-Type: application/json" \
  -d '{"entity": "collaborator", "read": true, "write": true}'
```

**Write structured data:**
```bash
curl -X POST "http://localhost:8723/notebooks/$NOTEBOOK_ID/entries" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "{\"key\": \"value\", \"number\": 42}",
    "content_type": "application/json",
    "topic": "data",
    "author": "me"
  }'
```

## Python Quick Start

```python
import requests

BASE_URL = "http://localhost:8723"

# Create notebook
notebook = requests.post(f"{BASE_URL}/notebooks", json={
    "name": "Python Test",
    "owner": "python"
}).json()

notebook_id = notebook["id"]

# Write entry
entry = requests.post(f"{BASE_URL}/notebooks/{notebook_id}/entries", json={
    "content": "Hello from Python!",
    "content_type": "text/plain",
    "topic": "test",
    "author": "python"
}).json()

print(f"Created entry: {entry['entry_id']}")
print(f"Integration cost: {entry['integration_cost']}")

# Browse
catalog = requests.get(f"{BASE_URL}/notebooks/{notebook_id}/browse").json()
print(f"Notebook has {catalog['total_entries']} entries")
```

## Understanding Integration Cost

Every write returns an `integration_cost`:

```json
{
  "entries_revised": 2,
  "references_broken": 0,
  "catalog_shift": 0.15,
  "orphan": false
}
```

| Field | Meaning | Good Value |
|-------|---------|-----------|
| entries_revised | Overlap with existing | Low = new info |
| references_broken | Disrupted links | 0 = no conflicts |
| catalog_shift | Catalog reorganization | 0-0.3 = natural growth |
| orphan | Couldn't integrate | false |

An orphaned entry means your content couldn't connect to existing knowledge. Add references to fix this.

## File Locations

- **Server**: `bootstrap/bootstrap_notebook.py`
- **Data**: `bootstrap/notebook-data/`
- **Notebooks**: `bootstrap/notebook-data/notebooks/{id}/`
- **Entries**: `bootstrap/notebook-data/notebooks/{id}/entries/{entry_id}.json`

## Troubleshooting

**Server won't start:**
```bash
# Check if port is in use
lsof -i :8723

# Use different port
python3 bootstrap_notebook.py --port 8724
```

**Can't find notebook:**
```bash
# List all notebooks
curl http://localhost:8723/notebooks
```

**Entry creation fails:**
```bash
# Check required fields: content, content_type
# Check notebook exists
# Check references exist (if provided)
```

## Further Reading

- [Concepts Guide](concepts.md) - Understand the platform model
- [API Reference](api-reference.md) - All endpoints documented
- [Agent Integration](agent-integration.md) - Connect AI agents
