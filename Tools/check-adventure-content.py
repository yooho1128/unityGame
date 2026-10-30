"""Cross-reference the shipping growth guide, geography anchors and late-story branches."""
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / 'Assets/Resources/Data'
def load(name): return json.loads((DATA / (name + '.json')).read_text())
def source(path): return (ROOT / path).read_text()
conditions = load('EvolutionGuidanceCatalog')['entries']
layouts = load('RegionLayoutCatalog')['entries']
regions = {r['regionId']: r for r in load('RegionCatalog')['regions']}
quests = {q['questId']: q for q in load('QuestCatalog')['quests']}
events = {e['eventId']: e for e in load('CompanionEventCatalog')['events']}
flags = set()
for file in DATA.glob('*Catalog.json'):
    for shadow in json.loads(file.read_text()).get('shadows', []):
        if not isinstance(shadow, dict): continue
        for stage in ('restored', 'salvation', 'grudge'):
            form = shadow.get(stage) or {}
            if form.get('requiredFlag'): flags.add(form['requiredFlag'])
assert flags == {c['flag'] for c in conditions}
assert len(conditions) == len(flags)
for c in conditions:
    assert c['routes'], c['flag']
    for r in c['routes']:
        assert r['kind'] in ('Quest', 'Talk', 'Defeat', 'Reach', 'Companion')
        if r['kind'] == 'Quest':
            assert r['questId'] in quests
            if r['personal']: assert r['questId'].startswith('memory_')
        else: assert r['regionId'] in regions
        if r['kind'] == 'Companion': assert r['eventId'] in events
assert {l['regionId'] for l in layouts} == regions.keys()
for layout in layouts:
    assert (regions[layout['regionId']]['arrivalX'], regions[layout['regionId']]['arrivalY']) in {(p['x'], p['y']) for p in layout['anchors']}
    assert all(-10 <= p['x'] <= 10 and -8 <= p['y'] <= 8 for p in layout['anchors'])
    region = regions[layout['regionId']]
    if region['order'] > 40:
        required = [(-9,0),(9,0),(0,8),(0,-8)][:len(region['connectedRegionIds'])]
        assert set(required) <= {(p['x'],p['y']) for p in layout['anchors']}, 'loop-generated exit omitted'

dialogues = load('DialogueCatalog')['dialogues']
by_id = {d['dialogueId']: d for d in dialogues}
assert len(dialogues) == len(by_id)
for d in dialogues:
    for c in d.get('choices', []):
        assert not c.get('nextDialogueId') or c['nextDialogueId'] in by_id
# All three name choices must be heard again in repeat dialogue, a later scene and the finale.
for did in ['npc_name_keeper_repeat', 'boss_mourning_keeper_pre', 'npc_inverted_guide_repeat', 'npc_rival_director_final', 'boss_final_shadow_pre']:
    reactions = {line.get('requiredFlag') for line in by_id[did]['lines']}
    assert {'act7_names_preserved', 'act7_names_shared', 'act7_names_sealed'} <= reactions, did
assert by_id['boss_true_name_selene_post']['lines'][-1]['setFlag'] == 'act7_complete'
assert [l['setFlag'] for l in by_id['boss_final_shadow_post']['lines'][-2:]] == ['story_finale_unlocked', 'act8_complete']
assert all(e['dialogueId'] in by_id for e in load('EndingCatalog')['endings'])

# Verify important integration order, leaving native execution to Unity.
view = source('Assets/Scripts/UI/EvolutionGuideView.cs')
assert view.index('Close();closeBook();') < view.index('WorldMapController.Instance.OpenForObjective(objective)')
terrain = source('Assets/Scripts/Field/ReleaseTerrainPresentation.cs')
assert terrain.index('RegionFieldLayout.Apply') < terrain.index('foreach (var map in maps) owner.Apply(map)')
generator = source('Assets/Editor/PlayablePrologueGenerator.cs')
finish = generator[generator.index('private static void FinishFieldScene'):generator.index('private static PlayerController CreatePlayer')]
assert finish.index('RegionFieldLayout.Apply') < finish.index('CreateRegionalDiscoveryContent')
assert 'PersistLayoutTile' in finish and 'AssetDatabase.CreateAsset(tile,path)' in finish
runtime = source('Assets/Scripts/Field/RegionFieldLayout.cs')
assert 'SaveManager.Current.tileX' in runtime and 'GetComponentsInChildren<Collider2D>(true)' in runtime
assert 'SaveManager.Current?.checkpointMapId' in runtime and 'SaveManager.Current.checkpointX' in runtime
assert 'GetComponentsInChildren<TraversalRoute>(true)' in runtime and 'ProcessTilemapChanges()' in runtime
policy = source('Assets/Scripts/Field/RegionLayoutPolicy.cs')
kinds = {k.strip() for k in re.search(r'enum FieldTileKind \{([^}]+)', policy)[1].split(',')}
sprites = set()
for name in ('ReleaseTerrain', 'ReleaseProps'):
    sprites.update(re.findall(r'\bname: (\w+)', source('Assets/Resources/Art/Environment/' + name + '.png.meta')))
assert kinds <= sprites, 'invisible terrain fallback: ' + str(kinds - sprites)

for script, name in [('export-evolution-guidance.py', 'EvolutionGuidanceCatalog'), ('export-region-layouts.py', 'RegionLayoutCatalog')]:
    path = DATA / (name + '.json')
    before = path.read_bytes()
    subprocess.run([sys.executable, str(ROOT / 'Tools' / script)], check=True)
    assert path.read_bytes() == before, 'stale generated source: ' + name
print(f'PASS: {len(flags)} growth conditions, {len(layouts)} layout/arrival records; three-choice callbacks through final boss; completion flags, links, modal/terrain integration and deterministic exports')
