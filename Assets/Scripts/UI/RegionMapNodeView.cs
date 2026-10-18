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
        private RegionData _region;
        private RegionMapKnowledge _knowledge;

        public void Bind(RegionData region, RegionMapKnowledge knowledge, Action<RegionData> onSelected)
        {
            _region = region;
            _knowledge = knowledge;
            if (button == null) button = GetComponent<Button>();
            bool detailed = knowledge == RegionMapKnowledge.Visited || knowledge == RegionMapKnowledge.Current;
            bool rumored = knowledge == RegionMapKnowledge.Rumored;
            bool selectable = detailed || rumored || knowledge == RegionMapKnowledge.RouteKnown;
            if (orderText != null) orderText.text = detailed ? $"{region.order:00}" : rumored ? "?" : "--";
            if (nameText != null) nameText.text = detailed || knowledge == RegionMapKnowledge.RouteKnown
                ? L10n.Get($"region.{region.regionId}.name", L10n.Text(region.displayName))
                : rumored ? L10n.Get("map.rumored_path", "?")
                : L10n.Get("common.unknown_path", "아직 걷지 않은 길");
            if (levelText != null) levelText.text = detailed
                ? $"Lv.{region.recommendedLevelMin}–{region.recommendedLevelMax}"
                : "???";
            if (stateText != null) stateText.text = KnowledgeLabel(knowledge);
            if (accent != null) accent.color = detailed ? region.AccentColor
                : rumored ? new Color(.58f,.47f,.78f,1f) : new Color(.20f,.18f,.25f,1f);
            if (button != null)
            {
                button.interactable = selectable;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onSelected?.Invoke(region));
            }
            gameObject.SetActive(true);
        }

        public void SetSelected(RegionData selected)
        {
            bool active = _region != null && selected?.regionId == _region.regionId;
            if (stateText != null) stateText.text = (active ? "▶ " : "") + KnowledgeLabel(_knowledge);
        }

        private static string KnowledgeLabel(RegionMapKnowledge knowledge)
        {
            switch (knowledge)
            {
                case RegionMapKnowledge.Current: return L10n.Get("map.current", "현재 위치");
                case RegionMapKnowledge.Visited: return L10n.Get("map.visited", "방문 완료");
                case RegionMapKnowledge.RouteKnown: return L10n.Get("map.route_known", "경로 발견");
                case RegionMapKnowledge.Rumored: return L10n.Get("map.rumor", "소문");
                default: return L10n.Get("common.locked", "잠김");
            }
        }
    }
}
