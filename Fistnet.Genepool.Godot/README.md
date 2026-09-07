# Genepool Godot viewer

This application uses the existing Control/Dna simulation in a Godot window. The original Windows Forms application is still a startup choice in the same solution. Starting both applications creates independent simulations.

## Visual Studio 2022

1. Close the portable Godot editor before regenerating its settings. From the repository root, run `& .\Fistnet.Genepool.Godot\tools\Setup.ps1` in PowerShell. Run Setup again after moving the checkout; it regenerates local absolute paths and selects Visual Studio as Godot's C# editor.
2. Open the root `Fistnet.Genepool.sln` in Visual Studio 2022. Keep the existing .NET SDK 9.0.315 pin and use **Debug**.
3. Set **Fistnet.Genepool.Godot** as the startup project and select the **Genepool (Godot)** executable launch profile. Build, then press **F5**.
4. For the original viewer, select **Fistnet.Genepool.App** as the startup project.

Setup enables native debugging as required by the [Godot C# Visual Studio instructions](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/c_sharp_basics.html#visual-studio-windows-only). The generated `Properties/launchSettings.json` is local configuration. Godot's external-editor setting uses its built-in Visual Studio integration; no custom editor path or global IDE preference is changed.

## Launch from PowerShell

Build **Debug** in Visual Studio first, and rebuild after C# changes. These commands reuse the generated Visual Studio profile and do not build:

```powershell
# From the repository root:
& .\Fistnet.Genepool.Godot\tools\Launch.ps1          # run the viewer
& .\Fistnet.Genepool.Godot\tools\Launch.ps1 -Run     # same action
& .\Fistnet.Genepool.Godot\tools\Launch.ps1 -Editor  # edit scenes and UI
& .\Fistnet.Genepool.Godot\tools\Launch.ps1 -WhatIf  # preview without launching
```

The helper requires the Debug assembly to exist. Godot development launches load that assembly; building the solution in Release maps the Godot project to ExportRelease and does not change what these development commands run. A compiled Godot C# library is not a `dotnet run` application. Standalone exported packaging and its matching export templates are deferred.

Both launch paths explicitly select the Compatibility renderer. The profile confines the process's application-data and temporary directories to `.tools/godot-process`, uses `.tools/nuget/packages`, and sets no machine-wide environment variables or PATH entries. The portable editor's `._sc_` marker keeps its settings in `.tools/godot/4.7.2/editor_data`. Close Godot before running Setup so an open editor cannot overwrite its updated preferences on exit.

## Editor dependency and attribution

Use **Godot .NET 4.7.2 stable, Windows x64**, with its complete `GodotSharp` companion directory. The verified distribution is installed at `.tools/godot/4.7.2`; its main executable is `Godot_v4.7.2-stable_mono_win64.exe`. Use that executable for debugging. The console companion is a separate file.

- [Official version archive](https://godotengine.org/download/archive/4.7.2-stable/)
- [Official .NET Windows x64 ZIP](https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_win64.zip)
- [Publisher release metadata](https://github.com/godotengine/godot-builds/releases/expanded_assets/4.7.2-stable) and [SHA-512 checksums](https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/SHA512-SUMS.txt)

The downloaded ZIP's verified SHA-256 is `a2a48473a7414c5f19fab690518caebb738c09ef9601f6bd2388676a7f53b3c0`. To prepare another checkout, verify that ZIP, extract its contents into `.tools/godot/4.7.2` with the executable directly in that directory, retain all companion files, add an empty `._sc_` file beside the executable, and run Setup. Setup performs no download or installation. If editor settings have not yet been generated, launch the editor once, close it, and rerun Setup.

This viewer uses **Godot Engine**, developed by Juan Linietsky, Ariel Manzur and Godot contributors, under the [MIT license](https://godotengine.org/license/). Godot also includes third-party components with their own notices; retain the [versioned copyright and license information](https://github.com/godotengine/godot/blob/4.7.2-stable/COPYRIGHT.txt) when preparing a distributable package. See Godot's [attribution guidance](https://docs.godotengine.org/en/4.7/about/complying_with_licenses.html). This first view uses built-in geometry and UI controls and requires no external artwork pack.

## Controls and observation

Run/Pause, one-season Step, an eight-season Cycle, Restart, New simulation and target speeds use the existing simulation worker. Pause is acknowledged immediately and settles after the current calculation finishes. Restart repeats the active seed and settings. New simulation opens the existing starting options and comparison presets; random initialization remains the default.

Use the wheel to zoom, right/middle drag to pan, and Fit to return to the whole board. Combined view leaves local food visible around each organism; Organisms and Food isolate the layers. Click an organism for its DNA and completed actions, or an empty cell for food. Patterns group ordered action types and can be highlighted; they are not species or fitness rankings. Activity and the optional history explain what was observed and what was sampled. History starts hidden in shorter windows and can be enabled with its checkbox.

The board uses two reusable GPU instance batches and detached completed snapshots. Camera and controls keep updating while the worker calculates; this rendering change does not speed up simulation calculations or change the ecosystem rules.

## Repeatable verification

After building the root solution, run the existing console test suite in either configuration:

```powershell
& .\Fistnet.Genepool.Tests\bin\Debug\net9.0-windows7.0\Fistnet.Genepool.Tests.exe --all
& .\Fistnet.Genepool.Tests\bin\Release\net9.0-windows7.0\Fistnet.Genepool.Tests.exe --all
```

The opt-in `Scenes/Verification.tscn` exercises real Godot controls, transformed selection, pixels, settings, simulation passivity and shutdown. `KnowledgeBase/tools/run_godot_checks.py` runs it with an external deadline and current-result files. With an existing Python 3.11+ runtime, use `--mode headless` for logic, `--mode smoke` for the actual GPU window, or `--mode benchmark` for the 45-second renderer workload. Run graphics measurements while the desktop is otherwise idle. The runner deliberately replaces its current reports and previews; it does not create historical test archives. See the implementation review for the verified local Python path.

## Verified implementation status

Whole-solution **Visual Studio 2022 MSBuild Debug and Release builds passed**, preserving SDK9.0.315 and the net9 target. The existing suite plus new board/settings checks passed **149/149 in each configuration**. The 106 CA1416 Windows platform warnings remain in the existing application/visualization code; the new Godot project compiled without warnings.

Actual Compatibility rendering on the **AMD Radeon RX 6800** passed pixel, layout, controls, populated-world selection/actions and shutdown checks. At 1920×1080, independent 0/1,000/10,000-organism fixtures achieved about **60 FPS** and **16.7 ms p95 main-loop frame intervals**, with all requested 10 Hz snapshot updates delivered. Each used five seconds of warmup and ten seconds of sampling. These are bounded local measurements, not physical presentation timings or a general hardware guarantee.

Godot's native host selected the already-installed **.NET10.0.9** through its own roll-forward policy; the project still targets **net9** and builds with **SDK9.0.315**. Actual **VS2022 F5, breakpoint binding, stepping and inspecting locals remain for owner review**. Command launches and VS2022 builds have passed; they do not establish IDE debugger interaction. The profile follows the official Godot workflow. Do not force the host down to .NET8, which cannot load the net9 application.

Results and the review are in `KnowledgeBase/IMPROVEMENT_RESULT_R03_2D.md`. R03 depth/3D, graphics polish and standalone export remain separate checkpoints. Owner acceptance of this viewer is pending.
