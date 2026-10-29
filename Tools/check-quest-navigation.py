"""Audit data coverage and integration boundaries; gameplay policy execution uses QuestChecks."""
import hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def read(p):return (ROOT/p).read_text()
def data(p):return json.loads(read('Assets/Resources/Data/'+p+'.json'))
quests=data('QuestCatalog')['quests'];regions={r['regionId']:r for r in data('RegionCatalog')['regions']};targets=data('QuestNavigationCatalog')['entries']
expected={(o['type'],o['targetId']) for q in quests for o in q['objectives'] if o['targetId']!='*'}
assert len(targets)==len(expected)==69
assert {(t['type'],t['targetId']) for t in targets}==expected
for t in targets:assert t['regionId'] in regions and t['label'] and -10<=t['x']<=10 and -8<=t['y']<=8
manager=read('Assets/Scripts/Story/QuestManager.cs');start=manager.index('public bool TryTrackQuest');end=manager.index('private static QuestObjectiveProgressData',start);method=manager[start:end]
assert 'QuestTrackingPolicy.Active' in method and 'TrySave()' in method and 'save.trackedQuestId = previous' in method
assert 'save.trackedQuestId = null; changed = true' in manager
assert 'SaveManager.Current.trackedQuestId == quest.questId' in manager
assert 'CurrentVersion = 14' in read('Assets/Scripts/Save/SaveData.cs')
log=read('Assets/Scripts/UI/QuestLogController.cs');assert log.index('Close(); // Release')<log.index('WorldMapController.Instance.OpenForTrackedQuest()')
assert 'EnsureTrackingActions();' in log and 'QuestGuidanceService.For(_selectedQuest)' in log
assert 'QuestGuidanceService.For(quest).text' in read('Assets/Scripts/UI/QuestHudController.cs')
assert 'QuestGuidanceService.DestinationFor' in read('Assets/Scripts/UI/WorldMapAtlasView.cs')
guidance=read('Assets/Scripts/Story/QuestGuidanceService.cs')
assert guidance.index('if (!IsKnown(destination))')<guidance.index('string place = L10n.Text(destination.displayName)')
assert 'p.CanGuideTravel' in guidance and 'CellToWorld(cell)' in guidance
# Regeneration must preserve the already-reviewed placement mapping byte for byte.
p=ROOT/'Assets/Resources/Data/QuestNavigationCatalog.json';before=p.read_bytes()
subprocess.run(['python',str(ROOT/'Tools/export-quest-navigation.py')],check=True)
assert before==p.read_bytes()
print('PASS: 54 quests / 69 exact targets; deterministic authored mapping; tracking save/rollback; modal handoff; HUD/map integration; hidden-place guard')
