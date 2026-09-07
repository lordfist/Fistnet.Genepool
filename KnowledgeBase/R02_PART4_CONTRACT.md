# R02 Part 4: starting setups and ecological comparison

Prospective Genepool Analyzer contract, 2026-09-06. User authorized Part 4, explicitly accepted Part 3, and accepted the ecological criteria in the current task before these trials. Starting KB revision 37; local HEAD c5a494a412684d801031d1dec2c2c7670f5e3293. Accepted Part 3 and governance edits remain uncommitted. Exact affected predecessor identities and verified Git/snapshot recovery are in r02_part4_source_horizon.json.

## Scope and candidates

Implement the optional starting repertoire and measurements needed to evaluate Part 4. Keep all five projects compatible with Visual Studio 2022, the existing SDK/framework settings and the accepted default simulation behavior. No external research, dependency installation, Git writes, automatic rescue, mid-run ecology changes or automatic default promotion.

Compare four fixed configurations, using current production execution and the repaired action/resource model:

| ID | Change from accepted default |
| --- | --- |
| default | None: unrestricted random founders, density 10%, cell food 3, regrowth 1 per eight-season cycle, founder reserves 5, repaired legacy learning, legacy overweight health and damage-only attacks. |
| constrained | Only founder repertoire: two distinct random slots contain GenerateFood and CombineDna; remaining six genes remain random with the existing maximum-four-per-type cap. Reserve the mandatory counts before drawing remaining types. Placement and gene targets remain random. |
| regrowth2 | Only food regrowth: 2 per cycle instead of 1. |
| capped | Only health rule: existing capped-health candidate, maximum 50. |

The optional repertoire ensures gene availability, not successful gathering, healing, mating or births. Its additional initialization draws may change later random placement relative to the unrestricted run with the same seed; compare each run to its own measured initial population. Do not claim cell-for-cell paired initialization. Default initialization must retain its prior exact reference state and RNG draws. Supplied test occupant factories remain independent of the optional founder generator.

Show optional repertoire and regrowth settings in the existing new-world dialog and active configuration. Preserve unrestricted random defaults and existing candidate options. No new population-management policy or additional ecological candidate is introduced after observing results.

## Frozen evaluation definitions

Owner-accepted criteria: living population stays at or above 10% of its initial count; the same exact ordered DNA action/direction pattern must not exceed 90% of the living population for 128 consecutive seasons; reproduction and varied actions continue. Evaluate through 2,048 seasons on four comparison seeds, followed by two reserved confirmation seeds for a selected candidate. A timed-out run remains inconclusive at the requested horizon.

Implement these definitions without retroactive tuning:

- Collapse: at a completed season, current population times ten is less than initial population. Equality at 10% passes this particular boundary. Zero initial population has an undefined ecological survival criterion. Track minimum population and peak-relative drawdown separately. Empty cells and cumulative deaths are not collapse measures.
- Exact behavioral pattern: ordered gene action type and target/direction for every slot. Exclude random legacy gene codes, learning scores and ancestry. Recompute each completed season because a target can change during movement. A new dominant key starts a new streak. Equality at 90% does not increment the greater-than-90% streak.
- Continued activity, an Analyzer-selected operational detail: after the first 128 warmup seasons, every complete nonoverlapping 128-season window contains at least one successfully placed birth and at least two distinct action types with a committed actual effect. Report incomplete windows without classifying them. Resource expenditure counts as an actual effect; a blocked zero-effect move does not. Report actual movement, gathering, damaging attacks, predation transfers and other effects separately. There is no minimum predator quota.
- Track living members of the initial cohort, living descendants, placed births/removals, living and highest-observed generations, local food/reserves and transfers. Check initial population plus births minus removals against current population. Do not equate shared ancestry with behavioral monoculture or claim inherited adaptation from population size or individual learning alone.

A completed run passes this operational screen only if all applicable criteria pass. Report every seed individually. An observed threshold violation remains evidence of a violation even when later horizon coverage is incomplete; incomplete coverage cannot support a passing 2,048-season result. A candidate can be recommended as meeting this screen only if all four comparison runs complete and pass, followed by both reserved confirmation runs completing and passing. This strict aggregation is Analyzer-selected and avoids hiding failed seeds. Passing is evidence for these bounded worlds, not a guarantee for every random world or owner closure of R02.

## Bounded execution and selection

1. Add focused metric and initialization tests. Verify exact default replay with existing reference checks. Compare observer on/off using seeds 11 and 29 through 128 reference seasons: four processes, at most 60 seconds each and five minutes total. No ecological candidate selection uses these passivity checks.
2. Run production comparisons in the fixed order default, constrained, regrowth2, capped. For each, run seeds 11, 29, 47 and 83 once through requested checkpoints 128/512/2048, with at most 120 seconds per process and ten minutes per candidate batch. Do not discard warmups or natural failures. Use a completed-season soft stop around 110 seconds and a process watchdog by 115 seconds, with the outer 120-second limit. Stop the batch at its cap and mark unstarted jobs. Run measurement processes sequentially so the comparison does not introduce simultaneous workload contention.
3. If any candidate passes all four full comparison runs, choose among those using continued activity first (minimum complete-window placed births), then diversity (lower largest exact-pattern share), then median elapsed time as a tie-breaker. Keep default as a valid status-quo choice. Record selection and its evidence before running reserved seeds 101 and 211. These two final production processes have the same horizon and 120-second limits, within five minutes. They are confirmation, not feedback for retuning. If no candidate qualifies, report no qualifying candidate and leave reserved seeds unused.
4. If comparisons answer the named question earlier, stop expansion and explain why. Do not silently shorten the horizon, increase a time budget, combine candidates or add a new parameter family to obtain success. No rerun of an earlier implementation version is required solely to reproduce a historical report. Earlier R02 baselines remain explicitly historical context; matched candidate claims use this implementation and its recorded identities.
5. Final Debug and Release whole-solution VS2022 builds and automated suites remain required. Use proportionate dialog/scaling/preview checks for the changed UI; preserve the previously verified viewer. No new broad performance matrix is needed for a change confined to optional initialization and passive off-screen measurements.

Measure engine calls, collector census and export overhead separately as far as the hooks permit; report callback overhead honestly. Keep event/history storage bounded and exports compact. Record runtime, loaded assembly hashes, settings, seed, actual completed horizon and process outcome. Preserve current aggregate reports and useful failed-result summaries; use disposable caches, no TestResults archive trees.

## Completion boundary

The deliverable is the optional setup feature, verified measurement support, honest bounded comparisons and a review recommendation. Distinguish implementation completion, functional verification, ecological screen outcome and owner acceptance. A missed horizon or failed ecological criterion is partial/inconclusive or failed as applicable, not an invented balanced result. Record the actual state in the KB with reviewed source revisions and deep validation, then present the Part 4 review and stop. Do not promote defaults or close R02 without the owner's decision.
