"""Prepare the owner approval transaction and verify exact pre-edit Git history."""
import copy, hashlib, json, subprocess
from datetime import datetime, timezone
from pathlib import Path

root = Path(r'D:\Posao\Fistnet.Genepool')
kb = root / 'KnowledgeBase'
core = json.loads((kb/'core.json').read_text(encoding='utf-8'))
assert core['kb_revision'] == 45
records = {r['id']: r for r in map(json.loads, (kb/'records.jsonl').read_text(encoding='utf-8').splitlines())}
sources = json.loads((kb/'sources.json').read_text(encoding='utf-8'))['sources']
git = r'C:\Program Files\Git\cmd\git.exe'
head = subprocess.check_output([git, '-C', str(root), 'rev-parse', 'HEAD'], text=True).strip()
checks = []
for name in ['.gitignore', 'Fistnet.Genepool.sln', 'Fistnet.Genepool.Tests/Program.cs', 'KnowledgeBase/tools/build_solution.py']:
    path = root/name
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest().upper()
    registered = [s for s in sources if s.get('locator',{}).get('path','').casefold()==str(path).casefold() and s.get('sha256')==digest]
    blob = subprocess.check_output([git, '-C', str(root), '--no-replace-objects', 'show', head+':'+name])
    transform = 'raw' if blob==data else 'lf_to_crlf' if blob.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')==data else None
    snapshot = None
    if transform is None:
        assert registered, f'Unregistered source requiring history: {name}'
        snapshot = kb/'source-history'/(registered[-1]['source_id']+path.suffix)
        assert snapshot.resolve().parent == (kb/'source-history').resolve()
        if snapshot.exists():
            assert snapshot.read_bytes()==data, 'History collision'
        else:
            snapshot.parent.mkdir(exist_ok=True)
            with snapshot.open('xb') as stream:
                stream.write(data)
    checks.append({'path': str(path), 'source_id': registered[-1]['source_id'] if registered else None, 'sha256': digest, 'bytes': len(data), 'git_commit': head if transform else None, 'transform': transform, 'history_file':str(snapshot) if snapshot else None, 'note':'Exact working bytes require the single affected-source snapshot; tested raw/CRLF Git forms did not reproduce them.' if snapshot else 'Exact pinned Git reproduction verified.'})
now = datetime.now(timezone.utc).isoformat()
(kb/'r03_step3_preedit_history.json').write_text(json.dumps({'actor':'Genepool Analyzer','at_utc':now,'checks':checks},indent=2)+'\n',encoding='utf-8')
decision = {'id':'DEC-0025','kind':'owner_decision','status':'active',
 'statement':'Owner approves REC-0009 R03 Godot 2D plan, starts its bounded implementation and authorizes required downloads. Implement and verify setup/2D viewer, then stop for review before 3D/graphics-polish stages. If an issue cannot be resolved, stop and report it for owner help.',
 'source_refs':[], 'related_record_ids':['DEC-0024','REC-0009','DEC-0020'], 'tags':['R03','Step3','Godot','implementation'],
 'origin':{'type':'direct_user_message','actor':'Genepool Analyzer','recorded_at_utc':now,'quote':"Approved. Start the implementation. Download anything you need. If there is an issue you can't fix stop and tell me, I will try to help."},
 'uncertainty':'Approval authorizes the planned local setup, downloads, edits, builds and bounded verification; it does not establish successful integration or owner acceptance of the resulting viewer. R02 ecology remains closed.'}
rec=copy.deepcopy(records['REC-0009'])
rec.setdefault('resolution_notes',[]).append({'at_utc':now,'actor':'Genepool Analyzer','prior_statement':rec['statement'],'note':'Owner accepted the plan and authorized its implementation/downloads in DEC-0025. Result acceptance remains pending.','record_ids':['DEC-0025']})
rec['status']='resolved'
rec['statement']='R03 Step2 Godot 2D implementation plan accepted by owner in DEC-0025. R03 Step3 authorized setup/implementation is in progress; dependency, VS2022, GPU and viewer verification remain to be established. Detailed accepted scope is R03_GODOT_2D_IMPLEMENTATION_PLAN.md; later depth/graphics polish and standalone packaging remain outside this milestone.'
rec['related_record_ids'].append('DEC-0025')
patch={k:copy.deepcopy(core[k]) for k in ['experiment','owner_decision_ids','authority','proposal','current_iteration_part','next_iteration','routes','tooling']}
patch['experiment']['status']='R03_Step3_implementation_in_progress'
patch['experiment']['design_accepted_scope']='R02 Parts1-4 accepted and closed with ecology inconclusive. R03 Godot engine selected by DEC-0024; Step2 plan REC-0009 accepted and bounded setup/2D implementation authorized by DEC-0025.'
patch['owner_decision_ids'].append('DEC-0025')
patch['authority'].update(current_stage_record='DEC-0025',next_action='Implement approved Godot setup and 2D viewer; verify and stop for owner review or an unresolved blocker.',originals='Bounded source edits/builds/tests authorized by DEC-0025; exact pre-edit Git histories checked in r03_step3_preedit_history.json.',current_research_scope='Implementation-related primary-source research and required downloads authorized; no new ecological evaluation.')
patch['proposal'].update(status='OWNER_ACCEPTED_IMPLEMENTATION_AUTHORIZED',implementation_authorized=True,owner_acceptance_record='DEC-0025',limits='Accepted plan; implementation and verification in progress, outcome not accepted.')
patch['current_iteration_part']={'id':'R03_Step3','status':'IMPLEMENTATION_IN_PROGRESS','record_ids':['DEC-0025','REC-0009'],'plan':str(kb/'R03_GODOT_2D_IMPLEMENTATION_PLAN.md'),'verification':'Pending initial toolchain/GPU checkpoint.','limits':'2D setup/viewer only; stop for review before depth/3D/graphics polish.'}
patch['next_iteration'].update(status='R03_STEP3_IMPLEMENTATION_IN_PROGRESS',implementation_started=True,next_action='Complete approved setup/2D implementation and verification, then owner review.',start_scope='Step3 authorized by DEC-0025',implementation_authorization_record='DEC-0025')
patch['routes']['r03_implementation_plan']['record_ids'].append('DEC-0025')
patch['tooling'].update(current_maintenance_authority='DEC-0025',current_verification_receipt=str(kb/'r03_step3_begin_receipt.json'))
txn={'actor':'Genepool Analyzer','note':'Record owner approval and begin bounded Godot 2D implementation; exact existing-source predecessors checked before edits.','upsert_records':[decision,rec],'upsert_sources':[],'core_patch':patch}
(kb/'r03_step3_begin_transaction.json').write_text(json.dumps(txn,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'revision':45,'history':checks,'transaction':'r03_step3_begin_transaction.json'},indent=2))
