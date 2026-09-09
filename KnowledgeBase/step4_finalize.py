"""Draft Step4 review and one KB transaction; never applies canonical changes."""
import copy
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parent.parent
KB = ROOT / 'KnowledgeBase'
os.environ['PATH'] = r'C:\Program Files\Git\cmd;' + os.environ['PATH']
def read(name): return json.loads((KB/name).read_text(encoding='utf-8-sig'))
def save(name, value): (KB/name).write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
def identity(path):
    data = path.read_bytes()
    return {'path':str(path), 'sha256':hashlib.sha256(data).hexdigest().upper(), 'bytes':len(data)}
core = read('core.json')
assert core['kb_revision'] == 54
records = {r['id']:r for r in map(json.loads,(KB/'records.jsonl').read_text(encoding='utf-8').splitlines())}
sources = read('sources.json')['sources']
before = read('r03_step4_predecessors.json')
now = dt.datetime.now(dt.timezone.utc).isoformat()
debug, release = read('r03_step4_tests_debug.json'), read('r03_step4_tests_release.json')
gpu, headless, old2d = read('r03_step4_verification.json'), read('r03_step4_headless.json'), read('r03_godot_verification.json')
benchmark, builds = read('r03_step4_benchmark.json'), read('r03_step4_builds.json')
assert all(x['ok'] for x in [gpu, headless, old2d, benchmark])
assert debug['failed'] == release['failed'] == 0 and debug['total'] == release['total'] == 159
assert builds['stages']['step4-batched-debug']['ok'] and builds['stages']['step4-final-release']['ok']
for configuration, report in [('Debug',debug),('Release',release)]:
    for name, expected in report['assemblySha256'].items():
        path = ROOT/name/'bin'/configuration/'net9.0-windows7.0'/(name+'.dll')
        assert identity(path)['sha256'] == expected, (configuration,name)
builds['actor'] = 'Genepool Analyzer'
save('r03_step4_builds.json',builds)

cost = release['observationCost']
metrics = benchmark['measurements']
table = '\n'.join(f"| {m['population']:,} | {'Organism (L3)' if m['detailLevel']==3 else 'World (L1)'} | {m['visibleOrganismInstances']:,} | {m['visibleActionCues']} | {m['averageFps']:.1f} | {m['p95FrameMilliseconds']:.2f} ms |" for m in metrics)
cost_lines = '\n'.join(f"- Pair {p['repeat']}: observer-on versus completely observer-off mean season difference {p['meanSeasonOverheadMilliseconds']:.2f} ms ({p['meanSeasonOverheadPercent']:.1f}%); separate capture {p['meanCaptureMilliseconds']:.2f} ms; additional season allocations {p['meanAdditionalSeasonAllocatedBytes']/1024/1024:.2f} MiB; capture allocations {p['meanCaptureAllocatedBytes']/1024/1024:.2f} MiB." for p in cost['pairs'])
report = f'''# R03 Step 4 — Living habitat view

Genepool Analyzer · {now} · **Implemented and verified; awaiting owner review.**

The owner explicitly started Step 4: “Good! You can start now with step 4. I am off to sleep. I will see what you did in the morning :D”. This implements the accepted design direction (DEC-0031), including navigation and both action-detail scopes. Step 5 polish, standalone export and the deferred food-growth cycle remain later work. No ecological trial or iteration closure is claimed.

## What changed

- A genuine Godot 3D viewport with top-down and gently angled orthographic cameras over a shallow flat board. The earlier Godot 2D renderer remains selectable on the same running world; the Windows Forms app remains a separate startup project in the unchanged solution.
- Brown dirt through olive to green represents the existing food quantity, with ground grain and small grass fans at closer scales. Fuzzy, irregular mould colonies replace small squares in the new view. Decorative colony shape follows identity through movement. Colors improve contrast and group patterns, with a finite palette rather than a unique DNA/species claim. Materials are procedural GPU code; no new artwork package, engine installation or dependency was needed.
- Smooth mouse zoom plus four named presets: World, Habitat, Organism and Inspect. Right/middle dragging, a clickable minimap, Fit and Zoom to selection support navigation. Only visible cells are submitted at close scales; the crop has no artificial world rim. Only true world boundaries get an edge.
- Level 3 displays muted actual outcomes throughout all relevant visible cells. Level 4 displays full outcomes only for the explicitly focused organism. The entire latest completed season's observed cohort is detached into the frame, replacing the batch each season; there is no 64-actor or nearest-neighbor cap in this new feed. Existing 2D markers remain unchanged.
- Cues distinguish movement, failed attempts, gathering, reserve consumption, healing, damage, confirmed DNA changes and actual child placement. Birth coordinates and identity come from the placement callback, separately from the mate/self target. Paused outcomes remain inspectable; running cues expire without being replayed by same-season acknowledgments. A deceased selection remains a historical location, without selecting its replacement. Target effects describe the cell at action time, since affected organisms may move later in the season.
- The inspector explains detail levels, biological units and actual effects. It labels unavailable before-state resource changes rather than asserting measured zero deltas. The existing selected history stays bounded to sixteen observations.

Simulation rules, scheduling, policies, random draws and the deferred grow/grow/hold/decay request were not changed. Observation itself has a measured cost described below. The new view does not claim to accelerate simulation calculations.

## Actual rendered previews

These are screenshots of the implemented viewer on detached **synthetic review fixtures**, not generated mockups or ecological results. The control buttons are disabled because these preview fixtures own no simulation. Normal startup has the live controls.

![Habitat](r03_step4_habitat.png)

![Organism](r03_step4_organism.png)

![Inspect](r03_step4_inspect.png)

## Verification

| Check | Result |
| --- | --- |
| Whole-solution Visual Studio 2022 Debug rebuild | PASS, 0 errors; 106 existing CA1416 warnings |
| Whole-solution Visual Studio 2022 Release rebuild | PASS, 0 errors; 106 existing CA1416 warnings |
| Console suite, Debug | {debug['passed']}/{debug['total']} PASS; {debug['seconds']:.2f} s |
| Console suite, Release | {release['passed']}/{release['total']} PASS; {release['seconds']:.2f} s |
| New depth GPU checks | {len(gpu['checks'])} PASS, no engine errors |
| New depth headless checks | {len(headless['checks'])} PASS, no engine errors |
| Retained original 2D GPU/shared-controls suite | {len(old2d['checks'])} PASS, no engine errors |
| New renderer benchmark | 12/12 coverage/budget checks PASS |

New regressions cover a cohort beyond 64 actors, published-frame immutability, partial/completed boundaries, acknowledgments, reset, real movement and fallback destinations, blocked movement, mutations, no-choice death, actual self/partner birth placement, newborn timing and deterministic full-state/RNG passivity. The extra observer-cost case measures rather than imposes a machine-specific timing threshold. Debug assembly identities still match the passing suite after the later renderer-only rebuild.

Godot checks exercise both depth cameras and all four levels, visible-cell counts, true/interior world edges, picking after zoom/pan/resize, all-visible versus focused action scopes, stale/expired outcomes, dead-selection replacement, stable appearance, minimap navigation, material pixels and actual injected mouse input. Layouts were checked at 1366×768 and 1920×1080. The live worker check uses a real selected organism, preserves paused state and RNG across view changes, advances one season and shuts down. Headless navigation proves handler/geometry behavior; GPU checks separately establish rendered pixels and injected Godot input. Physical mouse-to-photon latency, a physical DPI/monitor transition and IDE breakpoint interaction were not measured.

## Renderer performance and the repaired slowdown

Measured on **{benchmark['adapter']}**, Godot {benchmark['engine']} Compatibility rendering, 1920×1080, 60 FPS cap. Each workload has 3 s warmup and 7 s sampling, with 70/70 requested 10 Hz snapshots delivered and no skipped sample updates. Main-loop intervals include fixture replay and UI work; they exclude simulation calculation and are not hardware presentation measurements.

| World population | Detail | Visible organisms | Visible actions | FPS | p95 frame interval |
| --- | --- | --- | --- | --- | --- |
{table}

The first dense L3 implementation naturally failed its 33.3 ms p95 budget: 54.16 FPS, 42.94 ms p95 and 58.48 ms maximum, while still displaying 364 visible actions. It issued many separate drawing calls and repeatedly accessed native projection properties. The repair batches all muted line geometry by color and caches projection data once per redraw. The same workload now reports {metrics[-1]['averageFps']:.2f} FPS and {metrics[-1]['p95FrameMilliseconds']:.2f} ms p95, preserving all 364 cues. No test threshold or spatial coverage was weakened. Level 4 retains detailed focused geometry.

## Observation cost

The controlled 10,000-organism Move/Self workload completed {cost['completedPairs']}/2 pairs in Release ({cost['elapsedSeconds']:.2f} s total). It uses serial deterministic execution, alternating pair order, 3 warmup plus 5 measured seasons per run and a 10 s cooperative bound. Occupancy stays full. Both complete pairs preserve the compact cell/organism/gene/RNG digest; learning internals are covered by the separate full-state passivity regression, not this cost digest.

{cost_lines}

This comparison includes **all collector work versus no collector at all**. It is not a measured regression against the previous viewer's collector. It covers a simple full-board workload, not mixed actions or long learning histories; percentages include host/JIT/GC variation. Capture timing is separate from season calculation. The complete Debug and Release measurements are retained in their current result files.

## Build recovery and evidence horizon

The first sandboxed VS build could not read NuGet's user-settings location during Godot SDK resolution. Running the already-authorized build with host access resolved that environment restriction. The first compiled viewer then exposed an incorrect Godot enum member, corrected against the installed API, and a hiding warning, corrected by renaming the helper. The first sandboxed GPU run passed its internal checks but reported a certificate-store access error at shutdown; host execution completed without that error. These natural failures were not counted as clean end-to-end passes.

Implementation started from local Git **{before['head']}**, with an initially clean tracked tree. The exact previous registered source bytes were checked before edits. Git reproduced the affected code/doc predecessors except the registered Tests Program/README versions, for which the smallest exact source snapshots were retained under KB/source-history. Source revisions preserve old identities and locators. The existing SRC-0115 historical-byte warning is unrelated and remains disclosed. No commit or push was performed.

Genepool Analyzer integrated and reviewed bounded transient helper contributions for the renderer, observation feed, independent action audit and verification. The root performed all builds and test runs. Installed SDK9.0.315, net9 targets, project mappings and launch profiles remain unchanged. Godot's installed native host reports {gpu['runtime']}; this does not change the solution target.

Current evidence: [builds](r03_step4_builds.json), [Debug tests](r03_step4_tests_debug.json), [Release tests](r03_step4_tests_release.json), [GPU](r03_step4_verification.json), [headless](r03_step4_headless.json), [benchmark](r03_step4_benchmark.json), and [original 2D](r03_godot_verification.json). KB transaction, receipt and final validation are r03_step4_transaction.json, r03_step4_receipt.json and r03_step4_validation.json. Current reports replace routine test history; no backup tree or historical test archive was created.

## Owner review and stop

Open the entire solution in Visual Studio 2022, build Debug and start the existing Genepool Godot profile. Try Habitat, Organism and Inspect; select a life, step a season, pan with the right/middle button and navigate with the minimap. Compare angled/top-down and Original 2D. The code is ready for this review; owner acceptance is pending. Work stops at Step 4. Step 5 polish, standalone packaging and deferred food mechanics await their own next instruction.
'''
(KB/'IMPROVEMENT_RESULT_R03_STEP4.md').write_text(report,encoding='utf-8')

updates = []
current_ids = []
next_id = max(int(s['source_id'][4:]) for s in sources)+1
for item in before['sources']:
    path = ROOT/item['path']
    if identity(path)['sha256'] == item['sha256']: continue
    sid = f'SRC-{next_id:04d}'; next_id += 1
    args = [sys.executable,'-X','utf8','-B',str(KB/'tools/kb.py'),'revise-source',item['source_id'],sid,
            '--expect-revision','54','--actor','Genepool Analyzer','--reason','Authorized R03 Step4 implementation and verification',
            '--record-id','DEC-0033','--record-id','FACT-0055','--compact']
    history = item['history']
    args += ['--git-commit',history['commit']] if history['kind']=='git' else ['--history-file',str(KB/history['path'])]
    result = subprocess.run(args,cwd=ROOT,capture_output=True,text=True,encoding='utf-8',check=True)
    changed = json.loads(result.stdout)['transaction']['upsert_sources']
    changed[1]['coverage'] = 'Relevant implementation or documentation changes inspected by root and bounded helpers; integrated builds and checks documented in IMPROVEMENT_RESULT_R03_STEP4.md. Exact current bytes hashed.'
    updates.extend(changed); current_ids.append(sid)
new_paths = ['Fistnet.Genepool.Godot/'+n for n in ['BoardView3D.cs','BoardView3D.Materials.cs','BoardView3D.Actions.cs','Main.Views.cs','MinimapView.cs','Verification/Step4Fixtures.cs','Verification/VerificationRunner.Step4.cs']]
new_paths += ['Fistnet.Genepool.Tests/SeasonObservationTests.cs','Fistnet.Genepool.Tests/SeasonObservationBenchmark.cs']
for relative in new_paths:
    path = ROOT/relative; entry = identity(path); sid=f'SRC-{next_id:04d}';next_id+=1
    updates.append({'source_id':sid,'title':relative,'locator':{'type':'file','path':str(path)},'sha256':entry['sha256'],
        'size_bytes':entry['bytes'],'observed_at_utc':now,'status':'implemented current working-tree source; owner review pending',
        'coverage':'New implementation authored by root or bounded transient helper, reviewed and integrated; relevant contracts verified in R03 Step4 current reports. Exact bytes hashed.'})
    current_ids.append(sid)

artifacts = [identity(KB/n) for n in ['IMPROVEMENT_RESULT_R03_STEP4.md','r03_step4_builds.json','r03_step4_tests_debug.json','r03_step4_tests_release.json','r03_step4_verification.json','r03_step4_headless.json','r03_step4_benchmark.json','r03_godot_verification.json','r03_step4_habitat.png','r03_step4_organism.png','r03_step4_inspect.png','tools/run_godot_checks.py']]
def record(id,kind,statement,uncertainty,refs,related,derivation):
    return {'id':id,'kind':kind,'status':'active','statement':statement,'uncertainty':uncertainty,'source_refs':refs,
        'related_record_ids':related,'origin':{'actor':'Genepool Analyzer','type':'authorized_implementation_and_verification','recorded_at_utc':now},'derivation':derivation}
decision = record('DEC-0033','owner_decision','Owner authorizes starting R03 Step4 implementation from the accepted visual design and will review the work after sleeping.',
    'Implementation authorization is not acceptance of the result, Step5 authorization, ecological trial permission or approval of deferred food-rule changes.',[],['DEC-0031','REC-0010'],{})
decision['origin']={'actor':'User','recorded_by':'Genepool Analyzer','type':'direct_user_message','recorded_at_utc':now,'quote':'Good! You can start now with step 4. I am off to sleep. I will see what you did in the morning :D'}
implementation = record('FACT-0055','observed_fact','R03 Step4 implemented: Godot depth viewport with angled/top-down cameras, brown-green procedural terrain, identity-stable mould colonies, four detail presets, visible-cell batching, true world edges, minimap/focus navigation, complete detached season outcomes and level3 all-visible muted versus level4 focused feedback. Existing Godot2D and WinForms entry points remain.',
    'Working implementation awaiting owner review. Finite colors and procedural shapes are display aids; effects refer to action-time cells. Fast playback samples whole seasons. Step5 and deferred food mechanics remain unstarted; no simulation rules/RNG changed.',current_ids,['DEC-0033','DEC-0031','REC-0010','FACT-0056','DEC-0027'],
    {'artifacts':artifacts,'source_horizon_utc':now,'base_commit':before['head'],'owner_acceptance':'pending','author':'Genepool Analyzer with bounded transient helper contributions','locators':['BoardView3D: camera/culling/material batches','BoardView3D.Actions: actual outcomes, death/history and batched muted geometry','ViewCollector.ActorCompleted/Born/Capture: complete current cohort and actual child placement','Main.Views/MinimapView: navigation and viewer selection']})
verification = record('FACT-0056','observed_fact',f'R03 Step4 verification passed: whole-solution VS2022 Debug/Release zero-error builds,159/159 console checks in each;{len(gpu["checks"])} GPU,{len(headless["checks"])} headless and{len(old2d["checks"])} retained2D checks. RX6800 1920x1080 overview0/1000/10000 and denseL3 all about60FPS,p95 about16.7ms,complete10Hz replay. Initial denseL3 p9542.94ms repaired through batched cues without dropping364visible outcomes.',
    'Local bounded synthetic workloads, not ecological or physical presentation results.106existingCA1416warnings. Full collector-versus-none cost is measured separately and is not the incremental regression from the old viewer. IDEdebugging/physicalDPI/export not newly established. KnownSRC0115historical-byte gap persists.',current_ids,['FACT-0055','DEC-0033'],
    {'artifacts':artifacts[:8],'gpu_measurements':metrics,'observation_cost_report':'r03_step4_tests_release.json','initial_failures':['Sandbox NuGet access; host build resolved it','Incorrect KeepHeight enum and Material hiding warning fixed','Sandbox GPU certificate-store error; host run clean','DenseL3 initial54.16FPS,p9542.94ms,max58.48ms; same364cue workload passes after batching'], 'debug_assembly_identities_rechecked':True})
proposal = copy.deepcopy(records['REC-0010'])
proposal.setdefault('resolution_notes',[]).append({'actor':'Genepool Analyzer','at_utc':now,'prior_statement':proposal['statement'],'note':'DEC-0033 starts implementation; FACT-0055/0056 record completed engineering and current checks. Owner result review remains pending.','record_ids':['DEC-0033','FACT-0055','FACT-0056']})
proposal['statement']='The accepted R03 Step4 design is now implemented and verified under DEC-0033; current result FACT-0055/0056 and IMPROVEMENT_RESULT_R03_STEP4.md await owner review. No Step5 start or food-cycle change.'
proposal['uncertainty']='Owner accepted the design, not yet this implementation. Performance and interaction coverage are bounded as recorded in FACT-0056.'
proposal['derivation']['next_action']='Stop for owner review of implemented Step4.'
proposal['current_resolution']={'record_ids':['FACT-0055','FACT-0056'],'statement':'Implementation ready for owner review; design history retained.'}
old_fact=copy.deepcopy(records['FACT-0053'])
old_fact['current_resolution']={'record_ids':['FACT-0055','FACT-0056'],'statement':'The earlier limits describe the prior2D implementation. Step4 adds a separate depth view and complete-season outcome feed; earlier2Dappearance/markers remain available.'}
old_dec=copy.deepcopy(records['DEC-0031'])
old_dec['current_resolution']={'record_ids':['DEC-0033','FACT-0055','FACT-0056'],'statement':'Subsequent owner instruction starts implementation; engineering complete and owner review pending.'}

patch={k:copy.deepcopy(core[k]) for k in ['experiment','authority','proposal','current_iteration_part','next_iteration','routes','owner_decision_ids','environment','tooling']}
patch['experiment'].update(status='R03_Step4_implemented_verified_awaiting_owner_review',design_accepted_scope='R02 closed with ecology inconclusive; R03Godot2D accepted; Step4 design accepted DEC0031 and implemented under DEC0033. Result review pending; no active trial.')
patch['authority'].update(originals='Owner-authorized bounded Step4 edits, builds and verification completed; stop before next part.',next_action='Stop for owner review of R03 Step4 implementation.',current_stage_record='DEC-0033')
patch['proposal'].update(status='IMPLEMENTED_VERIFIED_AWAITING_OWNER_REVIEW',implementation_authorized=True,implementation_authority_record='DEC-0033',result_record='FACT-0055',result_path=str(KB/'IMPROVEMENT_RESULT_R03_STEP4.md'),limits='Current implementation and bounded checks completed; result acceptance pending. Step5/deferred food remain later.')
patch['current_iteration_part'].update(status='IMPLEMENTED_VERIFIED_AWAITING_OWNER_REVIEW',record_ids=['DEC-0033','FACT-0055','FACT-0056','REC-0010','DEC-0031','DEC-0030','DEC-0029'],report=str(KB/'IMPROVEMENT_RESULT_R03_STEP4.md'),verification='VS2022 Debug/Release builds and159/159tests each;106GPU94headless62original2Dchecks;12benchmarkchecks pass.',limits='Owner implementation review pending. No new ecological trial; Step5, export and food-cycle remain deferred.')
patch['next_iteration'].update(status='R03_STEP4_IMPLEMENTED_AWAITING_REVIEW',next_action='Stop for owner review of Step4; Step5 needs next instruction.',start_scope='Step4 implemented underDEC0033; result awaitingreview.',step4_implementation_authorized=True,step4_implementation_authority_record='DEC-0033')
patch['owner_decision_ids'].append('DEC-0033')
patch['routes']['r03_step4_result']={'record_ids':['FACT-0055','FACT-0056','DEC-0033','REC-0010','DEC-0031']}
for route in ['r03_step4_design','current_implementation']:
    patch['routes'][route]['record_ids']=['FACT-0055','FACT-0056','DEC-0033']+patch['routes'][route]['record_ids']
patch['environment']['status']='R03Step4 VS2022 Debug/Release builds pass and159/159consolechecks each; GodotRX6800 depth/pixels/control/benchmark verified.'
patch['environment']['record_ids'] += ['FACT-0056']
patch['environment']['r03_gpu_research']='Godot4.7.2 RX6800 Compatibility: Step4overview0/1000/10000 anddenseL3~60FPS,p95~16.7ms at1920x1080. SDK9.0.315/net9retained; installednativehost.NET10.0.9. Bounded results FACT0056.'
patch['tooling']['knowledgebase_git_scope']=f'CurrentbaseGitHEAD {before["head"]}; pre-edit trackedtreeclean. Step4 source/KB changes left uncommitted for owner review. No commit/push.'
tx={'actor':'Genepool Analyzer','note':'Record authorized R03 Step4 implementation, verification, repaired dense-action slowdown and owner-review checkpoint.',
    'upsert_records':[decision,implementation,verification,proposal,old_fact,old_dec],'upsert_sources':updates,'core_patch':patch}
save('r03_step4_transaction.json',tx)
print(json.dumps({'draft_only':True,'revision':54,'record_upserts':len(tx['upsert_records']),'source_upserts':len(updates),'new_source_ids':current_ids,'report':str(KB/'IMPROVEMENT_RESULT_R03_STEP4.md'),'transaction_sha256':identity(KB/'r03_step4_transaction.json')['sha256']}))
