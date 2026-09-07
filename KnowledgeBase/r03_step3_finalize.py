"""Prepare verified result knowledge; canonical commit remains a reviewed kb.py transaction."""
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

ROOT = Path(r'D:\Posao\Fistnet.Genepool')
KB = ROOT / 'KnowledgeBase'
NOW = datetime.now(timezone.utc).isoformat()
core = json.loads((KB/'core.json').read_text(encoding='utf-8'))
assert core['kb_revision'] == 46
catalog = json.loads((KB/'sources.json').read_text(encoding='utf-8'))['sources']
records = {r['id']: r for r in map(json.loads, (KB/'records.jsonl').read_text(encoding='utf-8').splitlines())}
git = r'C:\Program Files\Git\cmd\git.exe'
head = subprocess.check_output([git, '-C', str(ROOT), 'rev-parse', 'HEAD'], text=True).strip()
assert head == 'bdfaa0c555edf8807f680f46b71e0ef16e387d07'

def identity(path):
    data = path.read_bytes()
    return dict(path=str(path), sha256=hashlib.sha256(data).hexdigest().upper(), size_bytes=len(data))

def read(name):
    return json.loads((KB/name).read_text(encoding='utf-8'))

builds = read('r03_step3_builds.json')
regressions = read('r03_step3_regressions.json')
verification = {}
for configuration in ['Debug', 'Release']:
    build = builds['stages']['godot_final_' + configuration.lower()]
    result = regressions['configurations'][configuration]
    assert build['ok'] and build['scratch_removed'] and result['ok'] and result['scratch_removed']
    tests = result['records'][-1]
    assert tests['total'] == tests['passed'] == 149 and tests['failed'] == 0
    source_errors = [name for name, digest in build['source_sha256'].items() if identity(ROOT/name)['sha256'].lower() != digest.lower()]
    assembly_errors = [name for name, digest in tests['assemblySha256'].items() if identity(ROOT/f'Fistnet.Genepool.Tests/bin/{configuration}/net9.0-windows7.0/{name}.dll')['sha256'] != digest]
    assert not source_errors and not assembly_errors, (source_errors, assembly_errors)
    verification[configuration] = dict(build_ok=True, tests_passed=149, source_inputs_verified=len(build['source_sha256']), loaded_assemblies_verified=len(tests['assemblySha256']), current_godot_assembly=identity(ROOT/f'Fistnet.Genepool.Godot/.godot/mono/temp/bin/{"Debug" if configuration == "Debug" else "ExportRelease"}/Fistnet.Genepool.Godot.dll'))
smoke, headless, benchmark = [read(name) for name in ['r03_godot_verification.json','r03_godot_headless.json','r03_godot_benchmark.json']]
assert all(row['ok'] for row in [smoke,headless,benchmark])
assert smoke['gpuExecuted'] and not headless['gpuExecuted'] and benchmark['gpuExecuted']
assert len(benchmark['measurements']) == 3 and all(r['frameBudgetPassed'] and r['replayCoverageComplete'] and r['sampleCoverageComplete'] for r in benchmark['measurements'])
assert read('r03_step3_startup.json')['normal_main_scene_exit_code'] == 0

next_source = max(int(s['source_id'][4:]) for s in catalog) + 1
upsert_sources = []
source_ids = {}
for old, relative, history in [('SRC-0202','.gitignore',['--git-commit',head]), ('SRC-0052','Fistnet.Genepool.sln',['--git-commit',head]), ('SRC-0165','Fistnet.Genepool.Tests/Program.cs',['--history-file',str(KB/'source-history/SRC-0165.cs')])]:
    new = f'SRC-{next_source:04d}'; next_source += 1
    command = [sys.executable,'-X','utf8','-B',str(KB/'tools/kb.py'),'revise-source',old,new,'--expect-revision','46','--actor','Genepool Analyzer','--reason','Owner-approved R03 Godot 2D implementation; exact predecessor verified before editing.','--record-id','DEC-0025',*history,'--compact']
    result = subprocess.run(command,capture_output=True,encoding='utf-8')
    assert result.returncode == 0, result.stdout + result.stderr
    draft = json.loads(result.stdout)
    upsert_sources.extend(draft['transaction']['upsert_sources'])
    source_ids[relative] = new

paths = [ROOT/'NuGet.Config',ROOT/'Fistnet.Genepool.Control/Presentation/BoardPresentation.cs',ROOT/'Fistnet.Genepool.Control/Presentation/SimulationSettings.cs',ROOT/'Fistnet.Genepool.Tests/GodotBoardTests.cs',ROOT/'Fistnet.Genepool.Tests/GodotSettingsTests.cs']
paths += [p for p in (ROOT/'Fistnet.Genepool.Godot').rglob('*') if p.is_file() and '.godot' not in p.relative_to(ROOT).parts and 'Properties' not in p.relative_to(ROOT).parts]
for path in sorted(paths):
    relative = path.relative_to(ROOT).as_posix()
    assert not any(s.get('locator',{}).get('path','').casefold() == str(path).casefold() for s in catalog), relative
    src = f'SRC-{next_source:04d}'; next_source += 1
    ident = identity(path)
    upsert_sources.append(dict(source_id=src,title=relative,locator={'type':'file','path':str(path)},sha256=ident['sha256'],size_bytes=ident['size_bytes'],observed_at_utc=NOW,origin={'actor':'Genepool Analyzer','type':'authorized_R03_2D_implementation','record_ids':['DEC-0025']},coverage='Complete new implementation/configuration source; compiled and exercised where applicable. Godot UID files are editor-generated identity metadata, not simulation evidence.' if path.suffix != '.md' else 'Complete new viewer setup, operation and verification documentation.'))
    source_ids[relative] = src
public_ids = []
for title,url,coverage in [
    ('Godot4.7.2 SpinBox behavior','https://raw.githubusercontent.com/godotengine/godot/4.7.2-stable/scene/gui/spin_box.cpp','Transient UI helper inspected value/text redraw and input parsing; root reproduced settings result and corrected signed input/harness timing.'),
    ('Godot4.7.2 external editor enum','https://github.com/godotengine/godot/blob/4.7.2-stable/modules/mono/editor/GodotTools/GodotTools/ExternalEditorId.cs','Transient setup helper verified VisualStudio enum1 used in portable editor preferences.')]:
    src=f'SRC-{next_source:04d}'; next_source += 1
    upsert_sources.append(dict(source_id=src,title=title,locator={'type':'url','url':url},coverage=coverage,observed_at_utc=NOW,origin={'actor':'Genepool Analyzer','type':'primary_source_implementation_research'}))
    public_ids.append(src)

archive = identity(ROOT/'.tools/downloads/Godot_v4.7.2-stable_mono_win64.zip')
assert archive['sha256'].lower() == 'a2a48473a7414c5f19fab690518caebb738c09ef9601f6bd2388676a7f53b3c0'
evidence = dict(actor='Genepool Analyzer',at_utc=NOW,head=head,implementation_authority='DEC-0025',status='2D ready for owner review; no next-stage implementation',source_ids=source_ids,
    source_horizon=[dict(identity(ROOT/path),source_id=src) for path,src in source_ids.items()],
    verified_build_inputs_and_test_assemblies=verification,
    godot_checks={'gpu_passed':len(smoke['checks']),'headless_passed':len(headless['checks']),'renderer_passed':len(benchmark['checks']),'backend':smoke['backend'],'adapter':smoke['adapter'],'native_host_runtime':smoke['runtime'],'engine':smoke['engine'],'measurements':benchmark['measurements'],'live_measurements':smoke['measurements']},
    dependency={'archive':archive,'sha512':'79229fd112b0c9cbeab82363a4ef7be18ea70f1caf86bf912789335b136fbe7e01db0053a33461438c5da1c680c17bbb10040bd09bedc51221cd4423d0367757','sha512_status':'Compared with publisher checksum list during download by transient setup helper.','extracted_distribution_files_verified':82,'extraction_method':'Helper compared SHA256 for every extracted original ZIP entry; original distribution retained, portable marker and editor preferences added separately.','executable':identity(ROOT/'.tools/godot/4.7.2/Godot_v4.7.2-stable_mono_win64.exe'),'download_sources':['https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_win64.zip','https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/SHA512-SUMS.txt'],'new_sdk_installed':False,'export_templates_installed':False},
    derived_support=[identity(KB/name) for name in ['tools/build_solution.py','tools/run_godot_checks.py','tools/run_r03_regressions.py','r03_step3_preedit_history.json','r03_step3_builds.json','r03_step3_regressions.json','r03_godot_verification.json','r03_godot_headless.json','r03_godot_benchmark.json','r03_step3_startup.json','r03-godot-1920x1080.png','r03-godot-1366x768.png','r03-godot-settings.png','r03-godot-live.png']],
    coverage={'source_changes':'Three existing registered sources versioned; new Godot app, two pure Control presentation helpers, two test sources and NuGet config. No Dna or simulation engine source changes.','benchmark_horizon':'Renderer/UI and benchmark method measured before only the last additional populated verification method; this does not claim the entire final assembly was benchmarked.','framework':'All whole-solution mappings inspected; Debug/Release builds executed; direct ExportRelease project with mapped Release Control/Dna observed. SDK9.0.315/net9 retained.','limits':'F5/breakpoints/step/locals inside VS2022 unverified; physical monitor transitions and exported release runtime unverified. Fixed fixtures and eight-season engineering world are not ecology. R02 closed INCONCLUSIVE; reserved evidence unread.','history':'Existing SRC-0115 exact historical bytes unavailable (FACT-0042); no new gap.','git':'No commit/push. Current implementation and KB are local changes; remote uninspected.'},
    interventions=['Restricted SDK/package resolution failed; authorized normal Windows permissions resolved it; project public feed/cache explicit.','Godot versus crypto RandomNumberGenerator name collision fixed.','Settings harness initially applied text before programmatic values redrew; synchronization wait fixed the test.','Actual intermediate-minus seed input issue fixed by committing completed text rather than every keystroke.','One direct SDK first-use invocation reported ASP.NET developer-certificate generation; no trust command run. Subsequent process settings suppress certificate generation, PATH changes and telemetry.'],
    attribution='Transient setup, GPU renderer and UI helpers contributed bounded implementation and static review; Genepool Analyzer owns integration, actual execution, source reconciliation and canonical KB.')
(KB/'r03_step3_evidence.json').write_text(json.dumps(evidence,indent=2)+'\n',encoding='utf-8')
artifacts=[dict(identity(KB/name),status='Current derived R03 implementation/review evidence; not owner acceptance.') for name in ['IMPROVEMENT_RESULT_R03_2D.md','r03_step3_evidence.json','r03_step3_builds.json','r03_step3_regressions.json','r03_godot_verification.json','r03_godot_headless.json','r03_godot_benchmark.json']]

def record(id,statement,refs,uncertainty):
    return dict(id=id,kind='observed_fact',status='active',statement=statement,source_refs=refs,related_record_ids=['DEC-0025','REC-0009'],tags=['R03','Step3','Godot','2D','VS2022'],origin={'actor':'Genepool Analyzer','type':'authorized_implementation_and_actual_verification','recorded_at_utc':NOW},uncertainty=uncertainty,derivation={'artifacts':copy.deepcopy(artifacts),'locators':['Result: setup, verification, measurements, interventions and review boundary. Evidence: source_horizon, verified_build_inputs_and_test_assemblies, godot_checks, dependency, coverage.']})

fact49=record('FACT-0049','Godot.NET4.7.2 Windows x64 downloaded with verified publisher checksums and complete82-file extraction. Root solution retains SDK9.0.315/net9 and VS2022-compatible mappings; Debug and Release whole-solution builds passed. Direct ExportRelease uses ordinary Release Control/Dna. Root public NuGet feed/cache and per-configuration locks added; portable editor and generated main-executable/native-debugging profile installed. Actual Godot native host selected installed.NET10.0.9; no new SDK or export templates installed.',[source_ids[p] for p in source_ids if p.endswith(('.sln','.csproj','.lock.json','Setup.ps1','Launch.ps1','NuGet.Config'))]+['SRC-0210','SRC-0211']+public_ids[1:], 'SDK/target compatibility and command launch are verified; actual VS2022 F5/breakpoints/stepping/locals remain owner-review checks. Native host runtime is not the project target. SDK first-use and permission details are disclosed in the result.')
fact50=record('FACT-0050','R03 Godot 2D viewer implemented: detached completed ViewFrames drive two GPU instance batches; camera, picking, food/organism layers, pattern dimming, selected DNA/actions/death retention, settings/presets, activity/history and playback commands are available. One unchanged SimulationRunner owns mutation. New Control helpers contain only presentation logic. No simulation mechanics, defaults or ecological policy change; WinForms remains available in the same solution.',list(source_ids.values()),'Implementation ready for owner review, not owner accepted. The new renderer isolates presentation but does not accelerate simulation calculation. Depth/3D, graphic asset work and packaging have not begun.')
fact51=record('FACT-0051',f'R03 verification PASS within scope:149/149 Debug and149/149 Release; whole VS2022 builds0errors with existing106CA1416warnings. Actual RX6800 OpenGL3.3 GPU smoke {len(smoke["checks"])} checks and headless {len(headless["checks"])} checks pass, including settings, actual transformed live selection, completed actions, state/RNG passivity, controlled busy-worker responsiveness and shutdown. Renderer fixtures0/1000/10000 each deliver100/100sample snapshots at10Hz and~60FPS, p95 16.68–16.69ms at1920x1080. Pause signal handler0.24ms. Normal startup and four inspected previews pass.',[source_ids[p] for p in source_ids if p.endswith('.cs')]+['SRC-0147','SRC-0149'],'Main-loop intervals/CPU upload timing are not GPU timestamps or physical mouse-to-photon latency. F5/debugger and monitor transitions remain unverified; no exported Release runtime. Eight-season seed29 world was engineering integration, not ecology. Existing SRC-0115 historical warning only; final KB receipt determines committed validation status.')
updates=[]
for key in ['REC-0009','REC-0008','FACT-0048']:
    value=copy.deepcopy(records[key]); value.setdefault('resolution_notes',[]).append({'actor':'Genepool Analyzer','at_utc':NOW,'prior_statement':value['statement'],'prior_uncertainty':value.get('uncertainty'),'note':'Approved R03 setup/2D implementation is now ready for review; current facts FACT-0049/0050/0051 and result report supersede earlier not-yet-installed/current-stage wording. Original planning findings keep their dated horizon. Owner result acceptance and later stages remain pending.','record_ids':['FACT-0049','FACT-0050','FACT-0051']})
    value['related_record_ids']=list(dict.fromkeys(value.get('related_record_ids',[])+['FACT-0049','FACT-0050','FACT-0051']))
    if key=='REC-0009':
        value['statement']='Owner-approved R03 Godot 2D plan implemented and verified within the limits in FACT-0049/0050/0051. Current result IMPROVEMENT_RESULT_R03_2D.md is ready for owner review. Actual VS2022 debugger interaction remains a review check; depth/3D, graphics polish and standalone packaging remain separate.'
        value['uncertainty']='Plan acceptance in DEC-0025 is distinct from result acceptance, which is pending. Measurements and unverified workflow limits are in FACT-0051.'
    elif key=='REC-0008':
        value['statement']='Godot engine choice accepted in DEC-0024; detailed plan REC-0009 accepted in DEC-0025 and first2D implementation now ready for owner review (FACT-0049/0050/0051). Earlier renderer comparisons retain their historical research horizon.'
        value['uncertainty']='Earlier research did not establish integration or performance; current measured results are FACT-0049/0050/0051. Future 3D still needs its own scene/camera/picking; owner2D acceptance and actual IDE debugger interaction remain pending.'
        value['derivation']['next_action']='Review IMPROVEMENT_RESULT_R03_2D.md and VS2022 startup/debugger workflow; no automatic next-stage work.'
    else:
        value['uncertainty']='Dated Step2 static planning evidence. Subsequent installed/build/GPU results are FACT-0049/0050/0051; actual IDE debugger interaction remains unverified.'
    updates.append(value)
patch={k:copy.deepcopy(core[k]) for k in ['experiment','routes','authority','proposal','environment','tooling','current_iteration_part','next_iteration']}
patch['experiment']['status']='R03_2D_implemented_ready_for_owner_review'
patch['routes']['r03_2d_result']={'record_ids':['DEC-0025','FACT-0049','FACT-0050','FACT-0051','REC-0009']}
patch['routes']['current_implementation']={'record_ids':['DEC-0025','FACT-0049','FACT-0050','FACT-0051']}
patch['routes']['environment_capabilities']['record_ids']+=['FACT-0049','FACT-0051']
patch['authority'].update(originals='Approved R03 2D source edits and bounded builds/tests complete; source predecessors reconciled.',next_action='Stop for owner review of Godot2D and VS2022 interactive startup/debugger workflow. No next-stage work.',current_research_scope='R03 implementation-related research/downloads completed; no active ecology or further installation.')
patch['proposal'].update(status='ACCEPTED_PLAN_IMPLEMENTED_RESULT_REVIEW_PENDING',limits='Implementation complete with stated verification limits; owner result acceptance pending.',result_record_ids=['FACT-0049','FACT-0050','FACT-0051'])
patch['environment'].update(status='R03 whole-solution VS2022 Debug/Release builds and149/149tests each pass; Godot4.7.2 RX6800 GPU verified.',r03_gpu_research='Installed portable Godot4.7.2. OpenGL3.3 RX6800:~60FPS/16.7msp95 with0/1000/10000fixtures. SDK9.0.315/net9 retained; Godot host.NET10.0.9. IDE F5/debug interaction unverified.')
patch['environment']['record_ids']+=['FACT-0049','FACT-0051']
patch['tooling'].update(current_verification_receipt=str(KB/'r03_step3_receipt.json'),knowledgebase_git_scope='HEADbdfaa0c555edf8807f680f46b71e0ef16e387d07; approved R03 source and KB edits remain local/uncommitted. No commit/push or remote inspection.')
patch['current_iteration_part']={'id':'R03_Step3','status':'IMPLEMENTED_VERIFIED_WITH_LIMITS; OWNER_REVIEW_PENDING','record_ids':['DEC-0025','FACT-0049','FACT-0050','FACT-0051'],'report':str(KB/'IMPROVEMENT_RESULT_R03_2D.md'),'verification':'VS2022 whole builds;149/149tests each;GPU/headless/pixels/live controls/renderer budget pass. See evidence and receipt.','limits':'Actual IDE debugging and monitor transitions unverified. Stop before depth/3D/artwork/export. Owner acceptance pending.'}
patch['next_iteration'].update(status='R03_2D_READY_FOR_OWNER_REVIEW',next_action='Owner reviews2D result and VS2022 workflow; await next applicable instruction.',start_scope='Authorized Step3 complete; later stages await instruction.')
txn={'actor':'Genepool Analyzer','note':'Record completed Godot2D implementation and bounded verification, preserve exact source lineage, and stop for owner review with explicit IDE/runtime and history limits.','upsert_sources':upsert_sources,'upsert_records':[fact49,fact50,fact51,*updates],'core_patch':patch}
(KB/'r03_step3_transaction.json').write_text(json.dumps(txn,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'revision':46,'sources_added':len(source_ids)+len(public_ids),'source_predecessors_updated':3,'records_added':['FACT-0049','FACT-0050','FACT-0051'],'records_updated':[r['id'] for r in updates],'verification':verification,'godot_check_counts':[len(smoke['checks']),len(headless['checks']),len(benchmark['checks'])]},indent=2))
