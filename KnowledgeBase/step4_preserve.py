"""One bounded pre-edit identity check for registered Step4 inputs."""
import hashlib, json, os, subprocess, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / 'KnowledgeBase/tools'))
from source_versions import git_history, snapshot_history
os.environ['PATH'] = r'C:\Program Files\Git\cmd;' + os.environ['PATH']
head = subprocess.check_output(['git','rev-parse','HEAD'], cwd=ROOT, text=True).strip()
sources = json.loads((ROOT/'KnowledgeBase/sources.json').read_text())['sources']
rows = []
for source in sources:
    if source.get('supersession') or source.get('locator',{}).get('type') != 'file': continue
    path = Path(source['locator']['path'])
    relative = path.relative_to(ROOT).as_posix()
    if not (relative.startswith('Fistnet.Genepool.Godot/') or relative in ['Fistnet.Genepool.Control/ViewFrame.cs','Fistnet.Genepool.Control/ViewCollector.cs','Fistnet.Genepool.Tests/Program.cs','Fistnet.Genepool.Tests/README.md']): continue
    data = path.read_bytes()
    assert hashlib.sha256(data).hexdigest().upper() == source['sha256'].upper(), relative
    try: history = git_history(ROOT, source, head)
    except ValueError:
        snapshot = ROOT/'KnowledgeBase/source-history'/('step4-' + source['source_id'] + path.suffix)
        if snapshot.exists(): assert snapshot.read_bytes() == data
        else: snapshot.parent.mkdir(exist_ok=True); snapshot.write_bytes(data)
        history = snapshot_history(ROOT, source, str(snapshot))
    rows.append({'source_id':source['source_id'], 'path':relative, 'sha256':source['sha256'], 'history':history})
result = {'head':head,'kb_revision':54,'sources':rows}
(ROOT/'KnowledgeBase/r03_step4_predecessors.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result))
