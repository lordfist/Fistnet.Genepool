"""Prepare the bounded World-outline correction transaction; never apply it."""
import copy, datetime, hashlib, json, os, subprocess, sys
from pathlib import Path
KB = Path(__file__).resolve().parent
ROOT = KB.parent
os.environ['PATH'] = r'C:\Program Files\Git\cmd;' + os.environ['PATH']
def read(name): return json.loads((KB/name).read_text(encoding='utf-8-sig'))
def save(name, value): (KB/name).write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
def identity(path):
    return {'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest().upper()}
core = read('core.json')
assert core['kb_revision'] == 61
records = {r['id']: r for r in map(json.loads, (KB/'records.jsonl').read_text(encoding='utf-8').splitlines())}
sources = {s['source_id']: s for s in read('sources.json')['sources']}
gpu = read('r03_step4_verification.json')
builds = read('r03_world_border_builds.json')
stage = builds['stages']['world-border-debug']
assert gpu['ok'] and not gpu['process']['engineErrors'] and all(c['passed'] for c in gpu['checks'])
assert stage['ok'] and stage['scratch_removed']
for path, sha in stage['source_sha256'].items(): assert identity(ROOT/path)['sha256'] == sha.upper(), path
builds['actor'] = 'Genepool Analyzer'
save('r03_world_border_builds.json', builds)
now = datetime.datetime.now(datetime.timezone.utc).isoformat()
upserts = []
for old, new in [('SRC-0295','SRC-0302'),('SRC-0296','SRC-0303'),('SRC-0301','SRC-0304')]:
    history = KB/'source-history'/('world-border-'+old+Path(sources[old]['locator']['path']).suffix)
    result = subprocess.run([sys.executable,'-X','utf8','-B',str(KB/'tools/kb.py'),'revise-source',old,new,
        '--expect-revision','61','--actor','Genepool Analyzer','--reason','Owner requests borderless View 1 organisms; preserve closer-view appearance',
        '--record-id','DEC-0038','--record-id','FACT-0062','--history-file',str(history),'--compact'], cwd=ROOT,
        capture_output=True, text=True, encoding='utf-8', check=True)
    versions = json.loads(result.stdout)['transaction']['upsert_sources']
    versions[1]['coverage'] = 'Inspected bounded World-outline change and relevant GPU checks; results and limits in FACT-0062.'
    upserts.extend(versions)
report_path = KB/'R03_STEP5_GRAPHICS_RESULT.md'
previous_report = identity(report_path)
old_report = report_path.read_text(encoding='utf-8')
report_path.write_text(f'''# View 1 outline adjustment — current update

Genepool Analyzer · {now} · **Implemented; awaiting owner review.**

Owner DEC-0038 requests no black organism border at View 1. World now disables the organism contrast-outline blend throughout the entire View 1 zoom range (projected cell size below 12 pixels). Views 2–4 retain their existing outline, body colour, shape and texture. The selection indicator and real board boundary are unaffected.

The whole solution rebuilt successfully with Visual Studio 2022 in Debug, with zero errors and the existing platform warnings. The actual GPU suite passed **{len(gpu['checks'])}/{len(gpu['checks'])} checks**, with no engine errors. Its previous World contrast requirement depended on the now-unwanted outline; it was intentionally replaced before evaluation by borderless RGB pixel checks. Closer-view visibility and body-colour checks remain in place. A ground-matching organism can blend into the ground at World scale by design.

Current screenshots below were refreshed by this GPU run. Prior Release, headless, shared-control and benchmark results are dated evidence from FACT-0061, not repeated runs of this adjustment. The small shader change does not alter project settings or simulation code. Step5/R03 acceptance remains pending.

![Borderless World organisms](r03_step5_overview.png)

---

The initial Step5 result below is retained as dated history. Its outline description and old current-report hashes are superseded by FACT-0062 for this adjustment; the shared current screenshot/report paths now show the updated run.

''' + old_report, encoding='utf-8')
decision = {'id':'DEC-0038','kind':'owner_decision','status':'active',
    'statement':'Owner requests removal of the black organism border throughout View 1 zoom. This is a bounded appearance adjustment, not acceptance or closure of Step5/R03.',
    'origin':{'type':'direct_owner_message','actor':'User','recorded_by':'Genepool Analyzer','at_utc':now,
              'quote':'can you remove the black border completely as long as you are in view 1 zoom?'},
    'source_refs':[],'related_record_ids':['DEC-0037','FACT-0061','FACT-0062']}
fact = {'id':'FACT-0062','kind':'observed_fact','status':'active',
    'statement':f'World/View1 organism outlines are disabled below the same 12-pixel threshold used by the UI detail level; closer outlines and other rendering behavior retained. VS2022 whole-solution Debug build and {len(gpu["checks"])} actual GPU checks pass. Step5 review remains pending.',
    'source_refs':['SRC-0302','SRC-0303','SRC-0304','SRC-0297'], 'related_record_ids':['DEC-0038','FACT-0061'],
    'origin':{'type':'authorized_local_appearance_adjustment','actor':'Genepool Analyzer','at_utc':now},
    'derivation':{'locators':['BoardView3D.Materials.cs:ColonyShader outline blend','BoardView3D.cs:DetailLevel',
        'VerificationRunner.Step5.cs:Step5RgbPixelChecks'],
        'artifacts':[identity(KB/p) for p in ['r03_world_border_builds.json','r03_step4_verification.json',
            'r03_step5_overview.png','r03_step5_habitat.png','r03_step5_inspect.png','R03_STEP5_GRAPHICS_RESULT.md']],
        'previous_result_report':previous_report,
        'intervention':read('r03_world_border_intervention.json'),
        'verification_scope':'Current Debug build plus GPU suite and rendered preview review; no repeat benchmark, Release, or unchanged simulation suite.',
        'test_contract_change':'Before evaluation, replace the former mandatory World contrast-with-terrain criterion with absence of added outline tint. Owner explicitly requests borderless RGB at this scale; retained close-view contrast criteria are unchanged.'},
    'uncertainty':'At World scale a colour matching terrain may blend into it; full 24-bit RGB is retained. Existing SRC-0115 historical-byte warning remains. No owner acceptance inferred.'}
prior = copy.deepcopy(records['FACT-0061'])
prior.setdefault('resolution_notes',[]).append({'actor':'Genepool Analyzer','at_utc':now,'record_ids':['DEC-0038','FACT-0062'],
    'note':'Owner requests World outlines removed. FACT-0062 records implementation and current GPU result; prior outline/contrast claims are dated. Shared screenshots and GPU report were refreshed, so prior artifact hashes retain historical identity only.'})
patch = {key:copy.deepcopy(core[key]) for key in ['proposal','current_iteration_part','authority','routes','owner_decision_ids']}
patch['proposal'].update(result_record='FACT-0062',appearance='Full DNA RGB; borderless World dots; existing outlined flat mould in Views 2–4. Accepted terrain/camera retained.')
patch['current_iteration_part']['record_ids'].extend(['DEC-0038','FACT-0062'])
patch['current_iteration_part']['verification']=f'Current View1 adjustment: VS2022 Debug build and {len(gpu["checks"])} GPU checks pass. Other Step5 evidence remains dated in FACT-0061.'
patch['authority'].update(current_stage_record='DEC-0038', originals='Authorized World-outline adjustment complete; stop for review.')
patch['owner_decision_ids'].append('DEC-0038')
for route in ['current_implementation','r03_step5_graphics']:
    patch['routes'][route]['record_ids']=['FACT-0062','DEC-0038']+patch['routes'][route]['record_ids']
save('r03_world_border_transaction.json', {'actor':'Genepool Analyzer','note':'Record owner-requested borderless World view and verification; stop for review.',
    'upsert_records':[decision,fact,prior],'upsert_sources':upserts,'core_patch':patch})
print(json.dumps({'ok':True,'revision':61,'gpu_checks':len(gpu['checks']),'sources':['SRC-0302','SRC-0303','SRC-0304']}))
