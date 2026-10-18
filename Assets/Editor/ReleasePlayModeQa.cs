#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using L10n = ShadowTheater.UI.L10n;

namespace ShadowTheater.EditorTools
{
    /// <summary>Run release workflows in real Editor Play Mode with an isolated save identity.</summary>
    public static class ReleasePlayModeQa
    {
        private const string Key = "ShadowTheater.ReleasePlayQa.";
        private const string Folder = "Docs/Generated/ReleasePlayQa";
        private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
        private static double _started;
        private static bool _running;
        private static int _errors;
        private static readonly List<string> Notes = new List<string>();

        public static void RunForCi()
        {
            Directory.CreateDirectory(Folder);
            SessionState.SetString(Key + "Company", PlayerSettings.companyName);
            SessionState.SetString(Key + "Product", PlayerSettings.productName);
            PlayerSettings.companyName = "ShadowTheaterReleaseQA";
            PlayerSettings.productName = "ReleasePlayQA";
            SessionState.SetBool(Key + "Active", true);
            SessionState.SetBool(Key + "Finished", false);
            SessionState.SetInt(Key + "Exit", 1);
            var atlases = Resources.LoadAll<Sprite>("Art/Environment/ReleaseTerrain").Concat(Resources.LoadAll<Sprite>("Art/Environment/ReleaseProps")).Select(x => x.name).ToHashSet();
            var tiles = AssetDatabase.FindAssets("t:Tile", new[] { "Assets/Data/Generated/Tiles" });
            foreach (string guid in tiles)
            {
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid));
                string art = ReleaseTerrainPresentation.ArtFor(tile.name);
                Require(art != null && atlases.Contains(art), "Missing release terrain art: " + tile.name);
            }
            File.WriteAllText(Folder + "/TERRAIN.txt", "Release atlas coverage: " + tiles.Length + "/" + tiles.Length + " generated tiles PASS");
            EditorSceneManager.OpenScene("Assets/Scenes/Prologue/PrologueTheater.unity");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Attach()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= Capture;
            Application.logMessageReceived += Capture;
        }

        private static void Capture(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Key + "Active", false) || !Application.isPlaying ||
                type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message.Contains("QA_EXPECTED_SAVE_FAILURE")) return;
            _errors++;
            Notes.Add("RUNTIME ERROR: " + message + "\n" + stack);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key + "Active", false)) return;
            if (SessionState.GetBool(Key + "Finished", false))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                PlayerSettings.companyName = SessionState.GetString(Key + "Company", "DefaultCompany");
                PlayerSettings.productName = SessionState.GetString(Key + "Product", "Shadow Theater");
                AssetDatabase.SaveAssets();
                SessionState.SetBool(Key + "Active", false);
                EditorApplication.Exit(SessionState.GetInt(Key + "Exit", 1));
                return;
            }
            if (!Application.isPlaying) return;
            try
            {
                if (!_running)
                {
                    _running = true; _started = EditorApplication.timeSinceStartup;
                    Steps.Push(Scenarios());
                }
                Require(EditorApplication.timeSinceStartup - _started < 600, "Play workflow timeout");
                if (Steps.Count == 0) { Finish(_errors == 0); return; }
                var step = Steps.Peek();
                if (!step.MoveNext()) { Steps.Pop(); return; }
                if (step.Current is IEnumerator nested) Steps.Push(nested);
            }
            catch (Exception e)
            {
                Notes.Add("FAIL: " + e);
                Finish(false);
            }
        }

        private static void Finish(bool passed)
        {
            Notes.Add("Runtime errors=" + _errors);
            Notes.Add(passed ? "PASS" : "FAIL");
            File.WriteAllLines(Folder + "/WORKFLOWS.txt", Notes);
            SessionState.SetInt(Key + "Exit", passed ? 0 : 1);
            SessionState.SetBool(Key + "Finished", true);
            EditorApplication.ExitPlaymode();
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }

        private static IEnumerator Scenarios()
        {
            yield return Wait(2f);
            Require(SaveManager.Instance != null && PlayerController.Instance != null && FieldGrid.Current != null, "Field boot");
            var database = ShadowDatabase.Instance;
            var knight = database.GetShadow("mage");
            Require(knight != null, "Starter fixture");
            var save = SaveManager.Instance.NewGame(knight, 20);
            yield return Wait(.1f);
            Require(!PlayerController.Instance.IsLocked, "New save leaves movement available");

            var dialogue = DialogueController.Instance;
            int completed = 0, chosen = 0, cancelled = 0;
            Require(!dialogue.Play("qa_missing_dialogue", Color.white, () => completed++), "Missing dialogue rejects start");
            Require(completed == 0 && !PlayerController.Instance.IsLocked, "Missing dialogue grants no completion or lock");
            var sequence = new DialogueSequence { dialogueId = "qa_choice", choices = new List<DialogueChoice> { new DialogueChoice { text = "Choose" } } };
            Require(dialogue.PlayDynamic(sequence, Color.white, _ => { chosen++; dialogue.SelectChoice(0); }, () => completed++, () => cancelled++), "Choice starts");
            yield return Wait(.1f);
            dialogue.SelectChoice(0); dialogue.SelectChoice(0);
            Require(chosen == 1 && completed == 1 && !dialogue.IsPlaying && !PlayerController.Instance.IsLocked, "Choice is exactly once and releases lock");
            Require(dialogue.PlayDynamic(sequence, Color.white, null, () => completed++, () => cancelled++), "Cancelable dialogue starts");
            dialogue.Cancel();
            Require(cancelled == 1 && completed == 1 && !PlayerController.Instance.IsLocked, "Cancel is distinct from completion");
            Notes.Add("Dialogue missing data, repeated/reentrant selection, cancel and movement unlock PASS");

            var item = database.items.Find(x => x != null && !x.isKeyItem && x.buyPrice > 0 && x.sellPrice > 0);
            Require(item != null, "Trade fixture");
            save.gold = 1000; SaveManager.AddItem(item.itemId, 1);
            int gold = save.gold, owned = SaveManager.GetItemCount(item.itemId);
            Require(SaveManager.TryBuyItem(item, out _) && save.gold == gold - item.buyPrice && SaveManager.GetItemCount(item.itemId) == owned + 1, "Buy exchanges complete value");
            save.gold = int.MaxValue;
            owned = SaveManager.GetItemCount(item.itemId);
            Require(!SaveManager.TrySellItem(item, out _) && SaveManager.GetItemCount(item.itemId) == owned, "Gold cap does not discard sold item");
            save.gold = 1000;
            var stack = save.inventory.Find(x => x.itemId == item.itemId); stack.count = int.MaxValue;
            Require(!SaveManager.TryBuyItem(item, out _) && save.gold == 1000, "Stack cap does not charge buyer");
            stack.count = 2;
            SaveManager.SetFlag(TownServicePolicy.RumorCompletionFlag("prologue_theater"));
            save.party[0].currentHp = 1;
            var shop = SettlementShopController.Instance;
            int cost = TownServicePolicy.RestCost("prologue_theater", 30);
            shop.Open(); yield return Wait(.1f); shop.RestParty();
            Require(save.gold == 1000 - cost && save.party[0].currentHp == save.party[0].MaxHp, "Shop uses the town rest quote");
            shop.Close();
            Require(!PlayerController.Instance.IsLocked, "Shop closes without movement lock");
            Notes.Add("Purchases, full gold/stack safeguards, discounted shop rest and movement unlock PASS");

            var book = ScriptBookController.Instance;
            book.Open(); yield return Wait(.2f);
            Require(Field<ShadowData>(book, "_selectedData").shadowId == "mage", "Book initially selects owned starter instead of unknown entry");
            book.ShowRecorded(); yield return Wait(.2f);
            var lore = Field<Text>(book, "loreText");
            var scroll = lore.GetComponentInParent<ScrollRect>();
            Require(scroll != null && scroll.content == lore.rectTransform && scroll.viewport.rect.height > 0, "Book uses a nonzero lore viewport");
            Require(lore.text.Contains(L10n.Get("book.learned_skills", "Current skills 쨌 {0}").Split('{')[0]), "Book shows current skills");
            var bookRoot = Field<GameObject>(book, "root");
            var close = bookRoot.transform.Find("Close");
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, close.position) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Notes.Add("Book panel canvas=" + bookRoot.GetComponent<Canvas>().sortingOrder + " override=" + bookRoot.GetComponent<Canvas>().overrideSorting);
            Notes.Add("Book close raycast at " + pointer.position + ": " + string.Join(",", hits.Select(x => x.gameObject.name + "@" + x.sortingOrder)));
            Require(hits.Count > 0 && (hits[0].gameObject.transform == close || hits[0].gameObject.transform.IsChildOf(close)), "Book close is above the menu dock");
            ExecuteEvents.Execute(close.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Require(!book.IsOpen && !PlayerController.Instance.IsLocked, "Book close receives click and unlocks movement");
            Notes.Add("Book recorded filter, current skill guidance, lore viewport and close PASS");

            foreach (var modal in new MonoBehaviour[] { PartyStorageController.Instance, WorldMapController.Instance,
                SettlementShopController.Instance, FieldInventoryController.Instance, QuestLogController.Instance,
                BossInvestigationJournalController.Instance, FieldPauseMenuController.Instance })
            {
                modal.GetType().GetMethod("Open").Invoke(modal, null);
                yield return Wait(.1f);
                Require((bool)modal.GetType().GetProperty("IsOpen").GetValue(modal), "Modal opens: " + modal.name);
                var panel = Field<GameObject>(modal, "root");
                var button = panel.GetComponentsInChildren<Button>().First(x => x.name == "Close");
                Canvas.ForceUpdateCanvases();
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
                Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "Modal close receives top raycast: " + modal.name);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Require(!(bool)modal.GetType().GetProperty("IsOpen").GetValue(modal) && !PlayerController.Instance.IsLocked, "Modal click closes and unlocks: " + modal.name);
                Notes.Add("Modal open, close hit-test, actual pointer click and movement unlock: " + modal.name + " PASS");
            }

            var original = save.party[0];
            Require(SaveManager.Instance.TrySave() && SaveManager.Instance.Load(), "Save round trip");
            Require(!MemoryAwakeningService.TryRestore(original, out _), "Previous save object cannot awaken");
            save = SaveManager.Current;
            var current = save.party[0]; current.level = 120;
            if (!string.IsNullOrEmpty(current.Data.restoredForm.requiredFlag)) SaveManager.SetFlag(current.Data.restoredForm.requiredFlag);
            current.currentHp = 0;
            Require(MemoryAwakeningService.TryRestore(current, out _) && current.currentHp == 0, "Restore cannot revive fainted unit");
            SaveManager.HealParty();
            Notes.Add("Save/load round trip, old-object ownership rejection and fainted restore PASS");

            Action fail = () => throw new IOException("QA_EXPECTED_SAVE_FAILURE");
            SaveManager.BeforeSave += fail;
            try
            {
                var pause = FieldPauseMenuController.Instance;
                pause.Open(); pause.ConfirmReturnToTitle(); yield return Wait(.2f);
                Require(pause.IsOpen && !Field<bool>(pause, "_returning") && SceneManager.GetActiveScene().name == "PrologueTheater", "Save failure keeps current game available");
                pause.Close();
                Require(!PlayerController.Instance.IsLocked, "Failed title return unlocks after menu close");
            }
            finally { SaveManager.BeforeSave -= fail; }
            Require(SaveManager.Instance.TrySave(), "Save succeeds after injected fault removal");
            Notes.Add("Injected save failure prevents title loss; subsequent retry succeeds PASS");

            foreach (string scene in new[] { "EchoVillage", "MoonlitMeadow", "PrologueTheater" })
            {
                yield return Wait(1f);
                var region = RegionRepository.GetByScene(scene);
                Require(MapLoader.Instance.TravelTo(scene, region.ArrivalCell, FacingDir.Down), "Travel accepted: " + scene);
                float deadline = Time.unscaledTime + 25f;
                while (MapLoader.Instance != null && MapLoader.Instance.IsTransitioning && Time.unscaledTime < deadline) yield return null;
                yield return Wait(.3f);
                Require(MapLoader.Instance != null && !MapLoader.Instance.IsTransitioning && SceneManager.GetActiveScene().name == scene &&
                    SaveManager.Current.mapId == scene && !PlayerController.Instance.IsLocked, "Travel arrival/save/unlock: " + scene);
                Notes.Add("Real scene travel " + scene + " PASS");
            }

            // Scene coverage uses an explicitly unlocked QA save, not a story completion claim.
            foreach (var unlocked in RegionRepository.All)
            {
                if (!SaveManager.Current.unlockedRegionIds.Contains(unlocked.regionId)) SaveManager.Current.unlockedRegionIds.Add(unlocked.regionId);
                if (!string.IsNullOrEmpty(unlocked.requiredFlag)) SaveManager.SetFlag(unlocked.requiredFlag);
            }
            foreach (var region in RegionRepository.Ordered().ToArray())
            {
                if (SceneManager.GetActiveScene().name == region.sceneName) continue;
                yield return Wait(.8f);
                Require(MapLoader.Instance.TravelTo(region.sceneName, region.ArrivalCell, FacingDir.Down), "Full field tour accepted: " + region.sceneName);
                float deadline = Time.unscaledTime + 25f;
                while (MapLoader.Instance.IsTransitioning && Time.unscaledTime < deadline) yield return null;
                yield return Wait(.12f);
                Require(!MapLoader.Instance.IsTransitioning && SceneManager.GetActiveScene().name == region.sceneName && FieldGrid.Current != null &&
                    SaveManager.Current.mapId == region.sceneName && !PlayerController.Instance.IsLocked, "Full field arrival: " + region.sceneName);
                Require(!FieldGrid.Current.IsBlocked(PlayerController.Instance.Cell, PlayerController.Instance.GetComponent<Collider2D>(), false), "Walkable arrival: " + region.sceneName);
                foreach (var map in UnityEngine.Object.FindObjectsOfType<Tilemap>())
                {
                    var used = new TileBase[map.GetUsedTilesCount()]; map.GetUsedTilesNonAlloc(used);
                    foreach (var tile in used) Require(tile == null || tile.name.EndsWith("_Release"), "Unstyled live tile: " + tile?.name);
                }
                Notes.Add("Field tour, grid, release tile art, save and controls: " + region.sceneName + " PASS");
            }
            yield return Wait(.8f);
            var home = RegionRepository.GetByScene("PrologueTheater");
            Require(MapLoader.Instance.TravelTo(home.sceneName, home.ArrivalCell, FacingDir.Down), "Return home");
            while (MapLoader.Instance.IsTransitioning) yield return null;
            yield return Wait(.3f);

            SaveManager.Current.autoBattle = true;
            SaveManager.Current.battleSpeed = 3f;
            bool battleFinished = false;
            GameFlowController.Instance.StartScriptedBattle(BattleMode.Wild,
                new List<ShadowInstance> { new ShadowInstance(database.GetShadow("puppet"), 1) }, "qa_workflow_battle", outcome => battleFinished = outcome.result == BattleResult.Victory);
            float battleDeadline = Time.unscaledTime + 45f;
            while (GameFlowController.Instance.IsInBattle && Time.unscaledTime < battleDeadline)
            {
                var ui = UnityEngine.Object.FindObjectOfType<BattleUIController>();
                if (ui != null) ui.ConfirmResult();
                yield return null;
            }
            Require(battleFinished && !GameFlowController.Instance.IsInBattle && !PlayerController.Instance.IsLocked, "Battle victory returns to controllable field");
            Notes.Add("Real battle start, auto resolution, result confirmation, field return and movement unlock PASS");
        }

        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
