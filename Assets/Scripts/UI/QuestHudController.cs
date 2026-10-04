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

        private void OnEnable()
        {
            QuestManager.GlobalQuestChanged += Refresh;
            L10n.Changed += Refresh;
            Refresh();
        }

        private void Start() => Refresh();

        private void Update()
        {
            bool inBattle = GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle;
            if (inBattle == _wasInBattle) return;
            _wasInBattle = inBattle;
            Refresh();
        }

        private void OnDisable()
        {
            QuestManager.GlobalQuestChanged -= Refresh;
            L10n.Changed -= Refresh;
        }

        public void Refresh()
        {
            var manager = QuestManager.Instance;
            var quest = manager != null ? manager.TrackedQuest : null;
            bool inBattle = GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle;
            _wasInBattle = inBattle;
            if (panelRoot != null) panelRoot.SetActive(quest != null && !inBattle);
            if (quest == null || manager == null) return;

            if (titleText != null) titleText.text = L10n.Get($"quest.{quest.questId}.title", L10n.Text(quest.title));

            var builder = new StringBuilder();
            foreach (var objective in quest.objectives)
            {
                var item = manager.GetObjectiveProgress(quest.questId, objective.objectiveId);
                int current = item?.current ?? 0;
                int required = Mathf.Max(1, objective.requiredCount);
                bool done = current >= required;
                if (builder.Length > 0) builder.AppendLine();
                builder.Append(done ? "✓ " : "□ ");
                builder.Append(L10n.Get($"quest.{quest.questId}.{objective.objectiveId}", L10n.Text(objective.description)));
                if (required > 1) builder.Append($"  {current}/{required}");
            }
            if (objectiveText != null) objectiveText.text = builder.ToString();
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
    }
}
