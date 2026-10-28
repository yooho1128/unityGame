using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Story;
using UnityEngine;

namespace ShadowTheater.Battle
{
    public class StatusState
    {
        public StatusEffectType type;
        public int remainingTurns;
        public int elapsedTurns; // 부여 후 지난 턴 수 (출혈 누적 피해 계산용)
    }

    /// <summary>
    /// 전투 중에만 존재하는 런타임 유닛. ShadowInstance(영구 데이터)를 감싸고
    /// 상태 이상, 버프 등 전투 한정 값을 관리한다. HP는 Instance에 바로 반영(전투 후 유지).
    /// </summary>
    public class BattleUnit
    {
        public const float BurnAttackFactor = 0.8f; // 화상: 지속 피해 + 공격력 감소
        public const int BleedMaxStacks = 3;        // 출혈: 턴마다 피해가 1→2→3배로 누적

        public ShadowInstance Instance { get; }
        public ShadowData Data => Instance.Data;
        public BattleSide Side { get; }
        public CompanionPassiveSnapshot CompanionPassive { get; }

        public StatusState MajorStatus { get; private set; } // Freeze/Burn/Bleed 중 1개
        private readonly List<StatusState> _debuffs = new List<StatusState>();
        public IReadOnlyList<StatusState> Debuffs => _debuffs;

        public BattleUnit(ShadowInstance instance, BattleSide side,
            CompanionPassiveSnapshot companionPassive = default)
        {
            Instance = instance;
            Side = side;
            CompanionPassive = string.IsNullOrEmpty(companionPassive.shadowId)
                ? CompanionPassiveSnapshot.None : companionPassive;
            Instance.EnsureHp();
        }

        // ── 스탯 ──
        public string Name => Instance.DisplayName;
        public int Level => Instance.level;
        public int MaxHp => Instance.MaxHp;
        public int Hp => Instance.currentHp;
        public float HpRatio => (float)Hp / MaxHp;
        public bool IsFainted => Hp <= 0;

        public int Atk => Mathf.RoundToInt(Instance.Atk * CompanionPassive.attackMultiplier
            * (HasDebuff(StatusEffectType.AttackDown) ? 0.7f : 1f)
            * (HasStatus(StatusEffectType.Burn) ? BurnAttackFactor : 1f));
        public int Def => Mathf.RoundToInt(Instance.Def * CompanionPassive.defenseMultiplier * (HasDebuff(StatusEffectType.DefenseDown) ? 0.7f : 1f));
        public int Spd => Mathf.RoundToInt(Instance.Spd * CompanionPassive.speedMultiplier * (HasDebuff(StatusEffectType.SpeedDown) ? 0.5f : 1f));
        public float CritRate => Mathf.Clamp01(Instance.CritRate + CompanionPassive.criticalBonus);
        public float Evasion => Mathf.Clamp01(Instance.Evasion + CompanionPassive.evasionBonus);
        public SkillData BasicAttack => Data.basicAttack;
        public List<SkillData> Skills => Instance.GetAvailableSkills();

        public bool CanAct => MajorStatus == null || MajorStatus.type != StatusEffectType.Freeze;

        // ── HP ──
        public int TakeDamage(int amount)
        {
            int dealt = Mathf.Clamp(amount, 0, Hp);
            Instance.currentHp -= dealt;
            return dealt;
        }

        public int Heal(int amount)
        {
            int healed = Mathf.Clamp(amount, 0, MaxHp - Hp);
            Instance.currentHp += healed;
            return healed;
        }

        // ── 상태 이상 ──
        public static bool IsMajor(StatusEffectType t) =>
            t == StatusEffectType.Freeze || t == StatusEffectType.Burn || t == StatusEffectType.Bleed;

        /// <summary>상태 이상 부여. 이미 주요 상태 이상이 있으면 실패</summary>
        public bool ApplyStatus(StatusEffectType type, int duration)
        {
            if (type == StatusEffectType.None || IsFainted) return false;

            if (IsMajor(type))
            {
                if (MajorStatus != null) return false;
                MajorStatus = new StatusState { type = type, remainingTurns = duration };
                return true;
            }

            var exist = _debuffs.Find(d => d.type == type);
            if (exist != null) { exist.remainingTurns = Mathf.Max(exist.remainingTurns, duration); return true; }
            _debuffs.Add(new StatusState { type = type, remainingTurns = duration });
            return true;
        }

        public bool HasDebuff(StatusEffectType type) => _debuffs.Exists(d => d.type == type);
        public bool HasStatus(StatusEffectType type) =>
            type != StatusEffectType.None &&
            ((MajorStatus != null && MajorStatus.type == type) || HasDebuff(type));
        public bool HasAnyStatus => MajorStatus != null || _debuffs.Count > 0;
        public HealingSkillBudget HealingSkills { get; } = new HealingSkillBudget();
        public static bool IsHealingSkill(SkillData skill) =>
            skill != null && skill.target == SkillTarget.Self && skill.healRatio > 0f;
        public bool CanUseHealingSkill(SkillData skill, int turn) =>
            !IsHealingSkill(skill) || (!IsFainted && Hp < MaxHp && HealingSkills.CanUse(turn));

        public int UseHealingSkill(SkillData skill, int turn)
        {
            if (!IsHealingSkill(skill) || !CanUseHealingSkill(skill, turn) || !HealingSkills.TryConsume(turn)) return 0;
            return Heal(Mathf.Max(1, Mathf.RoundToInt(MaxHp * Mathf.Clamp01(skill.healRatio))));
        }

        public bool NeedsRestoration => !IsFainted && (Hp < MaxHp || HasAnyStatus);

        /// <summary>복합 회복은 기절을 해제하지 않는다. HP가 가득해도 상태 정화는 가능하다.</summary>
        public int RestoreAndCleanse(float ratio)
        {
            if (!NeedsRestoration || ratio <= 0f) return 0;
            int restored = Heal(Mathf.Max(1, Mathf.RoundToInt(MaxHp * Mathf.Clamp01(ratio))));
            ClearAllStatus();
            return restored;
        }

        /// <summary>교체 시 호출: 능력치 디버프만 해제, 주요 상태 이상은 유지</summary>
        public void ClearDebuffs() => _debuffs.Clear();

        public void ClearAllStatus()
        {
            MajorStatus = null;
            _debuffs.Clear();
        }

        /// <summary>출혈의 현재 누적 단계(다음 턴 종료 피해 배수). 1~BleedMaxStacks</summary>
        public static int BleedStacks(StatusState bleed) =>
            bleed == null ? 0 : Mathf.Min(BleedMaxStacks, bleed.elapsedTurns + 1);

        /// <summary>턴 종료 시 지속 피해 계산 (Burn/Bleed). 실제 적용은 BattleManager가 연출과 함께 처리</summary>
        public int GetDotDamage()
        {
            if (MajorStatus == null) return 0;
            switch (MajorStatus.type)
            {
                case StatusEffectType.Burn:
                    return Mathf.Max(1, MaxHp / 16);
                case StatusEffectType.Bleed:
                    return Mathf.Max(1, MaxHp / 24) * BleedStacks(MajorStatus);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 빙결로 행동이 막힐 때마다 호출. 빙결의 지속 턴은 턴 종료가 아니라 "막힌 행동 횟수"로 센다.
        /// (턴 종료에 세면 느린 쪽이 건 빙결이 상대의 행동을 한 번도 막지 못하고 풀린다.)
        /// 이번 호출로 빙결이 풀렸으면 true
        /// </summary>
        public bool ConsumeFreezeTurn()
        {
            if (MajorStatus == null || MajorStatus.type != StatusEffectType.Freeze) return false;
            if (--MajorStatus.remainingTurns > 0) return false;
            MajorStatus = null;
            return true;
        }

        /// <summary>턴 종료 시 지속시간 감소(빙결 제외, ConsumeFreezeTurn 참고). 해제된 상태 목록 반환</summary>
        public List<StatusEffectType> TickStatus()
        {
            var expired = new List<StatusEffectType>();
            if (MajorStatus != null && MajorStatus.type != StatusEffectType.Freeze)
            {
                MajorStatus.elapsedTurns++;
                if (--MajorStatus.remainingTurns <= 0)
                {
                    expired.Add(MajorStatus.type);
                    MajorStatus = null;
                }
            }
            for (int i = _debuffs.Count - 1; i >= 0; i--)
            {
                if (--_debuffs[i].remainingTurns <= 0)
                {
                    expired.Add(_debuffs[i].type);
                    _debuffs.RemoveAt(i);
                }
            }
            return expired;
        }
    }
}
