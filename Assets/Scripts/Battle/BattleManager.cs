using System;
using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Battle
{
    /// <summary>
    /// 1대1 턴제 전투 진행자.
    ///
    /// 턴 흐름:
    ///   턴 시작(FP +1) → 플레이어 입력 대기(Auto면 AI) → 적 AI 선택
    ///   → 우선순위(도주 > 교체 > 도구/기록 > 공격) + 속도 순 정렬 → 행동 처리 → 기절 처리
    ///   → 턴 종료(화상/출혈 피해, 상태 지속시간 감소) → 반복
    ///
    /// 로직만 담당하고 연출은 IBattlePresenter, 화면 갱신은 이벤트로 UI에 위임한다.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        [Header("연출 (IBattlePresenter 구현 컴포넌트)")]
        [SerializeField] private MonoBehaviour presenterComponent;

        [Header("FP (공연 열기)")]
        [SerializeField] private int maxFp = 5;
        [SerializeField] private int startFp = 0;
        [SerializeField] private int fpPerTurn = 1;

        // ── 이벤트 (UI 바인딩용) ──
        public event Action<BattleState> OnStateChanged;
        public event Action<BattleSide, int> OnFpChanged;          // side, 현재 FP
        public event Action<int> OnMomentumChanged;                // 플레이어 열연 게이지 (0~3)
        public event Action<BattleUnit> OnUnitChanged;             // HP/상태 갱신 필요
        public event Action<BattleSide, BattleUnit> OnActiveChanged; // 교체/등장
        public event Action<BattleOutcome> OnBattleEnded;

        // ── 상태 ──
        public BattleState State { get; private set; } = BattleState.None;
        public BattleContext Context { get; private set; }
        public int Turn { get; private set; }
        public BattleReviewEntry CurrentReview=>_outcome?.review;

        public IReadOnlyList<BattleUnit> PlayerUnits => _playerUnits;
        public IReadOnlyList<BattleUnit> EnemyUnits => _enemyUnits;
        public int PlayerActiveIndex => _playerIdx;
        public int EnemyActiveIndex => _enemyIdx;
        public BattleUnit PlayerActive => _playerUnits[_playerIdx];
        public BattleUnit EnemyActive => _enemyUnits[_enemyIdx];

        private IBattlePresenter _presenter;
        private readonly List<BattleUnit> _playerUnits = new List<BattleUnit>();
        private readonly List<BattleUnit> _enemyUnits = new List<BattleUnit>();
        private int _playerIdx, _enemyIdx;
        private readonly int[] _fp = new int[2];
        private readonly int[] _lastVoluntarySwitchTurn = { -99, -99 };
        private readonly HashSet<BattleUnit> _playerParticipants = new HashSet<BattleUnit>();
        private readonly HashSet<BattleUnit> _battleParticipants = new HashSet<BattleUnit>();
        private readonly HashSet<BattleUnit> _bossPhaseActivated = new HashSet<BattleUnit>();

        private BattleAction _pendingAction;
        private int _pendingForcedSwitch = -1;
        private BattleOutcome _outcome;
        private float _captureBonus = 1f;
        private bool _jointSkillUsed;
        private BattleUnit _companionUnit;
        private int _escapeAttempts;
        private string _lastPlayerSkillId;
        private Coroutine _routine;
        private readonly BattleMomentum _momentum = new BattleMomentum();
        private int _playerFaints;
        private readonly Dictionary<string, BattleContribution> _contributions =
            new Dictionary<string, BattleContribution>(StringComparer.Ordinal);
        private string _lastDecisiveAction;
        private readonly AudienceRequest _audience = new AudienceRequest();
        public AudienceRequest Audience => _audience;

        private sealed class BattleContribution
        {
            public BattleUnit unit;
            public int damage, healing, actions, finishes;
            public int Score => damage + Mathf.RoundToInt(healing * .7f) + actions * 8 + finishes * 120;
        }

        // ────────────────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _presenter = presenterComponent as IBattlePresenter;
            if (_presenter == null)
            {
                Debug.LogWarning("[BattleManager] Presenter 미지정 → DebugBattlePresenter 사용");
                _presenter = gameObject.AddComponent<DebugBattlePresenter>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        // ────────────────────────────────────────────
        #region Public API

        /// <summary>전투 시작. EncounterTrigger → (Fade Out) → 이 메서드 호출</summary>
        public bool StartBattle(BattleContext context)
        {
            if (State != BattleState.None && !IsEnded)
            {
                Debug.LogWarning("[BattleManager] 이미 전투 중");
                return false;
            }
            if (context?.playerParty == null || context.enemyParty == null ||
                context.playerParty.Count == 0 || context.enemyParty.Count == 0 ||
                context.playerParty.Exists(x => x == null) || context.enemyParty.Exists(x => x == null))
            {
                Debug.LogError("[BattleManager] BattleContext 파티 정보 누락");
                return false;
            }

            Context = context;
            _playerUnits.Clear();
            _enemyUnits.Clear();
            CompanionPassiveSnapshot passive = CompanionBondService.CurrentPassive();
            foreach (var s in context.playerParty) _playerUnits.Add(new BattleUnit(s, BattleSide.Player, passive));
            var region = RegionRepository.Get(context.regionId);
            var difficulty = EncounterDifficultyPolicy.For(region != null ? region.act : 0, context.mode);
            foreach (var s in context.enemyParty)
            {
                var enemy = new BattleUnit(s, BattleSide.Enemy);
                enemy.ConfigureDifficulty(difficulty);
                _enemyUnits.Add(enemy);
            }

            _playerIdx = FindNextAlive(_playerUnits, -1);
            _enemyIdx = FindNextAlive(_enemyUnits, -1);
            if (_playerIdx < 0 || _enemyIdx < 0)
            {
                Debug.LogError("[BattleManager] 싸울 수 있는 그림자가 한쪽 파티에 없음");
                return false;
            }
            _playerParticipants.Clear();
            _battleParticipants.Clear();
            _bossPhaseActivated.Clear();
            _playerParticipants.Add(PlayerActive);
            _battleParticipants.Add(PlayerActive);

            _fp[0] = _fp[1] = startFp;
            _lastVoluntarySwitchTurn[0] = _lastVoluntarySwitchTurn[1] = -99;
            Turn = 0;
            _captureBonus = 1f;
            _jointSkillUsed = false;
            ShadowInstance companion = CompanionBondService.CurrentCompanion();
            _companionUnit = companion == null ? null :
                _playerUnits.Find(x => x.Instance.instanceId == companion.instanceId);
            _escapeAttempts = 0;
            _lastPlayerSkillId = null;
            _momentum.Reset();
            OnMomentumChanged?.Invoke(_momentum.Value);
            _playerFaints = 0;
            _contributions.Clear();
            _lastDecisiveAction = string.Empty;
            _audience.Reset();
            _pendingAction = null;
            _pendingForcedSwitch = -1;
            _outcome = new BattleOutcome { encounterId = context.encounterId };
            _outcome.review=BattleReviewCapture.Begin(context,_playerUnits,_enemyUnits);

            _presenter.SetSpeed(context.timeScale);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(BattleRoutine());
            return true;
        }

        /// <summary>UI 버튼 → 플레이어 행동 제출. 유효하지 않으면 false</summary>
        public bool SubmitAction(BattleAction action) => SubmitAction(action, out _);

        public bool CanChooseAction => State == BattleState.WaitingForInput && _pendingAction == null;

        public bool SubmitAction(BattleAction action, out string reason)
        {
            reason = string.Empty;
            if (action == null || action.side != BattleSide.Player)
            { reason = L10n.Get("battle.invalid_action", "선택할 수 없는 행동입니다."); return false; }

            // 기절로 인한 강제 교체
            if (State == BattleState.ForcedSwitch)
            {
                if (_pendingForcedSwitch >= 0 || action.type != ActionType.Switch || !CanSwitchTo(action.switchIndex))
                { reason = L10n.Get("battle.switch_unavailable", "지금은 이 그림자로 교체할 수 없습니다."); return false; }
                _pendingForcedSwitch = action.switchIndex;
                return true;
            }

            if (!CanChooseAction)
            { reason = L10n.Get("battle.input_unavailable", "행동을 처리 중입니다. 다음 선택을 기다려 주세요."); return false; }
            if (!Validate(action, out reason))
            {
                Debug.Log($"[BattleManager] 행동 불가: {reason}");
                return false;
            }
            _pendingAction = action;
            return true;
        }


        public void SetSpeed(float timeScale)
        {
            if (Context != null) Context.timeScale = timeScale;
            _presenter.SetSpeed(timeScale);
        }

        public int GetFp(BattleSide side) => _fp[(int)side];
        public int MaxFp => maxFp;
        public int Momentum => _momentum.Value;
        public int MaxMomentum => BattleMomentum.Max;

        public bool CanUseItem(BattleSide side, ItemData item, out string reason)
        {
            reason = string.Empty;
            if (item == null || Context == null || !Context.HasItem(item))
            { reason = "보유 수량이 없습니다."; return false; }
            if (!item.usableInBattle) { reason = "필드 전용 도구입니다."; return false; }
            BattleUnit target = Active(side);
            if (target == null || target.IsFainted) { reason = "사용할 대상이 없습니다."; return false; }
            switch (item.effectType)
            {
                case ItemEffectType.HealFlat:
                case ItemEffectType.HealRatio:
                    if (target.Hp >= target.MaxHp) { reason = "HP가 이미 가득합니다."; return false; }
                    break;
                case ItemEffectType.CureStatus:
                    if (!target.HasAnyStatus) { reason = "해제할 상태 이상이 없습니다."; return false; }
                    break;
                case ItemEffectType.RestoreAndCleanse:
                    if (!target.NeedsRestoration) { reason = "회복할 HP나 해제할 상태 이상이 없습니다."; return false; }
                    break;
                case ItemEffectType.GainFP:
                    if (GetFp(side) >= maxFp) { reason = "FP가 이미 가득합니다."; return false; }
                    break;
                case ItemEffectType.CaptureBoost:
                    if (!Context.CanCapture) { reason = "기록 가능한 전투에서만 사용할 수 있습니다."; return false; }
                    if (item.value <= _captureBonus) { reason = "이미 더 진한 잉크가 적용 중입니다."; return false; }
                    break;
            }
            return true;
        }
        public bool CanRecordCurrent => Context != null && Context.CanCapture && _enemyUnits.Count > 0 &&
                                        !EnemyActive.IsFainted && EnemyActive.Data.IsCapturable;
        public float CurrentCaptureChance => CanRecordCurrent
            ? DamageCalculator.CaptureChance(EnemyActive, _captureBonus * PlayerActive.CompanionPassive.captureMultiplier) : 0f;

        public bool CanUseSkill(SkillData skill) => CanUseSkill(skill, out _);

        public bool CanUseSkill(SkillData skill, out string reason)
        {
            reason = string.Empty;
            if (Context == null || _playerIdx < 0 || _playerIdx >= _playerUnits.Count || skill == null ||
                PlayerActive.IsFainted || !PlayerActive.Skills.Contains(skill))
            { reason = L10n.Get("battle.skill_unavailable", "현재 그림자가 사용할 수 없는 기술입니다."); return false; }
            if (GetFp(BattleSide.Player) < skill.fpCost)
            { reason = L10n.Format("battle.fp_shortage", "FP 부족 · 현재 {0} / 필요 {1}", GetFp(BattleSide.Player), skill.fpCost); return false; }
            if (skill.target == SkillTarget.Self && skill.healRatio > 0f && PlayerActive.Hp >= PlayerActive.MaxHp)
            { reason = L10n.Get("battle.hp_full", "HP가 이미 가득합니다."); return false; }
            if (BattleUnit.IsHealingSkill(skill))
            {
                if (PlayerActive.HealingSkills.Remaining <= 0)
                { reason = "이번 전투의 회복 스킬을 모두 사용했습니다. (3/3)"; return false; }
                int wait = PlayerActive.HealingSkills.WaitTurns(Turn);
                if (wait > 0) { reason = $"회복 스킬 재사용까지 {wait}턴 남았습니다."; return false; }
            }
            return true;
        }

        public const int JointSkillFpCost = 5;
        public BattleUnit CompanionUnit => _companionUnit;
        public CompanionJointProfile JointProfile => CompanionBondService.JointProfile(_companionUnit?.Instance);
        public bool CanUseJointSkill => CanUseJointSkillNow(out _);

        public bool CanUseJointSkillNow(out string reason)
        {
            reason = string.Empty;
            if (_jointSkillUsed)
            { reason = L10n.Get("battle.joint_used", "이번 전투에서 합동기를 이미 사용했습니다."); return false; }
            if (_companionUnit == null || _companionUnit.IsFainted || !CompanionBondService.CanUseJointSkill(_companionUnit.Instance))
            { reason = L10n.Get("battle.joint_unavailable", "전투 가능한 동행과 충분한 유대가 필요합니다."); return false; }
            if (GetFp(BattleSide.Player) < JointSkillFpCost)
            { reason = L10n.Format("battle.fp_shortage", "FP 부족 · 현재 {0} / 필요 {1}", GetFp(BattleSide.Player), JointSkillFpCost); return false; }
            return true;
        }

        public bool CanSwitchTo(int index) =>
            index >= 0 && index < _playerUnits.Count && index != _playerIdx && !_playerUnits[index].IsFainted;

        public bool IsEnded => State == BattleState.Victory || State == BattleState.Defeat
                            || State == BattleState.Captured || State == BattleState.Escaped;

        #endregion

        // ────────────────────────────────────────────
        #region Main Loop

        private IEnumerator BattleRoutine()
        {
            SetState(BattleState.Intro);
            OnActiveChanged?.Invoke(BattleSide.Player, PlayerActive);
            OnActiveChanged?.Invoke(BattleSide.Enemy, EnemyActive);
            yield return _presenter.PlayIntro(PlayerActive, EnemyActive, Context.mode);
            if (Context.mode == BattleMode.Boss && Context.bossInvestigationLevel > 0)
            {
                BossInvestigationSnapshot investigation = BossInvestigationService.ForBoss(EnemyActive.Data);
                yield return _presenter.ShowMessage(Context.bossInvestigationLevel >= 3
                    ? $"사전 조사 완료 · 약점 {BossInvestigationService.ElementName(investigation.weakness)} · 페이즈 대응 준비"
                    : Context.bossInvestigationLevel >= 2 ? investigation.phaseHint : investigation.weaknessHint);
                if (Context.bossInvestigationLevel >= 2) AddFp(BattleSide.Player, 1);
            }
            yield return AnnounceStarVariant();
            yield return BeginAudienceRequest();

            while (!IsEnded)
            {
                Turn++;
                AddFp(BattleSide.Player, fpPerTurn);
                AddFp(BattleSide.Enemy, fpPerTurn);

                yield return ApplyBossPhase();

                yield return ApplyGimmickTurnStart();

                // 1) 플레이어 입력
                _pendingAction = null;
                SetState(BattleState.WaitingForInput);
                yield return new WaitUntil(() => _pendingAction != null);
                var playerAction = _pendingAction;
                playerAction.actor = PlayerActive;

                SetState(BattleState.ResolvingTurn);
                yield return ApplyEchoChoice(playerAction);

                // 2) 적 선택
                var enemyAction = ChooseAiAction(BattleSide.Enemy, Context.mode != BattleMode.Wild);
                enemyAction.actor = EnemyActive;

                // 3) 처리
                foreach (var action in OrderActions(playerAction, enemyAction))
                {
                    if (IsEnded) break;

                    var current = Active(action.side);
                    // 행동 전에 기절했거나 (기절 후 다른 유닛으로 교체되어) 다른 유닛이 나와 있으면 취소
                    if (current.IsFainted) continue;
                    if (action.type != ActionType.Switch && current != action.actor) continue;

                    yield return ExecuteAction(action);
                    yield return HandleFaints();
                }
                if (IsEnded) break;

                // 4) 턴 종료
                yield return EndOfTurn();
                yield return HandleFaints();
                if (!IsEnded && _audience.NotifyTurnEnded(Turn))
                    yield return _presenter.ShowMessage(_audience.FailedMessage);
            }

            BattleReviewCapture.EndCombat(_outcome,Turn,_playerFaints,_playerUnits,_enemyUnits);
            yield return _presenter.PlayResult(_outcome);
            OnBattleEnded?.Invoke(_outcome);
            _routine = null;
        }

        private List<BattleAction> OrderActions(BattleAction p, BattleAction e)
        {
            bool playerFirst;
            if (p.Priority != e.Priority) playerFirst = p.Priority > e.Priority;
            else if (PlayerActive.Spd != EnemyActive.Spd)
            {
                playerFirst = PlayerActive.Spd > EnemyActive.Spd;
                if (BattleGimmickRules.ReversesInitiative(Context?.gimmick ?? BattleGimmick.None, Turn))
                    playerFirst = !playerFirst;
            }
            else playerFirst = UnityEngine.Random.value < 0.5f;

            return playerFirst ? new List<BattleAction> { p, e } : new List<BattleAction> { e, p };
        }

        #endregion

        #region Hidden Battle Gimmicks

        private IEnumerator ApplyBossPhase()
        {
            if (Context.mode != BattleMode.Boss || EnemyActive.IsFainted ||
                _bossPhaseActivated.Contains(EnemyActive)) yield break;
            BossPhaseDefinition phase = BossPhaseRepository.Get(EnemyActive.Data.shadowId);
            float threshold = phase != null && phase.hpThreshold > 0f ? phase.hpThreshold : .5f;
            if (EnemyActive.HpRatio > threshold) yield break;
            _bossPhaseActivated.Add(EnemyActive);

            if (phase == null)
            {
                AddFp(BattleSide.Enemy, 2);
                yield return _presenter.ShowMessage(L10n.Format("battle.boss_phase",
                    "{0}의 비극이 폭주한다! 공연 열기가 치솟았다.", EnemyActive.Name));
                yield break;
            }

            int investigation = Context?.bossInvestigationLevel ?? 0;
            AddFp(BattleSide.Enemy, BossInvestigationService.ReducePhaseFp(phase.enemyFpGain, investigation));
            AddFp(BattleSide.Player, -BossInvestigationService.ReducePhaseFp(phase.playerFpDrain, investigation));
            if (phase.cleanseBoss) EnemyActive.ClearAllStatus();
            int healed = phase.healRatio > 0f
                ? EnemyActive.Heal(Mathf.RoundToInt(EnemyActive.MaxHp * phase.healRatio *
                    BossInvestigationService.PhaseHealMultiplier(investigation))) : 0;
            yield return _presenter.PlayBossPhase(EnemyActive, phase);
            if (healed > 0) yield return _presenter.PlayHeal(EnemyActive, healed);
            if (investigation < 3 && phase.TryGetPlayerStatus(out StatusEffectType status) &&
                PlayerActive.ApplyStatus(status, Mathf.Max(1, phase.statusDuration)))
                yield return _presenter.PlayStatusApplied(PlayerActive, status);
            OnUnitChanged?.Invoke(EnemyActive);
            OnUnitChanged?.Invoke(PlayerActive);
        }

        private IEnumerator ApplyGimmickTurnStart()
        {
            BattleGimmick gimmick = Context?.gimmick ?? BattleGimmick.None;
            if (BattleGimmickRules.IsBellWarningTurn(gimmick, Turn))
            {
                yield return _presenter.ShowMessage("녹슨 종이 크게 흔들린다. 다음 턴에는 양쪽의 방어가 무너진다!");
            }
            if (BattleGimmickRules.IsBellTollTurn(gimmick, Turn))
            {
                yield return _presenter.ShowMessage("봉화탑의 종이 울렸다! 불안정한 공명이 양쪽 방어를 낮춘다.");
                foreach (BattleUnit unit in new[] { PlayerActive, EnemyActive })
                {
                    if (unit.IsFainted || !unit.ApplyStatus(StatusEffectType.DefenseDown, 1)) continue;
                    yield return _presenter.PlayStatusApplied(unit, StatusEffectType.DefenseDown);
                    OnUnitChanged?.Invoke(unit);
                }
            }
            else if (BattleGimmickRules.ReversesInitiative(gimmick, Turn))
            {
                yield return _presenter.ShowMessage("달빛이 거꾸로 흐른다. 같은 종류의 행동은 느린 그림자가 먼저 움직인다!");
            }
            yield return ApplyEnvironmentTurnStart();
        }

        private IEnumerator ApplyEnvironmentTurnStart()
        {
            if (Context == null) yield break;
            if (Turn == 1)
            {
                _outcome.environmentSummary = EnvironmentLabel(Context.weather, Context.timeOfDay);
                yield return _presenter.ShowMessage("무대 환경 · " + _outcome.environmentSummary);
            }
            if (Turn <= 0 || Turn % 3 != 0) yield break;

            var affected = new List<BattleUnit>();
            StatusEffectType status = StatusEffectType.None;
            string message = string.Empty;
            switch (Context.weather)
            {
                case FieldWeather.Rain:
                    status = StatusEffectType.AttackDown; message = "빗물이 불꽃의 기세를 누른다!";
                    AddIfElement(affected, ShadowElement.Flame, true); break;
                case FieldWeather.Snow:
                    status = StatusEffectType.SpeedDown; message = "쌓인 눈이 서리 그림자 이외의 발을 붙잡는다!";
                    AddIfElement(affected, ShadowElement.Frost, false); break;
                case FieldWeather.Ashfall:
                    status = StatusEffectType.Burn; message = "뜨거운 재가 불꽃에 익숙하지 않은 그림자에게 달라붙는다!";
                    AddIfElement(affected, ShadowElement.Flame, false); break;
                case FieldWeather.InkStorm:
                    status = StatusEffectType.DefenseDown; message = "먹물 폭풍이 그림자 밖의 윤곽을 흐트러뜨린다!";
                    AddIfElement(affected, ShadowElement.Shade, false); break;
                case FieldWeather.Mist:
                    status = StatusEffectType.SpeedDown; message = "짙은 안개가 양쪽의 빠른 발을 늦춘다!";
                    affected.Add(PlayerActive); affected.Add(EnemyActive); break;
                case FieldWeather.MoonWind:
                    message = "달바람이 자줏빛 그림자의 공연 열기를 끌어올린다!";
                    if (PlayerActive.Data.element == ShadowElement.Shade) AddFp(BattleSide.Player, 1);
                    if (EnemyActive.Data.element == ShadowElement.Shade) AddFp(BattleSide.Enemy, 1);
                    break;
                case FieldWeather.ThreadDust:
                    message = "실먼지가 무명의 배역에게 새로운 즉흥 연기를 건넨다!";
                    if (PlayerActive.Data.element == ShadowElement.None) AddFp(BattleSide.Player, 1);
                    if (EnemyActive.Data.element == ShadowElement.None) AddFp(BattleSide.Enemy, 1);
                    break;
                case FieldWeather.Starfall:
                    message = "별빛이 희귀한 배역의 상처를 어루만진다!";
                    foreach (BattleUnit unit in new[] { PlayerActive, EnemyActive })
                        if (unit.Data.growthTier >= GrowthTier.Rare && !unit.IsFainted)
                        {
                            int healed = unit.Heal(Mathf.Max(1, unit.MaxHp / 16));
                            if (healed > 0) yield return _presenter.PlayHeal(unit, healed);
                            OnUnitChanged?.Invoke(unit);
                        }
                    break;
            }
            if (!string.IsNullOrEmpty(message)) yield return _presenter.ShowMessage(message);
            foreach (BattleUnit unit in affected)
            {
                if (unit == null || unit.IsFainted || !unit.ApplyStatus(status, 1)) continue;
                yield return _presenter.PlayStatusApplied(unit, status);
                OnUnitChanged?.Invoke(unit);
            }
        }

        private void AddIfElement(List<BattleUnit> target, ShadowElement element, bool matching)
        {
            foreach (BattleUnit unit in new[] { PlayerActive, EnemyActive })
                if ((unit.Data.element == element) == matching) target.Add(unit);
        }

        public static string EnvironmentLabel(FieldWeather weather, FieldTimeOfDay time)
        {
            string timeName = FieldWorldState.TimeLabel(time);
            string weatherName = FieldWorldState.WeatherLabel(weather);
            return $"{timeName} · {weatherName}";
        }

        private IEnumerator ApplyEchoChoice(BattleAction action)
        {
            if ((Context?.gimmick ?? BattleGimmick.None) != BattleGimmick.EchoSeal) yield break;
            string current = action != null &&
                             (action.type == ActionType.Attack || action.type == ActionType.Skill)
                ? action.skill?.skillId
                : null;
            if (BattleGimmickRules.IsEchoRepeat(Context.gimmick, _lastPlayerSkillId, current))
            {
                AddFp(BattleSide.Enemy, 2);
                yield return _presenter.ShowMessage("같은 기술의 메아리가 읽혔다. 가면 사냥꾼의 공연 열기 +2!");
            }
            else if (BattleGimmickRules.IsEchoBreak(Context.gimmick, _lastPlayerSkillId, current))
            {
                AddFp(BattleSide.Player, 1);
                yield return _presenter.ShowMessage("다른 울림으로 공명을 끊었다. 아군 공연 열기 +1!");
            }
            _lastPlayerSkillId = current;
        }

        #endregion

        // ────────────────────────────────────────────
        #region Action Execution

        private IEnumerator ExecuteAction(BattleAction a)
        {
            switch (a.type)
            {
                case ActionType.Attack:
                case ActionType.Skill:  yield return DoSkill(a); break;
                case ActionType.JointSkill: yield return DoJointSkill(); break;
                case ActionType.Switch: yield return DoSwitch(a.side, a.switchIndex); break;
                case ActionType.Record: yield return DoRecord(); break;
                case ActionType.Item:   yield return DoItem(a); break;
                case ActionType.Escape: yield return DoEscape(); break;
            }
        }

        private IEnumerator DoSkill(BattleAction a)
        {
            var user = Active(a.side);
            var opponent = Opponent(a.side);
            var skill = a.skill;

            if (skill == null) yield break;

            if (!user.CanAct)
            {
                yield return _presenter.PlayCantMove(user);
                yield return ConsumeFreeze(user);
                yield break;
            }

            // FP 처리 (AI가 잘못 골랐을 때 대비해 한 번 더 체크)
            if (GetFp(a.side) < skill.fpCost || !user.CanUseHealingSkill(skill, Turn))
            {
                skill = user.BasicAttack;
                if (skill == null || !user.CanUseHealingSkill(skill, Turn)) yield break;
            }
            AddFp(a.side, skill.fpGain - skill.fpCost);

            bool repertoire = a.side == BattleSide.Player && skill != user.BasicAttack &&
                              _audience.NotifyPlayerSkill(skill.skillId);

            // 자기 대상 (회복/버프)
            if (skill.target == SkillTarget.Self)
            {
                int healed = BattleUnit.IsHealingSkill(skill) ? user.UseHealingSkill(skill, Turn) : 0;
                if (a.side == BattleSide.Player) RecordContribution(user, 0, healed, false,
                    L10n.Text(skill.displayName));
                yield return _presenter.PlaySkill(user, user, skill, new HitResult { elementMultiplier = 1f });
                if (healed > 0) yield return _presenter.PlayHeal(user, healed);
                OnUnitChanged?.Invoke(user);
                if (repertoire) yield return AudienceAchieved();
                yield break;
            }

            // 적 대상
            var hit = DamageCalculator.Calculate(user, opponent, skill);
            bool encore = ApplyMomentum(a.side, skill.DealsDamage, ref hit);
            if (!hit.missed && hit.damage > 0)
                hit.damage = opponent.TakeDamage(hit.damage);
            if (a.side == BattleSide.Player) RecordContribution(user, hit.damage, 0,
                !hit.missed && opponent.IsFainted, L10n.Text(skill.displayName));
            bool encoreFinish = encore && opponent.IsFainted;
            if (encore) RecordEncore(encoreFinish);

            yield return _presenter.PlaySkill(user, opponent, skill, hit);
            if (encore)
                yield return _presenter.ShowMessage(L10n.Get("battle.encore",
                    "앙코르! 관객의 열광으로 피해가 25% 강해지고 FP를 1 되찾았다!"));
            if (encoreFinish)
                yield return _presenter.ShowMessage(L10n.Get("battle.encore_finish",
                    "완벽한 막내림! 앙코르로 마지막 장면을 장식했다."));
            OnUnitChanged?.Invoke(opponent);
            if (a.side == BattleSide.Player &&
                (repertoire | (encore && _audience.NotifyEncore()) | _audience.NotifyPlayerHit(hit, opponent.IsFainted)))
                yield return AudienceAchieved();

            if (!hit.missed && hit.statusApplied && !opponent.IsFainted &&
                opponent.ApplyStatus(skill.statusEffect, skill.statusDuration))
            {
                yield return _presenter.PlayStatusApplied(opponent, skill.statusEffect);
                OnUnitChanged?.Invoke(opponent);
                if (a.side == BattleSide.Player && _audience.NotifyStatusInflicted())
                    yield return AudienceAchieved();
            }
        }

        private IEnumerator DoJointSkill()
        {
            if (!CanUseJointSkill) yield break;
            BattleUnit user = PlayerActive;
            if (!user.CanAct)
            {
                yield return _presenter.PlayCantMove(user);
                yield return ConsumeFreeze(user);
                yield break;
            }
            CompanionJointProfile profile = JointProfile;
            AddFp(BattleSide.Player, -JointSkillFpCost);
            _jointSkillUsed = true;
            HitResult hit = DamageCalculator.CalculateJoint(user, _companionUnit, EnemyActive, profile);
            bool encore = ApplyMomentum(BattleSide.Player, true, ref hit);
            hit.damage = EnemyActive.TakeDamage(hit.damage);
            RecordContribution(user, hit.damage, 0, EnemyActive.IsFainted, profile.displayName);
            bool encoreFinish = encore && EnemyActive.IsFainted;
            if (encore) RecordEncore(encoreFinish);
            CompanionBondService.RecordJointSkillUse(_companionUnit.Instance.instanceId);
            yield return _presenter.PlayJointSkill(user, _companionUnit, EnemyActive, profile, hit);
            if (encore)
                yield return _presenter.ShowMessage(L10n.Get("battle.encore",
                    "앙코르! 관객의 열광으로 피해가 25% 강해지고 FP를 1 되찾았다!"));
            if (encoreFinish)
                yield return _presenter.ShowMessage(L10n.Get("battle.encore_finish",
                    "완벽한 막내림! 앙코르로 마지막 장면을 장식했다."));
            if (_audience.NotifyPlayerSkill(AudienceRequest.JointSkillKey) |
                (encore && _audience.NotifyEncore()) | _audience.NotifyPlayerHit(hit, EnemyActive.IsFainted))
                yield return AudienceAchieved();
            if (hit.statusApplied && !EnemyActive.IsFainted && EnemyActive.ApplyStatus(profile.status, 2))
            {
                yield return _presenter.PlayStatusApplied(EnemyActive, profile.status);
                if (_audience.NotifyStatusInflicted()) yield return AudienceAchieved();
            }
            OnUnitChanged?.Invoke(EnemyActive);
        }

        /// <summary>행동(공격/스킬/교체)을 한 번 소비한 빙결 유닛의 지속 횟수를 줄이고, 풀렸다면 알린다</summary>
        private IEnumerator ConsumeFreeze(BattleUnit unit)
        {
            if (!unit.ConsumeFreezeTurn()) yield break;
            yield return _presenter.ShowMessage(L10n.Format("battle.status_expired", "{0}의 [{1}] 상태가 풀렸다.",
                L10n.Text(unit.Name), BattleStatusFormatter.Label(StatusEffectType.Freeze)));
            OnUnitChanged?.Invoke(unit);
        }

        private IEnumerator DoSwitch(BattleSide side, int index)
        {
            var units = side == BattleSide.Player ? _playerUnits : _enemyUnits;
            if (index < 0 || index >= units.Count || units[index].IsFainted) yield break;

            var prev = Active(side);
            if (!prev.IsFainted)
            {
                _lastVoluntarySwitchTurn[(int)side] = Turn;
                prev.ClearDebuffs();
                yield return _presenter.PlayWithdraw(prev);
                yield return ConsumeFreeze(prev); // 빙결 중 교체도 행동 한 번으로 센다
            }

            if (side == BattleSide.Player)
            {
                _playerIdx = index;
                _playerParticipants.Add(PlayerActive);
                _battleParticipants.Add(PlayerActive);
            }
            else _enemyIdx = index;

            OnActiveChanged?.Invoke(side, Active(side));
            yield return _presenter.PlaySendOut(Active(side));
        }

        private IEnumerator DoRecord()
        {
            var target = EnemyActive;

            if (!Context.CanCapture)
            {
                yield return _presenter.ShowMessage("다른 연출가의 그림자는 기록할 수 없다!");
                yield break;
            }
            if (!target.Data.IsCapturable)
            {
                yield return _presenter.ShowMessage("이 그림자의 기억은 아직 각본집에 담을 수 없다!");
                yield break;
            }

            float chance = CurrentCaptureChance;
            _captureBonus = 1f; // 특수 잉크는 다음 한 번의 기록 시도에만 적용한다.
            bool success = UnityEngine.Random.value < chance;
            yield return _presenter.PlayRecordAttempt(target, success);
            OnUnitChanged?.Invoke(target);

            if (success)
            {
                target.ClearAllStatus();
                _outcome.capturedShadow = target.Instance;
                Finish(BattleResult.Captured, BattleState.Captured);
            }
        }

        private IEnumerator DoItem(BattleAction a)
        {
            var item = a.item;
            if (!CanUseItem(a.side, item, out string reason))
            {
                if (!string.IsNullOrEmpty(reason)) yield return _presenter.ShowMessage(reason);
                yield break;
            }

            if (Context.inventory != null)
            {
                if (!Context.inventory.TryGetValue(item, out int count) || count <= 0) yield break;
                Context.inventory[item] = count - 1;
            }

            var target = Active(a.side);
            yield return _presenter.PlayItem(target, item);

            switch (item.effectType)
            {
                case ItemEffectType.HealFlat:
                {
                    int healed = target.Heal(Mathf.RoundToInt(item.value));
                    if (a.side == BattleSide.Player) RecordContribution(target, 0, healed, false,
                        L10n.Text(item.displayName));
                    yield return _presenter.PlayHeal(target, healed);
                    break;
                }
                case ItemEffectType.HealRatio:
                {
                    int healed = target.Heal(Mathf.RoundToInt(target.MaxHp * item.value));
                    if (a.side == BattleSide.Player) RecordContribution(target, 0, healed, false,
                        L10n.Text(item.displayName));
                    yield return _presenter.PlayHeal(target, healed);
                    break;
                }
                case ItemEffectType.CureStatus:
                    target.ClearAllStatus();
                    break;
                case ItemEffectType.RestoreAndCleanse:
                {
                    int healed = target.RestoreAndCleanse(item.value);
                    if (a.side == BattleSide.Player) RecordContribution(target, 0, healed, false,
                        L10n.Text(item.displayName));
                    if (healed > 0) yield return _presenter.PlayHeal(target, healed);
                    yield return _presenter.ShowMessage("흐트러진 기억이 정돈되었다. 상태 이상이 모두 해제되었다!");
                    break;
                }
                case ItemEffectType.GainFP:
                    AddFp(a.side, Mathf.RoundToInt(item.value));
                    break;
                case ItemEffectType.CaptureBoost:
                    _captureBonus = Mathf.Max(_captureBonus, item.value);
                    yield return _presenter.ShowMessage("각본집의 잉크가 짙어졌다! (다음 기록 성공률 상승)");
                    break;
            }
            OnUnitChanged?.Invoke(target);
        }

        private IEnumerator DoEscape()
        {
            if (!Context.CanEscape)
            {
                yield return _presenter.ShowMessage("이 공연에서는 도망칠 수 없다!");
                yield break;
            }

            _escapeAttempts++;
            bool success = PlayerActive.Spd >= EnemyActive.Spd
                        || UnityEngine.Random.value < 0.5f + 0.1f * _escapeAttempts;

            yield return _presenter.PlayEscape(success);
            if (success) Finish(BattleResult.Escaped, BattleState.Escaped);
        }

        #endregion

        // ────────────────────────────────────────────
        #region Turn End / Faint

        private IEnumerator EndOfTurn()
        {
            foreach (var unit in new[] { PlayerActive, EnemyActive })
            {
                if (unit.IsFainted) continue;

                int dot = unit.GetDotDamage();
                if (dot > 0)
                {
                    var status = unit.MajorStatus.type;
                    int dealt = unit.TakeDamage(dot);
                    yield return _presenter.PlayStatusDamage(unit, status, dealt);
                }

                var expired = unit.TickStatus();
                foreach (var s in expired)
                    yield return _presenter.ShowMessage(L10n.Format("battle.status_expired", "{0}의 [{1}] 상태가 풀렸다.",
                        L10n.Text(unit.Name), BattleStatusFormatter.Label(s)));

                OnUnitChanged?.Invoke(unit);
            }
        }

        private IEnumerator HandleFaints()
        {
            if (IsEnded) yield break;

            // 동시 기절에서도 승리 확정 전에 관객의 무기절 조건을 판정한다.
            if (PlayerActive.IsFainted && _audience.NotifyPlayerFaint())
                yield return _presenter.ShowMessage(_audience.FailedMessage);

            // ── 적 기절 ──
            if (EnemyActive.IsFainted)
            {
                var defeated = EnemyActive;
                yield return _presenter.PlayFaint(defeated);
                yield return GrantExp(defeated);

                int next = FindNextAlive(_enemyUnits, _enemyIdx);
                if (next < 0)
                {
                    _outcome.goldGained = EnemyBaseGold();
                    BattlePerformanceResult performance = BattlePerformance.Evaluate(Turn, _enemyUnits.Count,
                        _outcome.encoreCount, _outcome.encoreFinishes, _playerFaints, _outcome.goldGained);
                    _outcome.performanceRank = performance.rank;
                    _outcome.performanceScore = performance.score;
                    _outcome.performanceBonusGold = performance.bonusGold;
                    _outcome.goldGained = (int)Math.Min((long)_outcome.goldGained + performance.bonusGold, int.MaxValue);
                    FinalizePerformanceSpotlight();
                    RollLoot();
                    Finish(BattleResult.Victory, BattleState.Victory);
                    yield break;
                }

                yield return _presenter.ShowMessage($"상대가 다음 그림자를 무대에 올린다!");
                yield return DoSwitch(BattleSide.Enemy, next);
            }

            // ── 아군 기절 ──
            if (PlayerActive.IsFainted)
            {
                _playerFaints++;
                yield return _presenter.PlayFaint(PlayerActive);

                int next = FindNextAlive(_playerUnits, -1);
                if (next < 0)
                {
                    Finish(BattleResult.Defeat, BattleState.Defeat);
                    yield break;
                }

                _pendingForcedSwitch = -1;
                SetState(BattleState.ForcedSwitch);
                yield return new WaitUntil(() => _pendingForcedSwitch >= 0);
                SetState(BattleState.ResolvingTurn);

                yield return DoSwitch(BattleSide.Player, _pendingForcedSwitch);
                _pendingForcedSwitch = -1;
            }
        }

        private void RollLoot()
        {
            foreach (BattleUnit enemy in _enemyUnits)
            {
                ShadowData data = enemy.Data;
                // 주연급 변주 그림자는 드롭 도구를 반드시 남긴다.
                bool guaranteed = enemy.Instance.starVariant;
                if (data == null || string.IsNullOrEmpty(data.dropItemId) ||
                    (!guaranteed && (data.dropChance <= 0f || UnityEngine.Random.value > data.dropChance))) continue;
                int count = UnityEngine.Random.Range(Mathf.Max(1, data.dropMinCount), Mathf.Max(data.dropMinCount, data.dropMaxCount) + 1);
                BattleLoot existing = _outcome.loot.Find(x => x.itemId == data.dropItemId);
                if (existing != null) existing.count += count;
                else _outcome.loot.Add(new BattleLoot { itemId = data.dropItemId, count = count });
            }
        }

        private void RecordContribution(BattleUnit unit, int damage, int healing, bool finish, string action)
        {
            if (unit?.Instance == null) return;
            if (!_contributions.TryGetValue(unit.Instance.instanceId, out BattleContribution value))
            {
                value = new BattleContribution { unit = unit };
                _contributions.Add(unit.Instance.instanceId, value);
            }
            value.damage = SaveValuePolicy.SaturatingAdd(value.damage, Mathf.Max(0, damage));
            value.healing = SaveValuePolicy.SaturatingAdd(value.healing, Mathf.Max(0, healing));
            value.actions++;
            if (finish)
            {
                value.finishes++;
                _lastDecisiveAction = $"{L10n.Text(unit.Name)}의 [{action}]이 마지막 장면을 완성했다";
            }
        }

        private void FinalizePerformanceSpotlight()
        {
            BattleContribution best = null;
            foreach (BattleContribution value in _contributions.Values)
            {
                if (value?.unit?.Instance == null) continue;
                if (best == null || value.Score > best.Score) best = value;
                if (!_outcome.participantInstanceIds.Contains(value.unit.Instance.instanceId))
                    _outcome.participantInstanceIds.Add(value.unit.Instance.instanceId);
            }
            foreach (BattleUnit unit in _battleParticipants)
                if (unit?.Instance != null && !_outcome.participantInstanceIds.Contains(unit.Instance.instanceId))
                    _outcome.participantInstanceIds.Add(unit.Instance.instanceId);
            if (best == null) best = new BattleContribution { unit = PlayerActive, actions = 1 };
            _outcome.standoutInstanceId = best.unit.Instance.instanceId;
            _outcome.standoutShadowName = L10n.Text(best.unit.Name);
            _outcome.decisiveAction = string.IsNullOrEmpty(_lastDecisiveAction)
                ? $"{_outcome.standoutShadowName}이 끝까지 무대를 지켰다"
                : _lastDecisiveAction;
        }

        private IEnumerator GrantExp(BattleUnit defeated)
        {
            int reward = StarVariantPolicy.Experience(DamageCalculator.ExpReward(defeated),
                defeated.Instance.starVariant);
            int supportCount = 0;
            foreach (BattleUnit receiver in _playerUnits)
            {
                if (receiver.Instance.IsMaxLevel) continue;
                bool participated = _playerParticipants.Contains(receiver);
                int exp = PartyExperiencePolicy.Amount(reward, participated);
                _outcome.expGained = SaveValuePolicy.SaturatingAdd(_outcome.expGained, exp);
                var knownSkills = new HashSet<SkillData>(receiver.Instance.GetAvailableSkills());
                int ups = PartyExperiencePolicy.Apply(receiver.Instance, exp);
                if (participated)
                    yield return _presenter.ShowMessage($"{receiver.Name}은(는) 참여 경험치 {exp}을(를) 얻었다.");
                else supportCount++;
                if (ups > 0)
                {
                    if (!_outcome.leveledUpInstanceIds.Contains(receiver.Instance.instanceId))
                        _outcome.leveledUpInstanceIds.Add(receiver.Instance.instanceId);
                    yield return _presenter.ShowMessage($"{receiver.Name}의 레벨이 {receiver.Level}(으)로 올랐다!");
                    foreach (SkillData skill in receiver.Instance.GetAvailableSkills())
                        if (!knownSkills.Contains(skill))
                            yield return _presenter.ShowMessage($"{receiver.Name}은(는) [{skill.displayName}] 기술을 깨달았다!");
                }
                OnUnitChanged?.Invoke(receiver);
            }
            if (supportCount > 0)
                yield return _presenter.ShowMessage($"대기 그림자 {supportCount}마리도 지원 경험치 {PartyExperiencePolicy.Amount(reward, false)}을(를) 각각 얻었다.");

            // 다음 적 그림자에 대한 참여 기록은 현재 출전 그림자부터 새로 시작한다.
            _playerParticipants.Clear();
            if (!PlayerActive.IsFainted) _playerParticipants.Add(PlayerActive);
        }

        #endregion

        // ────────────────────────────────────────────
        #region Helpers

        private BattleUnit Active(BattleSide side) => side == BattleSide.Player ? PlayerActive : EnemyActive;
        private BattleUnit Opponent(BattleSide side) => side == BattleSide.Player ? EnemyActive : PlayerActive;

        private BattleAction ChooseAiAction(BattleSide side, bool allowSwitch)
        {
            if (side == BattleSide.Player && CanUseJointSkill &&
                (Context?.mode == BattleMode.Boss || EnemyActive.HpRatio >= .35f))
                return BattleAction.UseJointSkill();
            var units = side == BattleSide.Player ? _playerUnits : _enemyUnits;
            int activeIndex = side == BattleSide.Player ? _playerIdx : _enemyIdx;
            if (allowSwitch && Turn - _lastVoluntarySwitchTurn[(int)side] >= 3)
            {
                int next = BattleAI.ChooseSwitchIndex(units, activeIndex, Opponent(side));
                if (next >= 0) return BattleAction.Switch(side, next);
            }
            BattleAiProfile profile = BattleAiProfile.Normal;
            if (side == BattleSide.Enemy)
            {
                bool boss = Context?.mode == BattleMode.Boss;
                profile = boss && Active(side).HpRatio <= .22f
                    ? BattleAiProfile.BossDesperate
                    : BattleAI.ProfileFor(Active(side).Data, boss);
            }
            return BattleAI.Choose(Active(side), Opponent(side), GetFp(side), profile, Turn);
        }

        private static int FindNextAlive(List<BattleUnit> units, int exclude)
        {
            for (int i = 0; i < units.Count; i++)
                if (i != exclude && !units[i].IsFainted) return i;
            return -1;
        }

        private void AddFp(BattleSide side, int delta)
        {
            int i = (int)side;
            int prev = _fp[i];
            _fp[i] = Mathf.Clamp(_fp[i] + delta, 0, maxFp);
            if (_fp[i] != prev) OnFpChanged?.Invoke(side, _fp[i]);
        }

        private bool ApplyMomentum(BattleSide side, bool dealsDamage, ref HitResult hit)
        {
            if (side != BattleSide.Player) return false;
            int previous = _momentum.Value;
            bool encore = _momentum.Apply(ref hit, dealsDamage);
            if (encore) AddFp(BattleSide.Player, 1);
            if (_momentum.Value != previous || encore) OnMomentumChanged?.Invoke(_momentum.Value);
            return encore;
        }

        private void RecordEncore(bool finishedEnemy)
        {
            if (_outcome.encoreCount < int.MaxValue) _outcome.encoreCount++;
            if (finishedEnemy && _outcome.encoreFinishes < int.MaxValue) _outcome.encoreFinishes++;
        }

        private bool Validate(BattleAction a, out string reason)
        {
            reason = null;
            switch (a.type)
            {
                case ActionType.Attack:
                    if (a.skill == null) a.skill = PlayerActive.BasicAttack;
                    if (a.skill == null) { reason = "기본 공격 미설정"; return false; }
                    if (!a.MatchesBasicAttack(PlayerActive)) { reason = "일반 공격 슬롯의 기술 불일치"; return false; }
                    return true;
                case ActionType.Skill:
                    return CanUseSkill(a.skill, out reason);
                case ActionType.JointSkill:
                    return CanUseJointSkillNow(out reason);
                case ActionType.Switch:
                    if (!CanSwitchTo(a.switchIndex)) { reason = "교체 불가"; return false; }
                    return true;
                case ActionType.Record:
                    if (!CanRecordCurrent) { reason = "기록할 수 없는 그림자"; return false; }
                    return true;
                case ActionType.Item:
                    return CanUseItem(BattleSide.Player, a.item, out reason);
                case ActionType.Escape:
                    if (!Context.CanEscape) { reason = "도주 불가 전투"; return false; }
                    return true;
            }
            reason = "알 수 없는 행동";
            return false;
        }

        private void Finish(BattleResult result, BattleState state)
        {
            _outcome.result = result;
            ConcludeShowcase(result);
            SetState(state);
        }

        private int EnemyBaseGold()
        {
            long gold = 0;
            foreach (BattleUnit enemy in _enemyUnits)
                gold += StarVariantPolicy.Gold(enemy.Data.goldReward, enemy.Instance.starVariant);
            return (int)Math.Min(gold, int.MaxValue);
        }

        /// <summary>관객 주문과 연속 흥행 보너스를 결과에 확정한다. 저장 반영은 GameFlowController가 한다.</summary>
        private void ConcludeShowcase(BattleResult result)
        {
            int enemyGold = EnemyBaseGold();
            if (_audience.IsActive)
            {
                _outcome.audienceRequestTitle = _audience.Title;
                _outcome.audienceRequestAchieved = _audience.Conclude(result);
                if (_outcome.audienceRequestAchieved)
                {
                    _outcome.audienceBonusGold = AudienceRequest.RewardGold(enemyGold, Context.mode);
                    _outcome.goldGained = SaveValuePolicy.SaturatingAdd(_outcome.goldGained, _outcome.audienceBonusGold);
                }
            }
            _outcome.performanceStreak = PerformanceStreakPolicy.Next(SaveManager.Current?.performanceStreak ?? 0, result);
            if (result == BattleResult.Victory)
            {
                _outcome.streakBonusGold = PerformanceStreakPolicy.BonusGold(enemyGold, _outcome.performanceStreak);
                _outcome.goldGained = SaveValuePolicy.SaturatingAdd(_outcome.goldGained, _outcome.streakBonusGold);
            }
        }

        private IEnumerator AnnounceStarVariant()
        {
            BattleUnit star = _enemyUnits.Find(x => x.Instance.starVariant);
            if (star == null) yield break;
            _outcome.starVariantAppeared = true;
            yield return _presenter.ShowMessage(
                $"객석이 술렁인다! 주연급 {L10n.Text(star.Name)}이(가) 무대에 올랐다!\n능력치 +10% · 경험치·금화 증가 · 전리품 확정");
        }

        private IEnumerator BeginAudienceRequest()
        {
            // 첫 전투 안내 중에는 새로운 규칙을 더하지 않는다.
            if (Context == null || SaveManager.Current == null || !SaveManager.HasFlag("tutorial_battle_complete"))
                yield break;
            var context = new AudienceRequestContext
            {
                mode = Context.mode,
                enemyCount = _enemyUnits.Count,
                canRecord = CanRecordCurrent
            };
            var repertoire = new HashSet<SkillData>();
            foreach (BattleUnit unit in _playerUnits)
            {
                if (unit.IsFainted) continue;
                if (HasWeaknessOption(unit, unit.BasicAttack)) context.hasWeaknessOption = true;
                foreach (SkillData skill in unit.Skills)
                {
                    if (skill == null || skill == unit.BasicAttack) continue;
                    repertoire.Add(skill);
                    if (HasWeaknessOption(unit, skill)) context.hasWeaknessOption = true;
                    if (skill.target != SkillTarget.Self && skill.statusEffect != StatusEffectType.None &&
                        skill.statusChance > 0f) context.hasStatusOption = true;
                }
            }
            context.hasRepertoireOption = repertoire.Count >= AudienceRequest.RepertoireTarget;
            AudienceRequestType type = AudienceRequest.Choose(context, UnityEngine.Random.value);
            if (type == AudienceRequestType.None) yield break;
            _audience.Begin(type, context);
            yield return _presenter.ShowMessage($"관객 주문 · {_audience.Title}\n{_audience.Description}");
        }

        private bool HasWeaknessOption(BattleUnit user, SkillData skill)
        {
            if (skill == null || !skill.DealsDamage) return false;
            ShadowElement element = skill.ResolveElement(user.Data.element);
            foreach (BattleUnit enemy in _enemyUnits)
                if (!enemy.IsFainted && ElementChart.GetMultiplier(element, enemy.Data.element) > 1f) return true;
            return false;
        }

        private IEnumerator AudienceAchieved()
        {
            if (_audience.IsMidBattleRequest) AddFp(BattleSide.Player, AudienceRequest.MidBattleFpReward);
            yield return _presenter.ShowMessage(_audience.AchievedMessage);
        }

        private void SetState(BattleState s)
        {
            if (State == s) return;
            State = s;
            OnStateChanged?.Invoke(s);
        }

        #endregion
    }
}
