#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    /// <summary>타이틀부터 달빛 초원 보스까지 플레이 가능한 프롤로그 씬 묶음을 생성한다.</summary>
    public static class PlayablePrologueGenerator
    {
        private const string SceneFolder = "Assets/Scenes/Prologue";
        private const string TileFolder = "Assets/Data/Generated/Tiles";
        private const string TileArtFolder = "Assets/Art/Generated/Tiles";
        private static Tile _groundTile;
        private static Tile _wallTile;
        private static Tile _accentTile;

        [MenuItem("Tools/Shadow Theater/Generate Playable Prologue")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            GenerateDependencies();
            EnsureFolders();
            CreateTitleScene();
            CreatePrologueTheater();
            CreateEchoVillage();
            CreateMoonlitMeadow();
            CreateMoonlitBossStage();
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene($"{SceneFolder}/Title.unity", OpenSceneMode.Single);
            Debug.Log("[Prologue] 타이틀→극장→마을→초원→보스 프롤로그 생성 완료");
        }

        private static void GenerateDependencies()
        {
            CoreContentBatchGenerator.Generate();
            LegendaryGrowthBatchGenerator.Generate();
            FrontEndPrefabGenerator.Generate();
            DialogueUIPrefabGenerator.Generate();
            QuestHudPrefabGenerator.Generate();
            ScriptBookPrefabGenerator.Generate();
            PartyStoragePrefabGenerator.Generate();
            BattleUIPrefabGenerator.Generate();
        }

        private static void CreateTitleScene()
        {
            var scene = NewScene();
            InstantiatePrefab("Assets/Prefabs/Systems/CoreSystems.prefab");
            var title = InstantiatePrefab("Assets/Prefabs/UI/TitleCanvas.prefab");
            CreateEventSystem();
            CreateCamera("TitleCamera", new Color(0.015f, 0.008f, 0.035f), null, false);

            var controller = title.GetComponent<TitleScreenController>();
            var controllerSo = new SerializedObject(controller);
            Set(controllerSo, "firstScene", "PrologueTheater");
            Set(controllerSo, "firstCell", new Vector2Int(0, -5));
            Set(controllerSo, "firstFacing", (int)FacingDir.Up);
            Set(controllerSo, "startingItem", LoadItem("potion"));
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            var selection = title.GetComponent<StarterSelectionController>();
            var selectionSo = new SerializedObject(selection);
            var starters = selectionSo.FindProperty("starters");
            starters.arraySize = 3;
            starters.GetArrayElementAtIndex(0).objectReferenceValue = LoadShadow("knight");
            starters.GetArrayElementAtIndex(1).objectReferenceValue = LoadShadow("mage");
            starters.GetArrayElementAtIndex(2).objectReferenceValue = LoadShadow("beast");
            selectionSo.ApplyModifiedPropertiesWithoutUndo();
            Save(scene, "Title");
        }

        private static void CreatePrologueTheater()
        {
            var map = BeginFieldScene("PrologueTheater", "잔향 극장", new Vector2Int(0, -5),
                new Color(0.055f, 0.035f, 0.10f), Theme.Theater);
            CreateNpc(map.fieldRoot, "Aria", new Vector2Int(0, 0), LoadShadow("mage"),
                "npc_aria", "npc_aria_intro", "npc_aria_repeat", "talked_npc_aria",
                new Color(0.72f, 0.42f, 1f));
            CreateInteractable(map.fieldRoot, "FirstScriptShrine", new Vector2Int(-3, 3),
                "shrine_first_script", LoadShadow("mask"), new Color(0.55f, 0.75f, 1f));
            CreatePortal(map.fieldRoot, "ToEchoVillage", new Vector2Int(9, 0), "EchoVillage",
                new Vector2Int(-8, 0), FacingDir.Right, false, "quest_prologue_01_awaken_complete");
            FinishFieldScene(map, "PrologueTheater");
        }

        private static void CreateEchoVillage()
        {
            var map = BeginFieldScene("EchoVillage", "잔향 마을", new Vector2Int(-8, 0),
                new Color(0.045f, 0.06f, 0.105f), Theme.Village);
            CreateInteractable(map.fieldRoot, "MoonlitSign", new Vector2Int(2, 2),
                "sign_moonlit_meadow", LoadShadow("puppet"), new Color(0.55f, 0.82f, 1f));
            CreatePortal(map.fieldRoot, "ToTheater", new Vector2Int(-9, 0), "PrologueTheater",
                new Vector2Int(8, 0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToMoonlitMeadow", new Vector2Int(9, 0), "MoonlitMeadow",
                new Vector2Int(-8, 0), FacingDir.Right, true);
            FinishFieldScene(map, "EchoVillage");
        }

        private static void CreateMoonlitMeadow()
        {
            var map = BeginFieldScene("MoonlitMeadow", "달빛 초원", new Vector2Int(-8, 0),
                new Color(0.035f, 0.065f, 0.11f), Theme.Meadow);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8, 0), "area_moonlit_meadow");
            CreateNpc(map.fieldRoot, "LanternKeeperMoen", new Vector2Int(1, 5), LoadShadow("crow"),
                "npc_lantern_keeper", "npc_lantern_keeper_intro", "npc_lantern_keeper_repeat",
                "talked_npc_lantern_keeper", new Color(1f, 0.70f, 0.38f));
            CreateEncounter(map.fieldRoot, "PuppetSymbol", new Vector2Int(-3, 3), LoadShadow("puppet"), 3, 5);
            CreateEncounter(map.fieldRoot, "CrowSymbol", new Vector2Int(4, 4), LoadShadow("crow"), 3, 6);
            CreateEncounter(map.fieldRoot, "MaskSymbol", new Vector2Int(4, -3), LoadShadow("mask"), 4, 6);
            CreateEncounter(map.fieldRoot, "WanderingBeast", new Vector2Int(-2, -4), LoadShadow("beast"), 5, 7);
            CreatePortal(map.fieldRoot, "ToEchoVillage", new Vector2Int(-9, 0), "EchoVillage",
                new Vector2Int(8, 0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToMoonlitBoss", new Vector2Int(0, 8), "MoonlitBossStage",
                new Vector2Int(0, -6), FacingDir.Up, false, "quest_prologue_04_echoes_complete");
            FinishFieldScene(map, "MoonlitMeadow");
        }

        private static void CreateMoonlitBossStage()
        {
            var map = BeginFieldScene("MoonlitBossStage", "비극의 무대 · 지지 않는 달", new Vector2Int(0, -6),
                new Color(0.045f, 0.035f, 0.09f), Theme.Boss);
            CreateBoss(map.fieldRoot, new Vector2Int(0, 3));
            CreatePortal(map.fieldRoot, "BackToMeadow", new Vector2Int(0, -8), "MoonlitMeadow",
                new Vector2Int(0, 7), FacingDir.Down);
            FinishFieldScene(map, "MoonlitBossStage");
        }

        private static FieldSceneContext BeginFieldScene(string mapId, string displayName, Vector2Int startCell,
                                                         Color cameraColor, Theme theme)
        {
            var scene = NewScene();
            CreateThemeTiles(theme);
            InstantiatePrefab("Assets/Prefabs/Systems/CoreSystems.prefab");
            var fieldRoot = new GameObject("FieldRoot");
            var gridObject = new GameObject("Grid", typeof(Grid), typeof(FieldGrid));
            gridObject.transform.SetParent(fieldRoot.transform);
            var ground = CreateTilemap("Ground", gridObject.transform, 0, false);
            var collision = CreateTilemap("Collision", gridObject.transform, 2, true);
            PaintMap(ground, collision, theme);

            var gridSo = new SerializedObject(gridObject.GetComponent<FieldGrid>());
            Set(gridSo, "mapId", mapId);
            Set(gridSo, "collisionTilemap", collision);
            gridSo.FindProperty("unitMask").intValue = 1 << 0;
            gridSo.ApplyModifiedPropertiesWithoutUndo();

            var player = CreatePlayer(fieldRoot.transform, startCell);
            CreateCamera("FieldCamera", cameraColor, player.transform, true).transform.SetParent(fieldRoot.transform);
            CreateFieldUi(fieldRoot.transform);
            CreateRegionLabel(fieldRoot.transform, displayName);

            var battleRoot = new GameObject("BattleRoot");
            CreateCamera("BattleCamera", new Color(0.018f, 0.012f, 0.045f), null, false).transform.SetParent(battleRoot.transform);
            var battleCanvas = InstantiatePrefab("Assets/Prefabs/UI/BattleCanvas.prefab", battleRoot.transform);
            var battleManager = battleCanvas.GetComponent<BattleManager>();

            var flowObject = new GameObject("GameFlow", typeof(GameFlowController));
            var flowSo = new SerializedObject(flowObject.GetComponent<GameFlowController>());
            Set(flowSo, "fieldRoot", fieldRoot);
            Set(flowSo, "battleRoot", battleRoot);
            Set(flowSo, "battleManager", battleManager);
            Set(flowSo, "devStarter", LoadShadow("knight"));
            Set(flowSo, "devStartItem", LoadItem("potion"));
            flowSo.ApplyModifiedPropertiesWithoutUndo();
            battleRoot.SetActive(false);

            InstantiatePrefab("Assets/Prefabs/UI/DialogueCanvas.prefab");
            InstantiatePrefab("Assets/Prefabs/UI/QuestHUDCanvas.prefab");
            InstantiatePrefab("Assets/Prefabs/UI/ScriptBookCanvas.prefab");
            InstantiatePrefab("Assets/Prefabs/UI/PartyStorageCanvas.prefab");
            CreateFader();
            CreateEventSystem();
            return new FieldSceneContext { scene = scene, fieldRoot = fieldRoot.transform };
        }

        private static void FinishFieldScene(FieldSceneContext context, string sceneName) => Save(context.scene, sceneName);

        private static PlayerController CreatePlayer(Transform parent, Vector2Int cell)
        {
            var go = new GameObject("Player", typeof(SpriteRenderer), typeof(Rigidbody2D),
                typeof(CapsuleCollider2D), typeof(PlayerController));
            go.transform.SetParent(parent);
            go.transform.position = Cell(cell);
            go.tag = "Player";
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = LoadShadow("knight")?.silhouetteSprite;
            renderer.color = new Color(0.10f, 0.055f, 0.16f, 1f);
            renderer.sortingOrder = 10;
            go.GetComponent<Rigidbody2D>().gravityScale = 0f;
            go.GetComponent<CapsuleCollider2D>().size = new Vector2(0.55f, 0.75f);
            var so = new SerializedObject(go.GetComponent<PlayerController>());
            Set(so, "silhouette", renderer);
            so.ApplyModifiedPropertiesWithoutUndo();
            return go.GetComponent<PlayerController>();
        }

        private static Camera CreateCamera(string name, Color background, Transform target, bool follow)
        {
            var go = new GameObject(name, typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(target != null ? target.position.x : 0,
                target != null ? target.position.y : 0, -10f);
            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            if (follow)
            {
                var cameraFollow = go.AddComponent<FieldCameraFollow>();
                var so = new SerializedObject(cameraFollow);
                Set(so, "target", target);
                Set(so, "minBounds", new Vector2(-7f, -2f));
                Set(so, "maxBounds", new Vector2(7f, 2f));
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return camera;
        }

        private static Tilemap CreateTilemap(string name, Transform parent, int sortingOrder, bool collider)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            if (collider) go.AddComponent<TilemapCollider2D>();
            return go.GetComponent<Tilemap>();
        }

        private static void PaintMap(Tilemap ground, Tilemap collision, Theme theme)
        {
            for (int y = -9; y <= 9; y++)
            for (int x = -11; x <= 11; x++) ground.SetTile(new Vector3Int(x, y, 0), _groundTile);
            for (int x = -11; x <= 11; x++)
            {
                collision.SetTile(new Vector3Int(x, -9, 0), _wallTile);
                collision.SetTile(new Vector3Int(x, 9, 0), _wallTile);
            }
            for (int y = -9; y <= 9; y++)
            {
                collision.SetTile(new Vector3Int(-11, y, 0), _wallTile);
                collision.SetTile(new Vector3Int(11, y, 0), _wallTile);
            }

            var blocks = new List<RectInt>();
            switch (theme)
            {
                case Theme.Theater:
                    blocks.Add(new RectInt(-7, 2, 3, 4)); blocks.Add(new RectInt(5, 2, 3, 4));
                    blocks.Add(new RectInt(-7, -6, 2, 2)); blocks.Add(new RectInt(6, -6, 2, 2)); break;
                case Theme.Village:
                    blocks.Add(new RectInt(-5, 3, 3, 3)); blocks.Add(new RectInt(3, 3, 3, 3));
                    blocks.Add(new RectInt(-4, -6, 2, 3)); blocks.Add(new RectInt(5, -5, 2, 2)); break;
                case Theme.Meadow:
                    blocks.Add(new RectInt(-6, 4, 2, 2)); blocks.Add(new RectInt(5, 2, 2, 3));
                    blocks.Add(new RectInt(-6, -5, 3, 2)); blocks.Add(new RectInt(2, -6, 2, 2));
                    blocks.Add(new RectInt(-1, 1, 2, 2)); break;
                case Theme.Boss:
                    blocks.Add(new RectInt(-7, -1, 2, 5)); blocks.Add(new RectInt(6, -1, 2, 5));
                    blocks.Add(new RectInt(-4, 6, 2, 2)); blocks.Add(new RectInt(3, 6, 2, 2)); break;
            }
            foreach (var block in blocks)
                for (int y = block.yMin; y < block.yMax; y++)
                for (int x = block.xMin; x < block.xMax; x++) collision.SetTile(new Vector3Int(x, y, 0), _accentTile);
        }

        private static void CreateNpc(Transform parent, string name, Vector2Int cell, ShadowData visual,
                                      string npcId, string first, string repeat, string flag, Color accent)
        {
            var go = CreateFieldActor(parent, name, cell, visual, false);
            var npc = go.AddComponent<StoryNpc>();
            var so = new SerializedObject(npc);
            Set(so, "npcId", npcId); Set(so, "firstDialogueId", first); Set(so, "repeatDialogueId", repeat);
            Set(so, "completionFlag", flag); Set(so, "dialogueAccent", accent);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateInteractable(Transform parent, string name, Vector2Int cell, string dialogueId,
                                               ShadowData visual, Color accent)
        {
            var go = CreateFieldActor(parent, name, cell, visual, false);
            var interactable = go.AddComponent<DialogueInteractable>();
            var so = new SerializedObject(interactable);
            Set(so, "dialogueId", dialogueId); Set(so, "accent", accent);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateFieldActor(Transform parent, string name, Vector2Int cell,
                                                   ShadowData visual, bool trigger)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D));
            go.transform.SetParent(parent); go.transform.position = Cell(cell);
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = visual?.silhouetteSprite;
            renderer.color = visual != null ? visual.accentColor * 0.8f : Color.white; renderer.sortingOrder = 8;
            var box = go.GetComponent<BoxCollider2D>(); box.size = new Vector2(0.72f, 0.82f); box.isTrigger = trigger;
            return go;
        }

        private static void CreateEncounter(Transform parent, string name, Vector2Int cell,
                                            ShadowData shadow, int minLevel, int maxLevel)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(EncounterSymbol));
            go.transform.SetParent(parent); go.transform.position = Cell(cell);
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = shadow?.silhouetteSprite;
            renderer.color = shadow != null ? shadow.accentColor * 0.72f : Color.black; renderer.sortingOrder = 7;
            go.GetComponent<CircleCollider2D>().radius = 0.38f;
            var so = new SerializedObject(go.GetComponent<EncounterSymbol>());
            Set(so, "leadShadow", shadow); Set(so, "levelRange", new Vector2Int(minLevel, maxLevel));
            Set(so, "silhouette", renderer); so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateBoss(Transform parent, Vector2Int cell)
        {
            var boss = LoadShadow("boss_moonlit_widow");
            var go = CreateFieldActor(parent, "MoonlitWidowBoss", cell, boss, false);
            go.transform.localScale = Vector3.one * 1.5f;
            var trigger = go.AddComponent<BossEncounterTrigger>();
            var so = new SerializedObject(trigger);
            var party = so.FindProperty("enemyParty"); party.arraySize = 1;
            party.GetArrayElementAtIndex(0).objectReferenceValue = boss;
            Set(so, "levelRange", new Vector2Int(10, 12)); Set(so, "silhouette", go.GetComponent<SpriteRenderer>());
            Set(so, "purificationReward", boss); Set(so, "purificationRewardLevel", 12);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateAreaTrigger(Transform parent, Vector2Int cell, string areaId)
        {
            var go = new GameObject("Area_" + areaId, typeof(BoxCollider2D), typeof(QuestAreaTrigger));
            go.transform.SetParent(parent); go.transform.position = Cell(cell);
            var box = go.GetComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(2f, 3f);
            var so = new SerializedObject(go.GetComponent<QuestAreaTrigger>()); Set(so, "areaId", areaId);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreatePortal(Transform parent, string name, Vector2Int cell, string target,
                                         Vector2Int arrival, FacingDir facing, bool checkpoint = false,
                                         string requiredFlag = null)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(MapPortal));
            go.transform.SetParent(parent); go.transform.position = Cell(cell);
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = _accentTile.sprite;
            renderer.color = new Color(0.55f, 0.38f, 0.92f, 0.75f); renderer.sortingOrder = 5;
            go.transform.localScale = new Vector3(0.7f, 0.9f, 1f);
            var box = go.GetComponent<BoxCollider2D>(); box.isTrigger = true; box.size = Vector2.one;
            var so = new SerializedObject(go.GetComponent<MapPortal>());
            Set(so, "targetScene", target); Set(so, "arrivalCell", arrival); Set(so, "arrivalFacing", (int)facing);
            Set(so, "activateOnTouch", true); Set(so, "setCheckpoint", checkpoint);
            if (!string.IsNullOrEmpty(requiredFlag)) Set(so, "requiredFlag", requiredFlag);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateFieldUi(Transform parent)
        {
            var root = new GameObject("FieldUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            DirectionButton("Up", root.transform, new Vector2(.13f,.15f), new Vector2(.25f,.22f), "▲", Vector2.up);
            DirectionButton("Left", root.transform, new Vector2(.02f,.08f), new Vector2(.14f,.15f), "◀", Vector2.left);
            DirectionButton("Right", root.transform, new Vector2(.24f,.08f), new Vector2(.36f,.15f), "▶", Vector2.right);
            DirectionButton("Down", root.transform, new Vector2(.13f,.01f), new Vector2(.25f,.08f), "▼", Vector2.down);
            var action = UiButton("Action", root.transform, new Vector2(.80f,.04f), new Vector2(.96f,.15f), "A");
            action.gameObject.AddComponent<VirtualActionButton>();
        }

        private static void DirectionButton(string name, Transform parent, Vector2 min, Vector2 max,
                                            string label, Vector2 direction)
        {
            var button = UiButton(name, parent, min, max, label);
            var input = button.gameObject.AddComponent<VirtualDPadButton>();
            var so = new SerializedObject(input); Set(so, "direction", direction); so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button UiButton(string name, Transform parent, Vector2 min, Vector2 max, string label)
        {
            var rect = UiRect(name, parent, min, max); var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.09f,.055f,.17f,.78f); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var text = UiText("Label", rect, Vector2.zero, Vector2.one, 34, TextAnchor.MiddleCenter); text.text = label;
            return button;
        }

        private static void CreateRegionLabel(Transform parent, string displayName)
        {
            var root = new GameObject("RegionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false); var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 15;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080,1920);
            var text = UiText("RegionName", root.transform, new Vector2(.20f,.94f), new Vector2(.80f,.985f), 28, TextAnchor.MiddleCenter);
            text.text = displayName; text.color = new Color(.85f,.78f,1f,.82f);
        }

        private static void CreateFader()
        {
            var go = new GameObject("FaderCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(Image), typeof(ScreenFader));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            go.GetComponent<Image>().color = Color.black;
        }

        private static void CreateEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static RectTransform UiRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; return rect;
        }

        private static Text UiText(string name, Transform parent, Vector2 min, Vector2 max, int size, TextAnchor anchor)
        {
            var text = UiRect(name, parent, min, max).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size;
            text.alignment = anchor; text.color = Color.white; text.raycastTarget = false; return text;
        }

        private static void CreateThemeTiles(Theme theme)
        {
            switch (theme)
            {
                case Theme.Theater:
                    _groundTile = CreateTile("Theater_Ground", new Color(.30f,.22f,.37f), new Color(.43f,.31f,.51f));
                    _wallTile = CreateTile("Theater_Wall", new Color(.07f,.04f,.10f), new Color(.24f,.13f,.25f));
                    _accentTile = CreateTile("Theater_Accent", new Color(.20f,.10f,.27f), new Color(.72f,.49f,.32f)); break;
                case Theme.Village:
                    _groundTile = CreateTile("Village_Ground", new Color(.22f,.31f,.39f), new Color(.31f,.43f,.50f));
                    _wallTile = CreateTile("Village_Wall", new Color(.05f,.09f,.15f), new Color(.13f,.25f,.34f));
                    _accentTile = CreateTile("Village_Accent", new Color(.10f,.22f,.29f), new Color(.38f,.72f,.78f)); break;
                case Theme.Meadow:
                    _groundTile = CreateTile("Meadow_Ground", new Color(.17f,.31f,.35f), new Color(.25f,.43f,.45f));
                    _wallTile = CreateTile("Meadow_Wall", new Color(.035f,.08f,.13f), new Color(.10f,.22f,.28f));
                    _accentTile = CreateTile("Meadow_Accent", new Color(.14f,.21f,.31f), new Color(.49f,.55f,.83f)); break;
                default:
                    _groundTile = CreateTile("Boss_Ground", new Color(.22f,.20f,.34f), new Color(.36f,.34f,.51f));
                    _wallTile = CreateTile("Boss_Wall", new Color(.025f,.018f,.055f), new Color(.15f,.10f,.24f));
                    _accentTile = CreateTile("Boss_Accent", new Color(.24f,.16f,.38f), new Color(.76f,.72f,.95f)); break;
            }
        }

        private static Tile CreateTile(string name, Color baseColor, Color detailColor)
        {
            string texturePath = $"{TileArtFolder}/{name}.png";
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                bool edge = x < 2 || y < 2 || x > 29 || y > 29;
                bool fleck = (x * 13 + y * 7 + name.Length) % 37 == 0;
                texture.SetPixel(x, y, edge || fleck ? detailColor : baseColor);
            }
            texture.Apply(); File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.SaveAndReimport();
            string tilePath = $"{TileFolder}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, tilePath); }
            tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath); EditorUtility.SetDirty(tile); return tile;
        }

        private static void RegisterBuildScenes()
        {
            string[] names = { "Title", "PrologueTheater", "EchoVillage", "MoonlitMeadow", "MoonlitBossStage" };
            var scenes = new List<EditorBuildSettingsScene>();
            var generatedPaths = new HashSet<string>();
            foreach (string name in names)
            {
                string path = $"{SceneFolder}/{name}.unity";
                generatedPaths.Add(path);
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            foreach (var existing in EditorBuildSettings.scenes)
                if (!generatedPaths.Contains(existing.path)) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Scene NewScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        private static void Save(Scene scene, string name) => EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{name}.unity");

        private static GameObject InstantiatePrefab(string path, Transform parent = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError($"[Prologue] 프리팹 누락: {path}"); return null; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null) instance.transform.SetParent(parent, false);
            return instance;
        }

        private static ShadowData LoadShadow(string id) =>
            AssetDatabase.LoadAssetAtPath<ShadowData>($"Assets/Data/Generated/Shadows/{id}.asset");
        private static ItemData LoadItem(string id) =>
            AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/Data/Generated/Items/{id}.asset");
        private static Vector3 Cell(Vector2Int cell) => new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        private static void Set(SerializedObject so, string name, UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue = value;
        private static void Set(SerializedObject so, string name, string value) => so.FindProperty(name).stringValue = value;
        private static void Set(SerializedObject so, string name, bool value) => so.FindProperty(name).boolValue = value;
        private static void Set(SerializedObject so, string name, int value) => so.FindProperty(name).intValue = value;
        private static void Set(SerializedObject so, string name, Vector2 value) => so.FindProperty(name).vector2Value = value;
        private static void Set(SerializedObject so, string name, Vector2Int value) => so.FindProperty(name).vector2IntValue = value;
        private static void Set(SerializedObject so, string name, Color value) => so.FindProperty(name).colorValue = value;

        private static void EnsureFolders()
        {
            Ensure("Assets", "Scenes"); Ensure("Assets/Scenes", "Prologue");
            Ensure("Assets/Data/Generated", "Tiles"); Ensure("Assets/Art/Generated", "Tiles");
        }
        private static void Ensure(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        private enum Theme { Theater, Village, Meadow, Boss }
        private class FieldSceneContext { public Scene scene; public Transform fieldRoot; }
    }
}
#endif
