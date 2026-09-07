# R02 Part 3 — viewer and measured simulation improvements

Seed Analyzer result, 2026-09-06. Authority: DEC-0015, following the owner's acceptance and reported commit of Part 2. Starting source horizon: clean local commit `c5a494a412684d801031d1dec2c2c7670f5e3293`, with canonical KB revision 30. This is derived implementation/evaluation support, not original experiment evidence or owner acceptance.

**Implementation is ready for owner review. All automated Part 3 gates pass.** Overall review remains **PARTIAL** pending hands-on owner acceptance and the historical-byte verification limitation described below. Part 4 has not started; no ecosystem balance, policy promotion or R02 closure is claimed.

## What changed

- The WinForms app starts maximized and can be resized. The 100 × 100 world has fit, zoom and pan, organism/food/combined layers, a 0–10 food legend, selected movement trails and brief event markers. History can collapse in short windows while keeping its data.
- The organism inspector follows a stable run-local identity and shows parents, generation, lifetime, both age definitions, health, carried reserves, local food and offspring observed since selection. Eight named DNA slots include target arrows. The last 16 observed seasons show chosen actions, actual outcomes, whole-season resource changes and learned/exploratory choice labels. Death retains the last observed state. The chosen-slot arrow is explicitly historical because DNA can subsequently change.
- Patterns show action names, counts and population percentages. Clicking a pattern highlights matching organisms. Grouping is by ordered action types, not fitness; directions and learned preferences can differ. The inspector supplies the selected organism's detailed directions. Activity separates gene abundance from performed actions.
- One worker owns world mutation. WinForms reads detached completed-season frames and sends commands. A latest-frame slot prevents a display backlog; colors and aggregate data are built off the UI thread. The display schedules at most 30 updates/s and receives up to about 10 fresh frames/s. Actual simulation speed is shown separately. Pause and restart report pending work and observed settlement latency. Selection cannot cross a pending reset or send commands to a stopped worker.
- New simulation settings expose random or explicit seed, starting density, local food, regrowth and founder reserves. Defaults remain 10%, 3, 1 and 5 respectively. Existing independent learning/health/predation candidates are optional, labeled comparisons. Settings apply to a new run. Restart repeats the configuration and seed. The dialog scrolls when needed while its action buttons stay visible.
- Simulation hot paths avoid repeated temporary sets, candidate lists and inherited-score matching. Ordinary cell refresh/setup runs serially; an explicit bounded parallel alternative remains available for comparison. Action/conflict ordering and accepted default resource policies are preserved.

Histories contain at most 240 sampled completed-season points. Curves derived from cumulative counts divide by the actual interval, rather than presenting skipped frames as consecutive seasons. At most 64 markers represent the latest published season. Fast playback can skip unselected visual events; the selected trace retains its most recent 16 consecutive observed seasons. There is no population-wide event archive.

## Measured computation result

Fixed population avoids an apparent speedup caused by organisms dying. These are synthetic no-op workloads using the production scheduler, not natural ecosystem runs. Each result is the median of two fresh processes in opposite matrix orders, for four cycle batches / 32 seasons; no warmup was discarded. Large history adds 32 external contexts per organism. All 16 baseline and 32 successor/scheduler processes completed within the frozen limits.

| Population | History | Prior engine time | Current engine time | Reduction |
| --- | --- | ---: | ---: | ---: |
| 10% | Early | 0.4952 s | 0.2518 s | 49.2% |
| 10% | Large | 0.5585 s | 0.3230 s | 42.2% |
| 50% | Early | 2.5032 s | 1.4763 s | 41.0% |
| 50% | Large | 2.3549 s | 1.5519 s | 34.1% |
| 100% | Early | 5.7521 s | 3.5897 s | 37.6% |
| 100% | Large | 5.5093 s | 3.6536 s | 33.7% |

The prospective target was at least 20% lower full/large engine time and no nonempty fixture regression over 10%. Both passed. The empty fixtures also completed and remain in the detailed result. Full/large process allocation fell about 34.8%; that measure includes setup. Bounded parallelism did not consistently help and was 2.6% slower on full/large, so serial is retained as the default.

Separate fixed microbenchmarks measured decision/completion at about 4.4–4.6 microseconds, and child construction/inheritance at 12.1 microseconds without external history versus 245.6 microseconds with 32 contexts. These have no predecessor microbenchmark comparison and are not speedup claims. Dense, long-history populations can still be computationally expensive; equal simulation throughput at every density is not promised.

All final workload metrics and random-draw records matched their predecessors. All four accepted Part 2 reference hashes and draw counts also match at 128 seasons. The final 44 DNA/Control source/project inputs and their Release DLLs match the measured engine exactly. App, Visualization and Tests were subsequently repaired and rebuilt; the timing report retains its actual benchmark assembly identities.

## Verification and limits

Visual Studio 2022 MSBuild rebuilt the entire five-project solution in **Debug and Release**, using the existing .NET 9.0.315 SDK. Both builds have zero errors and 106 CA1416 platform warnings. No project, framework or dependency migration was made.

Both complete suites pass **115/115**: Debug in 62.047 seconds and Release in 55.203 seconds. All 78 recorded source/build inputs and all five loaded assemblies were independently checked for each configuration. Tests include reference passivity/replay, exact Part 2 states, default/nondefault settings, worker ownership/pacing/reset/steps, following and death, actual effects, pattern/layer pixels, command races, slow-worker pause, and representative 100%/150%/200% main-window and settings geometry.

Release p95 board paint was 3.59 / 3.70 / 4.68 ms for empty / default / full-board fixtures; selection handlers were 2.29 / 2.12 / 2.06 ms. Complete-form painting was below 17 ms in those fixtures. The slow-worker pause handler took 1.13 ms; the displayed settlement was 172 ms including an intentional 150 ms hold. Debug also met the declared paint and input targets. These are bounded offscreen WinForms/programmatic measurements, not physical mouse-to-display latency or actual monitor-transition tests. Seven current captures support visual review; they include representative font/geometry scaling. Owner viewing still determines usefulness and actual desktop behavior.

Natural failures remain recorded: the first full suite was 111/113, with a real high-scale layout overlap and a synthetic fixture that the world-state verifier rejected. One intermediate rendering attempt still failed. The final geometry fix uses the actual available height; subsequent review added reset/stopped-worker guards, scrollable settings and displayed settlement timing. No failing required case was relabeled as passing.

## KnowledgeBase and history

The update records Part 2 acceptance, this implementation, measured results and the review stop. It registers SRC-0128 through SRC-0153: 16 revised source identities and 10 first registrations (nine new files plus the existing Control assembly attributes). Earlier records retain their dated evidence and gain current navigation notes. Selected current sources are prepared through the bundled tools. The actual final KB revision, transaction/preparation outcomes and deep-validation result are in `r02_part3_kb_receipt.json`.

Thirteen revised predecessors are verified against pinned Git bytes. Two more, SRC-0118 and SRC-0119, were reconstructed from committed text and retained line-ending evidence and saved only after matching their original SHA-256 and size. They are reconstructed exact snapshots, not claimed pre-edit backups.

**One historical-byte gap remains:** SRC-0115, the Part 2 `Board.cs` registration, contains mixed line endings not reproduced by the supported Git transformations. Bounded reconstruction did not recover its exact registered bytes. Its hash, lineage and prepared text remain intact; its successor is verified normally. Deep validation reports this as an explicit warning. The initial assumption that a clean Git state guaranteed byte-exact recovery was too strong; recovery should have been checked before editing. The current source/build/test verification is unaffected, and the Git code version remains available.

Existing Part 2 reports/results/build/verification files are preserved. Governance and KB validator tools were not changed in Part 3. The new bounded check wrapper reuses the existing strict child boundary. Temporary build/test/preview directories were removed. Changes remain local and uncommitted; no Git writes, installations, external research, reserved-seed trials or persistent Actors occurred in this stage.

## Owner review

Open the same solution in Visual Studio 2022 and start `Fistnet.Genepool.App`. Check Combined and Food views, zoom/pan, follow a moving organism and its actions, highlight a pattern, and compare Run/Pause/Cycle at different target speeds. Try an optional density or explicit seed in New simulation; verify Restart and resizing at your normal Windows scaling. Existing policy defaults are unchanged, so this review should focus on explanation, controls and computation. Ecological starting-setup comparisons belong to Part 4 after the next applicable instruction.

Supporting detail: `r02_part3_verification.json`, `r02_part3_results.json`, `r02_part3_builds.json`, `r02_part3_performance.json`, `R02_PART3_CONTRACT.md` and `Fistnet.Genepool.Tests/README.md`. Seed Analyzer owns integration and the KB; transient backend, viewer and performance helpers supplied bounded implementation and review assistance.
