# R03 — Godot 2D viewer implementation review

Genepool Analyzer · 2026-09-07 · **IMPLEMENTED, VERIFIED WITH STATED LIMITS, OWNER REVIEW PENDING**

The approved Godot setup and first 2D viewer are complete. The same root Visual Studio 2022 solution now contains `Fistnet.Genepool.Godot`, alongside the existing WinForms application. Godot renders food and organisms using two reusable GPU instance batches. Simulation rules, founder defaults, action resolution, learning and randomness remain in the existing Control/Dna code.

## What is available

- A 100×100 board with the existing food scale and DNA colors; Combined, Organisms and Food views.
- Fit, cursor-centered wheel zoom, right/middle drag pan and stable organism selection.
- Run, Pause, one-season Step, eight-season Cycle, Restart, New simulation and the existing target-speed choices.
- Organism parentage, generation, ages, resources, eight DNA slots and completed actions; actual effects remain distinct from whole-season changes. Last observed details remain after a selected organism dies.
- Explained pattern groups with highlighting/dimming, activity counts, food legend, trails, short-lived markers and sampled history. Shorter windows initially hide history to preserve board space.
- The existing seven settings presets and all visible starting fields. Hidden execution/policy values are preserved. Random is the default; signed explicit seeds are supported. Cancel does not affect the world; Create requests a new paused run.
- One existing SimulationRunner owns the world. The UI reads detached latest snapshots and submits commands. No accumulating frame queue, per-organism scene node, new physics system or WinForms embedding was introduced.

Pause requests are shown immediately; the worker settles them at a completed season boundary. GPU drawing does not accelerate simulation calculations. This implementation changes observation and presentation, not ecological behavior. R02 remains closed with ecological evaluation inconclusive.

## Local setup and Visual Studio 2022

Godot **.NET 4.7.2 Windows x64** is already installed locally in `.tools/godot/4.7.2`, with the complete GodotSharp distribution and portable editor data. The downloaded ZIP was checked against the publisher SHA-256 and SHA-512; all 82 extracted distribution files were compared with their ZIP entries. SHA-256: `a2a48473a7414c5f19fab690518caebb738c09ef9601f6bd2388676a7f53b3c0`.

The matching Godot SDK/bindings were restored through the explicit public NuGet feed into `.tools/nuget/packages`; Debug and ExportRelease dependency locks are included. Tools, caches, imports and machine-specific launch settings are ignored by Git. Export templates, standalone packaging, artwork packs and any new .NET SDK were unnecessary for this milestone and were not installed.

Open `D:\Posao\Fistnet.Genepool\Fistnet.Genepool.sln` in **Visual Studio 2022**, choose **Debug**, set **Fistnet.Genepool.Godot** as the startup project, select **Genepool (Godot)**, and build/start. The local executable profile has already been generated. If the checkout moves, run `Fistnet.Genepool.Godot/tools/Setup.ps1` again. Keep the portable Godot editor closed while running Setup.

The root SDK remains **9.0.315** and projects remain **net9.0-windows7.0**. Solution Debug maps to Godot Debug; Release maps to ExportRelease. All six existing platform/configuration combinations map to valid Godot configurations. Direct ExportRelease compilation was also checked with Control and Dna using Release, rather than creating unintended ExportRelease builds of those libraries.

The Godot host selected the already-installed **.NET10.0.9** through its distribution's runtime roll-forward policy. That is separate from the net9 target and SDK9 build. **Actual VS2022 F5, C# breakpoint binding, stepping, local inspection and Stop Debugging have not been exercised directly.** The profile follows Godot's documented VS2022 executable/native-debugging workflow; whole-solution VS2022 builds and command launches passed. No demonstrated debugger incompatibility was found, so no runtime workaround was introduced. [Godot C# Visual Studio setup](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/c_sharp_basics.html), [Microsoft SDK/Visual Studio compatibility](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs).

For a command launch after building Debug, run `Fistnet.Genepool.Godot/tools/Launch.ps1`; use `-Editor` for the scene editor. Select **Fistnet.Genepool.App** to use WinForms. Starting both applications creates independent worlds. Development Godot launches use the Debug assembly; a Release build does not make this launch an exported Release application.

## Verification

| Check | Result |
|---|---|
| Entire solution, installed VS2022 MSBuild | Debug and Release passed, 0 errors; existing 106 CA1416 Windows platform warnings in each. New Godot project compiled without warnings. |
| Existing regressions plus new presentation checks | **149/149 Debug**, **149/149 Release**. New checks cover palette, instance data, camera/picking boundaries, trails, settings preservation, presets and seeds. |
| Godot imports and headless integration | Passed; headless evidence is logic/import evidence, not GPU rendering evidence. |
| Actual GPU window | **AMD Radeon RX 6800**, native **OpenGL 3.3 Compatibility**, driver context `26.8.1.260806`; no software fallback. |
| GPU verification scene | Passed actual pixels for food/organism layers and dimming, transformed input selection, settings, playback/reset, observation passivity, responsive controls during a controlled worker block and clean shutdown. |
| Populated live integration | Existing random founder setup at 10% density, reference seed29, eight completed seasons; selected organism DNA/actions and retained death details reached the UI. This was a bounded engineering check, not an ecological evaluation. |
| Preview/layout inspection | Current fixed-fixture captures at 1920×1080 and 1366×768, settings window, and an actual live-world capture inspected. No board/sidebar overlap; controls remained accessible. |
| Normal application startup | Default Main scene launched with the local profile environment, then exited after a bounded 180-frame check with exit0 and no engine errors. |

The existing deterministic regression expectations were not rewritten. The Debug test assemblies still match the tested hashes after the final rebuild; the Release suite ran against the final Release build. Final build input identities and both configurations' loaded test assembly identities are checked in `r03_step3_evidence.json`.

Renderer-only fixtures replay fresh snapshots at 10 Hz, with five seconds of warmup and ten seconds of sampling per density at **1920×1080**. Every case delivered all 100 scheduled sample snapshots; none was skipped. The timing target was p95 at or below 33.3 ms.

| Organisms | Average main-loop FPS | p95 frame interval | Maximum interval | p95 CPU preparation/upload call |
|---:|---:|---:|---:|---:|
| 0 | 60.00 | 16.68 ms | 16.72 ms | 0.86 ms |
| 1,000 | 60.00 | 16.68 ms | 17.44 ms | 1.16 ms |
| 10,000 | 60.00 | 16.69 ms | 18.13 ms | 1.62 ms |

The actual GPU smoke measured a **0.24 ms Pause handler** while the simulation worker was held at a controlled calculation boundary; the camera continued updating. This is injected Godot input/signal handler time, not physical mouse-to-photon latency. Command settlement is reported separately by the UI. The renderer figures are main-loop intervals and CPU upload-call timing, not GPU timestamps or physical presentation measurements. They describe these bounded fixtures on this machine, not a universal guarantee.

The benchmark predates only the addition of the final populated-world verification method; its renderer/UI implementation and benchmark method were unchanged. No repeat was needed to substantiate the same renderer workload.

## Repeat the checks

The new project README has the normal startup and test instructions. Existing full suites run through `Fistnet.Genepool.Tests.exe --all` in the desired configuration.

The current host's existing Python runtime is `C:\Users\lordf\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`. Use it with `-X utf8 -B` and:

- `KnowledgeBase/tools/build_solution.py --stage <name> --configuration Debug|Release --report r03_step3_builds.json`
- `KnowledgeBase/tools/run_r03_regressions.py --configuration Debug|Release`
- `KnowledgeBase/tools/run_godot_checks.py --mode headless|smoke|benchmark`

The graphics runner enforces a 90-second smoke or 100-second benchmark process deadline, invalidates its prior current report before dispatch and records failed/incomplete runs. The scene's output allowlist excludes canonical KB files. Current reports/previews replace their earlier routine values; no backup tree or routine test archive was added. Engine process logs remain in the ignored `.tools/godot-process` directory.

## Interventions and practical limits

Initial package/SDK resolution failed under the restricted process permissions; running the authorized build with normal Windows access resolved it. NuGet's SDK resolver consults the user's standard configuration location despite temporary APPDATA redirection. Project restore uses the explicit public feed and contained package cache; no private configuration was inspected or changed by the analyzer.

The first full compile found a name collision between Godot and .NET random-number classes, corrected with an explicit cryptographic type name. Settings verification initially read programmatic SpinBox changes before Godot redrew their text; the harness now waits for that redraw. The separate real input issue—parsing an unfinished minus while entering a signed seed—was corrected by committing completed input at the dialog boundary. [Godot 4.7.2 SpinBox implementation](https://raw.githubusercontent.com/godotengine/godot/4.7.2-stable/scene/gui/spin_box.cpp).

One direct .NET CLI first-use invocation reported creating an ASP.NET development certificate. No trust command was run. Subsequent project process settings explicitly suppress that unrelated first-use certificate generation as well as PATH changes and telemetry. The development viewer itself needs no HTTPS certificate.

No source mechanics, accepted ecological criteria, reserved seeds, permanent roles, installed governance or remote Git state were changed. No commit or push was made. Direct VS2022 debugger interaction, physical monitor/DPI transitions and exported Release runtime packaging remain unverified. Owner visual acceptance is pending.

## KB and review boundary

The owner approval is DEC-0025. Result knowledge is recorded through a reviewed transaction from KB revision46; `r03_step3_receipt.json` and `r03_step3_validation.json` are the authoritative commit/validation results. Exact predecessors of the three edited registered sources were preserved through verified pinned Git history or the single affected Tests Program snapshot. The existing SRC-0115 historical mixed-newline gap remains separately disclosed; this implementation creates no new history gap.

Review the viewer and its VS2022 startup/debugging workflow. This completes R03's approved first 2D milestone and stops for owner review. Depth/2.5D/3D, improved artwork and standalone packaging have not begun.
