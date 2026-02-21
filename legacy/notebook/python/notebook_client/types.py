"""
Type definitions for the notebook client library.

Uses dataclasses for structured types and TypedDict for dict-like structures.
All types are designed to be immutable (frozen dataclasses) where appropriate.
"""

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Dict, List, Optional, TypedDict


# ---------------------------------------------------------------------------
# TypedDict types for nested structures
# ---------------------------------------------------------------------------


class ActivityContext(TypedDict, total=False):
    """Activity context within a causal position."""

    entries_since_last_by_author: int
    total_notebook_entries: int
    recent_entropy: float


class CausalPosition(TypedDict):
    """
    Causal position of an entry in the notebook.

    The sequence number provides a total ordering of entries.
    """

    sequence: int
    activity_context: Optional[ActivityContext]


class IntegrationCost(TypedDict):
    """
    Integration cost metrics for an entry.

    Measures how much an entry disrupts existing knowledge:
    - entries_revised: Number of existing entries affected
    - references_broken: Number of invalid references
    - catalog_shift: How much the catalog changes (0.0-1.0)
    - orphan: Whether the entry cannot be integrated
    """

    entries_revised: int
    references_broken: int
    catalog_shift: float
    orphan: bool


class Permissions(TypedDict):
    """Access permissions for a participant."""

    read: bool
    write: bool


# ---------------------------------------------------------------------------
# Dataclass types for API responses
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class Entry:
    """
    A notebook entry with all metadata.

    Entries are immutable once created. Revisions create new entries
    linked via revision_of.
    """

    id: str
    content: str
    content_type: str
    topic: Optional[str]
    references: List[str]
    revision_of: Optional[str]
    author: str
    causal_position: CausalPosition
    created: str
    integration_cost: IntegrationCost

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "Entry":
        """Create an Entry from a dictionary (API response)."""
        return cls(
            id=data["id"],
            content=data["content"],
            content_type=data["content_type"],
            topic=data.get("topic"),
            references=data.get("references", []),
            revision_of=data.get("revision_of"),
            author=data.get("author", "unknown"),
            causal_position=data.get("causal_position", {"sequence": 0}),
            created=data.get("created", ""),
            integration_cost=data.get(
                "integration_cost",
                {
                    "entries_revised": 0,
                    "references_broken": 0,
                    "catalog_shift": 0.0,
                    "orphan": False,
                },
            ),
        )


@dataclass(frozen=True)
class RevisionInfo:
    """Basic revision information."""

    id: str
    sequence: int

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "RevisionInfo":
        """Create RevisionInfo from a dictionary."""
        return cls(id=data["id"], sequence=data.get("sequence", 0))


@dataclass(frozen=True)
class ReadResponse:
    """
    Response from reading an entry.

    Includes the entry and list of its revisions.
    """

    entry: Entry
    revisions: List[RevisionInfo]

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "ReadResponse":
        """Create ReadResponse from a dictionary (API response)."""
        return cls(
            entry=Entry.from_dict(data["entry"]),
            revisions=[RevisionInfo.from_dict(r) for r in data.get("revisions", [])],
        )


@dataclass(frozen=True)
class ClusterSummary:
    """
    Summary of a topic cluster in the catalog.

    Clusters group related entries by topic and provide
    aggregate metrics.
    """

    topic: str
    summary: str
    entry_count: int
    cumulative_cost: float
    latest_sequence: int
    entry_ids: List[str]

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "ClusterSummary":
        """Create ClusterSummary from a dictionary."""
        return cls(
            topic=data.get("topic", ""),
            summary=data.get("summary", ""),
            entry_count=data.get("entry_count", 0),
            cumulative_cost=data.get("cumulative_cost", 0.0),
            latest_sequence=data.get("latest_sequence", 0),
            entry_ids=data.get("entry_ids", []),
        )


@dataclass(frozen=True)
class Catalog:
    """
    Catalog of notebook contents.

    Provides a dense summary of all entries organized by topic,
    designed to fit within an LLM's attention budget.
    """

    clusters: List[ClusterSummary]
    notebook_entropy: float
    total_entries: int
    generated: str

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "Catalog":
        """Create Catalog from a dictionary (API response)."""
        return cls(
            clusters=[
                ClusterSummary.from_dict(c) for c in data.get("catalog", [])
            ],
            notebook_entropy=data.get("notebook_entropy", 0.0),
            total_entries=data.get("total_entries", 0),
            generated=data.get("generated", ""),
        )


@dataclass(frozen=True)
class WriteResponse:
    """Response from writing a new entry."""

    entry_id: str
    causal_position: CausalPosition
    integration_cost: IntegrationCost

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "WriteResponse":
        """Create WriteResponse from a dictionary."""
        return cls(
            entry_id=data["entry_id"],
            causal_position=data.get("causal_position", {"sequence": 0}),
            integration_cost=data.get(
                "integration_cost",
                {
                    "entries_revised": 0,
                    "references_broken": 0,
                    "catalog_shift": 0.0,
                    "orphan": False,
                },
            ),
        )


@dataclass(frozen=True)
class ReviseResponse:
    """Response from revising an entry."""

    revision_id: str
    original_id: str
    causal_position: CausalPosition
    integration_cost: IntegrationCost

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "ReviseResponse":
        """Create ReviseResponse from a dictionary."""
        return cls(
            revision_id=data["revision_id"],
            original_id=data["original_id"],
            causal_position=data.get("causal_position", {"sequence": 0}),
            integration_cost=data.get(
                "integration_cost",
                {
                    "entries_revised": 0,
                    "references_broken": 0,
                    "catalog_shift": 0.0,
                    "orphan": False,
                },
            ),
        )


@dataclass(frozen=True)
class ChangeEntry:
    """A single change entry from OBSERVE."""

    entry_id: str
    operation: str  # "write" or "revise"
    author: str
    topic: Optional[str]
    integration_cost: IntegrationCost
    causal_position: CausalPosition

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "ChangeEntry":
        """Create ChangeEntry from a dictionary."""
        return cls(
            entry_id=data["entry_id"],
            operation=data.get("operation", "write"),
            author=data.get("author", "unknown"),
            topic=data.get("topic"),
            integration_cost=data.get(
                "integration_cost",
                {
                    "entries_revised": 0,
                    "references_broken": 0,
                    "catalog_shift": 0.0,
                    "orphan": False,
                },
            ),
            causal_position=data.get("causal_position", {"sequence": 0}),
        )


@dataclass(frozen=True)
class ObserveResponse:
    """Response from observing changes in a notebook."""

    changes: List[ChangeEntry]
    notebook_entropy: float
    since_sequence: int

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "ObserveResponse":
        """Create ObserveResponse from a dictionary."""
        return cls(
            changes=[ChangeEntry.from_dict(c) for c in data.get("changes", [])],
            notebook_entropy=data.get("notebook_entropy", 0.0),
            since_sequence=data.get("since_sequence", 0),
        )


@dataclass(frozen=True)
class Participant:
    """A participant in a notebook with their permissions."""

    entity: str
    read: bool
    write: bool

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "Participant":
        """Create Participant from a dictionary."""
        return cls(
            entity=data.get("entity", ""),
            read=data.get("read", False),
            write=data.get("write", False),
        )


@dataclass(frozen=True)
class NotebookSummary:
    """Summary information about a notebook."""

    id: str
    name: str
    owner: str
    created: str
    participants: List[Participant] = field(default_factory=list)

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "NotebookSummary":
        """Create NotebookSummary from a dictionary."""
        return cls(
            id=data["id"],
            name=data.get("name", ""),
            owner=data.get("owner", ""),
            created=data.get("created", ""),
            participants=[
                Participant.from_dict(p) for p in data.get("participants", [])
            ],
        )


@dataclass(frozen=True)
class ShareResponse:
    """Response from sharing a notebook."""

    status: str
    entity: str

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "ShareResponse":
        """Create ShareResponse from a dictionary."""
        return cls(
            status=data.get("status", ""),
            entity=data.get("entity", ""),
        )
