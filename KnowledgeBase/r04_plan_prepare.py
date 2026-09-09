"""Prepare the R03 acceptance / R04 planning transaction; never apply it."""
import copy
import datetime
import hashlib
import json
from pathlib import Path

KB = Path(__file__).resolve().parent
ROOT = KB.parent
ACTOR = 'Genepool Analyzer'
NOW = datetime.datetime.now(datetime.timezone.utc).isoformat()

def read(name):
    return json.loads((KB / name).read_text(encoding='utf-8-sig'))

def save(name, value):
    (KB / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def identity(path):
    data = path.read_bytes()
    return {'path': str(path), 'sha256': hashlib.sha256(data).hexdigest().upper(), 'size_bytes': len(data)}

core = read('core.json')
assert core['kb_revision'] == 62
assert ROOT == Path(r'D:\Posao\Fistnet.Genepool')
assert KB == Path(r'D:\Posao\Fistnet.Genepool\KnowledgeBase')
records = {r['id']: r for r in map(json.loads, (KB / 'records.jsonl').read_text(encoding='utf-8').splitlines())}
sources = {s['source_id']: s for s in read('sources.json')['sources']}
assert all(i not in records for i in ['DEC-0039', 'DEC-0040', 'FACT-0063', 'CLAIM-0009', 'REC-0012'])
assert 'SRC-0305' not in sources
inspected = [
    'global.json', 'Fistnet.Genepool.sln',
    *[f'Fistnet.Genepool.{p}/Fistnet.Genepool.{p}.csproj' for p in ['App','Control','Dna','Visualization','Tests','Godot']],
    'Fistnet.Genepool.Godot/project.godot',
    *[f'Fistnet.Genepool.Godot/{p}' for p in ['BoardView3D.cs','BoardView3D.Materials.cs','BoardView3D.Projection.cs','MinimapView.cs','Main.Views.cs']],
    'KnowledgeBase/tools/build_solution.py', 'KnowledgeBase/tools/run_godot_checks.py'
]
save('r04_planning_evidence.json', {
    'actor': ACTOR, 'recorded_at_utc': NOW, 'observation_date': '2026-09-08',
    'scope': 'Read-only installed-tool metadata, selected project sources and derived build helpers; public primary documentation. No project execution or installation.',
    'installed_metadata': {
        'visual_studio_2026': {'display_version': '18.9.2', 'installation_version': '18.9.12120.119', 'path': r'C:\Program Files\Microsoft Visual Studio\18\Community', 'complete': True, 'launchable': True, 'managed_desktop_workload_and_msbuild_present': True},
        'visual_studio_2022': {'display_version': '17.14.39', 'path': r'D:\Program Files\Microsoft Visual Studio\2022\Community', 'role': 'Historical development baseline; still installed'},
        'relevant_dotnet_sdks': ['9.0.317','10.0.301','10.0.400'],
        'dotnet10_core_and_desktop_runtime': '10.0.11',
        'godot_file_product_version': '4.7.2.stable.mono.official',
        'godot_path': str(ROOT / '.tools/godot/4.7.2/Godot_v4.7.2-stable_mono_win64_console.exe')
    },
    'metadata_probes': [
        'vswhere -all -products * -format json (relevant installations only retained)',
        'vswhere -version [18.0,19.0) -products * -requires Microsoft.VisualStudio.Workload.ManagedDesktop Microsoft.Component.MSBuild -property installationPath',
        r'C:\Program Files\dotnet\dotnet.exe --list-sdks',
        r'C:\Program Files\dotnet\dotnet.exe --list-runtimes',
        'Test-Path for VS2026 MSBuild and missing SDK directory; FileVersionInfo for installed Godot'
    ],
    'project_observations': {
        'all_six_target_frameworks': 'net9.0-windows7.0',
        'global_sdk': {'version': '9.0.315', 'rollForward': 'latestPatch', 'allowPrerelease': False},
        'stale_helper_sdk_directory': r'C:\Program Files\dotnet\sdk\9.0.315',
        'stale_helper_sdk_directory_exists': False,
        'interpretation': 'The helper pins an absent SDK. No build was attempted, and no compilation failure is claimed. Normal global.json policy may roll to installed 9.0.317; actual resolver/build not exercised.',
        'godot_sdk': 'Godot.NET.Sdk/4.7.2', 'renderer': 'gl_compatibility',
        'geometry': 'Real Camera3D/SubViewport, food and organism PlaneMesh batches; organism Y=.024; fixed-pose projection and ground picking need extension for raised bodies/free camera.'
    },
    'local_evidence_identities': [identity(ROOT / p) for p in inspected],
    'verification_not_performed': ['VS2026 solution loading/designer/debugger','net10 builds/tests/runtime launch','new 3D models/rendering/performance','export integration'],
    'limits': 'Metadata and static architecture evidence only. URL observations are dated published claims, not installed integration results or archived page bytes.'
})

result_path = KB / 'R03_STEP5_GRAPHICS_RESULT.md'
old_result_identity = identity(result_path)
old_result = result_path.read_text(encoding='utf-8')
assert not old_result.startswith('# Owner acceptance and closure')
result_path.write_text(
    '# Owner acceptance and closure — 8 September 2026\n\n'
    '**R03 Step 5 is accepted and closed by the owner (DEC-0039).** This completes the five planned R03 milestones. Full DNA RGB and borderless World/View 1 organisms are included in the accepted result.\n\n'
    'The owner next requests a planning-only proposal for VS2026/.NET10 and full 3D visuals (DEC-0040; REC-0012). No R04 implementation has started. The earlier pending-review statements below are retained as dated history; this acceptance supersedes only their review status. No tests were rerun for closure.\n\n---\n\n' + old_result,
    encoding='utf-8')

new_urls = [
    ('SRC-0305','Microsoft Visual Studio 2026 compatibility','https://learn.microsoft.com/en-us/visualstudio/releases/2026/compatibility','Supported .NET versions; VS2026 can host the existing .NET9 baseline.'),
    ('SRC-0306','Microsoft .NET support policy','https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core','Support dates: .NET9 2026-11-10; .NET10 LTS 2028-11-14.'),
    ('SRC-0307','Godot 4.7.2 exact managed API project','https://raw.githubusercontent.com/godotengine/godot/4.7.2-stable/modules/mono/glue/GodotSharp/GodotSharp/GodotSharp.csproj','TargetFramework net8.0; minimum library target, not local net10 runtime verification.'),
    ('SRC-0308','GodotSharp 4.7.2 official NuGet package','https://www.nuget.org/packages/GodotSharp/4.7.2','Published API package and computed .NET10 compatibility; no local integration claim.'),
    ('SRC-0309','Godot explanation of API and application frameworks','https://godotengine.org/article/godotsharp-packages-net8/','Distinction between library minimum framework and application retargeting; historical lifecycle wording not used.'),
    ('SRC-0310','Godot stable renderer comparison','https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html','Compatibility core3D suitability and implications of switching backend; no local Forward+ benchmark.'),
    ('SRC-0311','Godot ArrayMesh C# examples','https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/arraymesh.html','Reusable procedural-mesh C# starting example, not a ready-made mould asset.'),
    ('SRC-0312','Official Godot demo projects','https://github.com/godotengine/godot-demo-projects','MIT examples/version branches; 3D lighting/material references mostly GDScript need C# adaptation.')
]
source_upserts = [{'source_id': i, 'title': title, 'locator': {'type': 'url', 'url': url},
                   'observed_at_utc': NOW, 'coverage': coverage,
                   'origin': {'actor': ACTOR, 'type': 'R04_planning_primary_research'},
                   'limitations': 'URL registration does not archive bytes or establish local integration.'}
                  for i, title, url, coverage in new_urls]
for sid, coverage in [('SRC-0211','Refreshed SDK10.0.400/VS18.9 pairing, net10 requires VS18+, and down-level targeting.'),
                      ('SRC-0217','Refreshed 3D shared instance geometry/colour/custom data and whole-MultiMesh culling limits.'),
                      ('SRC-0198','Refreshed Nature Kit listing: CC0 3D vegetation; no matching mould model verified.')]:
    src = copy.deepcopy(sources[sid])
    src.setdefault('coverage_updates', []).append({'actor': ACTOR, 'observed_at_utc': NOW, 'coverage': coverage, 'record_ids': ['CLAIM-0009','REC-0012']})
    source_upserts.append(src)

def record(rid, kind, status, statement, refs, related, origin_type, **fields):
    return {'id': rid, 'kind': kind, 'status': status, 'statement': statement,
            'source_refs': refs, 'related_record_ids': related,
            'origin': {'type': origin_type, 'actor': 'User' if kind == 'owner_decision' else ACTOR, 'recorded_by': ACTOR, 'at_utc': NOW}, **fields}

dec39 = record('DEC-0039','owner_decision','active',
    'Owner accepts and closes R03 Step5. Its final full-DNA-RGB graphics and borderless World/View1 result are accepted. All five planned R03 milestones are now complete; no new runtime or ecological result is implied.',
    [], ['DEC-0036','DEC-0037','DEC-0038','FACT-0061','FACT-0062','REC-0011'], 'direct_owner_message')
dec39['origin']['quote'] = 'Ok close step 5 as accepted.'
dec40 = record('DEC-0040','owner_decision','active',
    'Owner requests a detailed planning-only next round: (1) upgrade the solution to installed VS2026 and consider .NET10, (2) full3D design and organism-model proposal, (3) execute full3D with organisms, (4) shaders/lighting/colours, (5) WASD plus mouse navigation. Do not start implementation; stop for plan review. The next sequential label R04 is analyzer organization. VS2026 is the newly requested development target; .NET10 remains a recommendation awaiting plan review.',
    [], ['DEC-0039','REC-0012','FACT-0063','CLAIM-0009','DEC-0004'], 'direct_owner_message')
dec40['origin']['quote'] = 'This is the plan for the next round of improvements, don\'t start just create a detailed plan based on this and let me review it'
dec40['authority_scope'] = 'Read-only project and installed-tool inspection plus public research and routine KB maintenance. No simulation source/governance/tool edits, installs, builds or new rendering execution in this task.'
fact63 = record('FACT-0063','observed_fact','active',
    'Read-only planning inspection finds VS2026 Community18.9.2 with desktop workload/MSBuild, SDK10.0.400 and .NET/desktop10.0.11 installed. All six projects still target net9.0-windows7.0 with Godot4.7.2/Compatibility. The build helper pins SDK9.0.315, whose exact directory is now absent. Current Godot bodies are flat meshes in a real3D camera. No VS2026/net10 build, launch, designer or performance result was produced.',
    ['SRC-0002','SRC-0069','SRC-0070','SRC-0068','SRC-0056','SRC-0230','SRC-0067','SRC-0241','SRC-0297','SRC-0293','SRC-0282'],
    ['DEC-0040','REC-0012','FACT-0062'], 'read_only_installed_metadata_and_static_code',
    derivation={'artifact': identity(KB/'r04_planning_evidence.json'), 'coverage': 'SDK/workload metadata, six targets, relevant build helpers and renderer seams; dated source identities in artifact.'},
    uncertainty='Framework eligibility is distinct from local integration. The absent helper SDK path is not a newly observed compilation failure. Current Git commit coverage was not re-established.')
claim9 = record('CLAIM-0009','source_claim','active',
    'Microsoft documents net10 targeting in VS18+ and SDK10.0.4xx paired with VS18.9; .NET9 ends support 2026-11-10 and .NET10LTS 2028-11-14. Godot4.7.2 API targets net8 with published computed net10 package compatibility. Godot documents core3D in Compatibility and shared MultiMesh rendering with whole-batch culling. Official C# mesh examples and CC0 Nature Kit vegetation are available; no matching ready-made mould asset was verified.',
    ['SRC-0211','SRC-0217','SRC-0198'] + [s[0] for s in new_urls], ['DEC-0040','FACT-0063','REC-0012'], 'official_primary_documentation_review',
    uncertainty='Published claims/framework compatibility and available examples do not establish local net10 execution, performance or a ready-made organism model. Source titles/coverage preserve the distinction.')
rec12 = record('REC-0012','recommendation','provisional',
    'R04 proposal: migrate coherently to VS2026 and .NET10 while retaining Godot4.7.2/backend; review real-volume raised mould models; implement a separate instanced Full3D mode with correct camera/picking/minimap; improve materials/lighting under measured budgets; then implement WASD/mouse navigation. Preserve accepted views, full DNA RGB, World borderless appearance and simulation passivity. Stop after each authorized step for review. Plan awaits approval; no implementation has started.',
    ['SRC-0297','SRC-0293','SRC-0302','SRC-0282','SRC-0291','SRC-0230','SRC-0211','SRC-0217','SRC-0198'] + [s[0] for s in new_urls],
    ['DEC-0040','DEC-0039','FACT-0063','CLAIM-0009','DEC-0027'], 'owner_requested_detailed_proposal',
    derivation={'artifact': identity(KB/'IMPROVEMENT_PROPOSAL_R04.md'), 'read_only_reviews': ['angle_review: toolchain/scope, no material findings','perspective_camera: no blocking contradictions; clarified accurate silhouette picking after candidate bounds and orbit camera with WASD moving ground focus']},
    uncertainty='net10 integration, final model choice and dense3D performance remain untested. Mesh/height/timing/input settings are analyzer recommendations, not owner decisions. Deferred food cycle/ecology outside this round.',
    current_resolution={'statement': 'Await owner review; next implementation instruction would be R04 Step1.', 'record_ids': ['DEC-0040']})

updated = []
for rid in ['FACT-0062','REC-0011','DEC-0036']:
    item = copy.deepcopy(records[rid])
    item.setdefault('resolution_notes', []).append({'actor': ACTOR, 'at_utc': NOW, 'record_ids': ['DEC-0039','DEC-0040'],
        'note': 'Owner now accepts/closes final R03 Step5. Earlier pending-review/closure statements are dated history. New R04 work is planning only; no test results rerun for closure.'})
    item['current_resolution'] = {'record_ids': ['DEC-0039','DEC-0040'], 'statement': 'R03 Step5 owner accepted and closed; all planned R03 milestones complete. R04 plan awaiting review, implementation not started.'}
    if rid == 'REC-0011': item['status'] = 'resolved'
    if rid == 'FACT-0062':
        item['resolution_notes'][-1]['current_report'] = identity(result_path)
        item['resolution_notes'][-1]['previous_report'] = old_result_identity
    updated.append(item)

patch = {key: copy.deepcopy(core[key]) for key in ['experiment','authority','environment','routes','owner_decision_ids','tooling']}
patch['experiment'].update(status='R03_complete_R04_plan_awaiting_review', design_accepted_scope='R02 closed with ecology inconclusive; R03 all five milestones complete, final Step5 accepted DEC-0039. R04 design/implementation not accepted or started. No active trial.')
patch['authority'].update(current_stage_record='DEC-0040', originals='Planning only: read-only source/installed metadata inspection; authorized derived KB maintenance.',
    next_action='Stop for R04 proposal review; no implementation start.', current_research_scope='Official Microsoft/Godot and original asset publisher documentation for R04 planning.')
patch['owner_decision_ids'].extend(['DEC-0039','DEC-0040'])
patch['proposal'] = {'id':'R04','record_id':'REC-0012','path':str(KB/'IMPROVEMENT_PROPOSAL_R04.md'),'status':'PROPOSED_AWAITING_OWNER_REVIEW','implementation_authorized':False,
    'scope':'VS2026/.NET10 recommendation; full3D design/model; implementation; shaders/lighting; WASD/mouse.',
    'basis_records':['DEC-0040','FACT-0063','CLAIM-0009'], 'limits':'Retain accepted views/RGB and passive observation. No simulation/ecology/food-policy change.'}
previous_part = copy.deepcopy(core['current_iteration_part'])
previous_part.update(status='OWNER_ACCEPTED_CLOSED', owner_acceptance_record='DEC-0039', limits='Accepted final R03 milestone. Dated verification retained; no reruns for closure.')
previous_part['record_ids'].append('DEC-0039')
patch['previous_iteration_part'] = previous_part
patch['current_iteration_part'] = {'id':'R04_PLAN','status':'PROPOSED_AWAITING_OWNER_REVIEW','record_ids':['DEC-0040','REC-0012','FACT-0063','CLAIM-0009'],
    'report':str(KB/'IMPROVEMENT_PROPOSAL_R04.md'),'verification':'Read-only metadata/source/documentation review; no builds or application execution.', 'next_action':'Owner review, then applicable start instruction for Step1.'}
patch['next_iteration'] = {'id':'R04','status':'PLAN_AWAITING_REVIEW','owner_outline_record':'DEC-0040','current_recommendation_record':'REC-0012',
    'plan_started':True,'implementation_started':False,'start_authorized':False,'next_action':'Review plan; implementation not authorized by this planning instruction.',
    'constraints':['VS2026 requested; net10 recommended pending review','Proportionate real3D mould visuals and examples','Full DNA RGB and retained accepted views','Step-by-step owner review; passive rendering']}
patch['completed_r03'] = {'status':'ALL_FIVE_PLANNED_MILESTONES_COMPLETE','final_step_acceptance':'DEC-0039','final_result':'FACT-0062','report':str(result_path),
    'prior_outline':'DEC-0021','limits':'No ecology outcome added by graphics acceptance.'}
env = patch['environment']
env.update(status='2026-09-08 metadata refreshed FACT-0063; VS2026/net10 not yet built or run.',
    refresh='Step1 must replace obsolete VS2022/SDK9.0.315 helper assumptions; exact old SDK directory absent.',
    current_toolchain={'ide':'VS2026 Community18.9.2','sdk':'10.0.400 installed; solution currentlynet9','runtime':'net10/WindowsDesktop10.0.11 installed','godot':'4.7.2 stable.NET'},
    migration_decision='DEC-0040 requests VS2026 future target; net10 is REC-0012 recommendation. Earlier VS2022 result/requirements are dated.')
env['record_ids'].append('FACT-0063')
env['sdk_for_vs2022'] = '9.0.315 historical build SDK; no longer present at recorded path'
patch['tooling']['knowledgebase_git_scope'] = 'Earlier baseHEAD14b39d984555897e88c279ed21651df7c114e05d and dirty-state note are historical. Current commit coverage not re-established in planning; no commit/push.'
for route in ['current_implementation','r03_step5_graphics']:
    patch['routes'][route]['record_ids'].insert(0,'DEC-0039')
patch['routes']['environment_capabilities']['record_ids'] = ['FACT-0063','DEC-0040','CLAIM-0009'] + patch['routes']['environment_capabilities']['record_ids']
patch['routes']['r03_closure'] = {'record_ids':['DEC-0039','FACT-0062','REC-0011','DEC-0036']}
patch['routes']['r04_plan'] = {'record_ids':['DEC-0040','REC-0012','FACT-0063','CLAIM-0009','DEC-0039','DEC-0027']}
merged_core = copy.deepcopy(core)
merged_core.update(patch)
core_estimate = (len(json.dumps(merged_core, ensure_ascii=False, separators=(',',':')).encode('utf-8')) + 2)//3
assert core_estimate < 6000, core_estimate
save('r04_plan_transaction.json', {'actor':ACTOR,'note':'Record owner closure of final R03 Step5 and R04 planning-only mandate; detailed proposal, environment refresh and primary-source research; stop for review.',
    'upsert_records':[dec39,dec40,fact63,claim9,rec12]+updated,'upsert_sources':source_upserts,'core_patch':patch})
print(json.dumps({'ok':True,'input_revision':62,'core_estimated_tokens':core_estimate,'new_records':5,'updated_records':3,'new_sources':8,'updated_sources':3,'transaction':'r04_plan_transaction.json'}))
