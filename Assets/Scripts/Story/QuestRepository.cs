using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.Story
{
    public static class QuestRepository
    {
        private const string ResourcePath = "Data/QuestCatalog";
        private static Dictionary<string, QuestDefinition> _byId;
        private static List<QuestDefinition> _all;

        public static IReadOnlyList<QuestDefinition> All
        {
            get { EnsureLoaded(); return _all; }
        }

        public static bool TryGet(string questId, out QuestDefinition quest)
        {
            EnsureLoaded();
            return _byId.TryGetValue(questId ?? string.Empty, out quest);
        }

        public static void Reload()
        {
            _byId = null;
            _all = null;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, QuestDefinition>(StringComparer.Ordinal);
            _all = new List<QuestDefinition>();

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError($"[Quest] Resources/{ResourcePath}.json을 찾지 못했습니다.");
                return;
            }

            QuestCatalogData catalog;
            try
            {
                catalog = JsonUtility.FromJson<QuestCatalogData>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Quest] JSON 파싱 실패: {e.Message}");
                return;
            }

            if (catalog?.quests == null) return;
            foreach (var quest in catalog.quests)
            {
                if (quest == null || string.IsNullOrWhiteSpace(quest.questId)) continue;
                if (_byId.ContainsKey(quest.questId))
                {
                    Debug.LogWarning($"[Quest] 중복 ID 무시: {quest.questId}");
                    continue;
                }
                _byId.Add(quest.questId, quest);
                _all.Add(quest);
            }
        }
    }
}
