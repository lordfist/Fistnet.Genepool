"""Prepare derived R03 planning artifacts only; canonical changes use kb.py separately."""
import copy
import hashlib
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(r'D:\Posao\Fistnet.Genepool')
KB = ROOT / 'KnowledgeBase'
NOW = datetime.now(timezone.utc).isoformat()
core = json.loads((KB / 'core.json').read_text(encoding='utf-8'))
assert core['kb_revision'] == 44
catalog = json.loads((KB / 'sources.json').read_text(encoding='utf-8'))['sources']
records = {r['id']: r for r in map(json.loads, (KB / 'records.jsonl').read_text(encoding='utf-8').splitlines())}
added = []
next_source = max(int(s['source_id'].split('-')[1]) for s in catalog) + 1

def add_source(title, locator, coverage, **fields):
    global next_source
    item = dict(source_id=f'SRC-{next_source:04d}', title=title, locator=locator,
                observed_at_utc=NOW, coverage=coverage,
                origin={'actor': 'Genepool Analyzer', 'type': 'authorized_R03_Step2_planning'}, **fields)
    next_source += 1
    added.append(item)
    return item

def identity(path):
    data = path.read_bytes()
    return {'path': str(path), 'sha256': hashlib.sha256(data).hexdigest().upper(), 'size_bytes': len(data)}

local_rows = [
 ('global.json', 'Complete SDK pin.'),
 ('Fistnet.Genepool.sln', 'Complete solution and all configuration mappings.'),
 ('Fistnet.Genepool.Dna/Fistnet.Genepool.Dna.csproj', 'Complete library project.'),
 ('Fistnet.Genepool.Control/Fistnet.Genepool.Control.csproj', 'Complete library project.'),
 ('Fistnet.Genepool.App/Fistnet.Genepool.App.csproj', 'Current identity plus Step1 complete project inspection.'),
 ('Fistnet.Genepool.Visualization/Fistnet.Genepool.Visualization.csproj', 'Current identity plus Step1 complete project inspection.'),
 ('Fistnet.Genepool.Tests/Fistnet.Genepool.Tests.csproj', 'Current identity plus Step1 complete project inspection.'),
 ('Fistnet.Genepool.Control/SimulationRunner.cs', 'Complete 191-line worker; command methods, LatestFrame, Publish, shutdown and fault paths.'),
 ('Fistnet.Genepool.Control/ViewFrame.cs', 'Complete detached snapshot model and child records.'),
 ('Fistnet.Genepool.Control/SimulationRunOptions.cs', 'Complete options, defaults, modes, policy and validation.'),
 ('Fistnet.Genepool.App/NewSimulationDialog.cs', 'Complete settings dialog including LoadOptions and preservation in CreateOptions.'),
 ('Fistnet.Genepool.App/MainForm.Layout.cs', 'Complete layout; control labels, speed choices, timer and panels.'),
 ('Fistnet.Genepool.App/MainForm.cs', 'Targeted display/commands/selection/settings; transient architecture helper reviewed UI integration.'),
 ('Fistnet.Genepool.Visualization/BoardView.cs', 'Step1 complete read plus Step2 targeted palette, picking and display-layer review; helper confirms mapping.'),
 ('Fistnet.Genepool.Control/ViewCollector.cs', 'Targeted collection/publication and detached data contract; transient architecture review.'),
 ('Fistnet.Genepool.Tests/ViewerBackendTests.cs', 'Transient architecture helper inspected existing viewer tests; root reuses current identity and prior bounded verification only.'),
 ('.gitignore', 'Complete current exclusions; planned tools/cache exclusions absent.')
]
local = []
for relative, coverage in local_rows:
    p = ROOT / relative
    ident = identity(p)
    matches = [s for s in catalog if s.get('locator', {}).get('path', '').casefold() == str(p).casefold() and s.get('sha256') == ident['sha256']]
    if not matches:
        assert relative == '.gitignore', f'Unexpected unregistered identity: {relative}'
        found = add_source(relative, {'type': 'file', 'path': str(p)}, coverage,
                           sha256=ident['sha256'], size_bytes=ident['size_bytes'])
    else:
        found = matches[-1]
    local.append(dict(ident, source_id=found['source_id'], coverage=coverage, current_bytes_match_registered_identity=bool(matches)))

public_rows = [
 ('Godot 4.7.2 release archive', 'https://godotengine.org/download/archive/4.7.2-stable/', 'Official release selection and .NET edition; retrieved 2026-09-07.'),
 ('Godot 4.7.2 official release assets', 'https://github.com/godotengine/godot-builds/releases/expanded_assets/4.7.2-stable', 'Transient dependency helper read exact editor/template names, sizes and publisher SHA256 metadata; no archive downloaded.'),
 ('Godot.NET.Sdk 4.7.2 package', 'https://www.nuget.org/packages/Godot.NET.Sdk/4.7.2', 'Exact SDK version, package identity and publication metadata.'),
 ('Godot 4.7.2 SDK configurations', 'https://github.com/godotengine/godot/blob/4.7.2-stable/modules/mono/editor/Godot.NET.Sdk/Godot.NET.Sdk/Sdk/Sdk.props', 'Versioned source via raw equivalent, complete SDK props: configurations, platform and output paths.'),
 ('Godot 4.7.2 implicit C# packages', 'https://github.com/godotengine/godot/blob/4.7.2-stable/modules/mono/editor/Godot.NET.Sdk/Godot.NET.Sdk/Sdk/Sdk.targets', 'Versioned source via raw equivalent, complete SDK targets: implicit bindings/generator and editor-only package.'),
 ('Godot 4.7.2 package version generation', 'https://github.com/godotengine/godot/blob/4.7.2-stable/modules/mono/build_scripts/build_assemblies.py', 'Dependency helper inspected generated SdkPackageVersions.props construction; package version uses stable engine major.minor.patch.'),
 ('Godot 4.7.2 project framework checks', 'https://github.com/godotengine/godot/blob/4.7.2-stable/modules/mono/editor/GodotTools/GodotTools.ProjectEditor/ProjectUtils.cs', 'Dependency helper inspected minimum framework comparison using NuGetFramework.Version; net9 Windows target compatibility is static inference.'),
 ('Godot 4.7.2 solution discovery', 'https://github.com/godotengine/godot/blob/4.7.2-stable/modules/mono/editor/GodotTools/GodotTools/Internals/GodotSharpDirs.cs', 'Complete versioned source via raw equivalent; assembly project path and parent-directory matching solution scan.'),
 ('Microsoft SDK and Visual Studio version matrix', 'https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs', 'SDK9.0.3xx primary VS17.14 and net9 minimum VS17.12; dependency helper primary-source check.'),
 ('Godot C# and Visual Studio setup', 'https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html', 'C# prerequisites, VS2022 desktop workload and executable debug launch workflow.'),
 ('MSBuild ProjectReference metadata', 'https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items?view=vs-2022', 'ProjectReference SetConfiguration and SetPlatform; source supports proposed explicit export-library mapping.'),
 ('Godot portable editor data', 'https://docs.godotengine.org/en/4.7/tutorials/io/data_paths.html', 'Self-contained editor marker and editor_data; no claim that it applies to exported projects.'),
 ('Godot GPU requirements', 'https://docs.godotengine.org/en/4.7/about/system_requirements.html', 'Windows editor minimums and Compatibility OpenGL3.3; local GPU/driver unknown.'),
 ('Godot renderer selection', 'https://docs.godotengine.org/en/4.7/tutorials/rendering/renderers.html', 'Compatibility renderer suitability guidance; no measured local performance.'),
 ('Godot MultiMeshInstance2D API', 'https://docs.godotengine.org/en/stable/classes/class_multimeshinstance2d.html', 'Instanced 2D rendering interface.'),
 ('Godot MultiMesh API', 'https://docs.godotengine.org/en/stable/classes/class_multimesh.html', 'Transform2D, colors, instance allocation, Buffer and VisibleInstanceCount.'),
 ('Godot bulk instance buffer layout', 'https://docs.godotengine.org/en/stable/classes/class_renderingserver.html', 'multimesh_set_buffer section: 2D transforms and optional per-instance color; main-thread bulk upload proposed.'),
 ('Godot scene threading constraints', 'https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html', 'Scene/resource ownership constraints supporting main-thread Godot updates.'),
 ('Godot command line', 'https://docs.godotengine.org/en/4.7/tutorials/editor/command_line_tutorial.html', 'Dependency helper checked editor/run/path/import/export forms; future commands not executed.'),
 ('Godot export workflow', 'https://docs.godotengine.org/en/4.7/tutorials/export/exporting_projects.html', 'Matching templates, platform selection, preset and output-directory requirements; packaging deferred.'),
 ('Godot 4.7.2 managed publish', 'https://github.com/godotengine/godot/blob/4.7.2-stable/modules/mono/editor/GodotTools/GodotTools/Build/BuildSystem.cs', 'Dependency helper checked ExportRelease/project/win-x64/self-contained publish arguments; no literal TFM override.')
]
public = []
for title, url, coverage in public_rows:
    existing = [s for s in catalog if s.get('locator', {}).get('url') == url]
    src = existing[-1] if existing else add_source(title, {'type': 'url', 'url': url}, coverage)
    public.append({'source_id': src['source_id'], 'title': title, 'url': url, 'coverage': coverage, 'retrieval_date': '2026-09-07'})

git = r'C:\Program Files\Git\cmd\git.exe'
head = subprocess.check_output([git, '-C', str(ROOT), 'rev-parse', 'HEAD'], text=True).strip()
status = subprocess.check_output([git, '-C', str(ROOT), 'status', '--short'], text=True).splitlines()
assert head == 'bdfaa0c555edf8807f680f46b71e0ef16e387d07'
assert all(line[3:].startswith('KnowledgeBase/') for line in status), status
evidence = {
 'actor': 'Genepool Analyzer', 'recorded_at_utc': NOW, 'scope': 'Godot selected; Step2 planning only. No application edits, downloads, installation, build, sample execution or trial.',
 'local_head': head, 'git_status': status, 'local_sources': local, 'public_sources': public,
 'derived_support': [dict(identity(KB / 'tools/build_solution.py'), coverage='Complete helper inspection: fixed SDK/MSBuild, cleared NuGet sources, disposable caches and input inventory; no modification.')],
 'installed_metadata': {'dotnet_sdk': '9.0.315 MSBuild file present, product17.14.43', 'netcore_runtime': '9.0.17 core library present', 'visual_studio_ide': 'VS2022 Community executable product17.14.37411.7', 'visual_studio_msbuild': 'amd64 MSBuild product17.14.40', 'method': 'File existence and version metadata, not a new capability execution.', 'godot_lookup': 'No command under godot/godot4 in checked PATH; not exhaustive installation census.', 'gpu': 'Unknown; earlier adapter query AccessDenied. Actual renderer must be verified during authorized implementation.'},
 'omissions': 'No archive extraction/import, local engine build/run/export or GPU measurement. No private IDE/account settings or reserved ecological evidence. URLs are locators, not archived immutable bytes.',
 'attribution': 'Root canonical writer and plan author; transient dependency and static architecture helpers supplied independently reviewed technical findings.'
}
(KB / 'r03_step2_evidence.json').write_text(json.dumps(evidence, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
arts = []
for name in ['R03_GODOT_2D_IMPLEMENTATION_PLAN.md', 'r03_step2_evidence.json']:
    ident = identity(KB / name)
    arts.append({'path': ident['path'], 'sha256': ident['sha256'], 'bytes': ident['size_bytes'], 'status': 'Derived Step2 plan/evidence; not implemented or owner accepted.'})
local_ids = [s['source_id'] for s in local]
public_ids = [s['source_id'] for s in public]

def rec(id, kind, status, statement, refs, related, uncertainty):
    return {'id': id, 'kind': kind, 'status': status, 'statement': statement, 'source_refs': refs,
            'related_record_ids': related, 'tags': ['R03', 'Step2', 'Godot', 'VS2022'],
            'origin': {'actor': 'Genepool Analyzer', 'type': 'authorized_R03_Step2_planning', 'recorded_at_utc': NOW},
            'uncertainty': uncertainty, 'derivation': {'artifacts': copy.deepcopy(arts), 'locators': ['Plan numbered dependencies and implementation checkpoints; evidence.local_sources/public_sources/installed_metadata.']}}

decision = rec('DEC-0024', 'owner_decision', 'active',
 'Owner selects Godot and authorizes R03 Step2 exact planning for libraries/installations, where to obtain them, and steps for the 2D viewer. Stop for owner review before actual implementation.', [], ['DEC-0022', 'DEC-0023', 'REC-0008', 'REC-0009'],
 'Godot choice is accepted. The detailed plan, installation footprint, metrics and implementation are not yet accepted or authorized. R02 closure is unchanged.')
decision['origin'] = {'actor': 'Genepool Analyzer', 'type': 'direct_user_message', 'recorded_at_utc': NOW,
 'quote': 'Ok lets pick Godot! But before we start the actual implementation I want the exact plan on: 1) What we need from libraries and installations 2) Where to get them 3) What are the steps for building the actual 2D visualisation using Godot. When you have the exact plan on how we do this stop and i will review it.'}
fact = rec('FACT-0048', 'observed_fact', 'active',
 'R03 Step2 static inspection supports reusing one existing SimulationRunner and detached LatestFrame in a new Godot application. Control references Dna; library projects target net9.0-windows7.0. Root SDK pin is9.0.315; installed SDK/runtime/VS2022 metadata is present. Existing build helper clears NuGet sources and uses disposable package caches, so it needs a scoped package-resolution adaptation for the proposed Godot SDK. Current source identities match catalog; no simulation source changes or new execution.', local_ids, ['FACT-0047', 'FACT-0046', 'DEC-0024'],
 'Direct Godot project integration, package restore, effective SDK/configuration mapping, F5 and actual GPU remain untested. Previous137/137 tests are dated Part4 evidence, not rerun in planning.')
claim = rec('CLAIM-0008', 'source_claim', 'active',
 'Official Godot4.7.2 release/package/source documentation supports a portable .NET Windows x64 editor, matching4.7.2 SDK bindings, Debug/ExportDebug/ExportRelease configurations, parent-solution discovery, bulk2D instancing and separate export templates. Microsoft documents SDK9.0.3xx with VS2022 net9 support. Versioned Godot framework checks support the proposed Windows-qualified net9 target by static inference.', public_ids, ['DEC-0024', 'REC-0009', 'CLAIM-0006'],
 'Publisher/source claims and static compatibility inference, not project measurements. Asset sizes/digests are published metadata without local binary verification. Stable documentation may evolve; versioned engine links pin implementation claims.')
recommendation = rec('REC-0009', 'recommendation', 'provisional',
 'R03 Step2 plan: portable Godot.NET4.7.2 plus implicit NuGet SDK packages; retain SDK9.0.315 and whole VS2022 solution. Add a complete Godot viewer owning the existing simulation worker, two GPU batches for food/organisms, camera and control/details parity. First verify toolchain/GPU/F5, then frozen frames, live integration, parity and bounded tests/performance. Defer standalone export templates,3D and graphics polish. Plan complete awaiting owner review; no implementation started.', local_ids + public_ids, ['DEC-0024', 'FACT-0048', 'CLAIM-0008', 'REC-0008', 'DEC-0020'],
 'Architecture and timing criteria are proposals; integration and actual GPU capability remain checkpoint risks. Keeping WinForms is the proposed migration/comparison path, not an owner requirement to embed Godot.')
old = copy.deepcopy(records['REC-0008'])
old.setdefault('resolution_notes', []).append({'at_utc': NOW, 'actor': 'Genepool Analyzer', 'type': 'owner_engine_selection_and_Step2_plan', 'prior_statement': old['statement'], 'note': 'Godot selected by DEC-0024. Current detailed plan is REC-0009; its acceptance/implementation remain pending. Earlier research comparisons retain dated provenance.', 'record_ids': ['DEC-0024', 'REC-0009']})
old['statement'] = 'Godot C# rendering recommendation was accepted as engine choice by the owner in DEC-0024. Current R03 Step2 dependency and 2D implementation plan is REC-0009, awaiting owner review. Earlier Skia/Godot tradeoff and uncertainty remain dated analysis; engine acceptance does not accept the detailed design or authorize installation/implementation.'
old['status'] = 'resolved'
old['related_record_ids'] += ['DEC-0024', 'REC-0009']
old['derivation']['next_action'] = 'Current route: REC-0009 and R03_GODOT_2D_IMPLEMENTATION_PLAN.md; stop for Step2 plan review.'

patch = {k: copy.deepcopy(core[k]) for k in ['experiment', 'owner_decision_ids', 'routes', 'authority', 'proposal', 'environment', 'tooling', 'current_iteration_part', 'next_iteration']}
patch['experiment']['status'] = 'R03_Step2_plan_complete_awaiting_owner_review'
patch['experiment']['design_accepted_scope'] += ' Godot engine selected by DEC-0024; R03 detailed plan remains pending.'
patch['owner_decision_ids'].append('DEC-0024')
patch['routes']['r03_implementation_plan'] = {'record_ids': ['DEC-0024', 'FACT-0048', 'CLAIM-0008', 'REC-0009', 'REC-0008']}
patch['routes']['r03_research']['record_ids'] += ['DEC-0024', 'REC-0009']
patch['authority'].update(originals='Step2 static inspection; simulation sources preserved. Registered source identities checked; existing SRC-0115 historical gap persists.', current_stage_record='DEC-0024', next_action='Stop for owner review of Step2 Godot plan; await explicit bounded setup/implementation instruction.', current_research_scope='R03 Step2 exact dependencies, official sources and local integration plan complete; no download, installation, implementation, build, run or trial.')
patch['proposal'] = {'id': 'R03_Step2', 'record_id': 'REC-0009', 'path': arts[0]['path'], 'sha256': arts[0]['sha256'], 'status': 'PLAN_COMPLETE_AWAITING_OWNER_REVIEW', 'engine': 'Godot C#', 'engine_selection_record': 'DEC-0024', 'implementation_authorized': False, 'research_authorization_record': 'DEC-0024', 'selection_criterion_record': 'DEC-0023', 'result_record_ids': ['FACT-0048', 'CLAIM-0008'], 'source_commit_horizon': head, 'evidence_manifest': arts[1]['path'], 'limits': 'Plan/source verification only. No Godot installation, import, build, launch, export or GPU measurement.'}
patch['environment']['record_ids'].append('FACT-0048')
patch['environment']['r03_gpu_research'] = 'Godot4.7.2 chosen. Existing SDK9.0.315, runtime9.0.17 and VS2022 17.14 file metadata refreshed. GPU/driver and candidate integration unverified; first implementation checkpoint.'
patch['tooling']['current_maintenance_authority'] = 'DEC-0024'
patch['tooling']['current_verification_receipt'] = str(KB / 'r03_step2_receipt.json')
patch['current_iteration_part'] = {'id': 'R03_Step2', 'status': 'PLAN_COMPLETE; OWNER_REVIEW_PENDING', 'record_ids': ['DEC-0024', 'FACT-0048', 'CLAIM-0008', 'REC-0009'], 'report': arts[0]['path'], 'verification': 'Static code and primary-source review; KB deep validation in r03_step2_receipt.json. No implementation execution.', 'limits': 'Godot engine accepted; detailed plan pending. Stop before install/implementation.'}
patch['next_iteration'].update(status='R03_STEP2_PLAN_COMPLETE_REVIEW_PENDING', plan_started=True, implementation_started=False, next_action='Review Godot Step2 plan; await bounded setup/2D implementation authorization.', start_scope='Step2 planning only by DEC-0024; Step1 research completed.', current_recommendation_record='REC-0009', engine_selection_record='DEC-0024')
txn = {'actor': 'Genepool Analyzer', 'note': 'Record owner Godot selection, exact Step2 setup/2D plan and evidence; stop before implementation.', 'upsert_records': [decision, fact, claim, recommendation, old], 'upsert_sources': added, 'core_patch': patch}
(KB / 'r03_step2_transaction.json').write_text(json.dumps(txn, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
print(json.dumps({'revision': core['kb_revision'], 'local_sources': len(local), 'public_sources': len(public), 'new_sources': len(added), 'records_added': 4, 'records_updated': 1, 'artifacts': arts}, indent=2))
