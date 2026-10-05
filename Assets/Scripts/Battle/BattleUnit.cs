using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Battle
{
    public class StatusState
    {
        public StatusEffectType type;
        public int remainingTurns;
    }

    /// <summary>
    /// 전투 중에만 존재하는 런타임 유닛. ShadowInstance(영구 데이터)를 감싸고
    /// 상태 이상, 버프 등 전투 한정 값을 관리한다. HP는 Instance에 바로 반영(전투 후 유지).
    /// </summary>
    public class BattleUnit
    {
        public ShadowInstance Instance { get; }
        public ShadowData Data => Instance.Data;
        public BattleSide Side { get; }

        public StatusState MajorStatus { get; private set; } // Freeze/Burn/Bleed 중 1개
        private readonly List<StatusState> _debuffs = new List<StatusState>();
        public IReadOnlyList<StatusState> Debuffs => _debuffs;

        public BattleUnit(ShadowInstance instance, BattleSide side)
        {
            Instance = instance;
            Side = side;
            Instance.EnsureHp();
        }

        // ── 스탯 ──
        public string Name => Instance.DisplayName;
        public int Level => Instance.level;
        public int MaxHp => Instance.MaxHp;
        public int Hp => Instance.currentHp;
        public float HpRatio => (float)Hp / MaxHp;
        public bool IsFainted => Hp <= 0;

        public int Atk => Mathf.RoundToInt(Instance.Atk * (HasDebuff(StatusEffectType.AttackDown) ? 0.7f : 1f));
        public int Def => Mathf.RoundToInt(Instance.Def * (HasDebuff(StatusEffectType.DefenseDown) ? 0.7f : 1f));
        public int Spd => Mathf.RoundToInt(Instance.Spd * (HasDebuff(StatusEffectType.SpeedDown) ? 0.5f : 1f));
        public float CritRate => Instance.CritRate;
        public float Evasion => Instance.Evasion;
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
        public bool HasAnyStatus => MajorStatus != null || _debuffs.Count > 0;

        /// <summary>교체 시 호출: 능력치 디버프만 해제, 주요 상태 이상은 유지</summary>
        public void ClearDebuffs() => _debuffs.Clear();

        public void ClearAllStatus()
        {
            MajorStatus = null;
            _debuffs.Clear();
        }

        /// <summary>턴 종료 시 지속 피해 계산 (Burn/Bleed). 실제 적용은 BattleManager가 연출과 함께 처리</summary>
        public int GetDotDamage()
        {
            if (MajorStatus == null) return 0;
            if (MajorStatus.type == StatusEffectType.Burn || MajorStatus.type == StatusEffectType.Bleed)
                return Mathf.Max(1, MaxHp / 16);
            return 0;
        }

        /// <summary>턴 종료 시 지속시간 감소. 해제된 상태 목록 반환</summary>
        public List<StatusEffectType> TickStatus()
        {
            var expired = new List<StatusEffectType>();
            if (MajorStatus != null && --MajorStatus.remainingTurns <= 0)
            {
                expired.Add(MajorStatus.type);
                MajorStatus = null;
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
