"""
Exception classes for the notebook client library.

Provides a hierarchy of exceptions for different error conditions:
- NotebookError: Base exception for all notebook-related errors
- NotFoundError: Resource not found (404)
- PermissionError: Access denied (403)
- ValidationError: Invalid request data (400/422)
"""

from typing import Any, Optional


class NotebookError(Exception):
    """
    Base exception for all notebook client errors.

    Attributes:
        message: Human-readable error message
        status_code: HTTP status code if applicable
        response: Raw response data for debugging
    """

    def __init__(
        self,
        message: str,
        status_code: Optional[int] = None,
        response: Optional[Any] = None,
    ):
        super().__init__(message)
        self.message = message
        self.status_code = status_code
        self.response = response

    def __str__(self) -> str:
        if self.status_code:
            return f"{self.message} (HTTP {self.status_code})"
        return self.message


class NotFoundError(NotebookError):
    """
    Raised when a requested resource does not exist.

    This includes:
    - Notebook not found
    - Entry not found
    - Revision not found
    """

    def __init__(
        self,
        message: str = "Resource not found",
        status_code: int = 404,
        response: Optional[Any] = None,
    ):
        super().__init__(message, status_code, response)


class PermissionError(NotebookError):
    """
    Raised when access to a resource is denied.

    This includes:
    - No read access to notebook
    - No write access to notebook
    - Not owner (for share/revoke operations)
    """

    def __init__(
        self,
        message: str = "Permission denied",
        status_code: int = 403,
        response: Optional[Any] = None,
    ):
        super().__init__(message, status_code, response)


class ValidationError(NotebookError):
    """
    Raised when request data is invalid.

    This includes:
    - Missing required fields
    - Invalid field values
    - Malformed request body
    """

    def __init__(
        self,
        message: str = "Validation error",
        status_code: int = 400,
        response: Optional[Any] = None,
    ):
        super().__init__(message, status_code, response)
