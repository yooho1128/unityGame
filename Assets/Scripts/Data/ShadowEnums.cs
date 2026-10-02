namespace ShadowTheater.Data
{
    /// <summary>그림자 속성 (스타팅 3종 기준)</summary>
    public enum ShadowElement
    {
        None = 0,   // 무속성
        Flame = 1,  // 붉은 불꽃
        Frost = 2,  // 푸른 서리
        Shade = 3   // 자줏빛 그림자
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

    public enum StatusEffectType
    {
        None,
        Freeze,      // 행동 불가 (공격/스킬만 막힘, 교체·도구는 가능)
        Burn,        // 턴 종료 시 최대 HP 1/16 피해
        Bleed,       // 턴 종료 시 최대 HP 1/16 피해
        AttackDown,  // 공격력 x0.7
        DefenseDown, // 방어력 x0.7
        SpeedDown    // 속도 x0.5
    }

    /// <summary>
    /// 속성 상성표. 기획 확정 전 임시값: Flame > Frost > Shade > Flame
    /// </summary>
    public static class ElementChart
    {
        public const float Strong = 1.5f;
        public const float Weak = 0.75f;
        public const float Neutral = 1f;

        public static float GetMultiplier(ShadowElement attack, ShadowElement defend)
        {
            if (attack == ShadowElement.None || defend == ShadowElement.None) return Neutral;
            if (Beats(attack, defend)) return Strong;
            if (Beats(defend, attack)) return Weak;
            return Neutral;
        }

        private static bool Beats(ShadowElement a, ShadowElement b)
        {
            return (a == ShadowElement.Flame && b == ShadowElement.Frost)
                || (a == ShadowElement.Frost && b == ShadowElement.Shade)
                || (a == ShadowElement.Shade && b == ShadowElement.Flame);
        }
    }
}
