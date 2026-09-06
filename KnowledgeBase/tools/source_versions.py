"""Explicit source lineage and read-only, local historical-byte verification.

This module never fetches, checks out files, commits, or writes KB state.
Git history is evidence for a named source version, not a backup of the KB.
"""
from __future__ import annotations

import hashlib
import os
import re
import stat
import subprocess
from pathlib import Path, PurePosixPath


class VersionError(ValueError):
    pass


def _inside(path: Path, root: Path) -> bool:
    return path.resolve().is_relative_to(root.resolve())


def _oid(value) -> bool:
    return isinstance(value, str) and re.fullmatch(r"(?:[0-9a-f]{40}|[0-9a-f]{64})", value) is not None


def git_blob(root: Path, commit: str, path: str) -> tuple[bytes, str]:
    """Read one regular blob from a pinned commit in this root's local repo."""
    if not _oid(commit):
        raise VersionError("Git history requires a full lowercase commit object ID")
    if (not isinstance(path, str) or not path or "\\" in path or ":" in path
            or "\x00" in path or path.startswith("/")
            or any(part in {"", ".", ".."} for part in path.split("/"))
            or PurePosixPath(path).parts[0].casefold() in {".git", "knowledgebase"}
            or not _inside(root / path, root)):
        raise VersionError("Git source path must stay inside the experiment corpus")
    git_dir = root / ".git"
    if not git_dir.is_dir() or git_dir.resolve() != root.resolve() / ".git":
        raise VersionError("Git history requires a local .git directory in the exact root")
    # Linked worktrees and shared object stores need a separately scoped adapter.
    for name in ("commondir", "objects/info/alternates", "objects/info/http-alternates"):
        if (git_dir / name).exists():
            raise VersionError("Git history cannot use a shared/outside object store")
    if not _inside(git_dir / "objects", git_dir):
        raise VersionError("Git object store escapes the local repository")
    env = {key: value for key, value in os.environ.items() if not key.upper().startswith("GIT_")}
    env.update(GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull,
               GIT_TERMINAL_PROMPT="0", GIT_NO_LAZY_FETCH="1")

    def run(*args: str) -> bytes:
        try:
            result = subprocess.run(
                ["git", "--no-optional-locks", "--no-replace-objects", "--literal-pathspecs",
                 "-c", "protocol.allow=never", "--git-dir", str(git_dir), *args],
                cwd=root, env=env, stdin=subprocess.DEVNULL, capture_output=True, timeout=15,
            )
        except (OSError, subprocess.TimeoutExpired) as exc:
            raise VersionError(f"local Git verification unavailable: {type(exc).__name__}") from exc
        if result.returncode:
            raise VersionError("local Git object unavailable or command failed; no fetch attempted")
        return result.stdout

    if run("cat-file", "-t", commit).strip() != b"commit":
        raise VersionError("Git history object is not a commit")
    entries = run("ls-tree", "-z", commit, "--", path).rstrip(b"\x00").split(b"\x00")
    if len(entries) != 1 or b"\t" not in entries[0]:
        raise VersionError("Git history path does not identify one blob")
    header, actual_path = entries[0].split(b"\t", 1)
    fields = header.split()
    if (len(fields) != 3 or fields[0] not in {b"100644", b"100755"}
            or fields[1] != b"blob" or actual_path != path.encode("utf-8")):
        raise VersionError("Git history path must identify a regular file, not a link or submodule")
    blob_oid = fields[2].decode("ascii")
    size = int(run("cat-file", "-s", blob_oid).strip())
    if size > 16 * 1024 * 1024:
        raise VersionError("Git evidence blob exceeds the 16 MiB local verification limit")
    data = run("cat-file", "blob", blob_oid)
    if len(data) != size:
        raise VersionError("Git blob size changed during verification")
    return data, blob_oid


def transformed(data: bytes, transform: str) -> bytes:
    if transform == "raw":
        return data
    if transform == "lf_to_crlf" and b"\r" not in data:
        return data.replace(b"\n", b"\r\n")
    raise VersionError("unsupported Git byte transform (only raw or LF to CRLF)")


def git_history(root: Path, source: dict, commit: str) -> dict:
    path = Path(source["locator"]["path"]).resolve().relative_to(root.resolve()).as_posix()
    data, oid = git_blob(root, commit, path)
    for transform in ("raw", "lf_to_crlf"):
        if transform == "lf_to_crlf" and b"\r" in data:
            continue
        candidate = transformed(data, transform)
        if (hashlib.sha256(candidate).hexdigest().upper() == source["sha256"].upper()
                and (source.get("size_bytes") is None or len(candidate) == source["size_bytes"])):
            return {"kind": "git", "commit": commit, "path": path,
                    "blob_oid": oid, "transform": transform}
    raise VersionError("Git commit does not reproduce the registered source's exact bytes")


def _snapshot_relative(path: str) -> PurePosixPath:
    """Validate metadata without reading any snapshot or following a link."""
    if (not isinstance(path, str) or not path or any(c in path for c in '\\:\x00<>"|?*')
            or path.startswith("/") or any(part in {"", ".", ".."}
            or part.endswith((".", " ")) for part in path.split("/"))):
        raise VersionError("KB snapshot path must be a strict KB-relative source-history file path")
    relative = PurePosixPath(path)
    if len(relative.parts) < 2 or relative.parts[0] != "source-history":
        raise VersionError("KB snapshot path must stay inside KnowledgeBase/source-history")
    return relative


def _snapshot_file(root: Path, path: str) -> tuple[Path, os.stat_result]:
    relative = _snapshot_relative(path)
    if not root.is_absolute():
        raise VersionError("KB snapshot verification requires an exact absolute experiment root")
    candidate = root / "KnowledgeBase" / relative
    current = root
    parts = ("KnowledgeBase", *relative.parts)
    # lstat each component before resolving/opening so even in-root links are refused.
    for index in range(len(parts) + 1):
        info = current.lstat()
        if (stat.S_ISLNK(info.st_mode)
                or getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT):
            raise VersionError("KB snapshot paths cannot contain symlinks or reparse points")
        expected = stat.S_ISREG if index == len(parts) else stat.S_ISDIR
        if not expected(info.st_mode):
            raise VersionError("KB snapshot requires regular files and ordinary parent directories")
        if index < len(parts):
            current /= parts[index]
    if root.resolve() != root or candidate.resolve() != candidate:
        raise VersionError("KB snapshot path does not identify the exact physical KB location")
    if info.st_size > 16 * 1024 * 1024:
        raise VersionError("KB snapshot exceeds the 16 MiB local verification limit")
    return candidate, info


def _file_identity(info: os.stat_result) -> tuple:
    # On this Windows host lstat/fstat ctime differs for some just-written files.
    # Identity, type, length, mtime and exact catalog SHA still remain mandatory.
    return (info.st_dev, info.st_ino, info.st_mode, info.st_size,
            info.st_mtime_ns, getattr(info, "st_file_attributes", 0))


def snapshot_bytes(root: Path, path: str) -> bytes:
    """Read exact bounded snapshot bytes; never create/copy/normalize a file."""
    try:
        candidate, before = _snapshot_file(root, path)
        flags = os.O_RDONLY | getattr(os, "O_BINARY", 0) | getattr(os, "O_NOFOLLOW", 0)
        descriptor = os.open(candidate, flags)
        with os.fdopen(descriptor, "rb") as handle:
            if _file_identity(os.fstat(handle.fileno())) != _file_identity(before):
                raise VersionError("KB snapshot changed before its bytes could be read")
            data = handle.read(16 * 1024 * 1024 + 1)
            opened_after = os.fstat(handle.fileno())
        _, after = _snapshot_file(root, path)
        if (len(data) != before.st_size or _file_identity(before) != _file_identity(opened_after)
                or _file_identity(before) != _file_identity(after)):
            raise VersionError("KB snapshot changed during verification")
        return data
    except (OSError, RuntimeError) as exc:
        raise VersionError(f"KB snapshot unavailable or unsafe: {type(exc).__name__}") from exc


def snapshot_history(root: Path, source: dict, history_file: str) -> dict:
    """Build history metadata only when an already-present snapshot matches."""
    try:
        absolute = Path(history_file)
        if not absolute.is_absolute() or any(part == ".." for part in absolute.parts):
            raise ValueError("not an exact absolute file path")
        relative = absolute.relative_to(root / "KnowledgeBase").as_posix()
    except (TypeError, ValueError) as exc:
        raise VersionError("--history-file must name an exact absolute KB/source-history file") from exc
    data = snapshot_bytes(root, relative)
    if (hashlib.sha256(data).hexdigest().upper() != str(source["sha256"]).upper()
            or (source.get("size_bytes") is not None and len(data) != source["size_bytes"])):
        raise VersionError("KB snapshot does not reproduce the registered source's exact bytes/size")
    return {"kind": "kb_snapshot", "path": relative}


def validate_lineage(sources: dict, record_ids: set, root: Path) -> list[str]:
    errors = []
    for sid, source in sources.items():
        revision = source.get("supersession")
        predecessor = source.get("supersedes")
        if predecessor is not None:
            prior = sources.get(predecessor) if isinstance(predecessor, str) else None
            prior_revision = prior.get("supersession") if isinstance(prior, dict) else None
            if not isinstance(prior_revision, dict) or prior_revision.get("source_id") != sid:
                errors.append(f"source {sid} has a broken predecessor link")
        if revision is None:
            continue
        if not isinstance(revision, dict):
            errors.append(f"source {sid} supersession must be an object")
            continue
        successor_id = revision.get("source_id")
        successor = sources.get(successor_id) if isinstance(successor_id, str) else None
        locator = source.get("locator")
        if (not successor or successor_id == sid or successor.get("supersedes") != sid
                or source.get("locator") != successor.get("locator")
                or not isinstance(locator, dict) or locator.get("type") != "file"
                or str(source.get("sha256", "")).upper() == str(successor.get("sha256", "")).upper()):
            errors.append(f"source {sid} needs a distinct successor version at the same file locator")
        refs = revision.get("record_ids")
        if (any(not isinstance(revision.get(key), str) or not revision[key].strip()
                for key in ("reason", "actor", "observed_at"))
                or not isinstance(refs, list) or not refs
                or any(not isinstance(ref, str) or ref not in record_ids for ref in refs)):
            errors.append(f"source {sid} has untraceable supersession metadata")
        if source.get("availability") is not None:
            errors.append(f"source {sid} cannot mix supersession and original-unavailable metadata")
        history = revision.get("history")
        if not isinstance(history, dict):
            errors.append(f"source {sid} needs explicit history verification metadata")
        elif history.get("kind") == "unavailable":
            if not isinstance(history.get("reason"), str) or not history["reason"].strip():
                errors.append(f"source {sid} unavailable history requires a reason")
        elif history.get("kind") == "git":
            try:
                expected_path = Path(source["locator"]["path"]).resolve().relative_to(root.resolve()).as_posix()
            except (KeyError, TypeError, ValueError):
                expected_path = None
            if (not _oid(history.get("commit")) or not _oid(history.get("blob_oid"))
                    or not expected_path or history.get("path") != expected_path
                    or not isinstance(history.get("transform"), str)
                    or history.get("transform") not in {"raw", "lf_to_crlf"}):
                errors.append(f"source {sid} has invalid Git history metadata")
        elif history.get("kind") == "kb_snapshot":
            try:
                _snapshot_relative(history.get("path"))
            except VersionError as exc:
                errors.append(f"source {sid} has invalid KB snapshot metadata: {exc}")
        else:
            errors.append(f"source {sid} has unsupported history verification kind")
    for sid in sources:
        seen = set()
        current = sid
        while isinstance(current, str) and current in sources:
            if current in seen:
                errors.append(f"source {sid} lineage contains a cycle")
                break
            seen.add(current)
            revision = sources[current].get("supersession")
            current = revision.get("source_id") if isinstance(revision, dict) else None
    return errors


def verify_history(root: Path, source: dict) -> dict:
    history = source["supersession"]["history"]
    if history["kind"] == "unavailable":
        return {"source_sha256": None, "history_verified": False,
                "warnings": [f"source {source['source_id']} historical bytes unavailable: {history['reason']}"]}
    if history["kind"] == "kb_snapshot":
        data = snapshot_bytes(root, history["path"])
        label = "KB snapshot"
    elif history["kind"] == "git":
        data, oid = git_blob(root, history["commit"], history["path"])
        if oid != history["blob_oid"]:
            raise VersionError("Git commit/path does not match the registered blob object")
        data = transformed(data, history["transform"])
        label = "Git"
    else:
        raise VersionError("unsupported history verification kind")
    sha = hashlib.sha256(data).hexdigest().upper()
    if sha != str(source["sha256"]).upper():
        raise VersionError(f"historical {label} bytes do not match the registered source SHA-256")
    if source.get("size_bytes") is not None and len(data) != source["size_bytes"]:
        raise VersionError(f"historical {label} bytes do not match the registered source size")
    return {"source_sha256": sha, "history_verified": True, "warnings": []}
