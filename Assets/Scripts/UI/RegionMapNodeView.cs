using System;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class RegionMapNodeView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image accent;
        [SerializeField] private Text orderText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text stateText;

        public void Bind(RegionData region, RegionMapState state, Action<RegionData> onSelected)
        {
            bool known = state != RegionMapState.Locked;
            if (orderText != null) orderText.text = $"{region.order:00}";
            if (nameText != null) nameText.text = known ? region.displayName : "아직 걷지 않은 길";
            if (levelText != null) levelText.text = known
                ? $"Lv.{region.recommendedLevelMin}–{region.recommendedLevelMax}"
                : "???";
            if (stateText != null) stateText.text = StateLabel(state);
            if (accent != null) accent.color = known ? region.AccentColor : new Color(.20f,.18f,.25f,1f);
            if (button != null)
            {
                button.interactable = known;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onSelected?.Invoke(region));
            }
            gameObject.SetActive(true);
        }

        private static string StateLabel(RegionMapState state)
        {
            switch (state)
            {
                case RegionMapState.Current: return "현재 위치";
                case RegionMapState.Visited: return "방문 완료";
                case RegionMapState.Unlocked: return "새 지역";
                default: return "잠김";
            }
        }
    }
}
