"""NPC source and image validation without claiming Unity compilation."""
import hashlib
import json
from pathlib import Path
from PIL import Image

root=Path(__file__).resolve().parents[1]
manifest=json.loads((root/'Assets/Resources/Data/NpcIdentityCatalog.json').read_text())
entries=manifest['entries']
assert len(entries)==65
ids={e['id'] for e in entries}
assert len(ids)==65 and len({e['name'] for e in entries})==65
regions=json.loads((root/'Assets/Resources/Data/RegionCatalog.json').read_text())['regions']
for r in regions:
    if r.get('isSettlement'):
        for role in ('healer','rumor','investigator','craftsman','merchant','requester'):
            assert f'town_{r["regionId"]}_{role}' in ids
hashes=set();cells=set()
for e in entries:
    assert e['name'] and e['role']
    key=(e['sheet'],e['cell'])
    assert key not in cells
    cells.add(key)
    path=root/f'Assets/Resources/Art/Npcs/npc_atlas_{e["sheet"]:02}.png'
    im=Image.open(path).convert('RGBA')
    assert im.size==(1024,1536)
    w,h=im.width//manifest['columns'],im.height//manifest['rows']
    x=e['cell']%manifest['columns']*w;y=e['cell']//manifest['columns']*h+e.get('cropTop',0)
    crop_h=e.get('cropHeight',h)
    assert x+w<=im.width and y+crop_h<=im.height
    # Read-only crops: reproduce the runtime Sprite.Create rect; do not alter source images.
    cell=im.crop((x,y,x+w,y+crop_h))
    alpha=cell.getchannel('A').histogram()
    assert alpha[0]>1000 and sum(alpha[200:])>4000,e['id']
    digest=hashlib.sha256(cell.tobytes()).hexdigest()
    assert digest not in hashes,e['id']
    hashes.add(digest)
    meta=path.with_suffix('.png.meta').read_text()
    assert 'nPOTScale: 0' in meta and 'maxTextureSize: 2048' in meta
gen=(root/'Assets/Editor/PlayablePrologueGenerator.cs').read_text()
assert 'CreateFieldActor(parent, name, cell, visual, false)' not in gen[gen.index('private static GameObject CreateNpc('):gen.index('private static Vector2Int TownServiceCell')]
for e in entries[:23]: assert f'"{e["id"]}"' in gen,e['id']
presence=(root/'Assets/Scripts/Field/FieldNpcPresence.cs').read_text()
assert 'AddComponent<FieldNpcIdentity>()' in presence
identity=(root/'Assets/Scripts/Field/FieldNpcIdentity.cs').read_text()
assert 'renderer.sprite = sprite;' in identity and 'renderer.enabled = sprite != null;' in identity
assert 'GraphicRaycaster' not in identity.replace('// No GraphicRaycaster: nameplates must not intercept taps on the field.','')
print('PASS: 65 unique IDs/names/art cells, 7 towns × 6 services, 23 story/memory IDs, alpha, atlas import dimensions, legacy scene hook')
