using System.Collections.Generic;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>타이틀에서 해금한 결말과 회차 진행을 열람하는 엔딩 도감.</summary>
    public class EndingGalleryController : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private GameObject titleRoot;
        [SerializeField] private Transform content;
        [SerializeField] private EndingGalleryEntryView entryTemplate;
        [SerializeField] private Text progressText;
        [SerializeField] private Text cycleText;
        [SerializeField] private Text detailTitle;
        [SerializeField] private Text detailSubtitle;
        [SerializeField] private Text detailState;

        private readonly List<GameObject> _entries = new List<GameObject>();

        private void Awake()
        {
            if (entryTemplate != null) entryTemplate.gameObject.SetActive(false);
            if (root != null) root.SetActive(false);
        }

        public void Open()
        {
            EnsureSaveLoaded();
            if (titleRoot != null) titleRoot.SetActive(false);
            if (root != null) root.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
            if (titleRoot != null) titleRoot.SetActive(true);
        }

        public void Refresh()
        {
            ClearEntries();
            if (entryTemplate == null || content == null) return;
            var save = SaveManager.Current;
            int unlockedCount = 0;
            int index = 0;
            foreach (EndingDefinition ending in EndingRepository.All)
            {
                if (ending == null) continue;
                bool unlocked = save?.unlockedEndingIds != null && save.unlockedEndingIds.Contains(ending.endingId);
                if (unlocked) unlockedCount++;
                var entry = Instantiate(entryTemplate, content);
                entry.Bind(index, ending, unlocked, Accent(index), Select);
                _entries.Add(entry.gameObject);
                index++;
            }

            if (progressText != null) progressText.text = $"기록한 결말  {unlockedCount} / {index}";
            if (cycleText != null) cycleText.text = save == null
                ? "아직 시작되지 않은 기억"
                : $"현재 {Mathf.Max(1, save.cycle)}회차 · 완주 {save.completedCycles}회";
            if (detailTitle != null) detailTitle.text = "결말 기록을 선택하세요";
            if (detailSubtitle != null) detailSubtitle.text = "선택에 따라 달라진 세계의 마지막 장면";
            if (detailState != null) detailState.text = unlockedCount == index && index > 0 ? "모든 결말 기록 완료" : "잠긴 결말은 다른 선택에서 해금됩니다";
        }

        private void Select(EndingDefinition ending, bool unlocked)
        {
            if (detailTitle != null) detailTitle.text = unlocked ? ending.title : "기록되지 않은 결말";
            if (detailSubtitle != null) detailSubtitle.text = unlocked
                ? (!string.IsNullOrEmpty(ending.archiveText) ? ending.archiveText : ending.subtitle)
                : "다른 선택과 진명으로 마지막 무대에 도달하세요.";
            if (detailState != null) detailState.text = unlocked ? $"기록 ID · {ending.endingId}" : "???";
        }

        private static void EnsureSaveLoaded()
        {
            if (SaveManager.Current == null && SaveManager.Instance != null && SaveManager.Instance.HasSave())
                SaveManager.Instance.Load();
        }

        private void ClearEntries()
        {
            foreach (GameObject entry in _entries) if (entry != null) Destroy(entry);
            _entries.Clear();
        }

        private static Color Accent(int index)
        {
            switch (index % 4)
            {
                case 0: return new Color(.88f,.32f,.48f,1f);
                case 1: return new Color(.42f,.92f,.82f,1f);
                case 2: return new Color(.72f,.48f,1f,1f);
                default: return new Color(1f,.76f,.38f,1f);
            }
        }
    }
}
