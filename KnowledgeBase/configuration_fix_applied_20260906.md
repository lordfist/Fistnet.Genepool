# App solution configuration fix — applied and owner-verified

**Latest status, 2026-09-06, KB revision 9:** User confirms the fix worked and the WinForms application rebuilt and ran successfully. The attached Visual Studio output shows **4 succeeded, 0 failed, 0 skipped**, including App. Recorded in CLAIM-0002 and FACT-0021; pending verification of this fix is complete. KB deep validation passes with zero errors and warnings. The sections below preserve the earlier horizons.

Seed Analyzer, 2026-09-06. Current owner instruction: “Fix it. And i will verify if it works.” The owner waived extra durability work and identified GitHub as the source recovery mechanism.

Applied REC-0004 to `D:\Posao\Fistnet.Genepool\Fistnet.Genepool.sln`: changed the ten App project mapping values from Debug/Release x86 to Debug/Release Any CPU and added the two App Build.0 entries for the solution's Any CPU configurations. The existing App `PlatformTarget` remains x64. No application source or project file changed; no build or experiment execution occurred.

Static verification passed: twelve App mapping entries all target the appropriate Any CPU project configuration; the diff has only twelve added/ten removed mapping lines and passes `git diff --check`. Reversing just those edits in memory reproduced the previously registered exact source hash, demonstrating preservation of the other bytes. Original CRLF formatting and UTF-8 BOM were retained. No backup tree, commit or push was created. Owner build verification is pending.

## Historical KB status at revision 7

Canonical KB remains at revision 7. Its existing `SRC-0001` identifies the pre-edit solution (SHA-256 `FC104E50DED1E70741D58453D29E63EC21373775ECEC9F413137096115EAAFAD`). The authorized edited solution has SHA-256 `ADA215E7C668B4DAAA500C4681586CDA87EECB40CC51FE842C8E63C9EAB1426C`, 4942 bytes.

Deep validation now reports exactly the expected SRC-0001 stale-hash and size-mismatch errors. This is the intentional source edit, not an unaccounted mutation. The installed transaction tool treats existing source locators/hashes as immutable, retains all historical catalog rows, and deeply validates every existing source against the live path, including sources marked unavailable when that path exists. Simply registering a new ID cannot remove the old mismatch. No supported source-change reconciliation was found in the inspected validation/upsert paths, so no canonical transaction or tool/governance modification was attempted.

This note supersedes the practical “repair not applied” state in FACT-0018 / REC-0004 and core.environment.configuration_finding until the KB's source-change handling is explicitly resolved. Preserve this disclosure; do not represent canonical deep validation as passing or repeat the already-applied source fix. The source fix itself is complete, and the owner will verify it in Visual Studio.

## Reconciliation completed — 2026-09-06, KB revision 8

The owner subsequently authorized adapting the KB tools for Git-versioned source changes (DEC-0006). Seed Analyzer added explicit source revisions, verified SRC-0001 against pinned local Git commit `4e3d72d4e00d6ae91ed45983b480c3171bb2fca4` with an explicit LF-to-CRLF conversion reproducing its exact SHA-256 and size, and registered the repaired solution as SRC-0042. The original identity and historical claims remain intact. FACT-0019 records the applied fix; FACT-0020 records the tool adaptation and 14 passing disposable integration tests. FACT-0018, REC-0004, Q-0002 and the compact core now link to the updated state.

The complete transaction preview was reviewed, applied once from revision 7 to 8, and verified. Deep validation passes with zero errors and zero warnings: 36 records, 42 source identities (one historical), and seven prepared representations. No recovery or cleanup failure occurred. The revision-7 failure above is resolved; it is retained here solely as historical context. The solution bytes remain the already-applied fix, and owner Visual Studio build verification is still pending.
