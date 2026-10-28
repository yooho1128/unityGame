"""Static integration checks. Unity state-machine regressions are committed separately."""
from pathlib import Path
import re

root=Path(__file__).resolve().parents[1]
read=lambda p:(root/p).read_text()
egg=read('Assets/Scripts/Story/BackstageEasterEgg.cs')
ritual=read('Assets/Scripts/Story/BackstageRitual.cs')
save=read('Assets/Scripts/Save/SaveManager.cs')
dialogue=read('Assets/Scripts/Story/DialogueInteractable.cs')
player=read('Assets/Scripts/Field/PlayerController.cs')
generator=read('Assets/Editor/PlayablePrologueGenerator.cs')
assert 'new Vector2Int(-3, 3)' in generator and '"shrine_first_script"' in generator
assert 'player.Cell == new Vector2Int(-3, 2)' in egg and 'player.Facing == FacingDir.Up' in egg
assert 'SceneManager.GetActiveScene().name == "PrologueTheater"' in egg
assert 'Readings == 3 && Rehearsals == 3' in ritual
assert 'ItemId = "rehearsal_script"' in ritual
assert dialogue.index('BackstageEasterEgg.TryOpen(dialogueId)')<dialogue.index('controller.Play(dialogueId')
assert 'ritualReading?.Invoke()' in dialogue
assert save.count('BackstageEasterEgg.OnFieldItemUsed(item.itemId)')==2
use=save[save.index('public static bool TryUseFieldItem('):save.index('public static Dictionary<ItemData, int> BuildBattleInventory')]
assert use.index('TryPreviewFieldItem')<use.index('BackstageEasterEgg.OnFieldItemUsed')
assert 'BackstageEasterEgg.OnPlayerStep();' in player
assert 'SceneManager.activeSceneChanged += OnSceneChanged' in egg
assert 'ReferenceEquals(save, SaveManager.Current)' in egg
execute=egg[egg.index('private static void Execute('):]
assert execute.index('HasFlag(UnlockedFlag)')<execute.index('TryCreateDeveloperBackup')
assert execute.index('TryCreateDeveloperBackup')<execute.index('SaveManager.AddGold')
assert '!i.isKeyItem' in execute and '!i.isEventOnly' in execute
assert 'Mathf.Min(ShadowInstance.MaxLevel, member.level + 5)' in execute
assert 'SaveManager.SetFlag(UsedFlag)' in execute and 'TrySave()' in execute
assert 'backstage-backups' in save and 'File.Copy(SavePath, path, false)' in save
assert 'PlayDynamic' in egg and 'KeyCode' not in egg
assert '#if' not in egg  # deliberately supports normal release builds
for obsolete in ('OwnerCheatAccess.cs','OwnerCheatConsole.cs'):
    assert not (root/'Assets/Scripts/UI'/obsolete).exists()
print('PASS: world shrine/item trigger, success-only hooks, movement/scene/save resets, dialogue menus, pre-command backups, release availability; not Unity execution')
