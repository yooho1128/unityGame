"""Source integration boundaries. Native Unity UI, physics and serialization require Play Mode."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def source(path):return (ROOT/path).read_text()
def load(name):return json.loads(source('Assets/Resources/Data/'+name+'.json'))
regions={r['regionId']:r for r in load('RegionCatalog')['regions']}
layouts={r['regionId']:r for r in load('RegionLayoutCatalog')['entries']}
puzzles=load('RegionalPuzzleCatalog')['entries']
assert len({p['id'] for p in puzzles})==len({p['regionId'] for p in puzzles})==3
assert {p['kind'] for p in puzzles}=={'Sequence','Toggle','Mirrors'}
for p in puzzles:
    assert p['regionId'] in regions and p['clue'] and p['completionText']
    anchors={(c['x'],c['y']) for c in layouts[p['regionId']]['anchors']}
    cells=[p['board']]+p['nodes']+p['exits']
    assert len({(c['x'],c['y']) for c in cells})==6
    assert {(c['x'],c['y']) for c in cells}<=anchors
for path in ['Assets/Editor/PlayablePrologueGenerator.cs','Assets/Scripts/Field/ReleaseTerrainPresentation.cs']:
    text=source(path);start=text.index('JourneyHubStation.Ensure')
    assert start<text.index('RegionalPuzzleController.Ensure',start)<text.index('RegionFieldLayout.Apply',start)
controller=source('Assets/Scripts/Field/RegionalPuzzleController.cs')
for hook in ['SaveManager.CurrentChanged+=ResetState','SaveManager.CurrentChanged-=ResetState','SaveManager.SetFlag(d.CompletionFlag)','player.SnapToSafeCell','finally{Release();}','collider.isTrigger=action<=-2','JourneyHubPolicy.StationCell','if(!state.Solved)']:
    assert hook in controller,hook
for forbidden in ['SaveManager.AddGold','SaveManager.AddItem','MarkEncounterCleared','Notify(','AddExperience','FullHeal']:
    assert forbidden not in controller,forbidden
manager=source('Assets/Scripts/Battle/BattleManager.cs')
assert manager.index('BattleReviewCapture.EndCombat')<manager.index('yield return _presenter.PlayResult(_outcome)')<manager.index('OnBattleEnded?.Invoke(_outcome)')
flow=source('Assets/Scripts/Field/GameFlowController.cs')
start=flow.index('BattleReviewService.Begin')
assert flow.index('if (!battleManager.StartBattle(ctx))')<start<flow.index('yield return ScreenFader.Instance.FadeIn();',start)
assert flow.index('ApplyOutcome(ctx, outcome);')<flow.index('BattleReviewCapture.AfterSettlement')<flow.index('private void ApplyOutcome')
capture=source('Assets/Scripts/Battle/BattleReviewCapture.cs')
assert 'row.hpAfter=unit.Hp' in capture and 'ShadowInstance.ExperienceToNext' in capture
assert 'unit.HealingSkills.Used' in capture and 'entry.goldAfter=SaveManager.Current?.gold' in capture
service=source('Assets/Scripts/Battle/BattleReviewService.cs')
assert 'SaveKey(entry.saveGuid)!=key' in service and 'Path.Combine(Application.persistentDataPath,"BattleReviews"' in service
for forbidden in ['TrySave(','HealParty','AddGold(','AddItem(','Current.gold=','Current.party=']:
    assert forbidden not in service+capture
pause=source('Assets/Scripts/UI/FieldPauseMenuController.cs')
assert 'new BattleReviewView(root.transform,goldText.font)' in pause and '_review.Close()' in pause
assert 'ReviewPuzzleRegression.Validate(result.errors)' in source('Assets/Editor/GameRegressionValidator.cs')
print('PASS: 3 puzzle/6-point anchors, generated and existing scenes, isolated optional flags, safe local shortcut; battle pre-result HP/settlement order, real growth/healing metrics, sidecar-only storage and pause menu wiring')
