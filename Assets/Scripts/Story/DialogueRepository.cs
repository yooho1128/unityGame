using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>대사 JSON을 한 번만 읽고 dialogueId로 제공하는 경량 저장소.</summary>
    public static class DialogueRepository
    {
        private const string ResourcePath = "Data/DialogueCatalog";
        private static Dictionary<string, DialogueSequence> _byId;

        public static bool TryGet(string dialogueId, out DialogueSequence sequence)
        {
            EnsureLoaded();
            return _byId.TryGetValue(dialogueId ?? string.Empty, out sequence);
        }

        public static void Reload()
        {
            _byId = null;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, DialogueSequence>(StringComparer.Ordinal);

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError($"[Dialogue] Resources/{ResourcePath}.json을 찾지 못했습니다.");
                return;
            }

            DialogueCatalogData catalog;
            try
            {
                catalog = JsonUtility.FromJson<DialogueCatalogData>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Dialogue] JSON 파싱 실패: {e.Message}");
                return;
            }

            if (catalog?.dialogues == null) return;
            foreach (var dialogue in catalog.dialogues)
            {
                if (dialogue == null || string.IsNullOrWhiteSpace(dialogue.dialogueId)) continue;
                if (_byId.ContainsKey(dialogue.dialogueId))
                {
                    Debug.LogWarning($"[Dialogue] 중복 ID 무시: {dialogue.dialogueId}");
                    continue;
                }
                _byId.Add(dialogue.dialogueId, dialogue);
            }
        }
    }
}
