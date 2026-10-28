"""Export all evolved portraits and companion frames without changing gameplay data."""
import json,re,sys
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
target=Path(sys.argv[1]).resolve()/'public/shadow-codex'
p=target/'index.html';html=p.read_text();pattern=r'(<script id="data" type="application/json">)([\s\S]*?)(</script>)'
data=json.loads(re.search(pattern,html)[2]);shadows={s['id']:s for s in data['shadows']}
entries=json.loads((ROOT/'Assets/Resources/Data/EvolutionArtCatalog.json').read_text())['entries']
for e in entries:
 f=shadows[e['shadowId']]['f'][['restored','salvation','grudge'].index(e['stage'])]
 assert f and f['name']==e['formName'],e
 key=e['shadowId']+'_'+e['stage'];f['image']=key+'.webp';f['fieldImages']=[key+'_f01.png',key+'_f02.png']
 Image.open(ROOT/'Assets/Resources'/(e['portrait']+'.png')).save(target/'img'/f['image'],lossless=True)
 for source,dest in zip(e['fieldFrames'],f['fieldImages']):
  Image.open(ROOT/'Assets/Resources'/(source+'.png')).save(target/'img'/dest)
html=re.sub(pattern,lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[3],html,count=1)
p.write_text(html)
print(f'Exported {len(entries)} evolution portraits and {len(entries)*2} companion frames')
