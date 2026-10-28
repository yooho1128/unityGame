"""Static asset/catalog regression check; does not replace Unity import/Play Mode."""
import collections,hashlib,json,re,sys
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
entries=json.loads((ROOT/'Assets/Resources/Data/EvolutionArtCatalog.json').read_text())['entries']
plan=json.loads((ROOT/'Docs/EvolutionArtSources.json').read_text())
expected={(f['shadowId'],f['stage']) for s in plan['sheets'] for f in s['forms']}
assert len(entries)==len(expected)==173
assert collections.Counter(e['stage'] for e in entries)==dict(restored=113,salvation=30,grudge=30)
assert {(e['shadowId'],e['stage']) for e in entries}==expected
portraits=set()
for e in entries:
 p=ROOT/'Assets/Resources'/(e['portrait']+'.png');im=Image.open(p);assert im.size==(512,512)
 h=hashlib.sha256(p.read_bytes()).hexdigest();assert h not in portraits;portraits.add(h)
 frames=[]
 for f in e['fieldFrames']:
  p=ROOT/'Assets/Resources'/(f+'.png');im=Image.open(p).convert('RGBA');assert im.size==(24,32)
  pixels=list(im.get_flattened_data());visible=[v for v in pixels if v[3]]
  assert all(v[3] in (0,255) for v in pixels);assert len(set(visible))<=16;assert 48<=len(visible)<=240
  occupied={i for i,v in enumerate(pixels) if v[3]};largest=0;components=0
  while occupied:
   pending=[occupied.pop()];size=0;components+=1
   while pending:
    i=pending.pop();size+=1;x,y=i%24,i//24
    for n,valid in ((i-1,x>0),(i+1,x<23),(i-24,y>0),(i+24,y<31)):
     if valid and n in occupied:occupied.remove(n);pending.append(n)
   largest=max(largest,size)
  assert components<=3 and largest/len(visible)>=.96
  assert p.with_suffix('.png.meta').exists();frames.append(im.tobytes())
 assert len(frames)==2 and frames[0]!=frames[1],e
if len(sys.argv)>1:
 web=Path(sys.argv[1])/'public/shadow-codex';html=(web/'index.html').read_text()
 data=json.loads(re.search(r'<script id="data" type="application/json">([\s\S]*?)</script>',html)[1]);shadows={s['id']:s for s in data['shadows']};assert len(shadows)==191
 for e in entries:
  f=shadows[e['shadowId']]['f'][['restored','salvation','grudge'].index(e['stage'])]
  assert f['name']==e['formName'];assert (web/'img'/f['image']).exists()
  for name in f['fieldImages']:assert Image.open(web/'img'/name).size==(24,32)
print('PASS: 173 stage portraits, 346 readable distinct walking frames, stage coverage and optional web export')
