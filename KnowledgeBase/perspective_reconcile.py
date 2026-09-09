import hashlib,json,sys,subprocess
from pathlib import Path
kb=Path('KnowledgeBase').resolve()
tx=json.loads((kb/'r03_perspective_transaction.json').read_text(encoding='utf-8'))
core=json.loads((kb/'core.json').read_text(encoding='utf-8'))
recs={r['id']:r for r in map(json.loads,(kb/'records.jsonl').read_text(encoding='utf-8').splitlines())}
sources={s['source_id']:s for s in json.loads((kb/'sources.json').read_text(encoding='utf-8'))['sources']}
assert core['kb_revision']==57
def clean(r): return {k:v for k,v in r.items() if k not in ['created_at','updated_at']}
for r in tx['upsert_records']: assert clean(r)==clean(recs[r['id']]),r['id']
for s in tx['upsert_sources']: assert s==sources[s['source_id']],s['source_id']
for k,v in tx['core_patch'].items():
    if k!='last_maintenance': assert core[k]==v,k
assert core['last_maintenance']['actor']==tx['actor'] and core['last_maintenance']['note']==tx['note']
evidence=json.loads((kb/'r03_perspective_evidence.json').read_text(encoding='utf-8'))
for artifact in evidence['artifacts']+evidence['godot_binaries']:
    data=Path(artifact['path']).read_bytes()
    assert len(data)==artifact['bytes'] and hashlib.sha256(data).hexdigest().upper()==artifact['sha256'],artifact['path']
for name,key in [('records.jsonl','records_jsonl_sha256'),('sources.json','sources_json_sha256')]:
    assert hashlib.sha256((kb/name).read_bytes()).hexdigest().upper()==core['state_files'][key]
result=subprocess.run([sys.executable,'-X','utf8','-B',str(kb/'tools/kb.py'),'context','--topic','r03_step4_result','--brief','--compact'],capture_output=True,text=True,encoding='utf-8',check=True)
context=json.loads(result.stdout)
assert 'FACT-0058' in result.stdout and 'DEC-0034' in result.stdout
receipt={'actor':'Genepool Analyzer','input_revision':56,'output_revision':57,'applied_once':True,
'transaction_sha256':hashlib.sha256((kb/'r03_perspective_transaction.json').read_bytes()).hexdigest().upper(),
'apply_stdout_captured':False,'capture_issue':'Shell call yielded a still-running session; orchestrator incorrectly attempted to parse its empty output. No second apply was issued.',
'reconciliation':'Revision57, every requested record/source/core value and state-file hashes match reviewed transaction; deep validation passes. This is reconstructed evidence, not the lost apply write_outcome.',
'expected_state_comparison':True,'artifact_and_godot_binary_identities_verified':True,
'context_reaches_current_correction':True,'deep_validation':'PASS with only pre-existing historicalSRC0115 warning',
'canonical_state_files':core['state_files'],'unresolved':'Original structured apply stdout/cleanup report unavailable; no staging files observed in later inspection.'}
(kb/'r03_perspective_receipt.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(receipt))

