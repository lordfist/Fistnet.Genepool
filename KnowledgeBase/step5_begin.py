"""Draft owner acceptance of Step4 and the bounded start of final R03 graphics polish."""
import copy, datetime, json
from pathlib import Path
KB = Path(__file__).resolve().parent
core = json.loads((KB/'core.json').read_text(encoding='utf-8-sig'))
assert core['kb_revision'] == 59
records = {r['id']:r for r in map(json.loads,(KB/'records.jsonl').read_text(encoding='utf-8').splitlines())}
now = datetime.datetime.now(datetime.timezone.utc).isoformat()
decision = {'id':'DEC-0036','kind':'owner_decision','status':'active',
 'statement':'Owner accepts and closes R03 Step4, confirms the board view is correct, and directs continuation to the next and final original R03 milestone, Step5 improved graphics. Organism colours are explicitly carried forward for improvement. R03 overall closure and Step5 result acceptance remain pending.',
 'source_refs':[],'related_record_ids':['DEC-0021','DEC-0035','FACT-0060','REC-0011'],
 'origin':{'type':'direct_owner_message','actor':'User','recorded_by':'Genepool Analyzer','recorded_at_utc':now,
 'quote':'Great! The view is now correct, but, you are right, Organism colours suck. But we can change that in the last step of our improvement iteration. Lets close Step 4 with "accepted" and move to the next step. I think the next step is also the last one if i remember correctly. :)'}}
plan = {'id':'REC-0011','kind':'recommendation','status':'active',
 'statement':'Implement final R03 Step5 as a focused graphics polish: stronger organism palette and ground separation across zoom levels, retaining flat mould detail and consistent minimap colours. The already accepted grass/dirt, grey Organisms ground, centred trapezoid, navigation and L3/L4 action scopes form the baseline. Verify actual GPU appearance/contrast, passivity, whole VS2022 solution and bounded rendering performance, then stop for owner review.',
 'source_refs':['SRC-0290','SRC-0271','SRC-0291','SRC-0294'],'related_record_ids':['DEC-0036','FACT-0060','REC-0010','DEC-0027'],
 'origin':{'type':'bounded_implementation_plan','actor':'Genepool Analyzer','recorded_at_utc':now},
 'uncertainty':'Specific palette and material choices are engineering proposals, not owner-selected colours. Deferred food-cycle mechanics, exports and ecology are outside this graphics part; no new dependency or governance work is needed.'}
updates=[decision,plan]
for rid in ['DEC-0021','REC-0010','FACT-0060']:
    old=copy.deepcopy(records[rid])
    note={'at_utc':now,'actor':'Genepool Analyzer','record_ids':['DEC-0036','REC-0011'],
      'note':'Owner accepts Step4, including corrected camera and grey ground; organism colour weakness carried to final Step5 graphics polish, now started. Earlier unaccepted/unstarted statements are dated history. R03 and Step5 result acceptance are not yet closed.'}
    old.setdefault('resolution_notes',[]).append(note)
    old['current_resolution']={'statement':note['note'],'record_ids':['DEC-0036','REC-0011']}
    if rid=='REC-0010': old['status']='resolved'
    updates.append(old)
patch={k:copy.deepcopy(core[k]) for k in ['experiment','authority','next_iteration','owner_decision_ids','routes']}
patch['experiment'].update(status='R03_Step5_graphics_in_progress',design_accepted_scope='R02 closed ecology inconclusive; R03 Godot2D and Step4 results accepted. FinalStep5 graphics authorized/in progress; no active trial.')
patch['authority'].update(current_stage_record='DEC-0036', originals='Bounded Step5 graphics edits and proportionate builds/tests authorized by owner continuation.',next_action='Implement and verify focused Step5 graphics polish; stop for owner review.')
previous=copy.deepcopy(core['current_iteration_part'])
previous.update(status='OWNER_ACCEPTED_CLOSED',owner_acceptance_record='DEC-0036',review_status='Accepted by owner; organism colour improvement carried to Step5.',limits='Dated verification retained. No new ecological claim or overall R03 closure.')
patch['previous_iteration_part']=previous
patch['current_iteration_part']={'id':'R03_Step5','status':'IMPLEMENTATION_IN_PROGRESS','record_ids':['DEC-0036','REC-0011','FACT-0060'],
 'scope':'Final graphics polish, especially organism colours/ground contrast; accepted Step4 geometry/ground/action scope retained.',
 'verification':'Pending current Step5 checks; accepted Step4 baseline in FACT-0060.',
 'limits':'Stop for owner result review. Food cycle, exports and ecology remain deferred.'}
patch['proposal']={'id':'R03_Step5','record_id':'REC-0011','status':'AUTHORIZED_IN_PROGRESS','engine':'Godot C#','engine_selection_record':'DEC-0024',
 'implementation_authorized':True,'implementation_authority_record':'DEC-0036','baseline_design_record':'REC-0010','baseline_result_record':'FACT-0060',
 'appearance':'Flat mould; approved dirt/grass and grey Organisms ground; stronger organism colours and clear edges.',
 'viewport_rule':'Accepted centred trapezoid/topdown and L2–4 visible-area rendering with real-world borders.',
 'action_scope_by_level':{'3':'Muted outcomes for all visible cells','4':'Full outcomes only for focused organism'},
 'limits':'Specific palette is an engineering choice for review; no changed simulation mechanics.'}
patch['next_iteration'].update(status='R03_FINAL_STEP5_IN_PROGRESS',next_action='Complete Step5 graphics and stop for review; overall R03 closure pending.',start_scope='Final Step5 graphics authorized by DEC-0036.',current_recommendation_record='REC-0011',current_implementation_acceptance_record='DEC-0036',step4_result_acceptance_record='DEC-0036')
patch['owner_decision_ids'].append('DEC-0036')
patch['routes']['current_implementation']['record_ids']=['DEC-0036','REC-0011']+patch['routes']['current_implementation']['record_ids']
patch['routes']['r03_step4_result']['record_ids']=['DEC-0036']+patch['routes']['r03_step4_result']['record_ids']
patch['routes']['r03_step5_graphics']={'record_ids':['REC-0011','DEC-0036','FACT-0060']}
transaction={'actor':'Genepool Analyzer','note':'Close Step4 as owner accepted and start final R03Step5 graphics polish.','upsert_records':updates,'core_patch':patch}
(KB/'r03_step5_begin_transaction.json').write_text(json.dumps(transaction,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Drafted Step4 acceptance and Step5 start; canonical KB unchanged at revision59.')
