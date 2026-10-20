#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShadowTheater.EditorTools
{
    public static class MobilePlayabilityChecks
    {
        public const string Folder = "Docs/Generated/Playability";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void GenerateAndCheckForCi()
        {
            GameRegressionValidator.GenerateAndValidateForCi();
            CheckAndCaptureForCi();
        }
        public static void GenerateCheckAndBuildForCi()
        {
            GenerateAndCheckForCi();
            ReleaseReadinessBatch.BuildAndroidQaForCi();
        }

        public static void CheckAndCaptureForCi()
        {
            Directory.CreateDirectory(Folder);
            var notes = new List<string>();
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            var quality = QualitySettings.renderPipeline;
            try
            {
                GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
                CheckArtAndBalance(notes);
                CheckTouchInput(notes);
                foreach (Vector2Int size in new[] { new Vector2Int(2184, 1968), new Vector2Int(2400, 1080), new Vector2Int(1920, 1080) })
                {
                    CheckSwitches(size, notes);
                    CheckAttackPresentation(size, notes);
                    CheckBattleFeedback(size, notes);
                    CheckBook(size, notes);
                    CaptureField(size, notes);
                }
            }
            finally { GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = quality; }
            File.WriteAllLines(Folder + "/CHECKS.txt", notes);
            Debug.Log("[MobilePlayability] PASS: " + string.Join(" · ", notes));
        }

        public static void RefreshUiAndCheckForCi()
        {
            MobileUiPolish.ApplyToGeneratedPrefabs();
            GameRegressionValidator.ValidateForCi();
            CheckAndCaptureForCi();
        }

        private static void CheckSwitches(Vector2Int size, List<string> notes)
        {
            using (var preview = new Preview(size))
            {
                GameObject root = preview.Prefab("BattleCanvas");
                var manager = root.GetComponent<BattleManager>();
                var ui = root.GetComponent<BattleUIController>();
                var data = Load("knight");
                var units = (List<BattleUnit>)typeof(BattleManager).GetField("_playerUnits", Private).GetValue(manager);
                for (int i = 0; i < 6; i++) units.Add(new BattleUnit(new ShadowInstance(data, 5), BattleSide.Player));
                var enemies = (List<BattleUnit>)typeof(BattleManager).GetField("_enemyUnits", Private).GetValue(manager);
                enemies.Add(new BattleUnit(new ShadowInstance(Load("puppet"), 6), BattleSide.Enemy));
                SetProperty(manager, "Context", new BattleContext { mode = BattleMode.Wild });
                SetProperty(manager, "State", BattleState.WaitingForInput);
                Invoke(ui, "Awake");
                Invoke(ui, "RefreshAll");
                // Bind uses a disabled prefab template; input must work regardless of Awake timing.
                ui.OpenSwitches(); preview.Layout(root);
                var first = root.GetComponentsInChildren<BattleOptionButton>().First();
                RectTransform rect = (RectTransform)first.transform;
                Require(rect.rect.width >= 400, "Switch row collapsed: " + rect.rect.width);
                Require(rect.rect.height >= 80, "Switch row too short: " + rect.rect.height);
                preview.Save("SwitchBeforeClick");
                preview.Click(first.GetComponent<Button>());
                var action = (BattleAction)typeof(BattleManager).GetField("_pendingAction", Private).GetValue(manager);
                Require(action != null && action.type == ActionType.Switch && action.switchIndex == 1,
                    "Pointer selection did not submit switch to slot 2");
                RemovePreviewOptions(ui);
                SetProperty(manager, "State", BattleState.ForcedSwitch);
                ui.OpenSwitches(); preview.Layout(root);
                var scroll = root.GetComponentsInChildren<ScrollRect>().First(); scroll.verticalNormalizedPosition = 0;
                preview.Layout(root);
                var last = root.GetComponentsInChildren<BattleOptionButton>().Last();
                preview.Save("SwitchScrolled");
                preview.Click(last.GetComponent<Button>());
                int pending = (int)typeof(BattleManager).GetField("_pendingForcedSwitch", Private).GetValue(manager);
                Require(pending == 5, "Forced switch pointer selection to slot 6 failed");
                RemovePreviewOptions(ui);
                ui.OpenSwitches(); preview.Layout(root);
                preview.Save("Switch");
                notes.Add($"{size.x}x{size.y}: normal + forced switch pointer raycast PASS, six-member scrolling PASS");
            }
        }

        private static void CheckAttackPresentation(Vector2Int size, List<string> notes)
        {
            using (var preview = new Preview(size))
            {
                GameObject root = preview.Prefab("BattleCanvas");
                var ui = root.GetComponent<BattleUIController>();
                var player = (BattleUnitPanel)typeof(BattleUIController).GetField("playerPanel", Private).GetValue(ui);
                var enemy = (BattleUnitPanel)typeof(BattleUIController).GetField("enemyPanel", Private).GetValue(ui);
                preview.Layout(root);
                Vector3 playerImageHome = player.Portrait.rectTransform.position, enemyImageHome = enemy.Portrait.rectTransform.position;
                Invoke(player, "Awake"); Invoke(enemy, "Awake");
                player.Bind(new BattleUnit(new ShadowInstance(Load("knight"), 5), BattleSide.Player));
                enemy.Bind(new BattleUnit(new ShadowInstance(Load("mage"), 5), BattleSide.Enemy));
                preview.Layout(root);
                Require(Vector3.Distance(player.Portrait.rectTransform.position, playerImageHome) < .01f &&
                    Vector3.Distance(enemy.Portrait.rectTransform.position, enemyImageHome) < .01f, "Portrait moved while separating HUD");
                Require(player.MotionRoot != player.transform && enemy.MotionRoot != enemy.transform, "Attack still moves HUD panel");
                var hud = new[] { player.transform, enemy.transform };
                var homes = hud.Select(t => t.position).ToArray();
                var actorHomes = new[] { player.MotionRoot.localPosition, enemy.MotionRoot.localPosition };
                var health = root.GetComponentsInChildren<Text>().Select(t => t.transform.position).ToArray();
                var skill = ScriptableObject.CreateInstance<SkillData>();
                try
                {
                    foreach (bool reverse in new[] { false, true })
                    foreach (bool magical in new[] { false, true })
                    foreach (bool miss in new[] { false, true })
                    {
                        skill.damageType = magical ? DamageType.Magical : DamageType.Physical;
                        skill.element = magical ? ShadowElement.Flame : ShadowElement.Shade;
                        var director = root.GetComponent<BattleFxDirector>();
                        var layer = (RectTransform)typeof(BattleFxDirector).GetMethod("CreateLayer", Private).Invoke(director, new object[] { "AttackPreview" });
                        preview.Layout(root);
                        RectTransform targetPortrait = (reverse ? player : enemy).Portrait.rectTransform;
                        Vector2 targetCenter = layer.InverseTransformPoint(targetPortrait.TransformPoint(targetPortrait.rect.center));
                        int impacts = 0;
                        using (var attack = new BattleAttackPresentation(layer, reverse ? enemy : player, reverse ? player : enemy, skill, miss, () => impacts++))
                        {
                            foreach (float p in new[] { 0f, .15f, .35f, .53f, .65f, .85f, 1f })
                            {
                                attack.Sample(p); preview.Layout(root);
                                Require(impacts == (p < .52f ? 0 : 1), "Impact callback timing/duplicate");
                                for (int i = 0; i < hud.Length; i++) Require(Vector3.Distance(hud[i].position, homes[i]) < .01f, "Attack moved panel");
                                var now = root.GetComponentsInChildren<Text>().Select(t => t.transform.position).ToArray();
                                for (int i = 0; i < health.Length; i++) Require(Vector3.Distance(now[i], health[i]) < .01f, "Attack moved HP/name label");
                                var projectile = layer.Find("ElementProjectile").GetComponent<Image>();
                                Require(projectile.enabled == (magical && p >= .22f && p < .52f), "Projectile travel timing");
                                Require(layer.Find("ImpactRing").GetComponent<Image>().enabled == (!miss && p >= .52f && p < .9f), "Impact on missed/early attack");
                                if (!miss && p >= .52f && p < .9f)
                                    Require(Vector2.Distance(((RectTransform)layer.Find("ImpactRing")).anchoredPosition, targetCenter) < .01f, "Impact missed portrait center");
                                foreach (Image effect in layer.GetComponentsInChildren<Image>()) Require(effect.sprite != null && !effect.raycastTarget, "Square or input-blocking effect");
                                if (!reverse && !miss && (p == .35f && magical || p == .65f))
                                    preview.Save((magical ? "Magic" : "Melee") + "Attack_" + (p < .52f ? "Travel" : "Impact"));
                            }
                            attack.Sample(.65f); // Interrupted mid-impact: Dispose must restore both actors.
                            Require(impacts == 1, "Impact replayed during recovery");
                        }
                        Require(Vector3.Distance(player.MotionRoot.localPosition, actorHomes[0]) < .01f && Vector3.Distance(enemy.MotionRoot.localPosition, actorHomes[1]) < .01f, "Attack restoration failed");
                    }
                }
                finally { Object.DestroyImmediate(skill); }
                notes.Add($"{size.x}x{size.y}: melee/magic, both sides, miss, stationary HP/name HUD, interruption recovery PASS");
            }
        }

        private static void CheckTouchInput(List<string> notes)
        {
            using (var preview = new Preview(new Vector2Int(1920,1080)))
            {
                var player = new GameObject("TouchPlayer").AddComponent<PlayerController>(); Invoke(player, "Awake");
                var up = new GameObject("Up").AddComponent<VirtualDPadButton>();
                var left = new GameObject("Left").AddComponent<VirtualDPadButton>();
                typeof(VirtualDPadButton).GetField("direction", Private).SetValue(left, Vector2.left);
                var owner = new PointerEventData(EventSystem.current) { pointerId = 11, button = PointerEventData.InputButton.Left, pointerPress = up.gameObject };
                var other = new PointerEventData(EventSystem.current) { pointerId = 12, button = PointerEventData.InputButton.Left };
                VirtualDPadButton.CancelInput();
                up.OnPointerDown(owner); Require(player.MoveInput == Vector2.up, "DPad press");
                left.OnPointerDown(other); left.OnPointerUp(other);
                Require(player.MoveInput == Vector2.up, "Second finger stole/released DPad");
                up.OnPointerExit(owner); Require(player.MoveInput == Vector2.zero, "DPad exit did not stop");
                left.OnPointerEnter(owner); Require(player.MoveInput == Vector2.left, "Finger slide lost direction");
                up.OnPointerUp(owner); Require(player.MoveInput == Vector2.zero, "Original pointer-up did not release slid button");
                left.OnPointerEnter(other); Require(player.MoveInput == Vector2.zero, "Unrelated menu drag started movement");
                up.OnPointerDown(owner); player.Lock(); Require(player.MoveInput == Vector2.zero, "Menu lock retained movement");
                left.OnPointerDown(other); Require(player.MoveInput == Vector2.zero, "Locked player accepted touch"); player.Unlock();
                up.OnPointerDown(owner);
                typeof(VirtualDPadButton).GetMethod("OnApplicationPause", Private).Invoke(up, new object[] { true });
                Require(player.MoveInput == Vector2.zero, "Pause retained touch");
                up.OnPointerDown(owner);
                typeof(VirtualDPadButton).GetMethod("OnApplicationFocus", Private).Invoke(up, new object[] { false });
                Require(player.MoveInput == Vector2.zero, "Focus loss retained touch");
                up.OnPointerDown(owner); Invoke(up, "OnDisable"); Require(player.MoveInput == Vector2.zero, "Disabled control retained touch");
                owner.button = PointerEventData.InputButton.Right; up.OnPointerDown(owner);
                Require(player.MoveInput == Vector2.zero, "Right click started movement");
                VirtualDPadButton.CancelInput();
                notes.Add("DPad: pointer ownership, second finger, sliding release, unrelated drag, menu lock, pause/focus loss, disable PASS");
            }
        }

        private static void CheckBattleFeedback(Vector2Int size, List<string> notes)
        {
            using (var preview = new Preview(size))
            {
                var root = preview.Prefab("BattleCanvas");
                var manager = root.GetComponent<BattleManager>(); var ui = root.GetComponent<BattleUIController>();
                var active = new BattleUnit(new ShadowInstance(Load("knight"),5), BattleSide.Player);
                var enemy = new BattleUnit(new ShadowInstance(Load("mage"),5), BattleSide.Enemy);
                ((List<BattleUnit>)typeof(BattleManager).GetField("_playerUnits", Private).GetValue(manager)).Add(active);
                ((List<BattleUnit>)typeof(BattleManager).GetField("_enemyUnits", Private).GetValue(manager)).Add(enemy);
                SetProperty(manager, "Context", new BattleContext { mode = BattleMode.Wild });
                SetProperty(manager, "State", BattleState.WaitingForInput);
                Invoke(ui, "Awake"); Invoke(ui, "RefreshAll"); preview.Layout(root);
                var panel = (BattleUnitPanel)typeof(BattleUIController).GetField("playerPanel", Private).GetValue(ui);
                var enemyPanel = (BattleUnitPanel)typeof(BattleUIController).GetField("enemyPanel", Private).GetValue(ui);
                Invoke(panel, "Awake"); Invoke(enemyPanel, "Awake");
                float full = panel.DisplayedHp;
                int damage = active.TakeDamage(active.MaxHp / 3);
                var hit = new HitResult { damage = damage, critical = true, elementMultiplier = 1.5f };
                typeof(BattleUIController).GetMethod("PresentHit", Private).Invoke(ui, new object[] { active, hit });
                Require(Mathf.Approximately(panel.DisplayedHp,full), "Health snapped before animation");
                panel.AdvanceHealthPresentation(.14f);
                Require(panel.DisplayedHp < full && panel.DisplayedHp > active.Hp, "Health did not interpolate");
                panel.Bind(active); // repeated state events must not restart the drain.
                panel.AdvanceHealthPresentation(.14f);
                Require(Mathf.Approximately(panel.DisplayedHp,active.Hp), "Repeated bind restarted HP animation");
                var popup = root.GetComponentsInChildren<BattleFloatingFeedback>().Single();
                Require(!popup.GetComponent<Text>().raycastTarget && !popup.GetComponent<CanvasGroup>().blocksRaycasts, "Damage number blocks controls");
                Require(popup.GetComponent<Text>().text.Contains(damage.ToString()), "Wrong actual damage number");
                Vector3 start = popup.transform.localPosition; popup.Sample(.4f);
                Require(popup.transform.localPosition.y > start.y, "Damage number did not float");
                Require(popup.GetComponent<CanvasGroup>().alpha == 1f, "Damage number fades before reading interval");
                preview.Save("DamageFeedback");
                var hpLabel = (Text)typeof(BattleUnitPanel).GetField("hpText", Private).GetValue(panel);
                Require(hpLabel.cachedTextGenerator.lineCount == 1, "HP number wrapped on mobile screen");
                popup.Sample(1f);
                Require(popup.GetComponent<CanvasGroup>().alpha == 0f, "Feedback did not fade"); Object.DestroyImmediate(popup.gameObject);
                int healed = active.Heal(20); panel.Bind(active); panel.AdvanceHealthPresentation(.28f);
                Require(Mathf.Approximately(panel.DisplayedHp,active.Hp) && healed > 0, "Healing HP animation");
                var bench = new BattleUnit(new ShadowInstance(Load("beast"),5), BattleSide.Player);
                var healing = ui.PlayHeal(bench,10); healing.MoveNext(); (healing as IDisposable)?.Dispose();
                Require(panel.Represents(active), "Bench healing replaced active portrait");
                active.TakeDamage(int.MaxValue); panel.Bind(active); panel.AdvanceHealthPresentation(1f);
                Require(panel.DisplayedHp == 0f, "Fainted HP below/above zero");
                enemyPanel.Bind(enemy);
                panel.Bind(bench); Require(Mathf.Approximately(panel.DisplayedHp,bench.Hp), "Switch inherited previous HP animation");
                Require(!string.IsNullOrWhiteSpace(BattleFloatingFeedback.HitText(new HitResult { missed = true })) &&
                    !BattleFloatingFeedback.HitText(new HitResult { missed = true }).Contains("−"), "Miss feedback shows damage");
                notes.Add($"{size.x}x{size.y}: damage/heal HP tween, repeated bind, actual numbers, no raycast, float/fade, bench heal, faint/switch PASS");
            }
        }

        private static void CheckBook(Vector2Int size, List<string> notes)
        {
            using (var preview = new Preview(size))
            {
                GameObject root = preview.Prefab("ScriptBookCanvas");
                root.transform.Find("BookRoot").gameObject.SetActive(true);
                var template = root.GetComponentsInChildren<ScriptBookEntryView>(true).First();
                for (int i = 0; i < 8; i++)
                {
                    var row = Object.Instantiate(template, template.transform.parent);
                    row.Bind(Load(i % 2 == 0 ? "knight" : "mage"), i, ScriptBookEntryState.Recorded, _ => { });
                }
                var detail = root.transform.Find("BookRoot/DetailPanel");
                detail.Find("Name").GetComponent<Text>().text = "잊힌 왕국의 기사";
                detail.Find("Title").GetComponent<Text>().text = "기억을 되찾은 첫 번째 그림자";
                detail.Find("LoreScroll/Lore").GetComponent<Text>().text = "마지막 무대에 남은 기사는 잃어버린 동료의 이름을 기억합니다.\n각본집에서 그림자의 이야기와 성장 조건을 읽을 수 있습니다.";
                detail.Find("Portrait").GetComponent<Image>().sprite = Load("knight").silhouetteSprite;
                preview.Layout(root);
                var rowRect = (RectTransform)root.GetComponentsInChildren<ScriptBookEntryView>().First().transform;
                Require(rowRect.rect.width >= 260, "Book row collapsed");
                foreach (string name in new[] { "Name", "Title", "Lore", "Stats" })
                    Require(((RectTransform)detail.Find(name == "Lore" ? "LoreScroll/Lore" : name)).rect.width >= 260, "Book reading area too narrow: " + name);
                preview.Save("Book"); notes.Add($"{size.x}x{size.y}: book row width + reading area PASS");
            }
        }

        private static void CaptureField(Vector2Int size, List<string> notes)
        {
            using (var preview = new Preview(size, false))
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Prologue/PrologueTheater.unity");
                foreach (var map in Object.FindObjectsOfType<UnityEngine.Tilemaps.Tilemap>())
                    map.gameObject.AddComponent<ReleaseTerrainPresentation>().Apply(map);
                foreach (var portal in Object.FindObjectsOfType<MapPortal>())
                    ReleaseTerrainPresentation.StylePortal(portal);
                var field = GameObject.Find("FieldRoot");
                if (field == null) field = Object.FindObjectOfType<FieldGrid>().transform.parent.gameObject;
                Camera camera = Object.FindObjectsOfType<Camera>().First(x => x.orthographic);
                camera.GetComponent<FieldCameraFollow>()?.Snap();
                camera.targetTexture = preview.Render;
                foreach (Camera other in Object.FindObjectsOfType<Camera>()) if (other != camera) other.enabled = false;
                preview.Camera = camera;
                foreach (var renderer in Object.FindObjectsOfType<UnityEngine.Tilemaps.TilemapRenderer>())
                    renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                foreach (Canvas canvas in field.GetComponentsInChildren<Canvas>(true)) preview.Configure(canvas.gameObject);
                foreach (DialogueController dialogue in field.GetComponentsInChildren<DialogueController>(true))
                {
                    var panel = new SerializedObject(dialogue).FindProperty("panelRoot").objectReferenceValue as GameObject;
                    panel?.SetActive(false);
                }
                foreach (MonoBehaviour popup in field.GetComponentsInChildren<MonoBehaviour>(true).Where(x => x is SaveFeedbackController || x is QuestCompletionToast))
                {
                    var group = new SerializedObject(popup).FindProperty("root").objectReferenceValue as CanvasGroup;
                    if (group != null) group.alpha = 0;
                }
                foreach (QuestHudController hud in field.GetComponentsInChildren<QuestHudController>(true))
                {
                    Transform panel = hud.transform.Find("QuestPanel");
                    panel.Find("Title").GetComponent<Text>().text = "막이 오르다";
                    panel.Find("Objectives").GetComponent<Text>().text = "□ 아리아와 대화하기";
                    panel.Find("Reward").GetComponent<Text>().text = "보상 50 금화";
                }
                var player = field.GetComponentInChildren<PlayerController>();
                var follower = new GameObject("CompanionPreview", typeof(SpriteRenderer));
                follower.transform.position = player.transform.position + Vector3.left;
                var sprite = follower.GetComponent<SpriteRenderer>(); sprite.sprite = Load("mage").fieldFrames[0]; sprite.sortingOrder = 9;
                FieldSpriteSizing.Fit(sprite, .92f);
                Require(sprite.bounds.size.y <= .93f && sprite.bounds.size.x <= 1.16f, "Companion exceeds one tile");
                preview.Layout(field); preview.Save("Field");
                notes.Add($"{size.x}x{size.y}: field controls + companion one-tile bounds PASS (preview renderer)");
            }
        }

        private static void CheckArtAndBalance(List<string> notes)
        {
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>("Assets/Resources/ShadowDatabase.asset");
            foreach (ShadowData data in database.shadows)
            {
                Require(data.fieldFrames != null && data.fieldFrames.Length >= 2, "No two-frame field art: " + data.shadowId);
                foreach (Sprite sprite in data.fieldFrames) Require(sprite != null && EditorUtility.IsPersistent(sprite) &&
                    sprite.rect.width == 24 && sprite.rect.height == 32, "Invalid 24x32 field sprite: " + data.shadowId);
            }
            var region = RegionRepository.Get("moonlit_meadow");
            RegionalEncounterPolicy.GetCadence(region, out int min, out int max, out float chance, out int pity);
            Require(min == 14 && max == 20 && chance <= .08f && pity == 24, "Intro encounter recovery window failed");
            Require(IntroEncounterPolicy.ClampLevel(region, 10, new[] { new ShadowInstance(Load("mage"), 5) }) == 4, "Solo intro level cap failed");
            Require(IntroEncounterPolicy.ClampLevel(region, 10, new[] { new ShadowInstance(Load("mage"), 5), new ShadowInstance(Load("knight"), 5) }) == 6, "Two-member intro level cap failed");
            Require(!IntroEncounterPolicy.IsAvailable(region, Load("beast"), new[] { new ShadowInstance(Load("mage"), 5) }), "Rare encounter not gated for solo beginner");
            Require(IntroEncounterPolicy.ClampLevel(RegionRepository.Get("ash_border"), 18, null) == 18, "Later regions unexpectedly changed");
            notes.Add($"{database.shadows.Count} field-art bindings 24x32 two-frame PASS; solo intro Lv.4 / duo Lv.6 caps + rare gate + recovery cadence PASS");
            SimulateIntro(database, region, notes);
        }

        private static void SimulateIntro(ShadowDatabase database, RegionData region, List<string> notes)
        {
            var manifest = JsonUtility.FromJson<Roster>(File.ReadAllText("Assets/Resources/Data/RegionalRosterManifest.json"));
            var ids = new HashSet<string>(manifest.entries.Where(x => x.regionId == region.regionId).Select(x => x.shadowId));
            foreach (var shadow in database.shadows)
                if ((region.featuredShadows ?? Array.Empty<string>()).Contains(shadow.displayName) && RegionalEncounterPolicy.IsEligible(region, shadow)) ids.Add(shadow.shadowId);
            var lines = new List<string> { "starter\tenemy\tenemyLevel\twins\ttrials\twinRate\taverageSurvivingHp" };
            var originalRandom = UnityEngine.Random.state;
            try
            {
                foreach (string starter in new[] { "knight", "mage", "beast" })
                {
                    int totalWins = 0, total = 0;
                    foreach (string id in ids.OrderBy(x => x))
                    {
                        var party = new[] { new ShadowInstance(Load(starter), 5) };
                        if (!IntroEncounterPolicy.IsAvailable(region, Load(id), party)) continue;
                        int enemyLevel = IntroEncounterPolicy.ClampLevel(region, 6, party);
                        int wins = 0; float remaining = 0;
                        for (int trial = 0; trial < 128; trial++)
                        {
                            UnityEngine.Random.InitState(913 + trial);
                            var a = new BattleUnit(new ShadowInstance(Load(starter), 5), BattleSide.Player);
                            var b = new BattleUnit(new ShadowInstance(Load(id), enemyLevel), BattleSide.Enemy);
                            int afp = 0, bfp = 0;
                            for (int turn = 0; turn < 80 && !a.IsFainted && !b.IsFainted; turn++)
                            {
                                afp = Math.Min(5, afp + 1); bfp = Math.Min(5, bfp + 1);
                                var aa = BattleAI.Choose(a, b, afp, BattleAI.ProfileFor(a.Data, false));
                                var ba = BattleAI.Choose(b, a, bfp, BattleAI.ProfileFor(b.Data, false));
                                bool aFirst = a.Spd > b.Spd || a.Spd == b.Spd && UnityEngine.Random.value < .5f;
                                if (aFirst) { Act(a, b, aa.skill, ref afp); if (!b.IsFainted) Act(b, a, ba.skill, ref bfp); }
                                else { Act(b, a, ba.skill, ref bfp); if (!a.IsFainted) Act(a, b, aa.skill, ref afp); }
                                if (!a.IsFainted) { a.TakeDamage(a.GetDotDamage()); a.TickStatus(); }
                                if (!b.IsFainted) { b.TakeDamage(b.GetDotDamage()); b.TickStatus(); }
                            }
                            if (!a.IsFainted && b.IsFainted) { wins++; remaining += a.HpRatio; }
                        }
                        totalWins += wins; total += 128;
                        lines.Add($"{starter}\t{id}\t{enemyLevel}\t{wins}\t128\t{wins / 128f:0.000}\t{(wins > 0 ? remaining / wins : 0):0.000}");
                        Require(wins / 128f >= .65f, "Intro matchup win rate below 65%: " + starter + " / " + id);
                    }
                    notes.Add($"{starter} Lv.5 solo vs available introductory roster: {totalWins}/{total} automated duels won");
                    Require(total > 0 && totalWins / (float)total >= .80f, "Intro balance: starter win rate below 80%: " + starter);
                }
            }
            finally { UnityEngine.Random.state = originalRandom; File.WriteAllLines(Folder + "/INTRO_BALANCE.tsv", lines); }
        }
        private static void Act(BattleUnit actor, BattleUnit target, SkillData skill, ref int fp)
        {
            if (!actor.CanAct) { actor.ConsumeFreezeTurn(); return; }
            if (skill == null || skill.fpCost > fp) skill = actor.BasicAttack;
            if (skill == null) return;
            fp = Mathf.Clamp(fp + skill.fpGain - skill.fpCost, 0, 5);
            if (skill.target == SkillTarget.Self) { actor.Heal(Mathf.RoundToInt(actor.MaxHp * skill.healRatio)); return; }
            HitResult hit = DamageCalculator.Calculate(actor, target, skill);
            if (hit.missed) return;
            target.TakeDamage(hit.damage);
            if (!target.IsFainted && hit.statusApplied) target.ApplyStatus(skill.statusEffect, skill.statusDuration);
        }
        [Serializable] private class Roster { public List<RosterEntry> entries; }
        [Serializable] private class RosterEntry { public string regionId, shadowId; }

        private static ShadowData Load(string id) => AssetDatabase.LoadAssetAtPath<ShadowData>("Assets/Data/Generated/Shadows/" + id + ".asset");
        private static void RemovePreviewOptions(BattleUIController ui)
        {
            var options = (List<GameObject>)typeof(BattleUIController).GetField("_options", Private).GetValue(ui);
            foreach (GameObject option in options) Object.DestroyImmediate(option);
            options.Clear();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new BuildFailedException(message); }
        private static void SetProperty(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private)?.Invoke(target, null);

        private sealed class Preview : IDisposable
        {
            public RenderTexture Render { get; }
            public Camera Camera { get; set; }
            private readonly Vector2Int _size;
            public Preview(Vector2Int size, bool createScene = true)
            {
                _size = size; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                Render = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Render.hideFlags = HideFlags.HideAndDontSave; Render.Create();
                if (!createScene) return;
                Camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>();
                Camera.orthographic = true; Camera.transform.position = new Vector3(0, 0, -10);
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.02f, .015f, .04f);
                Camera.targetTexture = Render;
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Invoke(events.GetComponent<EventSystem>(), "OnEnable");
            }
            public GameObject Prefab(string name)
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + name + ".prefab"));
                Configure(root); return root;
            }
            public void Configure(GameObject root)
            {
                foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (script.GetType().Namespace?.StartsWith("ShadowTheater.") == true || script is CanvasScaler) script.enabled = false;
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                { canvas.enabled = true; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera; canvas.planeDistance = 10; canvas.scaleFactor = _size.y / 900f; }
                foreach (GraphicRaycaster raycaster in root.GetComponentsInChildren<GraphicRaycaster>(true)) Invoke(raycaster, "OnEnable");
                foreach (MobileSafeArea safe in root.GetComponentsInChildren<MobileSafeArea>(true))
                    MobileUiPolish.Anchors((RectTransform)safe.transform, new Vector2(.025f, .018f), new Vector2(.975f, .982f));
                Font font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 28);
                foreach (Text text in root.GetComponentsInChildren<Text>(true)) text.font = font;
            }
            public void Layout(GameObject root)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var content in root.GetComponentsInChildren<ContentSizeFitter>(true)) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content.transform);
                Canvas.ForceUpdateCanvases();
            }
            public void Click(Button button)
            {
                Vector3 world = ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center);
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(Camera, world), button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                var graphic = button.targetGraphic;
                Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
                    "Button blocked or outside viewport: " + button.name + " at " + pointer.position + " depth=" + graphic.depth +
                    " culled=" + graphic.canvasRenderer.cull + " hits=" + string.Join(",", hits.Select(x => x.gameObject.name)));
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            }
            public void Save(string name)
            {
                foreach (Text text in Object.FindObjectsOfType<Text>()) text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                var previous = RenderTexture.active;
                try
                {
                    Camera.Render(); RenderTexture.active = Render;
                    var texture = new Texture2D(_size.x, _size.y, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0, 0, _size.x, _size.y), 0, 0); texture.Apply();
                    File.WriteAllBytes($"{Folder}/{name}_{_size.x}x{_size.y}.png", texture.EncodeToPNG()); Object.DestroyImmediate(texture);
                }
                finally { RenderTexture.active = previous; }
            }
            public void Dispose()
            { if (Camera != null) Camera.targetTexture = null; if (Render != null) { Render.Release(); Object.DestroyImmediate(Render); } EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); }
        }
    }
}
#endif
