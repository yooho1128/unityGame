using System.Collections.Generic;
using ShadowTheater.Data;

namespace ShadowTheater.Battle
{
    public enum BattleSide { Player, Enemy }

    public enum BattleMode
    {
        Wild,       // 필드 심볼 인카운터 - 포획 가능
        Rival,      // 라이벌/검열단 - 포획 불가, 도주 불가
        Boss        // 지역 보스(비극의 연극) - 승리 시 정화 이벤트
    }

    public enum BattleState
    {
        None,
        Intro,
        WaitingForInput,
        ResolvingTurn,
        ForcedSwitch,   // 아군 기절 → 교체 선택 대기
        Victory,
        Defeat,
        Captured,
        Escaped
    }

    public enum BattleResult { None, Victory, Defeat, Captured, Escaped }

    /// <summary>명세서 5종 + 도주(야생 전투 전용)</summary>
    public enum ActionType { Attack, Skill, Switch, Record, Item, Escape }

    /// <summary>한 턴에 한 진영이 선택한 행동</summary>
    public class BattleAction
    {
        public BattleSide side;
        public ActionType type;
        public SkillData skill;       // Attack/Skill
        public int switchIndex = -1;  // Switch: 파티 인덱스
        public ItemData item;         // Item
        public BattleUnit actor;      // 선택 시점의 행동 유닛 (BattleManager가 채움. 도중 기절/교체 시 행동 취소 판정용)

        /// <summary>
        /// 처리 우선순위. 높을수록 먼저. 교체/도구/기록은 공격보다 먼저 (포켓몬 방식)
        /// </summary>
        public int Priority
        {
            get
            {
                switch (type)
                {
                    case ActionType.Escape: return 4;
                    case ActionType.Switch: return 3;
                    case ActionType.Item:   return 2;
                    case ActionType.Record: return 2;
                    default:                return 0;
                }
            }
        }

        public static BattleAction Attack(BattleSide side, SkillData basic) =>
            new BattleAction { side = side, type = ActionType.Attack, skill = basic };
        public static BattleAction UseSkill(BattleSide side, SkillData skill) =>
            new BattleAction { side = side, type = ActionType.Skill, skill = skill };
        public static BattleAction Switch(BattleSide side, int index) =>
            new BattleAction { side = side, type = ActionType.Switch, switchIndex = index };
        public static BattleAction Record() =>
            new BattleAction { side = BattleSide.Player, type = ActionType.Record };
        public static BattleAction UseItem(BattleSide side, ItemData item) =>
            new BattleAction { side = side, type = ActionType.Item, item = item };
        public static BattleAction Escape() =>
            new BattleAction { side = BattleSide.Player, type = ActionType.Escape };
    }

    /// <summary>
    /// 전투 진입 시 주입되는 정보. EncounterTrigger가 만들어서 BattleManager.StartBattle()에 전달.
    /// </summary>
    public class BattleContext
    {
        public BattleMode mode = BattleMode.Wild;
        public List<ShadowInstance> playerParty;
        public List<ShadowInstance> enemyParty;   // 야생은 1마리, 라이벌은 여러 마리
        public Dictionary<ItemData, int> inventory; // 전투 중 사용할 도구 (null 가능)
        public string encounterId;                 // 퀘스트 플래그/심볼 제거용
        public bool autoBattle;
        public float timeScale = 1f;               // 접근성/연출 배속 옵션

        public bool CanCapture => mode == BattleMode.Wild;
        public bool CanEscape => mode == BattleMode.Wild;
    }

    /// <summary>전투 결과 → 필드/세이브 쪽으로 넘겨줌</summary>
    public class BattleOutcome
    {
        public BattleResult result;
        public string encounterId;
        public ShadowInstance capturedShadow;
        public int expGained;
        public int goldGained;
        public List<string> leveledUpInstanceIds = new List<string>();
    }

    /// <summary>데미지 계산 결과 (연출용)</summary>
    public struct HitResult
    {
        public bool missed;
        public bool critical;
        public float elementMultiplier;
        public int damage;
        public bool statusApplied;
        public StatusEffectType status;

        public bool IsEffective => elementMultiplier > 1f;
        public bool IsResisted => elementMultiplier < 1f;
    }
}
