using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>표지판, 기억의 제단, 조사 오브젝트처럼 이름 없는 대상에 붙이는 범용 대화 컴포넌트.</summary>
    public class DialogueInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string dialogueId = "sign_moonlit_meadow";
        [SerializeField] private Color accent = new Color(0.45f, 0.77f, 1f, 1f);
        [Tooltip("비워 두면 매번 재생. 값이 있으면 첫 완료 때 저장한다.")]
        [SerializeField] private string completionFlag;
        [Tooltip("완료 플래그가 이미 있으면 더는 상호작용하지 않는다.")]
        [SerializeField] private bool oneShot;

        public void Interact(PlayerController player)
        {
            if (oneShot && !string.IsNullOrEmpty(completionFlag) && SaveManager.HasFlag(completionFlag)) return;
            var controller = DialogueController.Instance;
            if (controller == null || controller.IsPlaying) return;

            controller.Play(dialogueId, accent, () =>
            {
                if (!string.IsNullOrEmpty(completionFlag)) SaveManager.SetFlag(completionFlag);
                if (SaveManager.Instance != null && SaveManager.Current != null)
                    SaveManager.Instance.Save();
            });
        }
    }
}
