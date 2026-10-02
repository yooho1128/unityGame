using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>모바일 전투 입력 UI와 경량 연출 Presenter.</summary>
    public class BattleUIController : MonoBehaviour, IBattlePresenter
    {
        [SerializeField] private BattleManager manager;
        [SerializeField] private BattleUnitPanel playerPanel;
        [SerializeField] private BattleUnitPanel enemyPanel;
        [SerializeField] private BattleFxDirector fxDirector;
        [SerializeField] private GameObject actionRoot;
        [SerializeField] private GameObject optionRoot;
        [SerializeField] private Transform optionContent;
        [SerializeField] private BattleOptionButton optionTemplate;
        [SerializeField] private Text messageText;
        [SerializeField] private Text fpText;
        [SerializeField] private Text turnText;
        [SerializeField] private Text autoText;
        [SerializeField] private Text speedText;
        [SerializeField] private Button recordButton;
        [SerializeField] private Button escapeButton;
        [SerializeField, Min(0f)] private float messageDuration = 0.55f;

        private readonly List<GameObject> _options = new List<GameObject>();
        private float _speed = 1f;

        private void Awake()
        {
            if (manager == null) manager = GetComponent<BattleManager>();
            if (optionTemplate != null) optionTemplate.gameObject.SetActive(false);
            ShowActions(false);
        }

        private void OnEnable()
        {
            if (manager == null) return;
            manager.OnStateChanged += OnStateChanged;
            manager.OnFpChanged += OnFpChanged;
            manager.OnUnitChanged += OnUnitChanged;
            manager.OnActiveChanged += OnActiveChanged;
        }

        private void OnDisable()
        {
            if (manager == null) return;
            manager.OnStateChanged -= OnStateChanged;
            manager.OnFpChanged -= OnFpChanged;
            manager.OnUnitChanged -= OnUnitChanged;
            manager.OnActiveChanged -= OnActiveChanged;
        }

        public void Attack() => Submit(BattleAction.Attack(BattleSide.Player, manager.PlayerActive.BasicAttack));
        public void Record() => Submit(BattleAction.Record());
        public void Escape() => Submit(BattleAction.Escape());

        public void OpenSkills()
        {
            OpenOptions("사용할 기술을 선택하세요");
            foreach (var skill in manager.PlayerActive.Skills)
            {
                if (skill == null) continue;
                var captured = skill;
                AddOption(skill.displayName, $"FP {skill.fpCost} · 위력 {skill.damageMultiplier:0.0}",
                    manager.CanUseSkill(skill), () => Submit(BattleAction.UseSkill(BattleSide.Player, captured)));
            }
        }

        public void OpenSwitches()
        {
            OpenOptions(manager.State == BattleState.ForcedSwitch ? "다음 그림자를 선택하세요" : "교체할 그림자를 선택하세요");
            for (int i = 0; i < manager.PlayerUnits.Count; i++)
            {
                int index = i;
                var unit = manager.PlayerUnits[i];
                AddOption(unit.Name, $"Lv.{unit.Level}  HP {unit.Hp}/{unit.MaxHp}", manager.CanSwitchTo(i),
                    () => Submit(BattleAction.Switch(BattleSide.Player, index)));
            }
        }

        public void OpenItems()
        {
            OpenOptions("사용할 도구를 선택하세요");
            if (manager.Context?.inventory == null) return;
            foreach (var pair in manager.Context.inventory)
            {
                ItemData item = pair.Key;
                int count = pair.Value;
                AddOption(item.displayName, $"보유 {count} · {item.description}", count > 0 && item.usableInBattle,
                    () => Submit(BattleAction.UseItem(BattleSide.Player, item)));
            }
        }

        public void BackToActions()
        {
            if (manager.State == BattleState.ForcedSwitch)
            {
                OpenSwitches();
                return;
            }
            ClearOptions();
            if (optionRoot != null) optionRoot.SetActive(false);
            if (actionRoot != null) actionRoot.SetActive(manager.State == BattleState.WaitingForInput);
        }

        public void ToggleAuto()
        {
            manager.SetAuto(!manager.IsAuto);
            RefreshLabels();
        }

        public void CycleSpeed()
        {
            float next = _speed < 1.5f ? 2f : _speed < 2.5f ? 3f : 1f;
            manager.SetSpeed(next);
            SetSpeed(next);
            RefreshLabels();
        }

        private void Submit(BattleAction action)
        {
            if (!manager.SubmitAction(action)) return;
            ClearOptions();
            ShowActions(false);
        }

        private void OnStateChanged(BattleState state)
        {
            if (turnText != null) turnText.text = $"TURN {manager.Turn}";
            if (state == BattleState.WaitingForInput) ShowActions(!manager.IsAuto);
            else if (state == BattleState.ForcedSwitch) { ShowActions(false); OpenSwitches(); }
            else ShowActions(false);
            RefreshAll();
        }

        private void OnFpChanged(BattleSide side, int value) { if (side == BattleSide.Player) RefreshLabels(); }
        private void OnUnitChanged(BattleUnit unit) { if (unit.Side == BattleSide.Player) playerPanel.Bind(unit); else enemyPanel.Bind(unit); }
        private void OnActiveChanged(BattleSide side, BattleUnit unit) { if (side == BattleSide.Player) playerPanel.Bind(unit); else enemyPanel.Bind(unit); }

        private void RefreshAll()
        {
            if (manager.Context == null) return;
            playerPanel.Bind(manager.PlayerActive);
            enemyPanel.Bind(manager.EnemyActive);
            if (recordButton != null) recordButton.interactable = manager.Context.CanCapture;
            if (escapeButton != null) escapeButton.interactable = manager.Context.CanEscape;
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (fpText != null && manager.Context != null)
                fpText.text = $"FP {manager.GetFp(BattleSide.Player)} / {manager.MaxFp}";
            if (autoText != null) autoText.text = manager.IsAuto ? "AUTO ON" : "AUTO OFF";
            if (speedText != null) speedText.text = $"×{_speed:0}";
        }

        private void ShowActions(bool show)
        {
            if (actionRoot != null) actionRoot.SetActive(show);
            if (!show && optionRoot != null) optionRoot.SetActive(false);
        }

        private void OpenOptions(string prompt)
        {
            ClearOptions();
            if (messageText != null) messageText.text = prompt;
            if (actionRoot != null) actionRoot.SetActive(false);
            if (optionRoot != null) optionRoot.SetActive(true);
        }

        private void AddOption(string title, string subtitle, bool enabled, System.Action action)
        {
            var option = Instantiate(optionTemplate, optionContent);
            option.Bind(title, subtitle, enabled, action);
            _options.Add(option.gameObject);
        }

        private void ClearOptions()
        {
            foreach (var option in _options) Destroy(option);
            _options.Clear();
        }

        public void SetSpeed(float timeScale) => _speed = Mathf.Max(0.1f, timeScale);

        public IEnumerator PlayIntro(BattleUnit player, BattleUnit enemy, BattleMode mode)
        { RefreshAll(); yield return Say($"{enemy.Name}이(가) 무대에 올랐다!\n가라, {player.Name}!"); }
        public IEnumerator PlaySendOut(BattleUnit unit) { yield return Say($"{unit.Name}, 무대로!"); }
        public IEnumerator PlayWithdraw(BattleUnit unit) { yield return Say($"{unit.Name}, 각본집으로 돌아와!"); }
        public IEnumerator PlaySkill(BattleUnit a, BattleUnit d, SkillData s, HitResult hit)
        {
            BattleUnitPanel attacker = a.Side == BattleSide.Player ? playerPanel : enemyPanel;
            BattleUnitPanel defender = d.Side == BattleSide.Player ? playerPanel : enemyPanel;
            if (fxDirector != null) yield return fxDirector.Play(attacker, defender, s, hit, _speed);
            yield return Say(hit.missed ? $"{a.Name}의 {s.displayName}! 빗나갔다." : $"{a.Name}의 {s.displayName}!" );
        }
        public IEnumerator PlayHeal(BattleUnit unit, int amount) { yield return Say($"{unit.Name}의 HP가 {amount} 회복됐다."); }
        public IEnumerator PlayStatusApplied(BattleUnit unit, StatusEffectType status) { yield return Say($"{unit.Name}에게 {status} 상태가 걸렸다."); }
        public IEnumerator PlayStatusDamage(BattleUnit unit, StatusEffectType status, int damage) { yield return Say($"{unit.Name}은(는) {status}로 {damage} 피해!"); }
        public IEnumerator PlayCantMove(BattleUnit unit) { yield return Say($"{unit.Name}은(는) 움직일 수 없다!"); }
        public IEnumerator PlayFaint(BattleUnit unit) { yield return Say($"{unit.Name}의 실루엣이 흩어졌다."); }
        public IEnumerator PlayRecordAttempt(BattleUnit target, bool success) { yield return Say(success ? $"{target.Name}의 기억을 기록했다!" : "기록에 실패했다!"); }
        public IEnumerator PlayItem(BattleUnit target, ItemData item) { yield return Say($"{item.displayName} 사용!"); }
        public IEnumerator PlayEscape(bool success) { yield return Say(success ? "무대에서 벗어났다." : "도망칠 수 없다!"); }
        public IEnumerator ShowMessage(string message) { yield return Say(message); }
        public IEnumerator PlayResult(BattleOutcome outcome) { yield return Say($"전투 {ResultLabel(outcome.result)}\nEXP +{outcome.expGained}  금화 +{outcome.goldGained}", 1.4f); }

        private IEnumerator Say(string message, float multiplier = 1f)
        {
            if (messageText != null) messageText.text = message;
            RefreshAll();
            yield return new WaitForSecondsRealtime(messageDuration * multiplier / _speed);
        }

        private static string ResultLabel(BattleResult result)
        {
            switch (result)
            {
                case BattleResult.Victory: return "승리";
                case BattleResult.Defeat: return "패배";
                case BattleResult.Captured: return "기록 성공";
                case BattleResult.Escaped: return "종료";
                default: return result.ToString();
            }
        }
    }
}
