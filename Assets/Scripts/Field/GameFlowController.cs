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
        public bool CanStartEncounter =>
            !IsInBattle && PlayerController.Instance != null && !PlayerController.Instance.IsLocked
            && SaveManager.HasAliveShadow();

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
            PlayerController.Instance.SnapToCell(new Vector2Int(save.tileX, save.tileY));
            PlayerController.Instance.SetFacing((FacingDir)save.facing);
        }

        // ────────────────────────────────────────────
        #region Public API

        /// <summary>필드 심볼과 충돌했을 때 (EncounterSymbol이 호출)</summary>
        public void RequestSymbolBattle(EncounterSymbol symbol)
        {
            if (!CanStartEncounter) return;

            var ctx = CreateContext(symbol.Mode, symbol.BuildEnemyParty(), symbol.EncounterId);
            StartCoroutine(BattleFlow(ctx, symbol, null));
        }

        /// <summary>NPC 대화 후 라이벌전, 보스 연출 후 전투 등 스크립트에서 직접 시작</summary>
        public void StartScriptedBattle(BattleMode mode, List<ShadowInstance> enemies, string encounterId,
                                        Action<BattleOutcome> onFinished = null)
        {
            if (IsInBattle) return;
            var ctx = CreateContext(mode, enemies, encounterId);
            StartCoroutine(BattleFlow(ctx, null, onFinished));
        }

        #endregion

        // ────────────────────────────────────────────
        #region Flow

        private BattleContext CreateContext(BattleMode mode, List<ShadowInstance> enemies, string encounterId)
        {
            var save = SaveManager.Current;
            return new BattleContext
            {
                mode = mode,
                playerParty = save.party,              // 같은 인스턴스를 넘겨서 HP/경험치가 그대로 세이브에 반영됨
                enemyParty = enemies,
                inventory = SaveManager.BuildBattleInventory(),
                encounterId = encounterId,
                autoBattle = save.autoBattle,
                timeScale = save.battleSpeed
            };
        }

        private IEnumerator BattleFlow(BattleContext ctx, EncounterSymbol symbol, Action<BattleOutcome> onFinished)
        {
            IsInBattle = true;
            var player = PlayerController.Instance;
            player.Lock();
            player.MoveInput = Vector2.zero;
            if (symbol != null) symbol.Freeze(true);

            foreach (var e in ctx.enemyParty) SaveManager.MarkSeen(e.shadowId);

            // ── 진입 ──
            FieldAmbientAudio.Instance?.BeginFadeOut();
            AdaptiveMusicDirector.Instance?.EnterBattle(ctx.mode);
            yield return new WaitForSeconds(encounterFlashTime);
            yield return ScreenFader.Instance.FadeOut();

            fieldRoot.SetActive(false);
            battleRoot.SetActive(true);

            BattleOutcome outcome = null;
            void Handler(BattleOutcome o) => outcome = o;
            battleManager.OnBattleEnded += Handler;
            battleManager.StartBattle(ctx);

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
            IsInBattle = false;

            SaveManager.Instance.Save();
            onFinished?.Invoke(outcome);
            OnBattleFlowFinished?.Invoke(ctx, outcome);
        }

        private void ApplyOutcome(BattleContext ctx, BattleOutcome outcome)
        {
            var save = SaveManager.Current;
            SaveManager.ApplyBattleInventory(ctx.inventory);
            save.gold += outcome.goldGained;
            save.autoBattle = battleManager.IsAuto;
            save.battleSpeed = ctx.timeScale;

            switch (outcome.result)
            {
                case BattleResult.Captured:
                {
                    var shadow = outcome.capturedShadow;
                    shadow.FullHeal(); // 기억이 정화되어 온전한 모습으로 합류
                    bool toParty = SaveManager.AddCapturedShadow(shadow);
                    OnShadowRecorded?.Invoke(shadow, toParty);
                    QuestManager.Instance?.Notify(QuestObjectiveType.Record, shadow.shadowId);
                    break;
                }
                case BattleResult.Victory:
                    SaveManager.MarkEncounterCleared(outcome.encounterId);
                    QuestManager.Instance?.Notify(QuestObjectiveType.Defeat, outcome.encounterId);
                    break;

                case BattleResult.Defeat:
                    SaveManager.HealParty();
                    break;
            }
        }

        private void RespawnAtCheckpoint()
        {
            var save = SaveManager.Current;
            PlayerController.Instance.SnapToCell(new Vector2Int(save.checkpointX, save.checkpointY));
            PlayerController.Instance.SetFacing(FacingDir.Down);
        }

        /// <summary>패배 체크포인트가 다른 맵이면 현재 페이드 상태를 유지한 채 해당 씬으로 이동한다.</summary>
        private bool TryTravelToCheckpoint(BattleContext context, BattleOutcome outcome,
                                           Action<BattleOutcome> onFinished)
        {
            var save = SaveManager.Current;
            if (save == null || string.IsNullOrEmpty(save.checkpointMapId) ||
                save.checkpointMapId == FieldGrid.Current.MapId) return false;

            IsInBattle = false;
            bool started = MapLoader.Instance != null && MapLoader.Instance.TravelTo(save.checkpointMapId,
                new Vector2Int(save.checkpointX, save.checkpointY), FacingDir.Down, true);
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
            var p = PlayerController.Instance;
            var save = SaveManager.Current;
            if (p == null || save == null) return;
            save.mapId = FieldGrid.Current != null ? FieldGrid.Current.MapId : save.mapId;
            save.tileX = p.Cell.x;
            save.tileY = p.Cell.y;
            save.facing = (int)p.Facing;
        }
    }
}
