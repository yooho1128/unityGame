using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>
    /// 보스에게 말을 걸면 전투 전 대사 → 보스전 → 승리 대사 순서로 진행한다.
    /// 승리한 encounterId는 기존 GameFlow/SaveManager가 저장하며 재입장 시 보스가 사라진다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BossEncounterTrigger : MonoBehaviour, IInteractable
    {
        [Header("식별 / 전투")]
        [SerializeField] private string encounterId = "boss_moonlit_stage";
        [SerializeField] private List<ShadowData> enemyParty = new List<ShadowData>();
        [SerializeField] private Vector2Int levelRange = new Vector2Int(10, 12);

        [Header("컷신")]
        [SerializeField] private string preBattleDialogueId = "boss_moonlit_pre";
        [SerializeField] private string victoryDialogueId = "boss_moonlit_post";
        [SerializeField] private string victoryFlag = "boss_moonlit_story_complete";
        [SerializeField] private Color accent = new Color(0.62f, 0.70f, 1f, 1f);

        [Header("정화 보상 (선택)")]
        [Tooltip("승리 후 각본집에 합류할 보스 그림자. 비우면 보상하지 않음")]
        [SerializeField] private ShadowData purificationReward;
        [SerializeField, Min(1)] private int purificationRewardLevel = 12;
        [SerializeField] private string purificationRewardFlag = "boss_moonlit_reward_claimed";

        [Header("표현")]
        [SerializeField] private SpriteRenderer silhouette;

        private bool _busy;

        private IEnumerator Start()
        {
            if (silhouette == null) silhouette = GetComponentInChildren<SpriteRenderer>();
            // 생성기가 지정한 필드용 픽셀 스프라이트를 유지한다. 직접 배치한 보스처럼
            // 렌더러가 비어 있는 경우에만 고해상도 전투 실루엣을 대체 이미지로 사용한다.
            if (silhouette != null && silhouette.sprite == null && enemyParty.Count > 0 && enemyParty[0] != null)
                silhouette.sprite = enemyParty[0].silhouetteSprite;

            yield return new WaitUntil(() => SaveManager.Current != null);
            if (SaveManager.IsEncounterCleared(encounterId)) gameObject.SetActive(false);
        }

        public void Interact(PlayerController player)
        {
            if (_busy || SaveManager.IsEncounterCleared(encounterId) || !SaveManager.HasAliveShadow()) return;
            if (GameFlowController.Instance == null)
            {
                Debug.LogWarning($"[Boss:{encounterId}] GameFlowController가 없습니다.");
                return;
            }

            var dialogue = DialogueController.Instance;
            if (dialogue != null && dialogue.IsPlaying) return;
            _busy = true;

            if (dialogue != null && !string.IsNullOrEmpty(preBattleDialogueId))
                dialogue.Play(preBattleDialogueId, accent, StartBattle);
            else
                StartBattle();
        }

        private void StartBattle()
        {
            var enemies = new List<ShadowInstance>();
            int min = Mathf.Max(1, Mathf.Min(levelRange.x, levelRange.y));
            int max = Mathf.Max(min, Mathf.Max(levelRange.x, levelRange.y));
            foreach (var shadow in enemyParty)
                if (shadow != null) enemies.Add(new ShadowInstance(shadow, Random.Range(min, max + 1)));

            if (enemies.Count == 0)
            {
                Debug.LogError($"[Boss:{encounterId}] enemyParty가 비어 있습니다.");
                _busy = false;
                return;
            }

            GameFlowController.Instance.StartScriptedBattle(BattleMode.Boss, enemies, encounterId,
                OnBattleFinished);
        }

        private void OnBattleFinished(BattleOutcome outcome)
        {
            if (outcome.result != BattleResult.Victory)
            {
                _busy = false;
                return;
            }

            GrantPurificationReward();

            var dialogue = DialogueController.Instance;
            if (dialogue != null && !string.IsNullOrEmpty(victoryDialogueId))
                dialogue.Play(victoryDialogueId, accent, FinishStory);
            else
                FinishStory();
        }

        private void GrantPurificationReward()
        {
            if (purificationReward == null ||
                (!string.IsNullOrEmpty(purificationRewardFlag) && SaveManager.HasFlag(purificationRewardFlag))) return;

            var instance = new ShadowInstance(purificationReward, purificationRewardLevel);
            bool joinedParty = SaveManager.AddCapturedShadow(instance);
            QuestManager.Instance?.Notify(QuestObjectiveType.Record, purificationReward.shadowId);
            if (!string.IsNullOrEmpty(purificationRewardFlag)) SaveManager.SetFlag(purificationRewardFlag);
            SaveManager.Instance.Save();
            string rewardName = L10n.Text(purificationReward.displayName);
            SaveFeedbackController.Instance?.ShowMessage(joinedParty
                ? L10n.Format("capture.to_party", "{0}이(가) 파티에 합류했습니다!", rewardName)
                : L10n.Format("capture.to_storage", "파티가 가득 차 {0}을(를) 각본 서고에 보관했습니다.", rewardName));
            Debug.Log($"[Boss:{encounterId}] {purificationReward.displayName} 정화 완료 → " +
                      (joinedParty ? "파티" : "각본 서고"));
        }

        private void FinishStory()
        {
            if (!string.IsNullOrEmpty(victoryFlag)) SaveManager.SetFlag(victoryFlag);
            SaveManager.Instance.Save();
            _busy = false;
            gameObject.SetActive(false);
        }
    }
}
