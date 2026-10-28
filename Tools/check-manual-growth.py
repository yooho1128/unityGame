"""Integration checks for manual input, capped XP grants and the north-portal camera frustum."""
import json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def read(p):return (ROOT/p).read_text()
limits=json.loads(read('Assets/Resources/Data/TrainingItemLimits.json'))['entries']
assert [(r['minimumLevel'],r['maximumLevel']) for r in limits]==[(1,20),(20,50),(50,80)]
manager=read('Assets/Scripts/Battle/BattleManager.cs')
for removed in ['IsAuto','SetAuto','ChooseAiAction(BattleSide.Player','_pendingAction != null ||']:assert removed not in manager,removed
assert 'new WaitUntil(() => _pendingAction != null)' in manager
assert 'new WaitUntil(() => _pendingForcedSwitch >= 0)' in manager
assert 'ChooseAiAction(BattleSide.Enemy' in manager
ui=read('Assets/Scripts/UI/BattleUIController.cs');assert 'oldAuto.gameObject.SetActive(false)' in ui and 'ToggleAuto' not in ui
assert '"Auto", topBar' not in read('Assets/Editor/BattleUIPrefabGenerator.cs')
assert 'autoBattle = save.autoBattle' not in read('Assets/Scripts/Field/GameFlowController.cs')
assert 'data.autoBattle = false' in read('Assets/Scripts/Save/SaveManager.cs')
save=read('Assets/Scripts/Save/SaveManager.cs');preview=save[save.index('public static bool TryPreviewFieldItem'):save.index('private static',save.index('public static bool TryPreviewFieldItem'))]
assert 'TrainingItemPolicy.TryGrant' in preview and 'restored = experience' in preview
use=save[save.index('public static bool TryUseFieldItem'):];assert use.index('if (!TryPreviewFieldItem')<use.index('AddItem(item.itemId,-1)')
proto=read('Prototype/shadow-theater.html');assert 'G.auto' not in proto and 'id="btnAuto"' not in proto and 'pa = aiChoose(PA()' not in proto
camera=read('Assets/Scripts/Field/FieldCameraFollow.cs');gen=read('Assets/Editor/PlayablePrologueGenerator.cs')
assert 'Mathf.Approximately(maxBounds.y, 2f)' in camera and 'maxBounds.y = 4.5f' in camera
assert 'new Vector2(7f, 4.5f)' in gen and 'new Vector2(-7f, -4.5f)' in gen
# Actual north exit y=8; released orthographic size=5.5. Camera center clamp previously 2.
assert 'camera.orthographicSize = 5.5f' in gen and 'newVector2Int(0,8)' in gen.replace(' ','')
for half_height in [5.28,5.5,5.62]:
 old=.5+(8-2)/(2*half_height);new=.5+(8-4.5)/(2*half_height)
 assert old>1 and .1<new<.9,(half_height,old,new)
marker=read('Assets/Scripts/Field/PortalExitMarker.cs')
assert 'portal.DestinationName' in marker and 'sprite.sortingOrder = 320' in marker and 'raycastTarget = false' in marker
assert 'AddComponent<PortalExitMarker>()' in read('Assets/Scripts/Field/ReleaseTerrainPresentation.cs')
print('PASS: manual-only player/forced switch input; legacy AUTO ignored; capped preview/use wired; north exit moves from outside frustum to 81–83% viewport; marker preserves input. Unity not executed.')
