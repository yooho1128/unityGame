namespace ShadowTheater.Battle
{
    /// <summary>숨은 지역 보스 규칙의 결정론적 판정. 에디터 회귀 검사에서도 같은 규칙을 사용한다.</summary>
    public static class BattleGimmickRules
    {
        public static bool IsBellWarningTurn(BattleGimmick gimmick, int turn) =>
            gimmick == BattleGimmick.BellToll && turn > 0 && turn % 3 == 2;

        public static bool IsBellTollTurn(BattleGimmick gimmick, int turn) =>
            gimmick == BattleGimmick.BellToll && turn > 0 && turn % 3 == 0;

        public static bool ReversesInitiative(BattleGimmick gimmick, int turn) =>
            gimmick == BattleGimmick.MoonReversal && turn > 0 && turn % 2 == 0;

        public static bool IsEchoRepeat(BattleGimmick gimmick, string previousSkillId, string currentSkillId) =>
            gimmick == BattleGimmick.EchoSeal && !string.IsNullOrEmpty(previousSkillId) &&
            previousSkillId == currentSkillId;

        public static bool IsEchoBreak(BattleGimmick gimmick, string previousSkillId, string currentSkillId) =>
            gimmick == BattleGimmick.EchoSeal && !string.IsNullOrEmpty(previousSkillId) &&
            !string.IsNullOrEmpty(currentSkillId) && previousSkillId != currentSkillId;
    }
}
