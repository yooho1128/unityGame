using System;
using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>
    /// 필드 ↔ 전투 전환 담당 (명세서 6장 흐름):
    ///   심볼 충돌 → 플레이어 이동 잠금 → Fade Out → 필드 숨김/전투 표시 → BattleManager.StartBattle(적 주입)
    ///   → Fade In → 전투 → 결과 반영(포획/경험치/재화/플래그) → Fade Out → 필드 복귀 → Fade In → 자동 저장
    ///
    /// 씬 구성 (단일 씬 + 루트 토글 방식):
    ///   [GameFlow]      ← 이 컴포넌트. fieldRoot/battleRoot 바깥에 둘 것 (꺼지면 코루틴이 멈춤)
    ///   [FieldRoot]     ← Grid/Tilemap, Player, 심볼, 필드 UI, 필드 카메라
    ///   [BattleRoot]    ← BattleManager, 전투 무대, 전투 UI, 전투 카메라 (기본 비활성)
    ///   [FaderCanvas]   ← ScreenFader
    /// </summary>
    public class GameFlowController : MonoBehaviour
    {
        public static GameFlowController Instance { get; private set; }

        [SerializeField] private GameObject fieldRoot;
        [SerializeField] private GameObject battleRoot;
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private float encounterFlashTime = 0.25f; // 충돌 직후 잠깐 멈춤 (긴장감)

        /// <summary>전투 종료 후 (퀘스트/대화 시스템이 구독)</summary>
        public event Action<BattleContext, BattleOutcome> OnBattleFlowFinished;
        /// <summary>각본집 신규 기록 (도감 알림 팝업 등)</summary>
        public event Action<ShadowInstance, bool> OnShadowRecorded; // (개체, 파티에 들어갔는지)

        public bool IsInBattle { get; private set; }
        private float _encountersEnabledAt;
        public bool CanStartEncounter =>
            !IsInBattle && PlayerController.Instance != null && !PlayerController.Instance.IsLocked
            && Time.unscaledTime >= _encountersEnabledAt
            && (MapLoader.Instance == null || !MapLoader.Instance.IsTransitioning)
            && SaveManager.HasAliveShadow();

        public void AllowTimeToLeave(float seconds = 1.5f) =>
            _encountersEnabledAt = Mathf.Max(_encountersEnabledAt, Time.unscaledTime + Mathf.Max(0f, seconds));

        [Header("개발용: 세이브 없을 때 바로 시작")]
        [SerializeField] private ShadowData devStarter;
        [SerializeField] private ItemData devStartItem;

        private void Awake()
        {
            Instance = this;
            if (battleRoot != null) battleRoot.SetActive(false);
            SaveManager.BeforeSave += WritePlayerPosition;
        }

        private void OnDestroy()
        {
            SaveManager.BeforeSave -= WritePlayerPosition;
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            // 타이틀 화면이 생기기 전까지: 세이브 있으면 로드, 없으면 개발용 새 게임
            if (SaveManager.Current == null && !SaveManager.Instance.Load())
            {
                if (devStarter == null)
                {
                    Debug.LogError("[GameFlow] 세이브도 devStarter도 없음");
                    yield break;
                }
                SaveManager.Instance.NewGame(devStarter);
                if (devStartItem != null) SaveManager.AddItem(devStartItem.itemId, 3);

                // 새 게임: 현재 배치된 위치를 시작점/체크포인트로
                yield return null; // PlayerController.Start 이후
                var p = PlayerController.Instance;
                var s = SaveManager.Current;
                s.checkpointX = s.tileX = p.Cell.x;
                s.checkpointY = s.tileY = p.Cell.y;
                yield break;
            }

            yield return null;
            var save = SaveManager.Current;
            var requested = new Vector2Int(save.tileX, save.tileY);
            var region = RegionRepository.GetByScene(FieldGrid.Current != null ? FieldGrid.Current.MapId : save.mapId);
            var fallback = region != null ? region.ArrivalCell : PlayerController.Instance.Cell;
            if (PlayerController.Instance.SnapToSafeCell(requested, fallback, out Vector2Int resolved) &&
                resolved != requested)
            {
                save.tileX = resolved.x;
                save.tileY = resolved.y;
                if (save.checkpointMapId == save.mapId && save.checkpointX == requested.x &&
                    save.checkpointY == requested.y)
                {
                    save.checkpointX = resolved.x;
                    save.checkpointY = resolved.y;
                }
                Debug.LogWarning($"[GameFlow] 막힌 저장 좌표 {requested}를 {resolved}(으)로 복구했습니다.");
                SaveManager.Instance.Save(false);
                SaveFeedbackController.Instance?.ShowMessage(
                    L10n.Get("save.position_recovered", "막힌 위치에서 가까운 안전한 길로 이동했습니다."));
            }
            PlayerController.Instance.SetFacing((FacingDir)save.facing);
        }

        // ────────────────────────────────────────────
        #region Public API

        /// <summary>필드 심볼과 충돌했을 때 (EncounterSymbol이 호출)</summary>
        public void RequestSymbolBattle(EncounterSymbol symbol)
        {
            if (!CanStartEncounter) return;

            var ctx = CreateContext(symbol.Mode, symbol.BuildEnemyParty(), symbol.EncounterId, symbol.Gimmick);
            StartCoroutine(BattleFlow(ctx, symbol, null, null));
        }

        /// <summary>NPC 대화 후 라이벌전, 보스 연출 후 전투 등 스크립트에서 직접 시작</summary>
        public void StartScriptedBattle(BattleMode mode, List<ShadowInstance> enemies, string encounterId,
                                        Action<BattleOutcome> onFinished = null)
        {
            if (IsInBattle) return;
            var ctx = CreateContext(mode, enemies, encounterId, BattleGimmick.None);
            StartCoroutine(BattleFlow(ctx, null, onFinished, null));
        }

        /// <summary>지역 색상과 선두 적 실루엣을 사용해 랜덤 조우 전환을 재생한다.</summary>
        public void StartRandomBattle(List<ShadowInstance> enemies, RegionData region)
        {
            if (!CanStartEncounter || enemies == null || enemies.Count == 0) return;
            var ctx = CreateContext(BattleMode.Wild, enemies, string.Empty, BattleGimmick.None);
            ShadowInstance lead = enemies[0];
            var cue = new EncounterEntranceCue
            {
                silhouette = lead?.Silhouette,
                color = region != null ? region.AccentColor : new Color(.46f, .31f, .78f, 1f)
            };
            StartCoroutine(BattleFlow(ctx, null, null, cue));
        }

        #endregion

        // ────────────────────────────────────────────
        #region Flow

        private BattleContext CreateContext(BattleMode mode, List<ShadowInstance> enemies, string encounterId,
            BattleGimmick gimmick)
        {
            var save = SaveManager.Current;
            RegionData region = RegionRepository.GetByScene(save.mapId);
            FieldWorldSnapshot world = FieldWorldState.Current(region);
            return new BattleContext
            {
                mode = mode,
                playerParty = save.party,              // 같은 인스턴스를 넘겨서 HP/경험치가 그대로 세이브에 반영됨
                enemyParty = enemies,
                inventory = SaveManager.BuildBattleInventory(),
                encounterId = encounterId,
                gimmick = gimmick,
                regionId = region?.regionId,
                timeOfDay = world.timeOfDay,
                weather = world.weather,
                bossInvestigationLevel = mode == BattleMode.Boss && enemies != null && enemies.Count > 0
                    ? BossInvestigationService.PreparationLevel(enemies[0].Data) : 0,
                autoBattle = save.autoBattle,
                timeScale = save.battleSpeed
            };
        }

        private IEnumerator BattleFlow(BattleContext ctx, EncounterSymbol symbol, Action<BattleOutcome> onFinished,
            EncounterEntranceCue entranceCue)
        {
            IsInBattle = true;
            var player = PlayerController.Instance;
            player.Lock();
            player.MoveInput = Vector2.zero;
            if (symbol != null)
            {
                symbol.Freeze(true);Vector2 delta=symbol.transform.position-player.transform.position;
                Vector2Int direction=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?
                    new Vector2Int(delta.x>=0f?1:-1,0):new Vector2Int(0,delta.y>=0f?1:-1);
                player.SetFacing(FieldGrid.VectorToDir(direction));symbol.FaceToward(player.transform.position);
                FieldCameraFollow.Instance?.FocusOn((player.transform.position+symbol.transform.position)*.5f,.35f);
                GameSettings.TryVibrate();
            }

            foreach (var e in ctx.enemyParty) SaveManager.MarkSeen(e.shadowId);

            // BattleUnit은 SaveData의 ShadowInstance HP를 직접 변경한다. 앱이 전투 도중 종료돼도
            // 절반만 정산된 상태가 저장되지 않도록 전투 시작 직전의 일관된 체크포인트를 남긴다.
            SaveManager.Instance?.Save();

            // ── 진입 ──
            FieldAmbientAudio.Instance?.BeginFadeOut();
            AdaptiveMusicDirector.Instance?.EnterBattle(ctx.mode);
            if (entranceCue != null)
                yield return ScreenFader.Instance.PlayEncounterIntro(entranceCue.silhouette, entranceCue.color, .7f);
            else
            {
                yield return new WaitForSeconds(encounterFlashTime);
                yield return ScreenFader.Instance.FadeOut();
            }

            fieldRoot.SetActive(false);
            battleRoot.SetActive(true);

            BattleOutcome outcome = null;
            void Handler(BattleOutcome o) => outcome = o;
            battleManager.OnBattleEnded += Handler;
            if (!battleManager.StartBattle(ctx))
            {
                battleManager.OnBattleEnded -= Handler;
                Debug.LogError($"[GameFlow] 전투 시작 실패, 필드로 복구합니다: {ctx.encounterId}");
                battleRoot.SetActive(false);
                fieldRoot.SetActive(true);
                AdaptiveMusicDirector.Instance?.ReturnToField();
                symbol?.Freeze(false);
                player.Unlock();
                AllowTimeToLeave();
                IsInBattle = false;
                yield return ScreenFader.Instance.FadeIn();
                yield break;
            }

            yield return ScreenFader.Instance.FadeIn();

            // ── 전투 진행 ──
            yield return new WaitUntil(() => outcome != null);
            battleManager.OnBattleEnded -= Handler;

            // ── 결과 반영 (화면 전환 전에 데이터부터) ──
            ApplyOutcome(ctx, outcome);

            // ── 복귀 ──
            yield return ScreenFader.Instance.FadeOut();
            battleRoot.SetActive(false);
            fieldRoot.SetActive(true);
            AdaptiveMusicDirector.Instance?.ReturnToField();

            if (symbol != null) symbol.OnBattleFinished(outcome.result); // fieldRoot 켜진 뒤 호출해야 코루틴 동작

            if (outcome.result == BattleResult.Defeat && TryTravelToCheckpoint(ctx, outcome, onFinished))
                yield break;
            if (outcome.result == BattleResult.Defeat) RespawnAtCheckpoint();

            yield return ScreenFader.Instance.FadeIn();

            player.Unlock();
            AllowTimeToLeave();
            IsInBattle = false;

            SaveManager.Instance.Save();
            ShowCaptureDestination(outcome);
            onFinished?.Invoke(outcome);
            OnBattleFlowFinished?.Invoke(ctx, outcome);
        }

        private sealed class EncounterEntranceCue
        {
            public Sprite silhouette;
            public Color color;
        }

        private void ApplyOutcome(BattleContext ctx, BattleOutcome outcome)
        {
            var save = SaveManager.Current;
            SaveManager.ApplyBattleInventory(ctx.inventory);
            SaveManager.AddGold(outcome.goldGained);
            foreach (BattleLoot loot in outcome.loot)
                if (loot != null && !string.IsNullOrEmpty(loot.itemId) && loot.count > 0)
                    SaveManager.AddItem(loot.itemId, loot.count);
            save.autoBattle = battleManager.IsAuto;
            save.battleSpeed = ctx.timeScale;

            switch (outcome.result)
            {
                case BattleResult.Captured:
                {
                    var shadow = outcome.capturedShadow;
                    shadow.FullHeal(); // 기억이 정화되어 온전한 모습으로 합류
                    bool toParty = SaveManager.AddCapturedShadow(shadow, out bool newlyRecorded, out int fragments);
                    outcome.memoryFragmentsGained = fragments;
                    if (newlyRecorded)
                        QuestManager.Instance?.Notify(QuestObjectiveType.Record, shadow.shadowId);
                    // 포획 개체와 퀘스트 진행을 같은 저장 스냅샷으로 확정한다.
                    SaveManager.Instance.Save();
                    if (fragments <= 0) OnShadowRecorded?.Invoke(shadow, toParty);
                    break;
                }
                case BattleResult.Victory:
                    if (ctx.mode == BattleMode.Wild)
                        save.wildVictoryCount = SaveValuePolicy.SaturatingAdd(save.wildVictoryCount, 1);
                    SaveManager.MarkEncounterCleared(outcome.encounterId);
                    QuestManager.Instance?.Notify(QuestObjectiveType.Defeat, outcome.encounterId);
                    ShadowInstance companion = CompanionBondService.CurrentCompanion();
                    if (companion != null)
                    {
                        bool leveled = CompanionBondService.AddExperience(companion.instanceId,
                            CompanionBondService.VictoryExperience(ctx.mode), out int oldBond, out int newBond);
                        if (leveled) SaveFeedbackController.Instance?.ShowMessage(
                            $"{L10n.Text(companion.DisplayName)}과의 유대가 Lv.{oldBond} → Lv.{newBond}(으)로 깊어졌습니다.\n" +
                            CompanionBondService.BondDialogue(companion,newBond));
                    }
                    string growth = CollectionGrowthService.ApplyBattleGrowth(outcome, ctx.mode);
                    if (!string.IsNullOrEmpty(growth)) SaveFeedbackController.Instance?.ShowMessage(growth);
                    break;

                case BattleResult.Defeat:
                    SaveManager.HealParty();
                    break;
            }
        }

        private void RespawnAtCheckpoint()
        {
            var save = SaveManager.Current;
            string currentMap = FieldGrid.Current != null ? FieldGrid.Current.MapId : save.mapId;
            var region = RegionRepository.GetByScene(currentMap);
            var fallback = region != null ? region.ArrivalCell : Vector2Int.zero;
            // 다른 지역의 좌표를 현재 지역 좌표로 해석하지 않는다.
            var requested = save.checkpointMapId == currentMap
                ? new Vector2Int(save.checkpointX, save.checkpointY) : fallback;
            if (PlayerController.Instance.SnapToSafeCell(requested, fallback, out Vector2Int resolved))
            {
                save.checkpointX = resolved.x;
                save.checkpointY = resolved.y;
                save.mapId = save.checkpointMapId = currentMap;
                save.tileX = resolved.x;
                save.tileY = resolved.y;
            }
            PlayerController.Instance.SetFacing(FacingDir.Down);
        }

        private static void ShowCaptureDestination(BattleOutcome outcome)
        {
            var captured = outcome?.capturedShadow;
            if (captured == null || SaveManager.Current == null) return;
            if (outcome.memoryFragmentsGained > 0)
            {
                SaveFeedbackController.Instance?.ShowMessage(
                    $"이미 기록한 {L10n.Text(captured.DisplayName)}의 기억이 조각 {outcome.memoryFragmentsGained}개로 정리되었습니다.");
                return;
            }
            bool inParty = SaveManager.Current.party.Exists(x => x.instanceId == captured.instanceId);
            string name = L10n.Text(captured.DisplayName);
            SaveFeedbackController.Instance?.ShowMessage(inParty
                ? L10n.Format("capture.to_party", "{0}이(가) 파티에 합류했습니다!", name)
                : L10n.Format("capture.to_storage", "파티가 가득 차 {0}을(를) 각본 서고에 보관했습니다.", name));
        }

        /// <summary>패배 체크포인트가 다른 맵이면 현재 페이드 상태를 유지한 채 해당 씬으로 이동한다.</summary>
        private bool TryTravelToCheckpoint(BattleContext context, BattleOutcome outcome,
                                           Action<BattleOutcome> onFinished)
        {
            var save = SaveManager.Current;
            if (save == null || string.IsNullOrEmpty(save.checkpointMapId) ||
                save.checkpointMapId == FieldGrid.Current.MapId) return false;

            IsInBattle = false;
            var player = PlayerController.Instance;
            bool started = MapLoader.Instance != null && MapLoader.Instance.TravelTo(save.checkpointMapId,
                new Vector2Int(save.checkpointX, save.checkpointY), FacingDir.Down, true, () =>
                {
                    if (!ReferenceEquals(save, SaveManager.Current))
                    {
                        if (player != null) player.Unlock();
                        return;
                    }
                    RespawnAtCheckpoint();
                    if (player != null) player.Unlock(); // MapLoader의 잠금 외에 남은 전투 잠금 해제
                    AllowTimeToLeave();
                    SaveManager.Instance?.Save();
                    SaveFeedbackController.Instance?.ShowMessage(L10n.Get("battle.checkpoint_fallback", "거점으로 이동하지 못해 현재 지역의 안전한 길에서 회복했습니다."));
                });
            if (started)
            {
                onFinished?.Invoke(outcome);
                OnBattleFlowFinished?.Invoke(context, outcome);
            }
            else IsInBattle = true;
            return started;
        }

        #endregion

        // ────────────────────────────────────────────

        /// <summary>저장 직전 플레이어 위치를 세이브에 기록 (SaveManager.Save 전에 호출)</summary>
        public static void WritePlayerPosition()
        {
            // 포털 페이드 중에는 MapLoader가 이미 기록한 목표 씬/도착점을 이전 씬의 플레이어 위치로
            // 덮어쓰면 안 된다. 모바일 백그라운드 저장이 전환 중 끼어들어도 한쪽 상태만 남긴다.
            if (MapLoader.Instance != null && MapLoader.Instance.IsTransitioning) return;
            var p = PlayerController.Instance;
            var save = SaveManager.Current;
            if (p == null || save == null) return;
            save.mapId = FieldGrid.Current != null ? FieldGrid.Current.MapId : save.mapId;
            Vector2Int cell=p.Cell;FacingDir facing=p.Facing;
            if(TownInteriorController.Instance!=null&&
               TownInteriorController.Instance.TryGetOutdoorPosition(out Vector2Int outdoorCell,out FacingDir outdoorFacing))
            {cell=outdoorCell;facing=outdoorFacing;}
            save.tileX = cell.x;
            save.tileY = cell.y;
            save.facing = (int)facing;
        }
    }
}
