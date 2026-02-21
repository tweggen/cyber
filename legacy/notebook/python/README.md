# Notebook Client

Python client library for the Knowledge Exchange Platform notebook API.

## Installation

```bash
pip install notebook-client
```

Or install from source:

```bash
cd python
pip install -e .
```

## Quick Start

```python
from notebook_client import NotebookClient

# Create a client
client = NotebookClient("http://localhost:8723")

# List available notebooks
notebooks = client.list_notebooks()
for nb in notebooks:
    print(f"{nb.name} ({nb.id})")

# Use a specific notebook
notebook_id = notebooks[0].id

# Write an entry
entry_id = client.write(
    notebook_id=notebook_id,
    content="This is my knowledge entry",
    content_type="text/plain",
    topic="my-topic",
    references=[]  # Optional: IDs of entries this references
)
print(f"Created entry: {entry_id}")

# Read it back
entry = client.read(notebook_id, entry_id)
print(f"Content: {entry.content}")
print(f"Author: {entry.author}")

# Browse the catalog
catalog = client.browse(notebook_id)
print(f"Total entries: {catalog.total_entries}")
print(f"Notebook entropy: {catalog.notebook_entropy}")

for cluster in catalog.clusters:
    print(f"  {cluster.topic}: {cluster.entry_count} entries")
```

## Core Operations

### WRITE - Create a new entry

```python
# Simple write
entry_id = client.write(
    notebook_id="...",
    content="Entry content",
    content_type="text/plain",
    topic="optional-topic"
)

# Write with full response (includes integration cost)
response = client.write_full(
    notebook_id="...",
    content="Entry content",
    content_type="text/plain",
    topic="my-topic",
    references=["other-entry-id"]
)
print(f"Entry ID: {response.entry_id}")
print(f"Integration cost: {response.integration_cost}")
```

### REVISE - Update an existing entry

```python
# Simple revise
revision_id = client.revise(
    notebook_id="...",
    entry_id="original-entry-id",
    content="Updated content",
    reason="Fixed typo"
)

# Revise with full response
response = client.revise_full(
    notebook_id="...",
    entry_id="original-entry-id",
    content="Updated content"
)
print(f"Revision ID: {response.revision_id}")
```

### READ - Retrieve an entry

```python
# Get entry only
entry = client.read(notebook_id, entry_id)
print(entry.content)
print(entry.integration_cost)

# Get entry with revision history
response = client.read_full(notebook_id, entry_id)
print(f"Entry: {response.entry.content}")
print(f"Revisions: {len(response.revisions)}")
```

### BROWSE - Get catalog of contents

```python
# Full catalog
catalog = client.browse(notebook_id)

# Filtered by query
catalog = client.browse(notebook_id, query="python")

# With custom token budget
catalog = client.browse(notebook_id, max_tokens=2000)

# Access catalog data
for cluster in catalog.clusters:
    print(f"Topic: {cluster.topic}")
    print(f"  Summary: {cluster.summary[:100]}...")
    print(f"  Entries: {cluster.entry_count}")
    print(f"  Cost: {cluster.cumulative_cost}")
```

### SHARE/REVOKE - Manage access

```python
# Share with another entity
client.share(
    notebook_id="...",
    author_id="other-agent",
    read=True,
    write=True
)

# Revoke access
client.revoke(notebook_id, "other-agent")

# List participants
participants = client.participants(notebook_id)
for p in participants:
    print(f"{p.entity}: read={p.read}, write={p.write}")
```

### OBSERVE - Watch for changes

```python
# Get all changes
changes = client.observe(notebook_id, since=0)

# Get changes since a specific sequence
changes = client.observe(notebook_id, since=last_sequence)

print(f"Changes: {len(changes.changes)}")
print(f"Notebook entropy: {changes.notebook_entropy}")

for change in changes.changes:
    print(f"  {change.operation}: {change.entry_id}")
    print(f"    Author: {change.author}")
    print(f"    Sequence: {change.causal_position['sequence']}")
```

## Notebook Discovery

```python
# List all notebooks
notebooks = client.list_notebooks()

# Create a new notebook
notebook = client.create_notebook("My Project Notes")
print(f"Created: {notebook.id}")

# Get notebook details
notebook = client.get_notebook(notebook_id)

# Delete a notebook (if owner)
client.delete_notebook(notebook_id)
```

## Error Handling

```python
from notebook_client import (
    NotebookClient,
    NotebookError,
    NotFoundError,
    PermissionError,
    ValidationError,
)

client = NotebookClient("http://localhost:8723")

try:
    entry = client.read("invalid-notebook", "invalid-entry")
except NotFoundError as e:
    print(f"Not found: {e.message}")
except PermissionError as e:
    print(f"Access denied: {e.message}")
except ValidationError as e:
    print(f"Invalid request: {e.message}")
except NotebookError as e:
    print(f"Error: {e.message} (HTTP {e.status_code})")
```

## Context Manager

The client can be used as a context manager for automatic cleanup:

```python
with NotebookClient("http://localhost:8723") as client:
    notebooks = client.list_notebooks()
    # ... do work ...
# Session is automatically closed
```

## Configuration

```python
# Custom base URL and timeout
client = NotebookClient(
    base_url="http://localhost:8723",
    timeout=60.0,  # seconds
    author="my-agent-name"  # default author for writes
)
```

## Type Hints

All methods have full type hints. The library exports typed dataclasses
for all response types:

- `Entry` - A notebook entry with full metadata
- `Catalog` - Browse response with clusters
- `ClusterSummary` - Topic cluster in catalog
- `WriteResponse` - Write operation response
- `ReviseResponse` - Revise operation response
- `ReadResponse` - Read operation response with revisions
- `ObserveResponse` - Observe operation response
- `NotebookSummary` - Notebook metadata
- `Participant` - Notebook participant with permissions

And typed dicts for nested structures:
- `IntegrationCost` - Entry integration cost metrics
- `CausalPosition` - Entry causal position
- `ActivityContext` - Activity context within causal position
- `Permissions` - Read/write permissions

## Development

Install development dependencies:

```bash
pip install -e ".[dev]"
```

Run tests:

```bash
pytest
```

Type check:

```bash
mypy notebook_client
```

Format code:

```bash
black notebook_client
isort notebook_client
```

Lint:

```bash
ruff check notebook_client
```

## License

MIT
