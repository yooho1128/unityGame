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
        }

        public void Refresh()
        {
            var manager = QuestManager.Instance;
            var quest = manager != null ? manager.TrackedQuest : null;
            bool inBattle = GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle;
            _wasInBattle = inBattle;
            if (panelRoot != null) panelRoot.SetActive(quest != null && !inBattle);
            if (quest == null || manager == null) return;

            if (titleText != null) titleText.text = quest.title;

            var builder = new StringBuilder();
            foreach (var objective in quest.objectives)
            {
                var item = manager.GetObjectiveProgress(quest.questId, objective.objectiveId);
                int current = item?.current ?? 0;
                int required = Mathf.Max(1, objective.requiredCount);
                bool done = current >= required;
                if (builder.Length > 0) builder.AppendLine();
                builder.Append(done ? "✓ " : "□ ");
                builder.Append(objective.description);
                if (required > 1) builder.Append($"  {current}/{required}");
            }
            if (objectiveText != null) objectiveText.text = builder.ToString();
            if (rewardText != null)
                rewardText.text = quest.rewardGold > 0 ? $"보상  {quest.rewardGold:N0} 금화" : string.Empty;
        }
    }
}
