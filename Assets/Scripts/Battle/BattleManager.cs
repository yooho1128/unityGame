using System;
using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Data;
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
        public event Action<BattleUnit> OnUnitChanged;             // HP/상태 갱신 필요
        public event Action<BattleSide, BattleUnit> OnActiveChanged; // 교체/등장
        public event Action<BattleOutcome> OnBattleEnded;

        // ── 상태 ──
        public BattleState State { get; private set; } = BattleState.None;
        public BattleContext Context { get; private set; }
        public int Turn { get; private set; }
        public bool IsAuto { get; private set; }

        public IReadOnlyList<BattleUnit> PlayerUnits => _playerUnits;
        public IReadOnlyList<BattleUnit> EnemyUnits => _enemyUnits;
        public BattleUnit PlayerActive => _playerUnits[_playerIdx];
        public BattleUnit EnemyActive => _enemyUnits[_enemyIdx];

        private IBattlePresenter _presenter;
        private readonly List<BattleUnit> _playerUnits = new List<BattleUnit>();
        private readonly List<BattleUnit> _enemyUnits = new List<BattleUnit>();
        private int _playerIdx, _enemyIdx;
        private readonly int[] _fp = new int[2];

        private BattleAction _pendingAction;
        private int _pendingForcedSwitch = -1;
        private BattleOutcome _outcome;
        private float _captureBonus = 1f;
        private int _escapeAttempts;
        private Coroutine _routine;

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
        public void StartBattle(BattleContext context)
        {
            if (State != BattleState.None && !IsEnded)
            {
                Debug.LogWarning("[BattleManager] 이미 전투 중");
                return;
            }
            if (context?.playerParty == null || context.enemyParty == null ||
                context.playerParty.Count == 0 || context.enemyParty.Count == 0)
            {
                Debug.LogError("[BattleManager] BattleContext 파티 정보 누락");
                return;
            }

            Context = context;
            _playerUnits.Clear();
            _enemyUnits.Clear();
            foreach (var s in context.playerParty) _playerUnits.Add(new BattleUnit(s, BattleSide.Player));
            foreach (var s in context.enemyParty) _enemyUnits.Add(new BattleUnit(s, BattleSide.Enemy));

            _playerIdx = FindNextAlive(_playerUnits, -1);
            _enemyIdx = 0;
            if (_playerIdx < 0)
            {
                Debug.LogError("[BattleManager] 싸울 수 있는 그림자가 없음");
                return;
            }

            _fp[0] = _fp[1] = startFp;
            Turn = 0;
            IsAuto = context.autoBattle;
            _captureBonus = 1f;
            _escapeAttempts = 0;
            _pendingAction = null;
            _pendingForcedSwitch = -1;
            _outcome = new BattleOutcome { encounterId = context.encounterId };

            _presenter.SetSpeed(context.timeScale);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(BattleRoutine());
        }

        /// <summary>UI 버튼 → 플레이어 행동 제출. 유효하지 않으면 false</summary>
        public bool SubmitAction(BattleAction action)
        {
            if (action == null || action.side != BattleSide.Player) return false;

            // 기절로 인한 강제 교체
            if (State == BattleState.ForcedSwitch)
            {
                if (action.type != ActionType.Switch || !CanSwitchTo(action.switchIndex)) return false;
                _pendingForcedSwitch = action.switchIndex;
                return true;
            }

            if (State != BattleState.WaitingForInput) return false;
            if (!Validate(action, out string reason))
            {
                Debug.Log($"[BattleManager] 행동 불가: {reason}");
                return false;
            }
            _pendingAction = action;
            return true;
        }

        public void SetAuto(bool on) => IsAuto = on;

        public void SetSpeed(float timeScale)
        {
            if (Context != null) Context.timeScale = timeScale;
            _presenter.SetSpeed(timeScale);
        }

        public int GetFp(BattleSide side) => _fp[(int)side];
        public int MaxFp => maxFp;

        public bool CanUseSkill(SkillData skill) =>
            skill != null && GetFp(BattleSide.Player) >= skill.fpCost;

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

            while (!IsEnded)
            {
                Turn++;
                AddFp(BattleSide.Player, fpPerTurn);
                AddFp(BattleSide.Enemy, fpPerTurn);

                // 1) 플레이어 입력
                _pendingAction = null;
                SetState(BattleState.WaitingForInput);
                yield return new WaitUntil(() => _pendingAction != null || IsAuto);
                var playerAction = _pendingAction
                    ?? BattleAI.Choose(PlayerActive, EnemyActive, GetFp(BattleSide.Player));
                playerAction.actor = PlayerActive;

                // 2) 적 선택
                var enemyAction = BattleAI.Choose(EnemyActive, PlayerActive, GetFp(BattleSide.Enemy));
                enemyAction.actor = EnemyActive;

                // 3) 처리
                SetState(BattleState.ResolvingTurn);
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
            }

            yield return _presenter.PlayResult(_outcome);
            OnBattleEnded?.Invoke(_outcome);
            _routine = null;
        }

        private List<BattleAction> OrderActions(BattleAction p, BattleAction e)
        {
            bool playerFirst;
            if (p.Priority != e.Priority) playerFirst = p.Priority > e.Priority;
            else if (PlayerActive.Spd != EnemyActive.Spd) playerFirst = PlayerActive.Spd > EnemyActive.Spd;
            else playerFirst = UnityEngine.Random.value < 0.5f;

            return playerFirst ? new List<BattleAction> { p, e } : new List<BattleAction> { e, p };
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
                yield break;
            }

            // FP 처리 (AI가 잘못 골랐을 때 대비해 한 번 더 체크)
            if (GetFp(a.side) < skill.fpCost)
            {
                skill = user.BasicAttack;
                if (skill == null) yield break;
            }
            AddFp(a.side, skill.fpGain - skill.fpCost);

            // 자기 대상 (회복/버프)
            if (skill.target == SkillTarget.Self)
            {
                int healed = user.Heal(Mathf.RoundToInt(user.MaxHp * skill.healRatio));
                yield return _presenter.PlaySkill(user, user, skill, new HitResult { elementMultiplier = 1f });
                if (healed > 0) yield return _presenter.PlayHeal(user, healed);
                OnUnitChanged?.Invoke(user);
                yield break;
            }

            // 적 대상
            var hit = DamageCalculator.Calculate(user, opponent, skill);
            if (!hit.missed && hit.damage > 0)
                hit.damage = opponent.TakeDamage(hit.damage);

            yield return _presenter.PlaySkill(user, opponent, skill, hit);
            OnUnitChanged?.Invoke(opponent);

            if (!hit.missed && hit.statusApplied && !opponent.IsFainted &&
                opponent.ApplyStatus(skill.statusEffect, skill.statusDuration))
            {
                yield return _presenter.PlayStatusApplied(opponent, skill.statusEffect);
                OnUnitChanged?.Invoke(opponent);
            }
        }

        private IEnumerator DoSwitch(BattleSide side, int index)
        {
            var units = side == BattleSide.Player ? _playerUnits : _enemyUnits;
            if (index < 0 || index >= units.Count || units[index].IsFainted) yield break;

            var prev = Active(side);
            if (!prev.IsFainted)
            {
                prev.ClearDebuffs();
                yield return _presenter.PlayWithdraw(prev);
            }

            if (side == BattleSide.Player) _playerIdx = index; else _enemyIdx = index;

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

            float chance = DamageCalculator.CaptureChance(target, _captureBonus);
            bool success = UnityEngine.Random.value < chance;
            yield return _presenter.PlayRecordAttempt(target, success);

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
            if (item == null) yield break;

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
                    yield return _presenter.PlayHeal(target, healed);
                    break;
                }
                case ItemEffectType.HealRatio:
                {
                    int healed = target.Heal(Mathf.RoundToInt(target.MaxHp * item.value));
                    yield return _presenter.PlayHeal(target, healed);
                    break;
                }
                case ItemEffectType.CureStatus:
                    target.ClearAllStatus();
                    break;
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
                    unit.TakeDamage(dot);
                    yield return _presenter.PlayStatusDamage(unit, status, dot);
                }

                var expired = unit.TickStatus();
                foreach (var s in expired)
                    yield return _presenter.ShowMessage($"{unit.Name}의 [{s}] 상태가 풀렸다.");

                OnUnitChanged?.Invoke(unit);
            }
        }

        private IEnumerator HandleFaints()
        {
            if (IsEnded) yield break;

            // ── 적 기절 ──
            if (EnemyActive.IsFainted)
            {
                var defeated = EnemyActive;
                yield return _presenter.PlayFaint(defeated);
                yield return GrantExp(defeated);

                int next = FindNextAlive(_enemyUnits, _enemyIdx);
                if (next < 0)
                {
                    foreach (var e in _enemyUnits) _outcome.goldGained += e.Data.goldReward;
                    Finish(BattleResult.Victory, BattleState.Victory);
                    yield break;
                }

                yield return _presenter.ShowMessage($"상대가 다음 그림자를 무대에 올린다!");
                yield return DoSwitch(BattleSide.Enemy, next);
            }

            // ── 아군 기절 ──
            if (PlayerActive.IsFainted)
            {
                yield return _presenter.PlayFaint(PlayerActive);

                int next = FindNextAlive(_playerUnits, -1);
                if (next < 0)
                {
                    Finish(BattleResult.Defeat, BattleState.Defeat);
                    yield break;
                }

                if (IsAuto)
                {
                    _pendingForcedSwitch = next;
                }
                else
                {
                    _pendingForcedSwitch = -1;
                    SetState(BattleState.ForcedSwitch);
                    yield return new WaitUntil(() => _pendingForcedSwitch >= 0 || IsAuto);
                    if (_pendingForcedSwitch < 0) _pendingForcedSwitch = next;
                    SetState(BattleState.ResolvingTurn);
                }

                yield return DoSwitch(BattleSide.Player, _pendingForcedSwitch);
                _pendingForcedSwitch = -1;
            }
        }

        private IEnumerator GrantExp(BattleUnit defeated)
        {
            int exp = DamageCalculator.ExpReward(defeated);
            _outcome.expGained += exp;

            var receiver = PlayerActive.IsFainted ? null : PlayerActive;
            if (receiver == null) yield break;

            int ups = receiver.Instance.AddExp(exp);
            yield return _presenter.ShowMessage($"{receiver.Name}은(는) {exp} 경험치를 얻었다.");
            if (ups > 0)
            {
                if (!_outcome.leveledUpInstanceIds.Contains(receiver.Instance.instanceId))
                    _outcome.leveledUpInstanceIds.Add(receiver.Instance.instanceId);
                yield return _presenter.ShowMessage($"{receiver.Name}의 레벨이 {receiver.Level}(으)로 올랐다!");
                OnUnitChanged?.Invoke(receiver);
            }
        }

        #endregion

        // ────────────────────────────────────────────
        #region Helpers

        private BattleUnit Active(BattleSide side) => side == BattleSide.Player ? PlayerActive : EnemyActive;
        private BattleUnit Opponent(BattleSide side) => side == BattleSide.Player ? EnemyActive : PlayerActive;

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

        private bool Validate(BattleAction a, out string reason)
        {
            reason = null;
            switch (a.type)
            {
                case ActionType.Attack:
                    if (a.skill == null) a.skill = PlayerActive.BasicAttack;
                    if (a.skill == null) { reason = "기본 공격 미설정"; return false; }
                    return true;
                case ActionType.Skill:
                    if (a.skill == null || !PlayerActive.Skills.Contains(a.skill)) { reason = "보유하지 않은 스킬"; return false; }
                    if (!CanUseSkill(a.skill)) { reason = "FP 부족"; return false; }
                    return true;
                case ActionType.Switch:
                    if (!CanSwitchTo(a.switchIndex)) { reason = "교체 불가"; return false; }
                    return true;
                case ActionType.Record:
                    if (!Context.CanCapture) { reason = "기록 불가 전투"; return false; }
                    return true;
                case ActionType.Item:
                    if (a.item == null || !a.item.usableInBattle) { reason = "사용 불가 도구"; return false; }
                    if (Context.inventory != null &&
                        (!Context.inventory.TryGetValue(a.item, out int c) || c <= 0)) { reason = "도구 없음"; return false; }
                    return true;
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
            SetState(state);
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
