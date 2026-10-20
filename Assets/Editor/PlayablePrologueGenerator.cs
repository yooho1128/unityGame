#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
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
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using ShadowData = ShadowTheater.Data.ShadowData;

namespace ShadowTheater.EditorTools
{
    /// <summary>타이틀부터 달빛 초원 보스까지 플레이 가능한 프롤로그 씬 묶음을 생성한다.</summary>
    public static class PlayablePrologueGenerator
    {
        private const string SceneFolder = "Assets/Scenes/Prologue";
        private const string TileFolder = "Assets/Data/Generated/Tiles";
        private const string TileArtFolder = "Assets/Art/Generated/Tiles";
        private const string PixelCharacterFolder = "Assets/Art/Generated/Characters";
        private const string FinalFieldCharacterFolder = "Assets/Art/Final/Field";
        private const string FinalPlayerFolder = "Assets/Art/Final/Player";
        private const string FinalTileFolder = "Assets/Art/Final/Tiles";
        private const string RegionalRosterManifestPath = "Assets/Resources/Data/RegionalRosterManifest.json";
        private const string RegionCatalogPath = "Assets/Resources/Data/RegionCatalog.json";
        private static Tile _groundTile;
        private static Tile _wallTile;
        private static Tile _accentTile;
        private static Tile _meadowGrassTile;
        private static Tile _meadowPathTile;
        private static Tile _meadowWaterTile;
        private static Tile _meadowCliffTile;
        private static Tile _meadowBushTile;
        private static Material _litMaterial;
        private static Sprite _fogSprite;
        private static Sprite _moteSprite;
        private static Sprite _pixelEncounterSprite;
        private static Sprite _pixelLanternKeeperSprite;
        private static PlayerSpriteSet _pixelPlayerSprites;
        private static readonly Dictionary<string, Sprite> PixelFieldActors = new Dictionary<string, Sprite>();

        [MenuItem("Tools/Shadow Theater/Generate Playable Prologue")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            PixelFieldActors.Clear();
            _pixelPlayerSprites = null;
            _pixelEncounterSprite = null;
            _pixelLanternKeeperSprite = null;
            GenerateDependencies();
            MissingScriptRepairUtility.RepairGeneratedPrefabs();
            EnsureFolders();
            FieldArtAtlasImporter.Import();
            foreach (ShadowData shadow in AssetDatabase.LoadAssetAtPath<ShadowDatabase>("Assets/Resources/ShadowDatabase.asset").shadows)
            {
                shadow.fieldFrames = CreatePixelFieldActorFrames(shadow);
                EditorUtility.SetDirty(shadow);
            }
            AssetDatabase.SaveAssets();
            EnsureUrp2DRenderer();
            CreateTitleScene();
            CreatePrologueTheater();
            CreateEchoVillage();
            CreateMoonlitMeadow();
            CreateMoonlitBossStage();
            CreateCurtainPass();
            CreateAshBorder();
            CreateCinderCity();
            CreateRuinedBarracks();
            CreateEmberCatacombs();
            CreateCrownlessThrone();
            CreateFrostPort();
            CreateWhiteArchive();
            CreateForbiddenStacks();
            CreateMirrorVault();
            CreateBlueAbyss();
            CreateVioletMarsh();
            CreateHowlVillage();
            CreateMoonfangForest();
            CreateBloodmoonRidge();
            CreateSleepingBeastDen();
            CreateThreadMarket();
            CreateClockworkAlley();
            CreateMarionetteOpera();
            CreateSeveredWorkshop();
            CreatePuppeteerStage();
            CreateErasedStation();
            CreateBlankPrison();
            CreateRedactionLab();
            CreateSilentCourt();
            CreateBlackArchive();
            CreateGlassCoast();
            CreateDrownedGallery();
            CreateNameIslands();
            CreateMourningLighthouse();
            CreateWidowMoonPalace();
            CreateInvertedLobby();
            CreateEndlessBackstage();
            CreateFirstActorRoom();
            CreateCosmicAuditorium();
            CreateFinalCurtain();
            CreateExpansionRegions();
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene($"{SceneFolder}/Title.unity", OpenSceneMode.Single);
            Debug.Log("[World] 7개 극단 마을과 63개 픽셀 필드 생성 완료");
        }

        private static void CreateExpansionRegions()
        {
            RegionCatalogData catalog = LoadRegionCatalog();
            var byId = new Dictionary<string, RegionData>();
            foreach (RegionData region in catalog.regions) byId[region.regionId] = region;
            var expansion = catalog.regions.FindAll(x => x.order > 40);
            expansion.Sort((a, b) => a.order.CompareTo(b.order));
            var portalCells = new[]
            {
                new Vector2Int(-9, 0), new Vector2Int(9, 0),
                new Vector2Int(0, 8), new Vector2Int(0, -8)
            };

            foreach (RegionData region in expansion)
            {
                Theme theme = ExpansionTheme(region);
                Color cameraColor = Color.Lerp(region.AccentColor, Color.black, .76f);
                var map = BeginFieldScene(region.sceneName, region.displayName, region.ArrivalCell,
                    cameraColor, theme);
                CreateAreaTrigger(map.fieldRoot, region.ArrivalCell, "area_" + region.regionId);
                string[] connections = region.connectedRegionIds ?? new string[0];
                if (connections.Length > portalCells.Length)
                    Debug.LogError($"[World] {region.regionId}: 연결 {connections.Length}개 중 " +
                                   $"{portalCells.Length}개만 배치할 수 있습니다.");
                for (int i = 0; i < connections.Length && i < portalCells.Length; i++)
                {
                    if (!byId.TryGetValue(connections[i], out RegionData target)) continue;
                    Vector2Int cell = portalCells[i];
                    CreatePortal(map.fieldRoot, "To" + target.sceneName, cell, target.sceneName,
                        target.ArrivalCell, cell.x < 0 ? FacingDir.Left : cell.x > 0 ? FacingDir.Right :
                        cell.y > 0 ? FacingDir.Up : FacingDir.Down);
                }
                if (region.regionId == "bubble_cave")
                    CreateTraversalRoutePair(map.fieldRoot, "FrostwingWindGap", new Vector2Int(-8,-6),
                        new Vector2Int(8,-6), TraversalAbility.Glide, new Color(.55f,.88f,1f));
                else if (region.regionId == "lantern_reed_field")
                    CreateTraversalRoutePair(map.fieldRoot, "SpiritLanternPath", new Vector2Int(-8,-6),
                        new Vector2Int(8,-6), TraversalAbility.SpiritLight, new Color(1f,.72f,.32f));
                else if (region.regionId == "abandoned_toy_store")
                    CreateTraversalRoutePair(map.fieldRoot, "ClockworkRail", new Vector2Int(-8,-6),
                        new Vector2Int(8,-6), TraversalAbility.ClockworkRide, new Color(.82f,.55f,.35f));
                CreateHiddenRegionChallenge(map.fieldRoot, region);
                FinishFieldScene(map, region.sceneName);
            }
        }

        private static Theme ExpansionTheme(RegionData region)
        {
            if (region.regionId == "ashes_bell_tower") return Theme.AshWastes;
            if (region.regionId == "inverted_moon_path") return Theme.MoonfangForest;
            if (region.regionId == "windless_echo_cavern") return Theme.BloodmoonRidge;
            switch (region.act)
            {
                case 1: return region.isBossStage ? Theme.Boss : Theme.Meadow;
                case 2: return region.isBossStage ? Theme.AshThrone : Theme.EmberCity;
                case 3: return Theme.MemorySea;
                case 4: return region.isBossStage ? Theme.BeastDen : Theme.HowlVillage;
                case 5: return region.isBossStage ? Theme.PuppeteerStage : Theme.ThreadMarket;
                case 6: return region.isBossStage ? Theme.BlackArchive : Theme.Censor;
                default: return Theme.FinalTheater;
            }
        }

        private static RegionCatalogData LoadRegionCatalog()
        {
            if (!File.Exists(RegionCatalogPath)) return new RegionCatalogData();
            return JsonUtility.FromJson<RegionCatalogData>(File.ReadAllText(RegionCatalogPath))
                   ?? new RegionCatalogData();
        }

        private static void GenerateDependencies()
        {
            ReleaseVisualAssetGenerator.Generate();
            CoreContentBatchGenerator.Generate();
            LegendaryGrowthBatchGenerator.Generate();
            FinalPixelArtPipeline.ImportAndValidate();
            UltimateFxAssetGenerator.Generate();
            StandardSkillAudioAssetGenerator.Generate();
            FieldAudioAssetGenerator.Generate();
            FrontEndPrefabGenerator.Generate();
            DialogueUIPrefabGenerator.Generate();
            RegionArrivalPrefabGenerator.Generate();
            QuestHudPrefabGenerator.Generate();
            ScriptBookPrefabGenerator.Generate();
            PartyStoragePrefabGenerator.Generate();
            WorldMapPrefabGenerator.Generate();
            SettlementShopPrefabGenerator.Generate();
            FieldInventoryPrefabGenerator.Generate();
            QuestLogPrefabGenerator.Generate();
            BossInvestigationJournalPrefabGenerator.Generate();
            FieldPausePrefabGenerator.Generate();
            BattleUIPrefabGenerator.Generate();
            MobileUiPolish.ApplyToGeneratedPrefabs();
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
            var map = BeginFieldScene("PrologueTheater", "웃음막 유랑촌", new Vector2Int(0, -5),
                new Color(0.055f, 0.035f, 0.10f), Theme.Theater);
            CreateNpc(map.fieldRoot, "Aria", new Vector2Int(0, 0), LoadShadow("mage"),
                "npc_aria", "npc_aria_intro", "npc_aria_repeat", "talked_npc_aria",
                new Color(0.72f, 0.42f, 1f));
            CreateInteractable(map.fieldRoot, "FirstScriptShrine", new Vector2Int(-3, 3),
                "shrine_first_script", LoadShadow("mask"), new Color(0.55f, 0.75f, 1f));
            CreatePortal(map.fieldRoot, "ToEchoVillage", new Vector2Int(9, 0), "EchoVillage",
                new Vector2Int(-8, 0), FacingDir.Right, false, "quest_prologue_01_awaken_complete");
            CreatePortal(map.fieldRoot, "ToPaintedWagonRoad", new Vector2Int(-9, 0), "PaintedWagonRoad",
                new Vector2Int(-8, 0), FacingDir.Left);
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
            CreatePortal(map.fieldRoot, "ToPaintedWagonRoad", new Vector2Int(0, 8), "PaintedWagonRoad",
                new Vector2Int(-8, 0), FacingDir.Up);
            FinishFieldScene(map, "EchoVillage");
        }

        private static void CreateMoonlitMeadow()
        {
            var map = BeginFieldScene("MoonlitMeadow", "달빛 초원", new Vector2Int(-8, 0),
                new Color(0.035f, 0.065f, 0.11f), Theme.Meadow);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8, 0), "area_moonlit_meadow");
            var lanternKeeper = CreateNpc(map.fieldRoot, "LanternKeeperMoen", new Vector2Int(1, 5), LoadShadow("crow"),
                "npc_lantern_keeper", "npc_lantern_keeper_intro", "npc_lantern_keeper_repeat",
                "talked_npc_lantern_keeper", new Color(1f, 0.70f, 0.38f));
            var keeperRenderer = lanternKeeper.GetComponentInChildren<SpriteRenderer>();
            keeperRenderer.sprite = CreatePixelLanternKeeperSprite();
            keeperRenderer.color = Color.white;
            var keeperMotion = lanternKeeper.GetComponentInChildren<ShadowFieldMotion>();
            if (keeperMotion != null) keeperMotion.enabled = false;
            CreateEncounter(map.fieldRoot, "PuppetSymbol", new Vector2Int(-3, 3), LoadShadow("puppet"), 3, 5);
            CreateEncounter(map.fieldRoot, "CrowSymbol", new Vector2Int(4, 4), LoadShadow("crow"), 3, 6);
            CreateEncounter(map.fieldRoot, "MaskSymbol", new Vector2Int(4, -3), LoadShadow("mask"), 4, 6);
            CreateEncounter(map.fieldRoot, "WanderingBeast", new Vector2Int(-2, -4), LoadShadow("beast"), 5, 7);
            CreatePortal(map.fieldRoot, "ToEchoVillage", new Vector2Int(-9, 0), "EchoVillage",
                new Vector2Int(8, 0), FacingDir.Left);
            // 북쪽 절벽에 숨겨져 있던 출구를 화면에서 바로 읽히는 동쪽 길로 옮긴다.
            // 보스 퀘스트는 입장 후 자동 진행되므로 탐색 자체를 플래그로 막지 않는다.
            CreatePortal(map.fieldRoot, "ToMoonlitBoss", new Vector2Int(9, 0), "MoonlitBossStage",
                new Vector2Int(0, -6), FacingDir.Right);
            FinishFieldScene(map, "MoonlitMeadow");
        }

        private static void CreateMoonlitBossStage()
        {
            var map = BeginFieldScene("MoonlitBossStage", "비극의 무대 · 지지 않는 달", new Vector2Int(0, -6),
                new Color(0.045f, 0.035f, 0.09f), Theme.Boss);
            CreateBoss(map.fieldRoot, new Vector2Int(0, 3));
            CreatePortal(map.fieldRoot, "BackToMeadow", new Vector2Int(0, -8), "MoonlitMeadow",
                new Vector2Int(-3, 7), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToCurtainPass", new Vector2Int(9, 0), "CurtainPass",
                new Vector2Int(-8, 0), FacingDir.Right, false, "boss_moonlit_story_complete");
            FinishFieldScene(map, "MoonlitBossStage");
        }

        private static void CreateCurtainPass()
        {
            var map = BeginFieldScene("CurtainPass", "찢어진 장막길", new Vector2Int(-8, 0),
                new Color(.12f,.07f,.07f), Theme.AshWastes);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8,0), "area_curtain_pass");
            CreateNpc(map.fieldRoot, "CurtainWatcher", new Vector2Int(0,3), LoadShadow("banner_spearman"),
                "npc_curtain_watcher", "npc_curtain_watcher_intro", "npc_curtain_watcher_repeat",
                "talked_curtain_watcher", new Color(.84f,.48f,.34f));
            CreateEncounter(map.fieldRoot, "AshHound", new Vector2Int(-2,-4), LoadShadow("ash_hound"), 11,14);
            CreateEncounter(map.fieldRoot, "BannerSpearman", new Vector2Int(6,1), LoadShadow("banner_spearman"), 12,15);
            CreatePortal(map.fieldRoot, "BackToMoonStage", new Vector2Int(-9,0), "MoonlitBossStage",
                new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToAshBorder", new Vector2Int(9,0), "AshBorder",
                new Vector2Int(-8,0), FacingDir.Right, false, "quest_chapter_01_departure_complete");
            FinishFieldScene(map, "CurtainPass");
        }

        private static void CreateAshBorder()
        {
            var map = BeginFieldScene("AshBorder", "재의 국경", new Vector2Int(-8,0),
                new Color(.15f,.065f,.045f), Theme.AshWastes);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8,0), "area_ash_border");
            CreateNpc(map.fieldRoot, "AshScout", new Vector2Int(1,4), LoadShadow("soot_archer"),
                "npc_ash_scout", "npc_ash_scout_intro", "npc_ash_scout_repeat", "talked_ash_scout",
                new Color(.92f,.39f,.24f));
            CreateInteractable(map.fieldRoot, "AshBorderSign", new Vector2Int(-3,2), "sign_ash_border",
                LoadShadow("tomb_candle"), new Color(.9f,.52f,.3f));
            CreateEncounter(map.fieldRoot, "AshHoundA", new Vector2Int(-7,-2), LoadShadow("ash_hound"),13,16);
            CreateEncounter(map.fieldRoot, "AshHoundB", new Vector2Int(3,5), LoadShadow("ash_hound"),14,17);
            CreateEncounter(map.fieldRoot, "BannerSpearman", new Vector2Int(5,-3), LoadShadow("banner_spearman"),15,18);
            CreatePortal(map.fieldRoot, "BackToCurtainPass", new Vector2Int(-9,0), "CurtainPass",
                new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToCinderCity", new Vector2Int(9,0), "CinderCity",
                new Vector2Int(-7,-1), FacingDir.Right, false, "quest_act2_01_border_complete");
            FinishFieldScene(map, "AshBorder");
        }

        private static void CreateCinderCity()
        {
            var map = BeginFieldScene("CinderCity", "대학로 극단 거리", new Vector2Int(-7,-1),
                new Color(.14f,.075f,.055f), Theme.EmberCity);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-7,-1), "area_cinder_city");
            CreateNpc(map.fieldRoot, "EmberArchivist", new Vector2Int(2,2), LoadShadow("furnace_keeper"),
                "npc_ember_archivist", "npc_ember_archivist_intro", "npc_ember_archivist_repeat",
                "talked_ember_archivist", new Color(1f,.62f,.34f));
            CreateInteractable(map.fieldRoot, "CitySign", new Vector2Int(-2,-2), "sign_cinder_city",
                LoadShadow("tomb_candle"), new Color(.96f,.58f,.31f));
            CreatePortal(map.fieldRoot, "BackToAshBorder", new Vector2Int(-9,0), "AshBorder",
                new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToRuinedBarracks", new Vector2Int(0,8), "RuinedBarracks",
                new Vector2Int(0,-7), FacingDir.Up, false, "quest_act2_02_city_complete");
            CreatePortal(map.fieldRoot, "ToEmberCatacombs", new Vector2Int(9,0), "EmberCatacombs",
                new Vector2Int(-8,0), FacingDir.Right, false, "boss_barracks_story_complete");
            CreatePortal(map.fieldRoot, "ToPosterAlley", new Vector2Int(0,-8), "PosterAlley",
                new Vector2Int(-8,0), FacingDir.Down);
            FinishFieldScene(map, "CinderCity");
        }

        private static void CreateRuinedBarracks()
        {
            var map = BeginFieldScene("RuinedBarracks", "무너진 병영", new Vector2Int(0,-7),
                new Color(.11f,.05f,.045f), Theme.AshWastes);
            CreateEncounter(map.fieldRoot, "SootArcherA", new Vector2Int(-3,4), LoadShadow("soot_archer"),19,22);
            CreateEncounter(map.fieldRoot, "BannerSpearmanA", new Vector2Int(4,-2), LoadShadow("banner_spearman"),20,23);
            CreateEncounter(map.fieldRoot, "BellKnightA", new Vector2Int(7,2), LoadShadow("bell_knight"),21,24);
            CreateBossEncounter(map.fieldRoot, "KneelingCaptainBoss", new Vector2Int(0,5), "kneeling_captain",
                "boss_barracks_captain", new Vector2Int(24,26), "boss_barracks_pre", "boss_barracks_post",
                "boss_barracks_story_complete", "boss_barracks_reward_claimed", 26, new Color(.95f,.25f,.16f));
            CreatePortal(map.fieldRoot, "BackToCinderCity", new Vector2Int(0,-8), "CinderCity",
                new Vector2Int(0,7), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToEmberCatacombs", new Vector2Int(9,0), "EmberCatacombs",
                new Vector2Int(-8,0), FacingDir.Right, false, "boss_barracks_story_complete");
            CreatePortal(map.fieldRoot, "ToPosterAlley", new Vector2Int(-9,0), "PosterAlley",
                new Vector2Int(-8,0), FacingDir.Left);
            FinishFieldScene(map, "RuinedBarracks");
        }

        private static void CreateEmberCatacombs()
        {
            var map = BeginFieldScene("EmberCatacombs", "불씨 지하묘", new Vector2Int(-8,0),
                new Color(.055f,.04f,.065f), Theme.Catacombs);
            CreateNpc(map.fieldRoot, "AshPriest", new Vector2Int(-2,4), LoadShadow("ash_priest"),
                "npc_ash_priest", "npc_ash_priest_intro", "npc_ash_priest_repeat", "talked_ash_priest",
                new Color(.72f,.58f,.82f));
            CreateEncounter(map.fieldRoot, "TombCandleA", new Vector2Int(-4,-4), LoadShadow("tomb_candle"),22,25);
            CreateEncounter(map.fieldRoot, "TombCandleB", new Vector2Int(4,4), LoadShadow("tomb_candle"),23,26);
            CreateEncounter(map.fieldRoot, "AshPriestShadow", new Vector2Int(5,-3), LoadShadow("ash_priest"),24,27);
            CreateBossEncounter(map.fieldRoot, "HeadlessGuardBoss", new Vector2Int(0,4), "headless_guard",
                "boss_headless_guard", new Vector2Int(27,29), "boss_headless_pre", "boss_headless_post",
                "boss_catacombs_story_complete", "boss_headless_reward_claimed", 29, new Color(.54f,.31f,.48f));
            CreatePortal(map.fieldRoot, "BackToCinderCity", new Vector2Int(-9,0), "CinderCity",
                new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "BackToBarracks", new Vector2Int(9,0), "RuinedBarracks",
                new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToCrownlessThrone", new Vector2Int(0,8), "CrownlessThrone",
                new Vector2Int(0,-6), FacingDir.Up, false, "quest_act2_04_catacombs_complete");
            FinishFieldScene(map, "EmberCatacombs");
        }

        private static void CreateCrownlessThrone()
        {
            var map = BeginFieldScene("CrownlessThrone", "왕관 없는 옥좌", new Vector2Int(0,-6),
                new Color(.12f,.025f,.025f), Theme.AshThrone);
            CreateEncounter(map.fieldRoot, "RoyalBellKnight", new Vector2Int(-5,1), LoadShadow("bell_knight"),26,29);
            CreateEncounter(map.fieldRoot, "RoyalGuard", new Vector2Int(5,1), LoadShadow("headless_guard"),27,30);
            CreateBossEncounter(map.fieldRoot, "AshKingBoss", new Vector2Int(0,4), "ash_king",
                "boss_ash_king", new Vector2Int(30,32), "boss_ash_king_pre", "boss_ash_king_post",
                "boss_ash_king_story_complete", "boss_ash_king_reward_claimed", 32, new Color(1f,.2f,.10f));
            CreatePortal(map.fieldRoot, "BackToCatacombs", new Vector2Int(0,-8), "EmberCatacombs",
                new Vector2Int(0,7), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToFrostPort", new Vector2Int(9,0), "FrostPort",
                new Vector2Int(-8,0), FacingDir.Right, false, "act2_complete");
            FinishFieldScene(map, "CrownlessThrone");
        }

        private static void CreateFrostPort()
        {
            var map = BeginFieldScene("FrostPort", "물결막 수상극 항구", new Vector2Int(-8,0),
                new Color(.035f,.09f,.14f), Theme.FrostPort);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8,0), "area_frost_port");
            CreateNpc(map.fieldRoot, "GlacierFerryman", new Vector2Int(1,3), LoadShadow("glacier_ferryman"),
                "npc_glacier_ferryman", "npc_glacier_ferryman_intro", "npc_glacier_ferryman_repeat",
                "talked_glacier_ferryman", new Color(.48f,.84f,1f));
            CreateTraversalRoutePair(map.fieldRoot, "FrozenChannel", new Vector2Int(3,4), new Vector2Int(7,4),
                TraversalAbility.FreezeShallows, new Color(.45f,.88f,1f));
            CreatePortal(map.fieldRoot, "BackToThrone", new Vector2Int(-9,0), "CrownlessThrone",
                new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToWhiteArchive", new Vector2Int(9,0), "WhiteArchive",
                new Vector2Int(-7,0), FacingDir.Right, false, "quest_act3_01_port_complete");
            CreatePortal(map.fieldRoot, "ToTidalBoardwalk", new Vector2Int(0,8), "TidalBoardwalk",
                new Vector2Int(-8,0), FacingDir.Up);
            FinishFieldScene(map, "FrostPort");
        }

        private static void CreateWhiteArchive()
        {
            var map = BeginFieldScene("WhiteArchive", "등불막 그림자촌", new Vector2Int(-7,0),
                new Color(.07f,.11f,.17f), Theme.Archive);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-7,0), "area_white_archive");
            CreateNpc(map.fieldRoot, "SnowScribe", new Vector2Int(1,3), LoadShadow("snow_scribe"),
                "npc_snow_scribe", "npc_snow_scribe_intro", "npc_snow_scribe_repeat",
                "talked_snow_scribe", new Color(.72f,.93f,1f));
            CreateInteractable(map.fieldRoot, "ForbiddenIndex", new Vector2Int(-2,2), "sign_forbidden_index",
                LoadShadow("ink_fox"), new Color(.46f,.62f,1f));
            CreatePortal(map.fieldRoot, "BackToFrostPort", new Vector2Int(-9,0), "FrostPort",
                new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToForbiddenStacks", new Vector2Int(0,8), "ForbiddenStacks",
                new Vector2Int(0,-7), FacingDir.Up, false, "quest_act3_02_archive_complete");
            CreatePortal(map.fieldRoot, "ToLanternReedField", new Vector2Int(9,0), "LanternReedField",
                new Vector2Int(-8,0), FacingDir.Right);
            FinishFieldScene(map, "WhiteArchive");
        }

        private static void CreateForbiddenStacks()
        {
            var map = BeginFieldScene("ForbiddenStacks", "금단의 서가", new Vector2Int(0,-7),
                new Color(.035f,.045f,.12f), Theme.ForbiddenStacks);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(0,-7), "area_forbidden_stacks");
            CreateEncounter(map.fieldRoot, "PageBatA", new Vector2Int(-5,-2), LoadShadow("page_bat"),35,38);
            CreateEncounter(map.fieldRoot, "PageBatB", new Vector2Int(4,5), LoadShadow("page_bat"),36,39);
            CreateEncounter(map.fieldRoot, "InkFoxB", new Vector2Int(-4,4), LoadShadow("ink_fox"),35,38);
            CreateBossEncounter(map.fieldRoot, "BookDrakeBoss", new Vector2Int(0,5), "book_drake",
                "boss_book_drake", new Vector2Int(39,41), "boss_book_drake_pre", "boss_book_drake_post",
                "boss_book_drake_story_complete", "boss_book_drake_reward_claimed", 41, new Color(.32f,.63f,1f));
            CreatePortal(map.fieldRoot, "BackToWhiteArchive", new Vector2Int(0,-8), "WhiteArchive",
                new Vector2Int(0,7), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToMirrorVault", new Vector2Int(9,0), "MirrorVault",
                new Vector2Int(-8,0), FacingDir.Right, false, "boss_book_drake_story_complete");
            CreatePortal(map.fieldRoot, "ToTidalBoardwalk", new Vector2Int(-9,0), "TidalBoardwalk",
                new Vector2Int(-8,0), FacingDir.Left);
            FinishFieldScene(map, "ForbiddenStacks");
        }

        private static void CreateMirrorVault()
        {
            var map = BeginFieldScene("MirrorVault", "거울 문고", new Vector2Int(-8,0),
                new Color(.055f,.055f,.14f), Theme.MirrorVault);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8,0), "area_mirror_vault");
            CreateNpc(map.fieldRoot, "MirrorScribe", new Vector2Int(-2,3), LoadShadow("mirror_scribe"),
                "npc_mirror_scribe", "npc_mirror_scribe_intro", "npc_mirror_scribe_repeat",
                "talked_mirror_scribe", new Color(.68f,.71f,1f));
            CreateEncounter(map.fieldRoot, "GlassOwlA", new Vector2Int(-4,-3), LoadShadow("glass_owl"),38,41);
            CreateEncounter(map.fieldRoot, "MirrorScribeEcho", new Vector2Int(5,-3), LoadShadow("mirror_scribe"),39,42);
            CreateBossEncounter(map.fieldRoot, "InvertedLibrarianBoss", new Vector2Int(3,4), "inverted_librarian",
                "boss_inverted_librarian", new Vector2Int(42,44), "boss_inverted_librarian_pre",
                "boss_inverted_librarian_post", "boss_inverted_librarian_story_complete",
                "boss_inverted_librarian_reward_claimed", 44, new Color(.55f,.45f,1f));
            CreatePortal(map.fieldRoot, "BackToForbiddenStacks", new Vector2Int(-9,0), "ForbiddenStacks",
                new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToBlueAbyss", new Vector2Int(9,0), "BlueAbyss",
                new Vector2Int(0,-6), FacingDir.Right, false, "quest_act3_04_mirror_complete");
            FinishFieldScene(map, "MirrorVault");
        }

        private static void CreateBlueAbyss()
        {
            var map = BeginFieldScene("BlueAbyss", "푸른 심연 서고", new Vector2Int(0,-6),
                new Color(.018f,.025f,.09f), Theme.BlueAbyss);
            CreateEncounter(map.fieldRoot, "AbyssGlassOwl", new Vector2Int(-5,0), LoadShadow("glass_owl"),41,44);
            CreateEncounter(map.fieldRoot, "AbyssPageBat", new Vector2Int(5,0), LoadShadow("page_bat"),42,45);
            CreateBossEncounter(map.fieldRoot, "InfiniteArchiveDragonBoss", new Vector2Int(0,4),
                "infinite_archive_dragon", "boss_archive_dragon", new Vector2Int(45,48),
                "boss_archive_dragon_pre", "boss_archive_dragon_post", "boss_archive_dragon_story_complete",
                "boss_archive_dragon_reward_claimed", 48, new Color(.32f,.64f,1f));
            CreatePortal(map.fieldRoot, "BackToMirrorVault", new Vector2Int(0,-8), "MirrorVault",
                new Vector2Int(8,0), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToVioletMarsh", new Vector2Int(9,0), "VioletMarsh",
                new Vector2Int(-8,0), FacingDir.Right, false, "act3_complete");
            FinishFieldScene(map, "BlueAbyss");
        }

        private static void CreateVioletMarsh()
        {
            var map = BeginFieldScene("VioletMarsh", "자줏빛 늪", new Vector2Int(-8,0), new Color(.08f,.035f,.12f), Theme.VioletMarsh);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8,0), "area_violet_marsh");
            CreateNpc(map.fieldRoot, "MarshGuide", new Vector2Int(1,4), LoadShadow("lantern_toad"), "npc_marsh_guide", "npc_marsh_guide_intro", "npc_marsh_guide_repeat", "talked_marsh_guide", new Color(.8f,.5f,1f));
            CreateEncounter(map.fieldRoot, "MarshSerpentA", new Vector2Int(-4,-3), LoadShadow("marsh_serpent"),44,47);
            CreateEncounter(map.fieldRoot, "LanternToadA", new Vector2Int(5,-3), LoadShadow("lantern_toad"),44,48);
            CreatePortal(map.fieldRoot, "BackToBlueAbyss", new Vector2Int(-9,0), "BlueAbyss", new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToHowlVillage", new Vector2Int(9,0), "HowlVillage", new Vector2Int(-7,0), FacingDir.Right, false, "quest_act4_01_marsh_complete");
            CreatePortal(map.fieldRoot, "ToLanternReedField", new Vector2Int(0,8), "LanternReedField", new Vector2Int(-8,0), FacingDir.Up);
            FinishFieldScene(map, "VioletMarsh");
        }

        private static void CreateHowlVillage()
        {
            var map = BeginFieldScene("HowlVillage", "달가면 산촌", new Vector2Int(-7,0), new Color(.095f,.045f,.13f), Theme.HowlVillage);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-7,0), "area_howl_village");
            CreateNpc(map.fieldRoot, "MoonShaman", new Vector2Int(1,3), LoadShadow("moon_shaman"), "npc_moon_shaman", "npc_moon_shaman_intro", "npc_moon_shaman_repeat", "talked_moon_shaman", new Color(.75f,.62f,1f));
            CreatePortal(map.fieldRoot, "BackToMarsh", new Vector2Int(-9,0), "VioletMarsh", new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToMoonfangForest", new Vector2Int(9,0), "MoonfangForest", new Vector2Int(-8,-2), FacingDir.Right, false, "quest_act4_02_village_complete");
            CreatePortal(map.fieldRoot, "ToMaskDancePass", new Vector2Int(0,8), "MaskDancePass", new Vector2Int(-8,0), FacingDir.Up);
            FinishFieldScene(map, "HowlVillage");
        }

        private static void CreateMoonfangForest()
        {
            var map = BeginFieldScene("MoonfangForest", "월아 숲", new Vector2Int(-8,-2), new Color(.045f,.055f,.11f), Theme.MoonfangForest);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8,-2), "area_moonfang_forest");
            CreateEncounter(map.fieldRoot, "CrescentFoxA", new Vector2Int(-3,3), LoadShadow("crescent_fox"),50,53);
            CreateEncounter(map.fieldRoot, "ShadowStagA", new Vector2Int(4,4), LoadShadow("shadow_stag"),51,54);
            CreateEncounter(map.fieldRoot, "MaskHunterB", new Vector2Int(4,-4), LoadShadow("mask_hunter"),50,54);
            CreatePortal(map.fieldRoot, "BackToVillage", new Vector2Int(-9,-2), "HowlVillage", new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToBloodmoonRidge", new Vector2Int(0,8), "BloodmoonRidge", new Vector2Int(0,-7), FacingDir.Up, false, "quest_act4_03_forest_complete");
            CreatePortal(map.fieldRoot, "ToSleepingBeastDen", new Vector2Int(9,0), "SleepingBeastDen", new Vector2Int(0,-6), FacingDir.Right, false, "boss_bloodmane_story_complete");
            FinishFieldScene(map, "MoonfangForest");
        }

        private static void CreateBloodmoonRidge()
        {
            var map = BeginFieldScene("BloodmoonRidge", "핏빛 달고개", new Vector2Int(0,-7), new Color(.13f,.025f,.075f), Theme.BloodmoonRidge);
            CreateEncounter(map.fieldRoot, "CliffBatA", new Vector2Int(-5,0), LoadShadow("cliff_bat"),53,56);
            CreateEncounter(map.fieldRoot, "CliffBatB", new Vector2Int(5,0), LoadShadow("cliff_bat"),54,57);
            CreateBossEncounter(map.fieldRoot, "BloodmaneBoss", new Vector2Int(0,5), "bloodmane_stalker", "boss_bloodmane", new Vector2Int(57,59), "boss_bloodmane_pre", "boss_bloodmane_post", "boss_bloodmane_story_complete", "boss_bloodmane_reward_claimed", 59, new Color(.92f,.25f,.48f));
            CreatePortal(map.fieldRoot, "BackToForest", new Vector2Int(0,-8), "MoonfangForest", new Vector2Int(0,7), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToBeastDen", new Vector2Int(9,0), "SleepingBeastDen", new Vector2Int(0,-6), FacingDir.Right, false, "boss_bloodmane_story_complete");
            CreatePortal(map.fieldRoot, "ToMaskDancePass", new Vector2Int(-9,0), "MaskDancePass", new Vector2Int(-8,0), FacingDir.Left);
            FinishFieldScene(map, "BloodmoonRidge");
        }

        private static void CreateSleepingBeastDen()
        {
            var map = BeginFieldScene("SleepingBeastDen", "잠든 야수의 굴", new Vector2Int(0,-6), new Color(.055f,.018f,.09f), Theme.BeastDen);
            CreateEncounter(map.fieldRoot, "MoonHeartA", new Vector2Int(-5,0), LoadShadow("moon_heart"),57,60);
            CreateEncounter(map.fieldRoot, "CrescentFoxB", new Vector2Int(5,0), LoadShadow("crescent_fox"),57,60);
            CreateBossEncounter(map.fieldRoot, "NightDevouringBeastBoss", new Vector2Int(0,4), "night_devouring_beast", "boss_night_beast", new Vector2Int(60,63), "boss_night_beast_pre", "boss_night_beast_post", "boss_night_beast_story_complete", "boss_night_beast_reward_claimed", 63, new Color(.68f,.25f,.92f));
            CreatePortal(map.fieldRoot, "BackToRidge", new Vector2Int(0,-8), "BloodmoonRidge", new Vector2Int(8,0), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToThreadMarket", new Vector2Int(9,0), "ThreadMarket", new Vector2Int(-8,0), FacingDir.Right, false, "act4_complete");
            CreatePortal(map.fieldRoot, "ToMoonfangForest", new Vector2Int(-9,0), "MoonfangForest", new Vector2Int(-8,-2), FacingDir.Left);
            FinishFieldScene(map, "SleepingBeastDen");
        }

        private static void CreateThreadMarket()
        {
            var map = BeginFieldScene("ThreadMarket", "실타래 인형극 공방촌", new Vector2Int(-8,0), new Color(.13f,.055f,.11f), Theme.ThreadMarket);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8,0), "area_thread_market");
            CreateNpc(map.fieldRoot, "ThreadMerchant", new Vector2Int(1,4), LoadShadow("thread_cat"), "npc_thread_merchant", "npc_thread_merchant_intro", "npc_thread_merchant_repeat", "talked_thread_merchant", new Color(1f,.55f,.72f));
            CreatePortal(map.fieldRoot, "BackToBeastDen", new Vector2Int(-9,0), "SleepingBeastDen", new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToClockworkAlley", new Vector2Int(9,0), "ClockworkAlley", new Vector2Int(-8,0), FacingDir.Right, false, "quest_act5_01_market_complete");
            CreatePortal(map.fieldRoot, "ToStitchField", new Vector2Int(0,8), "StitchField", new Vector2Int(-8,0), FacingDir.Up);
            FinishFieldScene(map, "ThreadMarket");
        }

        private static void CreateClockworkAlley()
        {
            var map = BeginFieldScene("ClockworkAlley", "태엽 골목", new Vector2Int(-8,0), new Color(.12f,.07f,.075f), Theme.ClockworkAlley);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8,0), "area_clockwork_alley");
            CreateNpc(map.fieldRoot, "SecondDancer", new Vector2Int(1,3), LoadShadow("second_dancer"), "npc_second_dancer", "npc_second_dancer_intro", "npc_second_dancer_repeat", "talked_second_dancer", new Color(.65f,.82f,1f));
            CreateEncounter(map.fieldRoot, "ClockworkMouseA", new Vector2Int(-4,-4), LoadShadow("clockwork_mouse"),62,65);
            CreateEncounter(map.fieldRoot, "BrassClownA", new Vector2Int(5,-2), LoadShadow("brass_clown"),63,66);
            CreatePortal(map.fieldRoot, "BackToMarket", new Vector2Int(-9,0), "ThreadMarket", new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToOpera", new Vector2Int(0,8), "MarionetteOpera", new Vector2Int(0,-7), FacingDir.Up, false, "quest_act5_02_alley_complete");
            CreatePortal(map.fieldRoot, "ToSeveredWorkshop", new Vector2Int(9,0), "SeveredWorkshop", new Vector2Int(-8,0), FacingDir.Right, false, "boss_primadonna_story_complete");
            FinishFieldScene(map, "ClockworkAlley");
        }

        private static void CreateMarionetteOpera()
        {
            var map = BeginFieldScene("MarionetteOpera", "마리오네트 오페라", new Vector2Int(0,-7), new Color(.14f,.035f,.08f), Theme.MarionetteOpera);
            CreateEncounter(map.fieldRoot, "ChoirPuppetA", new Vector2Int(-5,0), LoadShadow("choir_puppet"),65,68);
            CreateEncounter(map.fieldRoot, "ScissorConductorA", new Vector2Int(5,0), LoadShadow("scissor_conductor"),66,69);
            CreateBossEncounter(map.fieldRoot, "PrimadonnaBoss", new Vector2Int(0,5), "wire_primadonna", "boss_primadonna", new Vector2Int(69,71), "boss_primadonna_pre", "boss_primadonna_post", "boss_primadonna_story_complete", "boss_primadonna_reward_claimed", 71, new Color(.95f,.31f,.58f));
            CreatePortal(map.fieldRoot, "BackToAlley", new Vector2Int(0,-8), "ClockworkAlley", new Vector2Int(0,7), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToWorkshop", new Vector2Int(9,0), "SeveredWorkshop", new Vector2Int(-8,0), FacingDir.Right, false, "boss_primadonna_story_complete");
            CreatePortal(map.fieldRoot, "ToStitchField", new Vector2Int(-9,0), "StitchField", new Vector2Int(-8,0), FacingDir.Left);
            FinishFieldScene(map, "MarionetteOpera");
        }

        private static void CreateSeveredWorkshop()
        {
            var map = BeginFieldScene("SeveredWorkshop", "끊어진 공방", new Vector2Int(-8,0), new Color(.09f,.055f,.09f), Theme.SeveredWorkshop);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8,0), "area_severed_workshop");
            CreateNpc(map.fieldRoot, "StitchedKnight", new Vector2Int(-1,4), LoadShadow("stitched_knight"), "npc_stitched_knight", "npc_stitched_knight_intro", "npc_stitched_knight_repeat", "talked_stitched_knight", new Color(.76f,.54f,.65f));
            CreateEncounter(map.fieldRoot, "NeedleThiefB", new Vector2Int(-4,-3), LoadShadow("needle_thief"),68,71);
            CreateEncounter(map.fieldRoot, "StitchedKnightEcho", new Vector2Int(5,-3), LoadShadow("stitched_knight"),69,72);
            CreatePortal(map.fieldRoot, "BackToOpera", new Vector2Int(-9,0), "MarionetteOpera", new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToPuppeteerStage", new Vector2Int(9,0), "PuppeteerStage", new Vector2Int(0,-6), FacingDir.Right, false, "quest_act5_04_workshop_complete");
            CreatePortal(map.fieldRoot, "ToClockworkAlley", new Vector2Int(0,8), "ClockworkAlley", new Vector2Int(-8,0), FacingDir.Up);
            FinishFieldScene(map, "SeveredWorkshop");
        }

        private static void CreatePuppeteerStage()
        {
            var map = BeginFieldScene("PuppeteerStage", "인형사의 대무대", new Vector2Int(0,-6), new Color(.11f,.018f,.065f), Theme.PuppeteerStage);
            CreateEncounter(map.fieldRoot, "RoyalChoirPuppet", new Vector2Int(-5,0), LoadShadow("choir_puppet"),71,74);
            CreateEncounter(map.fieldRoot, "RoyalScissorConductor", new Vector2Int(5,0), LoadShadow("scissor_conductor"),72,75);
            CreateBossEncounter(map.fieldRoot, "LastPuppeteerBoss", new Vector2Int(0,4), "last_puppeteer", "boss_last_puppeteer", new Vector2Int(75,78), "boss_last_puppeteer_pre", "boss_last_puppeteer_post", "boss_last_puppeteer_story_complete", "boss_last_puppeteer_reward_claimed", 78, new Color(.94f,.2f,.5f));
            CreatePortal(map.fieldRoot, "BackToWorkshop", new Vector2Int(0,-8), "SeveredWorkshop", new Vector2Int(8,0), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToErasedStation", new Vector2Int(9,0), "ErasedStation", new Vector2Int(-8,0), FacingDir.Right, false, "act5_complete");
            FinishFieldScene(map, "PuppeteerStage");
        }

        private static void CreateErasedStation()
        {
            var map = BeginFieldScene("ErasedStation", "별노래 오페라 성도", new Vector2Int(-8,0), new Color(.08f,.08f,.10f), Theme.Censor);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8,0), "area_erased_station");
            CreateNpc(map.fieldRoot, "InkConductor", new Vector2Int(1,4), LoadShadow("ink_conductor"), "npc_ink_conductor", "npc_ink_conductor_intro", "npc_ink_conductor_repeat", "talked_ink_conductor", new Color(.72f,.72f,.8f));
            CreatePortal(map.fieldRoot, "BackToPuppeteerStage", new Vector2Int(-9,0), "PuppeteerStage", new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToBlankPrison", new Vector2Int(9,0), "BlankPrison", new Vector2Int(0,-7), FacingDir.Right, false, "quest_act6_01_station_complete");
            CreatePortal(map.fieldRoot, "ToSilentFoyer", new Vector2Int(0,8), "SilentFoyer", new Vector2Int(-8,0), FacingDir.Up);
            FinishFieldScene(map, "ErasedStation");
        }

        private static void CreateBlankPrison()
        {
            var map = BeginFieldScene("BlankPrison", "백지 감옥", new Vector2Int(0,-7), new Color(.075f,.075f,.095f), Theme.Censor);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(0,-7), "area_blank_prison");
            CreateEncounter(map.fieldRoot, "NumberedPrisonerA", new Vector2Int(-5,0), LoadShadow("numbered_prisoner"),77,80);
            CreateEncounter(map.fieldRoot, "SealShackleA", new Vector2Int(5,0), LoadShadow("seal_shackle"),78,81);
            CreateBossEncounter(map.fieldRoot, "ZeroWardenBoss", new Vector2Int(0,5), "zero_warden", "boss_zero_warden", new Vector2Int(81,83), "boss_zero_warden_pre", "boss_zero_warden_post", "boss_zero_warden_story_complete", "boss_zero_warden_reward_claimed", 83, new Color(.85f,.86f,.92f));
            CreatePortal(map.fieldRoot, "BackToStation", new Vector2Int(0,-8), "ErasedStation", new Vector2Int(8,0), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToRedactionLab", new Vector2Int(9,0), "RedactionLab", new Vector2Int(-8,0), FacingDir.Right, false, "boss_zero_warden_story_complete");
            FinishFieldScene(map, "BlankPrison");
        }

        private static void CreateRedactionLab()
        {
            var map = BeginFieldScene("RedactionLab", "먹칠 연구소", new Vector2Int(-8,0), new Color(.075f,.035f,.085f), Theme.Censor);
            CreateAreaTrigger(map.fieldRoot, new Vector2Int(-8,0), "area_redaction_lab");
            CreateNpc(map.fieldRoot, "CensorSurgeon", new Vector2Int(-1,4), LoadShadow("censor_surgeon"), "npc_censor_surgeon", "npc_censor_surgeon_intro", "npc_censor_surgeon_repeat", "talked_censor_surgeon", new Color(.66f,.42f,.61f));
            CreateEncounter(map.fieldRoot, "BlackVialA", new Vector2Int(-4,-3), LoadShadow("black_vial"),80,83);
            CreateEncounter(map.fieldRoot, "CensorSurgeonEcho", new Vector2Int(5,-3), LoadShadow("censor_surgeon"),81,84);
            CreateBossEncounter(map.fieldRoot, "SubjectMBoss", new Vector2Int(3,4), "extract_subject_m", "boss_subject_m", new Vector2Int(84,86), "boss_subject_m_pre", "boss_subject_m_post", "boss_subject_m_story_complete", "boss_subject_m_reward_claimed", 86, new Color(.72f,.18f,.42f));
            CreatePortal(map.fieldRoot, "BackToPrison", new Vector2Int(-9,0), "BlankPrison", new Vector2Int(8,0), FacingDir.Left);
            CreatePortal(map.fieldRoot, "ToSilentCourt", new Vector2Int(9,0), "SilentCourt", new Vector2Int(0,-7), FacingDir.Right, false, "quest_act6_03_lab_complete");
            FinishFieldScene(map, "RedactionLab");
        }

        private static void CreateSilentCourt()
        {
            var map = BeginFieldScene("SilentCourt", "침묵 재판정", new Vector2Int(0,-7), new Color(.06f,.045f,.07f), Theme.Censor);
            CreateEncounter(map.fieldRoot, "NumberedWitness", new Vector2Int(-5,0), LoadShadow("numbered_prisoner"),83,86);
            CreateEncounter(map.fieldRoot, "MuteJudgeA", new Vector2Int(5,0), LoadShadow("mute_judge"),84,87);
            CreateBossEncounter(map.fieldRoot, "MuteJudgeBoss", new Vector2Int(0,5), "mute_judge", "boss_mute_judge", new Vector2Int(87,89), "boss_mute_judge_pre", "boss_mute_judge_post", "boss_mute_judge_story_complete", "boss_mute_judge_reward_claimed", 89, new Color(.43f,.36f,.48f));
            CreatePortal(map.fieldRoot, "BackToLab", new Vector2Int(0,-8), "RedactionLab", new Vector2Int(8,0), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToBlackArchive", new Vector2Int(9,0), "BlackArchive", new Vector2Int(0,-6), FacingDir.Right, false, "boss_mute_judge_story_complete");
            CreatePortal(map.fieldRoot, "ToSilentFoyer", new Vector2Int(-9,0), "SilentFoyer", new Vector2Int(-8,0), FacingDir.Left);
            FinishFieldScene(map, "SilentCourt");
        }

        private static void CreateBlackArchive()
        {
            var map = BeginFieldScene("BlackArchive", "검은 기록원", new Vector2Int(0,-6), new Color(.025f,.018f,.035f), Theme.BlackArchive);
            CreateEncounter(map.fieldRoot, "ArchiveBlackVial", new Vector2Int(-5,0), LoadShadow("black_vial"),86,89);
            CreateEncounter(map.fieldRoot, "ArchiveWarden", new Vector2Int(5,0), LoadShadow("zero_warden"),87,90);
            CreateBossEncounter(map.fieldRoot, "HighCensorNoxBoss", new Vector2Int(0,4), "high_censor_nox", "boss_high_censor_nox", new Vector2Int(90,93), "boss_high_censor_nox_pre", "boss_high_censor_nox_post", "boss_high_censor_nox_story_complete", "boss_high_censor_nox_reward_claimed", 93, new Color(.28f,.20f,.34f));
            CreatePortal(map.fieldRoot, "BackToCourt", new Vector2Int(0,-8), "SilentCourt", new Vector2Int(8,0), FacingDir.Down);
            CreatePortal(map.fieldRoot, "ToGlassCoast", new Vector2Int(9,0), "GlassCoast", new Vector2Int(-8,0), FacingDir.Right, false, "act6_complete");
            FinishFieldScene(map, "BlackArchive");
        }

        private static void CreateGlassCoast()
        {
            var map=BeginFieldScene("GlassCoast","유리 해안",new Vector2Int(-8,0),new Color(.025f,.10f,.14f),Theme.MemorySea);
            CreateAreaTrigger(map.fieldRoot,new Vector2Int(-8,0),"area_glass_coast");
            CreateNpc(map.fieldRoot,"WaveBard",new Vector2Int(1,4),LoadShadow("wave_bard"),"npc_wave_bard","npc_wave_bard_intro","npc_wave_bard_repeat","talked_wave_bard",new Color(.44f,.82f,.9f));
            CreateEncounter(map.fieldRoot,"GlassConchA",new Vector2Int(-4,-3),LoadShadow("glass_conch"),89,92); CreateEncounter(map.fieldRoot,"StarSandCrabA",new Vector2Int(5,-3),LoadShadow("star_sand_crab"),90,93);
            CreateTraversalRoutePair(map.fieldRoot,"RubberBoatChannel",new Vector2Int(4,3),new Vector2Int(8,3),TraversalAbility.PortableBoat,new Color(.32f,.78f,1f));
            CreatePortal(map.fieldRoot,"BackToBlackArchive",new Vector2Int(-9,0),"BlackArchive",new Vector2Int(8,0),FacingDir.Left); CreatePortal(map.fieldRoot,"ToDrownedGallery",new Vector2Int(9,0),"DrownedGallery",new Vector2Int(0,-7),FacingDir.Right,false,"quest_act7_01_coast_complete"); FinishFieldScene(map,"GlassCoast");
        }
        private static void CreateDrownedGallery()
        {
            var map=BeginFieldScene("DrownedGallery","물에 잠긴 화랑",new Vector2Int(0,-7),new Color(.02f,.07f,.12f),Theme.MemorySea);
            CreateEncounter(map.fieldRoot,"FrameJellyfishA",new Vector2Int(-5,0),LoadShadow("frame_jellyfish"),92,95); CreateEncounter(map.fieldRoot,"PigmentWraithA",new Vector2Int(5,0),LoadShadow("pigment_wraith"),93,96);
            CreateBossEncounter(map.fieldRoot,"DrownedPainterBoss",new Vector2Int(0,5),"drowned_painter","boss_drowned_painter",new Vector2Int(96,98),"boss_drowned_painter_pre","boss_drowned_painter_post","boss_drowned_painter_story_complete","boss_drowned_painter_reward_claimed",98,new Color(.3f,.64f,.82f));
            CreatePortal(map.fieldRoot,"BackToCoast",new Vector2Int(0,-8),"GlassCoast",new Vector2Int(8,0),FacingDir.Down); CreatePortal(map.fieldRoot,"ToNameIslands",new Vector2Int(9,0),"NameIslands",new Vector2Int(-8,0),FacingDir.Right,false,"boss_drowned_painter_story_complete"); FinishFieldScene(map,"DrownedGallery");
        }
        private static void CreateNameIslands()
        {
            var map=BeginFieldScene("NameIslands","이름의 군도",new Vector2Int(-8,0),new Color(.025f,.09f,.15f),Theme.MemorySea);
            CreateAreaTrigger(map.fieldRoot,new Vector2Int(-8,0),"area_name_islands"); CreateNpc(map.fieldRoot,"NameKeeper",new Vector2Int(1,4),LoadShadow("nameplate_turtle"),"npc_name_keeper","npc_name_keeper_intro","npc_name_keeper_repeat","talked_name_keeper",new Color(.45f,.82f,1f));
            CreateEncounter(map.fieldRoot,"NameBirdA",new Vector2Int(-4,-3),LoadShadow("name_bird"),95,98); CreateEncounter(map.fieldRoot,"NameplateTurtleA",new Vector2Int(5,-3),LoadShadow("nameplate_turtle"),96,99);
            CreatePortal(map.fieldRoot,"BackToGallery",new Vector2Int(-9,0),"DrownedGallery",new Vector2Int(8,0),FacingDir.Left); CreatePortal(map.fieldRoot,"ToLighthouse",new Vector2Int(9,0),"MourningLighthouse",new Vector2Int(0,-7),FacingDir.Right,false,"quest_act7_03_islands_complete"); FinishFieldScene(map,"NameIslands");
        }
        private static void CreateMourningLighthouse()
        {
            var map=BeginFieldScene("MourningLighthouse","애도의 등대",new Vector2Int(0,-7),new Color(.025f,.055f,.11f),Theme.MemorySea);
            CreateEncounter(map.fieldRoot,"StormSwallowA",new Vector2Int(-5,0),LoadShadow("storm_swallow"),98,101); CreateEncounter(map.fieldRoot,"StormSwallowB",new Vector2Int(5,0),LoadShadow("storm_swallow"),99,102);
            CreateBossEncounter(map.fieldRoot,"MourningKeeperBoss",new Vector2Int(0,5),"mourning_keeper","boss_mourning_keeper",new Vector2Int(102,104),"boss_mourning_keeper_pre","boss_mourning_keeper_post","boss_mourning_keeper_story_complete","boss_mourning_keeper_reward_claimed",104,new Color(.5f,.62f,.88f));
            CreatePortal(map.fieldRoot,"BackToIslands",new Vector2Int(0,-8),"NameIslands",new Vector2Int(8,0),FacingDir.Down); CreatePortal(map.fieldRoot,"ToMoonPalace",new Vector2Int(9,0),"WidowMoonPalace",new Vector2Int(0,-6),FacingDir.Right,false,"boss_mourning_keeper_story_complete"); FinishFieldScene(map,"MourningLighthouse");
        }
        private static void CreateWidowMoonPalace()
        {
            var map=BeginFieldScene("WidowMoonPalace","미망인의 월궁",new Vector2Int(0,-6),new Color(.035f,.025f,.10f),Theme.MoonPalace);
            CreateEncounter(map.fieldRoot,"MoonSeaGuard",new Vector2Int(-5,0),LoadShadow("mourning_keeper"),102,105); CreateEncounter(map.fieldRoot,"NameBirdMoon",new Vector2Int(5,0),LoadShadow("name_bird"),102,105);
            CreateBossEncounter(map.fieldRoot,"TrueNameSeleneBoss",new Vector2Int(0,4),"boss_moonlit_widow","boss_true_name_selene",new Vector2Int(105,108),"boss_true_name_selene_pre","boss_true_name_selene_post","boss_true_name_selene_story_complete","boss_true_name_selene_reward_claimed",108,new Color(.78f,.7f,1f));
            CreatePortal(map.fieldRoot,"BackToLighthouse",new Vector2Int(0,-8),"MourningLighthouse",new Vector2Int(8,0),FacingDir.Down);
            CreatePortal(map.fieldRoot,"ToInvertedLobby",new Vector2Int(9,0),"InvertedLobby",new Vector2Int(0,-7),FacingDir.Right,false,"act7_complete");
            FinishFieldScene(map,"WidowMoonPalace");
        }

        private static void CreateInvertedLobby()
        {
            var map=BeginFieldScene("InvertedLobby","뒤집힌 로비",new Vector2Int(0,-7),new Color(.045f,.025f,.11f),Theme.FinalTheater);
            CreateAreaTrigger(map.fieldRoot,new Vector2Int(0,-7),"area_inverted_lobby");
            CreateNpc(map.fieldRoot,"InvertedGuide",new Vector2Int(0,4),LoadShadow("inverted_guide"),"npc_inverted_guide","npc_inverted_guide_intro","npc_inverted_guide_repeat","talked_inverted_guide",new Color(.68f,.45f,.95f));
            CreateEncounter(map.fieldRoot,"CeilingAudienceA",new Vector2Int(-5,0),LoadShadow("ceiling_audience"),104,107);
            CreateEncounter(map.fieldRoot,"MirroredUnderstudyA",new Vector2Int(5,0),LoadShadow("mirrored_understudy"),105,108);
            CreatePortal(map.fieldRoot,"BackToMoonPalace",new Vector2Int(0,-8),"WidowMoonPalace",new Vector2Int(8,0),FacingDir.Down);
            CreatePortal(map.fieldRoot,"ToEndlessBackstage",new Vector2Int(9,0),"EndlessBackstage",new Vector2Int(0,-7),FacingDir.Right,false,"quest_act8_01_lobby_complete");
            FinishFieldScene(map,"InvertedLobby");
        }

        private static void CreateEndlessBackstage()
        {
            var map=BeginFieldScene("EndlessBackstage","끝없는 무대 뒤",new Vector2Int(0,-7),new Color(.035f,.02f,.075f),Theme.FinalTheater);
            CreateEncounter(map.fieldRoot,"ForgottenPropA",new Vector2Int(-5,-1),LoadShadow("forgotten_prop"),107,110);
            CreateEncounter(map.fieldRoot,"InterludeActorA",new Vector2Int(5,-1),LoadShadow("interlude_actor"),107,111);
            CreateBossEncounter(map.fieldRoot,"InterludeManagerBoss",new Vector2Int(0,5),"interlude_manager","boss_interlude_manager",new Vector2Int(110,112),"boss_interlude_manager_pre","boss_interlude_manager_post","boss_interlude_manager_story_complete","boss_interlude_manager_reward_claimed",112,new Color(.55f,.34f,.78f));
            CreatePortal(map.fieldRoot,"BackToLobby",new Vector2Int(0,-8),"InvertedLobby",new Vector2Int(8,0),FacingDir.Down);
            CreatePortal(map.fieldRoot,"ToFirstActorRoom",new Vector2Int(9,0),"FirstActorRoom",new Vector2Int(0,-6),FacingDir.Right,false,"boss_interlude_manager_story_complete");
            CreatePortal(map.fieldRoot,"ToCosmicAuditorium",new Vector2Int(0,8),"CosmicAuditorium",new Vector2Int(0,-6),FacingDir.Up,false,"boss_first_actor_story_complete");
            FinishFieldScene(map,"EndlessBackstage");
        }

        private static void CreateFirstActorRoom()
        {
            var map=BeginFieldScene("FirstActorRoom","최초 배우의 분장실",new Vector2Int(0,-6),new Color(.10f,.065f,.025f),Theme.FinalTheater);
            CreateEncounter(map.fieldRoot,"FirstLineEchoA",new Vector2Int(-5,0),LoadShadow("first_line_echo"),110,113);
            CreateEncounter(map.fieldRoot,"GoldenMaskA",new Vector2Int(5,0),LoadShadow("golden_mask"),111,114);
            CreateBossEncounter(map.fieldRoot,"FirstActorBoss",new Vector2Int(0,4),"legend_first_actor","boss_first_actor",new Vector2Int(113,115),"boss_first_actor_pre","boss_first_actor_post","boss_first_actor_story_complete","boss_first_actor_reward_claimed",115,new Color(.92f,.68f,.28f));
            CreatePortal(map.fieldRoot,"BackToBackstage",new Vector2Int(0,-8),"EndlessBackstage",new Vector2Int(8,0),FacingDir.Down);
            CreatePortal(map.fieldRoot,"ToCosmicAuditorium",new Vector2Int(9,0),"CosmicAuditorium",new Vector2Int(0,-6),FacingDir.Right,false,"boss_first_actor_story_complete");
            FinishFieldScene(map,"FirstActorRoom");
        }

        private static void CreateCosmicAuditorium()
        {
            var map=BeginFieldScene("CosmicAuditorium","우주의 객석",new Vector2Int(0,-6),new Color(.012f,.02f,.075f),Theme.CosmicStage);
            CreateEncounter(map.fieldRoot,"ConstellationSeatA",new Vector2Int(-5,0),LoadShadow("constellation_seat"),113,116);
            CreateEncounter(map.fieldRoot,"SilentApplauseA",new Vector2Int(5,0),LoadShadow("silent_applause"),114,117);
            CreateBossEncounter(map.fieldRoot,"LastAudienceBoss",new Vector2Int(0,4),"legend_last_audience","boss_last_audience",new Vector2Int(116,118),"boss_last_audience_pre","boss_last_audience_post","boss_last_audience_story_complete","boss_last_audience_reward_claimed",118,new Color(.48f,.55f,1f));
            CreatePortal(map.fieldRoot,"BackToFirstActor",new Vector2Int(0,-8),"FirstActorRoom",new Vector2Int(8,0),FacingDir.Down);
            CreatePortal(map.fieldRoot,"ToFinalCurtain",new Vector2Int(9,0),"FinalCurtain",new Vector2Int(0,-6),FacingDir.Right,false,"boss_last_audience_story_complete");
            CreatePortal(map.fieldRoot,"ToEndlessBackstage",new Vector2Int(-9,0),"EndlessBackstage",new Vector2Int(0,-7),FacingDir.Left);
            FinishFieldScene(map,"CosmicAuditorium");
        }

        private static void CreateFinalCurtain()
        {
            var map=BeginFieldScene("FinalCurtain","마지막 장막",new Vector2Int(0,-6),new Color(.035f,.025f,.055f),Theme.CosmicStage);
            CreateAreaTrigger(map.fieldRoot,new Vector2Int(0,-6),"area_final_curtain");
            CreateNpc(map.fieldRoot,"RivalDirector",new Vector2Int(-5,2),LoadShadow("rival_director"),"npc_rival_director","npc_rival_director_final","npc_rival_director_repeat","talked_rival_director_final",new Color(.9f,.28f,.52f));
            CreateEncounter(map.fieldRoot,"CensoredProtagonistA",new Vector2Int(5,-1),LoadShadow("censored_protagonist"),116,119);
            CreateBossEncounter(map.fieldRoot,"OutsideScriptShadowBoss",new Vector2Int(0,3),"outside_script_shadow","boss_final_shadow",new Vector2Int(119,120),"boss_final_shadow_pre","boss_final_shadow_post","boss_final_shadow_story_complete","boss_final_shadow_reward_claimed",120,new Color(.92f,.82f,1f));
            CreateEndingTrigger(map.fieldRoot,new Vector2Int(0,7));
            CreatePortal(map.fieldRoot,"BackToAuditorium",new Vector2Int(0,-8),"CosmicAuditorium",new Vector2Int(8,0),FacingDir.Down);
            FinishFieldScene(map,"FinalCurtain");
        }

        private static FieldSceneContext BeginFieldScene(string mapId, string displayName, Vector2Int startCell,
                                                         Color cameraColor, Theme theme)
        {
            var scene = NewScene();
            CreateThemeTiles(theme);
            InstantiatePrefab("Assets/Prefabs/Systems/CoreSystems.prefab");
            var fieldRoot = new GameObject("FieldRoot");
            fieldRoot.AddComponent<ShadowCompanionFollower>();
            var companionEvents = fieldRoot.AddComponent<CompanionRegionEventController>();
            var gridObject = new GameObject("Grid", typeof(Grid), typeof(FieldGrid));
            gridObject.transform.SetParent(fieldRoot.transform);
            var ground = CreateTilemap("Ground", gridObject.transform, 0, false);
            var collision = CreateTilemap("Collision", gridObject.transform, 2, true);
            PaintMap(ground, collision, theme);

            var gridSo = new SerializedObject(gridObject.GetComponent<FieldGrid>());
            Set(gridSo, "mapId", mapId);
            Set(gridSo, "groundTilemap", ground);
            Set(gridSo, "collisionTilemap", collision);
            gridSo.FindProperty("unitMask").intValue = 1 << 0;
            gridSo.ApplyModifiedPropertiesWithoutUndo();

            var player = CreatePlayer(fieldRoot.transform, startCell, theme);
            CreateCamera("FieldCamera", cameraColor, player.transform, true).transform.SetParent(fieldRoot.transform);
            CreateEnvironment(fieldRoot.transform, theme);
            ApplyPixelFieldPass(fieldRoot.transform, theme);
            RegionData region = LoadRegionCatalog().regions.Find(x => x.sceneName == mapId);
            if (region != null)
            {
                var companionEventSo = new SerializedObject(companionEvents);
                Set(companionEventSo, "regionId", region.regionId);
                companionEventSo.ApplyModifiedPropertiesWithoutUndo();
                var worldState = fieldRoot.AddComponent<FieldWorldStateController>();
                var worldStateSo = new SerializedObject(worldState);
                Set(worldStateSo, "regionId", region.regionId);
                worldStateSo.ApplyModifiedPropertiesWithoutUndo();
                var weatherEffects = fieldRoot.AddComponent<FieldWeatherEffects>();
                var weatherEffectsSo = new SerializedObject(weatherEffects);
                Set(weatherEffectsSo, "regionId", region.regionId);
                weatherEffectsSo.ApplyModifiedPropertiesWithoutUndo();
                if (!region.isSettlement && !region.isBossStage)
                {
                    var incidentCell=FindRegionalEncounterCell(collision,startCell,new HashSet<Vector2Int>{startCell},StableHash("incident_"+region.regionId));
                    var incidentObject=CreateFieldActor(fieldRoot.transform,"FieldIncident",incidentCell,LoadShadow("crow"),false);
                    var incident = incidentObject.AddComponent<FieldIncidentController>();
                    var incidentSo = new SerializedObject(incident);
                    Set(incidentSo, "regionId", region.regionId);
                    incidentSo.ApplyModifiedPropertiesWithoutUndo();
                }
                if (region.isSettlement) CreateTownServices(fieldRoot.transform, region);
            }
            if (region?.AllowsRandomEncounters == true)
            {
                var encounters = fieldRoot.AddComponent<RandomEncounterController>();
                var encounterSo = new SerializedObject(encounters);
                Set(encounterSo, "regionId", region.regionId);
                RegionalEncounterPolicy.GetCadence(region, out int safeMin, out int safeMax,
                    out float chancePerStep, out int pitySteps);
                Set(encounterSo, "safeStepsMin", safeMin);
                Set(encounterSo, "safeStepsMax", safeMax);
                Set(encounterSo, "chancePerStep", chancePerStep);
                Set(encounterSo, "pitySteps", pitySteps);
                encounterSo.ApplyModifiedPropertiesWithoutUndo();
            }
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

            // 필드 전용 Canvas는 반드시 FieldRoot 아래에 둔다. 그래야 전투 진입 시
            // 각본집/파티/월드맵/상점 버튼이 전투 UI 위에 남지 않는다.
            InstantiatePrefab("Assets/Prefabs/UI/DialogueCanvas.prefab", fieldRoot.transform);
            InstantiatePrefab("Assets/Prefabs/UI/RegionArrivalCanvas.prefab", fieldRoot.transform);
            InstantiatePrefab("Assets/Prefabs/UI/QuestHUDCanvas.prefab", fieldRoot.transform);
            InstantiatePrefab("Assets/Prefabs/UI/ScriptBookCanvas.prefab", fieldRoot.transform);
            InstantiatePrefab("Assets/Prefabs/UI/PartyStorageCanvas.prefab", fieldRoot.transform);
            InstantiatePrefab("Assets/Prefabs/UI/WorldMapCanvas.prefab", fieldRoot.transform);
            InstantiatePrefab("Assets/Prefabs/UI/SettlementShopCanvas.prefab", fieldRoot.transform);
            InstantiatePrefab("Assets/Prefabs/UI/FieldInventoryCanvas.prefab", fieldRoot.transform);
            InstantiatePrefab("Assets/Prefabs/UI/QuestLogCanvas.prefab", fieldRoot.transform);
            InstantiatePrefab("Assets/Prefabs/UI/BossInvestigationJournalCanvas.prefab", fieldRoot.transform);
            InstantiatePrefab("Assets/Prefabs/UI/FieldPauseCanvas.prefab", fieldRoot.transform);
            CreateFader();
            CreateEventSystem();
            return new FieldSceneContext { scene = scene, fieldRoot = fieldRoot.transform };
        }

        private static void FinishFieldScene(FieldSceneContext context, string sceneName)
        {
            foreach (EncounterSymbol symbol in context.fieldRoot.GetComponentsInChildren<EncounterSymbol>(true))
                if (!symbol.IsFixed) Object.DestroyImmediate(symbol.gameObject);
            Save(context.scene, sceneName);
        }

        private static PlayerController CreatePlayer(Transform parent, Vector2Int cell, Theme theme)
        {
            var go = new GameObject("Player", typeof(Rigidbody2D),
                typeof(CapsuleCollider2D), typeof(PlayerController));
            go.transform.SetParent(parent);
            go.transform.position = Cell(cell);
            go.tag = "Player";
            var visual = new GameObject("Visual", typeof(SpriteRenderer), typeof(PixelCharacterMotion));
            visual.transform.SetParent(go.transform, false);
            var renderer = visual.GetComponent<SpriteRenderer>();
            var sprites = LoadPixelPlayerSprites();
            renderer.sprite = sprites != null && sprites.down != null && sprites.down.Length > 0
                ? sprites.down[0]
                : LoadShadow("knight")?.silhouetteSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 10;
            FieldSpriteSizing.Fit(renderer);
            go.AddComponent<GroundContactShadow>().Configure(renderer);
            go.GetComponent<Rigidbody2D>().gravityScale = 0f;
            go.GetComponent<CapsuleCollider2D>().size = new Vector2(0.55f, 0.75f);
            var so = new SerializedObject(go.GetComponent<PlayerController>());
            Set(so, "silhouette", renderer);
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.GetComponent<PixelCharacterMotion>().Configure(go.GetComponent<PlayerController>(), renderer);
            if (sprites != null)
            {
                var pixelAnimator = go.AddComponent<PixelFieldAnimator>();
                var animatorSo = new SerializedObject(pixelAnimator);
                Set(animatorSo, "controller", go.GetComponent<PlayerController>());
                Set(animatorSo, "target", renderer);
                SetSpriteArray(animatorSo.FindProperty("downFrames"), sprites.down);
                SetSpriteArray(animatorSo.FindProperty("upFrames"), sprites.up);
                SetSpriteArray(animatorSo.FindProperty("sideFrames"), sprites.side);
                animatorSo.ApplyModifiedPropertiesWithoutUndo();
            }
            FootstepSurface surface = SurfaceFor(theme);
            var traversal = go.AddComponent<FieldTraversalEffects>();
            var traversalSo = new SerializedObject(traversal);
            Set(traversalSo, "controller", go.GetComponent<PlayerController>());
            Set(traversalSo, "defaultSurface", (int)surface);
            traversalSo.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<FieldEnvironmentReactor>().Configure(go.GetComponent<PlayerController>());
            go.AddComponent<FieldInteractionFeedback>().Configure(go.GetComponent<PlayerController>());
            var footstep = go.AddComponent<FieldFootstepAudio>();
            var footstepSo = new SerializedObject(footstep);
            Set(footstepSo, "controller", go.GetComponent<PlayerController>());
            Set(footstepSo, "surface", (int)surface);
            AudioClip[] clips = FieldAudioAssetGenerator.LoadFootsteps(surface);
            SerializedProperty clipProperty = footstepSo.FindProperty("clips");
            clipProperty.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
                clipProperty.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            footstepSo.ApplyModifiedPropertiesWithoutUndo();
            return go.GetComponent<PlayerController>();
        }

        private static FootstepSurface SurfaceFor(Theme theme)
        {
            switch (theme)
            {
                case Theme.Theater:
                case Theme.MarionetteOpera:
                case Theme.PuppeteerStage:
                case Theme.FinalTheater: return FootstepSurface.Wood;
                case Theme.Village:
                case Theme.Meadow:
                case Theme.VioletMarsh:
                case Theme.HowlVillage:
                case Theme.MoonfangForest: return FootstepSurface.Grass;
                case Theme.AshWastes:
                case Theme.AshThrone: return FootstepSurface.Ash;
                case Theme.FrostPort:
                case Theme.Archive:
                case Theme.ForbiddenStacks: return FootstepSurface.Snow;
                case Theme.MemorySea: return FootstepSurface.Water;
                case Theme.ClockworkAlley:
                case Theme.SeveredWorkshop: return FootstepSurface.Metal;
                case Theme.BlueAbyss:
                case Theme.CosmicStage: return FootstepSurface.Void;
                default: return FootstepSurface.Stone;
            }
        }

        private static PlayerSpriteSet LoadPixelPlayerSprites()
        {
            if (_pixelPlayerSprites != null) return _pixelPlayerSprites;
            Sprite down = FieldArtAtlasImporter.PlayerFrame("Down0");
            if (down != null) return _pixelPlayerSprites = new PlayerSpriteSet
            {
                down = new[] { down, FieldArtAtlasImporter.PlayerFrame("Down1") },
                up = new[] { FieldArtAtlasImporter.PlayerFrame("Up0"), FieldArtAtlasImporter.PlayerFrame("Up1") },
                side = new[] { FieldArtAtlasImporter.PlayerFrame("Side0"), FieldArtAtlasImporter.PlayerFrame("Side1") }
            };
            PlayerSpriteSet final = LoadFinalPlayerSprites();
            if (final != null) return _pixelPlayerSprites = final;
            _pixelPlayerSprites = new PlayerSpriteSet
            {
                down = new[] { CreatePixelPlayerSprite("Director_Down_0", FacingDir.Down, 0),
                               CreatePixelPlayerSprite("Director_Down_1", FacingDir.Down, 1) },
                up = new[] { CreatePixelPlayerSprite("Director_Up_0", FacingDir.Up, 0),
                             CreatePixelPlayerSprite("Director_Up_1", FacingDir.Up, 1) },
                side = new[] { CreatePixelPlayerSprite("Director_Side_0", FacingDir.Right, 0),
                               CreatePixelPlayerSprite("Director_Side_1", FacingDir.Right, 1) }
            };
            return _pixelPlayerSprites;
        }

        private static PlayerSpriteSet LoadFinalPlayerSprites()
        {
            Sprite[] LoadDirection(string direction)
            {
                var result = new Sprite[2];
                for (int i = 0; i < result.Length; i++)
                {
                    string path = $"{FinalPlayerFolder}/director_{direction}_{i + 1:00}.png";
                    result[i] = LoadFinalPixelSprite(path, 32f, new Vector2(.5f, 0f));
                    if (result[i] == null) return null;
                }
                return result;
            }

            Sprite[] down = LoadDirection("down");
            Sprite[] up = LoadDirection("up");
            Sprite[] side = LoadDirection("side");
            return down == null || up == null || side == null ? null :
                new PlayerSpriteSet { down = down, up = up, side = side };
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
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            if (_litMaterial != null) renderer.sharedMaterial = _litMaterial;
            if (collider) go.AddComponent<TilemapCollider2D>();
            return go.GetComponent<Tilemap>();
        }

        private static void PaintMap(Tilemap ground, Tilemap collision, Theme theme)
        {
            if (theme == Theme.Meadow)
            {
                PaintMoonlitPixelMap(ground, collision);
                return;
            }

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
                case Theme.AshWastes:
                    blocks.Add(new RectInt(-7,3,3,2)); blocks.Add(new RectInt(4,4,3,2));
                    blocks.Add(new RectInt(-5,-5,2,3)); blocks.Add(new RectInt(3,-6,2,3));
                    blocks.Add(new RectInt(-1,-1,2,2)); break;
                case Theme.EmberCity:
                    blocks.Add(new RectInt(-6,3,3,3)); blocks.Add(new RectInt(4,3,3,3));
                    blocks.Add(new RectInt(-6,-6,3,3)); blocks.Add(new RectInt(4,-6,3,3));
                    blocks.Add(new RectInt(-1,1,2,4)); break;
                case Theme.Catacombs:
                    blocks.Add(new RectInt(-7,2,5,2)); blocks.Add(new RectInt(3,2,5,2));
                    blocks.Add(new RectInt(-3,-5,2,5)); blocks.Add(new RectInt(2,-5,2,5));
                    blocks.Add(new RectInt(-1,5,2,2)); break;
                case Theme.FrostPort:
                    blocks.Add(new RectInt(-6,3,3,3)); blocks.Add(new RectInt(4,3,3,3));
                    blocks.Add(new RectInt(-3,-6,2,2)); blocks.Add(new RectInt(5,-5,2,3)); break;
                case Theme.Archive:
                    blocks.Add(new RectInt(-6,3,3,3)); blocks.Add(new RectInt(4,3,3,3));
                    blocks.Add(new RectInt(-5,-6,3,2)); blocks.Add(new RectInt(4,-6,3,2)); break;
                case Theme.ForbiddenStacks:
                    blocks.Add(new RectInt(-7,1,3,4)); blocks.Add(new RectInt(5,1,3,4));
                    blocks.Add(new RectInt(-3,-4,2,5)); blocks.Add(new RectInt(2,-4,2,5)); break;
                case Theme.MirrorVault:
                    blocks.Add(new RectInt(-6,2,2,4)); blocks.Add(new RectInt(5,2,2,4));
                    blocks.Add(new RectInt(-2,-5,2,3)); blocks.Add(new RectInt(2,-5,2,3)); break;
                case Theme.VioletMarsh:
                case Theme.HowlVillage:
                    blocks.Add(new RectInt(-6,3,3,3)); blocks.Add(new RectInt(4,3,3,3));
                    blocks.Add(new RectInt(-5,-6,2,3)); blocks.Add(new RectInt(4,-5,2,2)); break;
                case Theme.MoonfangForest:
                    blocks.Add(new RectInt(-7,2,3,3)); blocks.Add(new RectInt(5,3,3,3));
                    blocks.Add(new RectInt(-2,-5,2,3)); blocks.Add(new RectInt(2,-6,2,3)); break;
                case Theme.ThreadMarket:
                case Theme.ClockworkAlley:
                case Theme.SeveredWorkshop:
                    blocks.Add(new RectInt(-6,3,3,3)); blocks.Add(new RectInt(4,3,3,3));
                    blocks.Add(new RectInt(-5,-6,3,2)); blocks.Add(new RectInt(4,-6,3,2)); break;
                case Theme.Censor:
                case Theme.MemorySea:
                case Theme.FinalTheater:
                    blocks.Add(new RectInt(-7,2,3,4)); blocks.Add(new RectInt(5,2,3,4));
                    blocks.Add(new RectInt(-3,-5,2,3)); blocks.Add(new RectInt(2,-5,2,3)); break;
                case Theme.Boss:
                case Theme.AshThrone:
                case Theme.BlueAbyss:
                case Theme.BloodmoonRidge:
                case Theme.BeastDen:
                case Theme.MarionetteOpera:
                case Theme.PuppeteerStage:
                case Theme.BlackArchive:
                case Theme.MoonPalace:
                case Theme.CosmicStage:
                    blocks.Add(new RectInt(-7, -1, 2, 5)); blocks.Add(new RectInt(6, -1, 2, 5));
                    blocks.Add(new RectInt(-4, 6, 2, 2)); blocks.Add(new RectInt(3, 6, 2, 2)); break;
            }
            foreach (var block in blocks)
                for (int y = block.yMin; y < block.yMax; y++)
                for (int x = block.xMin; x < block.xMax; x++) collision.SetTile(new Vector3Int(x, y, 0), _accentTile);
        }

        private static void PaintMoonlitPixelMap(Tilemap ground, Tilemap collision)
        {
            for (int y = -9; y <= 9; y++)
            for (int x = -11; x <= 11; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool walkable = IsMoonlitWalkable(x, y);
                bool path = IsMoonlitPath(x, y);
                ground.SetTile(cell, path ? _meadowPathTile : _meadowGrassTile);
                if (walkable) continue;

                bool border = x == -11 || x == 11 || y == -9 || y == 9;
                bool water = x >= 6 && y >= -2 && !border;
                bool cliff = border || (x <= -6 && y >= 2) || (x >= -1 && x <= 1 && y <= -5);
                collision.SetTile(cell, water ? _meadowWaterTile : cliff ? _meadowCliffTile : _meadowBushTile);
            }
        }

        private static bool IsMoonlitWalkable(int x, int y)
        {
            bool westEntrance = x >= -10 && x <= -6 && y >= -1 && y <= 1;
            bool eastExit = x >= 3 && x <= 10 && y >= -1 && y <= 1;
            bool centralClearing = x >= -8 && x <= 5 && y >= -4 && y <= 2;
            bool northernTrail = x >= -4 && x <= -2 && y >= 2 && y <= 8;
            bool upperLoop = x >= -5 && x <= 4 && y >= 3 && y <= 5;
            bool southernLoop = x >= -7 && x <= 5 && y >= -6 && y <= -3;
            bool eastConnector = x >= 3 && x <= 5 && y >= -5 && y <= 4;
            // 확장 지역은 최대 네 방향 포털을 사용한다. 기존 숲길의 형태는 유지하되
            // 북·남쪽 가장자리까지 한 칸 폭의 출구를 열어 (0,±8) 포털이 벽에 묻히지 않게 한다.
            bool northExit = x == 0 && y >= 2 && y <= 8;
            bool southExit = x == 0 && y >= -8 && y <= -3;
            bool centralGrove = x >= -1 && x <= 1 && y >= 2 && y <= 4;
            bool westernCliff = x <= -6 && y >= 2;
            bool easternWater = x >= 6 && y >= -2;
            bool lowerRuin = x >= -1 && x <= 1 && y <= -5;
            return (westEntrance || eastExit || centralClearing || northernTrail || upperLoop || southernLoop ||
                    eastConnector || northExit || southExit)
                   && (!centralGrove || northExit) && !westernCliff && (!easternWater || eastExit) &&
                   (!lowerRuin || southExit);
        }

        private static bool IsMoonlitPath(int x, int y)
        {
            bool westRoad = x <= -4 && y >= -1 && y <= 1;
            bool northRoad = x >= -4 && x <= -2 && y >= 0;
            bool southRoad = y >= -5 && y <= -3 && x >= -5 && x <= 4;
            bool eastRoad = x >= 3 && x <= 10 && y >= -4 && y <= 4;
            bool verticalExitRoad = x == 0 && (y >= 2 || y <= -3);
            return IsMoonlitWalkable(x, y) && (westRoad || northRoad || southRoad || eastRoad || verticalExitRoad);
        }

        private static GameObject CreateNpc(Transform parent, string name, Vector2Int cell, ShadowData visual,
                                            string npcId, string first, string repeat, string flag, Color accent)
        {
            var go = CreateFieldActor(parent, name, cell, visual, false);
            var npc = go.AddComponent<StoryNpc>();
            go.AddComponent<FieldNpcPresence>();
            var so = new SerializedObject(npc);
            Set(so, "npcId", npcId); Set(so, "firstDialogueId", first); Set(so, "repeatDialogueId", repeat);
            Set(so, "completionFlag", flag); Set(so, "dialogueAccent", accent);
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static Vector2Int TownServiceCell(Transform parent, Vector2Int preferred)
        {
            FieldGrid grid = parent.GetComponentInChildren<FieldGrid>();
            if (grid != null && grid.TryFindNearestWalkable(preferred, out Vector2Int cell, null, 12, false))
                return cell;
            throw new System.InvalidOperationException("마을 서비스 NPC의 이동 가능한 배치 좌표가 없습니다.");
        }

        private static void CreateTownServices(Transform parent, RegionData region)
        {
            var restoration=parent.gameObject.AddComponent<TownRestorationVisualController>();
            var stageOne=new List<GameObject>{CreateRestorationProp(parent,"RelitFootlights",new Vector2Int(-6,2),region.AccentColor,1.0f)};
            var stageTwo=new List<GameObject>{CreateRestorationProp(parent,"RepairedFacade",new Vector2Int(6,2),Color.Lerp(region.AccentColor,Color.white,.25f),1.45f),CreateRestorationProp(parent,"FestivalBanner",new Vector2Int(-7,5),region.AccentColor,.75f)};
            var stageThree=new List<GameObject>{CreateRestorationProp(parent,"RestoredGrandStage",new Vector2Int(0,6),Color.Lerp(region.AccentColor,Color.white,.45f),2.1f),CreateRestorationProp(parent,"MarketLantern",new Vector2Int(7,5),new Color(1f,.72f,.3f),.8f)};
            var restorationSo=new SerializedObject(restoration);Set(restorationSo,"regionId",region.regionId);Set(restorationSo,"restoredLightColor",Color.Lerp(region.AccentColor,new Color(1f,.78f,.48f),.55f));SetObjectArray(restorationSo.FindProperty("stageOneObjects"),stageOne);SetObjectArray(restorationSo.FindProperty("stageTwoObjects"),stageTwo);SetObjectArray(restorationSo.FindProperty("stageThreeObjects"),stageThree);restorationSo.ApplyModifiedPropertiesWithoutUndo();
            var healer = CreateFieldActor(parent, "TownHealer", TownServiceCell(parent, new Vector2Int(-4, 4)),
                LoadShadow("mage"), false).AddComponent<TownHealerNpc>();
            healer.gameObject.AddComponent<FieldNpcPresence>();
            var healerSo = new SerializedObject(healer);
            Set(healerSo, "regionId", region.regionId);
            Set(healerSo, "restCost", 30);
            healerSo.ApplyModifiedPropertiesWithoutUndo();

            var rumor = CreateFieldActor(parent, "TownRumorKeeper", TownServiceCell(parent, new Vector2Int(4, 4)),
                LoadShadow("crow"), false).AddComponent<TownRumorNpc>();
            rumor.gameObject.AddComponent<FieldNpcPresence>();
            var rumorSo = new SerializedObject(rumor);
            Set(rumorSo, "regionId", region.regionId);
            Set(rumorSo, "dialogueId", TownServicePolicy.RumorDialogueId(region));
            Set(rumorSo, "accent", region.AccentColor);
            rumorSo.ApplyModifiedPropertiesWithoutUndo();

            var investigator = CreateFieldActor(parent, "BossInvestigator", TownServiceCell(parent, new Vector2Int(0, 5)),
                LoadShadow("mask"), false).AddComponent<BossInvestigatorNpc>();
            investigator.gameObject.AddComponent<FieldNpcPresence>();
            var investigatorSo = new SerializedObject(investigator);
            Set(investigatorSo, "act", region.act);
            investigatorSo.ApplyModifiedPropertiesWithoutUndo();

            CreateRecoveryNpc(parent, region, "ReturningCraftsman", new Vector2Int(-2, 5), 2, "puppet");
            CreateRecoveryNpc(parent, region, "RestoredMerchant", new Vector2Int(2, 5), 3, "mage");
        }

        private static void CreateRecoveryNpc(Transform parent, RegionData region, string name,
            Vector2Int cell, int stage, string shadowId)
        {
            var actor=CreateFieldActor(parent, name, TownServiceCell(parent, cell), LoadShadow(shadowId), false);var npc = actor.AddComponent<TownRecoveryNpc>();
            actor.AddComponent<FieldNpcPresence>();
            var so = new SerializedObject(npc);
            Set(so, "regionId", region.regionId); Set(so, "requiredStage", stage);
            so.ApplyModifiedPropertiesWithoutUndo();
            var patrol=actor.AddComponent<TownNpcPatrol>();var patrolSo=new SerializedObject(patrol);Set(patrolSo,"requiredStage",stage);Set(patrolSo,"localOffset",stage>=3?new Vector2(-2f,0):new Vector2(2f,0));Set(patrolSo,"speed",stage>=3?.52f:.68f);patrolSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateRestorationProp(Transform parent,string name,Vector2Int cell,Color color,float scale)
        {var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(parent);go.transform.position=Cell(cell);go.transform.localScale=Vector3.one*scale;var r=go.GetComponent<SpriteRenderer>();r.sprite=_accentTile!=null?_accentTile.sprite:null;r.color=color;r.sortingOrder=4;go.SetActive(false);return go;}

        private static void CreateInteractable(Transform parent, string name, Vector2Int cell, string dialogueId,
                                               ShadowData visual, Color accent)
        {
            var go = CreateFieldActor(parent, name, cell, visual, false);
            var interactable = go.AddComponent<DialogueInteractable>();
            var so = new SerializedObject(interactable);
            Set(so, "dialogueId", dialogueId); Set(so, "accent", accent);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateHiddenRegionChallenge(Transform parent, RegionData region)
        {
            string hintDialogue;
            string completeDialogue;
            string npcId;
            string npcFirst;
            string npcRepeat;
            string npcShadowId;
            string rareShadowId;
            string encounterId;
            string rewardDialogue;
            string rewardItemId;
            string rewardMessage;
            int rewardItemCount;
            int rewardGold;
            int minLevel;
            int maxLevel;
            BattleGimmick battleGimmick;
            int[] sequence;
            Vector2Int[] nodeCells;
            Vector2Int npcCell;
            Vector2Int encounterCell;
            Vector2Int rewardCell;

            switch (region.regionId)
            {
                case "ashes_bell_tower":
                    hintDialogue = "hidden_tower_hint"; completeDialogue = "hidden_tower_complete";
                    npcId = "ashes_watchman_memory"; npcFirst = "hidden_tower_memory_intro";
                    npcRepeat = "hidden_tower_memory_repeat"; npcShadowId = "crow";
                    rareShadowId = "bell_knight"; encounterId = "hidden_ashes_bell_tower_rare";
                    rewardDialogue = "hidden_tower_reward"; rewardItemId = "silver_ink";
                    rewardItemCount = 1; rewardGold = 150;
                    rewardMessage = "진명의 은빛 잉크 1개 · 금화 150 획득";
                    minLevel = 14; maxLevel = 17; sequence = new[] { 0, 2, 1 };
                    battleGimmick = BattleGimmick.BellToll;
                    nodeCells = new[] { new Vector2Int(-6,0), new Vector2Int(0,3), new Vector2Int(6,0) };
                    npcCell = new Vector2Int(0,5); encounterCell = new Vector2Int(6,-3); rewardCell = new Vector2Int(0,-4);
                    break;
                case "inverted_moon_path":
                    hintDialogue = "hidden_moon_path_hint"; completeDialogue = "hidden_moon_path_complete";
                    npcId = "moon_path_messenger_memory"; npcFirst = "hidden_moon_path_memory_intro";
                    npcRepeat = "hidden_moon_path_memory_repeat"; npcShadowId = "crescent_fox";
                    rareShadowId = "moon_heart"; encounterId = "hidden_inverted_moon_path_rare";
                    rewardDialogue = "hidden_moon_path_reward"; rewardItemId = "focus_elixir";
                    rewardItemCount = 2; rewardGold = 240;
                    rewardMessage = "무대 열기 영약 2개 · 금화 240 획득";
                    minLevel = 61; maxLevel = 65; sequence = new[] { 1, 0, 2 };
                    battleGimmick = BattleGimmick.MoonReversal;
                    nodeCells = new[] { new Vector2Int(-6,0), new Vector2Int(0,3), new Vector2Int(6,0) };
                    npcCell = new Vector2Int(0,5); encounterCell = new Vector2Int(6,-3); rewardCell = new Vector2Int(0,-4);
                    break;
                case "windless_echo_cavern":
                    hintDialogue = "hidden_echo_cavern_hint"; completeDialogue = "hidden_echo_cavern_complete";
                    npcId = "echo_cavern_listener_memory"; npcFirst = "hidden_echo_cavern_memory_intro";
                    npcRepeat = "hidden_echo_cavern_memory_repeat"; npcShadowId = "cliff_bat";
                    rareShadowId = "mask_hunter"; encounterId = "hidden_windless_echo_cavern_rare";
                    rewardDialogue = "hidden_echo_cavern_reward"; rewardItemId = "greater_potion";
                    rewardItemCount = 2; rewardGold = 280;
                    rewardMessage = "농축 회복 시약 2개 · 금화 280 획득";
                    minLevel = 65; maxLevel = 69; sequence = new[] { 2, 0, 1 };
                    battleGimmick = BattleGimmick.EchoSeal;
                    nodeCells = new[] { new Vector2Int(-5,-3), new Vector2Int(0,3), new Vector2Int(5,-3) };
                    npcCell = new Vector2Int(0,5); encounterCell = new Vector2Int(5,4); rewardCell = new Vector2Int(0,-4);
                    break;
                default: return;
            }

            Color accent = region.AccentColor;
            CreateInteractable(parent, "PuzzleHint", new Vector2Int(-2,-2), hintDialogue,
                LoadShadow(npcShadowId), accent);

            var puzzleObject = new GameObject("HiddenSequencePuzzle", typeof(HiddenRegionSequencePuzzle));
            puzzleObject.transform.SetParent(parent, false);
            var puzzle = puzzleObject.GetComponent<HiddenRegionSequencePuzzle>();
            var nodes = new HiddenRegionPuzzleNode[nodeCells.Length];
            for (int i = 0; i < nodeCells.Length; i++)
            {
                var nodeObject = new GameObject($"PuzzleNode_{i + 1}", typeof(SpriteRenderer),
                    typeof(BoxCollider2D), typeof(HiddenRegionPuzzleNode));
                nodeObject.transform.SetParent(parent); nodeObject.transform.position = Cell(nodeCells[i]);
                var renderer = nodeObject.GetComponent<SpriteRenderer>();
                renderer.sprite = _accentTile != null ? _accentTile.sprite : null;
                renderer.sortingOrder = 6; nodeObject.transform.localScale = new Vector3(.72f,.72f,1f);
                nodeObject.GetComponent<BoxCollider2D>().size = new Vector2(.8f,.8f);
                nodes[i] = nodeObject.GetComponent<HiddenRegionPuzzleNode>();
                var nodeSo = new SerializedObject(nodes[i]);
                Set(nodeSo, "controller", puzzle); Set(nodeSo, "nodeIndex", i); Set(nodeSo, "target", renderer);
                Set(nodeSo, "inactiveColor", Color.Lerp(accent, Color.black, .65f));
                Set(nodeSo, "activeColor", Color.Lerp(accent, Color.white, .22f));
                nodeSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var rewardRoot = new GameObject("UnlockedMemoryStage");
            rewardRoot.transform.SetParent(parent, false);
            CreateNpc(rewardRoot.transform, "MemoryNpc", npcCell, LoadShadow(npcShadowId), npcId,
                npcFirst, npcRepeat, "talked_" + npcId, accent);
            CreateEncounter(rewardRoot.transform, "RareFixedEncounter", encounterCell, LoadShadow(rareShadowId),
                minLevel, maxLevel, encounterId, battleGimmick);
            CreateHiddenReward(rewardRoot.transform, region.regionId, rewardCell, LoadShadow(npcShadowId),
                rewardDialogue, encounterId, rewardItemId, rewardItemCount, rewardGold, rewardMessage, accent);
            rewardRoot.SetActive(false);

            var puzzleSo = new SerializedObject(puzzle);
            Set(puzzleSo, "puzzleId", region.regionId); Set(puzzleSo, "rewardRoot", rewardRoot);
            Set(puzzleSo, "completionDialogueId", completeDialogue); Set(puzzleSo, "accent", accent);
            SetIntArray(puzzleSo.FindProperty("sequence"), sequence);
            SerializedProperty nodeProperty = puzzleSo.FindProperty("nodes");
            nodeProperty.arraySize = nodes.Length;
            for (int i = 0; i < nodes.Length; i++)
                nodeProperty.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
            puzzleSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateHiddenReward(Transform parent, string rewardId, Vector2Int cell,
            ShadowData visual, string dialogueId, string encounterId, string itemId, int itemCount, int gold,
            string message, Color accent)
        {
            var go = CreateFieldActor(parent, "HiddenReward", cell, visual, false);
            var reward = go.AddComponent<HiddenRegionReward>();
            var so = new SerializedObject(reward);
            Set(so, "rewardId", rewardId); Set(so, "dialogueId", dialogueId);
            Set(so, "requiredEncounterId", encounterId);
            Set(so, "rewardItemId", itemId); Set(so, "rewardItemCount", itemCount);
            Set(so, "rewardGold", gold); Set(so, "rewardMessage", message); Set(so, "accent", accent);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateFieldActor(Transform parent, string name, Vector2Int cell,
                                                   ShadowData visual, bool trigger)
        {
            var go = new GameObject(name, typeof(BoxCollider2D));
            go.transform.SetParent(parent); go.transform.position = Cell(cell);
            var visualObject = new GameObject("Visual", typeof(SpriteRenderer), typeof(ShadowFieldMotion));
            visualObject.transform.SetParent(go.transform, false);
            var renderer = visualObject.GetComponent<SpriteRenderer>();
            var frames = CreatePixelFieldActorFrames(visual);
            renderer.sprite = frames.Length > 0 ? frames[0] : null;
            if (renderer.sprite == null) renderer.sprite = visual?.silhouetteSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 8;
            FieldSpriteSizing.Fit(renderer);
            go.AddComponent<GroundContactShadow>().Configure(renderer);
            var box = go.GetComponent<BoxCollider2D>(); box.size = new Vector2(0.72f, 0.82f); box.isTrigger = trigger;
            visualObject.GetComponent<ShadowFieldMotion>().Configure(visual, frames);
            return go;
        }

        private static void CreateEncounter(Transform parent, string name, Vector2Int cell,
                                            ShadowData shadow, int minLevel, int maxLevel,
                                            string encounterId = "", BattleGimmick battleGimmick = BattleGimmick.None)
        {
            var go = new GameObject(name, typeof(CircleCollider2D), typeof(EncounterSymbol));
            go.transform.SetParent(parent); go.transform.position = Cell(cell);
            var visualObject = new GameObject("Visual", typeof(SpriteRenderer), typeof(ShadowFieldMotion));
            visualObject.transform.SetParent(go.transform, false);
            var renderer = visualObject.GetComponent<SpriteRenderer>();
            var frames = CreatePixelFieldActorFrames(shadow);
            renderer.sprite = frames.Length > 0 ? frames[0] : CreatePixelEncounterSprite();
            renderer.color = Color.white;
            renderer.sortingOrder = 7;
            FieldSpriteSizing.Fit(renderer);
            go.AddComponent<GroundContactShadow>().Configure(renderer);
            go.GetComponent<CircleCollider2D>().radius = 0.38f;
            var so = new SerializedObject(go.GetComponent<EncounterSymbol>());
            Set(so, "leadShadow", shadow); Set(so, "levelRange", new Vector2Int(minLevel, maxLevel));
            if (!string.IsNullOrEmpty(encounterId)) Set(so, "encounterId", encounterId);
            if (battleGimmick != BattleGimmick.None)
            {
                Set(so, "mode", (int)BattleMode.Boss);
                Set(so, "battleGimmick", (int)battleGimmick);
            }
            Set(so, "silhouette", renderer); so.ApplyModifiedPropertiesWithoutUndo();
            visualObject.GetComponent<ShadowFieldMotion>().Configure(shadow, frames);
        }

        private static void CreateRegionalExpansionEncounters(Transform parent, string mapId,
            Tilemap collision, Vector2Int startCell)
        {
            if (!File.Exists(RegionalRosterManifestPath)) return;
            var manifest = JsonUtility.FromJson<RegionalRosterManifest>(File.ReadAllText(RegionalRosterManifestPath));
            if (manifest?.entries == null) return;

            var occupied = new HashSet<Vector2Int> { startCell };
            int regionIndex = 0;
            foreach (RegionalRosterEntry entry in manifest.entries)
            {
                if (entry.sceneName != mapId) continue;
                ShadowData shadow = LoadShadow(entry.shadowId);
                if (shadow == null)
                {
                    Debug.LogWarning($"[RegionalRoster] 그림자 데이터를 찾지 못했습니다: {entry.shadowId}");
                    continue;
                }

                Vector2Int cell = FindRegionalEncounterCell(collision, startCell, occupied,
                    StableHash(entry.shadowId) + regionIndex * 17);
                occupied.Add(cell);
                CreateEncounter(parent, $"Regional_{entry.shadowId}", cell, shadow,
                    Mathf.Max(1, entry.minLevel), Mathf.Max(entry.minLevel, entry.maxLevel));
                regionIndex++;
            }
        }

        private static Vector2Int FindRegionalEncounterCell(Tilemap collision, Vector2Int startCell,
            HashSet<Vector2Int> occupied, int seed)
        {
            var candidates = new List<Vector2Int>();
            for (int y = -7; y <= 7; y++)
            for (int x = -9; x <= 9; x++)
            {
                var cell = new Vector2Int(x, y);
                if (Mathf.Abs(x) < 3 && Mathf.Abs(y) < 3) continue;
                if ((cell - startCell).sqrMagnitude < 9 || occupied.Contains(cell)) continue;
                if (collision != null && collision.HasTile(new Vector3Int(x, y, 0))) continue;
                candidates.Add(cell);
            }
            if (candidates.Count == 0) return Vector2Int.zero;
            int index = (seed & int.MaxValue) % candidates.Count;
            return candidates[index];
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
            Set(so, "levelRange", new Vector2Int(10, 12)); Set(so, "silhouette", go.GetComponentInChildren<SpriteRenderer>());
            Set(so, "purificationReward", boss); Set(so, "purificationRewardLevel", 12);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateBossEncounter(Transform parent, string objectName, Vector2Int cell,
            string shadowId, string encounterId, Vector2Int levels, string preDialogue, string postDialogue,
            string victoryFlag, string rewardFlag, int rewardLevel, Color accent)
        {
            var boss = LoadShadow(shadowId);
            var go = CreateFieldActor(parent, objectName, cell, boss, false);
            go.transform.localScale = Vector3.one * 1.5f;
            var trigger = go.AddComponent<BossEncounterTrigger>();
            var so = new SerializedObject(trigger);
            Set(so, "encounterId", encounterId);
            var party = so.FindProperty("enemyParty"); party.arraySize = 1;
            party.GetArrayElementAtIndex(0).objectReferenceValue = boss;
            Set(so, "levelRange", levels); Set(so, "preBattleDialogueId", preDialogue);
            Set(so, "victoryDialogueId", postDialogue); Set(so, "victoryFlag", victoryFlag);
            Set(so, "accent", accent); Set(so, "purificationReward", boss);
            Set(so, "purificationRewardLevel", rewardLevel); Set(so, "purificationRewardFlag", rewardFlag);
            Set(so, "silhouette", go.GetComponentInChildren<SpriteRenderer>());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateEndingTrigger(Transform parent, Vector2Int cell)
        {
            var go = CreateFieldActor(parent, "FinalScriptBook", cell, LoadShadow("outside_script_shadow"), false);
            go.transform.localScale = Vector3.one * 1.2f;
            go.AddComponent<EndingTrigger>();
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
                                         string requiredFlag = null,
                                         TraversalAbility requiredTraversalAbilities = TraversalAbility.None)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(MapPortal));
            go.transform.SetParent(parent); go.transform.position = Cell(cell);
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = _accentTile.sprite;
            renderer.color = new Color(0.55f, 0.38f, 0.92f, 0.75f); renderer.sortingOrder = 5;
            ApplyLit(renderer);
            go.transform.localScale = new Vector3(0.7f, 0.9f, 1f);
            var box = go.GetComponent<BoxCollider2D>(); box.isTrigger = true; box.size = Vector2.one;
            var so = new SerializedObject(go.GetComponent<MapPortal>());
            Set(so, "targetScene", target); Set(so, "arrivalCell", arrival); Set(so, "arrivalFacing", (int)facing);
            Set(so, "activateOnTouch", true); Set(so, "setCheckpoint", checkpoint);
            if (!string.IsNullOrEmpty(requiredFlag)) Set(so, "requiredFlag", requiredFlag);
            Set(so, "requiredTraversalAbilities", (int)requiredTraversalAbilities);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateTraversalRoutePair(Transform parent, string name, Vector2Int first,
            Vector2Int second, TraversalAbility abilities, Color color)
        {
            CreateTraversalRoute(parent, name + "_A", first, second, FieldGrid.VectorToDir(second - first),
                abilities, color);
            CreateTraversalRoute(parent, name + "_B", second, first, FieldGrid.VectorToDir(first - second),
                abilities, color);
        }

        private static void CreateTraversalRoute(Transform parent, string name, Vector2Int cell,
            Vector2Int destination, FacingDir facing, TraversalAbility abilities, Color color)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(TraversalRoute));
            go.transform.SetParent(parent); go.transform.position = Cell(cell);
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = _accentTile.sprite;
            renderer.color = color; renderer.sortingOrder = 5; ApplyLit(renderer);
            go.transform.localScale = new Vector3(.72f,.72f,1f);
            var box = go.GetComponent<BoxCollider2D>(); box.isTrigger = true; box.size = Vector2.one;
            var so = new SerializedObject(go.GetComponent<TraversalRoute>());
            Set(so, "allowedAbilities", (int)abilities); Set(so, "destinationCell", destination);
            Set(so, "destinationFacing", (int)facing); so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateFieldUi(Transform parent)
        {
            var root = new GameObject("FieldUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1f;
            var safe = UiRect("SafeArea", root.transform, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<MobileSafeArea>();
            var pad = UiRect("DirectionPad", safe, Vector2.zero, Vector2.zero);
            MobileUiPolish.Fixed(pad, Vector2.zero, new Vector2(28, 28), new Vector2(264, 264));
            DirectionButton("Up", pad, new Vector2(.34f,.67f), new Vector2(.66f,1f), "▲", Vector2.up);
            DirectionButton("Left", pad, new Vector2(0,.34f), new Vector2(.33f,.66f), "◀", Vector2.left);
            DirectionButton("Right", pad, new Vector2(.67f,.34f), new Vector2(1f,.66f), "▶", Vector2.right);
            DirectionButton("Down", pad, new Vector2(.34f,0), new Vector2(.66f,.33f), "▼", Vector2.down);
            var action = UiButton("Action", safe, Vector2.zero, Vector2.one, "대화 · 조사");
            MobileUiPolish.Fixed(action.transform as RectTransform, new Vector2(1, 0), new Vector2(-36, 48), new Vector2(164, 100));
            action.GetComponentInChildren<Text>().fontSize = 26;
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
            image.sprite = Resources.Load<Sprite>("Art/UI/ControlPlate"); image.type = Image.Type.Sliced;
            image.color = Color.white; var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var text = UiText("Label", rect, Vector2.zero, Vector2.one, 34, TextAnchor.MiddleCenter); text.text = label;
            return button;
        }

        private static void CreateRegionLabel(Transform parent, string displayName)
        {
            var root = new GameObject("RegionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false); var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 15;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = 1f;
            var safe = UiRect("SafeArea", root.transform, Vector2.zero, Vector2.one); safe.gameObject.AddComponent<MobileSafeArea>();
            var text = UiText("RegionName", safe, Vector2.zero, Vector2.one, 26, TextAnchor.MiddleLeft);
            MobileUiPolish.Fixed(text.rectTransform, new Vector2(0, 1), new Vector2(24, -92), new Vector2(460, 42));
            text.text = displayName; text.color = new Color(.85f,.78f,1f,.82f);
        }

        private static void CreateFader()
        {
            var go = new GameObject("FaderCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(Image), typeof(ScreenFader));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var overlay = go.GetComponent<Image>(); overlay.color = Color.black;
            var silhouette = UiRect("EncounterSilhouette", go.transform, new Vector2(.22f, .22f),
                new Vector2(.78f, .78f)).gameObject.AddComponent<Image>();
            silhouette.color = Color.clear;
            silhouette.preserveAspect = true;
            silhouette.raycastTarget = false;
            silhouette.gameObject.SetActive(false);
            var faderSo = new SerializedObject(go.GetComponent<ScreenFader>());
            Set(faderSo, "overlay", overlay);
            Set(faderSo, "encounterSilhouette", silhouette);
            faderSo.ApplyModifiedPropertiesWithoutUndo();
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

        private static void CreateEnvironment(Transform parent, Theme theme)
        {
            EnsureEnvironmentAssets();
            EnvironmentProfile profile = ProfileFor(theme);
            var root = new GameObject("Environment");
            root.transform.SetParent(parent, false);

            var globalObject = new GameObject("GlobalMoonlight", typeof(Light2D));
            globalObject.transform.SetParent(root.transform, false);
            var global = globalObject.GetComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.color = profile.globalColor;
            global.intensity = profile.globalIntensity;

            for (int i = 0; i < profile.lightPositions.Length; i++)
            {
                var lightObject = new GameObject($"LocalLight_{i + 1:00}", typeof(Light2D));
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localPosition = (Vector3)profile.lightPositions[i];
                var light = lightObject.GetComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = i % 2 == 0 ? profile.pointColor : profile.secondaryLightColor;
                light.intensity = profile.pointIntensity * (i % 3 == 0 ? 1.15f : 1f);
                light.pointLightInnerRadius = 0.65f;
                light.pointLightOuterRadius = 3.4f + i % 2 * 0.8f;
                light.falloffIntensity = 0.7f;
            }

            var fogRoot = new GameObject("FogLayers");
            fogRoot.transform.SetParent(root.transform, false);
            var fogs = new Transform[6];
            for (int i = 0; i < fogs.Length; i++)
            {
                var fogObject = new GameObject($"Fog_{i + 1:00}", typeof(SpriteRenderer));
                fogObject.transform.SetParent(fogRoot.transform, false);
                fogObject.transform.localPosition = new Vector3(-10f + i * 4.2f, -6.5f + i % 3 * 5.5f, 0f);
                fogObject.transform.localScale = new Vector3(2.6f + i % 2 * 0.7f, 1.25f + i % 3 * 0.18f, 1f);
                var renderer = fogObject.GetComponent<SpriteRenderer>();
                renderer.sprite = _fogSprite;
                renderer.color = WithAlpha(profile.fogColor, profile.fogAlpha * (0.75f + i % 3 * 0.12f));
                renderer.sortingOrder = i % 3 == 0 ? 11 : 3;
                fogs[i] = fogObject.transform;
            }

            var moteRoot = new GameObject("Motes");
            moteRoot.transform.SetParent(root.transform, false);
            var motes = new Transform[24];
            for (int i = 0; i < motes.Length; i++)
            {
                var moteObject = new GameObject($"Mote_{i + 1:00}", typeof(SpriteRenderer));
                moteObject.transform.SetParent(moteRoot.transform, false);
                float x = -10f + ((i * 37) % 211) / 10f;
                float y = -8f + ((i * 53) % 161) / 10f;
                moteObject.transform.localPosition = new Vector3(x, y, 0f);
                float size = 0.18f + (i % 5) * 0.065f;
                moteObject.transform.localScale = Vector3.one * size;
                var renderer = moteObject.GetComponent<SpriteRenderer>();
                renderer.sprite = _moteSprite;
                renderer.color = WithAlpha(i % 4 == 0 ? profile.secondaryLightColor : profile.moteColor,
                    0.38f + i % 4 * 0.1f);
                renderer.sortingOrder = i % 5 == 0 ? 12 : 4;
                motes[i] = moteObject.transform;
            }

            var atmosphere = root.AddComponent<FieldAtmosphereController>();
            var so = new SerializedObject(atmosphere);
            SetArray(so.FindProperty("fogLayers"), fogs);
            SetArray(so.FindProperty("motes"), motes);
            Set(so, "fogDrift", profile.fogDrift); Set(so, "moteDrift", profile.moteDrift);
            Set(so, "boundsMin", new Vector2(-13f, -11f)); Set(so, "boundsMax", new Vector2(13f, 11f));
            Set(so, "fogPulse", profile.fogPulse); Set(so, "motePulse", profile.motePulse);
            Set(so, "pulseSpeed", profile.pulseSpeed);
            so.ApplyModifiedPropertiesWithoutUndo();

            var ambience = root.AddComponent<FieldAmbientAudio>();
            var audioSo = new SerializedObject(ambience);
            var ambienceStyle = (FieldAmbienceStyle)(int)theme;
            Set(audioSo, "style", (int)ambienceStyle);
            Set(audioSo, "ambienceClip", FieldAudioAssetGenerator.LoadBed(ambienceStyle));
            Set(audioSo, "detailClip", FieldAudioAssetGenerator.LoadDetail(ambienceStyle));
            Set(audioSo, "ambienceVolume", profile.ambienceVolume);
            Set(audioSo, "detailVolume", profile.detailVolume);
            Set(audioSo, "fadeDuration", .4f);
            Set(audioSo, "detailInterval", profile.detailInterval);
            audioSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyPixelFieldPass(Transform fieldRoot, Theme theme)
        {
            Transform grid = fieldRoot.Find("Grid");
            if (grid != null)
            {
                var groundRenderer = grid.Find("Ground")?.GetComponent<TilemapRenderer>();
                var collisionRenderer = grid.Find("Collision")?.GetComponent<TilemapRenderer>();
                if (groundRenderer != null)
                {
                    groundRenderer.enabled = true;
                    groundRenderer.mode = TilemapRenderer.Mode.Chunk;
                }
                if (collisionRenderer != null)
                {
                    collisionRenderer.enabled = true;
                    collisionRenderer.mode = TilemapRenderer.Mode.Individual;
                }
            }

            var camera = fieldRoot.GetComponentInChildren<Camera>();
            if (camera != null)
            {
                if (theme == Theme.Meadow) camera.backgroundColor = new Color(.025f, .035f, .085f, 1f);
                camera.orthographicSize = 5.5f;
                var follow = camera.GetComponent<FieldCameraFollow>();
                if (follow != null)
                {
                    var followSo = new SerializedObject(follow);
                    Set(followSo, "pixelSnap", true);
                    Set(followSo, "pixelsPerUnit", 32f);
                    followSo.ApplyModifiedPropertiesWithoutUndo();
                }
                var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData != null) cameraData.renderPostProcessing = false;
            }

            // Pixel tiles must stay crisp. Fog quads and post-processing blur their edges,
            // so every playable field now uses the same handheld-style rendering rules.
            Transform environment = fieldRoot.Find("Environment");
            environment?.Find("FogLayers")?.gameObject.SetActive(false);
            environment?.Find("Motes")?.gameObject.SetActive(false);

            string profilePath = $"Assets/Data/Generated/Materials/{theme}Volume.asset";
            if (AssetDatabase.LoadMainAssetAtPath(profilePath) != null) AssetDatabase.DeleteAsset(profilePath);
        }

        private static EnvironmentProfile ProfileFor(Theme theme)
        {
            switch (theme)
            {
                case Theme.Theater:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.48f,.34f,.67f), globalIntensity = .48f,
                        pointColor = new Color(1f,.58f,.27f), secondaryLightColor = new Color(.72f,.36f,1f),
                        pointIntensity = 1.15f, fogColor = new Color(.37f,.16f,.54f), fogAlpha = .18f,
                        moteColor = new Color(1f,.72f,.35f), fogDrift = new Vector2(.10f,.008f),
                        moteDrift = new Vector2(.025f,.07f), fogPulse = .10f, motePulse = .35f, pulseSpeed = .68f,
                        ambienceVolume = .26f, detailVolume = .22f, detailInterval = new Vector2(5f,9f),
                        lightPositions = new[] { new Vector2(-5.5f,3.5f), new Vector2(5.5f,3.5f), new Vector2(0f,-1f) }
                    };
                case Theme.Village:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.40f,.57f,.73f), globalIntensity = .58f,
                        pointColor = new Color(.35f,.92f,1f), secondaryLightColor = new Color(1f,.66f,.32f),
                        pointIntensity = .95f, fogColor = new Color(.20f,.47f,.58f), fogAlpha = .14f,
                        moteColor = new Color(.48f,1f,.92f), fogDrift = new Vector2(.075f,.012f),
                        moteDrift = new Vector2(.02f,.055f), fogPulse = .08f, motePulse = .28f, pulseSpeed = .58f,
                        ambienceVolume = .23f, detailVolume = .18f, detailInterval = new Vector2(6f,11f),
                        lightPositions = new[] { new Vector2(-5f,4f), new Vector2(4.5f,4f), new Vector2(-3f,-4f), new Vector2(6f,-3f) }
                    };
                case Theme.Meadow:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.42f,.62f,.82f), globalIntensity = .62f,
                        pointColor = new Color(.56f,.64f,1f), secondaryLightColor = new Color(1f,.71f,.34f),
                        pointIntensity = 1.05f, fogColor = new Color(.27f,.60f,.64f), fogAlpha = .16f,
                        moteColor = new Color(.71f,.75f,1f), fogDrift = new Vector2(.14f,.018f),
                        moteDrift = new Vector2(.035f,.09f), fogPulse = .13f, motePulse = .42f, pulseSpeed = .82f,
                        ambienceVolume = .28f, detailVolume = .2f, detailInterval = new Vector2(4f,8f),
                        lightPositions = new[] { new Vector2(-6f,5f), new Vector2(1f,5f), new Vector2(6f,2f), new Vector2(-3f,-4f), new Vector2(4f,-5f) }
                    };
                case Theme.AshWastes:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.56f,.29f,.22f), globalIntensity = .46f,
                        pointColor = new Color(1f,.31f,.14f), secondaryLightColor = new Color(.83f,.58f,.36f),
                        pointIntensity = 1.05f, fogColor = new Color(.38f,.16f,.12f), fogAlpha = .20f,
                        moteColor = new Color(.95f,.39f,.18f), fogDrift = new Vector2(.19f,.014f),
                        moteDrift = new Vector2(-.02f,.11f), fogPulse = .12f, motePulse = .44f, pulseSpeed = .9f,
                        ambienceVolume = .29f, detailVolume = .24f, detailInterval = new Vector2(4f,7f),
                        lightPositions = new[] { new Vector2(-6f,4f), new Vector2(5f,4f), new Vector2(-3f,-4f), new Vector2(5f,-5f) }
                    };
                case Theme.EmberCity:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.62f,.34f,.20f), globalIntensity = .50f,
                        pointColor = new Color(1f,.56f,.22f), secondaryLightColor = new Color(.78f,.20f,.14f),
                        pointIntensity = 1.2f, fogColor = new Color(.43f,.20f,.12f), fogAlpha = .16f,
                        moteColor = new Color(1f,.69f,.30f), fogDrift = new Vector2(.09f,.01f),
                        moteDrift = new Vector2(.01f,.09f), fogPulse = .09f, motePulse = .38f, pulseSpeed = .75f,
                        ambienceVolume = .26f, detailVolume = .22f, detailInterval = new Vector2(5f,9f),
                        lightPositions = new[] { new Vector2(-6f,5f), new Vector2(0f,5f), new Vector2(6f,5f), new Vector2(-4f,-4f), new Vector2(4f,-4f) }
                    };
                case Theme.Catacombs:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.30f,.23f,.38f), globalIntensity = .34f,
                        pointColor = new Color(.30f,.84f,.88f), secondaryLightColor = new Color(.76f,.42f,.72f),
                        pointIntensity = .9f, fogColor = new Color(.19f,.13f,.25f), fogAlpha = .24f,
                        moteColor = new Color(.42f,.94f,.91f), fogDrift = new Vector2(.045f,.006f),
                        moteDrift = new Vector2(0f,.045f), fogPulse = .16f, motePulse = .48f, pulseSpeed = .54f,
                        ambienceVolume = .31f, detailVolume = .20f, detailInterval = new Vector2(6f,10f),
                        lightPositions = new[] { new Vector2(-5f,4f), new Vector2(5f,4f), new Vector2(-5f,-4f), new Vector2(5f,-4f), new Vector2(0f,1f) }
                    };
                case Theme.FrostPort:
                case Theme.Archive:
                case Theme.ForbiddenStacks:
                case Theme.MirrorVault:
                case Theme.BlueAbyss:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.33f,.52f,.78f), globalIntensity = .48f,
                        pointColor = new Color(.38f,.82f,1f), secondaryLightColor = new Color(.62f,.48f,1f),
                        pointIntensity = 1.05f, fogColor = new Color(.22f,.45f,.72f), fogAlpha = .16f,
                        moteColor = new Color(.64f,.90f,1f), fogDrift = new Vector2(.07f,.012f),
                        moteDrift = new Vector2(.015f,.065f), fogPulse = .11f, motePulse = .38f, pulseSpeed = .62f,
                        ambienceVolume = .27f, detailVolume = .21f, detailInterval = new Vector2(4.5f,8f),
                        lightPositions = new[] { new Vector2(-6f,4f), new Vector2(0f,5f), new Vector2(6f,4f), new Vector2(-4f,-4f), new Vector2(4f,-4f) }
                    };
                case Theme.VioletMarsh:
                case Theme.HowlVillage:
                case Theme.MoonfangForest:
                case Theme.BloodmoonRidge:
                case Theme.BeastDen:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.48f,.27f,.62f), globalIntensity = .46f,
                        pointColor = new Color(.72f,.34f,1f), secondaryLightColor = new Color(1f,.28f,.52f),
                        pointIntensity = 1.08f, fogColor = new Color(.38f,.12f,.49f), fogAlpha = .17f,
                        moteColor = new Color(.83f,.48f,1f), fogDrift = new Vector2(.11f,.01f),
                        moteDrift = new Vector2(.02f,.08f), fogPulse = .14f, motePulse = .44f, pulseSpeed = .78f,
                        ambienceVolume = .29f, detailVolume = .23f, detailInterval = new Vector2(4f,7.5f),
                        lightPositions = new[] { new Vector2(-6f,4f), new Vector2(0f,5f), new Vector2(6f,4f), new Vector2(-4f,-4f), new Vector2(4f,-4f) }
                    };
                case Theme.ThreadMarket:
                case Theme.ClockworkAlley:
                case Theme.MarionetteOpera:
                case Theme.SeveredWorkshop:
                case Theme.PuppeteerStage:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.58f,.27f,.43f), globalIntensity = .48f,
                        pointColor = new Color(1f,.38f,.63f), secondaryLightColor = new Color(1f,.65f,.32f),
                        pointIntensity = 1.12f, fogColor = new Color(.45f,.12f,.31f), fogAlpha = .17f,
                        moteColor = new Color(1f,.55f,.76f), fogDrift = new Vector2(.08f,.01f),
                        moteDrift = new Vector2(.01f,.075f), fogPulse = .12f, motePulse = .42f, pulseSpeed = .72f,
                        ambienceVolume = .28f, detailVolume = .23f, detailInterval = new Vector2(4f,8f),
                        lightPositions = new[] { new Vector2(-6f,4f), new Vector2(0f,5f), new Vector2(6f,4f), new Vector2(-4f,-4f), new Vector2(4f,-4f) }
                    };
                case Theme.Censor:
                case Theme.BlackArchive:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.32f,.28f,.37f), globalIntensity = .38f,
                        pointColor = new Color(.62f,.55f,.70f), secondaryLightColor = new Color(.42f,.22f,.43f),
                        pointIntensity = .92f, fogColor = new Color(.18f,.13f,.21f), fogAlpha = .2f,
                        moteColor = new Color(.66f,.60f,.72f), fogDrift = new Vector2(.045f,.006f),
                        moteDrift = new Vector2(0f,.04f), fogPulse = .1f, motePulse = .32f, pulseSpeed = .5f,
                        ambienceVolume = .31f, detailVolume = .22f, detailInterval = new Vector2(5f,9f),
                        lightPositions = new[] { new Vector2(-6f,4f), new Vector2(0f,5f), new Vector2(6f,4f), new Vector2(-4f,-4f), new Vector2(4f,-4f) }
                    };
                case Theme.MemorySea:
                case Theme.MoonPalace:
                    return new EnvironmentProfile { globalColor=new Color(.30f,.55f,.76f),globalIntensity=.48f,pointColor=new Color(.42f,.84f,1f),secondaryLightColor=new Color(.72f,.58f,1f),pointIntensity=1.05f,fogColor=new Color(.18f,.42f,.62f),fogAlpha=.16f,moteColor=new Color(.7f,.9f,1f),fogDrift=new Vector2(.08f,.01f),moteDrift=new Vector2(.02f,.07f),fogPulse=.12f,motePulse=.4f,pulseSpeed=.65f,ambienceVolume=.29f,detailVolume=.22f,detailInterval=new Vector2(4f,8f),lightPositions=new[]{new Vector2(-6f,4f),new Vector2(0f,5f),new Vector2(6f,4f),new Vector2(-4f,-4f),new Vector2(4f,-4f)} };
                case Theme.FinalTheater:
                    return new EnvironmentProfile { globalColor=new Color(.42f,.24f,.64f),globalIntensity=.45f,pointColor=new Color(.78f,.42f,1f),secondaryLightColor=new Color(1f,.62f,.32f),pointIntensity=1.12f,fogColor=new Color(.25f,.10f,.40f),fogAlpha=.18f,moteColor=new Color(.88f,.64f,1f),fogDrift=new Vector2(.08f,.01f),moteDrift=new Vector2(.02f,.07f),fogPulse=.13f,motePulse=.42f,pulseSpeed=.7f,ambienceVolume=.29f,detailVolume=.23f,detailInterval=new Vector2(4f,7f),lightPositions=new[]{new Vector2(-6f,4f),new Vector2(0f,5f),new Vector2(6f,4f),new Vector2(0f,-3f)} };
                case Theme.CosmicStage:
                    return new EnvironmentProfile { globalColor=new Color(.28f,.32f,.70f),globalIntensity=.42f,pointColor=new Color(.48f,.62f,1f),secondaryLightColor=new Color(.92f,.72f,1f),pointIntensity=1.18f,fogColor=new Color(.12f,.16f,.48f),fogAlpha=.2f,moteColor=new Color(.72f,.80f,1f),fogDrift=new Vector2(.04f,.008f),moteDrift=new Vector2(.01f,.09f),fogPulse=.16f,motePulse=.5f,pulseSpeed=.55f,ambienceVolume=.31f,detailVolume=.24f,detailInterval=new Vector2(4f,8f),lightPositions=new[]{new Vector2(-6f,5f),new Vector2(0f,5f),new Vector2(6f,5f),new Vector2(-3f,-4f),new Vector2(3f,-4f)} };
                case Theme.AshThrone:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.46f,.12f,.10f), globalIntensity = .38f,
                        pointColor = new Color(1f,.17f,.08f), secondaryLightColor = new Color(1f,.66f,.20f),
                        pointIntensity = 1.45f, fogColor = new Color(.45f,.07f,.06f), fogAlpha = .27f,
                        moteColor = new Color(1f,.40f,.12f), fogDrift = new Vector2(.16f,-.01f),
                        moteDrift = new Vector2(-.025f,.14f), fogPulse = .2f, motePulse = .58f, pulseSpeed = 1.12f,
                        ambienceVolume = .36f, detailVolume = .31f, detailInterval = new Vector2(3f,6f),
                        lightPositions = new[] { new Vector2(-6f,2f), new Vector2(6f,2f), new Vector2(-3f,6f), new Vector2(3f,6f), new Vector2(0f,0f) }
                    };
                default:
                    return new EnvironmentProfile
                    {
                        globalColor = new Color(.38f,.28f,.67f), globalIntensity = .38f,
                        pointColor = new Color(.82f,.40f,1f), secondaryLightColor = new Color(1f,.28f,.55f),
                        pointIntensity = 1.35f, fogColor = new Color(.42f,.15f,.59f), fogAlpha = .24f,
                        moteColor = new Color(.88f,.77f,1f), fogDrift = new Vector2(.18f,-.012f),
                        moteDrift = new Vector2(-.03f,.12f), fogPulse = .18f, motePulse = .52f, pulseSpeed = 1.05f,
                        ambienceVolume = .34f, detailVolume = .3f, detailInterval = new Vector2(3.5f,6.5f),
                        lightPositions = new[] { new Vector2(-6f,2f), new Vector2(6f,2f), new Vector2(-3f,6f), new Vector2(3f,6f), new Vector2(0f,1f) }
                    };
            }
        }

        private static void EnsureEnvironmentAssets()
        {
            if (_litMaterial == null)
            {
                const string materialPath = "Assets/Data/Generated/Materials/FieldSpriteLit.mat";
                _litMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (_litMaterial == null && shader != null)
                {
                    _litMaterial = new Material(shader) { name = "FieldSpriteLit" };
                    AssetDatabase.CreateAsset(_litMaterial, materialPath);
                }
                else if (_litMaterial != null && shader != null) _litMaterial.shader = shader;
            }
            if (_fogSprite == null) _fogSprite = CreateAtmosphereSprite("SoftFog", 128, 64, false);
            if (_moteSprite == null) _moteSprite = CreateAtmosphereSprite("LightMote", 32, 32, true);
        }

        private static void EnsureUrp2DRenderer()
        {
            RenderPipelineAsset configured = QualitySettings.renderPipeline != null
                ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
            if (configured != null) return;

            const string rendererPath = "Assets/Data/Generated/Materials/ShadowTheater2DRenderer.asset";
            const string pipelinePath = "Assets/Data/Generated/Materials/ShadowTheaterURP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                renderer.name = "ShadowTheater2DRenderer";
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                MethodInfo factory = typeof(UniversalRenderPipelineAsset).GetMethod("Create",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Renderer2DData) }, null);
                pipeline = factory?.Invoke(null, new object[] { renderer }) as UniversalRenderPipelineAsset;
                if (pipeline == null)
                {
                    pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                    var serialized = new SerializedObject(pipeline);
                    SerializedProperty list = serialized.FindProperty("m_RendererDataList");
                    if (list == null)
                    {
                        Object.DestroyImmediate(pipeline);
                        Debug.LogError("[Prologue] URP 2D Renderer 연결 필드를 찾지 못했습니다. Project Settings에서 2D Renderer를 직접 지정하세요.");
                        return;
                    }
                    list.arraySize = 1;
                    list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                    SerializedProperty defaultIndex = serialized.FindProperty("m_DefaultRendererIndex");
                    if (defaultIndex != null) defaultIndex.intValue = 0;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                pipeline.name = "ShadowTheaterURP";
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            Debug.Log("[Prologue] URP 2D Renderer 자산을 생성하고 프로젝트에 연결했습니다.");
        }

        private static Sprite CreateAtmosphereSprite(string name, int width, int height, bool circular)
        {
            string path = $"Assets/Art/Generated/Atmosphere/{name}.png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float nx = (x + .5f) / width * 2f - 1f;
                float ny = (y + .5f) / height * 2f - 1f;
                float distance = circular ? Mathf.Sqrt(nx * nx + ny * ny) : Mathf.Sqrt(nx * nx * .55f + ny * ny);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * (circular ? 1f : .72f + .12f * Mathf.Sin((x * 7 + y * 13) * .08f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ApplyLit(SpriteRenderer renderer)
        {
            EnsureEnvironmentAssets();
            if (renderer != null && _litMaterial != null) renderer.sharedMaterial = _litMaterial;
        }

        private static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }

        private static void SetArray(SerializedProperty property, Transform[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void SetSpriteArray(SerializedProperty property, Sprite[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void CreateThemeTiles(Theme theme)
        {
            EnsureEnvironmentAssets();
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
                    _meadowGrassTile = CreatePixelMeadowTile("MeadowPixel_Grass", PixelTileKind.Grass);
                    _meadowPathTile = CreatePixelMeadowTile("MeadowPixel_Path", PixelTileKind.Path);
                    _meadowWaterTile = CreatePixelMeadowTile("MeadowPixel_Water", PixelTileKind.Water);
                    _meadowCliffTile = CreatePixelMeadowTile("MeadowPixel_Cliff", PixelTileKind.Cliff);
                    _meadowBushTile = CreatePixelMeadowTile("MeadowPixel_Bush", PixelTileKind.Bush);
                    _groundTile = _meadowGrassTile; _wallTile = _meadowCliffTile; _accentTile = _meadowBushTile; break;
                case Theme.AshWastes:
                    _groundTile = CreateTile("AshWastes_Ground", new Color(.31f,.17f,.13f), new Color(.45f,.23f,.16f));
                    _wallTile = CreateTile("AshWastes_Wall", new Color(.11f,.045f,.035f), new Color(.28f,.10f,.07f));
                    _accentTile = CreateTile("AshWastes_Accent", new Color(.25f,.10f,.07f), new Color(.82f,.31f,.16f)); break;
                case Theme.EmberCity:
                    _groundTile = CreateTile("EmberCity_Ground", new Color(.34f,.21f,.16f), new Color(.50f,.31f,.20f));
                    _wallTile = CreateTile("EmberCity_Wall", new Color(.12f,.06f,.045f), new Color(.34f,.14f,.09f));
                    _accentTile = CreateTile("EmberCity_Accent", new Color(.30f,.13f,.08f), new Color(.95f,.50f,.20f)); break;
                case Theme.Catacombs:
                    _groundTile = CreateTile("Catacombs_Ground", new Color(.19f,.16f,.23f), new Color(.29f,.23f,.34f));
                    _wallTile = CreateTile("Catacombs_Wall", new Color(.045f,.03f,.065f), new Color(.14f,.08f,.18f));
                    _accentTile = CreateTile("Catacombs_Accent", new Color(.11f,.09f,.16f), new Color(.32f,.72f,.69f)); break;
                case Theme.FrostPort:
                    _groundTile = CreateTile("FrostPort_Ground", new Color(.18f,.35f,.46f), new Color(.37f,.69f,.78f));
                    _wallTile = CreateTile("FrostPort_Wall", new Color(.06f,.15f,.24f), new Color(.30f,.63f,.78f));
                    _accentTile = CreateTile("FrostPort_Accent", new Color(.12f,.29f,.39f), new Color(.58f,.88f,.94f)); break;
                case Theme.Archive:
                    _groundTile = CreateTile("Archive_Ground", new Color(.31f,.40f,.50f), new Color(.60f,.75f,.84f));
                    _wallTile = CreateTile("Archive_Wall", new Color(.10f,.16f,.27f), new Color(.35f,.52f,.70f));
                    _accentTile = CreateTile("Archive_Accent", new Color(.18f,.28f,.43f), new Color(.69f,.88f,.95f)); break;
                case Theme.ForbiddenStacks:
                    _groundTile = CreateTile("ForbiddenStacks_Ground", new Color(.15f,.18f,.35f), new Color(.35f,.43f,.68f));
                    _wallTile = CreateTile("ForbiddenStacks_Wall", new Color(.04f,.05f,.13f), new Color(.21f,.27f,.50f));
                    _accentTile = CreateTile("ForbiddenStacks_Accent", new Color(.12f,.14f,.29f), new Color(.33f,.53f,.83f)); break;
                case Theme.MirrorVault:
                    _groundTile = CreateTile("MirrorVault_Ground", new Color(.22f,.23f,.43f), new Color(.49f,.59f,.85f));
                    _wallTile = CreateTile("MirrorVault_Wall", new Color(.06f,.06f,.16f), new Color(.31f,.36f,.66f));
                    _accentTile = CreateTile("MirrorVault_Accent", new Color(.19f,.17f,.39f), new Color(.60f,.76f,1f)); break;
                case Theme.BlueAbyss:
                    _groundTile = CreateTile("BlueAbyss_Ground", new Color(.09f,.11f,.29f), new Color(.24f,.39f,.70f));
                    _wallTile = CreateTile("BlueAbyss_Wall", new Color(.025f,.03f,.10f), new Color(.15f,.25f,.54f));
                    _accentTile = CreateTile("BlueAbyss_Accent", new Color(.08f,.10f,.30f), new Color(.31f,.65f,1f)); break;
                case Theme.VioletMarsh:
                    _groundTile = CreateTile("VioletMarsh_Ground", new Color(.22f,.18f,.32f), new Color(.42f,.31f,.52f));
                    _wallTile = CreateTile("VioletMarsh_Wall", new Color(.06f,.04f,.12f), new Color(.25f,.12f,.34f));
                    _accentTile = CreateTile("VioletMarsh_Accent", new Color(.15f,.10f,.25f), new Color(.61f,.34f,.72f)); break;
                case Theme.HowlVillage:
                    _groundTile = CreateTile("HowlVillage_Ground", new Color(.27f,.20f,.34f), new Color(.48f,.33f,.55f));
                    _wallTile = CreateTile("HowlVillage_Wall", new Color(.08f,.05f,.13f), new Color(.29f,.16f,.35f));
                    _accentTile = CreateTile("HowlVillage_Accent", new Color(.18f,.11f,.27f), new Color(.70f,.43f,.75f)); break;
                case Theme.MoonfangForest:
                    _groundTile = CreateTile("MoonfangForest_Ground", new Color(.15f,.22f,.29f), new Color(.30f,.42f,.48f));
                    _wallTile = CreateTile("MoonfangForest_Wall", new Color(.035f,.07f,.11f), new Color(.13f,.27f,.32f));
                    _accentTile = CreateTile("MoonfangForest_Accent", new Color(.09f,.18f,.22f), new Color(.42f,.38f,.72f)); break;
                case Theme.BloodmoonRidge:
                    _groundTile = CreateTile("BloodmoonRidge_Ground", new Color(.31f,.13f,.23f), new Color(.54f,.22f,.37f));
                    _wallTile = CreateTile("BloodmoonRidge_Wall", new Color(.10f,.025f,.07f), new Color(.34f,.08f,.18f));
                    _accentTile = CreateTile("BloodmoonRidge_Accent", new Color(.25f,.07f,.16f), new Color(.88f,.25f,.46f)); break;
                case Theme.BeastDen:
                    _groundTile = CreateTile("BeastDen_Ground", new Color(.19f,.10f,.28f), new Color(.39f,.19f,.54f));
                    _wallTile = CreateTile("BeastDen_Wall", new Color(.045f,.015f,.08f), new Color(.20f,.055f,.31f));
                    _accentTile = CreateTile("BeastDen_Accent", new Color(.15f,.045f,.24f), new Color(.67f,.26f,.88f)); break;
                case Theme.ThreadMarket:
                    _groundTile = CreateTile("ThreadMarket_Ground", new Color(.32f,.20f,.28f), new Color(.55f,.32f,.43f));
                    _wallTile = CreateTile("ThreadMarket_Wall", new Color(.10f,.05f,.10f), new Color(.34f,.15f,.27f));
                    _accentTile = CreateTile("ThreadMarket_Accent", new Color(.24f,.11f,.21f), new Color(.86f,.47f,.65f)); break;
                case Theme.ClockworkAlley:
                    _groundTile = CreateTile("ClockworkAlley_Ground", new Color(.34f,.25f,.19f), new Color(.56f,.40f,.27f));
                    _wallTile = CreateTile("ClockworkAlley_Wall", new Color(.11f,.07f,.06f), new Color(.35f,.22f,.14f));
                    _accentTile = CreateTile("ClockworkAlley_Accent", new Color(.27f,.16f,.11f), new Color(.82f,.57f,.32f)); break;
                case Theme.MarionetteOpera:
                    _groundTile = CreateTile("MarionetteOpera_Ground", new Color(.34f,.12f,.22f), new Color(.58f,.20f,.36f));
                    _wallTile = CreateTile("MarionetteOpera_Wall", new Color(.10f,.02f,.06f), new Color(.34f,.07f,.16f));
                    _accentTile = CreateTile("MarionetteOpera_Accent", new Color(.27f,.06f,.15f), new Color(.92f,.27f,.52f)); break;
                case Theme.SeveredWorkshop:
                    _groundTile = CreateTile("SeveredWorkshop_Ground", new Color(.27f,.20f,.25f), new Color(.48f,.33f,.43f));
                    _wallTile = CreateTile("SeveredWorkshop_Wall", new Color(.075f,.045f,.075f), new Color(.27f,.14f,.23f));
                    _accentTile = CreateTile("SeveredWorkshop_Accent", new Color(.20f,.11f,.19f), new Color(.68f,.38f,.57f)); break;
                case Theme.PuppeteerStage:
                    _groundTile = CreateTile("PuppeteerStage_Ground", new Color(.27f,.07f,.18f), new Color(.50f,.12f,.31f));
                    _wallTile = CreateTile("PuppeteerStage_Wall", new Color(.065f,.012f,.045f), new Color(.28f,.035f,.14f));
                    _accentTile = CreateTile("PuppeteerStage_Accent", new Color(.22f,.035f,.13f), new Color(.94f,.19f,.48f)); break;
                case Theme.Censor:
                    _groundTile = CreateTile("Censor_Ground", new Color(.26f,.26f,.30f), new Color(.44f,.43f,.49f));
                    _wallTile = CreateTile("Censor_Wall", new Color(.055f,.05f,.065f), new Color(.20f,.17f,.23f));
                    _accentTile = CreateTile("Censor_Accent", new Color(.16f,.13f,.18f), new Color(.48f,.39f,.53f)); break;
                case Theme.BlackArchive:
                    _groundTile = CreateTile("BlackArchive_Ground", new Color(.12f,.09f,.15f), new Color(.25f,.18f,.30f));
                    _wallTile = CreateTile("BlackArchive_Wall", new Color(.018f,.012f,.025f), new Color(.10f,.065f,.13f));
                    _accentTile = CreateTile("BlackArchive_Accent", new Color(.08f,.045f,.10f), new Color(.35f,.22f,.42f)); break;
                case Theme.MemorySea:
                    _groundTile=CreateTile("MemorySea_Ground",new Color(.14f,.29f,.39f),new Color(.30f,.55f,.67f)); _wallTile=CreateTile("MemorySea_Wall",new Color(.035f,.10f,.17f),new Color(.15f,.34f,.47f)); _accentTile=CreateTile("MemorySea_Accent",new Color(.08f,.21f,.31f),new Color(.43f,.78f,.88f)); break;
                case Theme.MoonPalace:
                    _groundTile=CreateTile("MoonPalace_Ground",new Color(.19f,.17f,.36f),new Color(.41f,.36f,.65f)); _wallTile=CreateTile("MoonPalace_Wall",new Color(.035f,.025f,.09f),new Color(.17f,.12f,.31f)); _accentTile=CreateTile("MoonPalace_Accent",new Color(.13f,.09f,.27f),new Color(.74f,.68f,1f)); break;
                case Theme.AshThrone:
                    _groundTile = CreateTile("AshThrone_Ground", new Color(.25f,.10f,.09f), new Color(.42f,.15f,.10f));
                    _wallTile = CreateTile("AshThrone_Wall", new Color(.055f,.012f,.015f), new Color(.22f,.035f,.03f));
                    _accentTile = CreateTile("AshThrone_Accent", new Color(.28f,.055f,.035f), new Color(1f,.36f,.10f)); break;
                default:
                    _groundTile = CreateTile("Boss_Ground", new Color(.22f,.20f,.34f), new Color(.36f,.34f,.51f));
                    _wallTile = CreateTile("Boss_Wall", new Color(.025f,.018f,.055f), new Color(.15f,.10f,.24f));
                    _accentTile = CreateTile("Boss_Accent", new Color(.24f,.16f,.38f), new Color(.76f,.72f,.95f)); break;
            }
        }

        private static Tile CreateTile(string name, Color baseColor, Color detailColor)
        {
            Sprite finalSprite = LoadFinalPixelSprite($"{FinalTileFolder}/{name}.png", 32f, new Vector2(.5f, .5f));
            if (finalSprite != null) return SaveTile(name, finalSprite);
            string texturePath = $"{TileArtFolder}/{name}.png";
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            bool ground = name.EndsWith("_Ground");
            bool wall = name.EndsWith("_Wall");
            bool accent = name.EndsWith("_Accent");
            Color dark = Color.Lerp(baseColor, Color.black, .38f);
            Color transparent = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                Color color = baseColor;
                if (ground)
                {
                    bool patch = ((x / 8 + y / 8) & 1) == 0;
                    bool fleck = (x * 13 + y * 7 + name.Length) % 53 < 2;
                    color = fleck ? detailColor : patch ? baseColor : Color.Lerp(baseColor, dark, .10f);
                }
                else if (wall)
                {
                    bool mortar = y % 8 < 2 || ((x + (y / 8 % 2) * 8) % 16) < 2;
                    bool rim = y > 27;
                    color = rim ? detailColor : mortar ? dark : baseColor;
                }
                else if (accent)
                {
                    int dx = x - 16;
                    int dy = y - 14;
                    int radius = dx * dx + dy * dy;
                    bool trunk = x >= 13 && x <= 18 && y < 10;
                    if (!trunk && radius > 205) color = transparent;
                    else if (trunk) color = dark;
                    else if (radius < 115 && ((x / 4 + y / 3) & 1) == 0) color = dark;
                    else color = detailColor;
                }
                texture.SetPixel(x, y, color);
            }
            texture.Apply(); File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = accent; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return SaveTile(name, AssetDatabase.LoadAssetAtPath<Sprite>(texturePath));
        }

        private static Tile CreatePixelMeadowTile(string name, PixelTileKind kind)
        {
            Sprite finalSprite = LoadFinalPixelSprite($"{FinalTileFolder}/{name}.png", 32f, new Vector2(.5f, .5f));
            if (finalSprite != null) return SaveTile(name, finalSprite);
            string texturePath = $"{TileArtFolder}/{name}.png";
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Color grassDark = new Color32(37, 67, 83, 255);
            Color grass = new Color32(50, 88, 98, 255);
            Color grassLight = new Color32(68, 119, 119, 255);
            Color pathDark = new Color32(74, 71, 104, 255);
            Color path = new Color32(105, 96, 137, 255);
            Color pathLight = new Color32(139, 125, 163, 255);
            Color navy = new Color32(14, 31, 68, 255);
            Color blue = new Color32(24, 63, 105, 255);
            Color cyan = new Color32(55, 126, 151, 255);
            Color rock = new Color32(41, 43, 70, 255);
            Color rockLight = new Color32(92, 87, 126, 255);
            Color outline = new Color32(17, 24, 48, 255);

            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                Color color;
                switch (kind)
                {
                    case PixelTileKind.Path:
                        color = ((x / 4 + y / 4) & 1) == 0 ? path : pathDark;
                        if ((x * 11 + y * 7) % 43 < 2) color = pathLight;
                        break;
                    case PixelTileKind.Water:
                        color = (y / 3 & 1) == 0 ? navy : blue;
                        if ((y == 7 || y == 21) && x % 8 >= 2 && x % 8 <= 5) color = cyan;
                        break;
                    case PixelTileKind.Cliff:
                        color = rock;
                        if (y >= 26) color = rockLight;
                        if (y == 25 || x < 2 || x > 29) color = outline;
                        if (y < 24 && (x == 8 || x == 21) && y % 9 < 5) color = rockLight;
                        break;
                    case PixelTileKind.Bush:
                        color = grass;
                        int dx = x - 16;
                        int dy = y - 15;
                        int distance = dx * dx + dy * dy;
                        if (distance < 205) color = grassDark;
                        if (distance < 145 && ((x / 4 + y / 3) & 1) == 0) color = new Color32(20, 58, 68, 255);
                        if ((distance > 95 && distance < 155) && (x + y) % 5 < 2) color = grassLight;
                        if (distance >= 205) color.a = 0f;
                        break;
                    default:
                        color = ((x / 8 + y / 8) & 1) == 0 ? grass : new Color32(46, 82, 94, 255);
                        if ((x * 7 + y * 13) % 97 < 2) color = grassLight;
                        break;
                }
                texture.SetPixel(x, y, color);
            }

            texture.Apply(false, false);
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.alphaIsTransparency = kind == PixelTileKind.Bush;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return SaveTile(name, AssetDatabase.LoadAssetAtPath<Sprite>(texturePath));
        }

        private static Tile SaveTile(string name, Sprite sprite)
        {
            string tilePath = $"{TileFolder}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, tilePath);
            }
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static Sprite LoadFinalPixelSprite(string path, float pixelsPerUnit, Vector2 pivot)
        {
            if (!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreatePixelPlayerSprite(string name, FacingDir facing, int frame)
        {
            string path = $"{PixelCharacterFolder}/{name}.png";
            var texture = new Texture2D(24, 32, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 24; x++) texture.SetPixel(x, y, clear);

            Color outline = new Color32(19, 18, 39, 255);
            Color hair = new Color32(31, 34, 63, 255);
            Color hairLight = new Color32(63, 70, 119, 255);
            Color coat = new Color32(42, 35, 74, 255);
            Color coatLight = new Color32(78, 59, 119, 255);
            Color skin = new Color32(224, 190, 181, 255);
            Color cyan = new Color32(75, 211, 222, 255);
            Color pale = new Color32(211, 218, 224, 255);

            int leftStep = frame == 0 ? 1 : 0;
            int rightStep = frame == 0 ? 0 : 1;
            FillPixelRect(texture, 7, 2 + leftStep, 4, 7, outline);
            FillPixelRect(texture, 13, 2 + rightStep, 4, 7, outline);
            FillPixelRect(texture, 8, 3 + leftStep, 2, 5, coatLight);
            FillPixelRect(texture, 14, 3 + rightStep, 2, 5, coatLight);
            FillPixelRect(texture, 4, 9, 16, 10, outline);
            FillPixelRect(texture, 6, 10, 12, 10, coat);
            FillPixelRect(texture, 5, 10, 3, 7, coatLight);
            FillPixelRect(texture, 16, 10, 3, 7, coatLight);
            FillPixelRect(texture, 10, 10, 4, 8, new Color32(28, 25, 52, 255));
            FillPixelRect(texture, 10, 17, 4, 2, pale);

            if (facing == FacingDir.Up)
            {
                FillPixelRect(texture, 6, 19, 12, 10, outline);
                FillPixelRect(texture, 7, 20, 10, 9, hair);
                FillPixelRect(texture, 8, 27, 8, 3, hairLight);
                FillPixelRect(texture, 6, 21, 2, 5, hairLight);
                FillPixelRect(texture, 16, 21, 2, 5, hairLight);
            }
            else if (facing == FacingDir.Right)
            {
                FillPixelRect(texture, 6, 19, 12, 10, outline);
                FillPixelRect(texture, 7, 20, 10, 9, hair);
                FillPixelRect(texture, 13, 21, 5, 5, skin);
                FillPixelRect(texture, 16, 23, 2, 1, cyan);
                FillPixelRect(texture, 8, 27, 8, 3, hairLight);
                FillPixelRect(texture, 5, 11, 4, 6, outline);
                FillPixelRect(texture, 6, 12, 3, 4, cyan);
            }
            else
            {
                FillPixelRect(texture, 6, 19, 12, 10, outline);
                FillPixelRect(texture, 7, 20, 10, 8, skin);
                FillPixelRect(texture, 7, 25, 10, 5, hair);
                FillPixelRect(texture, 6, 22, 3, 6, hair);
                FillPixelRect(texture, 15, 22, 3, 6, hair);
                FillPixelRect(texture, 9, 23, 2, 1, cyan);
                FillPixelRect(texture, 13, 23, 2, 1, cyan);
                FillPixelRect(texture, 5, 11, 4, 6, outline);
                FillPixelRect(texture, 6, 12, 3, 4, cyan);
            }

            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, 0f);
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void FillPixelRect(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            for (int py = y; py < y + height; py++)
            for (int px = x; px < x + width; px++) texture.SetPixel(px, py, color);
        }

        private static Sprite CreatePixelEncounterSprite()
        {
            if (_pixelEncounterSprite != null) return _pixelEncounterSprite;
            const string path = PixelCharacterFolder + "/ShadowSymbol.png";
            var texture = NewClearTexture(24, 24);
            Color outline = new Color32(18, 16, 42, 255);
            Color body = new Color32(76, 57, 116, 255);
            Color light = new Color32(132, 91, 174, 255);
            Color eye = new Color32(130, 241, 232, 255);
            FillPixelRect(texture, 8, 5, 8, 13, outline);
            FillPixelRect(texture, 9, 7, 6, 10, body);
            for (int i = 0; i < 7; i++)
            {
                FillPixelRect(texture, 3 + i, 8 + i / 2, 1, 7 - i, i < 2 ? outline : body);
                FillPixelRect(texture, 20 - i, 8 + i / 2, 1, 7 - i, i < 2 ? outline : body);
            }
            FillPixelRect(texture, 7, 17, 3, 3, outline);
            FillPixelRect(texture, 14, 17, 3, 3, outline);
            FillPixelRect(texture, 9, 14, 2, 2, eye);
            FillPixelRect(texture, 13, 14, 2, 2, eye);
            FillPixelRect(texture, 10, 8, 4, 2, light);
            _pixelEncounterSprite = SavePixelSprite(texture, path, 32f);
            return _pixelEncounterSprite;
        }

        private static Sprite CreatePixelLanternKeeperSprite()
        {
            if (_pixelLanternKeeperSprite != null) return _pixelLanternKeeperSprite;
            const string path = PixelCharacterFolder + "/LanternKeeper.png";
            var texture = NewClearTexture(24, 32);
            Color outline = new Color32(20, 22, 45, 255);
            Color cloak = new Color32(37, 73, 83, 255);
            Color cloakLight = new Color32(63, 116, 116, 255);
            Color amber = new Color32(255, 181, 76, 255);
            Color flame = new Color32(255, 232, 139, 255);
            FillPixelRect(texture, 7, 3, 10, 17, outline);
            FillPixelRect(texture, 8, 4, 8, 15, cloak);
            FillPixelRect(texture, 5, 16, 14, 11, outline);
            FillPixelRect(texture, 7, 17, 10, 9, cloak);
            FillPixelRect(texture, 9, 19, 6, 5, new Color32(13, 35, 43, 255));
            FillPixelRect(texture, 10, 21, 1, 1, amber);
            FillPixelRect(texture, 13, 21, 1, 1, amber);
            FillPixelRect(texture, 8, 26, 8, 3, cloakLight);
            FillPixelRect(texture, 17, 8, 4, 7, outline);
            FillPixelRect(texture, 18, 9, 2, 5, amber);
            FillPixelRect(texture, 18, 11, 2, 2, flame);
            _pixelLanternKeeperSprite = SavePixelSprite(texture, path, 32f);
            return _pixelLanternKeeperSprite;
        }

        private static Sprite[] CreatePixelFieldActorFrames(ShadowData shadow)
        {
            if (shadow == null) return System.Array.Empty<Sprite>();
            Sprite[] finalFrames = LoadFinalFieldFrames(shadow.shadowId);
            if (finalFrames != null) return finalFrames;
            return new[] { CreatePixelFieldActorSprite(shadow, 0), CreatePixelFieldActorSprite(shadow, 1) };
        }

        private static Sprite[] LoadFinalFieldFrames(string shadowId)
        {
            var result = new Sprite[2];
            for (int i = 0; i < result.Length; i++)
            {
                string path = $"{FinalFieldCharacterFolder}/{shadowId}_{i + 1:00}.png";
                if (!File.Exists(path)) return null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) return null;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 32;
                importer.SaveAndReimport();
                result[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return result;
        }

        private static Sprite CreatePixelFieldActorSprite(ShadowData shadow, int frame = 0)
        {
            if (shadow == null || string.IsNullOrEmpty(shadow.shadowId)) return null;
            string cacheKey = $"{shadow.shadowId}:{frame}";
            if (PixelFieldActors.TryGetValue(cacheKey, out Sprite cached) && cached != null) return cached;

            string safeId = shadow.shadowId.Replace("/", "_").Replace("\\", "_");
            string suffix = frame == 0 ? string.Empty : $"_{frame + 1:00}";
            string path = $"{PixelCharacterFolder}/Field_{safeId}{suffix}.png";
            var texture = NewClearTexture(24, 32);
            Color accent = ClampColor(shadow.accentColor);
            Color light = Color.Lerp(accent, Color.white, .42f);
            Color dark = Color.Lerp(accent, new Color(.025f, .02f, .07f), .68f);
            Color body = Color.Lerp(accent, new Color(.08f, .07f, .14f), .48f);
            Color outline = new Color32(16, 15, 34, 255);

            // Feet and lower cloak. A one-pixel asymmetry keeps each generated actor from
            // looking like a scaled battle silhouette when viewed on the field grid.
            int seed = StableHash(shadow.shadowId);
            int footShift = ((seed & 1) + frame) & 1;
            FillPixelRect(texture, 7, 2 + footShift, 4, 6, outline);
            FillPixelRect(texture, 13, 3 - footShift, 4, 5, outline);
            FillPixelRect(texture, 8, 3 + footShift, 2, 4, dark);
            FillPixelRect(texture, 14, 4 - footShift, 2, 3, dark);

            int shoulder = shadow.role == ShadowRole.Tank ? 3 : shadow.role == ShadowRole.SpeedUtility ? 5 : 4;
            int width = 24 - shoulder * 2;
            FillPixelRect(texture, shoulder, 8, width, 12, outline);
            FillPixelRect(texture, shoulder + 2, 9, width - 4, 10, body);
            FillPixelRect(texture, shoulder + 1, 10, 2, 7, accent);
            FillPixelRect(texture, 21 - shoulder, 10, 2, 7, accent);

            // Head silhouette varies by combat role, creating readable NPC/boss families.
            if (shadow.role == ShadowRole.MagicNuker || shadow.role == ShadowRole.Support)
            {
                FillPixelRect(texture, 5, 20, 14, 4, outline);
                FillPixelRect(texture, 7, 23, 10, 6, outline);
                FillPixelRect(texture, 8, 21, 8, 7, dark);
                if (shadow.role == ShadowRole.MagicNuker) FillPixelRect(texture, 10, 29, 4, 2, accent);
            }
            else if (shadow.role == ShadowRole.SpeedUtility)
            {
                FillPixelRect(texture, 6, 20, 12, 9, outline);
                FillPixelRect(texture, 7, 21, 10, 7, dark);
                FillPixelRect(texture, 5, 27, 4, 4, outline);
                FillPixelRect(texture, 15, 27, 4, 4, outline);
                FillPixelRect(texture, 6, 28, 2, 2, accent);
                FillPixelRect(texture, 16, 28, 2, 2, accent);
            }
            else
            {
                FillPixelRect(texture, 6, 20, 12, 9, outline);
                FillPixelRect(texture, 7, 21, 10, 7, dark);
                if (shadow.role == ShadowRole.Tank)
                {
                    FillPixelRect(texture, 4, 23, 3, 5, outline);
                    FillPixelRect(texture, 17, 23, 3, 5, outline);
                }
            }

            int eyeY = 23 + ((seed >> 2) & 1);
            FillPixelRect(texture, 9, eyeY, 2, 2, light);
            FillPixelRect(texture, 13, eyeY, 2, 2, light);

            // Physical attackers carry a small side weapon; legendary actors gain a crown flare.
            if (shadow.role == ShadowRole.PhysicalDealer)
            {
                FillPixelRect(texture, 19, 8, 2, 13, outline);
                FillPixelRect(texture, 20, 10, 1, 10, light);
            }
            if (shadow.growthTier == GrowthTier.Legendary || shadow.growthTier == GrowthTier.RegionalBoss)
            {
                FillPixelRect(texture, 8, 29, 2, 2, light);
                FillPixelRect(texture, 11, 30, 2, 2, accent);
                FillPixelRect(texture, 14, 29, 2, 2, light);
            }

            Sprite sprite = SavePixelSprite(texture, path, 32f);
            PixelFieldActors[cacheKey] = sprite;
            return sprite;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return hash & int.MaxValue;
            }
        }

        private static Color ClampColor(Color color) => new Color(
            Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);

        private static Texture2D NewClearTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) texture.SetPixel(x, y, clear);
            return texture;
        }

        private static Sprite SavePixelSprite(Texture2D texture, string path, float pixelsPerUnit)
        {
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, 0f);
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void RegisterBuildScenes()
        {
            var names = new List<string> { "Title" };
            RegionCatalogData catalog = LoadRegionCatalog();
            catalog.regions.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (RegionData region in catalog.regions) names.Add(region.sceneName);
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
        private static void Save(Scene scene, string name)
        {
            int removed = MissingScriptRepairUtility.RemoveFromScene(scene, name);
            if (removed > 0) Debug.LogWarning($"[Prologue] {name} 씬에서 Missing Script {removed}개를 정리했습니다.");
            EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{name}.unity");
        }

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
        private static void Set(SerializedObject so, string name, float value) => so.FindProperty(name).floatValue = value;
        private static void Set(SerializedObject so, string name, Vector2 value) => so.FindProperty(name).vector2Value = value;
        private static void Set(SerializedObject so, string name, Vector2Int value) => so.FindProperty(name).vector2IntValue = value;
        private static void Set(SerializedObject so, string name, Color value) => so.FindProperty(name).colorValue = value;
        private static void SetObjectArray(SerializedProperty property, IList<GameObject> values)
        { property.arraySize=values?.Count??0;for(int i=0;i<property.arraySize;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; }
        private static void SetIntArray(SerializedProperty property, int[] values)
        {
            property.arraySize = values != null ? values.Length : 0;
            for (int i = 0; i < property.arraySize; i++) property.GetArrayElementAtIndex(i).intValue = values[i];
        }

        private static void EnsureFolders()
        {
            Ensure("Assets", "Scenes"); Ensure("Assets/Scenes", "Prologue");
            Ensure("Assets/Data/Generated", "Tiles"); Ensure("Assets/Art/Generated", "Tiles");
            Ensure("Assets/Data/Generated", "Materials"); Ensure("Assets/Art/Generated", "Atmosphere");
            Ensure("Assets/Art/Generated", "Characters");
        }
        private static void Ensure(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        private enum Theme
        {
            Theater, Village, Meadow, Boss, AshWastes, EmberCity, Catacombs, AshThrone,
            FrostPort, Archive, ForbiddenStacks, MirrorVault, BlueAbyss,
            VioletMarsh, HowlVillage, MoonfangForest, BloodmoonRidge, BeastDen,
            ThreadMarket, ClockworkAlley, MarionetteOpera, SeveredWorkshop, PuppeteerStage,
            Censor, BlackArchive, MemorySea, MoonPalace, FinalTheater, CosmicStage
        }
        private enum PixelTileKind { Grass, Path, Water, Cliff, Bush }
        private class EnvironmentProfile
        {
            public Color globalColor, pointColor, secondaryLightColor, fogColor, moteColor;
            public float globalIntensity, pointIntensity, fogAlpha, fogPulse, motePulse, pulseSpeed;
            public float ambienceVolume, detailVolume;
            public Vector2 fogDrift, moteDrift, detailInterval;
            public Vector2[] lightPositions;
        }
        private class PlayerSpriteSet { public Sprite[] down, up, side; }
        private class FieldSceneContext { public Scene scene; public Transform fieldRoot; }
        [System.Serializable] private class RegionalRosterManifest { public List<RegionalRosterEntry> entries; }
        [System.Serializable] private class RegionalRosterEntry
        {
            public string shadowId, regionId, sceneName;
            public int minLevel, maxLevel;
            public bool featured;
        }
    }
}
#endif
