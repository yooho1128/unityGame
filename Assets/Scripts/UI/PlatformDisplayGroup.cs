using UnityEngine;

namespace ShadowTheater.UI
{
    /// <summary>같은 프리팹에서 터치 UI와 데스크톱 UI를 플랫폼에 맞게 전환한다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlatformDisplayGroup : MonoBehaviour
    {
        [SerializeField] private bool showOnMobile = true;
        [SerializeField] private bool showOnDesktop;

        private void Awake() => Apply();
        private void OnEnable() => Apply();

        public void Apply()
        {
            bool visible = Application.isMobilePlatform ? showOnMobile : showOnDesktop;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
