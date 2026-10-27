"""Source/art checks without Unity. Does not claim compilation or Play Mode."""
import json
import re
import sys
from pathlib import Path
from PIL import Image

root=Path(__file__).resolve().parents[1]
data=root/'Assets/Resources/Data'
catalog=json.loads((data/'ElementExpansionCatalog.json').read_text())
rules=json.loads((data/'ElementCatalog.json').read_text())['elements']
manifest=json.loads((root/'Docs/ElementExpansionSources.json').read_text())
assert len(catalog['shadows'])==11 and len(catalog['skills'])==33
assert len({s['shadowId'] for s in catalog['shadows']})==11
assert {s['element'] for s in catalog['shadows']}=={r['id'] for r in rules}
assert [r['value'] for r in rules]==list(range(11))
strong={r['id']:r['strong'] for r in rules}
def multiplier(a,b): return 1 if a==b else 1.5 if b in strong[a] else .75 if a in strong[b] else 1
for a in strong:
    for b in strong: assert multiplier(a,b) in (1,.75,1.5)
for a,b in [('Light','Shade'),('Shade','Light'),('Psychic','None'),('None','Psychic')]:
    assert multiplier(a,b)==1.5
skills={s['skillId']:s for s in catalog['skills']}
regions={r['regionId']:r for r in json.loads((data/'RegionCatalog.json').read_text())['regions']}
for e in json.loads((data/'ElementExpansionEncounters.json').read_text())['entries']:
    assert not regions[e['regionId']].get('isSettlement',False)
for s in catalog['shadows']:
    assert s['basicAttack'] in skills and all(k in skills for k in s['skills'])
    assert s['restored']['requiredLevel']==18
    assert all(k in skills for k in s['restored']['bonusSkills'])
    assert skills[s['basicAttack']]['forceNeutralElement']==(s['element']=='None')
field_ids=manifest['preservedFieldIds']+[id.rsplit('_',1)[0] for sheet in manifest['sheets'] if sheet['kind']=='field' for id in sheet['ids'][::2]]
assert len(field_ids)==len(set(field_ids))==191
for id in field_ids:
    frames=[]
    for n in (1,2):
        im=Image.open(root/'Assets/Art/Final/Field'/f'{id}_{n:02}.png').convert('RGBA')
        assert im.size==(24,32),id
        pixels=list(im.getdata());opaque=[c for c in pixels if c[3]]
        if id not in manifest['preservedFieldIds']:
            assert all(c[3] in (0,255) for c in pixels),id
            assert 48<=len(opaque)<=240,(id,len(opaque))
            assert len(set(opaque))<=16,id
        frames.append(im.tobytes())
    assert frames[0]!=frames[1],('identical walking frames',id)
for s in catalog['shadows']:
    for suffix in ('','_restored'):
        im=Image.open(root/'Assets/Art/Final/Portraits'/f'{s["shadowId"]}{suffix}.png')
        assert im.size==(512,512) and im.mode=='RGBA'
if len(sys.argv)>1:
    target=Path(sys.argv[1])/'public/shadow-codex'
    html=(target/'index.html').read_text()
    d=json.loads(re.search(r'<script id="data" type="application/json">([\s\S]*?)</script>',html)[1])
    assert len(d['shadows'])==191 and d['elements']==rules
    for s in d['shadows']:
        assert s['fld']
        for suffix in ('.webp','_f01.png','_f02.png'):
            assert (target/'img'/f'{s["id"]}{suffix}').exists()
    for s in catalog['shadows']:
        ds=next(x for x in d['shadows'] if x['id']==s['shadowId'])
        assert ds['f'][0]['lv']==s['restored']['requiredLevel']
        assert ds['el']==s['element']
montage=Image.new('RGBA',(11*96,160),(35,30,45,255))
for i,s in enumerate(catalog['shadows']):
    im=Image.open(root/'Assets/Art/Final/Field'/f'{s["shadowId"]}_01.png').resize((96,128),Image.Resampling.NEAREST)
    montage.alpha_composite(im,(i*96,16))
montage.save('/tmp/element-companions.png')
print('PASS: 11 species, 33 skills, 121 matchups, 191 walking pairs, 22 portraits, codex parity')
