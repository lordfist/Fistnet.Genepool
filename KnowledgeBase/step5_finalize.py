"""Prepare final R03 graphics result and reviewed transaction; never apply."""
import copy, datetime as dt, hashlib, json, os, subprocess, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parent.parent
KB = ROOT/'KnowledgeBase'
os.environ['PATH'] = r'C:\Program Files\Git\cmd;' + os.environ['PATH']
def read(name): return json.loads((KB/name).read_text(encoding='utf-8-sig'))
def save(name, value): (KB/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def identity(path):
    data=path.read_bytes()
    return {'path':str(path),'sha256':hashlib.sha256(data).hexdigest().upper(),'bytes':len(data)}
core=read('core.json'); assert core['kb_revision']==60
records={r['id']:r for r in map(json.loads,(KB/'records.jsonl').read_text(encoding='utf-8').splitlines())}
catalog=read('sources.json')['sources']
now=dt.datetime.now(dt.timezone.utc).isoformat()
before=read('r03_step5_predecessors.json')
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
names=['r03_step4_verification.json','r03_step4_headless.json','r03_godot_verification.json','r03_step4_benchmark.json']
gpu,headless,smoke,bench=results=[read(name) for name in names]
assert all(r['ok'] and r['process']['exitCode']==0 and not r['process']['engineErrors'] and all(c['passed'] for c in r['checks']) for r in results)
builds=read('r03_step5_builds.json')
for config in ['debug','release']:
    stage=builds['stages']['step5-final-'+config]
    assert stage['ok'] and stage['scratch_removed']
    for path,sha in stage['source_sha256'].items(): assert identity(ROOT/path)['sha256']==sha.upper(),path
builds['actor']='Genepool Analyzer'; save('r03_step5_builds.json',builds)
reused=[]
for config in ['Debug','Release']:
    report=read('r03_step4_tests_'+config.lower()+'.json')
    assert report['failed']==0 and report['total']==report['passed']==159
    for assembly,sha in report['assemblySha256'].items():
        assert identity(ROOT/assembly/'bin'/config/'net9.0-windows7.0'/(assembly+'.dll'))['sha256']==sha,(config,assembly)
    reused.append({'configuration':config,'tests':159,'report':'r03_step4_tests_'+config.lower()+'.json','hashes':report['assemblySha256'],
                   'execution':'Prior passing console suite reused by exact comparison of all five rebuilt non-Godot assembly identities.'})
upserts=[];source_ids=[]
for number,item in enumerate(before,295):
    assert identity(Path(item['history']))['sha256']==item['sha256']
    assert identity(Path(item['path']))['sha256']!=item['sha256'],'Unused safeguard'
    sid=f'SRC-{number:04d}'
    result=subprocess.run([sys.executable,'-X','utf8','-B',str(KB/'tools/kb.py'),'revise-source',item['source_id'],sid,
        '--expect-revision','60','--actor','Genepool Analyzer','--reason','R03 final graphics polish with owner-requested original full RGB colours',
        '--record-id','DEC-0037','--record-id','FACT-0061','--history-file',item['history'],'--compact'],cwd=ROOT,capture_output=True,text=True,encoding='utf-8',check=True)
    versions=json.loads(result.stdout)['transaction']['upsert_sources']
    versions[1]['title']=str(Path(item['path']).relative_to(ROOT))
    versions[1]['coverage']='Bounded full-RGB graphics and associated tests/docs inspected; current verification, provenance and limits in FACT-0061.'
    upserts.extend(versions);source_ids.append(sid)
new_path=ROOT/'Fistnet.Genepool.Godot/Verification/VerificationRunner.Step5.cs'
new_id=f'SRC-{295+len(before):04d}'; ident=identity(new_path)
upserts.append({'source_id':new_id,'title':str(new_path.relative_to(ROOT)),'locator':{'type':'file','path':str(new_path)},'sha256':ident['sha256'],
 'size_bytes':ident['bytes'],'observed_at_utc':now,'status':'implemented working source; owner review pending',
 'coverage':'Authored RGB mapping and actual GPU readability checks plus current previews; reviewed and executed under bounded Step5 scope.'})
source_ids.append(new_id)
evidence_paths=['Fistnet.Genepool.Dna/Common.cs','Fistnet.Genepool.Control/ViewCollector.cs','Fistnet.Genepool.Visualization/GameboardBitmap.cs','Fistnet.Genepool.Godot/MinimapView.cs']
for path in evidence_paths:
    matching=[s for s in catalog if s.get('locator',{}).get('path')==str(ROOT/path) and not s.get('supersession')]
    assert len(matching)==1,path
    assert identity(ROOT/path)['sha256']==matching[0]['sha256']
    source_ids.append(matching[0]['source_id'])
interventions=read('r03_step5_interventions.json') if (KB/'r03_step5_interventions.json').exists() else []
table='\n'.join(f"| {m['population']:,} | {m['detailLevel']} | {m['averageFps']:.2f} | {m['p95FrameMilliseconds']:.2f} ms | {m['maxFrameMilliseconds']:.2f} ms |" for m in bench['measurements'])
report=f'''# R03 Step 5 — Full RGB graphics polish

Genepool Analyzer · {now} · **Implemented and verified; awaiting owner review.**

The owner accepted and closed Step4 (DEC-0036) and directed continuation to Step5, the last milestone in the original five-step R03 outline. The owner then explicitly requested the full RGB spectrum used by the old WinForms view (DEC-0037), replacing the analyst's initial small-palette idea. R03 overall closure remains pending owner review.

## Result

Habitat and its minimap now use each snapshot cell's original 24-bit RGB colour directly, with opaque alpha. The seven pastel groups and pattern-hash replacement are removed. Dna.Common.GetOrganismColors and ViewCollector.PatternCode both pack eight ordered DNA action types, three bits each; directions and learned preferences are not included. Full RGB includes legitimate white, grey and pale DNA colours too: there is no forced pastel recolouring or quantization.

The flat mould material retains the supplied body hue and stable identity-based irregular silhouette. A contrasting edge separates dark or ground-coloured organisms without replacing their interiors. Close views use a thin fringe; the overview edge follows screen-pixel size. At the smallest scale the edge and body share pixels, so colour is clearer after zooming in. World-level display footprint increases from .64 to .88 cell width, improving distant visibility; close views retain their existing .92 footprint. Screen derivatives support antialiasing across depth. The former near-white rim treatment is removed. Pattern filtering derives the edge from original RGB and then dims the whole body/edge together, avoiding a bright-outline reversal.

WinForms historically lifts pure black to grey against its black background; Habitat preserves a black interior and gives it a visible edge. Inspector and README explain the actual DNA colour mapping. The accepted grey ground, dirt/grass shader, trapezoid camera, navigation and L3/L4 action rules are retained. No simulation rule or random draw changed.

## Verification

- Whole solution rebuilt with Visual Studio 2022 MSBuild, Debug and Release: zero errors, the same 106 existing CA1416 warnings. Project/SDK/solution settings unchanged.
- **{len(gpu['checks'])} GPU, {len(headless['checks'])} headless, and {len(smoke['checks'])} Original2D/shared-control checks passed**, with zero engine errors. Coverage includes actual camera/input/passivity, RGB conversion, representative colour readability and body/outline highlighting. Detailed sample counts, criteria and measurements are in the current GPU report.
- Earlier 159 console checks per configuration reused only after exact identity match of all five rebuilt non-Godot assemblies. They were not rerun for this Godot-only graphics change.
- **{len(bench['checks'])} benchmark checks passed** on {bench['adapter']},1920×1080 Compatibility renderer,60FPS cap. Four detached-frame workloads,3s warmup/7s measurement each; current delivery and timing values below.

| Population | Detail | Mean FPS | p95 frame interval | Maximum |
| --- | --- | --- | --- | --- |
{table}

These are bounded main-loop observations, excluding simulation calculation and physical display latency. They do not guarantee constant frame timing or ecological outcomes. The runner replaces current test reports and previews; prior records preserve dated results.

## Review and evidence

Current Step5 screenshots use synthetic detached RGB fixtures for visual comparison, not ecological trials. Their exact identities and those of reports/binaries are retained in FACT-0061. Actual sample-based checks support the exercised colours/backgrounds and views; they are not an accessibility certification or proof of every possible pixel arrangement. Owner visual acceptance, IDE breakpoint interaction and physical monitor/DPI transitions remain unverified here.

The first GPU evaluation passed 211 of 215 checks. Four overview visibility checks failed because colours matching the ground had a subpixel boundary. The implementation now uses a stronger, screen-sized edge at overview scale; all 215 checks passed on the subsequent run with the original contrast criteria unchanged. Source review also corrected the highlight-edge treatment before evaluation. The initial seven-colour proposal was replaced by the owner's explicit RGB instruction before GPU evaluation. These interventions are retained in FACT-0061.

### Current rendered examples

These captures use the same synthetic colour fixture. They show actual Godot output.

![Full RGB Habitat view](r03_step5_habitat.png)

![Full RGB Inspect view](r03_step5_inspect.png)

[World overview on grey ground](r03_step5_overview.png)

BaseGitHEAD {head}. Existing Step4 changes retained; no commit/push. Only affected registered predecessors have exact minimal KB history snapshots. KB verification retains the existing unavailable exact SRC-0115 historical-byte warning.

Review the Godot viewer from Visual Studio2022 as usual, compare Combined/Organisms at World and Habitat zoom, and select/highlight a pattern. Step4 remains owner accepted. Step5 and overall R03 closure await the owner's decision. Deferred food-cycle mechanics, exports and ecology remain outside this graphics pass.
'''
(KB/'R03_STEP5_GRAPHICS_RESULT.md').write_text(report,encoding='utf-8')
preview_paths=sorted(KB.glob('r03_step5_*.png'))
assert preview_paths,'Step5 previews missing'
artifacts=[identity(KB/name) for name in names+['r03_step5_builds.json','R03_STEP5_GRAPHICS_RESULT.md']]+[identity(p) for p in preview_paths]
binaries=[identity(ROOT/'Fistnet.Genepool.Godot/.godot/mono/temp/bin'/config/'Fistnet.Genepool.Godot.dll') for config in ['Debug','ExportRelease']]
decision={'id':'DEC-0037','kind':'owner_decision','status':'active',
 'statement':'Owner specifies full RGB organism colours as in the original WinForms2D view and removes the pastel replacement palette from final R03 graphics scope. Use the existing DNA RGB mapping, not a selected small set of hues.',
 'source_refs':[],'related_record_ids':['DEC-0036','REC-0011','FACT-0061'],
 'origin':{'type':'direct_owner_message','actor':'User','recorded_by':'Genepool Analyzer','recorded_at_utc':now,
 'quote':'Remove pastel colours, give organisms full RGB spectrum just like in the old 2D winforms view'}}
fact={'id':'FACT-0061','kind':'observed_fact','status':'active',
 'statement':f'Final R03 Step5 graphics implemented: original 24-bit DNA RGB replaces seven pastel groups in Habitat/minimap; hue-preserving flat mould with a contrasting edge and larger overview footprint; whole body/edge dimming for nonmatching patterns. VS2022 Debug/Release builds pass; {len(gpu["checks"])} GPU, {len(headless["checks"])} headless and {len(smoke["checks"])} original 2D checks pass; {len(bench["checks"])} current benchmark checks pass. Owner Step5 review and R03 closure remain pending.',
 'source_refs':source_ids,'related_record_ids':['DEC-0036','DEC-0037','REC-0011','FACT-0060'],
 'origin':{'type':'authorized_graphics_implementation_and_verification','actor':'Genepool Analyzer','recorded_at_utc':now,
 'contributors':'Transient colour/material, test and independent-review helpers; root integration, execution, documentation and sole canonicalKB writer.'},
 'tags':['R03','Step5','RGB','DNA colours','graphics','Godot','final step'],
 'uncertainty':'Actual bounded fixtures, not all24bit colours atallpossiblepixelalignments or accessibility certification. Existing106CA1416warnings; IDEdebugger/physicalDPI unverified. HistoricalSRC0115bytes gap retained. Step4 accepted;Step5/R03 acceptance pending; no ecological claim.',
 'derivation':{'source_horizon_utc':now,'base_commit':head,'artifacts':artifacts,'godot_binaries':binaries,'reused_console_evidence':reused,
 'gpu_measurements':gpu['measurements'],'benchmark_measurements':bench['measurements'],'interventions':interventions,
 'locators':['Dna/Common.cs:GetOrganismColors; Control/ViewCollector.cs:PatternCode andCollect; Visualization/GameboardBitmap.cs:ProcessBoard',
 'BoardView3D.cs:ColonyColor/UploadFrame; Materials.cs:ColonyShader; MinimapView.cs:Upload',
 'VerificationRunner.Step5.cs:RGB/pixel regressions andpreviews; Perspective.cs:invocation; Step4.cs:blackRGB contract'],
 'scope':'Godot appearance/docs/tests only. Accepted camera/ground/actionbehaviour retained; deferred foodcycle,exports,ecology andgovernance untouched.'}}
plan=copy.deepcopy(records['REC-0011'])
plan['current_resolution']={'record_ids':['DEC-0037','FACT-0061'],'statement':'OwnerfullRGB instruction replaces initialpalettechoice. Final graphics implemented/verified; stop for owner review.'}
plan.setdefault('resolution_notes',[]).append({'actor':'Genepool Analyzer','at_utc':now,'record_ids':['DEC-0037','FACT-0061'],
 'note':'Full24bitDNA RGB required by owner; seven-colour/saturation-only proposed tests abandoned beforeGPUevaluation. Current result FACT0061, awaitingreview.'})
old=copy.deepcopy(records['FACT-0060'])
old.setdefault('resolution_notes',[]).append({'actor':'Genepool Analyzer','at_utc':now,'record_ids':['FACT-0061','DEC-0037'],
 'note':'Step4 remains owneraccepted underDEC0036. Step5 updatesorganismcolours/material only; current rendererreports/previews replaced. Prior measuredStep4 evidence remains dated.'})
patch={k:copy.deepcopy(core[k]) for k in ['experiment','authority','proposal','current_iteration_part','next_iteration','environment','routes','owner_decision_ids']}
patch['experiment']['status']='R03_Step5_implemented_verified_awaiting_owner_review'
patch['experiment']['design_accepted_scope']='R02 closed with ecology inconclusive; R03 Godot 2D and Step4 results accepted. Final Step5 graphics implemented and verified; owner review pending. No active trial.'
patch['authority'].update(current_stage_record='DEC-0037',next_action='Stop for owner Step5 graphics review; R03closure pending.',originals='Authorized Step5 graphics edits/builds/checks complete; no further stage authorized.')
patch['proposal'].update(status='IMPLEMENTED_VERIFIED_AWAITING_OWNER_REVIEW',result_record='FACT-0061',result_path=str(KB/'R03_STEP5_GRAPHICS_RESULT.md'),
 appearance='Owner-requested full 24-bit DNA RGB; hue-preserving flat mould, contrasting edges and larger overview markers; approved ground/camera retained.',colour_decision_record='DEC-0037',limits='Original DNA RGB mapping is owner selected. Graphics implementation awaits review; no changed simulation mechanics.')
patch['current_iteration_part'].update(status='IMPLEMENTED_VERIFIED_AWAITING_OWNER_REVIEW',record_ids=['DEC-0036','DEC-0037','REC-0011','FACT-0061'],
 report=str(KB/'R03_STEP5_GRAPHICS_RESULT.md'),verification=f'VS2022Debug/Release;{len(gpu["checks"])}GPU{len(headless["checks"])}headless{len(smoke["checks"])}original2D pass;159console/config reusedbybinaryidentity;{len(bench["checks"])}benchmarkchecks pass.')
patch['next_iteration'].update(status='R03_STEP5_AWAITING_REVIEW',next_action='StopforStep5review; finalR03closure requires owneracceptance.')
patch['environment']['status']=patch['current_iteration_part']['verification']
patch['environment']['record_ids'].append('FACT-0061')
patch['environment']['r03_gpu_research']='FACT0061 records currentRGBgraphics timings; detachedrendereronly, notsimulation speed orconstantFPSguarantee.'
for route in ['current_implementation','ground_food_colours','r03_step5_graphics']:
    patch['routes'][route]['record_ids']=['FACT-0061','DEC-0037']+patch['routes'][route]['record_ids']
patch['owner_decision_ids'].append('DEC-0037')
save('r03_step5_transaction.json',{'actor':'Genepool Analyzer','note':'CompletefinalR03 graphics with ownerfullRGBmapping; stopforresultreview.','upsert_records':[decision,fact,plan,old],'upsert_sources':upserts,'core_patch':patch})
print(json.dumps({'ok':True,'from_revision':60,'new_sources':[s['source_id'] for s in upserts if not s.get('supersession')],
 'record_upserts':['DEC-0037','FACT-0061','REC-0011','FACT-0060'],'checks':[len(r['checks']) for r in results]}))
