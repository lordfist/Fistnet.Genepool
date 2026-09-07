# Genepool KnowledgeBase policy and reference

Original tool policy packaged by Other and its transient packaging helper, 2026-09-05; specialized for Genepool Analyzer by owner instruction, 2026-09-06. This is operating guidance, not experiment evidence. `CONSTITUTION.md` is the Genepool Analyzer contract. Higher-priority instructions and current owner boundaries control.

## Required operating policy

### Binding and authority

The owner has assigned **Genepool Analyzer** to `D:\Posao\Fistnet.Genepool`, with working knowledge at `D:\Posao\Fistnet.Genepool\KnowledgeBase`. The role is active for requested project work. No dormant/unbound activation step remains. Package provenance is historical software metadata. The existing KnowledgeBase is derived support infrastructure, separate from original experiment evidence; preserve it through this specialization.

The tools require `<root>/KnowledgeBase/tools`; their physical installation root must equal the initialized `core.json` root. This assignment is already established by the owner and continues across tasks. A different physical root requires an explicit migration/rebinding assignment; moving an initialized core is not automatic rebinding. The owner's 2026-09-06 specialization instruction explicitly adapts the constitution as Genepool Analyzer. It does not authorize a different experiment or the next improvement part.

The owner's continuing authorization covers bundled-tool execution for retrieval/validation, registered permitted source preparation, and reviewed transactions, using the existing runtime and writing derived knowledge and necessary transaction/preparation files inside the exact KB. Do not request this authorization again. Honor any later narrowing of scope.

For analysis-only tasks, KB maintenance is the authorized exception to source read-only/non-executing work. For an authorized implementation or repair, apply the constitution's local editing/build/testing scope; KB policy does not revoke that authority. KB maintenance alone does not authorize outside-root evidence, external actions, installation, source repairs or experiment trials. Honor the current stage and blind/private/excluded boundaries. New maintenance uses Genepool Analyzer as the actual actor; historical authorship remains intact.

**Continuing owner-authorized tool adaptation, 2026-09-06:** DEC-0006 authorizes adapting KB tools to this experiment, including source revisions backed by Git. These tools are working aids that may be changed within that authorization; the packaged implementation is not an immutable rule. Preserve KB integrity and honest provenance while removing unnecessary obstacles. Test material tool changes with disposable synthetic fixtures before canonical use. User elected to keep the KnowledgeBase in Git (DEC-0008); Git presence does not establish that current KB edits are committed. Do not require redundant backup trees or silently relax KB state hashes, references, revision checks, preview, and failure reporting. This adaptation does not authorize unrelated experiment execution or external actions.

### Task entry and recording

When binding and tool access are authorized, validate the KB, load its core, then retrieve bounded relevant context or known records before returning to originals for a named missing, stale, disputed, or exact-evidence need. Respect communication-only tasks or smaller read sets. Use records-only queries and source metadata projections when content access is unnecessary. Reuse prepared evidence only when its source identity, freshness and coverage support the claim. Inspect revision, omissions, completeness and continuation fields; a capped search or last available page does not establish exhaustive coverage.

Use an existing Python 3.11+ runtime with `-X utf8 -B`; PDF preparation additionally needs optional `pypdf`. Check availability, never infer installation permission. Read relevant reference sections before unfamiliar operations. The populated core provides compact orientation; reconcile it from owner decisions and verified in-scope evidence without resetting prior knowledge.

Record material new in-scope findings and owner decisions when authorized. Keep compact orientation, sourced records, source catalog and prepared evidence distinct. Preserve actual origin, source references/locators and horizon, uncertainty, derivation, relationships and status. Distinguish observed fact, source claim, inference, assumption, hypothesis, estimate, recommendation, owner decision and unknown using the supported schema and explicit qualification where needed. Do not convert a model suggestion into an owner decision, or treat placement, filenames, hashes or passed checks as substantive truth/currentness. Validation establishes structure, not truth or sufficient provenance.

Keep bulk text in prepared representations. Extraction does not establish complete visual/table/OCR/raw-row/external-content coverage. An empty KB establishes no absence claim about experiment evidence.

### Corrections, updates and failure

Make material corrections discoverable from the earlier entry: preserve its claim and origin, add a dated, attributed note describing the corrected scope and link to correcting records/evidence. Partial correction does not supersede the whole record. Correct or flag known affected core summaries and derived retrieval material within scope; report unresolved affected material without an unrelated whole-KB audit. Changed source bytes need a new source identity. Preserve natural failures and the interventions behind assisted recoveries.

For an intentional source edit, link the existing source to a newly registered version using explicit supersession metadata. Keep the old locator, hash, coverage, and record references. Deep verification checks its pinned local Git version when that reproduces the registered bytes, while the successor is checked against the working file. If earlier bytes were never committed or are unavailable, record that limitation and its reason explicitly; historical-byte verification then reports a warning rather than blocking unrelated KB work. Never silently accept a changed current source or describe unavailable history as verified. Historical prepared evidence retains its own hash and identity checks; preparation of changed bytes uses the new source ID.

Use supported tools for canonical changes. Inspect current revision, exact changes, permitted source reads and competing writers; place the transaction inside the authorized KB, dry-run against that revision, inspect every relevant preview page, apply once against the same revision, and verify. Whole-object upserts replace optional fields; shallow `core_patch` replaces selected top-level values. Retrieval projections are not complete replacement objects.

Both dry-run and apply deeply validate current source versions, declared local Git history, and prepared evidence, reporting explicit historical gaps. Local Git verification reads only the exact registered path at a pinned commit; it does not fetch, check out, commit, or run source code. If that access exceeds the task, stop the affected mutation and continue independent permitted analysis. Prepare only registered permitted sources, with the actual nonempty authoring Actor and explicit coverage/limitations.

Inspect structured errors and `write_outcome`, including cleanup failures. Timeout, process failure or missing output does not prove rollback. Multi-file updates are not crash-atomic and mutations never retry automatically. Stop affected writes on partial/uncertain replacement, collision, unexpected scope or recovery-required state; inspect evidence within authority before choosing recovery. Report material changes, source horizon, actual verification, uncertainty and unresolved corrections/recovery, then finish the assigned task. The KB does not launch background maintenance, recurring scans, helper roles, agent hierarchies or redesigns.

<!-- END REQUIRED KB POLICY -->

## Project entry and instruction loading

The root `AGENTS.md` requires explicit loading of the complete specialized constitution and this required policy. Links are not guaranteed to load their targets. After context loss, read the actual files, validate and load the KB, retrieve relevant records, and reconcile the latest request with the recorded stage.

Use the established Genepool binding and continuing KB authority. Do not rerun installation or initial discovery, request a ceremonial role transition, or infer next-part approval from instruction loading. Package manifests and installation receipts describe the original distribution; leave their historical identities unchanged.

## Layout and checked calls

Paths below are relative to the installed root unless stated otherwise. All IDs, topics, actors and checkpoint names in examples are synthetic placeholders; they are not installed knowledge.

| Path | Content |
| --- | --- |
| `KnowledgeBase/core.json` | Compact orientation, experiment root, revision, routes/selected record IDs and state hashes. |
| `KnowledgeBase/records.jsonl` | Existing typed project records and dated provenance. |
| `KnowledgeBase/sources.json` | Source catalog, revision and source lineage. |
| `KnowledgeBase/prepared/SRC-xxxx.jsonl` | Optional source-bound extraction and coverage metadata. |
| `KnowledgeBase/tools/kb.py` | Retrieval, validation and optimistic transactions. |
| `KnowledgeBase/tools/prepare.py` | Static inspection, preparation and verification. |
| `KnowledgeBase/tools/consolidation_scope.py` | Explicit-checkpoint comparison. |
| `KnowledgeBase/tools/kb_client.py` | Checked calls to allowlisted adjacent tools. |
| `KnowledgeBase/tools/source_versions.py` | Source lineage and exact local Git-byte verification; imported by the KB tool. |
| `KnowledgeBase/tools/test_source_versions.py` | Disposable synthetic integration tests for source revisions and KB safeguards. |

Keep the runtime modules together. Baseline operations and non-PDF parsers use the standard library; Git-history verification additionally uses the existing local Git executable. No database, server, network service, embeddings or package manager is needed. Originals must be inside the physical root and outside KnowledgeBase. Use exact absolute file locators; prepared paths are KB-relative and transactions/checkpoints stay inside the KB.

```text
python -X utf8 -B KnowledgeBase/tools/kb.py validate --compact
python -X utf8 -B KnowledgeBase/tools/kb.py core --budget 6000 --compact
python -X utf8 -B KnowledgeBase/tools/kb.py context --topic TOPIC --records-only --no-core --summary --budget 2500 --compact
```

`python` means the actual available interpreter. For Python orchestration, import the client from the tools directory:

```python
from kb_client import call_tool, ToolCallError
result = call_tool("kb.py",
    ["get", "DEC-0001", "REC-0001", "--fields", "kind", "status",
     "source_refs", "origin", "uncertainty", "derivation", "--compact"],
    expected={"kb_revision": int, "results": list,
              "returned_ids": list, "missing_ids": list})
```

The client runs the current interpreter with `-X utf8 -B`, captures UTF-8 bytes, checks exit status, rejects malformed/duplicate-key/nonstandard JSON and `ok:false`, and validates required top-level types (booleans do not satisfy integers). It never retries calls. Check nested content yourself. `ToolCallError` retains exit status, stdout/stderr, parsed JSON when available and child `write_outcome`. Shared output handling uses UTF-8, including structured errors.

## Schema and identities

- Core requires `schema_version:1`, positive `kb_revision`, exact resolved parent of KnowledgeBase in `experiment.root`, and exact-byte SHA-256 values in `state_files.records_jsonl_sha256` and `state_files.sources_json_sha256`. Core warns above 5,000 estimated tokens and fails above 6,000. Estimate = compact UTF-8 JSON bytes / 3 rounded up, not a model tokenizer.
- Records are one object per line. IDs match `DEC|FACT|CLAIM|INF|ASM|REC|Q` plus `-` and four digits. Required fields: `id`, `kind`, `status`, nonempty `statement`. Kinds: `owner_decision`, `observed_fact`, `source_claim`, `inference`, `assumption`, `recommendation`, `open_question`. Statuses: `active`, `provisional`, `historical`, `superseded`, `disputed`, `open`, `resolved`. Optional context includes `tags`, `source_refs`, `related_record_ids`, `origin`, `uncertainty`, `derivation` and correction notes; references must resolve.
- The source catalog contains a `sources` array and revision. IDs have form `SRC-0001`. File locators use `{"type":"file","path":"<exact absolute original path>"}`, SHA-256 and preferably `size_bytes`. URL locators use `{"type":"url","url":"https://example.invalid/synthetic"}`; registration does not fetch. Existing locator/hash identities are immutable; hash-identical duplicates may declare `duplicate_of`.
- Only an existing source can record `availability.status:"original_unavailable"`, with nonempty `reason`, `observed_at` and valid provenance `record_ids`. This preserves historical identity, not verified availability. A reappeared original is checked again by deep validation. Required prepared evidence remains required even when the original is unavailable.

## Intentional source revisions and exact local history

`kb.py revise-source OLD_ID NEW_ID --expect-revision N --actor ACTOR --reason REASON --record-id RECORD --git-commit FULL_COMMIT_ID --compact` reads and returns a **transaction draft** in the JSON `transaction` field. It writes no files and requires the current registered version to have changed. Repeat `--record-id` for additional provenance. The referenced records may be included in the final transaction. Save that transaction inside the KB, add the actual findings/corrections and core patch, then use the normal complete dry-run preview, apply once at the observed revision, and verification procedure.

Alternatively, pass `--history-unavailable-reason REASON` instead of `--git-commit` when the old bytes cannot be recovered. This explicitly records an evidence gap and yields a deep-validation warning; it never asserts that Git holds uncommitted bytes. A missing previously pinned Git object remains a verification error until its provenance is deliberately reconciled in a reviewed transaction.

For an authorized revision whose registered predecessor bytes are still available but uncommitted, preserve only the affected exact predecessor files inside this KB's `source-history` directory before editing them. Then pass `--history-file EXACT_ABSOLUTE_SNAPSHOT_FILE` instead of either history option. This adapter only reads an already-present file and emits a draft; it never copies, creates, replaces, or normalizes a snapshot. Snapshot creation remains a separately authorized, collision-checked KB effect. It is source-version evidence, not a redundant backup tree or a claim that the KB is committed.

Snapshot history records `kind:"kb_snapshot"` and a strict KB-relative `path` beginning `source-history/`. The file must stay physically inside the exact `KnowledgeBase/source-history`, be a regular file of at most 16 MiB, and contain the predecessor's exact registered SHA-256 and known size. Symlinks, reparse points, traversal, alternate streams, and redirected parent directories are refused. Deep validation and prepared-history verification recheck those bytes; a missing, changed, or unsafe pinned snapshot is an error, never silently converted to an unavailable-history warning. Revision, immutable source identity, provenance links, reviewed transaction preview/apply, and failure rules remain unchanged. Disposable coverage is in `KnowledgeBase/tools/tests/test_source_snapshots.py` and leaves no test history.

The predecessor keeps its source identity and gains `supersession` with `source_id` (successor), `actor`, `reason`, `observed_at`, `record_ids`, and `history`. The successor uses a new ID, the same locator, new hash/size, and `supersedes` (predecessor). Reciprocal links and acyclic lineage are checked. Supersession is only for existing registrations, cannot be mixed with original-unavailable metadata, and does not rewrite past record references. New coverage is not inherited automatically. Existing registered hash, locator, and known size remain immutable.

Git history records `kind:"git"`, a full commit ID, exact repository-relative `path`, pinned `blob_oid`, and `transform:"raw"` or `"lf_to_crlf"`. Only a transformation reproducing the old source's SHA-256 and recorded size is accepted; newline normalization is explicit, not hash equivalence. Verification reads regular blobs of at most 16 MiB from the exact root's local `.git` directory, with no network, filters, text conversion commands, replacement objects, or Git writes. Linked worktrees/shared object stores require a separately scoped adapter. History with `kind:"unavailable"` requires a reason and never reports original bytes as verified. Git availability is a verification prerequisite only for sources explicitly pinned to Git.

Source and prepared search/get/context results label superseded sources as historical. Retrieval still does not rehash originals. `prepare.py verify OLD_ID` can verify retained prepared bytes against pinned Git history, or reports `original_bytes_verified:false` with the explicit history-gap warning. `prepare.py prepare OLD_ID` refuses superseded versions, including upgrades; prepare the current successor instead. Deep validation continues to check all retained prepared hashes and bindings.

Run `python -X utf8 -B KnowledgeBase/tools/test_source_versions.py` using the existing runtime. It creates synthetic repositories and KBs in an automatically cleaned temporary directory inside the KB; it does not transact against the working KB or retain test histories.

## Retrieval and projections

| Operation | Contract |
| --- | --- |
| `status`; `validate` | Cheap structural status; ordinary validation. `validate --deep` additionally reads/hashes local originals and prepared identities. |
| `get ID` | Full single record by default, in `kind`/`value`; unknown single ID is an error. A single source may read prepared content unless `--metadata-only`. |
| `get ID ID...` | `results`, `returned_ids`, `missing_ids`; duplicate IDs appear once in first-request order. Multiple arguments retain batch shape. Batch sources are metadata only. |
| `--metadata-only` | Single-source metadata without original/prepared content reads/hashes; canonical loading and ordinary metadata validation still occur. |
| `--fields`; `--summary`; `--ids-only` | Up to 32 named/dotted fields with identity retained; labeled summary/ID projections. Summary may shorten statements and omit optional fields. |
| `context --records-only` or `--evidence-limit 0` | Skip prepared scans. `search --records-only` excludes catalog and prepared hits. |
| `--no-core` | Omit core from `core`/`context` after loading it. Core/context `--fields` projects accompanying records only; default core is full (`core_is_projection:false`), `--summary` selects a compact subset. |
| `questions`; `decisions` | Kinds `open_question`; `owner_decision`, limited to statuses `open`, `active`, `provisional` unless `--all`. |

```text
python -X utf8 -B KnowledgeBase/tools/kb.py get SRC-0001 --metadata-only --fields source_id title preparation --compact
python -X utf8 -B KnowledgeBase/tools/kb.py questions --ids-only --limit 10 --budget 1500 --compact
```

Evidence retrieval checks prepared-file hashes and first-row metadata type/source ID/source hash, excluding stale or inconsistent representations without claiming completeness. It does not rehash originals. Use evidence modes only within corresponding content-read scope.

Search ranks complete records of every status, including historical/corrected ones. Its `projection_scope:search_hit` supports `id`, `kind`, `record_kind`, `status`, `title`, `snippet`, `score`, `type`, `locator`, `matched_fields`, `resolution_notes_snippet`. Other requested fields return `unsupported_search_fields`. Hits expose recorded title, matching top-level fields and bounded correction-note snippets as navigation. Retrieve the full record with `get` for provenance and correction scope/status.

## Budgets and continuation

Read pages accept `--budget` 300–20,000 estimated compact-JSON tokens, `--limit` 1–100 and `--offset`. Formatting whitespace and external interface truncation are outside the estimate; prefer `--compact`. Inspect returned/available/total/omitted counts, `next_offset`, `scan_complete`, `continuation_limited`, `truncated`. `omitted_count` includes earlier pages; `remaining_available_count` counts only later items. If metadata/next item cannot fit, a structured required-estimate error requires fewer fields or more budget.

Continue with unchanged query, scope, projection and limit, passing the first page's revision:

```python
from kb_client import call_tool
args = ["questions", "--ids-only", "--limit", "10", "--budget", "1500", "--compact"]
expected = {"kb_revision": int, "records": list,
            "next_offset": (int, type(None))}
page = call_tool("kb.py", args, expected=expected)
revision, ids = page["kb_revision"], []
while True:
    ids.extend(row["id"] for row in page["records"])
    if page["next_offset"] is None:
        break
    page = call_tool("kb.py", args + ["--offset", str(page["next_offset"]),
                     "--expect-revision", str(revision)], expected=expected)
```

Search considers at most 2,000 matching prepared chunks within a fixed candidate horizon. Capped/stale scans have unknown total/omitted counts and limited continuation. Context pagination/counts concern records (`pagination_scope:records`); evidence has separate scan/omission fields. Requested but unscanned evidence sets truncation. Context snippets are not independently pageable; single-source `get` supplies chunk pagination.

## Checkpoint comparison

```text
python -X utf8 -B KnowledgeBase/tools/consolidation_scope.py --checkpoint synthetic-checkpoint.json --budget 1500 --compact
```

The required checkpoint path resolves inside KnowledgeBase; the tool never creates it automatically. Default output is a bounded index of new/modified/removed/unchanged/direct-neighbour counts, input/baseline revisions, checkpoint identity and independent record/catalog agreement.

A checkpoint requires positive `output_kb_revision`, `post_pass_record_fingerprints` mapping IDs to SHA-256, and `fingerprint_convention:"canonical-json-sha256-v1"`. Hash UTF-8 sorted-key compact record JSON (`ensure_ascii=False`, separators `(',', ':')`), no trailing newline. An empty KB uses an empty map. Optional `verification.source_catalog_sha256` stores exact catalog-byte identity. This is a baseline, not acceptance.

- Catalog agreement is `same`, `different`, `unavailable`; no stored hash means unavailable.
- `--details`, `--ids ID...` or projections return selected current records in `items[].value`. Removed entries are tombstones (`record_present:false`). Selection is delta plus one hop of explicit record links in either direction; fingerprint-only baselines cannot recover removed records' old outgoing links. `--ids` must stay in that review set; empty `--ids` requests only the envelope.
- `--metadata` returns bounded prior/current canonical and current raw-line fingerprints. Summary/ID projections omit hashes; use plain metadata or field `record_fingerprint` to retain them.
- Continue with prior `input_revision` as `--expect-revision` plus `--expect-checkpoint-sha256`; both identities are rechecked.
- For older checkpoints without a convention, pass `--fingerprint-convention canonical-json-sha256-v1` only if independently known; output labels the assertion. Unsupported/incomplete checkpoints fail. All records changing is a difference, not a convention error.

## Transactions and preview pages

Allowed transaction fields: `actor`, `note`, `upsert_records`, `upsert_sources`, `core_patch`. This synthetic shape is not an actual finding or authorization:

```json
{"actor":"Synthetic example actor","note":"Not experiment knowledge.",
 "upsert_records":[{"id":"ASM-0001","kind":"assumption","status":"provisional",
 "statement":"Artificial example assumption.","source_refs":[],
 "origin":{"type":"synthetic_example"},"uncertainty":"Not real evidence."}],
 "upsert_sources":[],"core_patch":{}}
```

Use the actual responsible Actor; the string does not establish authority. `apply TRANSACTION --expect-revision N --dry-run` previews; omit `--dry-run` to apply once. Transaction paths must stay inside KnowledgeBase; relative apply paths resolve from the caller's cwd. Record `created_at` is preserved when present; updates supply automatic timestamps/revision. `core_patch` cannot set `schema_version` or `kb_revision`. Record/source deletion is unsupported.

Dry-run returns bounded `changes` with target, ID, JSON Pointer path, added/changed/removed status and automatic-change flag. An object addition/removal has an `object_presence_only` container marker followed by child-field changes; its empty-object value is not complete content. Arrays are atomic. `--preview-values` includes full before/after values; a single oversized value fails explicitly.

For preview continuation, keep the revision and transaction bytes fixed. First-page output includes `transaction_sha256`, `preview_time`, `next_offset`. Reissue identical preview arguments with `--offset NEXT --expect-transaction-sha256 HASH --preview-time TIME`; preserve limit/projection/budget choices. Inspect all relevant changes before apply. For the live apply, pass the reviewed `--expect-transaction-sha256 HASH` together with `--expect-revision N`; omit other preview flags. The tool hashes and parses one transaction read, checks both identities, and refuses transaction drift before replacement. Older callers without the hash remain supported. Preview time is prospective; actual apply generates current timestamps. Preview fields paginate without full values. Hashes establish identity/consistency, not approval.

## Write outcomes and recovery

`apply` and `prepare` return `write_outcome`: replacement progress, commit/verification status, uncertainty, cleanup errors and `rollback_performed:false`.

| Stage | Meaning |
| --- | --- |
| `before_replacement` | No state replacement established; staging may exist. |
| `partial_replacement` | Some replacements established; inspect recovery state before mutation. |
| `replacement_uncertain` | Attempted replacement cannot be established. |
| `commit_completed_verification_failed` | State committed; post-commit verification unsuccessful. |
| `commit_completed_reporting_failed` | Commit and verification completed; final reporting failed. |
| `commit_completed` | Commit, verification and reporting succeeded. |

Tools stage on the same volume and replace `core.json` last as the commit marker. Optimistic revision/hash checks detect competing/inconsistent state but provide no multi-writer lock: coordinate one writer. On `RECOVERY_REQUIRED`, inspect state. Even unchanged hashes after an identical-content replacement exception may leave uncertainty.

Preparation deeply validates existing evidence before parsing, then validates the candidate against its owned staged prepared files before publishing. It retains the final deep check; concurrent changes can still cause a reported failure. `--upgrade` does not silently repair corrupt prior evidence. Preparation publishes prepared files, then `sources.json`, then core; `replaced_files` paths are KB-relative. Failure retains published files that a changed catalog might reference; only owned staging files are cleaned. Cleanup errors accompany the primary failure and need inspection even before replacement. Reconciliation compares intended bytes; post-commit deep validation checks records/originals. A verified no-op reports `before_replacement` with `verification_completed:true` and preserves attribution/history.

Broken pipe, termination, timeout or malformed/missing output establishes neither rollback nor failure. Inspect canonical revision/hashes and recovery evidence within authority before deciding any subsequent mutation.

## Static source preparation

```text
python -X utf8 -B KnowledgeBase/tools/prepare.py inspect <exact-absolute-original-path>
python -X utf8 -B KnowledgeBase/tools/prepare.py prepare SRC-0001 --actor "Actual authorized actor" --compact
python -X utf8 -B KnowledgeBase/tools/prepare.py verify SRC-0001 --compact
```

Inspection reads without writing. Preparation needs prior source registration and nonempty `--actor`, including no-ops. New maintenance keeps Actor separate from `tool:prepare.py`; earlier history is unchanged. Existing preparation is a no-op unless `--upgrade` is explicitly authorized. Upgrade uses `prepared/SRC-xxxx.jsonl` and makes no historical copy; preserve earlier derivatives separately when required.

Formats: DOCX, PDF, static HTML, CSV, ODS, Python AST and ordinary text/code (`.txt`, `.md`, `.rst`, `.json`, `.xml`, `.js`, `.ts`, `.cs`, `.scala`, `.sql`, `.yaml`, `.yml`). Sources are hashed before/after parsing. Parsers never import/execute source code, invoke Office/macros/formulas, follow external relationships or fetch resources. ZIP formats check unsafe members/expansion limits; XML checks the first 4,096 bytes for DTD/entities. This is not a general hostile-document sandbox. Heuristic identifier/credential redaction does not establish disclosure safety.

PDF requires available `pypdf` and extracts page text without promising visual/table/OCR completeness. Text/archive size limits reject oversize inputs explicitly; do not represent rejection as full coverage. Prepared rows begin with identity and coverage metadata. Tables produce safe aggregates/schema/counts, not raw-row exports.

CSV v2 infers comma, semicolon, tab or pipe by strict quote-aware parsing of the header and up to 100 complete subsequent records. Exactly one candidate must split the header into multiple fields; zero fails, multiple are ambiguous. Header-only input works if unique. Ragged rows are accepted: missing cells empty, extra cells ignored beyond header width, blank records counted. Inference does not cut quoted multiline fields at a character boundary. The reader parses all size-bounded input and rejects later malformed quotes, but proves neither rectangularity nor whole-file dialect uniqueness. Errors describe structure without echoing cells.

ODS v2 preserves empty/covered positions and repeats. Positive-repeat limits: 100,000 per-cell repetitions, 10,000,000 row repetitions, 100,000 encoded material rows, 100,000 represented cells/row, 1,000,000 represented material cells/document. Exceeding them fails without silent column shifts; trailing blank repeats use arithmetic without consuming material-cell budgets. `material_data_rows` and aggregates count each encoded nonempty row once, without repetition weighting. `logical_rows_including_repetition` counts repeats, header and blank tails: a different population. Numeric conversion uses displayed paragraphs, without recovering formatted/typed-only/rejected values. Numeric counts may be below nonempty counts; aggregates do not imply full-population coverage. Formulas remain inert; external data is never refreshed.

<!-- END SEED KB REFERENCE -->
