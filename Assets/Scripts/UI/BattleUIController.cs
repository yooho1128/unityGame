using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Save;
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
        [SerializeField] private GameObject messageRoot;
        [SerializeField] private Text messageText;
        [SerializeField] private Text optionPromptText;
        [SerializeField] private Text fpText;
        [SerializeField] private Text turnText;
        [SerializeField] private Text autoText;
        [SerializeField] private Text speedText;
        [SerializeField] private Text recordText;
        [SerializeField] private Button recordButton;
        [SerializeField] private Button escapeButton;
        [Header("전투 결과 카드")]
        [SerializeField] private GameObject resultRoot;
        [SerializeField] private CanvasGroup resultGroup;
        [SerializeField] private RectTransform resultCard;
        [SerializeField] private Image resultAccent;
        [SerializeField] private Text resultTitleText;
        [SerializeField] private Text resultSummaryText;
        [SerializeField] private Text resultDetailsText;
        [SerializeField] private Text resultContinueText;
        [Header("첫 전투 안내")]
        [SerializeField] private GameObject tutorialRoot;
        [SerializeField] private Text tutorialStepText;
        [SerializeField] private Text tutorialTitleText;
        [SerializeField] private Text tutorialBodyText;
        [SerializeField] private Text tutorialNextText;
        [SerializeField, Min(0f)] private float messageDuration = 0.55f;

        private readonly List<GameObject> _options = new List<GameObject>();
        private float _speed = 1f;
        private bool _resultAcknowledged;
        private int _tutorialStep = -1;
        private const string BattleTutorialFlag = "tutorial_battle_complete";

        private void Awake()
        {
            if (manager == null) manager = GetComponent<BattleManager>();
            if (optionTemplate != null) optionTemplate.gameObject.SetActive(false);
            if (resultRoot != null) resultRoot.SetActive(false);
            if (tutorialRoot != null) tutorialRoot.SetActive(false);
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
            _resultAcknowledged = true;
            if (resultRoot != null) resultRoot.SetActive(false);
            if (tutorialRoot != null) tutorialRoot.SetActive(false);
            _tutorialStep = -1;
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
                AddOption(L10n.Text(skill.displayName), SkillDetail(skill),
                    manager.CanUseSkill(skill), () => Submit(BattleAction.UseSkill(BattleSide.Player, captured)));
            }
            if (_options.Count == 0)
                AddOption(L10n.Get("battle.no_unlocked_skills", "아직 깨달은 기술이 없습니다"),
                    L10n.Get("battle.skill_unlock_hint", "레벨을 올리면 새로운 기술을 깨달을 수 있습니다"), false, null);
            RebuildOptionsLayout();
        }

        private string SkillDetail(SkillData skill)
        {
            string detail = $"FP {skill.fpCost}";
            if (skill.target == SkillTarget.Self && skill.healRatio > 0f)
                detail += L10n.Format("battle.skill_heal_info", " · 회복 {0}%", Mathf.RoundToInt(skill.healRatio * 100f));
            else if (skill.DealsDamage)
            {
                ShadowElement element = skill.element != ShadowElement.None ? skill.element : manager.PlayerActive.Data.element;
                float matchup = ElementChart.GetMultiplier(element, manager.EnemyActive.Data.element);
                detail += L10n.Format("battle.skill_damage_info", " · 위력 {0:0.0} · 명중 {1}% · {2}",
                    skill.damageMultiplier, Mathf.RoundToInt(skill.accuracy * 100f),
                    L10n.Get("element." + element.ToString().ToLowerInvariant(), element.ToString()));
                if (matchup > 1f) detail += L10n.Get("battle.matchup_strong", " · 효과적 ×1.5");
                else if (matchup < 1f) detail += L10n.Get("battle.matchup_weak", " · 반감 ×0.75");
            }
            if (skill.statusEffect != StatusEffectType.None && skill.statusChance > 0f)
                detail += L10n.Format("battle.skill_status_info", "\n{0} {1}% · {2}턴",
                    BattleStatusFormatter.Label(skill.statusEffect), Mathf.RoundToInt(skill.statusChance * 100f), skill.statusDuration);
            if (skill.isUltimate) detail += L10n.Get("battle.ultimate_tag", " · 필살기");
            return detail;
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
                string exp = unit.Instance.IsMaxLevel ? "EXP MAX" : $"EXP {unit.Instance.exp}/{unit.Instance.ExpToNext}";
                AddOption(L10n.Text(unit.Name), $"Lv.{unit.Level}  HP {unit.Hp}/{unit.MaxHp} · {exp}", canSwitch,
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
                    bool usable = manager.CanUseItem(BattleSide.Player, item, out string reason);
                    string detail = L10n.Format("battle.item_info", "보유 {0} · {1}", count, L10n.Text(item.description));
                    if (!usable && !string.IsNullOrEmpty(reason)) detail += "\n" + reason;
                    AddOption(L10n.Text(item.displayName), detail, usable,
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
            if (messageRoot != null) messageRoot.SetActive(true);
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

        public void ConfirmResult() => _resultAcknowledged = true;

        public void NextTutorial()
        {
            if (_tutorialStep < 0) return;
            _tutorialStep++;
            if (_tutorialStep >= 3) CompleteTutorial();
            else RefreshTutorial();
        }

        public void SkipTutorial() => CompleteTutorial();

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
            if (state == BattleState.WaitingForInput) TryOpenTutorial();
        }

        private void TryOpenTutorial()
        {
            if (tutorialRoot == null || manager.IsAuto || SaveManager.Current == null ||
                SaveManager.HasFlag(BattleTutorialFlag) || _tutorialStep >= 0) return;
            _tutorialStep = 0;
            tutorialRoot.SetActive(true);
            RefreshTutorial();
        }

        private void RefreshTutorial()
        {
            if (tutorialStepText != null) tutorialStepText.text = L10n.Format("battle.tutorial_step", "전투 안내 {0} / 3", _tutorialStep + 1);
            if (tutorialTitleText != null) tutorialTitleText.text = L10n.Get("battle.tutorial_title_" + _tutorialStep,
                _tutorialStep == 0 ? "공연 열기" : _tutorialStep == 1 ? "기술과 교체" : "각본 기록");
            if (tutorialBodyText != null) tutorialBodyText.text = L10n.Get("battle.tutorial_body_" + _tutorialStep,
                _tutorialStep == 0
                    ? "기본 공격은 피해를 주고 FP를 1 생성합니다. 턴이 시작될 때도 FP를 1 얻습니다."
                    : _tutorialStep == 1
                        ? "FP를 소비해 고유 기술을 사용합니다. 불리하거나 HP가 낮다면 교체와 도구를 활용하세요."
                        : "야생 그림자의 HP를 낮추면 기록 성공률이 올라갑니다. 각본 기록으로 동료를 수집하세요.");
            if (tutorialNextText != null) tutorialNextText.text = _tutorialStep >= 2
                ? L10n.Get("battle.tutorial_start", "전투 시작")
                : L10n.Get("battle.tutorial_next", "다음");
        }

        private void CompleteTutorial()
        {
            if (_tutorialStep < 0) return;
            _tutorialStep = -1;
            if (tutorialRoot != null) tutorialRoot.SetActive(false);
            SaveManager.SetFlag(BattleTutorialFlag);
            SaveManager.Instance?.Save();
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
            RefreshLabels();
        }
        private void OnActiveChanged(BattleSide side, BattleUnit unit)
        {
            if (side == BattleSide.Player) playerPanel.Bind(unit); else enemyPanel.Bind(unit);
            RefreshPartyStrips();
            RefreshLabels();
        }

        private void RefreshAll()
        {
            if (manager.Context == null) return;
            playerPanel.Bind(manager.PlayerActive);
            enemyPanel.Bind(manager.EnemyActive);
            if (recordButton != null) recordButton.interactable = manager.CanRecordCurrent;
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
            if (recordText != null && manager.Context != null)
            {
                if (!manager.Context.CanCapture) recordText.text = L10n.Get("battle.record_unavailable", "각본 기록\n불가");
                else if (!manager.CanRecordCurrent) recordText.text = L10n.Get("battle.record_locked", "각본 기록\n봉인 불가");
                else recordText.text = L10n.Format("battle.record_chance", "각본 기록\n성공률 {0}%",
                    Mathf.RoundToInt(manager.CurrentCaptureChance * 100f));
            }
        }

        private void ShowActions(bool show)
        {
            if (actionRoot != null) actionRoot.SetActive(show);
            if (!show && optionRoot != null) optionRoot.SetActive(false);
            if (messageRoot != null) messageRoot.SetActive(true);
        }

        private void OpenOptions(string prompt)
        {
            ClearOptions();
            if (messageText != null) messageText.text = prompt;
            if (optionPromptText != null) optionPromptText.text = prompt;
            if (messageRoot != null) messageRoot.SetActive(false);
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
            if (hit.missed)
            {
                yield return Say(L10n.Format("battle.missed", "{0}의 {1}! 빗나갔다.", L10n.Text(a.Name), L10n.Text(s.displayName)));
                yield break;
            }

            string message = L10n.Format("battle.used_skill", "{0}의 {1}!", L10n.Text(a.Name), L10n.Text(s.displayName));
            if (hit.damage > 0) message += L10n.Format("battle.damage_detail", "\n{0} 피해", hit.damage);
            if (hit.critical) message += L10n.Get("battle.critical", " · 치명타");
            if (hit.IsEffective) message += L10n.Get("battle.effective", " · 효과가 굉장했다");
            else if (hit.IsResisted) message += L10n.Get("battle.resisted", " · 효과가 약했다");
            yield return Say(message);
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
        public IEnumerator PlayResult(BattleOutcome outcome)
        {
            if (resultRoot == null)
            {
                yield return Say(BuildLegacyResult(outcome), 1.4f);
                yield break;
            }

            ShowActions(false);
            if (messageRoot != null) messageRoot.SetActive(false);
            _resultAcknowledged = false;
            resultRoot.SetActive(true);
            if (resultTitleText != null) resultTitleText.text = ResultLabel(outcome.result);
            if (resultSummaryText != null) resultSummaryText.text = L10n.Format("battle.result_rewards",
                "EXP +{0}   ·   금화 +{1}", outcome.expGained, outcome.goldGained);
            if (resultDetailsText != null) resultDetailsText.text = BuildResultDetails(outcome);
            if (resultContinueText != null) resultContinueText.text = L10n.Get("battle.result_continue", "계속");
            if (resultAccent != null) resultAccent.color = ResultColor(outcome.result);

            yield return AnimateResult(true);
            yield return new WaitUntil(() => _resultAcknowledged);
            yield return AnimateResult(false);
            resultRoot.SetActive(false);
        }

        private string BuildLegacyResult(BattleOutcome outcome)
        {
            string message = L10n.Format("battle.result", "전투 {0}\nEXP +{1}  금화 +{2}",
                ResultLabel(outcome.result), outcome.expGained, outcome.goldGained);
            string details = BuildResultDetails(outcome);
            return string.IsNullOrEmpty(details) ? message : message + "\n" + details;
        }

        private string BuildResultDetails(BattleOutcome outcome)
        {
            var lines = new List<string>();
            if (outcome.capturedShadow != null)
                lines.Add(L10n.Format("battle.result_recorded", "기억 기록 · {0}",
                    L10n.Text(outcome.capturedShadow.DisplayName)));

            if (outcome.leveledUpInstanceIds != null && outcome.leveledUpInstanceIds.Count > 0)
            {
                var names = new List<string>();
                foreach (BattleUnit unit in manager.PlayerUnits)
                    if (outcome.leveledUpInstanceIds.Contains(unit.Instance.instanceId)) names.Add(L10n.Text(unit.Name));
                if (names.Count > 0) lines.Add(L10n.Format("battle.result_levelups", "레벨 상승 · {0}", string.Join(", ", names)));
            }

            if (outcome.loot != null && outcome.loot.Count > 0)
            {
                var drops = new List<string>();
                foreach (BattleLoot loot in outcome.loot)
                {
                    ItemData item = ShadowDatabase.Instance?.GetItem(loot.itemId);
                    drops.Add($"{(item != null ? L10n.Text(item.displayName) : loot.itemId)} x{loot.count}");
                }
                lines.Add(L10n.Get("battle.loot", "전리품") + " · " + string.Join(", ", drops));
            }

            if (outcome.result == BattleResult.Defeat)
                lines.Add(L10n.Get("battle.result_defeat_hint", "마지막 거점에서 파티를 회복합니다."));
            else if (outcome.result == BattleResult.Escaped)
                lines.Add(L10n.Get("battle.result_escape_hint", "기억의 무대에서 안전하게 벗어났습니다."));
            else if (lines.Count == 0)
                lines.Add(L10n.Get("battle.result_no_loot", "추가 전리품은 없습니다."));
            return string.Join("\n", lines);
        }

        private IEnumerator AnimateResult(bool showing)
        {
            if (resultGroup == null || resultCard == null) yield break;
            float duration = .18f;
            float startAlpha = showing ? 0f : 1f;
            float endAlpha = showing ? 1f : 0f;
            Vector3 startScale = Vector3.one * (showing ? .92f : 1f);
            Vector3 endScale = Vector3.one * (showing ? 1f : .96f);
            resultGroup.alpha = startAlpha;
            resultCard.localScale = startScale;
            for (float elapsed=0f; elapsed<duration; elapsed+=Time.unscaledDeltaTime)
            {
                float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
                resultGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                resultCard.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
                yield return null;
            }
            resultGroup.alpha = endAlpha;
            resultCard.localScale = endScale;
        }

        private static Color ResultColor(BattleResult result)
        {
            switch (result)
            {
                case BattleResult.Victory: return new Color(1f,.72f,.28f);
                case BattleResult.Captured: return new Color(.48f,.86f,1f);
                case BattleResult.Defeat: return new Color(.92f,.22f,.34f);
                default: return new Color(.66f,.48f,.94f);
            }
        }

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

        private static string StatusLabel(StatusEffectType status) => BattleStatusFormatter.Label(status);
    }
}
