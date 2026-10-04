using System;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class EndingGalleryEntryView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image accent;
        [SerializeField] private Text numberText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text lockText;

        public void Bind(int index, EndingDefinition ending, bool unlocked, Color color,
                         Action<EndingDefinition, bool> onSelected)
        {
            if (numberText != null) numberText.text = $"ENDING {index + 1:00}";
            if (titleText != null) titleText.text = unlocked
                ? L10n.Get($"ending.{ending.endingId}.title", L10n.Text(ending.title)) : "???";
            if (subtitleText != null) subtitleText.text = unlocked
                ? L10n.Get($"ending.{ending.endingId}.subtitle", L10n.Text(ending.subtitle))
                : L10n.Get("ending.unrecorded_sub", "아직 기록되지 않은 결말");
            if (lockText != null) lockText.text = unlocked
                ? L10n.Get("common.complete", "기록 완료") : L10n.Get("common.locked", "잠김");
            if (accent != null) accent.color = unlocked ? color : new Color(.24f,.22f,.30f,1f);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onSelected?.Invoke(ending, unlocked));
            }
            gameObject.SetActive(true);
        }
    }
}
