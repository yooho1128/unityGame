using System.Collections;
using ShadowTheater.Data;

namespace ShadowTheater.Battle
{
    /// <summary>
    /// 전투 연출 계층 인터페이스. BattleManager는 "무엇이 일어났는지"만 알리고,
    /// DOTween/카메라 쉐이크/FX/SFX는 구현체가 담당한다.
    /// 모든 메서드는 코루틴 → 연출이 끝날 때까지 전투 로직이 대기.
    /// </summary>
    public interface IBattlePresenter
    {
        void SetSpeed(float timeScale);

        IEnumerator PlayIntro(BattleUnit player, BattleUnit enemy, BattleMode mode);
        IEnumerator PlaySendOut(BattleUnit unit);
        IEnumerator PlayWithdraw(BattleUnit unit);

        IEnumerator PlaySkill(BattleUnit attacker, BattleUnit defender, SkillData skill, HitResult hit);
        IEnumerator PlayHeal(BattleUnit unit, int amount);
        IEnumerator PlayStatusApplied(BattleUnit unit, StatusEffectType status);
        IEnumerator PlayStatusDamage(BattleUnit unit, StatusEffectType status, int damage);
        IEnumerator PlayCantMove(BattleUnit unit);
        IEnumerator PlayFaint(BattleUnit unit);

        IEnumerator PlayRecordAttempt(BattleUnit target, bool success);
        IEnumerator PlayItem(BattleUnit target, ItemData item);
        IEnumerator PlayEscape(bool success);

        IEnumerator ShowMessage(string message);
        IEnumerator PlayResult(BattleOutcome outcome);
    }
}
