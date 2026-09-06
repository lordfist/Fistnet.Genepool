"""Seed-derived one-shot Part2 report/transaction preparation; no canonical writes."""
from pathlib import Path
import copy
import datetime
import hashlib
import json

KB = Path(__file__).resolve().parent
ROOT = KB.parent
now = datetime.datetime.now(datetime.timezone.utc).isoformat()
core = json.loads((KB / 'core.json').read_text())
records = {r['id']: r for r in map(json.loads, (KB / 'records.jsonl').read_text().splitlines())}
draft = json.loads((KB / 'r02_part2_source_draft.json').read_text())
results = json.loads((KB / 'r02_part2_results.json').read_text())
builds = json.loads((KB / 'r02_part2_builds.json').read_text())
assert core['kb_revision'] == draft['revision'] == 25
assert len(results['checks']['production-baseline']['runs']) == 4
assert 'ok' in results['checks']['production-baseline']

def identity(path, status):
    path = Path(path)
    return dict(path=str(path), sha256=hashlib.sha256(path.read_bytes()).hexdigest().upper(),
                bytes=path.stat().st_size, status=status)

mapping = draft['path_to_source_id']
for source in draft['upsert_sources']:
    if source.get('supersession'): continue
    assert hashlib.sha256(Path(source['locator']['path']).read_bytes()).hexdigest().upper() == source['sha256']
verification = {'actor': 'Seed Analyzer', 'at_utc': now, 'builds_and_tests': [], 'reference_passivity': [],
    'production': [], 'part1_preserved': [], 'scope': 'Part2 bounded mechanics/policy implementation; no ecological acceptance, Parts3-4 or owner acceptance.'}
for config in ('debug', 'release'):
    build = builds['stages']['part2-final-' + config]
    assert build['ok'] and build['scratch_removed']
    for path, sha in build['source_sha256'].items():
        assert hashlib.sha256((ROOT / path).read_bytes()).hexdigest() == sha
    run = results['checks']['final-' + config]['runs'][0]
    suite = run['records'][-1]
    assert run['exit_code'] == 0 and suite['total'] == suite['passed'] == 91 and suite['failed'] == 0 and run['scratch_removed']
    for name, sha in suite['assemblySha256'].items():
        file = ROOT / 'Fistnet.Genepool.Tests' / 'bin' / config.capitalize() / 'net9.0-windows7.0' / (name + '.dll')
        assert hashlib.sha256(file.read_bytes()).hexdigest().upper() == sha.upper()
    verification['builds_and_tests'].append({'configuration': config, 'source_inputs_verified': len(build['source_sha256']),
        'loaded_assemblies_verified': 5, 'tests': 91, 'passed': 91, 'seconds': suite['seconds'], 'warning_codes': build['builds'][-1]['warning_codes']})
for seed in (11, 29):
    rows = [r['records'][-1] for r in results['checks']['reference-baseline']['runs'] if r['name'].startswith(f'reference-{seed}-')]
    assert len(rows) == 2 and all(r['complete'] for r in rows)
    assert rows[0]['random'] == rows[1]['random'] and rows[0]['metrics'] == rows[1]['metrics']
    verification['reference_passivity'].append({'seed': seed, 'seasons': 128, 'state_draws_metrics_equal': True, **rows[0]['random']})
for row in results['checks']['production-baseline']['runs']:
    first, final = row['records'][0], row['records'][-1]
    assert row['scratch_removed'] and final['kind'] == 'final'
    diagnostics = final.get('diagnostics', {})
    timing = diagnostics.get('TimingsTicks', {})
    frequency = diagnostics.get('StopwatchFrequency', 1)
    verification['production'].append({'seed': final['options']['Seed'], 'initial_population': first['metrics']['population'],
        'final_population': final['metrics']['population'], 'completed_seasons': final['completedSeasons'],
        'requested_seasons': final['requestedSeasons'], 'complete': final['complete'], 'reason': final['reason'],
        'wall_seconds': row['wall_seconds'], 'maximum_age_batch_seconds': max(s['EngineCallSeconds'] for s in final['batchSamples']),
        'checkpoints': [{'season': x['completedSeasons'], 'population': x['metrics']['population']} for x in row['records'] if x['kind'] == 'checkpoint'],
        'final_metrics': final['metrics'], 'phase_seconds': {key: value / frequency for key, value in timing.items() if key.startswith('phase.')},
        'births': diagnostics.get('Totals', {}).get('birth.placed', 0),
        'deaths': diagnostics.get('Totals', {}).get('deaths.total', 0),
        'limitations': 'Changed model and occupancy, enabled diagnostics and no discarded warmup. Timing is descriptive, not a matched-workload speedup claim.'})
for item in core['current_iteration_part']['artifacts']:
    actual = identity(item['path'], 'Historical Part1 bytes retained')
    assert actual['sha256'] == item['sha256']
    verification['part1_preserved'].append(actual)
verification['overall'] = 'PASS' if all(x['complete'] for x in verification['production']) else 'PARTIAL: functional verification passes; longer production horizons stopped at the declared budget'
verification['support_tools'] = [identity(KB / path, 'Current Part2 support tool') for path in
    ['tools/source_versions.py', 'tools/kb.py', 'tools/tests/test_source_snapshots.py', 'tools/run_part2_checks.py']]
verification['support_policy'] = identity(ROOT / '.SeedAnalyzer/KB.md', 'Current required policy plus local source-snapshot reference')
verification['tool_checks'] = {'distinct_passed': 29, 'skipped': 1,
    'detail': '14 source-version +4 reviewed-commit +11 snapshot checks passed. Actual symlink fixture skipped because host denied symlink creation; mocked Windows reparse-boundary checks passed. Reviewed-commit script also reran the14 source checks successfully. Disposable fixtures removed; no canonical transactions by helpers.'}
verification['source_lineage'] = {'new_source_ids': 'SRC-0091..SRC-0127', 'existing_sources_revised': 33, 'new_code_files': 4,
    'git_predecessors': 21, 'exact_kb_snapshot_predecessors': 12, 'historical_bytes_unavailable': 0}
verification['interventions'] = ['First Debug suite87/88: one fixture failed to locate a private base-class field. Corrected fixture lookup; preserved original failing result.',
    'Independent review corrected duplicate/foreign decision guards before effect draining and forwarded actual source-cell callbacks for pending effects.',
    'Added regression coverage and made world verification read scores without mutating/reconciling history. Final91/91 both configurations.',
    'Declared unavailable-gene retarget change and captured old-gene execution before new baselines; no policy tuning or budget extension after seeing results.']
(KB / 'r02_part2_verification.json').write_text(json.dumps(verification, indent=2) + '\n', encoding='utf-8')

table = '\n'.join(f"| {x['seed']} | {x['initial_population']} → {x['final_population']} | {x['completed_seasons']} / 2048 | {x['wall_seconds']:.1f}s | {x['maximum_age_batch_seconds']:.2f}s |" for x in verification['production'])
report = f'''# R02 Part 2 review

Seed Analyzer • {now} • Owner authority DEC-0014 • awaiting owner review.

**Implementation is complete. Verification status: {verification['overall']}.** All91 functional checks passed in Debug and Release; the longer-run limits remain visible below. This is not owner acceptance or closure of R02.

## What changed

- One gene is chosen against its own actual target/context, then retained through execution. Self is object identity. Decisions resolve in seeded shuffled order, with current liveness and affordability checked before cost/benefit commit. Newborns join the next season; dead original actors still receive completed outcomes.
- Food gathers debit the beneficiary's actual cell and credit reserves in the same transaction, with caps and cumulative accounting. Movement never carries an unpaid debit to another cell.
- A child is constructed only when a free neighbor is secured. Available neighbors are sampled uniformly; birth transfers two parent reserves, charges/counts once and creates no pending child on failure. Self-parenting is retained.
- Movement gets one attempt and expires on every path, including blocked, Self and edge-fallback attempts. Finite borders and opposite-direction fallback remain.
- Infection and target-change predicates are exactly5/10 and4/10. Mutation protects the active self-mutating slot, not an unrelated attacker's slot in another organism. The founder generator enforces its existing four-per-type limit without reroll loops.
- Health/reserve arithmetic is wider and capped where applicable. Scalar organism/parent IDs, generation and lifetime-seasons are available for the later UI. The existing ninth-tick age/fertility clock remains.
- Learning consumes completed actual outcomes, invalidates incompatible gene history and copies matching parental history independently. The shared reward retains numeric weights with explicitly revised inputs; health/age terms remain whole-turn associations, not exclusive action causality.

## Defaults and candidate policies

The app continues with repaired legacy learning, the legacy overweight threshold, and damage-only attacks. Independently selectable candidates in SimulationRunOptions.Policy implement unseen-first/10% exploration and64 external LRU contexts, capped health50, and paid predation (cost1; up to5 actual victim reserves on the direct lethal non-Self hit). These candidates are tested but not selected as ecological winners. There are no fixed species, population quotas or guaranteed coexistence. The starting-settings UI belongs to a later part.

Eligibility filtering means unavailable gather/heal genes wait for surroundings or DNA to change instead of entering their old failed-target retarget branches. A captured old gene still executes if infection replaces its slot after selection; stale learning credit is discarded. The contract in R02_PART2_CONTRACT.md records these semantics and the exact reward formula.

## Verification

- Existing VS2022 MSBuild + SDK9.0.315 rebuilt all five solution projects in Debug and Release. Zero errors;163 CA1416 Windows-platform warnings in each, unchanged from Part1. No new dependency, solution/project edit, Git write or UI-layout change.
- Debug91/91 in {verification['builds_and_tests'][0]['seconds']:.1f}s; Release91/91 in {verification['builds_and_tests'][1]['seconds']:.1f}s. Tests cover actions, accounting, birth/movement boundaries, policies, learning, reset, rendering/UI boundaries and reference replay. The first87/88 result and fixture correction remain in the current result file.
- All69 source/build-input hashes and five loaded-assembly hashes per configuration match the final builds. The UI was exercised by automated boundary tests; an interactive owner review remains appropriate.
- Four reference seeds11/29/47/83 repeat with observation/rendering in the full suites; a fresh-process replay also passes. Separate seeds11/29 at128seasons match full state hashes, random draws and final metrics with diagnostics/rendering off versus on.
- No reserved holdouts101/211, candidate ecological tuning or Parts3-4 work was run.

## Bounded production observations

Repaired default, seeds11/29/47/83, intended2048seasons,120s external limit per process with earlier in-process orderly stop. Each row is the final completed batch, including partial horizons. Maximum batch time is the maximum among the retained last256 age-batch samples, not an all-time global maximum if earlier samples were omitted.

| Seed | Population | Completed seasons | Process time | Max retained age batch |
|---|---:|---:|---:|---:|
{table}

These are valid partial observations, not completed2048-season results. Higher populations now put substantial load on computation, and multi-second headless age batches occur in this changed model. No rendering was performed in these production runs. This does not retrospectively prove the exact cause of the older live-UI report. It also does not establish coexistence or absence of takeover: long-horizon diversity metrics and matched-workload performance comparisons remain for later work. Do not interpret reduced population as a performance success or these changed trajectories as directly comparable ecological replicates of Part1.

## Knowledge and review boundary

Part1 review acceptance is recorded separately from this pending Part2 review. Source catalog adds33 revised identities and4 new files;21 predecessors are pinned to local Git and12 to exact KB-local snapshots of uncommitted accepted sources. A small validated snapshot-history adapter supplies that narrow need. Historical Part1 reports/results remain byte-identical. Support verification:29 distinct checks passed, one real-symlink fixture skipped due to host privileges; mocked reparse checks passed.

See r02_part2_results.json for raw current results, r02_part2_builds.json for builds/input identities, and r02_part2_verification.json for the compact verification audit. Canonical KB validation is recorded after the reviewed transaction and source preparation in r02_part2_kb_verification.json.

Review the app in Visual Studio2022, particularly action targets, food spending and birth/movement behavior. Population-related slowdown is still expected on dense boards. **Stop here for owner review; no Part3 or Part4 implementation and no further runs are active.**
'''
(KB / 'IMPROVEMENT_RESULT_R02_PART2.md').write_text(report, encoding='utf-8')
artifacts = [identity(KB / name, 'Seed-derived Part2 support; implementation complete, verification/acceptance boundaries explicit') for name in
    ['IMPROVEMENT_RESULT_R02_PART2.md', 'R02_PART2_CONTRACT.md', 'r02_part2_results.json', 'r02_part2_builds.json', 'r02_part2_verification.json']]
origin = {'type': 'authorized_R02_Part2_implementation_and_verification', 'actor': 'Seed Analyzer', 'recorded_at_utc': now,
          'attribution': 'Transient helpers implemented/reviewed Control, DNA actions and learning; Seed Analyzer owns integration, results and canonical KB.'}
new = [
 {'id':'FACT-0037','kind':'observed_fact','status':'active','statement':'R02 Part2 implements single targeted action decisions, explicit shuffled transactions, same-cell immediate food accounting, placement-only birth cost/counts, movement expiry, exact probability guards, wider arithmetic, lifetime/generation IDs and completed-outcome learning. Default remains repaired control; optional learning/health/predation candidates are separate. UI layout and Parts3-4 are unchanged.',
  'source_refs':list(mapping.values()),'related_record_ids':['DEC-0014','REC-0006','FACT-0038','INF-0007'],'origin':origin,
  'derivation':{'artifacts':artifacts[:2], 'locators':['Organism.PrepareSeason/ResolveDecision/FinishSeason/CommitBirth/IsActionEligible','Board.ExecuteSingleSeason','BoardSquare.GatherFood/TryPlaceChild/TryMove','StrategyNetwork.Choose/Complete/LearnFromParents','OrganismEvaluation.EvaluateCompleted','ActionModel.SimulationPolicy']},
  'uncertainty':'New decision/reward/founder/ordering semantics break earlier trajectory comparability. Eligible-only selection prevents former failed-gather/heal retarget branches. Default retains Self Kill, self-parenting, DNA-sum Heal relation and ninth-tick aging; no balanced ecology established.'},
 {'id':'FACT-0038','kind':'observed_fact','status':'active','statement':verification['overall']+'. VS2022 whole-solution Debug/Release builds passed with zeroerrors/163existing CA1416warnings;91/91 tests each. All69 source/build-input and five loaded-assembly identities per configuration verified. Reference passivity/replay passed. Four production runs preserve actual partial/completed horizons and timing, without retuning or budget extension.',
  'source_refs':[mapping[p] for p in mapping if 'Tests/' in p], 'related_record_ids':['DEC-0014','FACT-0037','INF-0007','Q-0003'], 'origin':origin,
  'derivation':{'artifacts':artifacts[2:],'verification':verification}, 'uncertainty':'Dense-population multi-second headless batches occur in the changed model. No matched-workload speedup, ecological acceptance, completed horizon beyond emitted records or interactive owner review is claimed.'},
 {'id':'FACT-0039','kind':'observed_fact','status':'active','statement':'A bounded kb_snapshot history adapter now verifies exact regular files only inside KnowledgeBase/source-history, with16MiB limit, SHA/size/reparse/drift checks and --history-file transaction drafting. It preserves12 uncommitted accepted predecessor sources alongside21 newly pinned Git predecessors. Reviewed apply, revision, canonical integrity and failure guards remain.29 distinct tool checks passed;one actual-symlink fixture skipped for host privileges.',
  'source_refs':[], 'related_record_ids':['DEC-0006','DEC-0013','DEC-0014','FACT-0020','FACT-0036'], 'origin':origin,
  'derivation':{'tools':verification['support_tools'],'policy':verification['support_policy'],'tests':verification['tool_checks']}, 'uncertainty':'Tool validation establishes the checked structure/identity boundaries, not experiment truth. Real symlink creation could not be tested on this host; mocked reparse checks passed.'},
 {'id':'REC-0006','kind':'recommendation','status':'provisional','statement':'Part2 Seed-selected implementation contract keeps repaired legacy defaults for later comparison and exposes independent candidates: unseen-first10% exploration/64external LRU contexts, capped health50, attack cost1/direct lethal transfer up to5 actual reserves. Shared reward: .15gathered+.15(transferred-spent)+.25placedBirth+.35actorHealthDelta+.10actorAgeReduction-1ifdead/absent. This is not an owner-selected ecological optimum or promotion of a candidate.',
  'source_refs':[mapping['Fistnet.Genepool.Dna/ActionModel.cs'],mapping['Fistnet.Genepool.Dna/Elements/Brain/StrategyNetwork.cs'],mapping['Fistnet.Genepool.Dna/Elements/Brain/OrganismEvaluation.cs']], 'related_record_ids':['REC-0005','DEC-0014','FACT-0037','Q-0003'], 'origin':origin,
  'derivation':{'artifact':artifacts[1]}, 'uncertainty':'Health/age credit is whole-turn associative. Unavailable actions wait for context/mutation. Ecological comparison and owner acceptance remain outstanding.'},
 {'id':'INF-0007','kind':'inference','status':'provisional','statement':'Part2 implementation and functional tests are ready for owner review, but overall verification is PARTIAL because the longer default production horizons stopped at declared budgets. Retain dense-population computation and ecological behavior as unresolved; do not claim R02 closure or promote optional policies. Stop after this review delivery.',
  'source_refs':list(mapping.values()), 'related_record_ids':['DEC-0014','FACT-0037','FACT-0038','REC-0006','Q-0003'], 'origin':origin,
  'derivation':{'artifact':artifacts[0]}, 'uncertainty':'Seed completion judgment does not accept its own result. Next work requires the owner review/next applicable instruction.'}
]
updates = []
for rid in ['INF-0006','FACT-0033','REC-0005','FACT-0020','FACT-0036']:
    record = copy.deepcopy(records[rid])
    record.setdefault('resolution_notes', []).append({'at_utc':now,'actor':'Seed Analyzer','type':'R02_Part2_successor',
        'note':'DEC-0014 subsequently accepts Part1 and authorizes Part2. Part2 implementation is ready for review with91/91 tests both configurations, while longer production verification is partial. Current source/tool versions and limits are in FACT-0037/0038/0039 and INF-0007. Earlier statements/artifact hashes retain their original dated horizon.',
        'record_ids':['DEC-0014','FACT-0037','FACT-0038','FACT-0039','INF-0007']})
    record['related_record_ids'] = list(dict.fromkeys(record.get('related_record_ids',[])+['DEC-0014','FACT-0037','FACT-0039','INF-0007']))
    if rid == 'REC-0005':
        record['statement'] += ' Current successor: Part1 is owner-accepted (DEC-0014); Part2 is implemented and functionally verified, with partial longer-run verification, awaiting owner review (INF-0007). Parts3-4 remain deferred.'
    updates.append(record)
exp = dict(core['experiment'], status='R02_Part2_implemented_verification_partial_awaiting_owner_review', phase='EVALUATION', trial_authorized=False,
    trial_scope='No active trial; bounded Part2 runs stopped/completed. Further work awaits owner review.',
    design_accepted_scope='DEC-0014 accepts Part1 and authorizes Part2 implementation; Part2 result and optional policy promotion remain unaccepted.')
auth = dict(core['authority'], current_stage_record='DEC-0014', next_action='Stopped for owner review of Part2; no Parts3-4 or further runs.',
    originals='Part2 authorized source changes complete. Historical predecessor bytes retained in pinned Git or exact KB-local snapshots. No Git writes.',
    current_research_scope='Part2 implemented against accepted Part1 horizon; functional verification passes and long production horizons are partial.')
proposal = dict(core['proposal'], status='Part1 accepted; Part2 implemented with partial longer-run verification, awaiting owner review; Parts3-4 deferred',
    implementation_scope='Part2 completed implementation only; results await owner review', implementation_authorization_record='DEC-0014',
    tests_scope='Part2 functional mechanics/policy tests and bounded default baselines; no ecological acceptance', result_record_ids=core['proposal']['result_record_ids']+['FACT-0037','FACT-0038','REC-0006','INF-0007'])
routes = copy.deepcopy(core['routes']);routes['r02_part2_result']={'record_ids':['DEC-0014','FACT-0037','FACT-0038','REC-0006','INF-0007','Q-0003']}
routes['current_implementation']={'record_ids':['DEC-0009','DEC-0014','FACT-0037','FACT-0038','INF-0007']}
routes['kb_tooling']['record_ids'] += ['FACT-0039']
tooling = dict(core['tooling'], record_id='FACT-0039', source_history='Pinned local Git or exact KB/source-history snapshots, verified against immutable registered SHA/size; no unavailable history.',
    verification='29distinct source-version/reviewed-commit/snapshot checks passed,1host-symlink fixture skipped; current identities in FACT-0039.',current_maintenance_authority='DEC-0014',current_verification_receipt=str(KB/'r02_part2_verification.json'))
compact_environment = {'status':'VS2022 whole solution Debug/Release and91/91tests each verified for Part2','sdk_for_vs2022':'9.0.315',
    'report':core['environment']['report'],'vs2022_requirement_record':'DEC-0004','record_ids':['FACT-0013','FACT-0014','FACT-0015','FACT-0016','FACT-0019','FACT-0021','FACT-0024','FACT-0028','FACT-0038'],
    'refresh':'Existing paths/cache isolation in KnowledgeBase/tools/build_solution.py; recheck material inputs before a future changed build.'}
part1 = {'id':'R02_Part1','status':'Accepted by User in DEC-0014','record_ids':['FACT-0033','FACT-0034','FACT-0035','INF-0006','DEC-0014'],
    'report':str(KB/'IMPROVEMENT_RESULT_R02_PART1.md'),'limits':'Historical multi-second live slowdown was not reproduced; no ecological acceptance.'}
part2 = {'id':'R02_Part2','status':'IMPLEMENTED; verification PARTIAL; stopped for owner review','record_ids':['DEC-0014','FACT-0037','FACT-0038','REC-0006','INF-0007'],
    'report':str(KB/'IMPROVEMENT_RESULT_R02_PART2.md'),'verification':'91/91Debug+Release; full solution builds; reference passivity. Long production horizons stopped at budget; actual results in FACT-0038.',
    'limits':'No ecological acceptance, policy promotion, interactive owner acceptance or Parts3-4. Dense-population computation remains material.'}
transaction = {'actor':'Seed Analyzer','note':'Record Part1 acceptance and Part2 implementation, functional passes and partial bounded production observations; preserve source history and stop for owner review.',
    'upsert_sources':draft['upsert_sources'],'upsert_records':new+updates,
    'core_patch':{'experiment':exp,'authority':auth,'proposal':proposal,'routes':routes,'tooling':tooling,'environment':compact_environment,
        'implementation':{'status':'R01 CLOSED successful/useful by User','actor':'Seed Analyzer','owner_closure_record_id':'DEC-0009',
            'record_ids':core['implementation']['record_ids'],'report':str(KB/'IMPROVEMENT_RESULT_R01.md'),'committed_horizon':core['implementation']['committed_horizon']},
        'previous_iteration_part':part1,'current_iteration_part':part2}}
(KB/'r02_part2_result_transaction.json').write_text(json.dumps(transaction,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'overall':verification['overall'],'records_added':len(new),'records_updated':len(updates),'source_upserts':len(draft['upsert_sources']),
    'production':verification['production'],'transaction':str(KB/'r02_part2_result_transaction.json')}))
