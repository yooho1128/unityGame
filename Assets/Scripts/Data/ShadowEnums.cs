namespace ShadowTheater.Data
{
    /// <summary>11속성 (기존 저장 데이터의 숫자 값 보존)</summary>
    public enum ShadowElement
    {
        None = 0,   // 무속성
        Flame = 1,  // 붉은 불꽃
        Frost = 2,  // 푸른 서리
        Shade = 3,  // 어둠. 기존 저장된 0~3 값은 유지한다.
        Water = 4,
        Grass = 5,
        Light = 6,
        Earth = 7,
        Electric = 8,
        Wind = 9,
        Psychic = 10
    }

    /// <summary>역할군 (UI 표기, AI 성향 참고용)</summary>
    public enum ShadowRole
    {
        PhysicalDealer,
        MagicNuker,
        SpeedUtility,
        Tank,
        Support
    }

    /// <summary>기억 성장 연출/아트 제작 등급. 상위 등급일수록 형태별 실루엣 변화가 크다.</summary>
    public enum GrowthTier
    {
        Standard,
        Rare,
        RegionalBoss,
        Legendary
    }

    public enum MemoryStage
    {
        Echo = 0,       // 잔영
        Restored = 1,   // 기억 복원
        TrueName = 2    // 진명 각성
    }

    public enum AwakeningPath
    {
        None = 0,
        Salvation = 1, // 구원
        Grudge = 2     // 원한
    }

    public enum TrainingFocus
    {
        Balanced = 0,
        Power = 1,
        Guard = 2,
        Tempo = 3
    }

    public enum DamageType
    {
        None,       // 데미지 없음 (상태이상/회복 전용 스킬)
        Physical,   // 방어력 100% 적용
        Magical     // 방어력 50%만 적용
    }

    public enum SkillTarget
    {
        Enemy,
        Self
    }

    /// <summary>진명 필살기의 공용 UI 연출 테마.</summary>
    public enum UltimateFxStyle
    {
        None,
        FlameCrown,
        FrozenArchive,
        MoonBeast,
        PuppetThreads,
        AshBird,
        FrozenMask,
        MoonPetals,
        LivingScript,
        CosmicAudience
    }

    public enum StatusEffectType
    {
        None,
        Freeze,      // 행동 불가 (공격/스킬만 막힘, 교체·도구는 가능). 지속 턴 = 막힌 공격/스킬 + 교체 횟수
        Burn,        // 턴 종료 시 최대 HP 1/16 피해 + 공격력 x0.8
        Bleed,       // 턴 종료 시 최대 HP 1/24 피해, 턴마다 1→2→3배 누적
        AttackDown,  // 공격력 x0.7
        DefenseDown, // 방어력 x0.7
        SpeedDown    // 속도 x0.5
    }

}
