# Improvement R01 implementation and evaluation

Author: Seed Analyzer. Date: 2026-09-06. Status: bounded implementation and automated evaluation complete; owner viewing checkpoint pending.
Root: `D:\Posao\Fistnet.Genepool`. Authority: DEC-0007. This is derived working knowledge, not original experiment evidence or owner acceptance of an enjoyment outcome.

## Outcome

The five-project solution rebuilt with the installed Visual Studio 2022 x64 MSBuild and .NET SDK 9.0.315: Debug / Any CPU and Release / Mixed Platforms. Both have zero errors and 163 CA1416 platform-analysis warnings. The original four-project baseline built with zero errors and 72 CA1416 warnings. Existing project target frameworks and App x64 target are unchanged; `global.json` keeps SDK selection in the installed .NET 9.0.3xx feature band rather than the incompatible default SDK10 toolchain. No dependency installation or download was needed. The earlier missing Newtonsoft.Json HintPath was not a build blocker; no dependency substitution was performed.

Final Debug fast suite: **38/38 passed in 2.49 seconds**. Final Release full suite: **43/43 passed in 16.09 seconds**, comprising 42 simulation/observation checks plus one runner negative-control check. The negative control verified the deliberately failing child exits 7. Source SHA-256 maps, loaded assembly hashes, exact timings, named cases and full current trajectories are in `implementation_status.json`. The final executed Release scenarios stayed within the proposed 60-second total budget; this measurement is not a universal timing guarantee.

## Changes and attribution

- Mutation replaces at most two distinct eligible positions and preserves the protected position.
- Matching parent scores copy independently; two parental contributions use their arithmetic mean. This intentionally enables inherited experience and is a simulation design choice.
- Zero-baseline learning arithmetic remains finite; absent targets clear old target snapshots.
- Food increments saturate before byte conversion. In-bounds image pixels map safely to cells, including the origin; outside clicks return no cell. All-Eat organisms remain visible against empty black space.
- A 256-sample population graph records completed seasons without consuming randomness. Statistics count final occupancy after movement. Labels distinguish action-pattern frequency from fitness and inherited sequence age from individual age.
- UI painting/control updates occur on the UI thread. Pause stops new work; reset and close wait for the active worker. The board bitmap matches the scaled picture area; the pattern list fits above controls. One current offscreen preview was visually inspected. This is not an owner watch judgment or an interactive VS designer test.
- Test support adds reset/run options, explicit randomness, reference execution and a full reachable-state snapshot. Production retains its parallel rule path; initialization now has a single owner and production random access is synchronized. Reference mode additionally uses a single stable random stream, serial cell traversal, ordered effects and stable learned-score tie breaking. These interventions change stochastic trajectories and are not neutral measurements of the original scheduler.
- Removed explicit per-season garbage collection while retaining ordinary runtime collection. No ecological constants or action repertoire were retuned.

A transient helper contributed integration cases, snapshot and graph code, documentation and a bounded UI review. Seed Analyzer reviewed/integrated those changes and ran validation. This was collaborative implementation, not an independent scientific evaluation.

## Baseline, exposed failure and repair

The original-source build used the clean tracked baseline at local commit `1637a5dd7af2a469904cba9e3ce8789faa68467a`, after the previously owner-verified solution mapping repair. The initial six desired-behavior regressions all failed against the original mechanics/renderer: mutation seed0 replaced five positions, parent scores were not inherited, a zero baseline yielded infinity, the target snapshot stayed populated, the origin pixel threw, and a byte-max food increment wrapped 3 to 2. All six pass after repair.

The original four-season production smoke completed with populations **917, 890, 879, 873**. This unseeded run is a short characterization, not a matched ecological comparison with the later reference mode.

The first longer successor scenarios all failed with a null-target snapshot during aging. Clearing stale target snapshots exposed a stale previous-action evaluation on the ninth tick. A focused regression reproduced the exception before the follow-up fix. Organisms now evaluate learning only for a newly executed action, then clear that pending action/target. This changes stale credit assignment while retaining the ninth-tick aging schedule. The regression and all four complete scenarios now pass. Earlier failure summaries and this intervention remain in the current results file.

During viewer verification, an initially blank offscreen capture was corrected by creating/showing the offscreen form before capture; an added pixel assertion was corrected from client to full-window coordinates. These were capture/test-harness errors, not simulation defects. Static layout review and the visible preview also identified and corrected label/list overlap and bitmap scaling.

## Current reference horizon

All runs below used xorshift32-v1, 10% initial population probability, a 100x100 finite board, 128 seasons, runtime 9.0.17 and the final Release build identified in the JSON. Population at season1 is after one update, not the initial draw count.

| Seed | Population s1 | Population s128 | Action patterns s128 | Largest pattern | Board food | Sequence age min / mean / max | Extinction by s128 |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| 11 | 910 | 1461 | 760 | 2.19% | 99075 | 0 / 13.10 / 14 | none observed |
| 29 | 996 | 1408 | 809 | 1.85% | 99067 | 0 / 13.29 / 14 | none observed |
| 47 | 973 | 1465 | 829 | 2.46% | 99118 | 0 / 13.05 / 14 | none observed |
| 83 | 965 | 1540 | 808 | 3.44% | 99203 | 0 / 13.30 / 14 | none observed |

Each seed was repeated with population history and board rendering enabled: exact full-state hash, draw count and trajectory matched its unobserved run. Seed11 also matched between two fresh processes. Production smoke exercised empty, sparse and full occupancy for eight seasons, checking finite scores, food bounds, gene ownership and unique occupancy. These bounded checks do not prove all concurrency schedules safe.

The hash includes options, random state/draw count, board clocks, rule cursor, statistics and reachable organisms/genes/children/targets, private counters, learned scores and ordered pending effects. Unsupported types/non-finite values fail explicitly. This is reference-state comparison, not a save/load or production replay format. Population trajectories are current observations, not proof of improved biological realism, survival or fun.

## Remaining boundary and practical use

Open `Fistnet.Genepool.sln` in Visual Studio 2022 and use App as startup to watch the result. Use Tests as startup with Ctrl+F5 for the default suite; executable commands and mode limitations are in `Fistnet.Genepool.Tests/README.md`. The suite is a dependency-free executable, not a Test Explorer adapter. Routine execution writes console output only; optional preview explicitly writes one file. Current KB reports replace current support output; no TestResults history folders or backup trees were created. Temporary build caches were removed. Normal project bin/obj output remains.

Known broader questions remain: separate target/action selection, differing world/organism age clocks and the double reproduction counter. The latter two have named current-behavior tests rather than new model requirements. Long-horizon ecology, subjective watchability, arbitrary high-DPI/display configurations and exhaustive concurrent interactions remain unevaluated. The completed short rendering check uses the installed display/font environment and an offscreen window.

The next useful checkpoint is the owner's normal Visual Studio rebuild/run and a short look at whether the history and labels make population changes easier and more enjoyable to follow. No larger model change, unattended trial, role transition, commit or push has been performed. Source reversal should use a reviewed Git diff and only this iteration's changed/new files; the KB is not protected by Git.
