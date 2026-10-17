using ShadowTheater.Field;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>비활성 목록 템플릿에도 설치되어 이후 생성되는 선택 항목이 소리를 상속한다.</summary>
    public class UiButtonSound : MonoBehaviour, IPointerDownHandler, ISubmitHandler
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        private static void Install(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                    if (button.GetComponent<VirtualDPadButton>() == null && button.GetComponent<UiButtonSound>() == null)
                        button.gameObject.AddComponent<UiButtonSound>();
        }

        private void Play()
        {
            var button = GetComponent<Button>();
            if (button != null && button.IsActive() && button.IsInteractable())
                GameFeedbackAudio.Play(FeedbackCue.Confirm);
        }
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) Play(); }
        public void OnSubmit(BaseEventData data) => Play();
    }
}
