# R04 — Visual Studio 2026 and a full 3D Genepool viewer

Genepool Analyzer · Created 8 September 2026; Step 1 revised and accepted 9 September 2026 · **Step 1 implemented and verified; awaiting owner review. Later steps remain unstarted.**

## Decision and scope

The owner explicitly accepts and closes R03 Step 5. This completes the five planned R03 milestones. The accepted appearance includes full DNA RGB, borderless organisms throughout World/View 1, grey ground in the Organisms layer, food terrain, and the corrected perspective board. Earlier verification remains dated evidence in FACT-0061/0062; acceptance is a new owner decision, not a new test result.

The owner requests a detailed plan for the next round in this order: (1) Visual Studio 2026 and consideration of .NET 10; (2) full 3D design and organism model proposal; (3) implementation; (4) shaders, lighting and colours; (5) WASD and mouse navigation. R04 is the next sequential iteration label used for this plan.

**Recommendation:** migrate the complete solution to Visual Studio 2026 and .NET 10, keep Godot 4.7.2 during migration, then add a distinct Full 3D view with shallow, irregular mould colonies. Retain the accepted views for comparison and everyday use. Each step ends with a result for owner review; moving onward requires the next applicable instruction.

This is a visual and development-tool iteration. The discrete board, DNA, resource rules, season scheduling and simulation ownership remain the model underneath the new view. Three-dimensional appearance does not require three-dimensional movement, stacked organisms or a physics simulation. The deferred food growth/hold/decay change remains outside this round (DEC-0027).

## Inspected baseline and evidence limits

Read-only metadata checks on 8 September 2026 found:

| Component | Observed current state | Planning consequence |
| --- | --- | --- |
| Visual Studio 2026 Community | 18.9.2; installed at `C:\Program Files\Microsoft Visual Studio\18\Community`; managed desktop workload and MSBuild present | No new IDE installation currently appears necessary |
| .NET | SDKs 10.0.400, 10.0.301 and 9.0.317 present; .NET and Windows Desktop 10.0.11 runtimes present | The proposed SDK/runtime are already available; restore or workload gaps still need actual build verification |
| Existing solution | Six projects target `net9.0-windows7.0`; root SDK policy requests 9.0.315 with latest-patch roll-forward | Installing VS2026 has not itself retargeted the solution |
| Build helper | Pins the old VS2022 path and SDK directory 9.0.315; that SDK directory is now absent | Step 1 must update tool selection before using this helper; this is a metadata finding, not a failed build in this task |
| Godot | Installed executable reports 4.7.2 stable .NET; project uses Godot.NET.Sdk/4.7.2 and GL Compatibility | Start from the engine already integrated and accepted |
| Current angled view | Real Camera3D/SubViewport, but food and organism batches use flat PlaneMesh surfaces; organisms are just above the board | Extend the existing renderer with actual thickness, rather than replace the simulation or add another engine |

Coverage: project targets and solution mappings, SDK policy, relevant build/test helpers, installed tool metadata, and the current board renderer, materials, projection, minimap and view switching. No private IDE settings, unrelated directories, experiment trials or new compilation/runtime results were used. Public research used Microsoft, Godot and original asset publishers. Documentation supports a migration attempt; local designer, debugger, runtime and performance outcomes remain untested.

**9 September review update:** read-only installed-directory inspection now also finds SDK 10.0.401 and .NET/Windows Desktop runtime 10.0.12. The table above retains the earlier observation, not a fixed version requirement. The saved accepted Godot report `r03_step4_verification.json`, dated 8 September, already records runtime **.NET 10.0.9**, although all six projects still target .NET 9. The installed Godot `GodotPlugins.runtimeconfig.json` uses `LatestMajor`; the Godot host selects its runtime separately from the project's target framework. This is dated runtime evidence, not a new execution result. The focused source review found no confirmed .NET 10 source incompatibility, but did not build or run the proposed target. The owner requested incorporating the review changes and stopping for proposal review; this does not authorize Step 1 implementation.

Microsoft supports targeting .NET 10 in Visual Studio 18.0+ and pairs SDK 10.0.4xx with VS18.9. .NET 9 support ends 10 November 2026; .NET 10 LTS support ends 14 November 2028. This makes the runtime upgrade useful maintenance rather than a cosmetic version change. [SDK/IDE matrix](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs), [support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

Godot's exact 4.7.2 managed API targets .NET 8, and the published package lists computed .NET 10 compatibility. That establishes framework eligibility, not successful startup of this particular application. Keep engine and SDK versions matched and prove local integration in Step 1. [4.7.2 API project](https://raw.githubusercontent.com/godotengine/godot/4.7.2-stable/modules/mono/glue/GodotSharp/GodotSharp/GodotSharp.csproj), [GodotSharp 4.7.2](https://www.nuget.org/packages/GodotSharp/4.7.2), [Godot framework explanation](https://godotengine.org/article/godotsharp-packages-net8/)

## Step 1 — Migrate the development environment and runtime

**Result:** one complete solution that opens, builds, debugs and starts the supported applications from Visual Studio 2026, with .NET 10 as the recommended target.

1. Recheck the current checkout and tool versions, preserve any owner edits, and establish the existing .NET 9-targeted solution as a comparison under VS2026. Resolve the available servicing SDK explicitly; do not reinstall the missing 9.0.315 just to satisfy a stale helper constant. Record the compiler/build SDK, target framework and actual running runtime separately for the console tests, WinForms app and Godot viewer. Retain and inspect the Godot report's existing runtime field. Do not label the Godot comparison as a .NET 9-runtime-to-.NET 10-runtime change unless the measured runtimes establish that distinction.
2. Make the build/test helpers select VS2026 through installed-tool discovery and genuinely follow the root `global.json` and NuGet policy. The current helper creates its own SDK/configuration files and forces resolver paths, so updating only the root files or old constants is insufficient. Remove contradictory temporary SDK pins and resolver overrides, or derive necessary isolated settings from the declared policy. Record the effective SDK, MSBuild and package sources actually used. Preserve isolated caches, bounded runs and automatic temporary-file cleanup.
3. Retarget all six projects coherently to .NET 10 with their existing Windows platform intent; update `global.json` to the installed 10.0.4xx feature band with a deliberate servicing roll-forward policy, resolving and recording the installed servicing version at execution time. Review the existing Windows API platform suffix separately: it is not a claim of supported Windows 7 deployment. The owner's 9 September implementation instruction additionally requires a VS2026 `.slnx`: generate and verify it with all six projects and their configuration mappings, use it as the primary solution, and retain the existing `.sln` as a compatibility entry point. This explicit instruction supersedes the earlier keep-`.sln`-only wording.
4. Regenerate affected package lockfiles deliberately, review the resolved dependency changes, and then use locked restore for final verification. Preserve the configuration-specific lockfiles: Godot Debug includes `GodotSharpEditor`, while ExportRelease does not. Preserve the Godot ExportDebug/ExportRelease-to-Debug/Release project-reference mappings. Retain Godot 4.7.2 and the current rendering backend during this step.
5. Update affected launch/output paths and test runners, preserving WinForms startup. Verify freshly produced migrated outputs and identify the executable or loaded assemblies actually checked; an existing file is insufficient. The console wrapper's fixed `net9.0-windows7.0` path can run leftover output, while Godot's `.godot/mono/temp/bin/Debug` path does not distinguish target frameworks. Remove only obsolete generated outputs as needed, preserving source and owner changes, and do not create permanent duplicate build trees.
6. Align the project instructions and development notes with the owner's new VS2026 target. Once retargeted to .NET 10, VS2022 is no longer the officially supported development baseline. Older instructions requiring VS2022 must not silently override the new instruction.
7. Build and verify the scope in the matrix below. Check debugger integration and the WinForms designer through an available supported interface; if native IDE inspection remains unavailable, explicitly leave those manual checks for the owner's normal review. Do not label a build as proof of designer interaction.

| Verification | Required Step 1 evidence |
| --- | --- |
| Whole solution | Fresh Debug and Release builds; solution Release maps the Godot project to ExportRelease |
| Simulation/control | Meaningful console regressions in Debug and Release against the identified migrated executables |
| Reference semantics | Preserve all four accepted reference-state hashes and random-draw counts as a distinct check; investigate any mismatch before changing expectations |
| Retained 2D viewer | Godot `headless` and `smoke` checks against the freshly built Debug assembly |
| Habitat and accepted Step 5 RGB/readability | Explicit Godot `step4-headless` and `step4` checks; the ordinary modes do not cover this suite |
| Bounded performance comparison | Hold workload and measurement conditions comparable; record the actual runtime on each side and distinguish an intended runtime change from other differences. Use `benchmark` for the earlier 2D workload and `step4-benchmark` for Habitat where comparable |
| Startup and development workflow | Supported WinForms and Godot startup/interactions, plus verified or explicitly pending IDE debugger/designer checks |

Godot development launches and the current verification helper load **Debug**, even after a solution Release build. Report ExportRelease compilation separately from Debug runtime checks. Do not count a Debug launch as Release execution. Standalone exported packaging and its matching export templates remain deferred; no exported Release runtime result is claimed without a separately supported export path and applicable authorization.

Reference mode uses the project's own deterministic generator. If a reference hash changes, distinguish changed simulation state or random draws from JSON/reflection/compiler representation changes before any expectation update. Preserve the original mismatch and the explanation; do not silently refresh the reference values to make the migration pass.

**Review evidence:** effective toolchain and package sources, reviewed lockfiles and locked-restore result, changed files, target/runtime and executable/assembly identities, both build-configuration results, explicitly selected nonvisual/visual suites, reference hash/draw comparisons, a bounded before/after performance comparison where comparable, and supported launch/debug status. Existing counts such as 217 GPU checks are a dated baseline, not a fixed future quota. State compilation, runtime coverage and pending owner-only checks separately.

**Failure handling:** investigate integration regressions within this step. If the .NET 10-targeted Godot build cannot run correctly after a bounded repair attempt, present the precise blocker and the working VS2026/.NET 9-targeted baseline, including its actual runtime. Consider a minimal engine update only if evidence requires it. Do not combine an engine/rendering overhaul with this migration. Stop for review before Step 2.

## Step 2 — Full 3D design and organism model review

**Result:** an approved visual/model specification before implementation. Deliver a small set of images showing a single organism from above and from the side, a mixed-colour group at cell scale, and the proposed board at overview and close range. Label generated concepts as illustrative; they are not proof of engine output.

Propose these three silhouettes, with the first recommended:

| Model | Appearance | Tradeoff |
| --- | --- | --- |
| **Raised mould mat** | Uneven spreading footprint, shallow connected lobes, folded surface and short fringes; suggested height 0.1–0.2 cell widths | Closest to the approved mould direction; visible volume without obscuring neighbours |
| Spore tuft | Low mould base with several short upright folds or stalks | Stronger depth cue, more clutter and geometry |
| Amoeboid body | A thicker, continuous lobed body | Readable at distance, but may return toward the rounded appearance the owner disliked |

The recommended model uses a small reusable mesh template with an irregular perimeter and several surface rings. A stable visual seed changes the lobes; it does not consume simulation randomness. Shape and height start as decoration, not an undocumented health/species/fitness encoding. If later used to convey a variable, that mapping needs an explicit legend and design choice.

Use the existing full DNA RGB as material base colour. Neutral illumination may change the visible brightness, so retain a flat RGB swatch in the inspector/minimap as a stable reference. Keep black and white DNA colours valid. No return to a small pastel palette.

Define all four detail levels before coding: quiet overview, simple solid colonies at habitat scale, stronger surface detail and muted visible-cell actions at organism scale, and the richest detail plus full focused-organism actions at inspection scale. Preserve the accepted absence of organism outlines in World view. Approximate starting mesh budgets are 12–40 triangles for distant solid proxies, 80–200 at medium range, and 300–600 close up; these are engineering starting points subject to measurement, not accepted performance claims.

The board remains a flat simulation surface with visible 3D organisms and food/grass. Use genuine perspective, visible side surfaces and occlusion. Define how actions sit above organisms without hiding neighbours; markers behind the camera or terrain must not be presented as visible current events. Historical targets retain their existing meaning.

**Assets and examples:** Godot provides C# procedural mesh examples and documented instancing. Official lighting/material demos are useful references, although much of their code is GDScript and needs adaptation. Kenney's Nature Kit offers CC0 3D vegetation for optional food/ground decoration. No ready-made mould model matching this design has been verified. The small native template avoids relying on an unavailable model or requiring Blender on the owner's machine. If an external asset is selected, retain its exact source and licence; use a portable glTF/GLB export when appropriate. [C# ArrayMesh examples](https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/arraymesh.html), [official demos](https://github.com/godotengine/godot-demo-projects), [Nature Kit](https://kenney.nl/assets/nature-kit)

**Review:** owner chooses the silhouette, thickness, food scale and acceptable visual density. The camera interaction contract is designed here so picking/projection can support Step 5, but keyboard navigation is implemented in Step 5. Stop after design review material; do not import a concept image as though it were a finished model.

## Step 3 — Implement Full 3D with organisms

**Result:** a usable Full 3D option with actual volume, plain adequate lighting, selection and the existing simulation controls. Keep Original 2D and the accepted Habitat views selectable; changing views must not restart or modify the run.

1. Add a named view mode and a small scene/resource structure for the board, shared organism mesh tiers, materials, camera and overlays. Replace fragile view-index assumptions where the new mode makes them ambiguous.
2. Render detached completed snapshots. Use shared mesh/material resources and instanced batches, with colour and stable shape data per organism. Avoid one scene tree, animation controller, skeleton or physics body per organism.
3. Introduce spatial batches and manual detail tiers where measurements justify them. A MultiMesh is culled as one spatial object; it does not automatically cull every organism independently. A provisional 16-by-16-cell chunk is a starting experiment for draw-count versus visibility cost, not a fixed requirement. Bound allocations and update only needed rendering data. [MultiMesh contract](https://docs.godotengine.org/en/stable/classes/class_multimesh.html)
4. Base projection on the actual camera rather than the fixed-angle equations used by the present view. Use the camera frustum to clip the visible board footprint and keep the minimap correct even when the horizon is visible. Include organism height in visibility bounds.
5. Make selection hit the visible raised organism correctly, then fall back to the ground cell. Use nearby instance bounds to find candidates, followed by an accurate silhouette/shape or mesh test so empty space around an irregular edge does not select the wrong organism. Avoid thousands of physics nodes. Keep selection/action anchors aligned through resizing and view switches.
6. Preserve food layers, grey Organisms-only ground, selection, DNA details and the existing L3/L4 action rules. Provide fit and review camera presets plus necessary existing mouse navigation; defer the new WASD controller to Step 5.

**Verification:** empty, sparse and populated boards; selection near tall edges and through empty space around irregular silhouettes; board-edge clipping; minimap footprint; pause/restart; selected organisms disappearing; retained view behaviour; and unchanged completed simulation snapshots/RNG under view switches. Actual GPU captures must show thickness from an oblique or side view. A screenshot alone does not prove selection or performance. Stop with a working view and a concise review package before visual polish.

## Step 4 — Shaders, lighting and colours

**Result:** readable, coherent 3D visuals whose cost remains bounded.

Start with one directional light, neutral ambient fill, rough matte surfaces and restrained shadows near the camera. Develop mould surface folds/filaments and subtle material variation without whitening every DNA colour. Give food natural brown-to-green terrain and restrained grass detail, without obscuring small organisms.

Add at most a small, purposeful set of visual cues: gentle material motion where helpful, and brief action effects driven by actual observed outcomes. Shader motion is decoration; do not fabricate movement or reproduction between missing sampled seasons. Avoid transparent solid bodies, per-organism lights and unlimited particles.

The current Compatibility renderer supports core 3D; keep it if it meets this design. Compare Forward+ only if a needed effect or measured result justifies the change. A switch affects the whole Godot project, so retest the retained 2D/2.5D views as well as Full 3D. Update the verification runner to exercise the selected backend rather than silently force its currently hardcoded `gl_compatibility`. Optional quality settings should expose meaningful choices such as shadows and surface detail. [Godot renderer guidance](https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html)

**Performance proposal:** target 60 FPS at 1920×1080 on this machine, with a proposed steady-state p95 frame time no greater than 20 ms for the agreed reference views. Record hardware/backend/quality, use a fixed completed snapshot, warm up for 3 seconds and measure for 7 seconds. Compare empty, 1,000 and 10,000-organism fixtures at overview and close range; use one additional matched run only for a borderline/noisy result. Report simulation calculation time separately and check that viewing adds no material simulation throughput regression or queue growth. These are proposed engineering review targets, not a guarantee about every board or resolution. Measure peak allocations/memory alongside timing when a new batching strategy is introduced.

If the target is missed, reduce distant detail, shadow coverage or unnecessary effects before reducing displayed population or changing simulation work. Retain concise current results and clean temporary runs rather than building an archive of benchmark histories.

**Review:** show the same scene at overview, middle and close scale, with food and Organisms-only layers, representative extreme RGB colours, normal/default quality and the measured cost. Stop for owner review.

## Step 5 — WASD and mouse camera movement

**Result:** comfortable navigation across the board, with input that behaves correctly around the UI.

| Control | Proposed Full 3D behaviour |
| --- | --- |
| W / S | Move forward/backward across the ground relative to camera heading |
| A / D | Strafe left/right across the ground |
| Shift | Optional faster movement while held |
| Right mouse drag | Orbit around the ground focus, preserving a useful board-facing view |
| Middle mouse drag | Pan across the board |
| Mouse wheel | Move closer/farther with bounded distance |
| Left click | Select the visible organism or cell |
| F | Focus the selected organism |
| Home / Fit | Return to a clear whole-board view |
| Escape / focus loss | End camera capture immediately |

WASD moves the ground focus together with the camera; right drag changes the viewing angle around that focus. Normalize diagonal movement, use elapsed time, and scale travel speed sensibly with viewing distance. Limit pitch/distance so the camera cannot dive below the board; allow exploration near the edge without a false board border. Keep existing navigation behaviour in the retained views, with accurate Full 3D hints.

Handle mouse movement only in the viewport during the requested gesture. Ignore navigation keys while typing in settings, using a dialog or interacting with another focused control. Release held keys/capture on focus loss, menu/dialog opening, view change and window close. Prevent a delayed frame from causing a large camera jump. Keep panning, zooming, picking, minimap and action projection tied to the same camera state.

**Verification:** movement distance independent of frame rate; diagonal speed; movement bounds and reset; mouse release; typing without camera drift; minimap/picking correctness across camera poses; resize/focus transitions; and unchanged simulation state. Finish with a short controls reference and a final R04 result for acceptance. Passing tests does not itself close the iteration.

## Delivery boundaries and next instruction

No builds, application runs, downloads, installations, source edits, new models or graphics implementation were performed while preparing this proposal. Only read-only investigation and authorized derived KB maintenance were carried out.

Each implementation step is scoped to be independently reviewable. Inspect the current Git state before editing; preserve unrelated owner work and use normal source-version lineage for changed registered files. Git reversibility is useful, but no automatic commits, destructive resets or redundant backup trees are part of this plan.

The owner accepted the revised **R04 Step 1** plan and authorized implementation on 9 September 2026, additionally requiring a VS2026 `.slnx` and confirmation that the app is buildable (DEC-0042). Step 1 is implemented and verified, with results in [R04_STEP1_MIGRATION_RESULT.md](D:/Posao/Fistnet.Genepool/KnowledgeBase/R04_STEP1_MIGRATION_RESULT.md); it awaits owner review (FACT-0065). Model selection remains reserved for Step 2, which has not been authorized to start; dense 3D performance remains open until its later checkpoint. The implementation result records actual build and runtime verification separately from this plan and its dated baseline observations.
