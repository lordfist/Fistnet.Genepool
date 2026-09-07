# R02 Part 4 review — starting setups and ecological evaluation

Genepool Analyzer · completed for owner review on 2026-09-07 (Europe/Zagreb).

**Implementation and functional verification: PASS. Ecological verification through the agreed 2,048 seasons: INCONCLUSIVE. Owner acceptance: pending.** Part 3 is explicitly accepted by the owner. Part 4 is ready to review with its limitations stated; no default promotion, reserved confirmation run or R02 closure has occurred.

## What changed

The new-world dialog now offers **Founder DNA → Gather + reproduce** and a matching comparison preset. It reserves two distinct random DNA slots for a gathering gene and a reproduction gene. Remaining genes and targets remain random, with the existing maximum of four genes per action type. This provides available genes; it does not guarantee successful feeding, mating, births or survival. Supplied occupants, inherited genomes and descendants remain governed by their existing behavior.

**Unrestricted random initialization remains the default.** Its prior reference states and random draws remain unchanged. The chosen founder option survives frame capture and restart and appears in the viewer configuration. The dialog also exposes **Food regrowth 2** as a preset; the existing capped-health option was evaluated without changing its mechanics. Each comparison preset resets other parameter families to their defaults. Manual settings remain available for a new run.

The automated evaluation now measures actual population, original-founder survivors, descendants, generations, placed births and removals, food, moves, gathering, damaging attacks and transfers. Exact DNA pattern identity includes all eight ordered action/target pairs, independent of random legacy codes, learned preferences or ancestry. The viewer's action-type-only grouping is deliberately coarser.

## Agreed screen and execution

The owner accepted: population must remain at least 10% of its actual starting count; the same exact pattern must not exceed 90% of living organisms for 128 consecutive completed seasons; reproduction and varied actions must continue; evaluate 2,048 seasons on four comparison seeds and two reserved confirmation seeds, with timeouts inconclusive.

The prospective operational detail was selected by Genepool Analyzer: skip the first 128 seasons for activity, then require at least one placed birth and two action types with actual committed effects in each complete nonoverlapping 128-season window. Incomplete windows are unclassified. Spending resources counts as an effect; blocked zero-effect movement does not. Population is compared with the actual initial count, rather than empty board cells or cumulative deaths. Peak drawdown is separate. There is no predator quota.

The frozen contract is [R02_PART4_CONTRACT.md](D:/Posao/Fistnet.Genepool/KnowledgeBase/R02_PART4_CONTRACT.md). It names four fixed configurations, seeds 11/29/47/83, shared checkpoints 128/512/2048, a completed-season stop near 110 seconds, watchdog at 115 seconds, and outer process cap at 120 seconds. Runs were sequential. No candidate was retuned, combined, rescued, restarted or given a larger time limit after results were seen. The four production batches each stayed under their ten-minute cap.

## Results

All 16 production processes reached a clean completed-season time stop after approximately 110 seconds. They reached 841–1,281 seasons; **none reached 2,048**. No collapse or sustained-domination violation was observed. All 105 complete activity windows passed, with at least 117 placed births and 7 effectful action types per complete window. Partial trailing windows remain unclassified. Accounting reconciled throughout the reported checks.

| Setup | Seasons reached across four seeds | Lowest population, % of initial count | Largest exact-pattern share observed | 2,048-season outcome |
| --- | --- | --- | --- | --- |
| Unrestricted random default | 980–1,281 | 61.2–68.4% | 20.7% | Inconclusive: 4/4 time-limited |
| Gather + reproduce founders | 841–1,274 | 83.0–93.4% | 29.3% | Inconclusive: 4/4 time-limited |
| Food regrowth 2 | 874–1,170 | 62.8–75.1% | 30.2% | Inconclusive: 4/4 time-limited |
| Capped health 50 | 950–1,132 | 59.1–73.6% | 28.5% | Inconclusive: 4/4 time-limited |

Each minimum range above contains the four runs' own lowest population divided by that run's initial population. It is not survival of the original individuals. The pattern column is the worst exact-pattern share across all observed seasons and all four seeds; it is not a lineage or species claim. Final extrema survive the every-eighth-season display sampling.

**Constrained founders are promising for avoiding the initial population dip.** At the shared 512-season checkpoint, their minimum populations were 83.0–93.4% of their initial counts, versus 61.2–68.4% for the unrestricted default. This difference appeared in each of the four seeds. The constrained worlds also performed more reproduction and often carried larger populations earlier, increasing the work required. These are observed bounded differences; they do not establish full-horizon ecological superiority or a performance improvement.

The default already maintained an active, varied population in these observed runs. Changing its default therefore has no demonstrated need under the completed evidence. None of the optional configurations has met the prospective full-horizon eligibility requirement. **No candidate was selected; reserved confirmation runs on seeds 101 and 211 were not dispatched.** The recorded decision is [r02_part4_selection.json](D:/Posao/Fistnet.Genepool/KnowledgeBase/r02_part4_selection.json).

| Setup | Seed | Seasons | Initial | Minimum | Final observed | Placed births | Removals | Highest generation | Maximum exact pattern |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| default | 11 | 1,105 | 996 | 681 | 7,934 | 112,871 | 105,933 | 62 | 7.06% |
| default | 29 | 1,281 | 1,039 | 670 | 9,614 | 127,997 | 119,422 | 62 | 20.36% |
| default | 47 | 1,079 | 1,006 | 616 | 9,061 | 194,478 | 186,423 | 54 | 16.39% |
| default | 83 | 980 | 1,021 | 674 | 9,685 | 16,920 | 8,256 | 39 | 20.66% |
| constrained | 11 | 859 | 996 | 827 | 9,289 | 51,170 | 42,877 | 35 | 6.49% |
| constrained | 29 | 1,274 | 1,039 | 970 | 7,689 | 153,020 | 146,370 | 71 | 19.39% |
| constrained | 47 | 841 | 1,006 | 901 | 9,386 | 61,388 | 53,008 | 58 | 15.88% |
| constrained | 83 | 879 | 1,021 | 947 | 9,217 | 39,432 | 31,236 | 44 | 29.28% |
| regrowth2 | 11 | 939 | 996 | 671 | 9,614 | 147,363 | 138,745 | 44 | 15.73% |
| regrowth2 | 29 | 1,170 | 1,039 | 678 | 8,534 | 161,040 | 153,545 | 74 | 24.05% |
| regrowth2 | 47 | 1,098 | 1,006 | 632 | 9,605 | 80,766 | 72,167 | 65 | 30.19% |
| regrowth2 | 83 | 874 | 1,021 | 767 | 9,826 | 31,553 | 22,748 | 39 | 10.31% |
| capped | 11 | 1,042 | 996 | 733 | 8,517 | 113,993 | 106,472 | 54 | 11.90% |
| capped | 29 | 950 | 1,039 | 656 | 9,485 | 43,633 | 35,187 | 44 | 15.58% |
| capped | 47 | 1,132 | 1,006 | 619 | 9,424 | 86,465 | 78,047 | 68 | 28.53% |
| capped | 83 | 1,100 | 1,021 | 603 | 9,818 | 19,817 | 11,020 | 29 | 15.18% |

The comparisons use damage-only attacks, so zero food transfers do not establish predator feeding. Turnover and generations establish reproduction, not inherited adaptation. These observations do not demonstrate biological realism, balance for all seeds, or the absence of later failure.

## Calculation time and observation cost

The collector accounted for approximately 2.14–3.17% of measured engine-call time across the 16 runs. Engine calls include collector callbacks; the reported subtraction removes their timed bodies while retaining hook dispatch. Census is part of collector time; initial census, export preparation and final reference hashing are separately labeled.

This makes simulation calculation the main cost in these runs. Elapsed-time differences between presets reflect different populations and histories, so they cannot establish a behavior-preserving optimization. Cumulative allocated bytes are reported separately and must not be read as peak/resident memory. A useful next focused step would investigate long-lived, highly populated workloads and allocation costs, then repeat a prospectively bounded long-horizon comparison. That work has not started.

## Verification and compatibility

- Whole solution built using the installed Visual Studio 2022 MSBuild in both Debug and Release, with **zero errors**. Both builds retain the existing 106 CA1416 platform-analysis warnings.
- **137/137 automated tests passed in each configuration**, including the existing default reference expectations and 22 added founder/ecology cases. Release suite: 57.26 seconds; Debug: 57.68 seconds.
- All **83 source/build inputs** and **five loaded assemblies per configuration** matched current bytes. The final Release suite, four observer comparisons and 16 production comparisons all used the same five Release assemblies.
- Observer on/off produced identical complete reference state hashes and random-draw records for seeds 11 and 29 through 128 seasons. Four processes completed; observation did not change those worlds.
- **37 disposable or in-memory tool checks** covered invalid launch requests, pending/timeout evidence, report replacement, arithmetic and identity audits, and refusal to rank incomplete reports. The final exported-result audit found zero errors across all 16 comparison rows. That audit checks exported evidence; it does not independently reconstruct unsampled seasons.
- Main viewer and new settings previews were inspected. Settings were rendered at representative 100/150/200% scaling; tests verify field reachability and fixed action buttons. This is programmatic rendering, not a physical monitor-transition test or owner acceptance.
- Temporary build, run, fixture and preview directories were removed. Current aggregate reports remain, without a TestResults archive tree. No dependencies, project configurations, solution mappings, Git commits or remote operations were introduced by this part.

Pre-trial review corrected the new snapshot test's reference lookup, made interrupted reports retain earlier failures, and added exact extrema so a short-lived spike cannot disappear between saved samples. The first Release suite passed 136/136 before the additional extremum regression; the final suite passed 137/137 in both configurations.

## Evidence and KnowledgeBase

Source horizon: local HEAD `c5a494a412684d801031d1dec2c2c7670f5e3293`, with accepted Part 3, specialization and Part 4 edits still local/uncommitted. This part changes ten previously registered sources and adds five source files. All ten exact predecessors were preserved before editing: one pinned Git version and nine smallest-needed KB snapshots. The pre-existing SRC-0115 historical byte gap remains explicitly disclosed; Part 4 created no new historical gap.

An early authority-only KB dry-run was rejected before replacement because root allowed a helper source edit first. Direct owner authority and the prospective contract existed before trials. The final reviewed transaction reconciles the actual authority, source versions and completed result together; canonical state was not silently reset or relaxed.

- [Verification and current artifact identities](D:/Posao/Fistnet.Genepool/KnowledgeBase/r02_part4_verification.json)
- [Per-run results and complete exported metrics](D:/Posao/Fistnet.Genepool/KnowledgeBase/r02_part4_results.json)
- [Read-only comparison summary and audit](D:/Posao/Fistnet.Genepool/KnowledgeBase/r02_part4_comparison_summary.json)
- [Source horizon and exact predecessor recovery](D:/Posao/Fistnet.Genepool/KnowledgeBase/r02_part4_source_horizon.json)
- [Current build report](D:/Posao/Fistnet.Genepool/KnowledgeBase/r02_part4_builds.json)
- [Automated evaluation instructions](D:/Posao/Fistnet.Genepool/Fistnet.Genepool.Tests/README.md)
- [KB transaction/validation receipt](D:/Posao/Fistnet.Genepool/KnowledgeBase/r02_part4_kb_receipt.json)

## Owner review

Open the full solution in Visual Studio 2022 and run the Windows Forms application. In **New simulation**, try **Gather + reproduce repertoire** and inspect the Founder DNA selection, then use **Restore defaults**. To reproduce a chosen world, clear **Random** and enter a seed. Check that the active configuration describes the choice and **Restart** repeats it. At a larger text size, use the settings scrollbar to reach the lower policy fields.

![Updated starting settings](D:/Posao/Fistnet.Genepool/KnowledgeBase/r02_part4_settings.png)

The new controls and evaluation support are ready for review. The long-horizon ecological conclusion remains incomplete. Stop here for the owner's review; do not promote a policy or close R02 implicitly.
