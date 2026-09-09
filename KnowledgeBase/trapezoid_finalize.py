"""Prepare this bounded visual refinement report and KB transaction; never apply."""
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
def read(name): return json.loads((KB / name).read_text(encoding='utf-8-sig'))
def save(name, value): (KB / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
def identity(path):
    data = path.read_bytes()
    return {'path': str(path), 'sha256': hashlib.sha256(data).hexdigest().upper(), 'bytes': len(data)}

core = read('core.json')
assert core['kb_revision'] == 58
records = {r['id']: r for r in map(json.loads, (KB / 'records.jsonl').read_text(encoding='utf-8').splitlines())}
before = read('r03_trapezoid_predecessors.json')
now = dt.datetime.now(dt.timezone.utc).isoformat()
head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
assert head == '14b39d984555897e88c279ed21651df7c114e05d'
names = ['r03_step4_verification.json', 'r03_step4_headless.json', 'r03_godot_verification.json', 'r03_step4_benchmark.json']
gpu, headless, smoke, bench = results = [read(n) for n in names]
assert all(r['ok'] and r['process']['exitCode'] == 0 and not r['process']['engineErrors'] and all(c['passed'] for c in r['checks']) for r in results)
assert [len(r['checks']) for r in results] == [185, 149, 62, 12]
builds = read('r03_trapezoid_builds.json')
for configuration in ['debug', 'release']:
    stage = builds['stages']['trapezoid-' + configuration]
    assert stage['ok'] and stage['scratch_removed']
    for path, sha in stage['source_sha256'].items():
        assert identity(ROOT / path)['sha256'] == sha.upper(), path
builds['actor'] = 'Genepool Analyzer'
save('r03_trapezoid_builds.json', builds)
reused = []
for configuration in ['Debug', 'Release']:
    report_name = 'r03_step4_tests_' + configuration.lower() + '.json'
    report = read(report_name)
    assert report['failed'] == 0 and report['total'] == report['passed'] == 159
    for assembly, sha in report['assemblySha256'].items():
        path = ROOT / assembly / 'bin' / configuration / 'net9.0-windows7.0' / (assembly + '.dll')
        assert identity(path)['sha256'] == sha, (configuration, assembly)
    reused.append({'configuration': configuration, 'report': report_name, 'tests': 159,
                   'all_five_current_assembly_hashes_match': True, 'hashes': report['assemblySha256'],
                   'execution': 'Prior passing evidence reused by exact rebuilt binary comparison; not rerun for this Godot-only change.'})

source_upserts, source_ids = [], []
for number, item in enumerate(before, 289):
    assert identity(Path(item['history']))['sha256'] == item['sha256']
    assert identity(Path(item['path']))['sha256'] != item['sha256'], 'Unused safeguard must be reconciled'
    sid = f'SRC-{number:04d}'
    result = subprocess.run([sys.executable, '-X', 'utf8', '-B', str(KB / 'tools/kb.py'), 'revise-source', item['source_id'], sid,
        '--expect-revision', '58', '--actor', 'Genepool Analyzer', '--reason', 'Owner-requested centred trapezoid and neutral Organisms ground',
        '--record-id', 'DEC-0035', '--record-id', 'FACT-0060', '--history-file', item['history'], '--compact'],
        cwd=ROOT, capture_output=True, text=True, encoding='utf-8', check=True)
    versions = json.loads(result.stdout)['transaction']['upsert_sources']
    versions[1]['title'] = str(Path(item['path']).relative_to(ROOT))
    versions[1]['coverage'] = 'Bounded camera/grey-ground refinement inspected and verified; relevant locators and limits in FACT-0060. Exact working bytes hashed.'
    source_upserts.extend(versions)
    source_ids.append(sid)

table = '\n'.join(f"| {m['population']:,} | {m['detailLevel']} | {m['averageFps']:.2f} | {m['p95FrameMilliseconds']:.2f} ms | {m['maxFrameMilliseconds']:.2f} ms |" for m in bench['measurements'])
report = f'''# R03 Step 4 — Trapezoid and Organisms ground refinement

Genepool Analyzer · {now} · **Implemented and verified; awaiting owner review.**

The owner requested light grey ground for the Organisms filter and supplied a red trapezoid reference showing a centred board with level back/front edges. This refines the earlier modest-diagonal direction in DEC-0034. The earlier brown-ground report remains resolved as a filter selection, not a reproduced restart bug (FACT-0059).

The Habitat angled camera now faces squarely across the ground: yaw 0°, elevation 34°, fitted vertical field of view 25.5°. Actual native-camera measurements at 1366×768 and 1920×1080 show front/back width ratios 1.631 and 1.641, and projected height/front-width approximately 0.45. Both edges are horizontal and horizontally centred. The geometry/navigation implementation and depth safeguards are retained.

Organisms-only Habitat ground is neutral light grey, including its minimap. Food and grass are hidden in that layer. Combined/Food retain the actual brown-to-green food display. The minimap uses a dark viewport outline and a dark halo around the selected point so navigation remains visible on grey. The earlier Original 2D renderer keeps its existing appearance.

These are actual GPU captures of the same detached 1,800-organism synthetic fixture and camera, not mockups or ecological results. Fixture controls are disabled because no simulation is attached; normal startup has live controls.

![Combined trapezoid](r03_perspective_world.png)

![Organisms-only grey ground](r03_perspective_organisms.png)

[Angled Habitat detail](r03_perspective_detail.png) · [Top-down comparison](r03_perspective_topdown.png)

Visual inspection found the intended shape and distinct grey ground with intact colonies and navigation. Pastel overview dots have lower contrast on grey, especially in the far rows; they remain discernible. The palette was retained, and visual acceptance remains the owner's decision.

## Verification

- **Whole solution built successfully with Visual Studio 2022 MSBuild in Debug and Release:** zero errors; the same 106 existing CA1416 warnings. No SDK, project, solution or dependency changes.
- **185 GPU checks passed**, including independent native-camera shape/picking/zoom/drag/clipping/culling, action anchors, colony body coverage, both sizes and actual grey pixels in angled/top-down. The same cells remain grey when food quantities swap; switching back to Combined restores brown/green. View switches preserve paused simulation state and RNG.
- **149 headless checks and 62 retained Original 2D/shared-control GPU checks passed.** All owned verification processes exited successfully without engine errors; live test workers shut down.
- Prior **159 console checks in each configuration** are reused after all five rebuilt non-Godot binary hashes match the passing reports exactly; those suites were not rerun for this visual-only refinement.
- **One renderer benchmark attempt, 12/12 checks passed**, on {bench['adapter']}. Each case used 3 seconds warmup and 7 seconds measurement at 1920×1080, Compatibility renderer, 60 FPS cap, with all 70 requested 10 Hz snapshots delivered.

| Population | Detail level | Mean FPS | p95 frame interval | Maximum interval |
| --- | --- | --- | --- | --- |
{table}

These are main-loop observations of detached frames; they exclude simulation calculation and physical presentation latency. The new close-view footprint has 258 visible cells/cues, versus 393 in the previous camera measurement. This is bounded current performance evidence, not an equal-workload speedup claim or a guarantee of constant frame timing. Prior perspective failures/interventions remain in FACT-0058 and R03_STEP4_PERSPECTIVE_RESULT.md; no failed build/test/benchmark occurred in this refinement.

## Scope and evidence

Six current registered sources changed: camera pose, board ground colour, minimap, camera/pixel verification, one explanatory test comment, and viewer README. Their exact uncommitted predecessors were preserved as minimal KB source snapshots. No simulation source, rule, RNG, food cycle, tools or governance changed. Existing uncommitted Step 4 work was retained. Base Git HEAD: {head}; no commit or push performed.

Current report/capture files are deliberately replaced by the verification runner; dated earlier record assertions remain historical. FACT-0060 holds current source/artifact hashes and result locators. The KB transaction is revision/hash-bound and deep validation follows apply. The longstanding unavailable exact SRC-0115 historical bytes remain an explicitly disclosed warning, unrelated to this correction.

Open Fistnet.Genepool.sln in Visual Studio 2022, rebuild Debug and launch the Godot viewer. Compare Habitat angled at Fit, then Organisms and Combined, and zoom/drag/select. IDE breakpoint interaction and physical monitor/DPI transitions were not newly tested. Stop for owner review; Step 5, exports, food-cycle changes and ecology remain deferred.
'''
(KB / 'R03_STEP4_TRAPEZOID_RESULT.md').write_text(report, encoding='utf-8')
artifact_names = names + ['r03_trapezoid_builds.json', 'R03_STEP4_TRAPEZOID_RESULT.md', 'r03_perspective_world.png',
    'r03_perspective_organisms.png', 'r03_perspective_detail.png', 'r03_perspective_topdown.png',
    'r03_step4_habitat.png', 'r03_step4_organism.png', 'r03_step4_inspect.png']
artifacts = [identity(KB / name) for name in artifact_names]
binaries = [identity(ROOT / 'Fistnet.Genepool.Godot/.godot/mono/temp/bin' / config / 'Fistnet.Genepool.Godot.dll') for config in ['Debug', 'ExportRelease']]
decision = {'id': 'DEC-0035', 'kind': 'owner_decision', 'status': 'active',
 'statement': 'Owner requests light grey ground for the Organisms filter to distinguish hidden food from brown soil, and a centred trapezoid camera matching the attached red reference. The reference refines the earlier diagonal orientation. Bounded visual refinement authorized; result acceptance remains pending.',
 'source_refs': [], 'related_record_ids': ['DEC-0034', 'FACT-0059', 'FACT-0060'],
 'origin': {'type': 'direct_owner_message_and_visual_reference', 'actor': 'User', 'recorded_by': 'Genepool Analyzer', 'recorded_at_utc': now,
 'quote': 'I think the organism filter should have light gray board so you can clearly distinguish it from food floor board. ... my idea is red trapezoid as board, your current implementation is more like rotated square',
 'quote_scope': 'Short excerpts; quotation marks/punctuation normalized. Screenshot red outline supports level centred near/far edges.',
 'attachment': 'codex-clipboard-40b9cc67-4e36-4b5a-87aa-aec464175a9e.png'}}
fact = {'id': 'FACT-0060', 'kind': 'observed_fact', 'status': 'active',
 'statement': 'R03 Step4 visual refinement implemented and verified: centred level-edged trapezoid (yaw0/elevation34/FOV25.5; measured front/back1.631–1.641), light neutral grey Organisms ground and matching minimap. VS2022 Debug/Release whole builds,185GPU149headless62retained2D checks pass; one12/12 benchmark passes near60FPS with all10Hz frames delivered. Prior159consolechecks/config reused by exact rebuilt binary identities.',
 'source_refs': source_ids, 'related_record_ids': ['DEC-0035', 'FACT-0058', 'FACT-0059', 'DEC-0034'],
 'tags': ['R03', 'step4', 'trapezoid', 'perspective', 'ground', 'grey', 'food', 'Organisms', 'Godot'],
 'origin': {'type': 'authorized_implementation_and_verification', 'actor': 'Genepool Analyzer', 'recorded_at_utc': now,
 'contributors': 'Transient helpers: camera pose, meaningful tests, independent code/screenshot review; root integration, execution and sole canonical KB writer.'},
 'uncertainty': 'Owner visual acceptance pending. Pastel far-row dots have lower contrast on grey. No fixedFPS or ecological claim.106existingCA1416warnings; IDEdebugger and physicalDPI unverified. HistoricalSRC0115 exact-byte gap remains. No Step5/foodcycle/exports/newtrial.',
 'derivation': {'source_horizon_utc': now, 'base_commit': head, 'artifacts': artifacts, 'godot_binaries': binaries,
 'reused_console_evidence': reused, 'gpu_measurements': gpu['measurements'], 'benchmark_measurements': bench['measurements'],
 'geometry_checks': [c for c in gpu['checks'] if 'trapezoid' in c['name']],
 'verification_attempts': 'One successful attempt per Debug/Release build, GPU/headless/original2D suite and renderer benchmark. No failed verification or runtime intervention.',
 'locators': ['BoardView3D.Projection.cs: AngledYawDegrees/AngledElevationDegrees/FitFieldOfViewDegrees',
 'BoardView3D.cs: OrganismsGroundColor/UploadFrame', 'MinimapView.cs: Upload/_Draw',
 'VerificationRunner.Perspective.cs: PerspectiveChecks/OrganismsGroundPixelChecks/PerspectivePreviews'],
 'scope': 'Six bounded Godot source/document edits, derived KB updates and existing-tool verification only; no simulation rule/source or RNG change.'}}
updates = [decision, fact]
for rid in ['DEC-0034', 'FACT-0058', 'FACT-0059']:
    old = copy.deepcopy(records[rid])
    note = {'at_utc': now, 'actor': 'Genepool Analyzer', 'record_ids': ['DEC-0035', 'FACT-0060'],
        'note': 'Owner refined camera to centred level-edged trapezoid and requested grey Organisms ground; implemented/verified in FACT-0060. Earlier direction, brown-filter clarification and measured result remain dated history. Current reports/captures replaced; owner result review pending.'}
    old.setdefault('resolution_notes', []).append(note)
    old['current_resolution'] = {'statement': note['note'], 'record_ids': ['FACT-0060', 'DEC-0035']}
    updates.append(old)
patch = {key: copy.deepcopy(core[key]) for key in ['authority', 'proposal', 'current_iteration_part', 'routes', 'owner_decision_ids', 'environment', 'next_iteration']}
patch['authority'].update(current_stage_record='DEC-0035', next_action='Stop for owner review of Step4 trapezoid/grey-ground refinement (FACT-0060).')
patch['proposal'].update(result_record='FACT-0060', result_path=str(KB / 'R03_STEP4_TRAPEZOID_RESULT.md'))
patch['current_iteration_part'].update(record_ids=['DEC-0035', 'FACT-0060', 'DEC-0033', 'FACT-0055', 'FACT-0056', 'REC-0010'],
 report=str(KB / 'R03_STEP4_TRAPEZOID_RESULT.md'), verification='VS2022 Debug/Release builds;185GPU149headless62retained2D pass;159console/config reused by exactbinarymatch. One benchmark12/12 passes.',
 review_status='Trapezoid and grey ground implemented/verified underDEC0035; owner visual review pending.')
patch['environment']['status'] = 'VS2022 Debug/Release builds;185GPU149headless62original2D;159console/config reused by exactbinarymatch.'
patch['environment']['r03_gpu_research'] = 'FACT0060: one12/12benchmark,near60FPS,p95~16.7ms,complete10Hz. Detachedframes exclude simulation; nofixedFPSclaim.'
patch['environment']['record_ids'] = list(dict.fromkeys(patch['environment']['record_ids'] + ['FACT-0060']))
patch['next_iteration']['next_action'] = 'Stop for owner review of Step4 trapezoid/grey ground; Step5 needs next instruction.'
for route in ['current_implementation', 'r03_step4_design', 'r03_step4_result', 'ground_food_colours']:
    patch['routes'][route]['record_ids'] = ['FACT-0060', 'DEC-0035'] + patch['routes'][route]['record_ids']
patch['owner_decision_ids'].append('DEC-0035')
patch['last_maintenance'] = {'at': now, 'actor': 'Genepool Analyzer', 'note': 'Record requested trapezoid and grey ground, current verification and exact source lineage; stop for review.'}
save('r03_trapezoid_transaction.json', {'actor': 'Genepool Analyzer', 'note': 'Complete bounded owner-requested camera and ground refinement; retain history and stop for review.',
 'upsert_records': updates, 'upsert_sources': source_upserts, 'core_patch': patch})
print(json.dumps({'ok': True, 'input_revision': 58, 'new_sources': source_ids, 'new_records': ['DEC-0035', 'FACT-0060'],
 'updated_records': ['DEC-0034', 'FACT-0058', 'FACT-0059'], 'registered_predecessors': len(before), 'core_unchanged': read('core.json')['kb_revision'] == 58}))
