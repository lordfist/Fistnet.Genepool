#!/usr/bin/env python3
"""Small, deterministic interface to the Seed Analyzer KnowledgeBase.

This tool intentionally uses only the Python standard library.  It reads the
original experiment corpus but writes only the KnowledgeBase directory.  The
`apply` command is an optimistic, append-friendly maintenance transaction:
the caller must name the revision it read and no record/source deletion is
supported.
"""

from __future__ import annotations

import argparse
import copy
import datetime as dt
import hashlib
import json
import os
import re
import sys
import tempfile
from pathlib import Path
from typing import Any, Iterable

import source_versions


KB_ROOT = Path(__file__).resolve().parent.parent
EXPERIMENT_ROOT = KB_ROOT.parent.resolve()
CORE_PATH = KB_ROOT / "core.json"
RECORDS_PATH = KB_ROOT / "records.jsonl"
SOURCES_PATH = KB_ROOT / "sources.json"

RECORD_ID_RE = re.compile(r"^(?:DEC|FACT|CLAIM|INF|ASM|REC|Q)-\d{4}$")
SOURCE_ID_RE = re.compile(r"^SRC-\d{4}$")
ALLOWED_KINDS = {
    "owner_decision",
    "observed_fact",
    "source_claim",
    "inference",
    "assumption",
    "recommendation",
    "open_question",
}
ALLOWED_STATUSES = {
    "active",
    "provisional",
    "historical",
    "superseded",
    "disputed",
    "open",
    "resolved",
}


class KBError(RuntimeError):
    """A concise, user-actionable KnowledgeBase error."""

    def __init__(self, message: str, **details: Any):
        super().__init__(message)
        self.details = details


class JSONArgumentParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        raise KBError(message, code="invalid_arguments")


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat()


def emit(value: Any, *, compact: bool = False) -> None:
    # Configure the tool boundary itself, including errors and imported callers.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="strict")
    text = json.dumps(
        value,
        ensure_ascii=False,
        indent=None if compact else 2,
        separators=(",", ":") if compact else None,
        allow_nan=False,
    )
    sys.stdout.write(text + "\n")
    sys.stdout.flush()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def canonical_json_bytes(value: Any) -> bytes:
    try:
        text = json.dumps(value, ensure_ascii=False, indent=2, sort_keys=False, allow_nan=False)
    except (TypeError, ValueError) as exc:
        raise KBError(f"value cannot be serialized as strict JSON: {exc}") from exc
    return (text + "\n").encode("utf-8")


def canonical_jsonl_bytes(rows: Iterable[dict[str, Any]]) -> bytes:
    try:
        return (
            "".join(
                json.dumps(row, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n"
                for row in rows
            )
        ).encode("utf-8")
    except (TypeError, ValueError) as exc:
        raise KBError(f"value cannot be serialized as strict JSONL: {exc}") from exc


def _reject_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise KBError(f"duplicate JSON object key: {key!r}")
        result[key] = value
    return result


def _reject_constant(value: str) -> None:
    raise KBError(f"non-finite JSON number is not allowed: {value}")


def strict_json_loads(text: str, label: str) -> Any:
    try:
        return json.loads(
            text,
            object_pairs_hook=_reject_pairs,
            parse_constant=_reject_constant,
        )
    except KBError:
        raise
    except json.JSONDecodeError as exc:
        raise KBError(f"invalid JSON in {label}: {exc}") from exc


def read_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8-sig") as handle:
            return strict_json_loads(handle.read(), str(path))
    except FileNotFoundError as exc:
        raise KBError(f"required file is missing: {path}") from exc
    except UnicodeDecodeError as exc:
        raise KBError(f"invalid UTF-8 in JSON file: {path}", code="invalid_encoding") from exc


def read_jsonl(path: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    try:
        with path.open("r", encoding="utf-8-sig") as handle:
            for line_number, raw in enumerate(handle, 1):
                if not raw.strip():
                    continue
                try:
                    value = strict_json_loads(raw, f"{path} line {line_number}")
                except KBError as exc:
                    raise KBError(f"invalid JSONL in {path} at line {line_number}: {exc}") from exc
                if not isinstance(value, dict):
                    raise KBError(
                        f"JSONL row in {path} at line {line_number} is not an object"
                    )
                rows.append(value)
    except FileNotFoundError as exc:
        raise KBError(f"required file is missing: {path}") from exc
    return rows


def load_state() -> tuple[dict[str, Any], list[dict[str, Any]], dict[str, Any]]:
    core = read_json(CORE_PATH)
    records = read_jsonl(RECORDS_PATH)
    sources = read_json(SOURCES_PATH)
    if not isinstance(core, dict) or not isinstance(sources, dict):
        raise KBError("core.json and sources.json must contain JSON objects")
    state_files = core.get("state_files")
    if isinstance(state_files, dict):
        expected_records = state_files.get("records_jsonl_sha256")
        expected_sources = state_files.get("sources_json_sha256")
        if expected_records and sha256_file(RECORDS_PATH) != str(expected_records).upper():
            raise KBError(
                "RECOVERY_REQUIRED: records.jsonl does not match the state committed by core.json"
            )
        if expected_sources and sha256_file(SOURCES_PATH) != str(expected_sources).upper():
            raise KBError(
                "RECOVERY_REQUIRED: sources.json does not match the state committed by core.json"
            )
    return core, records, sources


def within(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except (OSError, ValueError):
        return False


def estimate_tokens(value: Any) -> int:
    # Conservative and dependency-free: non-ASCII JSON often tokenizes more
    # densely than the familiar four-English-characters rule.
    payload = json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    return max(1, (len(payload) + 2) // 3)


def source_rows(sources: dict[str, Any]) -> list[dict[str, Any]]:
    rows = sources.get("sources")
    if not isinstance(rows, list):
        raise KBError("sources.json field 'sources' must be an array")
    return rows


def source_local_path(source: dict[str, Any]) -> Path | None:
    locator = source.get("locator")
    if not isinstance(locator, dict):
        return None
    if locator.get("type") != "file":
        return None
    value = locator.get("path")
    if not isinstance(value, str) or not value:
        return None
    return Path(value)


def prepared_path(source: dict[str, Any]) -> Path | None:
    prep = source.get("preparation")
    if not isinstance(prep, dict):
        return None
    value = prep.get("path")
    if not isinstance(value, str) or not value:
        return None
    candidate = (KB_ROOT / value).resolve()
    return candidate if within(candidate, KB_ROOT) else None


def _duplicates(values: Iterable[str]) -> list[str]:
    seen: set[str] = set()
    duplicates: set[str] = set()
    for value in values:
        if value in seen:
            duplicates.add(value)
        seen.add(value)
    return sorted(duplicates)


def validate_state(
    core: dict[str, Any],
    records: list[dict[str, Any]],
    sources: dict[str, Any],
    *,
    deep: bool = False,
    prepared_file_overrides: dict[Path, Path] | None = None,
) -> dict[str, Any]:
    errors: list[str] = []
    warnings: list[str] = []
    # A preparer can validate owned staging bytes without publishing them or
    # rewriting the candidate's logical catalog paths. Never redirect originals.
    prepared_overrides: dict[Path, Path] = {}
    used_prepared_overrides: set[Path] = set()
    if prepared_file_overrides is not None:
        if not isinstance(prepared_file_overrides, dict):
            errors.append("prepared file overrides must be a path mapping")
        else:
            for logical, staged in prepared_file_overrides.items():
                if not isinstance(logical, Path) or not isinstance(staged, Path):
                    errors.append("prepared file override keys and values must be Path objects")
                    continue
                logical_path, staged_path = logical.resolve(), staged.resolve()
                if not within(logical_path, KB_ROOT) or not within(staged_path, KB_ROOT):
                    errors.append("prepared file override logical and staged paths must remain inside KnowledgeBase")
                elif logical_path in prepared_overrides:
                    errors.append("duplicate resolved prepared file override")
                else:
                    prepared_overrides[logical_path] = staged_path

    if core.get("schema_version") != 1:
        errors.append("core.json schema_version must be 1")
    experiment = core.get("experiment")
    if not isinstance(experiment, dict) or experiment.get("root") != str(EXPERIMENT_ROOT):
        errors.append("core.json experiment.root does not match the exact tool-bound experiment root")
    revision = core.get("kb_revision")
    if not isinstance(revision, int) or revision < 1:
        errors.append("core.json kb_revision must be a positive integer")
    core_tokens = estimate_tokens(core)
    if core_tokens > 6000:
        errors.append(f"core.json is too large for the lightweight contract ({core_tokens} estimated tokens)")
    if core_tokens > 5000:
        warnings.append(f"core.json exceeds the preferred 5000-token target ({core_tokens} estimated tokens)")
    state_files = core.get("state_files")
    if not isinstance(state_files, dict):
        errors.append("core.json must declare state_files hashes")
    else:
        for field in ("records_jsonl_sha256", "sources_json_sha256"):
            value = state_files.get(field)
            if not isinstance(value, str) or not re.fullmatch(r"[0-9A-Fa-f]{64}", value):
                errors.append(f"core.json state_files.{field} must be a SHA-256")

    record_ids = [str(row.get("id", "")) for row in records]
    for duplicate in _duplicates(record_ids):
        errors.append(f"duplicate record id: {duplicate}")
    record_map = {str(row.get("id")): row for row in records if row.get("id")}

    try:
        sources_list = source_rows(sources)
    except KBError as exc:
        errors.append(str(exc))
        sources_list = []
    source_ids = [str(row.get("source_id", "")) for row in sources_list]
    for duplicate in _duplicates(source_ids):
        errors.append(f"duplicate source id: {duplicate}")
    source_map = {
        str(row.get("source_id")): row for row in sources_list if row.get("source_id")
    }
    lineage_errors = source_versions.validate_lineage(source_map, set(record_map), EXPERIMENT_ROOT)
    errors.extend(lineage_errors)

    for index, row in enumerate(records, 1):
        record_id = row.get("id")
        label = str(record_id or f"line {index}")
        if not isinstance(record_id, str) or not RECORD_ID_RE.fullmatch(record_id):
            errors.append(f"invalid record id at records.jsonl line {index}: {record_id!r}")
        if row.get("kind") not in ALLOWED_KINDS:
            errors.append(f"record {label} has invalid kind: {row.get('kind')!r}")
        if row.get("status") not in ALLOWED_STATUSES:
            errors.append(f"record {label} has invalid status: {row.get('status')!r}")
        if not isinstance(row.get("statement"), str) or not row.get("statement", "").strip():
            errors.append(f"record {label} has no statement")
        tags = row.get("tags", [])
        if not isinstance(tags, list) or any(not isinstance(tag, str) for tag in tags):
            errors.append(f"record {label} tags must be an array of strings")
        refs = row.get("source_refs", [])
        if not isinstance(refs, list) or any(not isinstance(ref, str) for ref in refs):
            errors.append(f"record {label} source_refs must be an array of source ids")
        else:
            for ref in refs:
                if ref not in source_map:
                    errors.append(f"record {label} has broken source reference: {ref}")
        related = row.get("related_record_ids", [])
        if not isinstance(related, list) or any(not isinstance(ref, str) for ref in related):
            errors.append(f"record {label} related_record_ids must be an array of record ids")
        else:
            for ref in related:
                if ref not in record_map:
                    errors.append(f"record {label} has broken related record reference: {ref}")

    for field in ("pinned_record_ids", "owner_decision_ids", "open_question_ids"):
        refs = core.get(field, [])
        if not isinstance(refs, list):
            errors.append(f"core.json {field} must be an array")
            continue
        for ref in refs:
            if ref not in record_map:
                errors.append(f"core.json {field} has broken record reference: {ref}")
    routes = core.get("routes", {})
    if not isinstance(routes, dict):
        errors.append("core.json routes must be an object")
    else:
        for route_name, route in routes.items():
            if not isinstance(route, dict) or not isinstance(route.get("record_ids", []), list):
                errors.append(f"core.json route {route_name!r} is invalid")
                continue
            for ref in route.get("record_ids", []):
                if ref not in record_map:
                    errors.append(f"core.json route {route_name!r} has broken record reference: {ref}")
    for ref in core.get("next_decision_order", []):
        if ref not in record_map:
            errors.append(f"core.json next_decision_order has broken record reference: {ref}")

    for index, source in enumerate(sources_list, 1):
        source_id = source.get("source_id")
        label = str(source_id or f"source {index}")
        if not isinstance(source_id, str) or not SOURCE_ID_RE.fullmatch(source_id):
            errors.append(f"invalid source id at sources.json item {index}: {source_id!r}")
        locator = source.get("locator")
        if not isinstance(locator, dict) or locator.get("type") not in {"file", "url"}:
            errors.append(f"source {label} locator must be a file or url object")
            continue
        if locator.get("type") == "url":
            if (
                not isinstance(locator.get("url"), str)
                or not re.match(r"^https://", locator.get("url", ""), re.I)
            ):
                errors.append(f"source {label} has no URL")
            continue

        duplicate_of = source.get("duplicate_of")
        if duplicate_of is not None:
            if duplicate_of == source_id:
                errors.append(f"source {label} cannot duplicate itself")
            elif duplicate_of not in source_map:
                errors.append(f"source {label} has broken duplicate_of reference: {duplicate_of}")
            elif str(source_map[duplicate_of].get("sha256", "")).upper() != str(source.get("sha256", "")).upper():
                errors.append(f"source {label} duplicate_of target has a different SHA-256")

        path = source_local_path(source)
        if path is None:
            errors.append(f"source {label} has no file path")
            continue
        availability = source.get("availability")
        declared_unavailable = False
        if availability is not None:
            if not isinstance(availability, dict):
                errors.append(f"source {label} availability must be an object")
            else:
                refs = availability.get("record_ids", [])
                valid_availability = (
                    availability.get("status") == "original_unavailable"
                    and isinstance(availability.get("reason"), str)
                    and bool(availability["reason"].strip())
                    and isinstance(availability.get("observed_at"), str)
                    and bool(availability["observed_at"].strip())
                    and isinstance(refs, list)
                    and bool(refs)
                    and all(isinstance(ref, str) and ref in record_map for ref in refs)
                )
                if not valid_availability:
                    errors.append(f"source {label} has invalid or untraceable availability metadata")
                else:
                    declared_unavailable = True
        try:
            resolved = path.resolve(strict=deep and not declared_unavailable and source.get("supersession") is None)
        except OSError as exc:
            errors.append(f"source {label} cannot be resolved: {exc}")
            continue
        if not within(resolved, EXPERIMENT_ROOT):
            errors.append(f"source {label} escapes the experiment root: {path}")
            continue
        if within(resolved, KB_ROOT):
            errors.append(f"source {label} points into the derived KnowledgeBase: {path}")
            continue
        expected_sha = source.get("sha256")
        if not isinstance(expected_sha, str) or not re.fullmatch(r"[0-9A-Fa-f]{64}", expected_sha):
            errors.append(f"source {label} has no valid SHA-256")
        if declared_unavailable:
            if not resolved.exists():
                warnings.append(
                    f"source {label} original unavailable; historical identity retained, "
                    "original bytes not verified"
                )
            else:
                warnings.append(f"source {label} marked unavailable but path exists; reconcile availability")
        historical = source.get("supersession") is not None
        if deep and historical and not lineage_errors:
            try:
                history_result = source_versions.verify_history(EXPERIMENT_ROOT, source)
                warnings.extend(history_result["warnings"])
            except source_versions.VersionError as exc:
                errors.append(f"source {label} history verification failed: {exc}")
        if deep and not historical and resolved.exists() and not resolved.is_file():
            errors.append(f"source {label} path is not a regular file")
        if deep and not historical and resolved.is_file() and isinstance(expected_sha, str):
            actual_sha = sha256_file(resolved)
            if actual_sha.upper() != expected_sha.upper():
                errors.append(
                    f"source {label} is stale: expected {expected_sha.upper()}, actual {actual_sha}"
                )
            if isinstance(source.get("size_bytes"), int) and resolved.stat().st_size != source["size_bytes"]:
                errors.append(f"source {label} size metadata mismatch")

        prep = source.get("preparation")
        if prep is None:
            continue
        if not isinstance(prep, dict):
            errors.append(f"source {label} preparation must be an object")
            continue
        prep_status = prep.get("status")
        if prep_status not in {"registered", "registered_only", "prepared", "partial"}:
            errors.append(f"source {label} has invalid preparation status: {prep_status!r}")
        if prep_status in {"prepared", "partial"}:
            prep_path = prepared_path(source)
            if prep_path is None:
                errors.append(f"source {label} has an invalid prepared path")
                continue
            if prep_path in prepared_overrides:
                used_prepared_overrides.add(prep_path)
                prep_path = prepared_overrides[prep_path]
            if not prep_path.is_file():
                errors.append(f"source {label} prepared file is missing: {prep_path}")
                continue
            if prep.get("source_sha256", "").upper() != str(expected_sha).upper():
                errors.append(f"source {label} preparation is bound to a different source hash")
            expected_prep_sha = prep.get("prepared_sha256")
            if deep:
                if not isinstance(expected_prep_sha, str) or not re.fullmatch(
                    r"[0-9A-Fa-f]{64}", expected_prep_sha
                ):
                    errors.append(f"source {label} has no valid prepared SHA-256")
                elif sha256_file(prep_path) != expected_prep_sha.upper():
                    errors.append(f"source {label} prepared representation hash mismatch")
                try:
                    prepared_rows = read_jsonl(prep_path)
                except KBError as exc:
                    errors.append(str(exc))
                else:
                    first = prepared_rows[0] if prepared_rows else {}
                    if first.get("type") != "meta" or first.get("source_id") != source_id:
                        errors.append(f"source {label} prepared meta record is invalid")
                    if str(first.get("source_sha256", "")).upper() != str(expected_sha).upper():
                        errors.append(f"source {label} prepared meta source hash mismatch")
                    if first.get("coverage") != prep.get("coverage"):
                        errors.append(f"source {label} preparation coverage differs from prepared meta")
                    locators = [
                        str(row.get("locator"))
                        for row in prepared_rows[1:]
                        if row.get("locator") is not None
                    ]
                    for duplicate in _duplicates(locators):
                        errors.append(f"source {label} has duplicate prepared locator: {duplicate}")

        assets = source.get("selected_assets", [])
        if not isinstance(assets, list):
            errors.append(f"source {label} selected_assets must be an array")
        else:
            for asset in assets:
                if not isinstance(asset, dict) or not isinstance(asset.get("path"), str):
                    errors.append(f"source {label} has an invalid selected asset")
                    continue
                asset_path = (KB_ROOT / asset["path"]).resolve()
                if not within(asset_path, KB_ROOT) or not asset_path.is_file():
                    errors.append(f"source {label} selected asset is missing or outside KnowledgeBase")
                    continue
                expected_asset_sha = str(asset.get("sha256", "")).upper()
                if not re.fullmatch(r"[0-9A-F]{64}", expected_asset_sha):
                    errors.append(f"source {label} selected asset has no valid SHA-256")
                elif deep and sha256_file(asset_path) != expected_asset_sha:
                    errors.append(f"source {label} selected asset hash mismatch")
                expected_size = asset.get("size_bytes")
                if isinstance(expected_size, int) and asset_path.stat().st_size != expected_size:
                    errors.append(f"source {label} selected asset size mismatch")

    if set(prepared_overrides) != used_prepared_overrides:
        errors.append("prepared file override does not match a declared prepared source path")

    return {
        "ok": not errors,
        "errors": errors,
        "warnings": warnings,
        "counts": {
            "records": len(records),
            "sources": len(sources_list),
            "historical_sources": sum(row.get("supersession") is not None for row in sources_list),
            "prepared_sources": sum(
                1
                for row in sources_list
                if isinstance(row.get("preparation"), dict)
                and row["preparation"].get("status") in {"prepared", "partial"}
            ),
        },
        "core_estimated_tokens": core_tokens,
        "deep": deep,
    }


def read_first_jsonl(path: Path) -> dict[str, Any]:
    try:
        with path.open("r", encoding="utf-8-sig") as handle:
            for line_number, raw in enumerate(handle, 1):
                if not raw.strip():
                    continue
                value = strict_json_loads(raw, f"{path} line {line_number}")
                if not isinstance(value, dict):
                    raise KBError(f"prepared file first row is not an object: {path}")
                return value
    except (OSError, KBError) as exc:
        raise KBError(f"cannot read prepared file {path}: {exc}") from exc
    raise KBError(f"prepared file is empty: {path}")


def require_valid(core: dict[str, Any], records: list[dict[str, Any]], sources: dict[str, Any]) -> None:
    result = validate_state(core, records, sources, deep=False)
    if not result["ok"]:
        raise KBError("KnowledgeBase validation failed: " + "; ".join(result["errors"][:8]))


def record_map(records: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    return {row["id"]: row for row in records}


def source_map(sources: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {row["source_id"]: row for row in source_rows(sources)}


def token_terms(text: str) -> list[str]:
    return [part for part in re.findall(r"[\w.-]+", text.casefold()) if len(part) > 1]


def text_blob(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).casefold()


def score_value(value: Any, terms: list[str]) -> int:
    blob = text_blob(value)
    return sum(blob.count(term) for term in terms)


def snippet(text: str, terms: list[str], width: int = 420) -> str:
    flat = re.sub(r"\s+", " ", text).strip()
    if len(flat) <= width:
        return flat
    lower = flat.casefold()
    starts = [lower.find(term) for term in terms if lower.find(term) >= 0]
    start = max(0, (min(starts) if starts else 0) - width // 4)
    result = flat[start : start + width]
    if start:
        result = "…" + result
    if start + width < len(flat):
        result += "…"
    return result


def source_version_info(source: dict[str, Any]) -> dict[str, Any]:
    revision = source.get("supersession")
    return {"historical_source": bool(revision), **(
        {"superseded_by": revision["source_id"]} if isinstance(revision, dict) else {}
    )}


def prepared_identity_matches(source: dict[str, Any], path: Path) -> bool:
    """Check prepared bytes and their meta binding without reading the original."""
    try:
        prep = source.get("preparation", {})
        first = read_first_jsonl(path)
        return (
            sha256_file(path) == str(prep.get("prepared_sha256", "")).upper()
            and first.get("type") == "meta"
            and first.get("source_id") == source.get("source_id")
            and str(first.get("source_sha256", "")).upper() == str(source.get("sha256", "")).upper()
        )
    except (OSError, KBError):
        return False


def iter_prepared_rows(
    sources: dict[str, Any], stale_sources: list[str] | None = None
) -> Iterable[tuple[str, dict[str, Any]]]:
    for source in source_rows(sources):
        path = prepared_path(source)
        if path is None or not path.is_file():
            continue
        if not prepared_identity_matches(source, path):
            if stale_sources is not None:
                stale_sources.append(source["source_id"])
            continue
        try:
            with path.open("r", encoding="utf-8-sig") as handle:
                for raw in handle:
                    if not raw.strip():
                        continue
                    row = strict_json_loads(raw, str(path))
                    if isinstance(row, dict) and row.get("type") != "meta":
                        yield source["source_id"], row
        except (OSError, KBError):
            # Validation reports malformed representations; search remains bounded.
            if stale_sources is not None and source["source_id"] not in stale_sources:
                stale_sources.append(source["source_id"])
            continue


def command_status(args: argparse.Namespace) -> None:
    core, records, sources = load_state()
    result = validate_state(core, records, sources, deep=False)
    result.update(
        {
            "kb_revision": core.get("kb_revision"),
            "updated_at": core.get("updated_at"),
            "root": str(KB_ROOT),
            "next": "Use 'validate --deep' after source changes; use 'context --topic ...' for task work.",
        }
    )
    emit(result, compact=args.compact)
    if not result["ok"]:
        raise SystemExit(2)


def check_read_revision(args: argparse.Namespace, core: dict[str, Any]) -> None:
    expected = getattr(args, "expect_revision", None)
    if getattr(args, "offset", 0) and expected is None:
        raise KBError("continuation pages require --expect-revision", code="revision_required")
    if expected is not None and expected != core["kb_revision"]:
        raise KBError(
            f"revision conflict: expected {expected}, current {core['kb_revision']}",
            code="revision_conflict", expected_revision=expected, kb_revision=core["kb_revision"],
        )


def projection_info(args: argparse.Namespace) -> dict[str, Any]:
    fields = getattr(args, "fields", None)
    mode = "ids" if getattr(args, "ids_only", False) else "summary" if getattr(args, "summary", False) else "fields" if fields else "full"
    return {"is_projection": mode != "full", "mode": mode, "fields": fields or []}


def project_value(row: dict[str, Any], args: argparse.Namespace) -> dict[str, Any]:
    info = projection_info(args)
    if not info["is_projection"]:
        return row
    identity = "id" if "id" in row else "source_id" if "source_id" in row else None
    if info["mode"] == "ids":
        return {identity: row[identity]} if identity else {}
    if info["mode"] == "summary":
        keys = ("id", "source_id", "kind", "status", "title", "statement", "source_refs")
        result = {key: row[key] for key in keys if key in row}
        if "statement" in result:
            result["statement"] = snippet(result["statement"], [], 420)
        return result
    result = {identity: row[identity]} if identity else {}
    for field in info["fields"]:
        parts = field.split(".")
        value: Any = row
        for part in parts:
            if not isinstance(value, dict) or part not in value:
                break
            value = value[part]
        else:
            destination = result
            for part in parts[:-1]:
                destination = destination.setdefault(part, {})
            destination[parts[-1]] = copy.deepcopy(value)
    return result


def bounded_page(
    base: dict[str, Any], key: str, items: list[Any], args: argparse.Namespace,
    *, scan_complete: bool = True, page_metadata: Any = None,
) -> dict[str, Any]:
    """Pages a stable ordered set; an oversized first item raises, never loops."""
    offset, limit, budget = args.offset, args.limit, args.budget
    available = len(items)

    def result_for(values: list[Any]) -> dict[str, Any]:
        end = min(offset + len(values), available)
        result = {
            **base, key: values, "offset": offset,
            "returned_count": len(values), "available_count": available,
            "total_count": available if scan_complete else None,
            "omitted_count": available - len(values) if scan_complete else None,
            "remaining_available_count": max(0, available - end),
            "scan_complete": scan_complete,
            "next_offset": end if end < available else None,
            "continuation_limited": not scan_complete,
            "truncated": len(values) < available or not scan_complete,
        }
        if page_metadata is not None:
            result.update(page_metadata(values))
        return result

    values: list[Any] = []
    result = result_for(values)
    if estimate_tokens(result) + 80 > budget:
        raise KBError("budget is smaller than response metadata", code="budget_too_small",
                      required_budget=estimate_tokens(result) + 80)
    for item in items[offset:offset + limit]:
        candidate = result_for([*values, item])
        if estimate_tokens(candidate) + 80 > budget:
            if not values:
                raise KBError(
                    "item exceeds page budget; select fewer fields or raise --budget",
                    code="item_too_large", offset=offset,
                    required_budget=estimate_tokens(candidate) + 80,
                    kb_revision=base.get("kb_revision", base.get("input_revision")),
                )
            break
        values.append(item)
        result = candidate
    return result


def emit_bounded(result: dict[str, Any], args: argparse.Namespace) -> None:
    result["estimated_tokens"] = 0
    for _ in range(3):
        result["estimated_tokens"] = estimate_tokens(result)
    if result["estimated_tokens"] > args.budget:
        raise KBError("response metadata exceeds budget; raise --budget or narrow the request",
                      code="budget_too_small", required_budget=result["estimated_tokens"])
    emit(result, compact=args.compact)


def command_core(args: argparse.Namespace) -> None:
    core, records, sources = load_state()
    require_valid(core, records, sources)
    check_read_revision(args, core)
    by_id = record_map(records)
    ids = list(dict.fromkeys(
        record_id for field in ("pinned_record_ids", "owner_decision_ids", "open_question_ids")
        for record_id in core.get(field, [])
    ))
    base: dict[str, Any] = {"kb_revision": core["kb_revision"], "projection": projection_info(args)}
    if not args.no_core:
        base["core"] = (
            {key: core[key] for key in ("schema_version", "kb_revision", "updated_at", "state_files") if key in core}
            if args.summary else core
        )
        base["core_is_projection"] = args.summary
    result = bounded_page(base, "records", [project_value(by_id[x], args) for x in ids], args)
    result["returned_record_count"] = result["returned_count"]
    result["omitted_record_count"] = result["omitted_count"]
    emit_bounded(result, args)


def command_context(args: argparse.Namespace) -> None:
    core, records, sources = load_state()
    require_valid(core, records, sources)
    check_read_revision(args, core)
    topics = [topic.casefold() for topic in args.topic]
    terms = token_terms(" ".join(topics))
    routed_ids: set[str] = set()
    for route_name, route in core.get("routes", {}).items():
        route_blob = " ".join([str(route_name), *route.get("topic_hints", [])]).casefold()
        if any(term in route_blob for term in terms):
            routed_ids.update(route.get("record_ids", []))
    scored = [(score_value(row, terms) + (500 if row["id"] in routed_ids else 0), row) for row in records]
    scored = sorted((x for x in scored if x[0]), key=lambda x: (-x[0], x[1]["id"]))
    records_only = args.records_only or args.evidence_limit == 0
    base: dict[str, Any] = {
        "query": args.topic, "kb_revision": core["kb_revision"],
        "projection": projection_info(args), "evidence_snippets": [],
        "stale_sources_excluded": [], "returned_evidence_count": 0,
        "omitted_evidence_match_count": None, "evidence_scan_complete": False,
        "evidence_scope": "not_requested" if records_only else "prepared_evidence",
        "pagination_scope": "records", "evidence_scan_reason": "not_requested" if records_only else "budget_or_no_terms",
    }
    if not args.no_core:
        base["core"] = core
    result = bounded_page(base, "records", [project_value(row, args) for _, row in scored], args)
    result["returned_record_count"] = result["returned_count"]
    result["omitted_record_match_count"] = result["omitted_count"]
    if terms and not records_only and estimate_tokens(result) + 80 < args.budget:
        evidence: list[tuple[int, dict[str, Any]]] = []
        stale: list[str] = []
        for source_id, row in iter_prepared_rows(sources, stale):
            score = score_value(row, terms)
            if score:
                evidence.append((score, {
                    "source_id": source_id, "type": row.get("type"), "locator": row.get("locator"),
                    **source_version_info(source_map(sources)[source_id]),
                    "text": snippet(str(row.get("text") or row.get("data") or ""), terms),
                }))
        evidence.sort(key=lambda x: (-x[0], x[1]["source_id"], str(x[1]["locator"])))
        result["stale_sources_excluded"] = sorted(set(stale))
        result["evidence_scan_complete"] = not stale
        result["evidence_scan_reason"] = "stale_sources" if stale else "complete"
        result["available_evidence_match_count"] = len(evidence)
        result["omitted_evidence_match_count"] = len(evidence) if not stale else None
        for _, row in evidence[:args.evidence_limit]:
            candidate = copy.deepcopy(result)
            candidate["evidence_snippets"].append(row)
            count = len(candidate["evidence_snippets"])
            candidate["returned_evidence_count"] = count
            candidate["omitted_evidence_match_count"] = len(evidence) - count if not stale else None
            if estimate_tokens(candidate) + 10 > args.budget:
                break
            result = candidate
        result["truncated"] = result["truncated"] or len(evidence) > result["returned_evidence_count"] or bool(stale)
    if not records_only and not result["evidence_scan_complete"]:
        result["truncated"] = True
    emit_bounded(result, args)


def command_search(args: argparse.Namespace) -> None:
    core, records, sources = load_state()
    require_valid(core, records, sources)
    check_read_revision(args, core)
    hit_fields = {"id", "kind", "record_kind", "status", "title", "snippet", "score",
                  "type", "locator", "matched_fields", "resolution_notes_snippet",
                  "historical_source", "superseded_by"}
    unsupported = sorted(set(args.fields or []) - hit_fields)
    if unsupported:
        raise KBError(
            "search --fields projects search hits; use get ID --fields for record fields",
            code="unsupported_search_fields", unsupported_fields=unsupported,
            supported_fields=sorted(hit_fields), projection_scope="search_hit",
        )
    terms = token_terms(args.query)
    if not terms:
        raise KBError("search query contains no usable terms")
    hits: list[tuple[int, dict[str, Any]]] = []
    for row in records:
        score = score_value(row, terms)
        if score:
            hit = {
                "kind": "record", "id": row["id"], "record_kind": row["kind"],
                "status": row["status"], "snippet": snippet(row["statement"], terms),
                "title": row.get("title"),
                "matched_fields": [key for key, value in row.items() if score_value(value, terms)],
            }
            if row.get("resolution_notes"):
                hit["resolution_notes_snippet"] = snippet(
                    json.dumps(row["resolution_notes"], ensure_ascii=False), terms
                )
            hits.append((score + 20, hit))
    if not args.records_only:
        for source in source_rows(sources):
            score = score_value({k: v for k, v in source.items() if k != "preparation"}, terms)
            if score:
                hits.append((score + 10, {
                    "kind": "source", "id": source["source_id"], "title": source.get("title"),
                    **source_version_info(source),
                    "snippet": snippet(str(source.get("summary", "")), terms),
                }))
    # This fixed candidate horizon is independent of page size/offset.
    prepared_cap, prepared_seen, complete = 2000, 0, True
    stale: list[str] = []
    if not args.records_only:
        for source_id, row in iter_prepared_rows(sources, stale):
            score = score_value(row, terms)
            if score:
                hits.append((score, {
                    "kind": "prepared_chunk", "id": source_id, "type": row.get("type"),
                    **source_version_info(source_map(sources)[source_id]),
                    "locator": row.get("locator"), "snippet": snippet(str(row.get("text") or row.get("data") or ""), terms),
                }))
                prepared_seen += 1
                if prepared_seen >= prepared_cap:
                    complete = False
                    break
        complete = complete and not stale
    hits.sort(key=lambda x: (-x[0], str(x[1].get("id")), str(x[1].get("locator", ""))))
    rows = [project_value(dict(hit, score=score), args) for score, hit in hits]
    result = bounded_page({
        "query": args.query, "terms": terms, "kb_revision": core["kb_revision"],
        "projection": projection_info(args), "stale_sources_excluded": sorted(set(stale)),
        "projection_scope": "search_hit",
        "search_scope": "records_only" if args.records_only else "records_sources_prepared",
        "prepared_match_cap": prepared_cap if not args.records_only else 0,
    }, "hits", rows, args, scan_complete=complete)
    emit_bounded(result, args)


def source_result(source: dict[str, Any], args: argparse.Namespace, *, metadata_only: bool) -> dict[str, Any]:
    result: dict[str, Any] = {
        "kind": "source", "value": project_value(source, args), "projection": projection_info(args),
        **source_version_info(source),
        "metadata_only": metadata_only, "prepared_chunks": [], "prepared_content_accessed": False,
        "stale_sources_excluded": [],
    }
    if metadata_only:
        return result
    path = prepared_path(source)
    chunks: list[dict[str, Any]] = []
    if path and path.is_file():
        result["prepared_content_accessed"] = True
        if not prepared_identity_matches(source, path):
            result.update(stale_sources_excluded=[source["source_id"]], scan_complete=False,
                          total_count=None, omitted_chunk_count=None, truncated=True)
            return result
        chunks = [row for row in read_jsonl(path) if row.get("type") != "meta"
                  and (not args.locator or args.locator.casefold() in str(row.get("locator", "")).casefold())]
    result = bounded_page(result, "prepared_chunks", chunks, args)
    result["returned_chunk_count"] = result["returned_count"]
    result["omitted_chunk_count"] = result["omitted_count"]
    return result


def command_get(args: argparse.Namespace) -> None:
    core, records, sources = load_state()
    require_valid(core, records, sources)
    check_read_revision(args, core)
    identifiers = list(dict.fromkeys(args.identifier))
    by_id, by_source = record_map(records), source_map(sources)
    if len(args.identifier) == 1:
        identifier = identifiers[0]
        if identifier in by_id:
            if args.offset:
                raise KBError("single-record retrieval has no continuation page")
            result = {"kind": "record", "value": project_value(by_id[identifier], args),
                      "projection": projection_info(args), "omitted_count": 0, "truncated": False}
        elif identifier in by_source:
            result = source_result(by_source[identifier], args,
                                   metadata_only=args.metadata_only or args.ids_only or args.summary)
        else:
            raise KBError(f"unknown KnowledgeBase id: {identifier}", code="unknown_id")
        result.update(kb_revision=core["kb_revision"], returned_ids=[identifier], missing_ids=[])
        emit_bounded(result, args)
        return
    # Batch results contain record values/source metadata; chunks use single-source get.
    rows, missing = [], []
    for identifier in identifiers:
        if identifier in by_id:
            rows.append({"kind": "record", "value": project_value(by_id[identifier], args)})
        elif identifier in by_source:
            rows.append(source_result(by_source[identifier], args, metadata_only=True))
        else:
            missing.append(identifier)
    result = bounded_page({
        "kb_revision": core["kb_revision"], "projection": projection_info(args),
        "requested_count": len(identifiers), "missing_ids": missing, "source_mode": "metadata_only",
    }, "results", rows, args, page_metadata=lambda values: {
        "returned_ids": [row["value"].get("id", row["value"].get("source_id")) for row in values],
    })
    emit_bounded(result, args)


def command_filter(args: argparse.Namespace, kind: str) -> None:
    core, records, sources = load_state()
    require_valid(core, records, sources)
    check_read_revision(args, core)
    rows = sorted((row for row in records if row.get("kind") == kind
                   and (args.all or row.get("status") in {"open", "active", "provisional"})), key=lambda x: x["id"])
    result = bounded_page({
        "kind": kind, "count": len(rows), "kb_revision": core["kb_revision"], "projection": projection_info(args),
    }, "records", [project_value(row, args) for row in rows], args)
    emit_bounded(result, args)


def command_validate(args: argparse.Namespace) -> None:
    core, records, sources = load_state()
    result = validate_state(core, records, sources, deep=args.deep)
    result["kb_revision"] = core.get("kb_revision")
    emit(result, compact=args.compact)
    if not result["ok"]:
        raise SystemExit(2)


def atomic_stage(path: Path, payload: bytes) -> Path:
    if not within(path, KB_ROOT):
        raise KBError(f"refusing write outside KnowledgeBase: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temp_name = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=path.parent)
    temp_path = Path(temp_name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())
    except Exception as exc:
        try:
            temp_path.unlink(missing_ok=True)
        except OSError as cleanup_exc:
            details = dict(getattr(exc, "details", {}))
            details.setdefault("stage_cleanup_errors", []).append({"path": str(temp_path), "error": str(cleanup_exc)})
            raise KBError(str(exc), **details) from exc
        raise
    return temp_path


def load_transaction(path_text: str) -> tuple[dict[str, Any], str]:
    path = Path(path_text).resolve(strict=True)
    if not within(path, KB_ROOT):
        raise KBError("transaction file must be inside the exact KnowledgeBase root")
    # Hash and parse one read, so the identity names the exact consumed bytes.
    payload = path.read_bytes()
    try:
        value = strict_json_loads(payload.decode("utf-8-sig"), str(path))
    except UnicodeDecodeError as exc:
        raise KBError("transaction must be valid UTF-8") from exc
    if not isinstance(value, dict):
        raise KBError("transaction must be a JSON object")
    permitted = {"note", "actor", "upsert_records", "upsert_sources", "core_patch"}
    unknown = sorted(set(value) - permitted)
    if unknown:
        raise KBError(f"unknown transaction fields: {', '.join(unknown)}")
    return value, hashlib.sha256(payload).hexdigest().upper()


def build_candidate(
    core: dict[str, Any],
    records: list[dict[str, Any]],
    sources: dict[str, Any],
    transaction: dict[str, Any],
    *, timestamp: str | None = None,
) -> tuple[dict[str, Any], list[dict[str, Any]], dict[str, Any], dict[str, int]]:
    next_core = copy.deepcopy(core)
    next_records = copy.deepcopy(records)
    next_sources = copy.deepcopy(sources)
    record_upserts = transaction.get("upsert_records", [])
    source_upserts = transaction.get("upsert_sources", [])
    core_patch = transaction.get("core_patch", {})
    if not isinstance(record_upserts, list) or any(not isinstance(row, dict) for row in record_upserts):
        raise KBError("upsert_records must be an array of objects")
    if not isinstance(source_upserts, list) or any(not isinstance(row, dict) for row in source_upserts):
        raise KBError("upsert_sources must be an array of objects")
    if not isinstance(core_patch, dict):
        raise KBError("core_patch must be an object")
    if "kb_revision" in core_patch or "schema_version" in core_patch:
        raise KBError("core_patch cannot set schema_version or kb_revision")

    existing_records = {row["id"]: index for index, row in enumerate(next_records)}
    for row in record_upserts:
        record_id = row.get("id")
        if record_id in existing_records:
            original_created = next_records[existing_records[record_id]].get("created_at")
            replacement = copy.deepcopy(row)
            if original_created:
                replacement["created_at"] = original_created
            replacement["updated_at"] = timestamp or utc_now()
            next_records[existing_records[record_id]] = replacement
        else:
            replacement = copy.deepcopy(row)
            replacement.setdefault("created_at", timestamp or utc_now())
            replacement.setdefault("updated_at", replacement["created_at"])
            next_records.append(replacement)
            existing_records[str(record_id)] = len(next_records) - 1

    next_source_rows = source_rows(next_sources)
    existing_sources = {row["source_id"]: index for index, row in enumerate(next_source_rows)}
    originally_registered_sources = set(existing_sources)
    for row in source_upserts:
        source_id = row.get("source_id")
        if source_id not in originally_registered_sources and (
            row.get("availability") is not None or row.get("supersession") is not None
        ):
            raise KBError(
                f"source {source_id} cannot declare unavailable or superseded original at first registration"
            )
        if source_id in existing_sources:
            old = next_source_rows[existing_sources[source_id]]
            if row.get("locator") != old.get("locator") or str(row.get("sha256", "")).upper() != str(old.get("sha256", "")).upper():
                raise KBError(
                    f"source identity is immutable for {source_id}; register changed bytes under a new source id"
                )
            if row.get("size_bytes") != old.get("size_bytes") and old.get("size_bytes") is not None:
                raise KBError(f"source size identity is immutable for {source_id}")
            next_source_rows[existing_sources[source_id]] = copy.deepcopy(row)
        else:
            next_source_rows.append(copy.deepcopy(row))
            existing_sources[str(source_id)] = len(next_source_rows) - 1

    next_core.update(copy.deepcopy(core_patch))
    next_core["kb_revision"] = int(core["kb_revision"]) + 1
    next_core["updated_at"] = timestamp or utc_now()
    if transaction.get("note"):
        next_core["last_maintenance"] = {
            "at": next_core["updated_at"],
            "actor": transaction.get("actor", "unspecified"),
            "note": str(transaction["note"])[:500],
        }
    if source_upserts:
        next_sources["catalog_revision"] = int(next_sources.get("catalog_revision", 0)) + 1
        next_sources["updated_at"] = next_core["updated_at"]

    effects = {
        "record_upserts": len(record_upserts),
        "source_upserts": len(source_upserts),
        "core_fields_patched": len(core_patch),
    }
    if not any(effects.values()) and not transaction.get("note"):
        raise KBError("transaction has no effect")
    return next_core, next_records, next_sources, effects


def command_revise_source(args: argparse.Namespace) -> None:
    """Emit a transaction draft; canonical changes still require reviewed apply."""
    core, records, sources = load_state()
    require_valid(core, records, sources)
    if core["kb_revision"] != args.expect_revision:
        raise KBError("KB revision changed before source revision drafting")
    lookup = source_map(sources)
    if args.source_id not in lookup or not SOURCE_ID_RE.fullmatch(args.new_source_id):
        raise KBError("revise-source requires a registered source and valid new source ID")
    if args.new_source_id in lookup:
        raise KBError("new source ID is already registered")
    if not args.actor.strip() or not args.reason.strip():
        raise KBError("source revision requires nonempty actor and reason")
    if any(not RECORD_ID_RE.fullmatch(ref) for ref in args.record_id):
        raise KBError("source revision record IDs must use the record ID format")
    old = copy.deepcopy(lookup[args.source_id])
    path = source_local_path(old)
    if old.get("supersession") is not None or old.get("availability") is not None:
        raise KBError("revise-source requires the current available source version")
    if path is None or not within(path, EXPERIMENT_ROOT) or within(path, KB_ROOT) or not path.is_file():
        raise KBError("source revision requires a permitted current file")
    actual_sha = sha256_file(path)
    if actual_sha == str(old.get("sha256", "")).upper():
        raise KBError("source bytes are unchanged; no new source identity needed")
    try:
        if args.git_commit:
            history = source_versions.git_history(EXPERIMENT_ROOT, old, args.git_commit)
        elif args.history_file:
            history = source_versions.snapshot_history(EXPERIMENT_ROOT, old, args.history_file)
        else:
            history = {"kind": "unavailable", "reason": args.history_unavailable_reason.strip()}
    except source_versions.VersionError as exc:
        raise KBError(str(exc)) from exc
    if history["kind"] == "unavailable" and not history["reason"]:
        raise KBError("unavailable history needs an explicit nonempty reason")
    timestamp = utc_now()
    old["supersession"] = {"source_id": args.new_source_id, "actor": args.actor,
        "reason": args.reason, "record_ids": args.record_id, "observed_at": timestamp, "history": history}
    new = {"source_id": args.new_source_id, "title": old.get("title", path.name),
        "locator": copy.deepcopy(old["locator"]), "sha256": actual_sha,
        "size_bytes": path.stat().st_size, "observed_at_utc": timestamp,
        "supersedes": args.source_id, "status": "registered current working-file version",
        "coverage": "Exact current bytes hashed; substantive coverage must be recorded separately."}
    if sha256_file(path) != actual_sha:
        raise KBError("source changed during revision drafting; inspect before retrying")
    emit({"ok": True, "kb_revision": core["kb_revision"], "canonical_writes": False,
          "transaction": {"actor": args.actor, "note": args.reason,
                          "upsert_sources": [old, new], "upsert_records": [], "core_patch": {}}},
         compact=args.compact)


def json_equal(left: Any, right: Any) -> bool:
    if type(left) is not type(right):
        return False
    if isinstance(left, dict):
        return left.keys() == right.keys() and all(json_equal(left[key], right[key]) for key in left)
    if isinstance(left, list):
        return len(left) == len(right) and all(json_equal(a, b) for a, b in zip(left, right))
    return left == right


def field_changes(before: Any, after: Any, path: str = "", *, before_exists: bool = True,
                  after_exists: bool = True) -> Iterable[dict[str, Any]]:
    """JSON Pointer paths; mappings recurse, arrays and scalar values stay atomic."""
    if before_exists and after_exists and json_equal(before, after):
        return
    left = before if before_exists and isinstance(before, dict) else {}
    right = after if after_exists and isinstance(after, dict) else {}
    mappings = ((not before_exists or isinstance(before, dict))
                and (not after_exists or isinstance(after, dict)))
    if mappings and (not before_exists or not after_exists):
        # Keep container presence distinct from emptying an existing object.
        # Child fields follow separately so automatic fields remain visible.
        yield {"path": path or "/", "change": "added" if not before_exists else "removed",
               "object_presence_only": True, "after" if not before_exists else "before": {}}
    if mappings:
        for key in sorted(set(left) | set(right)):
            pointer = path + "/" + key.replace("~", "~0").replace("/", "~1")
            yield from field_changes(left.get(key), right.get(key), pointer,
                                     before_exists=key in left, after_exists=key in right)
        return
    result = {"path": path or "/", "change": "added" if not before_exists else "removed" if not after_exists else "changed"}
    if before_exists:
        result["before"] = before
    if after_exists:
        result["after"] = after
    yield result


def candidate_preview(core: dict[str, Any], records: list[dict[str, Any]], sources: dict[str, Any],
                      next_core: dict[str, Any], next_records: list[dict[str, Any]],
                      next_sources: dict[str, Any], transaction: dict[str, Any],
                      *, include_values: bool = False) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    """Describe the actual replacement candidate, including omitted optional fields."""
    changes: list[dict[str, Any]] = []
    affected: dict[str, set[str]] = {"record": set(), "source": set(), "core": set(), "catalog": set()}
    requested = {row["id"]: row for row in transaction.get("upsert_records", [])}

    def add(target: str, identifier: str, before: Any, after: Any, *, exists: bool = True) -> None:
        for delta in field_changes(before if exists else {}, after):
            path = delta["path"]
            automatic = (
                (target == "core" and (path in {"/kb_revision", "/updated_at", "/last_maintenance/at"} or path.startswith("/state_files/")))
                or (target == "catalog" and path in {"/catalog_revision", "/updated_at"})
                or (target == "record" and (path == "/updated_at" and exists
                    or path in {"/created_at", "/updated_at"} and not exists and path[1:] not in requested.get(identifier, {})))
            )
            item = {"target": target, "id": identifier, "automatic": automatic, **delta}
            if not include_values:
                item.pop("before", None)
                item.pop("after", None)
            changes.append(item)
            affected[target].add(identifier)

    for target, before_rows, after_rows, id_key in (
        ("record", records, next_records, "id"),
        ("source", source_rows(sources), source_rows(next_sources), "source_id"),
    ):
        before_map = {row[id_key]: row for row in before_rows}
        for row in sorted(after_rows, key=lambda value: value[id_key]):
            identifier = row[id_key]
            # Top-level record/source presence is identified by its ID. Compare
            # fields so new automatic timestamps remain separately classified.
            add(target, identifier, before_map.get(identifier), row, exists=identifier in before_map)
    add("core", "core", core, next_core)
    add("catalog", "sources", {k: v for k, v in sources.items() if k != "sources"},
        {k: v for k, v in next_sources.items() if k != "sources"})
    # Semantic field changes precede automatic maintenance fields on every page.
    changes.sort(key=lambda item: (item["automatic"], item["target"], item["id"], item["path"]))
    return changes, {"affected_counts": {key: len(value) for key, value in affected.items()},
                     "field_change_count": sum(not item["automatic"] for item in changes),
                     "automatic_change_count": sum(item["automatic"] for item in changes)}


def write_outcome(progress: dict[str, Any], stage: str) -> dict[str, Any]:
    return {
        "stage": stage,
        "replaced_files": list(progress["replaced_files"]),
        "commit_completed": progress["commit_completed"],
        "verification_completed": progress["verification_completed"],
        "rollback_performed": False,
        "recovery_required": stage not in {"before_replacement", "commit_completed"},
        "cleanup_errors": list(progress["cleanup_errors"]),
        "replacement_uncertain": progress.get("replacement_uncertain", False),
    }


def command_apply(args: argparse.Namespace) -> None:
    progress: dict[str, Any] = {
        "phase": "before_replacement", "replaced_files": [],
        "commit_completed": False, "verification_completed": False, "cleanup_errors": [],
    }
    try:
        _command_apply(args, progress)
    except Exception as exc:
        phase = progress["phase"]
        if phase == "replacing":
            # A replacement may have completed before an I/O exception reached us.
            try:
                current = {path: sha256_file(path) for path in progress["before_hashes"]}
                intended = progress["payload_hashes"]
                for path in intended:
                    if current[path] == intended[path] != progress["before_hashes"][path] and path.name not in progress["replaced_files"]:
                        progress["replaced_files"].append(path.name)
                progress["commit_completed"] = all(current[p] == h for p, h in intended.items())
                progress["replacement_uncertain"] = any(
                    current[p] not in {progress["before_hashes"][p], intended[p]} for p in current
                )
                attempted = progress.get("attempted_path")
                if attempted and attempted.name not in progress["replaced_files"] and intended[attempted] == progress["before_hashes"][attempted]:
                    # Equal bytes cannot prove whether the attempted replacement ran.
                    progress["replacement_uncertain"] = True
            except Exception:
                progress["replacement_uncertain"] = True
        if progress["commit_completed"]:
            stage = "commit_completed_reporting_failed" if progress["verification_completed"] else "commit_completed_verification_failed"
        elif progress["replaced_files"]:
            stage = "partial_replacement"
        elif progress.get("replacement_uncertain"):
            stage = "replacement_uncertain"
        else:
            stage = "before_replacement"
        details = dict(getattr(exc, "details", {}))
        details.update(code="apply_failed", write_outcome=write_outcome(progress, stage))
        for name in ("from_revision", "to_revision"):
            if name in progress:
                details[name] = progress[name]
        raise KBError(str(exc), **details) from exc


def _command_apply(args: argparse.Namespace, progress: dict[str, Any]) -> None:
    before_hashes = {
        CORE_PATH: sha256_file(CORE_PATH),
        RECORDS_PATH: sha256_file(RECORDS_PATH),
        SOURCES_PATH: sha256_file(SOURCES_PATH),
    }
    progress["before_hashes"] = before_hashes
    core, records, sources = load_state()
    progress["from_revision"] = core["kb_revision"]
    require_valid(core, records, sources)
    if core.get("kb_revision") != args.expect_revision:
        raise KBError(
            f"revision conflict: expected {args.expect_revision}, current {core.get('kb_revision')}"
        )
    transaction_path = Path(args.transaction).resolve(strict=True)
    if not within(transaction_path, KB_ROOT):
        raise KBError("transaction file must be inside the exact KnowledgeBase root")
    transaction, transaction_hash = load_transaction(args.transaction)
    if args.expect_transaction_sha256 and args.expect_transaction_sha256.upper() != transaction_hash:
        raise KBError("transaction differs from the reviewed bytes", code="transaction_identity_conflict")
    if args.dry_run:
        if sha256_file(transaction_path) != transaction_hash:
            raise KBError("transaction changed while being read", code="transaction_identity_conflict")
        if args.offset and args.expect_transaction_sha256 is None:
            raise KBError("preview continuation requires --expect-transaction-sha256", code="transaction_identity_required")
    if args.dry_run and args.offset and args.preview_time is None:
        raise KBError("preview continuation requires --preview-time from the first page", code="preview_time_required")
    preview_time = (args.preview_time or utc_now()) if args.dry_run else None
    next_core, next_records, next_sources, effects = build_candidate(
        core, records, sources, transaction, timestamp=preview_time
    )
    progress["to_revision"] = next_core["kb_revision"]
    records_payload = canonical_jsonl_bytes(next_records)
    sources_payload = canonical_json_bytes(next_sources)
    next_core["state_files"] = {
        "records_jsonl_sha256": hashlib.sha256(records_payload).hexdigest().upper(),
        "sources_json_sha256": hashlib.sha256(sources_payload).hexdigest().upper(),
    }
    validation = validate_state(next_core, next_records, next_sources, deep=True)
    if not validation["ok"]:
        raise KBError("transaction candidate is invalid: " + "; ".join(validation["errors"][:8]))
    if args.dry_run:
        changes, preview_counts = candidate_preview(core, records, sources, next_core, next_records, next_sources,
                                                    transaction, include_values=args.preview_values)
        result = bounded_page({
                "ok": True,
                "dry_run": True,
                "kb_revision": core["kb_revision"],
                "from_revision": core["kb_revision"],
                "to_revision": next_core["kb_revision"],
                "effects": effects,
                "warnings": [str(value)[:500] for value in validation["warnings"][:5]],
                "warning_count": len(validation["warnings"]),
                "warnings_truncated": len(validation["warnings"]) > 5 or any(len(str(value)) > 500 for value in validation["warnings"][:5]),
                "validation_scope": "deep_sources_and_prepared",
                "write_outcome": write_outcome(progress, "before_replacement"),
                "transaction_sha256": transaction_hash,
                "preview_time": preview_time,
                "preview": {**preview_counts, "path_format": "json_pointer", "arrays": "atomic",
                            "values_included": args.preview_values, "upserts": "whole_object", "core_patch": "shallow_replacement"},
            }, "changes", changes, args)
        emit_bounded(result, args)
        return

    payloads = {
        RECORDS_PATH: records_payload,
        SOURCES_PATH: sources_payload,
        CORE_PATH: canonical_json_bytes(next_core),
    }
    staged: dict[Path, Path] = {}
    progress["payload_hashes"] = {path: hashlib.sha256(data).hexdigest().upper() for path, data in payloads.items()}
    replacement_error: Exception | None = None
    try:
        for path, payload in payloads.items():
            staged[path] = atomic_stage(path, payload)
        for path, expected in before_hashes.items():
            if sha256_file(path) != expected:
                raise KBError(f"concurrent modification detected: {path.name}")
        if sha256_file(transaction_path) != transaction_hash:
            raise KBError("transaction changed before commit", code="transaction_identity_conflict")
        # Core is the commit marker and is replaced last.
        for path in (RECORDS_PATH, SOURCES_PATH, CORE_PATH):
            progress["phase"] = "replacing"
            progress["attempted_path"] = path
            os.replace(staged[path], path)
            progress["replaced_files"].append(path.name)
            if path == CORE_PATH:
                progress["commit_completed"] = True
            staged.pop(path, None)
    except Exception as exc:
        replacement_error = exc
        progress["cleanup_errors"].extend(getattr(exc, "details", {}).get("stage_cleanup_errors", []))
    finally:
        for temp_path in staged.values():
            try:
                temp_path.unlink(missing_ok=True)
            except OSError as exc:
                progress["cleanup_errors"].append({"path": str(temp_path), "error": str(exc)})
    if replacement_error is not None:
        raise replacement_error
    if progress["cleanup_errors"]:
        raise KBError("staged-file cleanup failed; inspect reported temporary paths")

    progress["phase"] = "verification"
    committed_core, committed_records, committed_sources = load_state()
    committed = validate_state(committed_core, committed_records, committed_sources, deep=True)
    if not committed["ok"] or committed_core.get("kb_revision") != next_core["kb_revision"]:
        raise KBError("post-commit verification failed; inspect the KnowledgeBase before another apply")
    progress["verification_completed"] = True
    progress["phase"] = "reporting"
    emit(
        {
            "ok": True,
            "dry_run": False,
            "from_revision": core["kb_revision"],
            "to_revision": committed_core["kb_revision"],
            "effects": effects,
            "validation_scope": "deep_sources_and_prepared",
            "write_outcome": write_outcome(progress, "commit_completed"),
            "transaction_sha256": transaction_hash,
            "hashes": {
                "core.json": sha256_file(CORE_PATH),
                "records.jsonl": sha256_file(RECORDS_PATH),
                "sources.json": sha256_file(SOURCES_PATH),
            },
        },
        compact=args.compact,
    )


def add_common_output(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--compact", action="store_true", help="emit compact JSON")


def add_read_output(parser: argparse.ArgumentParser, *, budget: int = 6000, limit: int = 100) -> None:
    add_common_output(parser)
    parser.add_argument("--budget", type=int, default=budget, help="estimated compact-JSON token budget")
    parser.add_argument("--limit", type=int, default=limit, help="maximum items in this page")
    parser.add_argument("--offset", type=int, default=0)
    parser.add_argument("--expect-revision", type=int, help="required for continuation pages")
    view = parser.add_mutually_exclusive_group()
    view.add_argument("--fields", nargs="+", help="selected fields, including dotted metadata paths")
    view.add_argument("--summary", action="store_true", help="explicit projected summary")
    view.add_argument("--ids-only", action="store_true", help="explicit identity-only projection")


def build_parser() -> argparse.ArgumentParser:
    parser = JSONArgumentParser(description="KnowledgeBase interface")
    subparsers = parser.add_subparsers(dest="command", required=True)

    status = subparsers.add_parser("status", help="quick structural health and counts")
    add_common_output(status)
    status.set_defaults(func=command_status)

    core = subparsers.add_parser("core", help="load compact core plus pinned records")
    core.add_argument("--no-core", action="store_true", help="omit the already loaded core")
    add_read_output(core, budget=6000)
    core.set_defaults(func=command_core)

    context = subparsers.add_parser("context", help="load bounded topic context")
    context.add_argument("--topic", action="append", required=True, help="topic term; repeatable")
    context.add_argument("--evidence-limit", type=int, default=12)
    context.add_argument("--records-only", action="store_true", help="never read or hash prepared contents")
    context.add_argument("--no-core", action="store_true")
    add_read_output(context)
    context.set_defaults(func=command_context)

    search = subparsers.add_parser("search", help="search records, sources, and prepared chunks")
    search.add_argument("query")
    search.add_argument("--records-only", action="store_true", help="search only recorded knowledge")
    add_read_output(search, limit=20)
    search.set_defaults(func=command_search)

    get = subparsers.add_parser("get", help="retrieve known IDs; batches return source metadata")
    get.add_argument("identifier", nargs="+")
    get.add_argument("--locator", help="prepared-chunk locator substring")
    get.add_argument("--metadata-only", action="store_true", help="never read or hash prepared contents")
    add_read_output(get, limit=20)
    get.set_defaults(func=command_get)

    questions = subparsers.add_parser("questions", help="list active/open questions")
    questions.add_argument("--all", action="store_true")
    add_read_output(questions)
    questions.set_defaults(func=lambda args: command_filter(args, "open_question"))

    decisions = subparsers.add_parser("decisions", help="list active owner decisions")
    decisions.add_argument("--all", action="store_true")
    add_read_output(decisions)
    decisions.set_defaults(func=lambda args: command_filter(args, "owner_decision"))

    validate = subparsers.add_parser("validate", help="validate schemas and references")
    validate.add_argument("--deep", action="store_true", help="rehash source and prepared files")
    add_common_output(validate)
    validate.set_defaults(func=command_validate)

    revise = subparsers.add_parser("revise-source", help="draft an explicit source-version transaction without writing")
    revise.add_argument("source_id")
    revise.add_argument("new_source_id")
    revise.add_argument("--expect-revision", type=int, required=True)
    revise.add_argument("--actor", required=True)
    revise.add_argument("--reason", required=True)
    revise.add_argument("--record-id", action="append", required=True)
    history = revise.add_mutually_exclusive_group(required=True)
    history.add_argument("--git-commit", help="full local commit ID reproducing the old registered bytes")
    history.add_argument("--history-file", help="exact absolute KB/source-history file reproducing the old bytes")
    history.add_argument("--history-unavailable-reason", help="explicit gap; deep validation reports a warning")
    add_common_output(revise)
    revise.set_defaults(func=command_revise_source)

    apply = subparsers.add_parser("apply", help="apply an optimistic KB-only JSON transaction")
    apply.add_argument("transaction", help="transaction JSON path inside KnowledgeBase")
    apply.add_argument("--expect-revision", type=int, required=True)
    apply.add_argument("--dry-run", action="store_true")
    apply.add_argument("--budget", type=int, default=6000, help="dry-run preview output budget")
    apply.add_argument("--limit", type=int, default=100, help="dry-run preview field changes per page")
    apply.add_argument("--offset", type=int, default=0, help="dry-run preview continuation offset")
    apply.add_argument("--preview-values", action="store_true", help="include before/after values in dry-run changes")
    apply.add_argument("--expect-transaction-sha256", help="reviewed transaction identity for apply or preview continuation")
    apply.add_argument("--preview-time", help="reuse the first dry-run page's UTC timestamp for continuation")
    add_common_output(apply)
    apply.set_defaults(func=command_apply)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if hasattr(args, "budget") and args.budget < 300:
        raise KBError("budget must be at least 300 estimated tokens")
    if hasattr(args, "budget") and args.budget > 20_000:
        raise KBError("budget cannot exceed 20000 estimated tokens")
    if hasattr(args, "limit") and args.limit < 1:
        raise KBError("limit must be positive")
    if hasattr(args, "limit") and args.limit > 100:
        raise KBError("limit cannot exceed 100")
    if getattr(args, "offset", 0) < 0:
        raise KBError("offset cannot be negative")
    if not 0 <= getattr(args, "evidence_limit", 0) <= 100:
        raise KBError("evidence-limit must be between 0 and 100")
    if len(getattr(args, "identifier", [])) > 100:
        raise KBError("get accepts at most 100 IDs per request")
    fields = getattr(args, "fields", None) or []
    if len(fields) > 32 or any(not re.fullmatch(r"[A-Za-z_][\w-]*(?:\.[A-Za-z_][\w-]*)*", f) or len(f) > 120 for f in fields):
        raise KBError("fields must be at most 32 named paths, each at most 120 characters")
    if len(getattr(args, "query", "")) > 500:
        raise KBError("query cannot exceed 500 characters")
    if args.command == "apply":
        if not args.dry_run and (args.offset or args.preview_values or args.preview_time):
            raise KBError("preview options require --dry-run")
        if args.expect_transaction_sha256 and not re.fullmatch(r"[0-9a-fA-F]{64}", args.expect_transaction_sha256):
            raise KBError("expect-transaction-sha256 must be a SHA-256 hex digest")
        if args.preview_time:
            try:
                value = dt.datetime.fromisoformat(args.preview_time)
                if value.tzinfo is None or value.utcoffset() != dt.timedelta(0) or value.isoformat(timespec="seconds") != args.preview_time:
                    raise ValueError()
            except ValueError:
                raise KBError("preview-time must use UTC format YYYY-MM-DDTHH:MM:SS+00:00")
    args.func(args)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KBError as exc:
        emit({"ok": False, "error": str(exc), **exc.details}, compact=True)
        raise SystemExit(2)
    except OSError as exc:
        emit({"ok": False, "error": str(exc), "code": "read_failed"}, compact=True)
        raise SystemExit(2)
