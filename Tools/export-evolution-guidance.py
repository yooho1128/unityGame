"""Trace evolution flags to shipping quests, authored NPC/bosses and companion events.
No web repository or generated Unity assets are needed. Fails on unknown conditions.
"""
import json
import re
from pathlib import Path
from source_calls import calls, literal

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / 'Assets/Resources/Data'
def load(name): return json.loads((DATA / (name + '.json')).read_text())
regions = {r['sceneName']: r for r in load('RegionCatalog')['regions']}
npcs = {n['id']: n for n in load('NpcIdentityCatalog')['entries']}
quests = load('QuestCatalog')['quests']
dialogues = {d['dialogueId']: d for d in load('DialogueCatalog')['dialogues']}
events = load('CompanionEventCatalog')['events']
code = (ROOT / 'Assets/Editor/PlayablePrologueGenerator.cs').read_text()
service = (ROOT / 'Assets/Scripts/Story/MemoryAwakeningService.cs').read_text()
def table(name):
    body = service[service.index(' ' + name + ' ='):]
    return dict(re.findall(r'\{ "([^"]+)", "([^"]+)" \}', body[:body.index('};')]))
aliases, personal = table('StoryMilestoneAliases'), table('PersonalMemoryQuests')
flags = set()
for file in DATA.glob('*Catalog.json'):
    for shadow in json.loads(file.read_text()).get('shadows', []):
        if not isinstance(shadow, dict): continue
        for key in ('restored', 'salvation', 'grudge'):
            form = shadow.get(key) or {}
            if form.get('requiredFlag'): flags.add(form['requiredFlag'])
roots, bosses = [], {}
default_boss = re.search(r'encounterId = "([^"]+)"', (ROOT / 'Assets/Scripts/Story/BossEncounterTrigger.cs').read_text())[1]
for body in re.split(r'        private static void ', code):
    scene = re.search(r'BeginFieldScene\("([^"]+)"', body)
    if not scene or scene[1] not in regions: continue
    rid = regions[scene[1]]['regionId']
    for a in calls(body, 'CreateNpc'):
        if a[0] != 'map.fieldRoot': continue
        for index in (4, 5):
            roots.append((literal(a[index]), dict(kind='Talk', targetId=literal(a[3]), regionId=rid,
                         label=npcs[literal(a[3])]['name'], instruction='대화를 끝까지 마칩니다.')))
    for a in calls(body, 'CreateBossEncounter'):
        route = dict(kind='Defeat', targetId=literal(a[4]), regionId=rid,
                     label=regions[scene[1]]['bossShadowName'] or '이 지역의 보스', instruction='보스에게 승리하고 전투 후 대화를 끝까지 마칩니다.')
        bosses[literal(a[8])] = route
        roots.append((literal(a[7]), route))
    for a in calls(body, 'CreateInteractable'):
        if a[0] == 'map.fieldRoot':
            roots.append((literal(a[3]), dict(kind='Reach', targetId='area_' + rid, regionId=rid,
                          label='지역 조사', instruction='주변 조사 대상과 상호작용하고 대화를 끝까지 마칩니다.')))
    if list(calls(body, 'CreateBoss')):
        route = dict(kind='Defeat', targetId=default_boss, regionId=rid,
                     label=regions[scene[1]]['bossShadowName'], instruction='보스에게 승리하고 전투 후 대화를 끝까지 마칩니다.')
        bosses['boss_moonlit_story_complete'] = route
        roots.append(('boss_moonlit_post', route))

def reachable(did, seen=None):
    seen = set() if seen is None else seen
    if did in seen or did not in dialogues: return []
    seen.add(did)
    result = [did]
    for c in dialogues[did].get('choices', []): result += reachable(c.get('nextDialogueId'), seen)
    return result

def sets(did, flag):
    return any(x.get('setFlag') == flag and x.get('setFlagValue', 1) > 0
               for x in dialogues[did].get('lines', []) + dialogues[did].get('choices', []))

def routes(flag, seen=None):
    seen = set() if seen is None else seen
    if flag in seen: return []
    seen.add(flag)
    result = []
    for q in quests:
        if q.get('completionFlag') == flag:
            result.append(dict(kind='Quest', questId=q['questId'], personal=q['questId'] in personal.values(),
                               label=q['title'], instruction='여정의 모든 목표를 완료합니다.'))
    if flag in bosses: result.append(bosses[flag])
    for did, origin in roots:
        if not any(sets(d, flag) for d in reachable(did)): continue
        choices = [c['text'] for c in dialogues[did].get('choices', [])
                   if c.get('setFlag') == flag or any(sets(d, flag) for d in reachable(c.get('nextDialogueId')))]
        route = dict(origin)
        if choices: route['instruction'] += '\n선택지: ' + ' / '.join('「' + c + '」' for c in choices)
        result.append(route)
    for e in events:
        if flag in (e.get('rewardFlag'), 'companion_event_' + e['eventId'] + '_complete'):
            result.append(dict(kind='Companion', eventId=e['eventId'], regionId=e['regionId'], label='동행 기억',
                               instruction='필요한 그림자를 동행으로 지정하고 지역을 탐색합니다.'))
    if flag in aliases: result += routes(aliases[flag], seen)
    unique = {json.dumps(r, ensure_ascii=False, sort_keys=True): r for r in result}
    return list(unique.values())

entries = [dict(flag=f, routes=routes(f)) for f in sorted(flags)]
assert entries and all(e['routes'] for e in entries), [e['flag'] for e in entries if not e['routes']]
(DATA / 'EvolutionGuidanceCatalog.json').write_text(json.dumps(dict(entries=entries), ensure_ascii=False, indent=2) + '\n')
print(f"Exported {len(entries)} evolution conditions / {sum(len(e['routes']) for e in entries)} actionable routes")
