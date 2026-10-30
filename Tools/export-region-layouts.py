"""Reserve authored interactions, every portal landing, and expansion arrivals for layout changes."""
import json
import re
from pathlib import Path
from source_calls import calls, literal

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / 'Assets/Resources/Data'
regions = json.loads((DATA / 'RegionCatalog.json').read_text())['regions']
code = (ROOT / 'Assets/Editor/PlayablePrologueGenerator.cs').read_text()
by_scene = {r['sceneName']: r for r in regions}
anchors = {r['regionId']: {(r['arrivalX'], r['arrivalY'])} for r in regions}
themes = {}
def cell(text): return tuple(map(int, re.search(r'Vector2Int\(\s*(-?\d+),\s*(-?\d+)\)', text).groups()))
for body in re.split(r'        private static void ', code):
    scene = re.search(r'BeginFieldScene\("([^"]+)"', body)
    if not scene or scene[1] not in by_scene: continue
    rid = by_scene[scene[1]]['regionId']
    authored = next(a for a in calls(body, 'BeginFieldScene') if a[0].startswith('"'))
    themes[rid] = authored[-1].removeprefix('Theme.')
    for name, index in [('BeginFieldScene', 2), ('CreateNpc', 2), ('CreateEncounter', 2),
                        ('CreateBossEncounter', 2), ('CreateBoss', 1), ('CreateAreaTrigger', 1),
                        ('CreateInteractable', 2), ('CreatePortal', 2), ('CreateEndingTrigger', 1)]:
        for a in calls(body, name):
            if name == 'BeginFieldScene' and not a[0].startswith('"'): continue
            if name != 'BeginFieldScene' and a[0] != 'map.fieldRoot': continue
            if not a[index].startswith('new Vector2Int('):
                raise ValueError((scene[1], name, a[index]))
            anchors[rid].add(cell(a[index]))
    for a in calls(body, 'CreatePortal'):
        target = literal(a[3])
        assert target in by_scene, target
        anchors[by_scene[target]['regionId']].add(cell(a[4]))
    for a in calls(body, 'CreateTraversalRoutePair'):
        anchors[rid].update([cell(a[2]), cell(a[3])])

# The loop-generated regions use the actual common portal positions and region arrivals.
expansion = code[code.index('private static void CreateExpansionRegions'):code.index('private static Theme ExpansionTheme')]
portal_section = expansion[expansion.index('var portalCells'):expansion.index('foreach (RegionData region in expansion)')]
portal_cells = [tuple(map(int, pair)) for pair in re.findall(r'new Vector2Int\(\s*(-?\d+),\s*(-?\d+)\)', portal_section)]
for r in regions:
    if r['order'] <= 40: continue
    assert len(r['connectedRegionIds']) <= len(portal_cells)
    anchors[r['regionId']].update(portal_cells[:len(r['connectedRegionIds'])])
    if r['regionId'] in ('bubble_cave', 'lantern_reed_field', 'abandoned_toy_store'):
        anchors[r['regionId']].update([(-8,-6),(8,-6)])
# Puzzle node, memory NPC and reward cells authored in the hidden-region switch.
challenge = code[code.index('private static void CreateHiddenRegionChallenge'):code.index('private static void CreateRegionalExpansionEncounters')]
for rid, body in re.findall(r'case "([^"]+)":([\s\S]*?)(?=\n\s*(?:case |default:))', challenge):
    if rid in anchors:
        anchors[rid].update(tuple(map(int, pair)) for pair in re.findall(r'new Vector2Int\(\s*(-?\d+),\s*(-?\d+)\)', body))

theme_profiles = {
    'Theater':'Village', 'Village':'Village', 'Meadow':'Forest', 'AshWastes':'Wastes', 'EmberCity':'Village',
    'Catacombs':'Catacomb', 'AshThrone':'Stage', 'Boss':'Stage', 'FrostPort':'Village',
    'Archive':'Archive', 'ForbiddenStacks':'Archive', 'MirrorVault':'Archive', 'BlueAbyss':'Archive',
    'VioletMarsh':'Marsh', 'HowlVillage':'Village', 'MoonfangForest':'Forest', 'BloodmoonRidge':'Wastes',
    'BeastDen':'Catacomb', 'ThreadMarket':'Workshop', 'ClockworkAlley':'Workshop', 'SeveredWorkshop':'Workshop',
    'MarionetteOpera':'Stage', 'PuppeteerStage':'Stage', 'Censor':'Archive', 'BlackArchive':'Archive',
    'MemorySea':'Coast', 'MoonPalace':'Stage', 'FinalTheater':'Stage', 'CosmicStage':'Stage'
}

def profile(r):
    rid = r['regionId']
    if r.get('isSettlement'): return 'Village'
    if rid in themes: return theme_profiles[themes[rid]]
    if r.get('isBossStage'): return 'Stage'
    if any(k in rid for k in ['catacomb', 'barrack', 'prison', 'mask_room', 'cave', 'cavern']): return 'Catacomb'
    if any(k in rid for k in ['archive', 'stacks', 'vault', 'library', 'laboratory', 'foyer']): return 'Archive'
    if any(k in rid for k in ['coast', 'sea', 'island', 'boardwalk', 'gallery', 'lighthouse']): return 'Coast'
    if any(k in rid for k in ['marsh', 'reed', 'well']): return 'Marsh'
    if any(k in rid for k in ['forest', 'field', 'meadow', 'pass', 'road', 'ridge', 'garden']): return 'Forest'
    if any(k in rid for k in ['clock', 'workshop', 'toy', 'alley', 'thread']): return 'Workshop'
    if r['act'] >= 8 or any(k in rid for k in ['theater', 'tent', 'opera', 'stage', 'palace']): return 'Stage'
    return 'Wastes'

# Service outposts preserve both the supply stand and the respawn/fast-travel cell.
for hub in json.loads((DATA / 'JourneyHubCatalog.json').read_text())['entries']:
    anchors[hub['regionId']].update([(hub['x'],hub['y']),(hub['checkpointX'],hub['checkpointY'])])
for puzzle in json.loads((DATA / 'RegionalPuzzleCatalog.json').read_text())['entries']:
    anchors[puzzle['regionId']].update((p['x'],p['y']) for p in [puzzle['board']] + puzzle['nodes'] + puzzle['exits'])

entries = [dict(regionId=r['regionId'], profile=profile(r),
                anchors=[dict(x=x, y=y) for x, y in sorted(anchors[r['regionId']])]) for r in regions]
(DATA / 'RegionLayoutCatalog.json').write_text(json.dumps(dict(entries=entries), ensure_ascii=False, indent=2) + '\n')
print(f"Exported {len(entries)} fields / {len(set(e['profile'] for e in entries))} layouts / {sum(len(e['anchors']) for e in entries)} safe anchors")
