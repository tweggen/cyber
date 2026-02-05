"""
Notebook client for the Knowledge Exchange Platform.

Provides a clean Python interface to all notebook API operations.
"""

from typing import Any, Dict, List, Optional
from urllib.parse import urljoin

import requests

from .errors import NotFoundError, NotebookError, PermissionError, ValidationError
from .types import (
    Catalog,
    ChangeEntry,
    Entry,
    NotebookSummary,
    ObserveResponse,
    Participant,
    ReadResponse,
    ReviseResponse,
    ShareResponse,
    WriteResponse,
)


class NotebookClient:
    """
    Client for the Knowledge Exchange Platform notebook API.

    Provides methods for all six core operations plus discovery:
    - write: Create a new entry
    - revise: Update an existing entry
    - read: Retrieve an entry
    - browse: Get catalog of notebook contents
    - share/revoke/participants: Manage access
    - observe: Watch for changes

    Example:
        >>> client = NotebookClient("http://localhost:8723")
        >>> notebooks = client.list_notebooks()
        >>> entry_id = client.write(
        ...     notebook_id="...",
        ...     content="Hello, world!",
        ...     content_type="text/plain"
        ... )
        >>> entry = client.read(notebook_id, entry_id)
    """

    def __init__(
        self,
        base_url: str = "http://localhost:3000",
        timeout: float = 30.0,
        author: Optional[str] = None,
    ):
        """
        Initialize the notebook client.

        Args:
            base_url: Base URL of the notebook server
            timeout: Request timeout in seconds
            author: Default author name for write operations
        """
        self.base_url = base_url.rstrip("/")
        self.timeout = timeout
        self.author = author or "anonymous"
        self._session = requests.Session()

    def _url(self, path: str) -> str:
        """Build full URL from path."""
        return f"{self.base_url}/{path.lstrip('/')}"

    def _handle_response(self, response: requests.Response) -> Dict[str, Any]:
        """
        Handle API response, raising appropriate errors.

        Args:
            response: The HTTP response

        Returns:
            Parsed JSON response data

        Raises:
            NotFoundError: Resource not found (404)
            PermissionError: Access denied (403)
            ValidationError: Invalid request (400/422)
            NotebookError: Other errors
        """
        try:
            data = response.json() if response.content else {}
        except ValueError:
            data = {"raw": response.text}

        if response.status_code >= 400:
            message = data.get("error", response.reason or "Unknown error")

            if response.status_code == 404:
                raise NotFoundError(message, response.status_code, data)
            elif response.status_code == 403:
                raise PermissionError(message, response.status_code, data)
            elif response.status_code in (400, 422):
                raise ValidationError(message, response.status_code, data)
            else:
                raise NotebookError(message, response.status_code, data)

        return data

    # -------------------------------------------------------------------------
    # Core Operations
    # -------------------------------------------------------------------------

    def write(
        self,
        notebook_id: str,
        content: str,
        content_type: str = "text/plain",
        topic: Optional[str] = None,
        references: Optional[List[str]] = None,
        author: Optional[str] = None,
    ) -> str:
        """
        Write a new entry to a notebook.

        Args:
            notebook_id: ID of the notebook to write to
            content: Entry content
            content_type: MIME type of content (default: text/plain)
            topic: Optional topic for clustering
            references: Optional list of entry IDs this entry references
            author: Author name (uses client default if not specified)

        Returns:
            The ID of the created entry

        Raises:
            NotFoundError: Notebook not found
            PermissionError: No write access
            ValidationError: Invalid request data
        """
        payload = {
            "content": content,
            "content_type": content_type,
            "author": author or self.author,
        }
        if topic is not None:
            payload["topic"] = topic
        if references:
            payload["references"] = references

        response = self._session.post(
            self._url(f"/notebooks/{notebook_id}/entries"),
            json=payload,
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return data["entry_id"]

    def write_full(
        self,
        notebook_id: str,
        content: str,
        content_type: str = "text/plain",
        topic: Optional[str] = None,
        references: Optional[List[str]] = None,
        author: Optional[str] = None,
    ) -> WriteResponse:
        """
        Write a new entry and return full response with integration cost.

        Same as write() but returns the complete WriteResponse including
        causal position and integration cost.

        Args:
            notebook_id: ID of the notebook to write to
            content: Entry content
            content_type: MIME type of content (default: text/plain)
            topic: Optional topic for clustering
            references: Optional list of entry IDs this entry references
            author: Author name (uses client default if not specified)

        Returns:
            WriteResponse with entry_id, causal_position, and integration_cost
        """
        payload = {
            "content": content,
            "content_type": content_type,
            "author": author or self.author,
        }
        if topic is not None:
            payload["topic"] = topic
        if references:
            payload["references"] = references

        response = self._session.post(
            self._url(f"/notebooks/{notebook_id}/entries"),
            json=payload,
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return WriteResponse.from_dict(data)

    def revise(
        self,
        notebook_id: str,
        entry_id: str,
        content: str,
        reason: Optional[str] = None,
        author: Optional[str] = None,
    ) -> str:
        """
        Revise an existing entry.

        Creates a new entry linked to the original via revision_of.
        The original entry remains unchanged.

        Args:
            notebook_id: ID of the notebook
            entry_id: ID of the entry to revise
            content: New content for the revision
            reason: Optional reason for the revision
            author: Author name (uses client default if not specified)

        Returns:
            The ID of the new revision entry

        Raises:
            NotFoundError: Notebook or entry not found
            PermissionError: No write access
        """
        payload = {
            "content": content,
            "author": author or self.author,
        }
        if reason is not None:
            payload["reason"] = reason

        response = self._session.put(
            self._url(f"/notebooks/{notebook_id}/entries/{entry_id}"),
            json=payload,
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return data["revision_id"]

    def revise_full(
        self,
        notebook_id: str,
        entry_id: str,
        content: str,
        reason: Optional[str] = None,
        author: Optional[str] = None,
    ) -> ReviseResponse:
        """
        Revise an entry and return full response with integration cost.

        Same as revise() but returns the complete ReviseResponse.

        Args:
            notebook_id: ID of the notebook
            entry_id: ID of the entry to revise
            content: New content for the revision
            reason: Optional reason for the revision
            author: Author name (uses client default if not specified)

        Returns:
            ReviseResponse with revision_id, original_id, causal_position,
            and integration_cost
        """
        payload = {
            "content": content,
            "author": author or self.author,
        }
        if reason is not None:
            payload["reason"] = reason

        response = self._session.put(
            self._url(f"/notebooks/{notebook_id}/entries/{entry_id}"),
            json=payload,
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return ReviseResponse.from_dict(data)

    def read(
        self,
        notebook_id: str,
        entry_id: str,
        revision: Optional[int] = None,
    ) -> Entry:
        """
        Read an entry from a notebook.

        Args:
            notebook_id: ID of the notebook
            entry_id: ID of the entry to read
            revision: Optional revision number (not currently implemented)

        Returns:
            The Entry object

        Raises:
            NotFoundError: Notebook or entry not found
            PermissionError: No read access
        """
        response = self._session.get(
            self._url(f"/notebooks/{notebook_id}/entries/{entry_id}"),
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return Entry.from_dict(data["entry"])

    def read_full(
        self,
        notebook_id: str,
        entry_id: str,
        revision: Optional[int] = None,
    ) -> ReadResponse:
        """
        Read an entry with its revision history.

        Args:
            notebook_id: ID of the notebook
            entry_id: ID of the entry to read
            revision: Optional revision number (not currently implemented)

        Returns:
            ReadResponse with entry and list of revisions
        """
        response = self._session.get(
            self._url(f"/notebooks/{notebook_id}/entries/{entry_id}"),
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return ReadResponse.from_dict(data)

    def browse(
        self,
        notebook_id: str,
        query: Optional[str] = None,
        max_tokens: int = 4000,
    ) -> Catalog:
        """
        Browse the notebook catalog.

        Returns a dense summary of notebook contents organized by topic,
        designed to fit within an LLM's attention budget.

        Args:
            notebook_id: ID of the notebook
            query: Optional search query to filter results
            max_tokens: Maximum token budget for the catalog (default: 4000)

        Returns:
            Catalog with clusters, entropy, and metadata

        Raises:
            NotFoundError: Notebook not found
            PermissionError: No read access
        """
        params = {}
        if query:
            params["query"] = query
        if max_tokens != 4000:
            params["max"] = str(max_tokens)

        response = self._session.get(
            self._url(f"/notebooks/{notebook_id}/browse"),
            params=params,
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return Catalog.from_dict(data)

    # -------------------------------------------------------------------------
    # Sharing Operations
    # -------------------------------------------------------------------------

    def share(
        self,
        notebook_id: str,
        author_id: str,
        read: bool = True,
        write: bool = False,
    ) -> ShareResponse:
        """
        Share a notebook with another entity.

        Args:
            notebook_id: ID of the notebook to share
            author_id: ID of the entity to share with
            read: Grant read access (default: True)
            write: Grant write access (default: False)

        Returns:
            ShareResponse with status and entity

        Raises:
            NotFoundError: Notebook not found
            PermissionError: Not the notebook owner
        """
        payload = {
            "entity": author_id,
            "read": read,
            "write": write,
        }
        response = self._session.post(
            self._url(f"/notebooks/{notebook_id}/share"),
            json=payload,
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return ShareResponse.from_dict(data)

    def revoke(
        self,
        notebook_id: str,
        author_id: str,
    ) -> None:
        """
        Revoke access to a notebook.

        Note: This operation may not be supported by all server implementations.
        The bootstrap server does not have a dedicated revoke endpoint.

        Args:
            notebook_id: ID of the notebook
            author_id: ID of the entity to revoke access from

        Raises:
            NotFoundError: Notebook not found
            PermissionError: Not the notebook owner
            NotebookError: Operation not supported
        """
        # Try DELETE endpoint first (Rust server)
        try:
            response = self._session.delete(
                self._url(f"/notebooks/{notebook_id}/share/{author_id}"),
                timeout=self.timeout,
            )
            self._handle_response(response)
        except (NotFoundError, NotebookError):
            # Fallback: use share with no permissions
            self.share(notebook_id, author_id, read=False, write=False)

    def participants(
        self,
        notebook_id: str,
    ) -> List[Participant]:
        """
        List participants of a notebook.

        Args:
            notebook_id: ID of the notebook

        Returns:
            List of Participant objects with permissions

        Raises:
            NotFoundError: Notebook not found
            PermissionError: No read access
        """
        # Try dedicated participants endpoint (Rust server)
        try:
            response = self._session.get(
                self._url(f"/notebooks/{notebook_id}/participants"),
                timeout=self.timeout,
            )
            data = self._handle_response(response)
            return [Participant.from_dict(p) for p in data.get("participants", [])]
        except NotFoundError:
            # Fallback: get from notebook metadata (bootstrap server)
            response = self._session.get(
                self._url(f"/notebooks/{notebook_id}"),
                timeout=self.timeout,
            )
            data = self._handle_response(response)
            return [Participant.from_dict(p) for p in data.get("participants", [])]

    # -------------------------------------------------------------------------
    # Observation
    # -------------------------------------------------------------------------

    def observe(
        self,
        notebook_id: str,
        since: int = 0,
    ) -> ObserveResponse:
        """
        Observe changes in a notebook since a given sequence.

        Args:
            notebook_id: ID of the notebook
            since: Sequence number to start from (0 for all)

        Returns:
            ObserveResponse with changes and notebook entropy

        Raises:
            NotFoundError: Notebook not found
            PermissionError: No read access
        """
        params = {"since": str(since)}
        response = self._session.get(
            self._url(f"/notebooks/{notebook_id}/observe"),
            params=params,
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return ObserveResponse.from_dict(data)

    # -------------------------------------------------------------------------
    # Discovery
    # -------------------------------------------------------------------------

    def list_notebooks(self) -> List[NotebookSummary]:
        """
        List all accessible notebooks.

        Returns:
            List of NotebookSummary objects

        Raises:
            PermissionError: No access
        """
        response = self._session.get(
            self._url("/notebooks"),
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return [NotebookSummary.from_dict(n) for n in data.get("notebooks", [])]

    def create_notebook(
        self,
        name: str,
        owner: Optional[str] = None,
    ) -> NotebookSummary:
        """
        Create a new notebook.

        Args:
            name: Name for the new notebook
            owner: Owner ID (uses client author if not specified)

        Returns:
            NotebookSummary for the created notebook
        """
        payload = {
            "name": name,
            "owner": owner or self.author,
        }
        response = self._session.post(
            self._url("/notebooks"),
            json=payload,
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return NotebookSummary.from_dict(data)

    def delete_notebook(
        self,
        notebook_id: str,
    ) -> None:
        """
        Delete a notebook.

        Note: This operation may not be supported by all server implementations.

        Args:
            notebook_id: ID of the notebook to delete

        Raises:
            NotFoundError: Notebook not found
            PermissionError: Not the notebook owner
            NotebookError: Operation not supported
        """
        response = self._session.delete(
            self._url(f"/notebooks/{notebook_id}"),
            timeout=self.timeout,
        )
        self._handle_response(response)

    def get_notebook(
        self,
        notebook_id: str,
    ) -> NotebookSummary:
        """
        Get notebook metadata.

        Args:
            notebook_id: ID of the notebook

        Returns:
            NotebookSummary for the notebook

        Raises:
            NotFoundError: Notebook not found
        """
        response = self._session.get(
            self._url(f"/notebooks/{notebook_id}"),
            timeout=self.timeout,
        )
        data = self._handle_response(response)
        return NotebookSummary.from_dict(data)

    # -------------------------------------------------------------------------
    # Session Management
    # -------------------------------------------------------------------------

    def close(self) -> None:
        """Close the client session."""
        self._session.close()

    def __enter__(self) -> "NotebookClient":
        """Context manager entry."""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        """Context manager exit."""
        self.close()
