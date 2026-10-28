using ShadowTheater.Data;

namespace ShadowTheater.Battle
{
    /// <summary>Growing a full party must not dilute experience or punish a fainted participant.</summary>
    public static class PartyExperiencePolicy
    {
        public static int Amount(int reward, bool participated) =>
            reward <= 0 ? 0 : participated ? reward : System.Math.Max(1, reward / 2);

        public static int Apply(ShadowInstance shadow, int amount)
        {
            if (shadow == null || amount <= 0 || shadow.IsMaxLevel) return 0;
            bool fainted = shadow.IsFainted;
            int ups = shadow.AddExp(amount);
            // AddExp normally raises current HP on level-up; experience must not revive a KO mid-battle.
            if (fainted) shadow.currentHp = 0;
            return ups;
        }
    }
}
