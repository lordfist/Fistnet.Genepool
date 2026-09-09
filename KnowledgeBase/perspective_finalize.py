"""Draft the bounded perspective result and one reviewed KB transaction; never apply."""
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
    data=path.read_bytes()
    return {'path':str(path),'sha256':hashlib.sha256(data).hexdigest().upper(),'bytes':len(data)}
core=read('core.json')
assert core['kb_revision']==56
records={r['id']:r for r in map(json.loads,(KB/'records.jsonl').read_text(encoding='utf-8').splitlines())}
sources=read('sources.json')['sources']
before=read('r03_perspective_predecessors.json')
now=dt.datetime.now(dt.timezone.utc).isoformat()
gpu,headless,smoke,bench=[read(n) for n in ['r03_step4_verification.json','r03_step4_headless.json','r03_godot_verification.json','r03_step4_benchmark.json']]
assert all(r['ok'] and r['process']['exitCode']==0 and not r['process']['engineErrors'] for r in [gpu,headless,smoke,bench])
builds=read('r03_perspective_builds.json')
for config in ['debug','release']:
    stage=builds['stages']['perspective-final-'+config]
    assert stage['ok'] and stage['scratch_removed']
    for path, sha in stage['source_sha256'].items():
        assert identity(ROOT/path)['sha256']==sha.upper(),path
builds['actor']='Genepool Analyzer'
save('r03_perspective_builds.json',builds)
reused=[]
for config in ['Debug','Release']:
    name='r03_step4_tests_'+config.lower()+'.json'
    report=read(name)
    assert report['failed']==0 and report['total']==report['passed']==159
    for assembly,sha in report['assemblySha256'].items():
        assert identity(ROOT/assembly/'bin'/config/'net9.0-windows7.0'/(assembly+'.dll'))['sha256']==sha, (config,assembly)
    reused.append({'configuration':config,'passing_report':name,'tests':159,'all_five_assembly_hashes_match':True,'hashes':report['assemblySha256'],'execution':'Prior passing suite reused after exact current binary comparison; not rerun this correction.'})
interventions=read('r03_perspective_interventions.json')
table='\n'.join(f"| {m['population']:,} | L{m['detailLevel']} | {m['visibleActionCues']} | {m['averageFps']:.2f} | {m['p95FrameMilliseconds']:.2f} ms | {m['maxFrameMilliseconds']:.2f} ms |" for m in bench['measurements'])
failure_text='\n'.join(f"- **{f['stage']}**: {f['issue']} {f['resolution']}" for f in interventions)
body_metrics=[m for m in gpu['measurements'] if m['kind']=='perspective_colony_coverage']
demo_readme='https://raw.githubusercontent.com/godotengine/godot-demo-projects/master/mono/2.5d/README.md'
demo_transform='https://raw.githubusercontent.com/godotengine/godot-demo-projects/master/mono/2.5d/addons/node25d-cs/Transform25D.cs'
report=f"""# R03 Step 4 — Perspective correction

Genepool Analyzer · {now} · **Implemented and verified, awaiting owner review.**

The owner approved a closer/wider front edge, farther/narrower back edge, receding grid and modest diagonal. This corrects the shortcoming in FACT-0057. Approval of that correction is DEC-0034; acceptance of the resulting implementation remains pending.

## Result

Habitat angled now uses an actual perspective camera, at 50° above the ground with an 18° diagonal orientation and a 40° fitted vertical field of view. At the tested fitted sizes the front edge is about 39% longer on screen than the back edge. Top-down remains an unskewed orthographic view. No new dependency, simulation rule, learning logic or random draw changed.

Picking, pointer-anchored zoom, right/middle drag, selection, action anchors, actual world borders and visible-cell filtering now follow the projected ground. The minimap draws the actual visible ground polygon. Zoom changes field of view while the camera distance keeps every board point in front; tight world-derived depth bounds prevent the almost coplanar organism and ground layers from fighting. All-visible muted L3 and focused-only L4 action scopes are retained.

These are actual renderer captures on a detached 1,800-organism synthetic review fixture, not generated mockups or ecological outcomes. Its run controls are disabled because it owns no simulation; normal startup retains live controls.

![Angled world](r03_perspective_world.png)

![Angled Habitat detail](r03_perspective_detail.png)

[Same fixture top-down](r03_perspective_topdown.png)

## Verification

- **Whole-solution Visual Studio 2022 Debug and Release builds: PASS**, 0 errors and the same106 existing CA1416 warnings. SDK/framework/project/solution settings unchanged.
- **GPU: {len(gpu['checks'])} checks PASS**, including native-camera projection oracles, front/back ratios, receding rows, modest diagonal, all4 fit corners, clipping, native near/far picking, zoom/drag through Godot input, 80× maximum zoom, independent polygon culling, minimap footprint and near/far action pixels. Both1366×768 and1920×1080 exercised. No engine errors.
- **Headless: {len(headless['checks'])} checks PASS. Original2D/shared-controls GPU suite: {len(smoke['checks'])} PASS.** Paused state and RNG passivity, same-run view switching, real worker step and shutdown passed.
- **Mould pixels:**72 visible samples across Habitat/Organism detail. Body area30.36–32.70% of projected cell,100% central-body coverage and minimum99.09% connected body. Readability thresholds were15–60% area,90% central coverage,85% connected mat. Exact fractions are in current GPU measurements.
- **Prior console regression evidence reused:**159/159 in each configuration. All five rebuilt non-Godot assembly SHA-256 identities match their passing Debug/Release reports. Those suites were not rerun for these Godot-only edits.
- Current benchmark repeat:12/12 checks PASS. Every workload delivered70/70 requested10Hz snapshots after3s warmup and7s measurement. GPU: {bench['adapter']}; Godot {bench['engine']}, Compatibility,1920×1080,60FPS cap.

| Population | Detail | Visible cues | Mean FPS | p95 frame interval | Maximum |
| --- | --- | --- | --- | --- | --- |
{table}

These are main-loop timings for detached snapshots, excluding simulation calculation and physical presentation latency. The first empty-board sample missed the33.3ms p95 gate; the controlled repeat passed, but variability remains unexplained. Therefore this is bounded passing evidence, not a guarantee of60FPS. Dense perspective L3 contains393 visible cues; the old orthographic benchmark contained364, so its timing is not an identical-workload optimization comparison.

## Failures and corrections retained

{failure_text}

## Owner-supplied 2.5D demo

The [official C# demo README]({demo_readme}) links the exact Store page supplied by the owner. It describes2D sprites/camera,3D coordinates, fixed viewing bases, sorting and shadow placement. Its [Transform25D implementation]({demo_transform}) uses a fixed linear coordinate mapping without perspective division. Inference: useful later for sprite, height and shadow ideas, but adopting that projection would not supply the requested near/far size difference. Keep the current Camera3D correction; retain this as a future visual reference. The Store page itself failed direct retrieval; its corresponding official sources were inspected on {now}. No demo installed, executed or copied into the project.

## Scope, source horizon and review checkpoint

Base Git HEAD remains {before['head']}. Previous Step4 edits were already uncommitted; they were preserved. Six changed registered predecessors have exact minimal in-KB snapshots because HEAD did not reproduce their registered bytes; two new source files and two official reference URLs are registered with this correction. A seventh initially safeguarded file was unchanged; its unused duplicate snapshot is removed only after exact comparison. Current source/artifact hashes and binary reuse evidence are recorded in FACT-0058.

The initial IMPROVEMENT_RESULT_R03_STEP4.md and its record assertions remain dated evidence; the runner replaces current report and preview files. FACT-0055/0056/0057 point to this correction. No historical test archive or whole-KB backup is created.

Open the entire Fistnet.Genepool.sln in VS2022 as usual, run the Godot viewer, choose Habitat angled, then compare Fit, Habitat and Organism; drag and select near/far organisms. F5 breakpoint interaction and physical monitor/DPI transitions were not newly tested. Owner visual acceptance remains pending. Step5, food cycle, exports and ecology remain outside this correction. No commit/push performed.
"""
(KB/'R03_STEP4_PERSPECTIVE_RESULT.md').write_text(report,encoding='utf-8')

upserts=[];current_ids=[];new_id=279
for item in before['sources']:
    if identity(ROOT/item['path'])['sha256']==item['sha256']:
        assert item['source_id']=='SRC-0269'
        snapshot=KB/item['history']['path']
        assert snapshot.resolve().parent==(KB/'source-history').resolve() and identity(snapshot)['sha256']==item['sha256']
        # Only this unused task-owned exact file; no directory deletion.
        snapshot.unlink()
        continue
    sid=f'SRC-{new_id:04d}';new_id+=1
    result=subprocess.run([sys.executable,'-X','utf8','-B',str(KB/'tools/kb.py'),'revise-source',item['source_id'],sid,
        '--expect-revision','56','--actor','Genepool Analyzer','--reason','Owner-approved R03 Step4 perspective correction',
        '--record-id','DEC-0034','--record-id','FACT-0058','--history-file',str(KB/item['history']['path']),'--compact'],
        cwd=ROOT,capture_output=True,text=True,encoding='utf-8',check=True)
    versions=json.loads(result.stdout)['transaction']['upsert_sources']
    versions[1]['title']=item['path']
    versions[1]['coverage']='Current perspective camera/navigation/overlay change inspected and integrated; checks and limitations in R03_STEP4_PERSPECTIVE_RESULT.md. Exact bytes hashed.'
    upserts.extend(versions);current_ids.append(sid)
for path in ['Fistnet.Genepool.Godot/BoardView3D.Projection.cs','Fistnet.Genepool.Godot/Verification/VerificationRunner.Perspective.cs']:
    data=identity(ROOT/path);sid=f'SRC-{new_id:04d}';new_id+=1
    upserts.append({'source_id':sid,'title':path,'locator':{'type':'file','path':str(ROOT/path)},'sha256':data['sha256'],'size_bytes':data['bytes'],'observed_at_utc':now,
        'status':'implemented working source; owner review pending','coverage':'Authored by bounded helper, independently reviewed and integrated; native-camera and GPU evidence in current R03 perspective result.'})
    current_ids.append(sid)
for title,url,coverage in [('Official C#2.5D demo README',demo_readme,'Complete39line README, correspondence to owner Store URL and stated design.'),('Official C#2.5D demo Transform25D.cs',demo_transform,'Complete125line source; FlatTransform and FlatPosition lines27-44 inspected statically; no execution.')]:
    sid=f'SRC-{new_id:04d}';new_id+=1
    upserts.append({'source_id':sid,'title':title,'locator':{'type':'url','url':url},'observed_at_utc':now,'coverage':coverage,'status':'reference inspected; not adopted as dependency'})
    current_ids.append(sid)
assert new_id==289
# Remove the unused history pointer from this preparation inventory, preserving its observed unchanged identity.
for item in before['sources']:
    if item['source_id']=='SRC-0269':
        item.pop('history',None);item['outcome']='Unchanged by perspective correction; unnecessary task-owned snapshot removed after exact comparison.'
save('r03_perspective_predecessors.json',before)
artifacts=[identity(KB/name) for name in ['R03_STEP4_PERSPECTIVE_RESULT.md','r03_perspective_builds.json','r03_step4_verification.json','r03_step4_headless.json',
    'r03_godot_verification.json','r03_step4_benchmark.json','r03_step4_tests_debug.json','r03_step4_tests_release.json','r03_perspective_interventions.json',
    'r03_perspective_world.png','r03_perspective_detail.png','r03_perspective_topdown.png','r03_step4_habitat.png','r03_step4_organism.png','r03_step4_inspect.png']]
binary_evidence=[identity(ROOT/'Fistnet.Genepool.Godot/.godot/mono/temp/bin'/config/'Fistnet.Genepool.Godot.dll') for config in ['Debug','ExportRelease']]
decision={'id':'DEC-0034','kind':'owner_decision','status':'active','statement':'Owner approves the R03 Step4 correction: closer/wider front edge, farther/narrower back edge, visibly receding grid, and modest diagonal orientation. Implement and verify that bounded perspective correction; completed-result acceptance remains pending.',
 'source_refs':[],'related_record_ids':['FACT-0057','DEC-0033','FACT-0058'],'origin':{'type':'direct_owner_message','actor':'User','recorded_by':'Genepool Analyzer','recorded_at_utc':now,
 'quote':'Yes, exaclty, this is what we need: 1. A closer, wider front edge and a farther, narrower back edge. 2. Grid lines visibly receding into the distance. 3. A modest diagonal orientation that reinforces the sense of looking across the ground.'}}
fact={'id':'FACT-0058','kind':'observed_fact','status':'active','statement':f'R03 Step4 perspective correction implemented and verified: perspective50deg elevation/18deg yaw, front edge about39% wider, receding grid; native ground picking/zoom/drag, polygon culling/minimap and projected cues. Tight depth bounds repair fragmented mould rendering. VS2022 Debug/Release builds pass;{len(gpu["checks"])}GPU,{len(headless["checks"])}headless,{len(smoke["checks"])}retained2D checks pass. Prior159tests/config reused after all five non-Godot binaries match. Benchmark initial11/12 with marginal empty miss; one quiet repeat12/12,57-60FPS, variable timings.',
 'uncertainty':'Awaiting owner implementation review. Current capture fixtures are synthetic; no ecology conclusion or new trial. Initial timing miss cause unproven; repeat pass is not constant60FPS guarantee.106existingCA1416warnings; IDEdebugging and physicalDPI unverified. HistoricalSRC0115 exact-byte gap remains. No new dependencies, simulation rules/RNG, Step5 or food cycle.',
 'source_refs':current_ids,'related_record_ids':['DEC-0034','FACT-0057','FACT-0055','FACT-0056','DEC-0030','DEC-0027'],
 'tags':['R03','step4','perspective','angle','rendering','Godot','2.5D demo'],
 'origin':{'type':'authorized_implementation_and_verification','actor':'Genepool Analyzer','recorded_at_utc':now,'contributors':'Bounded transient camera, test and independent-review helpers; root integration, GPU verification and canonical KB writer.'},
 'derivation':{'source_horizon_utc':now,'base_commit':before['head'],'artifacts':artifacts,'godot_binaries':binary_evidence,'reused_console_evidence':reused,
 'gpu_colony_measurements':body_metrics,'benchmark_measurements':bench['measurements'],'interventions':interventions,
 'locators':['BoardView3D.Projection.cs: ProjectGround/GroundAt/RefreshProjection/BoardClipDistances/IsCellVisible','BoardView3D.cs: ZoomAt/PanBy/CellAt/_GuiInput','BoardView3D.Actions.cs: DrawOverlay/VisibleAction/ClipLineToView','MinimapView.cs: ViewFootprint/ClipFootprint','VerificationRunner.Perspective.cs: native geometry and actualGPU oracles'],
 'demo_reference':{'owner_url':'https://store.godotengine.org/asset/godot-foundation/project-with-csharp-2-5d-demo/','store_direct_retrieval':'failed; corresponding official README links exact page',
 'assessment':'Official README and Transform25D reviewed. Inference: fixed affine 2D projection does not give near/far perspective scaling; useful future sprite/height/shadow reference. No import/install/execution.'}}}
updates=[decision,fact]
note='Owner-approved perspective correction implemented and verified in FACT-0058 / R03_STEP4_PERSPECTIVE_RESULT.md. Earlier claim/evidence retained as dated; current runner reports/previews replaced. Overall Step4 implementation acceptance pending.'
for id in ['FACT-0055','FACT-0056','FACT-0057']:
    old=copy.deepcopy(records[id])
    old.setdefault('resolution_notes',[]).append({'actor':'Genepool Analyzer','at_utc':now,'note':note,'record_ids':['DEC-0034','FACT-0058']})
    old['current_resolution']={'statement':note,'record_ids':['FACT-0058','DEC-0034']}
    updates.append(old)
patch={k:copy.deepcopy(core[k]) for k in ['experiment','authority','proposal','current_iteration_part','routes','owner_decision_ids','environment','tooling','next_iteration']}
patch['authority'].update(next_action='Stop for owner review of corrected Step4 perspective (FACT-0058).',current_stage_record='DEC-0034')
patch['proposal'].update(result_record='FACT-0058',result_path=str(KB/'R03_STEP4_PERSPECTIVE_RESULT.md'))
patch['current_iteration_part'].update(record_ids=['DEC-0034','FACT-0058','DEC-0033','FACT-0055','FACT-0056','REC-0010'],
 report=str(KB/'R03_STEP4_PERSPECTIVE_RESULT.md'),verification='VS2022 Debug/Release builds;170GPU145headless62retained2D pass;159tests/config reused by exactbinarymatch. Benchmark11/12 then12/12; timing varies.',
 review_status='Angled correction implemented/verified underDEC0034; awaiting owner visual review.')
patch['environment']['status']='Perspective: VS2022 Debug/Release builds pass,170GPU145headless62original2D;159consolechecks/config reused by exactbinarymatch.'
patch['environment']['r03_gpu_research']='PerspectiveRX6800: repeat57-60FPS,p9516.7-22.9ms; initialempty33.69ms miss. Complete10Hz replay; no fixed60FPS guarantee. FACT0058.'
patch['environment']['record_ids'].append('FACT-0058')
patch['tooling']['knowledgebase_git_scope']='BaseHEAD14b39d984555897e88c279ed21651df7c114e05d. PreviousStep4 edits and currentcorrection uncommitted; no commit/push.'
patch['next_iteration']['next_action']='Stop for owner review of Step4 perspective correction; Step5 needs next instruction.'
for route in ['current_implementation','r03_step4_design','r03_step4_result']:
    patch['routes'][route]['record_ids']=['FACT-0058','DEC-0034']+patch['routes'][route]['record_ids']
patch['owner_decision_ids'].append('DEC-0034')
patch['last_maintenance']={'at':now,'actor':'Genepool Analyzer','note':'Record approved perspective correction, actual checks and retained failures; stop for owner review.'}
save('r03_perspective_transaction.json',{'actor':'Genepool Analyzer','note':'Complete owner-approved R03Step4 perspective correction; preserve histories and stop for review.',
    'upsert_records':updates,'upsert_sources':upserts,'core_patch':patch})
save('r03_perspective_evidence.json',{'actor':'Genepool Analyzer','at_utc':now,'input_revision':56,'sources_added':current_ids,'registered_predecessors_updated':6,
    'records_added':['DEC-0034','FACT-0058'],'records_updated':['FACT-0055','FACT-0056','FACT-0057'],'reused_console_evidence':reused,'artifacts':artifacts,'godot_binaries':binary_evidence})
print(json.dumps({'ok':True,'records':len(updates),'source_upserts':len(upserts),'new_sources':current_ids,'report':str(KB/'R03_STEP4_PERSPECTIVE_RESULT.md'),'kb_unchanged':read('core.json')['kb_revision']==56}))

