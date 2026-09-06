> R01 completion update, 2026-09-06, Seed Analyzer: The bounded first improvement is complete. The implementation, reusable tests, VS2022 builds and later reference cleanup are verified; the owner has supplied positive visual feedback and reports continued operation (CLAIM-0003). Local commit `2bb2b99d2f77774a3d2a0614c8dc7747349c5685` contains the reviewed solution and KB revision 17. All 61 source/build-input files match the last verified cleanup build. FACT-0029 and INF-0004 record this completion review. Remaining broader simulation questions concern future work and do not block R01 completion. This is a Seed completion assessment, not a new owner decision or a measured long-term enjoyment/ecological result.
>
> The text below preserves the earlier proposal/evaluation horizon. Its pending-viewing and unversioned-KB statements are superseded: the owner chose to keep the KB in Git (DEC-0008). This closure update creates new KB-only changes after that commit. No next iteration or role transition is started.

> Implementation update, 2026-09-06: User accepted this bounded iteration (DEC-0007), and implementation/evaluation are now recorded in [IMPROVEMENT_RESULT_R01.md](IMPROVEMENT_RESULT_R01.md). The text below retains its original proposal horizon. Its future-only status, unverified-runtime wording, backup baseline and unique TestResults-directory requirements are superseded by later owner instructions and verified environment evidence. Existing SDK9.0.315 and VS2022 build tools were used; Git holds prior committed source versions, while the KB remains unversioned. Keep one current result and disposable caches, not run archives. Fixed scenario values are held in ScenarioRunner.cs instead of a duplicate scenarios.json; RegressionTests.cs keeps the reproduced before/after cases separate. Additional BoardSquare and UI lifecycle/scaling changes address concrete food overflow, statistics/movement and viewer defects found during verification. No package installation was required.

# Gene game: first improvement and automated evaluation proposal — R01

Author: Seed Analyzer  
Status: proposal for owner review; implementation and execution have not been authorized or performed.  
Date: 2026-09-06  
Experiment root: `D:\Posao\Fistnet.Genepool`  
Working knowledge: `D:\Posao\Fistnet.Genepool\KnowledgeBase`

## Objective and evidence

The owner's goal is to make the experiment a better simulation and more fun to watch. The current assignment adds an automated suite that can evaluate the present implementation and future changes, including inexpensive visual checks.

This proposal uses KB revision 3 and its 41 registered sources. Deep validation rechecked all source/prepared identities this turn without errors or warnings. The content horizon remains 2026-09-06 01:04:55 UTC; branch metadata still names master and commit 4e3d72d4e00d6ae91ed45983b480c3171bb2fca4, without asserting that the working tree equals that commit. No simulation, build, test, package restore, outside-root research, or image evaluation has been run.

Current source evidence supports a four-project artificial-life sandbox with inherited actions and learned action scores. A successful first iteration should make selected mechanics behave as specified and make population changes easier to follow. It need not maximize population, eliminate extinction, or emulate real biology.

## Recommended first iteration

1. **Establish an honest baseline and a reusable test suite.** Capture current behavior before repairing it. Tests must call the real implementation rather than reimplementing the simulation.
2. **Correct a bounded set of mechanics.** Enforce the declared mutation limit, make proposed parent-history transfer work, clear stale learning-target state, and make zero-baseline score changes finite. Each correction gets an explicit test and its own before/after result.
3. **Improve observation modestly.** Add a bounded population-history graph, clarify misleading statistics, and make board-edge selection safe. Keep the existing board, controls and action repertoire.

This is preferable to tuning constants without measurements or replacing the engine/UI at the outset. The test project is justified by the owner's requested ongoing evaluation; the graph is justified by the current viewer's lack of population history. No new Actor, service, database or unattended automation is proposed.

## Proposed behavior contracts

These are Seed design choices offered for acceptance, not claims about the owner's unstated preferences.

- **Mutation:** replace at most two distinct eligible positions per mutation request; exclude the supplied protected position and preserve all unselected elements. A random replacement may retain the same action type, so tests count selected/replaced positions as well as actual content differences. Do not force visible novelty.
- **Parent history:** copy independent score entries for genes that match the child's position, DNA code, action type and target. If both parents contribute to the same target/action entry, use their arithmetic mean; if one contributes, copy it. Do not share dictionaries or mutate the parents. This deliberately enables inherited experience; it is a simulation choice, not biological validation.
- **Score arithmetic:** retain the existing ratio for a nonzero previous value. For a zero previous value, use denominator 1; zero to zero remains zero. Clear the previous target snapshot whenever the selected target is absent. Tests must reject NaN/infinite scores rather than hiding them by changing the pass criteria.
- **Observation:** sample population after a completed season; retain the latest 256 samples and label the horizontal axis in seasons. Sampling and rendering must not consume simulation randomness or change simulation state. The graph supports recognizing growth, decline and cycles; it does not grade them as good or bad.
- **Labels:** “Top rated DNA” becomes “Most common action patterns.” Retain the existing SequenceAge calculation only with a label and explanation that it may be inherited and is not an individual lifetime.
- **Picking cells:** an in-bounds pixel maps to its containing grid cell; (0,0) selects cell (0,0), the last in-bounds pixel selects the last cell, and out-of-bounds clicks are ignored. The current ceiling-minus-one code produces -1 at coordinate zero (SRC-0028, lines 99–106).

The separate action/target-selection calls, differing age clocks, reproduction counter increments and other broader ecological choices remain characterized review candidates. Do not silently fold their redesign into this iteration. If they prevent meaningful tests or cause a new blocker, report the specific failure and adjust the proposed scope before continuing.

## Test architecture

Add one `Fistnet.Genepool.Tests` project, initially a small dependency-free .NET test executable referencing the actual Dna, Control and Visualization projects. It provides named cases, assertions, nonzero failure exit status, bounded execution, and JSON/text reports. This keeps the first suite usable without adding a test-framework package dependency. The .NET runtime/SDK itself is still an unverified build prerequisite.

Use separate named groups for mechanics, integration, rendering and scenarios. Run cases that touch static Board/RuleManager state serially; isolate scenarios in fresh processes when complete reset cannot be established. Expected answers come from the contracts and deliberately constructed fixtures, not from the implementation's own summary helpers.

Add only the small source interfaces needed for explicit fixtures, controllable random draws, complete resets, read-only inspection and a headless step entry point. Avoid broad replacement of static classes or a new engine architecture.

**Two execution modes must remain explicit:**

- Production-mode smoke checks exercise the existing parallel update path and assert structural properties and bounded completion. Their exact trajectories are not assumed reproducible.
- A deterministic reference mode uses a supplied random stream, stable board/rule/effect ordering and clean per-run state. Every random call and local generator must be covered; a seed alone is insufficient. Its behavior is not assumed equivalent to the production scheduler.

Mode, source identities, runtime, seed, step count and relevant configuration belong in each result. Reference-mode results may be compared only against compatible reference-mode results. Do not change the UI's update schedule just to make a test pass.

## Initial automated suite

The table defines case families; boundary and parameter cases expand them. It does not claim tests already exist or have passed.

| Group | Checks and independent expected result |
| --- | --- |
| World boundaries | Interior, edges and corners resolve the correct neighbors; finite edges do not wrap. Empty and occupied cells behave distinctly. |
| Food and lifecycle | Food depletion floors at zero, replenishment respects the cap, and dead occupants are removed by the removal pass. Do not assume total food conservation: sources, sinks and deaths are part of the rules. |
| Movement and births | A successful move preserves the organism, clears the origin and does not overwrite an occupied destination. Birth placement respects occupancy; one organism reference must not occupy two cells after a completed season. |
| Mutation regression | Scripted draws include duplicates and the protected index; at most two eligible distinct positions are selected. Unselected DNA retains its identity. |
| Learning transfer regression | Zero, one and two contributing parents; matching/nonmatching genes; duplicate keys; independent parent/child dictionaries; specified averaging. |
| Learning arithmetic regression | Zero-to-zero, zero-to-positive and nonzero baselines yield the specified finite values. Occupied-target then empty-target sequences do not retain stale target state. |
| Learning characterization | Record which action and target are chosen, score updates and current age/reproduction transitions. Unaccepted redesigns stay identified as questions rather than becoming arbitrary failing requirements. |
| Statistics | A hand-built board yields an independently counted population and action-pattern histogram. SequenceAge and individual Age are deliberately different in one fixture to catch mislabeled output. |
| Reset and isolation | Reset removes all prior population, pending state, statistics and run-specific random state covered by the reset contract. Repeat in one process and fresh processes. If reset is incomplete, use process isolation and expose the defect. |
| Repeatability | A fixed reference scenario repeated with the same version/configuration yields matching exported state and random-draw count. Include DNA/targets, learned scores, pending effects and counters in the comparison; explicitly report any state not covered. |
| Observer independence | The same reference scenario with rendering/history on and off has matching simulation state and random-draw count. A chart may not perturb evolution. |
| Integration smoke | Empty, sparse, crowded and scripted-interaction scenarios complete their step budget without exceptions, duplicate occupancy or non-finite learned scores. Production mode receives these checks too. |
| Rendering | Known cells appear in the correct positions; empty cells use the expected background; dimensions/scaling are correct; legal occupied patterns remain distinguishable from empty space. Use selected pixels and geometry, not whole-window pixel equality. |
| Cell selection | Check (0,0), both sides of cell boundaries, all corners, the final valid pixel, negative coordinates and coordinates equal to the image width/height. |
| Graph and controls | A known population sequence yields the correct samples, order and capacity. Pause adds no simulation steps; reset clears run history. Test control behavior at its callable boundary, without brittle screen clicking. |

Predicted failures from source inspection must be labeled **unconfirmed until executed**. Once reproduced, retain their original results. A known failure remains a failure with an issue reference, not a green test or a silently skipped test. Desired-behavior regressions and current-behavior characterization must be distinct.

Include at least one negative-control check of the runner/reporting path: an intentionally failing assertion must produce a nonzero exit and identify its case. Keep that self-check separate from the simulation's correctness score.

## Baseline and future comparisons

The first execution stage, if authorized, attempts a build and short run against preserved current source bytes. The missing declared Newtonsoft.Json path is a preflight issue, not a demonstrated build failure. Do not substitute old binaries or search outside the root. If build-enablement edits are necessary, preserve the failed attempt and label the resulting baseline as build-enabled.

After additive test interfaces are introduced, label that version separately. Do not present its deterministic runs as measurements of untouched original code.

Use three practical run sizes:

- **Fast:** unit/fixture cases and small rendering checks, with an initial target of roughly 10 seconds.
- **Change evaluation:** four fixed seeds (11, 29, 47, 83), 128 seasons each, plus scripted edge cases; a provisional 60-second total execution budget. A timeout is an incomplete result, not permission to silently shorten a scenario.
- **Extended:** additional owner-approved seeds and horizons only when a particular change warrants them. No endless stochastic search.

These are proposed cost limits, not measured runtimes. The first authorized timing result may justify revising them before they become the standard comparison.

Report population over time, first extinction step if any, action-pattern counts/concentration, model-age distribution, total available board food, elapsed time, and exceptions. Call action-pattern diversity exactly that; it omits target directions and learned state. Compare identical horizons and configuration, show per-seed outcomes, and disclose incomplete runs. Do not label more survivors or more diversity “better” without an accepted objective.

For ordinary iterations, return one compact summary with changed outcomes, failing cases and links to detailed files. Full trajectories stay in artifacts. Use a small fixed visual scenario only when rendering/layout changes or a visual test fails: at most one contact sheet with sparse, dense and selected-cell views, plus one short human watch check. Routine nonvisual changes need no image review. Automated pixels can establish rendering behavior; the owner's judgment establishes whether watching it is more enjoyable.

## Proposed filesystem and execution effects for a later implementation assignment

All paths below are under `D:\Posao\Fistnet.Genepool`. These are planned effects, not files created by this proposal.

- Add `Fistnet.Genepool.Tests\Fistnet.Genepool.Tests.csproj`, `Program.cs`, `TestSupport.cs`, `MechanicsTests.cs`, `IntegrationTests.cs`, `RenderingTests.cs`, `ScenarioRunner.cs`, `scenarios.json`, and `README.md`.
- Add minimal support files: `Fistnet.Genepool.Dna\IRandomSource.cs`, `Fistnet.Genepool.Control\SimulationRunOptions.cs`, `Fistnet.Genepool.Control\SimulationSnapshot.cs`, `Fistnet.Genepool.Visualization\PopulationHistory.cs`, and `Fistnet.Genepool.Visualization\PopulationHistoryView.cs`.
- Expected edits: `Fistnet.Genepool.sln`; Dna `Common.cs`, `DnaElementFactory.cs`, `Organism.cs`, `Elements\Brain\StrategyNetwork.cs`; Control `Gameboard\Board.cs`, `RuleManager.cs`; Visualization `GameboardBitmap.cs`; App `MainForm.cs` and `MainForm.Designer.cs`. Existing project manifests may need only narrowly explained build/reference changes.
- Prospective run outputs: new, unique subdirectories under `TestResults`, containing the case/configuration manifest, source identities, machine-readable results, compact report, trajectories and optional images. Never overwrite a different existing run.
- Future build/test execution may create ordinary project `bin`/`obj` output and SDK cache files. Use project-local caches where feasible; no package download, SDK installation, outside-root dependency access or wider filesystem effects are silently included.

Before implementation, freeze the exact edit set and runtime effects from this proposal and the build preflight. If another file is required, identify its purpose and scope instead of silently expanding the change. Preserve a local baseline of exactly the files to be edited and separate infrastructure, mechanics and viewer changes. Never include private settings/history caches in that baseline merely because they exist.

## Acceptance, failure and reversal

Accept a first iteration only when the selected contracts pass, relevant pre-existing passing cases still pass, the known-failure list is explicit, runtime limits are reported honestly, and the viewer's data agrees with the simulation. Any proposed-defect fix must demonstrate a before-failure and after-pass where the baseline can execute. If the baseline cannot execute, disclose that limitation rather than manufacture a before-result.

The viewing checkpoint is a short owner review: can the population's changes be followed more easily, and is it more enjoyable to watch? An automated pass alone cannot answer that.

Stop the affected stage for a build blocker, unexplained state change, invalid comparison, newly exposed defect that defeats evaluation, or an unexpected write collision. Leave independent successful results intact. Reversal restores only the changed files from their frozen baseline or reverts the isolated change after checking for concurrent edits; never reset the whole working tree or erase the first attempt/results.

The next applicable instruction should authorize the selected implementation stage and its exact source/build/test effects. This proposal does not start implementation, run trials, install anything, or transition Seed Analyzer into another role.

