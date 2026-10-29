using System.Text;
using ShadowTheater.Field;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>현재 추적 중인 퀘스트와 목표 카운트를 필드 화면에 표시한다.</summary>
    public class QuestHudController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text rewardText;
        private bool _wasInBattle;
        private Image _progressFill;
        private Text _progressLabel;
        private bool _styled;
        private float _refreshAt;

        private void OnEnable()
        {
            QuestManager.GlobalQuestChanged += Refresh;
            L10n.Changed += Refresh;
            ShadowTheater.Save.SaveManager.CurrentChanged += Refresh;
            Refresh();
        }

        private void Start() => Refresh();

        private void Update()
        {
            bool inBattle = GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle;
            if (inBattle != _wasInBattle || (!inBattle && Time.unscaledTime >= _refreshAt))
            {
                _wasInBattle = inBattle;
                _refreshAt = Time.unscaledTime + .5f;
                Refresh();
            }
        }

        private void OnDisable()
        {
            QuestManager.GlobalQuestChanged -= Refresh;
            L10n.Changed -= Refresh;
            ShadowTheater.Save.SaveManager.CurrentChanged -= Refresh;
        }

        public void Refresh()
        {
            StylePanel();
            var manager = QuestManager.Instance;
            var quest = manager != null ? manager.TrackedQuest : null;
            bool inBattle = GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle;
            _wasInBattle = inBattle;
            if (panelRoot != null) panelRoot.SetActive(quest != null && !inBattle);
            if (quest == null || manager == null) return;

            if (titleText != null) titleText.text = L10n.Get($"quest.{quest.questId}.title", L10n.Text(quest.title));

            var builder = new StringBuilder();
            int completed = 0;
            float progress = 0f;
            foreach (var objective in quest.objectives)
            {
                var item = manager.GetObjectiveProgress(quest.questId, objective.objectiveId);
                int current = item?.current ?? 0;
                int required = Mathf.Max(1, objective.requiredCount);
                bool done = current >= required;
                if (done) completed++;
                progress += Mathf.Clamp01((float)current / required);
            }
            var next = QuestTrackingPolicy.NextObjective(quest, manager.GetProgress(quest.questId));
            if (next != null)
            {
                var item = manager.GetObjectiveProgress(quest.questId, next.objectiveId);
                builder.Append("□ ");
                builder.Append(L10n.Get($"quest.{quest.questId}.{next.objectiveId}", L10n.Text(next.description)));
                if (next.requiredCount > 1) builder.Append($"  {item?.current ?? 0}/{next.requiredCount}");
                builder.AppendLine();
                builder.Append(QuestGuidanceService.For(quest).text);
            }
            if (objectiveText != null) objectiveText.text = builder.ToString();
            int objectiveCount = quest.objectives.Count;
            if (_progressFill != null)
                _progressFill.rectTransform.anchorMax = new Vector2(objectiveCount > 0 ? progress / objectiveCount : 0f, 1f);
            if (_progressLabel != null) _progressLabel.text = $"{completed:00} / {objectiveCount:00}";
            if (rewardText != null)
            {
                string itemReward = string.Empty;
                if (!string.IsNullOrEmpty(quest.rewardItemId) && quest.rewardItemCount > 0)
                {
                    var item = ShadowTheater.Data.ShadowDatabase.Instance?.GetItem(quest.rewardItemId);
                    itemReward = item != null ? $" · {L10n.Text(item.displayName)} x{quest.rewardItemCount}" : string.Empty;
                }
                rewardText.text = quest.rewardGold > 0 || itemReward.Length > 0
                    ? L10n.Format("quest.reward", "보상  {0:N0} 금화", quest.rewardGold) + itemReward : string.Empty;
            }
        }

        private void StylePanel()
        {
            if (_styled || panelRoot == null) return;
            _styled = true;
            var panel = panelRoot.GetComponent<Image>();
            if (panel != null) panel.color = new Color(.035f, .055f, .085f, .94f);
            var outline = panelRoot.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(.85f, .71f, .43f, .45f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
            var caption = panelRoot.transform.Find("Label")?.GetComponent<Text>();
            if (caption != null)
            {
                caption.color = new Color(.85f, .71f, .43f);
                caption.text = "QUEST / MEMORY";
                Place(caption.rectTransform, new Vector2(.055f, .84f), new Vector2(.70f, .96f));
                caption.fontSize = 18;
            }
            if (titleText != null)
            {
                titleText.color = new Color(.95f, .92f, .85f);
                titleText.fontStyle = FontStyle.Bold;
                titleText.resizeTextForBestFit = true;
                titleText.resizeTextMinSize = 25;
                titleText.resizeTextMaxSize = 32;
                Place(titleText.rectTransform, new Vector2(.055f, .59f), new Vector2(.94f, .79f));
            }
            if (objectiveText != null)
            {
                objectiveText.color = new Color(.78f, .84f, .86f);
                objectiveText.lineSpacing = 1.08f;
                objectiveText.resizeTextForBestFit = true;
                objectiveText.resizeTextMinSize = 16;
                objectiveText.resizeTextMaxSize = 25;
                Place(objectiveText.rectTransform, new Vector2(.055f, .19f), new Vector2(.94f, .57f));
            }
            if (rewardText != null)
            {
                rewardText.color = new Color(.85f, .71f, .43f);
                Place(rewardText.rectTransform, new Vector2(.055f, .025f), new Vector2(.94f, .16f));
            }
            var rail = new GameObject("QuestProgressRail", typeof(RectTransform), typeof(Image));
            rail.transform.SetParent(panelRoot.transform, false);
            Place((RectTransform)rail.transform, new Vector2(.055f, .815f), new Vector2(.94f, .823f));
            rail.GetComponent<Image>().color = new Color(.19f, .24f, .29f);
            rail.GetComponent<Image>().raycastTarget = false;
            var fill = new GameObject("Progress", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(rail.transform, false);
            Place((RectTransform)fill.transform, Vector2.zero, Vector2.one);
            _progressFill = fill.GetComponent<Image>();
            _progressFill.color = new Color(.85f, .71f, .43f);
            _progressFill.raycastTarget = false;
            if (titleText != null)
            {
                var counter = new GameObject("QuestProgressCount", typeof(RectTransform), typeof(Text));
                counter.transform.SetParent(panelRoot.transform, false);
                Place((RectTransform)counter.transform, new Vector2(.70f, .84f), new Vector2(.94f, .96f));
                _progressLabel = counter.GetComponent<Text>();
                _progressLabel.font = titleText.font;
                _progressLabel.fontSize = 18;
                _progressLabel.alignment = TextAnchor.MiddleRight;
                _progressLabel.color = new Color(.78f, .84f, .86f);
                _progressLabel.raycastTarget = false;
            }
        }

        private static void Place(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
