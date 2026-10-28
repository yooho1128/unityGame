namespace ShadowTheater.Battle
{
    /// <summary>One shared healing-skill budget per combatant, per battle (not per skill).</summary>
    public sealed class HealingSkillBudget
    {
        public const int Limit = 3;
        public const int RestTurns = 2;
        public int Used { get; private set; }
        public int Remaining => Limit - Used;
        public int NextAvailableTurn { get; private set; } = 1;
        public int WaitTurns(int turn) => System.Math.Max(0, NextAvailableTurn - turn);
        public bool CanUse(int turn) => turn >= 1 && Remaining > 0 && turn >= NextAvailableTurn;
        public bool TryConsume(int turn)
        {
            if (!CanUse(turn)) return false;
            Used++;
            NextAvailableTurn = turn + RestTurns + 1;
            return true;
        }
    }
}
