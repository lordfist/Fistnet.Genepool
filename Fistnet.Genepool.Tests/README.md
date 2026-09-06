# Gene game automated checks

This project is a small executable test suite that calls the actual Dna, Control,
Visualization and App projects. It covers specified mechanics, integration,
selected rendering pixels and geometry, UI control boundaries, deterministic
reference scenarios and production smoke checks. A passing run establishes these
contracts; whether the simulation is more enjoyable to watch remains a human check.

## Visual Studio 2022

Open the entire `D:\Posao\Fistnet.Genepool\Fistnet.Genepool.sln` in Visual Studio
2022. All five projects remain in the solution. The projects target
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

Normal suite runs print a PASS/FAIL line per case, followed by one JSON summary on
the final stdout line. That summary contains case results, errors, timings, runtime
version, hashes of the five loaded assemblies and any collected scenario results. Exit code **0** means all selected
checks passed, **1** means a check failed, and **2** means no cases matched.

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
Production runs exercise the parallel update path and do not claim identical
trajectories from an identical seed. Compare reference hashes only for compatible
reference versions, configuration, random algorithm and horizons.

The reference snapshot includes board clocks, rule position, statistics, random
state, options and reachable organisms, genes, learned scores, private counters,
old targets, children and pending effects. It rejects unsupported state/types and
non-finite values instead of silently omitting them. It is an execution-state
comparison, not a save/load file or a production snapshot facility.

Scenario JSON reports population and total board food per season, action-pattern
count and largest-pattern fraction, minimum/maximum/mean SequenceAge, first
extinction season when observed, elapsed time and random information. Action
patterns omit target directions and learned state. SequenceAge may be inherited
and is not an individual's lifetime. Higher population or diversity is not itself
a pass criterion.

Two tests explicitly characterize the retained ninth-action-tick age increment
and the double reproduction-counter increment after a birth. Their names identify
them as current behavior; these broader semantics were not redesigned in this
iteration. Rendering/history independence is tested separately from those choices.

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
