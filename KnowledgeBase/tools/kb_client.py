"""Checked, UTF-8 subprocess access to the sibling KnowledgeBase tools.

This module does not load KnowledgeBase state when imported.  Callers must name
the required top-level result fields.  No operation is retried automatically.
An exception after a child starts does not establish that a write was rolled
back or that repeating the operation is safe; inspect ``write_outcome`` when
the child reports one, otherwise the outcome remains unknown.
"""

from __future__ import annotations

import json
import math
import subprocess
import sys
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Any


ALLOWED_TOOLS = frozenset({"kb.py", "consolidation_scope.py", "prepare.py"})
JSON_TYPES = frozenset({dict, list, str, int, float, bool, type(None)})
ExpectedType = type | tuple[type, ...]


class ToolCallError(RuntimeError):
    """Preserve the child's outcome, including output from failed reporting.

    ``stdout`` and ``stderr`` contain text when valid UTF-8, or the original
    bytes when decoding fails. ``parsed`` retains valid JSON even when the
    exit code, error flag, or expected result shape makes the call fail.
    ``write_outcome`` is the child's reported object, never an inferred
    rollback. Its absence means that no write outcome was established here.
    """

    def __init__(
        self,
        reason: str,
        message: str,
        *,
        command: Sequence[str],
        returncode: int | None = None,
        stdout: str | bytes = "",
        stderr: str | bytes = "",
        parsed: Any = None,
    ) -> None:
        super().__init__(message)
        self.reason = reason
        self.command = tuple(command)
        self.returncode = returncode
        self.stdout = stdout
        self.stderr = stderr
        self.parsed = parsed
        outcome = parsed.get("write_outcome") if isinstance(parsed, dict) else None
        self.write_outcome = outcome if isinstance(outcome, dict) else None


def _decode_capture(value: bytes | str | None) -> str | bytes:
    if value is None:
        return ""
    if isinstance(value, str):
        return value
    try:
        return value.decode("utf-8", errors="strict")
    except UnicodeDecodeError:
        return value


def _reject_constant(value: str) -> None:
    raise ValueError(f"non-JSON numeric constant: {value}")


def _unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON object key: {key!r}")
        result[key] = value
    return result


def _parse_capture(value: str | bytes) -> tuple[Any, str | None]:
    if isinstance(value, bytes):
        return None, "stdout is not valid UTF-8"
    try:
        return json.loads(
            value, parse_constant=_reject_constant, object_pairs_hook=_unique_object
        ), None
    except (ValueError, RecursionError) as exc:
        return None, str(exc)


def call_tool(
    tool_name: str,
    arguments: Sequence[str],
    *,
    expected: Mapping[str, ExpectedType],
    timeout: float = 120.0,
) -> dict[str, Any]:
    """Run an allowlisted sibling once and return its checked JSON object.

    ``tool_name`` is an exact filename from ``ALLOWED_TOOLS``. ``arguments`` is
    an argument vector, not a shell command. ``expected`` is a nonempty mapping
    from required top-level fields to JSON types, or nonempty tuples of those
    types. Types match exactly: a boolean does not satisfy an integer field.
    Additional result fields are retained. The child inherits the caller's
    working directory; use absolute paths when a path must be unambiguous.

    Invalid invocation configuration raises ``ValueError`` before starting a
    child. Process, output, and result failures raise ``ToolCallError``. UTF-8
    decoding is strict, and malformed output is never silently repaired.
    """
    if not isinstance(tool_name, str) or tool_name not in ALLOWED_TOOLS:
        raise ValueError("tool_name must be an exact allowlisted sibling filename")
    if isinstance(arguments, (str, bytes)) or not isinstance(arguments, Sequence):
        raise ValueError("arguments must be a sequence of strings")
    if any(not isinstance(arg, str) or "\x00" in arg for arg in arguments):
        raise ValueError("arguments must contain only strings without NUL characters")
    if not isinstance(expected, Mapping) or not expected:
        raise ValueError("expected must specify at least one required top-level field")
    expected_types: dict[str, tuple[type, ...]] = {}
    for field, required in expected.items():
        if not isinstance(field, str) or not field:
            raise ValueError("expected field names must be nonempty strings")
        types = required if isinstance(required, tuple) else (required,)
        if not types or any(not isinstance(item, type) or item not in JSON_TYPES for item in types):
            raise ValueError(f"expected type for {field!r} must be a JSON type or tuple of JSON types")
        expected_types[field] = types
    if (
        isinstance(timeout, bool)
        or not isinstance(timeout, (int, float))
        or not math.isfinite(timeout)
        or timeout <= 0
    ):
        raise ValueError("timeout must be a positive finite number of seconds")
    tools_directory = Path(__file__).resolve().parent
    try:
        target = (tools_directory / tool_name).resolve(strict=True)
    except OSError as exc:
        raise ValueError(f"allowlisted tool is unavailable: {tool_name}") from exc
    if target.parent != tools_directory or target.name != tool_name or not target.is_file():
        raise ValueError("tool must resolve to the named regular file in this module's directory")
    if not sys.executable:
        raise ValueError("the current Python executable is unavailable")
    command = [sys.executable, "-X", "utf8", "-B", str(target), *arguments]
    try:
        completed = subprocess.run(
            command, capture_output=True, check=False, timeout=timeout
        )
    except subprocess.TimeoutExpired as exc:
        stdout = _decode_capture(exc.stdout)
        stderr = _decode_capture(exc.stderr)
        parsed, _ = _parse_capture(stdout)
        raise ToolCallError(
            "timeout",
            "Tool timed out; completion and any unreported write outcome are unknown.",
            command=command, stdout=stdout, stderr=stderr, parsed=parsed,
        ) from exc
    except OSError as exc:
        raise ToolCallError(
            "launch_failed", f"Could not start tool: {exc}", command=command
        ) from exc

    stdout = _decode_capture(completed.stdout)
    stderr = _decode_capture(completed.stderr)
    parsed, parse_error = _parse_capture(stdout)
    details = {
        "command": command,
        "returncode": completed.returncode,
        "stdout": stdout,
        "stderr": stderr,
        "parsed": parsed,
    }
    if completed.returncode != 0:
        message = parsed.get("error") if isinstance(parsed, dict) else None
        raise ToolCallError(
            "exit_status",
            f"Tool exited with status {completed.returncode}: {message or 'inspect captured output'}",
            **details,
        )
    if isinstance(stdout, bytes) or isinstance(stderr, bytes):
        raise ToolCallError("encoding", "Tool output is not valid UTF-8.", **details)
    if parse_error is not None:
        raise ToolCallError("invalid_json", f"Tool output is not valid JSON: {parse_error}", **details)
    if not isinstance(parsed, dict):
        raise ToolCallError("result_shape", "Tool output must be a JSON object.", **details)
    if parsed.get("ok") is False:
        raise ToolCallError("reported_error", f"Tool reported failure: {parsed.get('error', 'ok:false')}", **details)
    for field, types in expected_types.items():
        if field not in parsed:
            raise ToolCallError("result_shape", f"Tool result is missing required field {field!r}.", **details)
        if type(parsed[field]) not in types:
            names = " or ".join(item.__name__ for item in types)
            raise ToolCallError("result_shape", f"Tool result field {field!r} must be {names}.", **details)
    return parsed
