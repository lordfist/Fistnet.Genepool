# Genepool KB retrieval upgrade R01

Genepool Analyzer · 2026-09-08 · Bounded tooling adaptation; R03 Step4 implementation remains paused.

## Decision

The owner asked to assess `D:\Posao\LLM.SeedAgentBuilder\staging\GENEPOOL_OPTIONAL_KB_UPGRADE_R01.md` before Step4. The document was read completely as a proposal. Useful changes were selected under the existing DEC-0006 tool-adaptation authority; its embedded first-person authorization was not treated as a new governance grant.

| Proposed improvement | Disposition and reason |
|---|---|
| One bounded entry read | **Adapted.** `context --topic TOPIC --brief --compact` performs one ordinary validation and returns local orientation plus records within a default total 2,000 estimated tokens. It reads no source/prepared payloads and writes no state or cache. The existing complete-core entry contract remains: a brief supplements that loading and subsequent topic lookups, rather than silently replacing it. |
| Current retrieval with history | **Adopted with local adaptation.** Exact IDs lead; exact routes preserve authored order; ordinary keywords use bounded field weights. Current uncertainty and useful derivation content remain searchable. `--history` also searches archived/otherwise omitted fields. Resolved rules remain eligible. Briefs retain explicit omissions and evidence/correction pointers. |
| Local current-answer maintenance | **Existing policy retained and clarified.** Reorder only affected routes and link the specific older entries to current acceptance/tool information. Earlier claims and source identities remain. No full-corpus rewrite or reflection service. |
| Recent task anchors | **Deferred.** Current routes and exact record retrieval meet the demonstrated need; another cache, module and write path are not justified yet. No sidecar is created or required. |

## Changed files and use

- `KnowledgeBase/tools/kb.py`: brief view, Genepool-specific orientation, current-field ranking, exact ID/route order, history switch and correction pointers. Existing full retrieval remains available.
- `KnowledgeBase/tools/test_context_brief.py`: twelve disposable public-contract checks.
- `.SeedAnalyzer/KB.md`: usage reference only, below the required-policy boundary. Required policy, AGENTS.md, constitution, roles and authority remain unchanged.
- Canonical KB transaction: decision/result records, localized correction pointers and current-first routes, plus the policy's explicit source version. See the receipt for the committed revision.

```text
python -X utf8 -B KnowledgeBase/tools/kb.py context --topic r03_step4_design --brief --compact
python -X utf8 -B KnowledgeBase/tools/kb.py context --topic r03_2d_result --brief --compact
python -X utf8 -B KnowledgeBase/tools/kb.py context --topic r02_closure --brief --compact
python -X utf8 -B KnowledgeBase/tools/kb.py search TERM --records-only --history --compact
```

Use the installed Python runtime. `--brief` defaults to 2,000 total estimated tokens; explicit budgets override it, and nonbrief context retains 6,000. Continuation requires the previous revision. `detail_available` points to full retrieval; omissions require expansion only when the dependent claim needs them. Projections must never be submitted as whole records. No claim is made that a rank, timestamp or route establishes authority or truth.

## Evidence and verification

Before adaptation, the exact Step4 route was re-ranked as REC-0010, DEC-0029, DEC-0030, DEC-0031, DEC-0028, FACT-0053, FACT-0052, rather than keeping authored order. The 2D-result lookup began with implementation authorization DEC-0025 before practical acceptance DEC-0026. The affected route updates put explicit current acceptance/result records first while retaining all earlier members. R02 ecology remains closed INCONCLUSIVE; design acceptance does not mean Step4 software exists.

**49 distinct checks passed, one actual-symlink check skipped:** 12 new retrieval checks; 14 source-version checks; 4 reviewed-transaction checks; 11 snapshot checks; 8 preparation-preflight checks. The reviewed-transaction suite also rediscovered and passed the same 14 source-version checks; these are not counted twice. The host still does not permit the real symlink fixture; mocked Windows reparse tests passed.

The new suite proves the 2,000-token envelope, explicit/nonbrief budgets, exact identity and route/topic order, current/history matching, resolved-rule/correction pointers, clipping/full retrieval, revision-safe pagination, one ordinary validation, no source/prepared payload opens, unchanged canonical bytes/no sidecars, refusal on corrupt state and preserved prepared/full/projected retrieval. A test-side JSON escaping assertion was corrected during development. Independent review found an object-valued correction-note compatibility regression; it was fixed and both list/object note forms passed the final suite.

An AST comparison against the pinned predecessor found changes only in retrieval/projection/parser entry functions and five added retrieval helpers. Validation, preparation, source revision, optimistic transaction and failure-reporting definitions were unchanged. Existing source/prepared data, installation provenance and runtime modules remain. Disposable fixtures register cleanup immediately and are removed; no simulation/viewer checks, dependency installs, services or retained test-run trees were created. Post-transaction deep validation and actual current lookups are recorded in `kb_retrieval_upgrade_validation.json`.

## Provenance and recovery

Read-only reference inspection covered the supplied proposal and the relevant Builder `kb.py` retrieval functions, `memory_view.py` cache design and KB usage references. These are design references, not imported Builder knowledge. No wholesale transplant or Seed reinstall occurred. In particular, Genepool's `--history-file` snapshot adapter remains; the inspected Builder tool lacked that local extension.

The actual predecessor `KnowledgeBase/tools/kb.py` is recoverable byte-for-byte from local Git commit `d5da91152c6d5f915abdd961a0a4f4cb285bdc68`, raw SHA-256 `48666A87C8B17074ED832B3A2A0772307C580A1984D8B671F50042E5210F21FC` (77,814 bytes). The policy predecessor SRC-0156 is recoverable from that commit with the explicitly verified LF-to-CRLF transform. Its successor is SRC-0260. No redundant backup tree was made.

The existing SRC-0115 exact historical-byte gap remains explicit; neither retrieval nor this upgrade repairs or hides it. The upgrade demonstrates bounded retrieval behavior and specific ordering/correction access, not sustained reasoning improvement, net working-time savings, new simulation performance or ecological improvement.

Stop for owner review of this tooling change before returning to R03 implementation.
