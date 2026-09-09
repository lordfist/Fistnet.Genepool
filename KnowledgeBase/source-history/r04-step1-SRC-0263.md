# Gene game automated checks

This project is a small executable test suite that calls the actual Dna, Control,
Visualization and App projects. It covers specified mechanics, integration,
selected rendering pixels and geometry, UI control boundaries, deterministic
reference scenarios and production smoke checks. A passing run establishes these
contracts; whether the simulation is more enjoyable to watch remains a human check.

## Visual Studio 2022

Open the entire `D:\Posao\Fistnet.Genepool\Fistnet.Genepool.sln` in Visual Studio
2022. All six projects, including the Godot viewer, remain in the solution. The projects target
`net9.0-windows7.0`; the App and Tests execute as x64 processes.

The solution's `global.json` requests the existing .NET SDK `9.0.315`, permits
`latestPatch` roll-forward and excludes prerelease SDKs. This keeps SDK selection
within the specified .NET 9 feature band. The established environment is Visual
Studio 2022 17.14; building requires an installed SDK compatible with the pin.

Build the solution in Debug or Release. Set **Fistnet.Genepool.Tests** as the startup
project and press **Ctrl+F5** to run the default checks. Set
**Fistnet.Genepool.App** back as startup when you want the WinForms simulation.

This suite uses its own named-case runner and failure exit codes. No Test Explorer
adapter or additional test-framework NuGet package is required. Run the executable
to execute the assertions; Test Explorer does not discover these custom cases.

## Running a built test executable

From PowerShell, use the configuration you have just built:

```powershell
Set-Location -LiteralPath 'D:\Posao\Fistnet.Genepool'

# Default checks, excluding the longer scenarios group.
& '.\Fistnet.Genepool.Tests\bin\Debug\net9.0-windows7.0\Fistnet.Genepool.Tests.exe'
$LASTEXITCODE

# All checks, including the reference comparison scenarios.
& '.\Fistnet.Genepool.Tests\bin\Release\net9.0-windows7.0\Fistnet.Genepool.Tests.exe' --all
$LASTEXITCODE

# Only the reference comparison scenarios.
& '.\Fistnet.Genepool.Tests\bin\Debug\net9.0-windows7.0\Fistnet.Genepool.Tests.exe' --group scenarios
$LASTEXITCODE
```

The default is the fast feedback selection, not a guaranteed duration. It includes
the integration group's bounded production smoke checks. Actual elapsed times are
reported for each case and the whole run. `--group NAME` selects the cases whose
group matches that exact name; `--group scenarios` runs the longer comparisons.

The `season-observation` group checks the whole-season action feed used by the
Godot habitat view: all actors beyond the older 64-marker limit, detached
published batches, acknowledgments and reset, actual movement/birth locations,
mutation counts, unknown no-choice deltas, newborn timing and observer passivity.
Godot camera, viewport, material pixels, action scope and minimap interaction are
checked in its own opt-in verification scene; see the Godot viewer README.

The `season-observation-cost` group measures a controlled 10,000-organism workload
with the observer off/on and frame capture timed separately. Its report includes
allocations, workload equality checks and explicit completion under a cooperative
budget. It is a cost measurement, with no machine-dependent timing pass threshold.

Normal suite runs print a PASS/FAIL line per case, followed by one JSON summary on
the final stdout line. That summary contains case results, errors, timings, runtime
version, hashes of the five loaded assemblies and any collected scenario results. Exit code **0** means all selected
checks passed, **1** means a check failed, and **2** means malformed selection or no cases matched.

To run one bounded scenario and receive its result as JSON on stdout:

```powershell
& '.\Fistnet.Genepool.Tests\bin\Release\net9.0-windows7.0\Fistnet.Genepool.Tests.exe' --scenario 11 128 reference
```

The last argument is `reference` or `production`. The CLI accepts **1 through 128
seasons**, with an initial population percentage of 10 for this direct command.
Failure is reported to stderr with exit code 1.

## What the scenario comparison means

The `scenarios` group uses the fixed seeds **11, 29, 47 and 83**, each for **128
seasons**. Each seed runs once without observation and once with population history
and board rendering. It compares the complete reference-state hash, random-draw
count and trajectory. A further case repeats seed 11 for 128 seasons in two fresh
processes. These repetitions are intentional checks of reset, process isolation and
observer independence.

Each scenario has a 60-second budget checked between seasons; a single in-progress
season is not interrupted by that check. Fresh child processes have a 60-second
wall-clock timeout and are terminated on expiry. A timeout is an incomplete,
failing check. These are per-scenario/child limits, not a 60-second total guarantee
for `--all`.

The default integration selection also exercises the production scheduler for
eight seasons at initial population percentages 0, 1 and 100. It checks completion,
unique occupancy, food bounds, finite learned scores, correct gene ownership and
independently counted population/action statistics.

**Reference and production scheduling are different modes.** Reference runs use
the specified random source, serial cell traversal and explicit effect order.
Production supports serial or bounded parallel cell refresh/setup; Part2 action
selection and shared transactions use explicit ordered phases, with seeded shuffled contention order.
Production is still not the reference-state export contract. Compare reference hashes only for compatible
reference versions, configuration, random algorithm and horizons.

The reference snapshot includes board clocks, rule position, statistics, random
state, options and reachable organisms, genes, learned scores, private counters,
detached completed outcomes, scalar parent IDs, generation/lifetime, policy and
the run-local identity counter. Live decision/target references are released after
the turn. It also includes any manually queued system effects. It rejects unsupported state/types and
non-finite values instead of silently omitting them. It is an execution-state
comparison, not a save/load file or a production snapshot facility.

Scenario JSON reports population and total board food per season, action-pattern
count and largest-pattern fraction, minimum/maximum/mean SequenceAge, first
extinction season when observed, elapsed time and random information. Action
patterns omit target directions and learned state. SequenceAge may be inherited
and is not an individual's lifetime. Higher population or diversity is not itself
a pass criterion.

The ninth-action-tick organism aging clock is retained. Part2 replaces the old
double birth count and deferred food/birth accounting with actual committed
transactions; tests assert those repaired outcomes. Historical Part1 results
retain the earlier characterization. Rendering/history independence is checked
against the newly built model. Part3 also checks all four accepted Part2 default
state hashes and random-draw counts, so two equally changed runs cannot hide a
regression. The exporter deliberately omits the newly added settings only when
they equal the prior defaults, and omits display-only choice text. Nondefault
food/regrowth/reserve settings and nondefault cell scheduling are represented.

## R02 Part 3 viewer and performance checks

The app uses one simulation worker and detached completed-season frames. The
`viewer-backend` group verifies observation passivity, immutable cell storage,
settings validation, following an organism through movement and death, bounded
traces/history, exact one/eight-season stepping, reset and worker ownership.
The rendering/UI groups cover the passive viewer, board picking/zoom, food layers,
representative scaling and input handling while a worker is busy. These checks
do not replace owner review of readability or claim ecological balance.

The `performance` group checks choice ordering and random consumption, invalid
candidates, and equivalent full execution state under serial/bounded cell phases.
Timing measurements are separate opt-in commands:

```powershell
& '.\Fistnet.Genepool.Tests\bin\Release\net9.0-windows7.0\Fistnet.Genepool.Tests.exe' --performance-micro
& '.\Fistnet.Genepool.Tests\bin\Release\net9.0-windows7.0\Fistnet.Genepool.Tests.exe' --performance-fixture 100 large serial 4
```

Micro measurements use fixed decision/completion and child-construction work,
one warmup and five retained repeats, with a 25-second checked batch budget.
The fixture command accepts density 0/10/50/100, `early`/`large` history,
`serial`/`parallel`, and one through four eight-season batches. Use a bounded
external process timeout when comparing versions. Fixture occupancy is held
constant; these are computation measurements, not population-survival trials.

## Runner checks and optional output

```powershell
& '.\Fistnet.Genepool.Tests\bin\Debug\net9.0-windows7.0\Fistnet.Genepool.Tests.exe' --runner-negative-control
$LASTEXITCODE  # Expected: 7
```

This deliberately fails an assertion, identifies the intentional failure on
stderr, and must exit **7**. The normal suite launches this as a child and checks
the outcome; a successful negative-control check is separate from simulation
correctness.

`--render-preview <output-path>` is an explicit request to write a preview image.
Ordinary runs write reports to the console and do not create result files,
historical log archives or backup trees. Retain an output only when it serves a
specific investigation. Build-generated `bin`/`obj` files are ordinary build
outputs rather than test history.

## Source identity and baseline

The initial local Git source baseline for this work is
`1637a5dd7af2a469904cba9e3ce8789faa68467a`. The test interfaces, reference mode,
mechanics repairs and viewer changes were introduced after that baseline.

The `--baseline-smoke` option runs four production seasons using whichever code
is currently built. Its name does not restore or measure the original baseline.
Historical baseline results must retain their original source attribution.

For a meaningful future before/after comparison, identify the **actual source set
used by each build**: current commit plus any uncommitted/new source files, project
and solution manifests, `global.json`, relevant build inputs and their exact file
identities. Rebuild before claiming that an executable represents edited sources;
record the SDK/runtime and current output identities when comparing retained
results. The runner hashes its five loaded assemblies, but does not automatically
collect source or compiler-input provenance.

`StateSha256` identifies exported simulation state. It is not a hash of the source
tree or executable and cannot establish which code was built. Keep production
smoke observations, historical baseline measurements and successor reference
comparisons clearly attributed to their respective versions.

## R02 opt-in diagnostics and bounded measurements

`--group diagnostics` runs the new fast observation checks. These compare complete
reference state and random draws with diagnostics off/on, exercise the actual
eight-season `ExecuteOneAge` path, verify exact food/movement/birth/death counters,
and check bounded/reset session behavior. The UI case measures existing offscreen
WinForms completion/refresh paths; it does not claim sustained interactive input
latency. These cases are included in the default and `--all` selections. The
original `--scenario` command and its 1..128 season range remain unchanged.

Longer diagnostic measurements use a separate explicitly invoked command:

```powershell
& '.\Fistnet.Genepool.Tests\bin\Release\net9.0-windows7.0\Fistnet.Genepool.Tests.exe' --diagnostic-scenario 11 2048 reference on 1 no-render
& '.\Fistnet.Genepool.Tests\bin\Release\net9.0-windows7.0\Fistnet.Genepool.Tests.exe' --diagnostic-scenario 29 512 production off 8 render 10
```

Arguments are seed, requested seasons (1..2048), mode (`reference`/`production`),
diagnostics (`on`/`off`), batch (`1`/`8`), rendering (`render`/`no-render`) and an
optional initial population percentage (0..100, default 10). Batch 8 calls the
actual `ExecuteOneAge`; a final remainder uses single seasons. Statistics are
therefore refreshed at each completed age, just as in that existing method.
Rendering refreshes one 800-pixel board image after each batch. This measures
image generation separately from engine calls; it does not create an interactive
window or measure its painting/input responsiveness.

Output is **JSON Lines**: one initial line, partial checkpoints at completed
seasons 128/512/2048 when applicable, and one final line. Only a final line with
`complete: true`, the requested completed horizon and exit 0 is a complete run.
Checkpoint hashes identify compatible reference states; production hashes remain
null. `firstExtinctionObservedAtBatchEnd` is deliberately a batch-end observation:
batch 8 does not establish which of its eight seasons first became empty.

Each process stops cooperatively before new work once elapsed time reaches 110
seconds. A 115-second process-local timer emits a minimal incomplete final line
and exits **3**, including when a batch/export is still in progress; only earlier
checkpoints then have verified state. The calling evaluator should still enforce
an external **120-second** limit because process scheduling/output failures can
prevent an in-process callback from running promptly. An exception emits an
incomplete final line and exits 1. Never treat the shorter observed horizon or a
missing final line as successful completion of the requested horizon.
Normal completion, exceptions and timeout compete for one terminal writer before
final export begins. A final export that stalls after reserving that writer relies
on the external 120-second backstop; timeout cannot issue a contradictory final.

Reports split engine-call wall time, diagnostic `engine.season` time, renderer,
validation, export preparation and total elapsed time. Enabled engine calls also
include diagnostic census/observer/collector work; the phase total excludes the
external observer and census but still includes instrumentation inside phases.
No warmup is discarded: initial/reset/JIT costs remain visible in total/early
measurements. Final JSON encoding and stdout cost are excluded from that line's
reported total; earlier output cost is included. Allocations and garbage
collections are **process-wide since before reset**, including setup, diagnostic
collection, validation and exports. No forced garbage collection is performed.
Timing keys are not all disjoint wall-time phases. For example, `dna.decision`
sums durations of calls that may overlap across production workers, and parent
history-copy time is nested within its caller. Do not add these values to phase
wall time or interpret their ratios as percentages of elapsed process time.

Batch timing retention is bounded to the latest 256 batches, plus exact
count/minimum/maximum/mean for early/middle/late thirds of the requested horizon.
Each third retains its latest 64 timings for sample median/p95, with omissions
reported. A partial run may not reach all thirds. Diagnostic final output retains
at most 128 sampled events and 256 season reports; intermediate checkpoints emit
aggregates instead of repeating these arrays. Event categories reserve capacity
for deaths, effects/vitality, actions/choices and other events; samples are not
frequency estimates. Death labels record observed same-season contributors and
can be mixed or unknown; they do not assign exclusive causal credit.
Each sampled death retains its first and last observed same-season contributor,
known action origin when available, scalar before/after states, and the number
of omitted intermediate contributors, even if ordinary effect samples are full.
Manual/unscoped effects retain unknown actor provenance. Census
totals are sums across observations, including sums of per-season maxima; use the
checkpoint maximum or individual season reports when a maximum is required.

For repeatable *synthetic workload* measurements at fixed occupancy:

```powershell
& '.\Fistnet.Genepool.Tests\bin\Release\net9.0-windows7.0\Fistnet.Genepool.Tests.exe' --diagnostic-fixture 50 large on 4
```

Fixture arguments are occupancy (0/10/50/100 percent), history (`early`/`large`),
diagnostics (`on`/`off`), and optional age batches (1..64, default 4). These fixtures
use test-only no-op genes and reserve 100, preserving population over the bounded
run while exercising the actual production scheduler and ordinary aging. Every
organism starts with two active learned contexts; `large` adds **32 external
contexts**, each with eight scores (34 contexts/272 entries total versus 2/16).
This controlled history load is not a claim of naturally evolved late-run state,
and its no-op action is not the production Eat action despite using that enum tag.
It does not exercise the cost of real births, mutation, attacks or movement.
Compare off/on in separate fresh processes with identical fixture arguments,
repeat in reversed order when timing noise matters, and inspect reported actual
occupancy/history size rather than inferring it from a requested percentage.

Diagnostics/session state is detached after every completed command or test. No
measurement command writes files, creates archives, retunes model parameters,
changes rendering cadence in the application, or turns diagnostics on globally.
Retention of console output remains the evaluator's explicit decision.

## R02 Part 2 checks and policies

`--group action-transactions`, `--group dna-actions` and `--group learning`
select the added checks. They cover each gene's own target, exact affordability,
source-cell food conservation, shared-turn completion, birth placement/counts,
newborn timing, movement expiry, mutation/infection, finite arithmetic, learning
invalidation, independent inheritance and optional LRU/exploration behavior.
The runner rejects missing, extra, repeated and conflicting flags before running
any test. An empty command still selects the ordinary default suite.

All actions are selected once before any resolve. The retained actor cohort then
resolves in a seeded shuffled order; births commit during that phase, deaths are
removed before movement, and reward follows the completed shared turn. This is
a deliberate behavior change, not proof of better ecosystem balance. Eligibility
filtering also means unavailable gather/heal genes wait for context changes or
mutation instead of running their old failed-target retarget branches.

The application still uses the repaired control defaults: legacy selection,
legacy overweight threshold and damage-only attacks. The Part 3 New simulation
dialog now exposes the optional policies without changing those defaults.
Tests also exercise them through `SimulationRunOptions.Policy`; a code caller
can explicitly select candidates at reset:

```csharp
Board.Reset(new SimulationRunOptions
{
    Mode = SimulationMode.DeterministicReference,
    Seed = 11,
    Policy = new SimulationPolicy
    {
        Learning = LearningPolicy.BoundedExploratory, // unseen first, then 10% exploration; 64 external contexts
        Health = HealthPolicy.Capped,                 // maximum 50
        Attack = AttackPolicy.ReserveTransfer         // cost 1; direct lethal hit transfers up to 5 actual reserves
    }
});
```

These parameters are candidates for subsequent comparison, not calibrated
success criteria. Predation transfers actual victim reserves once; it adds no
fixed species, kin immunity, population quota or guaranteed coexistence.
The exact reward formula and changed comparability are recorded in
`KnowledgeBase/R02_PART2_CONTRACT.md`. The Part2 result files are separate from
the unchanged Part1 baseline. No ecological pass/fail claim follows from tests.

## R02 Part 4: optional founders and ecological measurement

`--group founder-setup` checks the optional starting repertoire and settings;
`--group ecology` checks ecological accounting and threshold boundaries. The
complete suite includes these checks without launching long ecological trials.

`SimulationRunOptions.FounderRepertoire` defaults to
`FounderRepertoire.UnrestrictedRandom`. The optional `GatherAndReproduce` value
reserves distinct random slots for `GenerateFood` and `CombineDna`, with six
remaining random genes and the existing per-type cap. It guarantees available
gene types, not successful gathering, healing, reproduction or survival. Gene
directions remain random. Offspring still inherit and mutate normally; the
constraint applies only to initial founders. The new-world dialog exposes this
choice, a separate **Food regrowth 2** preset, and the existing capped-health
comparison. None is automatically selected as a new default.

The explicit scenario command is:

```powershell
& .\Fistnet.Genepool.Tests\bin\Release\net9.0-windows7.0\Fistnet.Genepool.Tests.exe --ecology-scenario 11 2048 production default on
```

Arguments after the command are a signed integer seed, 1..2048 seasons,
`production|reference`, `default|constrained|regrowth2|capped`, and collector
`on|off`. The fixed candidates change one family only: founder repertoire,
regrowth 1 to 2, or the existing capped-health rule at 50. Other defaults remain
unchanged. Observer-off reference runs check collection passivity; they cannot
provide an ecological verdict. Every explicit run has a completed-season soft
time limit and a 115-second process watchdog; the governed wrapper applies the
outer 120-second cap and batch limits.

The collector observes every completed season through existing passive hooks.
It distinguishes living population relative to its actual initial count from
empty-cell occupancy, cumulative deaths and peak drawdown. Its exact pattern
key contains all eight ordered action-type/target pairs, excluding random
legacy codes, learning scores and ancestry. A dominance streak must belong to
the same key throughout; movement can change targets, so keys are recomputed.
This grouping is finer than the viewer's action-type-only pattern list.

The owner-accepted screen requires population at or above 10% of its start,
no exact pattern above 90% for 128 consecutive seasons, and continued
reproduction with varied actions through 2048 seasons. The prospective activity
definition checks each complete 128-season window after the first 128 warmup
seasons for at least one placed birth and two distinct action types with actual
committed effects. Blocked zero-effect moves do not count. Partial windows and
zero-population initialization do not establish success. There is no predator
quota or claim that shared ancestry means monoculture.

Initial/checkpoint/final JSON records report configuration, seed, source-loaded
assembly identities, RNG draws, actual completed horizon, population/turnover,
generations, resources, action effects and timings. Threshold accounting is
per-season; the final display trajectory is explicitly sampled every eight
seasons. Action resource totals are the reported action outcomes, separate from
other system/aging costs and the actual board/reserve census. Time-limited runs
remain inconclusive at the requested horizon even if an earlier threshold
violation is already known. Full reference state is captured only for a final
quiescent reference result.

See `KnowledgeBase/R02_PART4_CONTRACT.md` for the frozen comparison seeds,
budgets, candidate-selection and reserved-confirmation rules. These metrics
evaluate that bounded screen; they do not prove biological realism, inherited
adaptation, long-term balance for every seed, or owner acceptance of R02.

The existing Python runtime can run `KnowledgeBase/tools/run_part4_checks.py`
with `--stage <unique-name> --configuration Release` and exactly one selector:
`--suite`, `--reference`, or `--comparison <candidate>`. The wrapper validates
arguments before launching a process, retains one current aggregate report,
and rejects reuse of an attempted stage name. `--confirmation <candidate>` is
only for an explicitly recorded selection after all four comparison seeds pass;
the wrapper never selects a candidate or runs reserved seeds automatically.
An interrupted run retains earlier observed failures and the exact season of
its last flushed criteria summary, separately from incomplete horizon coverage.
`KnowledgeBase/tools/summarize_part4.py` reads that report and prints an
independent arithmetic, coverage and executable-identity audit, per-seed results
and candidate eligibility. It launches nothing and writes no files. Eligibility
requires complete evidence, not just a passing label in a partial report.
