# R02 Part 3 implementation and verification contract

Seed-derived prospective contract, 2026-09-06. Authority: DEC-0015. Source horizon: clean local commit `c5a494a412684d801031d1dec2c2c7670f5e3293`; starting KB revision 30, authority recorded at 31. Part 2 is owner-accepted; its partial longer production observations remain partial.

## Scope

Implement Part 3 of the existing R02 proposal: a descriptive resizable WinForms viewer, a single simulation owner with detached latest-frame observation, optional settings applied to new runs, and measured computation improvements. Preserve all five projects, Visual Studio 2022 and the .NET 9 SDK pin. No new dependencies, Git writes, persistent Actors, ecological policy promotion or Part 4 comparisons.

Transient helpers implement the observation backend, viewer and measured performance work. Seed Analyzer owns integration, final verification and canonical KB writes. All changes stay inside the assigned root. Existing source versions are recoverable from the verified local commit; use supported source revision transactions after implementation.

## Behavior

- Retain the 100 by 100 world and accepted Part 2 default action/resource rules and random initialization. New settings expose density, explicit/random seed, initial local food, regrowth, starting reserves and the existing independent candidate policies. Validate before resetting; apply only to a new run. Named comparisons are options, not calibrated ecological recommendations.
- One worker initializes and changes the world. UI actions send requests and read detached completed-season values. Keep a latest frame slot rather than an unbounded queue. Stable selection consumes no simulation randomness. No reflection-based verification snapshot on the live display path.
- Provide a board-dominant resizable layout, fit/zoom/pan, organism/food/combined layers and food legend; named pattern grouping with counts/percentages and matching highlights; selected organism identity, genes/directions, resources, age definitions, action/outcome, follow/death state and a bounded last-16 trace. Separate genetic repertoire from performed actions. Keep histories bounded.
- Display scheduling targets 30 Hz independently of target and actual simulation speed. Pause/reset acknowledge immediately and settle at a completed-season boundary; explicitly show pending state and settlement latency. Slow calculation must not be disguised as fresh progress by repainting an old frame.
- UI targets on defined empty/default/full fixtures: p95 measured paint below 33 ms and input acknowledgment below 100 ms. These are targets, not previously established capabilities. Measure 100/150/200 percent representative scaling, selection, food, follow/death/reset, resize, overlays and a slow worker, with a small number of actual screenshots.
- Preserve reference behavior and random draw order for computation optimizations. Any intentional metadata addition to verification snapshots is separated from a behavioral change. Do not silently change action scheduling or ecological constants to improve timings.

## Bounded verification

1. Baseline performance: existing Release diagnostic fixtures at 0/10/50/100 percent exact occupancy and early/large (32-context) histories, diagnostics off, four age batches per process. Two fresh-process repetitions in opposite matrix order, at most 30 seconds per process and six minutes for the initial matrix. Capture median engine duration and allocations; fixed occupancy prevents collapse masquerading as optimization. Existing fixture warmup convention is reported.
2. Prospectively select a throughput target after inspecting this baseline and before optimization; record it in the current performance result. Compare simple serial and one bounded parallel layer, and measure decision/birth costs separately where useful. No timing-based model tuning or unbounded exploration. Any supplemental microbenchmark is fixed work with one warmup and five measured repeats, within the same six-minute version budget.
3. Final Debug and Release whole-solution builds and complete automated suites, normally at most 120 seconds per process. Tests must check source behavior, observation passivity, setting defaults/ranges/reset, worker lifecycle and responsive controls. Record natural failures and their specific corrections. Do not retain temporary test/build caches.
4. Repeat matched performance fixtures within the same limits. Check compatible reference states/random draws against Part 2 and observation on/off. Do not repeat four long ecological production baselines merely to test the viewer. Reserved seeds 101 and 211 remain unused.

PASS requires all applicable functional gates and measured targets. A missed mandatory target, incomplete fixture or unavailable interactive verification is reported as PARTIAL rather than converted to success. Owner acceptance is separate. No ecological success or extinction/takeover conclusion is made in this part.

Finish with reviewed KB source/record/core transactions and deep validation, present the review, then stop. Part 4 requires the next owner instruction.

## Implementation details fixed before final measurements

The unchanged performance baseline completed all 16 fixed-workload processes. Before optimization measurements, the performance helper selected a target of at least 20 percent lower full-occupancy/large-history median engine duration, with no nonempty fixture median regressing by more than 10 percent. These thresholds and the baseline assembly identities are recorded in `r02_part3_performance.json`.

The viewer publishes at most about ten fresh completed frames per second while its UI timer schedules at most thirty updates per second. It retains 240 sampled completed-season chart points, and sixteen consecutive observed seasons for the selected organism. Chart rate calculations divide cumulative counter changes by the actual season interval; they do not pretend skipped frames were consecutive seasons. Up to 64 markers represent the latest published season, so fast playback can skip unselected visual events. This avoids a population-wide event archive.

Default reference compatibility is checked against all four exact accepted Part 2 state hashes and random-draw counts inside the existing scenario cases. New options are omitted from the full-state exporter only at their explicit old default values; nondefault settings are included. Transient display-only choice labels are excluded from execution state. Model state is not normalized away to make comparisons pass.
