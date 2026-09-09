# R04 Step 1 — VS2026 / .NET 10 migration result

**Status: implemented and verified; awaiting owner review. Step 2 has not started.**

The complete six-project solution builds from [Fistnet.Genepool.slnx](D:/Posao/Fistnet.Genepool/Fistnet.Genepool.slnx) in Debug and Release with the installed VS2026 toolchain. The legacy `.sln` remains byte-identical; the installed solution parser confirms all 36 project/configuration mappings and build flags match.

## Changes and toolchain

- All six projects target `net10.0-windows7.0`. The retained Windows API suffix is not a claim of Windows 7 deployment support.
- Root `global.json`: SDK `10.0.401`, `rollForward: latestPatch`, prereleases disabled. CLI and VS MSBuild resolve the same SDK, including the Godot project.
- Verified environment: VS2026 Community 18.9.3, MSBuild 18.9.1.35102, Windows SDK root `D:\Windows Kits\10`. Godot remains 4.7.2 with the Compatibility renderer.
- Both Godot lockfiles were deliberately regenerated and reviewed. Only the framework key changed; package versions and content hashes did not. Debug retains GodotSharpEditor; ExportRelease does not. Final restores were locked.
- Helpers follow root policy, identify current targets and built outputs, invalidate stale results, and clean disposable process data. Windows platform SDK discovery is separate from .NET SDK selection. Explicit restore uses root NuGet.Config; VS’s NuGet SDK resolver uses its standard configuration chain, including the root policy, and requires normal host access.
- Project instructions, launch guidance and current development notes now use VS2026/.NET 10 and the primary `.slnx`.
- Of the 100 task-entry C# files, 98 are unchanged. The two edits are the passive snapshot comparison-label correction described below and Godot verification identity/report/shutdown handling. Simulation rules, DNA behavior, rendering design and the four accepted reference expectations are unchanged.

## Build and runtime evidence

| Verification | Result |
| --- | --- |
| Whole `.slnx`, Debug | Locked restore and rebuild pass; 0 compilation errors |
| Console and WinForms/control regressions, Debug | 159/159 pass on .NET 10.0.12; loaded assembly identities checked |
| Whole `.slnx`, Release | Locked restore and rebuild pass; 0 compilation errors |
| Console and WinForms/control regressions, Release | 159/159 pass on .NET 10.0.12; loaded assembly identities checked |
| Original 2D headless (`headless`) | 35 checks pass; Debug assembly and loaded module identities checked |
| Original 2D GPU (`smoke`) | 62 checks pass; Debug assembly and loaded module identities checked |
| Habitat / Step 5 headless (`step4-headless`) | 153 checks pass; Debug assembly and loaded module identities checked |
| Habitat / Step 5 GPU (`step4`) | 217 checks pass; Debug assembly and loaded module identities checked |
| WinForms apphost, Debug | Actual executable starts, owns a responsive window, loads .NET 10.0.12 and closes gracefully; process and scratch removed |

Both final builds emit the same **106 CA1416 platform-analysis warnings** as the .NET 9 comparison. No new warning category or compilation error remains in the final builds; final locked restores have no warnings.

Godot runtime verification exercises **Debug**. Solution Release compiles Godot as **ExportRelease**; this is not an exported Release runtime test. Godot reports **.NET 10.0.12 on both sides** of this comparison, despite the old target being .NET 9. The console comparison actually changes from .NET 9.0.20 to 10.0.12. SDK, target and runtime are recorded separately.

Build reports hash source inputs and output files. Console checks compare reported assembly hashes. Godot also checks the actual loaded module MVID against the exact DLL before hashing that file, including assemblies loaded from streams. The report explicitly does not call this a hash of loaded process memory.

## Reference-state mismatch investigated and corrected

The first .NET 10 Debug suite passed 155/159 tests. All four failures were the saved full-state hash assertions. No expectations were refreshed. A disposable diagnostic ran the existing .NET 9 and .NET 10 binaries for all four seeds and compared their complete raw snapshots.

Every difference was framework assembly-version text in `objects[*].type` for generic collections. After changing only those type-label tokens, each entire snapshot matched its .NET 9 predecessor byte for byte. RNG state, draws and all 128-season trajectory values also matched. The formatter now retains the historical schema-v2 core-library qualification in comparison labels; actual runtime reporting remains separate.

| Seed | Preserved random draws | Type-label differences before fix | Other state differences | Final accepted SHA-256 |
| --- | ---: | ---: | ---: | --- |
| 11 | 843,754 | 5,692 | 0 | `9F48B02B3E1E752A8448BA87F703DD65CC77BAA5EB0AC3316145B8DFEFF600B6` |
| 29 | 935,898 | 9,501 | 0 | `AB4EF8C91070F6C53AFB9FC2BB1217E3D61898FA3CE2BD660156181424A194E2` |
| 47 | 868,307 | 6,108 | 0 | `E3A0251A828379FD3C11A594697467D31A0ED34E03E5711996B83C5D86B124E7` |
| 83 | 840,616 | 6,551 | 0 | `0C6021B6F48DAF361B927E4BBF29F60FC0A1F46603796B4286449BF1AFB33A21` |

All four original hashes and draw counts pass in final Debug and Release, including observer independence and fresh-process replay. The probe and large diagnostic snapshots were removed; compact evidence remains in [reference diagnosis](D:/Posao/Fistnet.Genepool/KnowledgeBase/r04_step1_reference_diagnosis.json).

## Bounded performance comparison

These are local engineering observations, not an ecological trial or a general speedup claim. Measurements ran sequentially. Viewer fixtures replay detached snapshots at 10 Hz at 1920×1080 on the AMD Radeon RX 6800 / Compatibility backend, capped at 60 FPS. Original 2D uses 5 seconds of warmup and a 10-second sample; Habitat uses 3 and 7 seconds. Simulation calculation is excluded from these frame timings.

| View / population / detail | Before p95 frame (ms) | After p95 frame (ms) | Final replay and frame budget |
| --- | ---: | ---: | --- |
| Original 2D / 0 / default | 16.835 | 16.694 | Pass |
| Original 2D / 1,000 / default | 16.880 | 16.908 | Pass |
| Original 2D / 10,000 / default | 17.044 | 16.721 | Pass |
| Habitat / 0 / 1 | 16.679 | 16.686 | Pass |
| Habitat / 1,000 / 1 | 16.687 | 16.807 | Pass |
| Habitat / 10,000 / 1 | 16.683 | 16.739 | Pass |
| Habitat / 10,000 / 3 | 16.692 | 16.692 | Pass |

The console observation-cost fixture keeps 10,000 occupied cells with a fixed Move/Self repertoire, seed 17293, three warmup seasons and five measured seasons per run, with two alternating observation-off/on pairs. Capture is measured separately. No timing threshold determines the test result.

| Configuration / actual runtime | Observation off, mean season (ms) | Observation on, mean season (ms) | Mean capture (ms) |
| --- | ---: | ---: | ---: |
| Debug / 9.0.20 | 152.902 | 169.092 | 4.674 |
| Debug / 10.0.12 | 131.451 | 146.005 | 3.980 |
| Release / 9.0.20 | 108.128 | 113.114 | 2.835 |
| Release / 10.0.12 | 98.724 | 98.220 | 2.622 |

Short-run timing varies with JIT, GC and host activity. The measured fixture is deliberately narrow and does not establish long-history or mixed-action throughput. Complete samples, workload descriptions and allocations are in [comparison evidence](D:/Posao/Fistnet.Genepool/KnowledgeBase/r04_step1_comparison.json).

## Resolved execution issues

The initial helper attempts exposed protected Windows SDK and user NuGet configuration lookups. Windows SDK discovery was corrected; the supported VS NuGet resolver was run with normal host access and root policy. A sandboxed Godot run separately reported a denied certificate-store read and was repeated with normal host access, retaining strict error checking.

The first Godot identity reporter assumed Assembly.Location was populated. Stream loading disproved that assumption and an exception prevented normal shutdown until the watchdog fired. The reporter now verifies loaded/file MVIDs, reports file hashes honestly and guarantees a quit request even when reporting fails. Three targeted synthetic identity cases passed, alongside nine build-helper, seven runner-selection and seven output-identity checks. The original failures and interventions remain concise in the comparison evidence.

## Owner review and stop

Open [Fistnet.Genepool.slnx](D:/Posao/Fistnet.Genepool/Fistnet.Genepool.slnx) in VS2026. Native IDE debugger interaction and WinForms designer interaction remain **manual owner-review checks**; no supported IDE UI inspection was available here. Automated WinForms/Godot startup, controls and rendering checks are recorded separately above. Standalone Godot export packaging and exported runtime remain deferred.

Current source predecessors are preserved through exact pinned Git bytes or the smallest required KB snapshots. No commit or push was performed. The existing SRC-0115 historical-byte limitation is separate from this migration. Step 1 is ready for review; Step 2 and later steps remain unstarted.

Evidence: [builds](D:/Posao/Fistnet.Genepool/KnowledgeBase/r04_step1_builds.json), [locked dependencies](D:/Posao/Fistnet.Genepool/KnowledgeBase/r04_step1_lock_refresh.json), [regressions](D:/Posao/Fistnet.Genepool/KnowledgeBase/r04_step1_regressions.json), [solution mappings](D:/Posao/Fistnet.Genepool/KnowledgeBase/r04_step1_solution_mapping.json), [WinForms startup](D:/Posao/Fistnet.Genepool/KnowledgeBase/r04_step1_winforms_startup.json).
