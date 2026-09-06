# R02 Part 1 result: passive diagnostics and measured baseline

Author: Seed Analyzer, assisted by transient DNA, performance and test reviewers. Date: 2026-09-06. Status: Part 1 implementation and verification complete; stopped for owner review under DEC-0012. This is a Seed-derived result, not owner acceptance of R02 or an ecological success claim.

Root: `D:\Posao\Fistnet.Genepool`. Behavioral baseline: local commit `2bb2b99d2f77774a3d2a0614c8dc7747349c5685`. The owner authorized Part 1 only. Parts 2–4 remain deferred. R01 remains separately closed as successful and useful by the owner.

## Delivered scope

Added an optional diagnostic session that counts action invocations, applied effects, scalar state changes, requested/committed movement, actual birth placement, food debits/refills/returns and observed death contributors. It also measures simulation phases, statistics, learning-history size/copying, rendering and existing UI completion paths. Diagnostic state is separate from the reflected organism state and uses weak associations for organism/effect attribution.

Diagnostics are disabled in ordinary app startup. No settings panel, food overlay, larger viewer, action correction, balance change, scheduler optimization, new project or dependency was added. Existing simulation rules, random draws and the R01 UI layout were retained. Reference-state checks verify that claim at their stated deterministic horizon; production scheduling can still respond to observation overhead.

Changed 11 existing source/test files and added four files: `SimulationDiagnostics.cs`, `DiagnosticScenarios.cs`, `DiagnosticsTests.cs` and `UiDiagnosticsTests.cs`. The test executable now supports bounded diagnostic scenarios and controlled density/history workloads. Its README documents invocation and metric semantics. The KB build helper can select a separate current report, preserving R01's build evidence; a bounded local runner uses disposable in-root scratch space and terminates only its own process tree if necessary.

## Verification

| Check | Result |
| --- | --- |
| Unchanged Release baseline | Whole solution built; 43/43 tests passed |
| Instrumented Release | Whole solution built; 52/52 tests passed |
| Instrumented Debug | Whole solution built; 52/52 tests passed |
| VS2022 compatibility | Existing VS2022 MSBuild, SDK 9.0.315, all five projects; zero errors in each build |
| Existing warnings | 163 CA1416 platform warnings in each build, including the unchanged baseline |
| Pre/post deterministic parity | Seeds 11/29/47/83 at 128 seasons: full state hash, random-draw count and recorded trajectory unchanged |
| Additional observer parity | Seeds 11/29, diagnostics off/on at 128 seasons: state hash and draws match the original baseline |
| Current source/build identity | All 65 source/build-input hashes match both final configurations |
| Bounded execution | All four reference processes and all 21 production/fixture processes completed; no missing final output, timeout or unstarted job |

The nine additional tests cover diagnostic passivity, age batching, bounded samples/session lifecycle, actual effects and resource/movement/birth/death observations, contributor provenance after ordinary effect samples fill, controlled workload construction, and offscreen WinForms completion/rendering. During implementation review, terminal-output ownership was tightened to prevent competing timeout/final reports, and bounded contributor details were retained independently of the ordinary effect sample pool. These corrections preceded the passing instrumented builds and suites.

The full machine-readable evidence is in `r02_part1_results.json`; build results and source hashes are in `r02_part1_builds.json`. These are current derived evidence, not an archive of repeated runs. Build/test scratch directories were removed. No Git commit, push, dependency installation or unrelated process operation was performed.

## Natural production baseline

Four headless production runs used the unchanged default founder setup, diagnostics enabled and the real eight-season age batch. Each had a 120-second cap and checkpoints at 128, 512 and 2,048 seasons. The entire production/fixture batch took 89.110 seconds within its 600-second cap. Production is scheduler-dependent: one run per seed is an observation, not a confidence interval or deterministic ecological guarantee.

| Seed | Initial population | At 128 | At 512 | At 2,048 | Mean age time, first third | Mean age time, last third |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 11 | 996 | 1,457 | 885 | 296 | 59.5 ms | 22.1 ms |
| 29 | 1,040 | 1,532 | 1,251 | 2,500 | 79.9 ms | 111.7 ms |
| 47 | 1,006 | 1,576 | 719 | 317 | 53.0 ms | 18.9 ms |
| 83 | 1,021 | 1,704 | 976 | 508 | 68.0 ms | 28.8 ms |

An age here means the existing eight-season batch. First/last-third means use all batches in those segments, including first-use work; no warmup was discarded. Population accounting reconciles exactly in every run: initial population + placed births − removals = final population. World-food accounting also reconciles to the recorded initial amount, actual refill/return and actual debit. These identities verify the observer's accounting of existing behavior; they do not certify that existing food rules are correct.

Across the four runs:

- There were 71,253 actual removals. **69,185 (97.10%) had a same-season starvation contributor.** This is not an exclusive cause-of-death percentage: damage, healing and other contributors can coexist.
- There were 70,811 placed births; **22,700 (32.06%) were placed TopLeft**. Together with the inspected TopLeft-first search, this supports a birth-placement bias. Committed movement was distributed across directions; this does not establish a universal upper-left movement bias.
- The pending food-debit field was overwritten 1,378 times. Final global food remained 97,995–99,951 out of 100,000 capacity. Abundant global food does not establish local accessibility or identify a single starvation cause; it argues for auditing choice, uptake and debit accounting before increasing food blindly.
- Self-attack was observed among contributors to 587 removals. Some generated Kill genes themselves specify Self, so these observations cannot all be attributed to the separate double-choice target defect.

Retained event samples are deliberately bounded and biased toward first events: all 32 retained death events per natural run came from season 1. The long-run proportions above come from aggregate counters, not from extrapolating those early examples. Detailed effects/choices have separate reserved pools, omission counts are explicit, and only the latest 256 season reports are retained. No age-limit deaths within this horizon establishes no lifespan-policy result: organisms use the pre-existing ninth-turn clock.

## Performance findings

Population-sensitive computation is reproduced. Seed 29 grew and its mean age time rose about 40%; the three shrinking populations became faster. The measured eight-season calls in these natural runs stayed below 0.2 seconds. **The reported multi-second ages were not reproduced within this 2,048-season, headless horizon.** This leaves the owner's longer/live-UI slowdown unresolved; it does not disprove it.

Effects and action phases had the largest instrumented wall times. Those are useful next inspection targets, but these timings include enabled observation hooks and do not establish a specific lock, allocation or method as the root cause. Recorded parental-history copying totaled only 0.025–0.204 seconds per complete natural run. Inherited history is therefore not a demonstrated dominant bottleneck here; a longer or different population could behave differently. Seed 29's final checkpoint had 26,953 contexts across living organisms and a per-organism maximum of 52; these are checkpoint observations, not run-wide maxima.

The controlled workload holds population constant, uses no-op genes, and compares two learning-history sizes at 0%, 10%, 50% and 100% occupancy. Small history has two active contexts with eight scores each; large history additionally has 32 unused contexts. Each cell below is the inclusive engine-call time for 64 seasons, eight age batches, without rendering.

| Occupancy | Small history, diagnostics off | Small history, on | Large history, off | Large history, on |
| --- | ---: | ---: | ---: | ---: |
| 0% | 0.074 s | 0.091 s | 0.083 s | 0.086 s |
| 10% | 0.225 s | 0.295 s | 0.300 s | 0.470 s |
| 50% | 1.073 s | 1.476 s | 1.257 s | 2.325 s |
| 100% | 2.263 s | 2.948 s | 2.752 s | 4.434 s |

This isolates a useful scheduling/storage/observation workload, not a natural late population or a birth-copy benchmark. Additional history costs partly belong to the enabled diagnostic census. Each combination is one fresh process, without discarded warmup or repeated estimates. At full occupancy, diagnostics add about 30% to the small-history case and 61% to the large-history case. They are useful for diagnosis and remain off by default.

The separate reference comparison showed 1.67–1.75× engine-call time with diagnostics and rendering enabled together versus both disabled. Direct renderer time was excluded from engine-call timing, but interleaved rendering can affect later cache/GC/scheduling. That comparison is **not an isolated estimate of diagnostic overhead**; the controlled no-render table is the cleaner comparison.

Metric limits: `dna.decision` sums overlapping worker invocation durations including waits; it is not CPU time and cannot be added to phase totals. Parent-copy timing is nested in effects. Summed `census.max_*` values are sums of per-season observations, not run maxima. Allocation counters are cumulative process allocations, including observation/setup/export, not resident RAM or proof of a memory leak. Harness engine-call wall time includes collection bookkeeping; `engine.season` excludes the observer/census.

## UI observation coverage

Eight existing completion/refresh paths were exercised with message pumping and a full-form bitmap draw on an offscreen WinForms window. Release means were 2.14 ms for frame construction, 7.40 ms for the existing UI completion path and 10.54 ms for the separate full-form draw. State/randomness and sampled board pixels were checked.

Offscreen refresh can be clipped. These measurements do not establish interactive input latency, sustained visible frame cadence, percentile performance, or the proposed Part 3 responsiveness targets. The app still has the R01 viewer; this part supplies diagnostic hooks and tests for later work.

## Interpretation and stopping point

The best-supported next step remains Part 2's action/target/outcome and resource-accounting work, after owner review. The death and birth observations give that work a measured basis. Effects/action scheduling costs merit later matched-workload performance investigation. Capping inherited history should not be justified by a claim that it has already been shown to cause the reported slowdown.

Part 1's implementation, compatibility, passivity and bounded-measurement checks pass. The experiment's ecological outcome remains unevaluated: collapse denominator/horizon, takeover identity and predation semantics remain open in Q-0003, and no candidate settings or holdout seeds 101/211 were used. No success claim about balanced evolution or smooth live-UI behavior is made.

The owner requested a stop here. No active trial, background work, Part 2 change or next-part authorization is implied. Canonical KB completion and deep verification are recorded separately by the completion transaction and current verification receipt.
