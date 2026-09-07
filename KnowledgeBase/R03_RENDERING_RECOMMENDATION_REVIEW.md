# R03 Step 1 — recommendation review: Godot C#
Genepool Analyzer · 2026-09-07 · Provisional recommendation; awaiting owner review.

**Current recommendation: choose Godot C# as the rendering foundation, starting with a restrained 2D or 2.5D presentation and retaining a path to actual 3D.** This revises the earlier SkiaSharp preference in REC-0008. It does not record an owner engine selection or start Step 2.

The owner asked whether SkiaSharp's easier initial integration could justify replacing it if the viewer later moves to 3D. This is a request for technical judgment, not implementation authorization. Actual 3D was already a possible destination in the original R03 outline; the question exposes an underweighted cost in the initial recommendation.

I overweighted the easiest replacement of the existing WinForms board. That is a real local advantage, but it is not evidence that SkiaSharp minimizes the total work across R03 and a subsequent 3D upgrade. No prototype or effort measurement established that saving. Given the requested examples/assets and future upgrade path, my engineering judgment now favors Godot.

SkiaSharp would retain the current layout and provide a familiar drawing surface. Godot requires more initial application integration: a C# project and launch/export workflow, connection to the simulation's completed frames and commands, and a decision about how the new viewer relates to the existing WinForms application. Embedding Godot into a WinForms control has not been established as a simple supported route. VS2022 compatibility does not itself require that particular embedding arrangement.

Godot already supplies both 2D and 3D rendering facilities and scene/asset workflows. It is useful for modest visuals as well as elaborate games; using it does not require adding physics, complex game rules or advanced visual effects. Keep DNA and simulation ownership in the existing C# components. [Godot 2D facilities](https://docs.godotengine.org/en/stable/tutorials/2d/introduction_to_2d.html), [Godot 3D facilities](https://docs.godotengine.org/en/stable/tutorials/3d/introduction_to_3d.html).

A later Godot 2D-to-3D upgrade still involves work. Sprite-based presentation, cameras, coordinate mapping and picking must be adapted or replaced; 3D needs appropriate models/materials and perhaps lights. With a deliberate separation, the simulation adapter, organism identities, commands, state-to-appearance rules and some UI can be reused. The engine, development workflow and asset conventions remain familiar. This is a design expectation, not an already implemented reuse guarantee. It avoids replacing a Skia-specific rendering stack as well.

SkiaSharp remains sensible if flat/isometric sprites are expected to be the lasting destination, or if we deliberately want a short-lived prototype. I do not recommend selecting it merely to defer learning Godot when actual 3D is a meaningful future option.

Godot's official C# workflow supports Visual Studio 2022, and the earlier research documents a .NET 9 targeting path. Preserve the existing solution and WinForms app during transition. The exact chosen Godot version, project references, launch/export setup and whole-solution build still need a bounded integration check during authorized implementation. The Godot editor/runtime adds tooling; Visual Studio remains the C# IDE. [Official C# workflow](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html), [Godot .NET targets](https://godotengine.org/article/godotsharp-packages-net8/).

The original research's verified examples, model packs, license findings and hardware limitations still apply. Kenney assets and C# Godot scene examples provide actual starting material, but no pack has been imported or example executed here. No speedup, memory result or quantified engineering saving is claimed.

Next: owner review of this revised engine recommendation. If accepted and Step 2 authorized, settle the smallest integration path and its VS2022/GPU/state-passivity checks. Do not expand this into detailed planning or implementation now. R02 remains closed with ecology inconclusive.

The [original comparison](D:/Posao/Fistnet.Genepool/KnowledgeBase/R03_RENDERING_RESEARCH_STEP1.md) retains its initial reasoning and now points here. A dated REC-0008 correction preserves the prior recommendation. A transient independent reviewer challenged the lifecycle tradeoff; Genepool Analyzer owns this revised judgment.

