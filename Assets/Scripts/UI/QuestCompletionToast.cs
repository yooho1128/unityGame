using System.Collections;
using ShadowTheater.Data;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>퀘스트 완료와 실제 지급 보상을 화면 중앙에 잠시 표시한다.</summary>
    public class QuestCompletionToast : MonoBehaviour
    {
        [SerializeField] private CanvasGroup root;
        [SerializeField] private Text titleText;
        [SerializeField] private Text rewardText;
        [SerializeField, Min(.1f)] private float holdDuration = 2.2f;
        [SerializeField, Min(.01f)] private float fadeDuration = .22f;
        private Coroutine _routine;

        private void Awake() => HideImmediate();
        private void OnEnable() => QuestManager.GlobalQuestCompleted += Show;
        private void OnDisable()
        {
            QuestManager.GlobalQuestCompleted -= Show;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null; HideImmediate();
        }

        private void Show(QuestDefinition quest)
        {
            if (quest == null || root == null) return;
            if (_routine != null) StopCoroutine(_routine);
            if (titleText != null) titleText.text = L10n.Format("quest.completed", "기억 완료 · {0}",
                L10n.Get($"quest.{quest.questId}.title", L10n.Text(quest.title)));
            if (rewardText != null)
            {
                string reward = quest.rewardGold > 0 ? $"{quest.rewardGold:N0} 금화" : string.Empty;
                if (!string.IsNullOrEmpty(quest.rewardItemId) && quest.rewardItemCount > 0)
                {
                    ItemData item = ShadowDatabase.Instance?.GetItem(quest.rewardItemId);
                    string itemName = item != null ? L10n.Text(item.displayName) : quest.rewardItemId;
                    reward += (reward.Length > 0 ? " · " : string.Empty) + $"{itemName} x{quest.rewardItemCount}";
                }
                rewardText.text = L10n.Get("quest.reward_received", "보상 획득") + "  " + reward;
            }
            _routine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            root.gameObject.SetActive(true);
            yield return FadeTo(1f);
            yield return new WaitForSecondsRealtime(holdDuration);
            yield return FadeTo(0f);
            root.gameObject.SetActive(false); _routine = null;
        }

        private IEnumerator FadeTo(float target)
        {
            float start = root.alpha;
            for (float elapsed=0f; elapsed<fadeDuration; elapsed+=Time.unscaledDeltaTime)
            { root.alpha=Mathf.Lerp(start,target,elapsed/fadeDuration); yield return null; }
            root.alpha=target;
        }

        private void HideImmediate()
        {
            if (root == null) return;
            root.alpha=0f; root.interactable=false; root.blocksRaycasts=false; root.gameObject.SetActive(false);
        }
    }
}
