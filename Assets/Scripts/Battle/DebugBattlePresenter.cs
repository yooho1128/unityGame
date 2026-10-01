using System.Collections;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Battle
{
    /// <summary>
    /// 아트/연출 없이 전투 로직을 테스트하기 위한 임시 Presenter. 콘솔 로그 + 짧은 대기.
    /// 실제 연출(DOTween)은 같은 인터페이스로 BattlePresenter를 따로 만들어 교체.
    /// </summary>
    public class DebugBattlePresenter : MonoBehaviour, IBattlePresenter
    {
        [SerializeField] private float stepDelay = 0.4f;
        private float _speed = 1f;

        public void SetSpeed(float timeScale) => _speed = Mathf.Max(0.1f, timeScale);

        private IEnumerator Wait(float mul = 1f)
        {
            yield return new WaitForSeconds(stepDelay * mul / _speed);
        }

        private IEnumerator Log(string msg, float mul = 1f)
        {
            Debug.Log($"<color=#b48cff>[Battle]</color> {msg}");
            yield return Wait(mul);
        }

        public IEnumerator PlayIntro(BattleUnit p, BattleUnit e, BattleMode mode) =>
            Log($"[{mode}] {e.Name} Lv.{e.Level} 이(가) 무대에 올랐다! / 가라, {p.Name}!", 2f);

        public IEnumerator PlaySendOut(BattleUnit u) => Log($"{u.Name} 등장 (HP {u.Hp}/{u.MaxHp})");
        public IEnumerator PlayWithdraw(BattleUnit u) => Log($"{u.Name}, 돌아와!");

        public IEnumerator PlaySkill(BattleUnit a, BattleUnit d, SkillData s, HitResult hit)
        {
            string msg = $"{a.Name}의 {s.displayName}!";
            if (hit.missed) msg += " → 빗나감";
            else if (s.DealsDamage)
            {
                msg += $" → {d.Name}에게 {hit.damage} 피해 (HP {d.Hp}/{d.MaxHp})";
                if (hit.critical) msg += " [치명타]";
                if (hit.IsEffective) msg += " [효과 굉장]";
                if (hit.IsResisted) msg += " [효과 미미]";
            }
            return Log(msg, 1.5f);
        }

        public IEnumerator PlayHeal(BattleUnit u, int amount) => Log($"{u.Name} HP {amount} 회복 ({u.Hp}/{u.MaxHp})");
        public IEnumerator PlayStatusApplied(BattleUnit u, StatusEffectType s) => Log($"{u.Name}에게 [{s}] 부여");
        public IEnumerator PlayStatusDamage(BattleUnit u, StatusEffectType s, int dmg) => Log($"{u.Name} [{s}] 피해 {dmg} ({u.Hp}/{u.MaxHp})");
        public IEnumerator PlayCantMove(BattleUnit u) => Log($"{u.Name}은(는) 얼어붙어 움직일 수 없다!");
        public IEnumerator PlayFaint(BattleUnit u) => Log($"{u.Name}의 실루엣이 흩어졌다...", 1.5f);

        public IEnumerator PlayRecordAttempt(BattleUnit t, bool ok) =>
            Log(ok ? $"각본 기록 성공! {t.Name}의 기억이 각본집에 새겨졌다." : $"{t.Name}이(가) 각본을 찢고 뛰쳐나왔다!", 2f);

        public IEnumerator PlayItem(BattleUnit t, ItemData item) => Log($"{item.displayName} 사용 → {t.Name}");
        public IEnumerator PlayEscape(bool ok) => Log(ok ? "무사히 무대에서 내려왔다." : "도망칠 수 없다!");
        public IEnumerator ShowMessage(string m) => Log(m);

        public IEnumerator PlayResult(BattleOutcome o) =>
            Log($"=== 결과: {o.result} / EXP {o.expGained} / Gold {o.goldGained} ===", 2f);
    }
}
