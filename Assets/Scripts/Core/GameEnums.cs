namespace ShadowTheater.Core
{
    public enum ShadowElement
    {
        None,
        Flame,
        Frost,
        Shadow
    }

    public enum SkillType
    {
        Attack,
        Heal,
        Buff,
        Debuff
    }

    public enum SkillTarget
    {
        Self,
        Opponent
    }

    public enum StatusEffectType
    {
        None,
        Burn,
        Freeze,
        Poison,
        Stun
    }

    public enum BattleState
    {
        None,
        Starting,
        PlayerTurn,
        EnemyTurn,
        Processing,
        Victory,
        Defeat,
        CaptureSuccess,
        Escaped
    }

    public enum BattleActionType
    {
        Attack,
        Skill,
        Switch,
        Capture,
        Item
    }
}
