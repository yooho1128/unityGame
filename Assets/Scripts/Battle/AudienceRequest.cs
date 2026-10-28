using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.Battle
{
    /// <summary>전투마다 객석이 던지는 작은 주문. 같은 그림자와의 반복 전투에도 매번 다른 목표를 준다.</summary>
    public enum AudienceRequestType
    {
        None,
        WeaknessFinale, // 약점 속성 공격으로 상대를 쓰러뜨린다
        SwiftCurtain,   // 제한 턴 안에 공연을 마친다
        NoUnderstudy,   // 아군이 한 명도 쓰러지지 않는다
        EncoreCall,     // 앙코르를 한 번 이상 터뜨린다
        TensionScene,   // 상대에게 상태 이상을 건다
        CriticalLine,   // 치명타를 한 번 이상 성공한다
        Repertoire,     // 서로 다른 기술을 두 종류 이상 선보인다
        RecordScene     // 상대를 각본집에 기록한다
    }

    public enum AudienceRequestState { Pending, Achieved, Failed }

    /// <summary>주문 선택에 필요한 전투 시작 시점 정보. BattleManager가 채운다.</summary>
    public struct AudienceRequestContext
    {
        public BattleMode mode;
        public int enemyCount;
        public bool canRecord;
        public bool hasWeaknessOption;
        public bool hasStatusOption;
        public bool hasRepertoireOption;
    }

    /// <summary>
    /// 관객 주문 진행 상태. 로직만 담당하며 BattleManager가 전투 사건을 알려 준다.
    /// Notify* 메서드는 이번 호출로 상태가 바뀌었을 때 true를 반환해 연출 메시지를 띄울 수 있게 한다.
    /// 보상은 승리 또는 기록으로 공연을 마쳤을 때 달성 상태인 경우에만 지급된다.
    /// </summary>
    public sealed class AudienceRequest
    {
        public const int RepertoireTarget = 2;
        public const int MidBattleFpReward = 1;
        public const string JointSkillKey = "__joint__";

        private readonly HashSet<string> _skills = new HashSet<string>(StringComparer.Ordinal);

        public AudienceRequestType Type { get; private set; }
        public AudienceRequestState State { get; private set; }
        public int TurnLimit { get; private set; }
        public int RepertoireCount => _skills.Count;

        public bool IsActive => Type != AudienceRequestType.None;
        public bool IsAchieved => IsActive && State == AudienceRequestState.Achieved;
        public bool IsFailed => IsActive && State == AudienceRequestState.Failed;
        public bool IsPending => IsActive && State == AudienceRequestState.Pending;

        public void Reset()
        {
            Type = AudienceRequestType.None;
            State = AudienceRequestState.Pending;
            TurnLimit = 0;
            _skills.Clear();
        }

        public void Begin(AudienceRequestType type, AudienceRequestContext context)
        {
            Reset();
            Type = type;
            TurnLimit = SwiftTurnLimit(context.enemyCount, context.mode);
        }

        public static int SwiftTurnLimit(int enemyCount, BattleMode mode) =>
            Math.Max(1, enemyCount) * (mode == BattleMode.Boss ? 5 : 3);

        /// <summary>조건을 만족할 수 있는 주문만 가중치로 고른다. roll은 [0,1) 난수.</summary>
        public static AudienceRequestType Choose(AudienceRequestContext context, float roll)
        {
            var pool = new List<KeyValuePair<AudienceRequestType, float>>();
            void Add(AudienceRequestType type, float weight)
            {
                if (weight > 0f) pool.Add(new KeyValuePair<AudienceRequestType, float>(type, weight));
            }

            bool boss = context.mode == BattleMode.Boss;
            Add(AudienceRequestType.SwiftCurtain, 3f);
            Add(AudienceRequestType.NoUnderstudy, boss ? 3f : 1.5f);
            Add(AudienceRequestType.EncoreCall, 3f);
            Add(AudienceRequestType.CriticalLine, 2f);
            if (context.hasWeaknessOption) Add(AudienceRequestType.WeaknessFinale, 3f);
            if (context.hasStatusOption) Add(AudienceRequestType.TensionScene, 2f);
            if (context.hasRepertoireOption) Add(AudienceRequestType.Repertoire, 2f);
            if (context.canRecord) Add(AudienceRequestType.RecordScene, 2.5f);

            float total = 0f;
            foreach (var entry in pool) total += entry.Value;
            if (total <= 0f) return AudienceRequestType.None;
            float cursor = Mathf.Clamp01(roll) * total;
            foreach (var entry in pool)
            {
                cursor -= entry.Value;
                if (cursor < 0f) return entry.Key;
            }
            return pool[pool.Count - 1].Key;
        }

        // ── 전투 사건 ──

        /// <summary>플레이어 측 공격 결과. defeatedTarget은 이 공격으로 상대가 쓰러졌는지.</summary>
        public bool NotifyPlayerHit(HitResult hit, bool defeatedTarget)
        {
            if (!IsPending || hit.missed || hit.damage <= 0) return false;
            switch (Type)
            {
                case AudienceRequestType.WeaknessFinale:
                    return defeatedTarget && hit.IsEffective && Achieve();
                case AudienceRequestType.CriticalLine:
                    return hit.critical && Achieve();
            }
            return false;
        }

        /// <summary>플레이어가 기본 공격이 아닌 기술(합동기 포함)을 실제로 사용했을 때.</summary>
        public bool NotifyPlayerSkill(string skillKey)
        {
            if (!IsPending || Type != AudienceRequestType.Repertoire || string.IsNullOrEmpty(skillKey)) return false;
            _skills.Add(skillKey);
            return _skills.Count >= RepertoireTarget && Achieve();
        }

        public bool NotifyStatusInflicted() =>
            IsPending && Type == AudienceRequestType.TensionScene && Achieve();

        public bool NotifyEncore() =>
            IsPending && Type == AudienceRequestType.EncoreCall && Achieve();

        public bool NotifyPlayerFaint() =>
            IsPending && Type == AudienceRequestType.NoUnderstudy && Fail();

        /// <summary>턴 종료 시 호출. 제한 턴을 다 쓰고도 공연이 끝나지 않았으면 실패.</summary>
        public bool NotifyTurnEnded(int turn) =>
            IsPending && Type == AudienceRequestType.SwiftCurtain && turn >= TurnLimit && Fail();

        /// <summary>전투 종료 판정. 보상을 지급해야 하면 true.</summary>
        public bool Conclude(BattleResult result)
        {
            if (!IsActive) return false;
            bool finishedShow = result == BattleResult.Victory || result == BattleResult.Captured;
            if (IsPending)
            {
                switch (Type)
                {
                    case AudienceRequestType.SwiftCurtain:
                    case AudienceRequestType.NoUnderstudy:
                        if (finishedShow) Achieve(); else Fail();
                        break;
                    case AudienceRequestType.RecordScene:
                        if (result == BattleResult.Captured) Achieve(); else Fail();
                        break;
                    default:
                        Fail();
                        break;
                }
            }
            return finishedShow && IsAchieved;
        }

        /// <summary>전투 도중 달성하는 주문인지. 이 경우 관객의 환호로 즉시 FP를 돌려준다.</summary>
        public bool IsMidBattleRequest =>
            Type != AudienceRequestType.SwiftCurtain && Type != AudienceRequestType.NoUnderstudy &&
            Type != AudienceRequestType.RecordScene && Type != AudienceRequestType.None;

        private bool Achieve()
        {
            State = AudienceRequestState.Achieved;
            return true;
        }

        private bool Fail()
        {
            State = AudienceRequestState.Failed;
            return true;
        }

        // ── 보상 ──

        /// <summary>상대 기본 금화의 절반(최소 15). 보스 무대는 두 배.</summary>
        public static int RewardGold(int enemyBaseGold, BattleMode mode)
        {
            long reward = Math.Max(15L, (Math.Max(0L, enemyBaseGold) + 1L) / 2L);
            if (mode == BattleMode.Boss) reward *= 2L;
            return (int)Math.Min(reward, int.MaxValue);
        }

        /// <summary>누적 달성 10회마다 기억 조각 3개를 선물한다.</summary>
        public const int MilestoneInterval = 10;
        public const int MilestoneFragments = 3;
        public static bool IsMilestone(int completedTotal) =>
            completedTotal > 0 && completedTotal % MilestoneInterval == 0;

        // ── 표시 문구 ──

        public string Title => TitleOf(Type);

        public static string TitleOf(AudienceRequestType type)
        {
            switch (type)
            {
                case AudienceRequestType.WeaknessFinale: return "약점 피날레";
                case AudienceRequestType.SwiftCurtain: return "빠른 막내림";
                case AudienceRequestType.NoUnderstudy: return "대역 없는 무대";
                case AudienceRequestType.EncoreCall: return "앙코르 요청";
                case AudienceRequestType.TensionScene: return "긴장의 순간";
                case AudienceRequestType.CriticalLine: return "결정적 대사";
                case AudienceRequestType.Repertoire: return "다채로운 레퍼토리";
                case AudienceRequestType.RecordScene: return "기록의 순간";
                default: return string.Empty;
            }
        }

        public string Description
        {
            get
            {
                switch (Type)
                {
                    case AudienceRequestType.WeaknessFinale: return "약점 속성 공격으로 상대를 쓰러뜨려 주세요.";
                    case AudienceRequestType.SwiftCurtain: return $"{TurnLimit}턴 안에 공연을 끝내 주세요.";
                    case AudienceRequestType.NoUnderstudy: return "아군이 한 명도 쓰러지지 않고 이겨 주세요.";
                    case AudienceRequestType.EncoreCall: return "연속 명중으로 앙코르를 터뜨려 주세요.";
                    case AudienceRequestType.TensionScene: return "상대에게 상태 이상을 걸어 주세요.";
                    case AudienceRequestType.CriticalLine: return "치명타를 한 번 성공시켜 주세요.";
                    case AudienceRequestType.Repertoire: return $"서로 다른 기술을 {RepertoireTarget}종류 선보여 주세요.";
                    case AudienceRequestType.RecordScene: return "이 그림자를 각본집에 기록해 주세요.";
                    default: return string.Empty;
                }
            }
        }

        /// <summary>상단 HUD용 짧은 진행 표시.</summary>
        public string HudLabel
        {
            get
            {
                if (!IsActive) return string.Empty;
                string mark = IsAchieved ? " · 달성" : IsFailed ? " · 실패" : string.Empty;
                string progress = Type == AudienceRequestType.Repertoire && IsPending
                    ? $" {_skills.Count}/{RepertoireTarget}"
                    : Type == AudienceRequestType.SwiftCurtain && IsPending ? $" ~{TurnLimit}턴" : string.Empty;
                return $"주문 · {Title}{progress}{mark}";
            }
        }

        public string AchievedMessage => IsMidBattleRequest
            ? $"관객 주문 [{Title}] 달성! 객석의 환호에 FP +{MidBattleFpReward}"
            : $"관객 주문 [{Title}] 달성!";

        public string FailedMessage => $"객석에 아쉬운 한숨이 흐른다… 관객 주문 [{Title}] 실패";
    }
}
