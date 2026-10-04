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
        [SerializeField] private BattlePartyStrip playerPartyStrip;
        [SerializeField] private BattlePartyStrip enemyPartyStrip;
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
            OpenOptions(L10n.Get("battle.choose_skill", "사용할 기술을 선택하세요"));
            foreach (var skill in manager.PlayerActive.Skills)
            {
                if (skill == null) continue;
                var captured = skill;
                AddOption(L10n.Text(skill.displayName), L10n.Format("battle.skill_info", "FP {0} · 위력 {1:0.0}", skill.fpCost, skill.damageMultiplier),
                    manager.CanUseSkill(skill), () => Submit(BattleAction.UseSkill(BattleSide.Player, captured)));
            }
            RebuildOptionsLayout();
        }

        public void OpenSwitches()
        {
            OpenOptions(manager.State == BattleState.ForcedSwitch
                ? L10n.Get("battle.choose_next", "다음 그림자를 선택하세요")
                : L10n.Get("battle.choose_switch", "교체할 그림자를 선택하세요"));
            int candidates = 0;
            for (int i = 0; i < manager.PlayerUnits.Count; i++)
            {
                if (i == manager.PlayerActiveIndex) continue;
                int index = i;
                var unit = manager.PlayerUnits[i];
                bool canSwitch = manager.CanSwitchTo(i);
                if (canSwitch) candidates++;
                AddOption(L10n.Text(unit.Name), $"Lv.{unit.Level}  HP {unit.Hp}/{unit.MaxHp}", canSwitch,
                    () => Submit(BattleAction.Switch(BattleSide.Player, index)));
            }
            if (manager.PlayerUnits.Count <= 1 || candidates == 0)
                AddOption(L10n.Get("battle.no_switch_candidate", "교체 가능한 그림자가 없습니다"),
                    L10n.Get("battle.no_switch_hint", "필드의 파티 메뉴에서 포획한 그림자를 확인하세요"), false, null);
            RebuildOptionsLayout();
        }

        public void OpenItems()
        {
            OpenOptions(L10n.Get("battle.choose_item", "사용할 도구를 선택하세요"));
            if (manager.Context?.inventory != null)
            {
                foreach (var pair in manager.Context.inventory)
                {
                    ItemData item = pair.Key;
                    int count = pair.Value;
                    AddOption(L10n.Text(item.displayName), L10n.Format("battle.item_info", "보유 {0} · {1}", count, L10n.Text(item.description)), count > 0 && item.usableInBattle,
                        () => Submit(BattleAction.UseItem(BattleSide.Player, item)));
                }
            }
            if (_options.Count == 0)
                AddOption(L10n.Get("battle.no_items", "사용할 수 있는 도구가 없습니다"), string.Empty, false, null);
            RebuildOptionsLayout();
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
        private void OnUnitChanged(BattleUnit unit)
        {
            if (unit.Side == BattleSide.Player)
            {
                if (unit == manager.PlayerActive) playerPanel.Bind(unit);
            }
            else if (unit == manager.EnemyActive) enemyPanel.Bind(unit);
            RefreshPartyStrips();
        }
        private void OnActiveChanged(BattleSide side, BattleUnit unit)
        {
            if (side == BattleSide.Player) playerPanel.Bind(unit); else enemyPanel.Bind(unit);
            RefreshPartyStrips();
        }

        private void RefreshAll()
        {
            if (manager.Context == null) return;
            playerPanel.Bind(manager.PlayerActive);
            enemyPanel.Bind(manager.EnemyActive);
            if (recordButton != null) recordButton.interactable = manager.Context.CanCapture;
            if (escapeButton != null) escapeButton.interactable = manager.Context.CanEscape;
            RefreshPartyStrips();
            RefreshLabels();
        }

        private void RefreshPartyStrips()
        {
            if (manager.Context == null) return;
            playerPartyStrip?.Bind(manager.PlayerUnits, manager.PlayerActiveIndex);
            enemyPartyStrip?.Bind(manager.EnemyUnits, manager.EnemyActiveIndex);
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
            // 비활성 템플릿을 복제하므로 Bind 전에도 명시적으로 켠다.
            option.gameObject.SetActive(true);
            option.Bind(title, subtitle, enabled, action);
            _options.Add(option.gameObject);
        }

        private void RebuildOptionsLayout()
        {
            if (!(optionContent is RectTransform content)) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }

        private void ClearOptions()
        {
            foreach (var option in _options) Destroy(option);
            _options.Clear();
        }

        public void SetSpeed(float timeScale) => _speed = Mathf.Max(0.1f, timeScale);

        public IEnumerator PlayIntro(BattleUnit player, BattleUnit enemy, BattleMode mode)
        { RefreshAll(); yield return Say(L10n.Format("battle.intro", "{0}이(가) 무대에 올랐다!\n가라, {1}!", L10n.Text(enemy.Name), L10n.Text(player.Name))); }
        public IEnumerator PlaySendOut(BattleUnit unit) { yield return Say(L10n.Format("battle.send_out", "{0}, 무대로!", L10n.Text(unit.Name))); }
        public IEnumerator PlayWithdraw(BattleUnit unit) { yield return Say(L10n.Format("battle.withdraw", "{0}, 각본집으로 돌아와!", L10n.Text(unit.Name))); }
        public IEnumerator PlaySkill(BattleUnit a, BattleUnit d, SkillData s, HitResult hit)
        {
            BattleUnitPanel attacker = a.Side == BattleSide.Player ? playerPanel : enemyPanel;
            BattleUnitPanel defender = d.Side == BattleSide.Player ? playerPanel : enemyPanel;
            if (fxDirector != null) yield return fxDirector.Play(attacker, defender, s, hit, _speed);
            yield return Say(hit.missed
                ? L10n.Format("battle.missed", "{0}의 {1}! 빗나갔다.", L10n.Text(a.Name), L10n.Text(s.displayName))
                : L10n.Format("battle.used_skill", "{0}의 {1}!", L10n.Text(a.Name), L10n.Text(s.displayName)));
        }
        public IEnumerator PlayHeal(BattleUnit unit, int amount) { yield return Say(L10n.Format("battle.heal", "{0}의 HP가 {1} 회복됐다.", L10n.Text(unit.Name), amount)); }
        public IEnumerator PlayStatusApplied(BattleUnit unit, StatusEffectType status) { yield return Say(L10n.Format("battle.status_applied", "{0}에게 {1} 상태가 걸렸다.", L10n.Text(unit.Name), StatusLabel(status))); }
        public IEnumerator PlayStatusDamage(BattleUnit unit, StatusEffectType status, int damage) { yield return Say(L10n.Format("battle.status_damage", "{0}은(는) {1}로 {2} 피해!", L10n.Text(unit.Name), StatusLabel(status), damage)); }
        public IEnumerator PlayCantMove(BattleUnit unit) { yield return Say(L10n.Format("battle.cannot_move", "{0}은(는) 움직일 수 없다!", L10n.Text(unit.Name))); }
        public IEnumerator PlayFaint(BattleUnit unit) { yield return Say(L10n.Format("battle.faint", "{0}의 실루엣이 흩어졌다.", L10n.Text(unit.Name))); }
        public IEnumerator PlayRecordAttempt(BattleUnit target, bool success) { yield return Say(success ? L10n.Format("battle.recorded", "{0}의 기억을 기록했다!", L10n.Text(target.Name)) : L10n.Get("battle.record_failed", "기록에 실패했다!")); }
        public IEnumerator PlayItem(BattleUnit target, ItemData item) { yield return Say(L10n.Format("battle.used_item", "{0} 사용!", L10n.Text(item.displayName))); }
        public IEnumerator PlayEscape(bool success) { yield return Say(success ? L10n.Get("battle.escaped", "무대에서 벗어났다.") : L10n.Get("battle.escape_failed", "도망칠 수 없다!")); }
        public IEnumerator ShowMessage(string message) { yield return Say(message); }
        public IEnumerator PlayResult(BattleOutcome outcome) { yield return Say(L10n.Format("battle.result", "전투 {0}\nEXP +{1}  금화 +{2}", ResultLabel(outcome.result), outcome.expGained, outcome.goldGained), 1.4f); }

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
                case BattleResult.Victory: return L10n.Get("battle.victory", "승리");
                case BattleResult.Defeat: return L10n.Get("battle.defeat", "패배");
                case BattleResult.Captured: return L10n.Get("battle.record_success", "기록 성공");
                case BattleResult.Escaped: return L10n.Get("battle.ended", "종료");
                default: return result.ToString();
            }
        }

        private static string StatusLabel(StatusEffectType status) =>
            L10n.Get("status." + status.ToString().ToLowerInvariant(), status.ToString());
    }
}
