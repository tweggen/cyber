"""
Notebook Client - Python client library for the Knowledge Exchange Platform.

This library provides a clean, typed interface to the notebook API for
AI agents and other applications.

Example usage:
    >>> from notebook_client import NotebookClient
    >>>
    >>> client = NotebookClient("http://localhost:8723")
    >>>
    >>> # List notebooks
    >>> notebooks = client.list_notebooks()
    >>>
    >>> # Write an entry
    >>> entry_id = client.write(
    ...     notebook_id="...",
    ...     content="My knowledge entry",
    ...     content_type="text/plain",
    ...     topic="my-topic"
    ... )
    >>>
    >>> # Read it back
    >>> entry = client.read(notebook_id, entry_id)
    >>> print(entry.content)
    >>>
    >>> # Browse the catalog
    >>> catalog = client.browse(notebook_id)
    >>> for cluster in catalog.clusters:
    ...     print(f"{cluster.topic}: {cluster.entry_count} entries")
"""

__version__ = "0.1.0"

# Main client
from .client import NotebookClient

# Types
from .types import (
    ActivityContext,
    CausalPosition,
    IntegrationCost,
    Permissions,
    Entry,
    RevisionInfo,
    ReadResponse,
    ClusterSummary,
    Catalog,
    WriteResponse,
    ReviseResponse,
    ChangeEntry,
    ObserveResponse,
    Participant,
    NotebookSummary,
    ShareResponse,
)

# Errors
from .errors import (
    NotebookError,
    NotFoundError,
    PermissionError,
    ValidationError,
)

__all__ = [
    # Version
    "__version__",
    # Client
    "NotebookClient",
    # Types
    "ActivityContext",
    "CausalPosition",
    "IntegrationCost",
    "Permissions",
    "Entry",
    "RevisionInfo",
    "ReadResponse",
    "ClusterSummary",
    "Catalog",
    "WriteResponse",
    "ReviseResponse",
    "ChangeEntry",
    "ObserveResponse",
    "Participant",
    "NotebookSummary",
    "ShareResponse",
    # Errors
    "NotebookError",
    "NotFoundError",
    "PermissionError",
    "ValidationError",
]
