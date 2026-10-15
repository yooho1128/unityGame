using System;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>일곱 극단 마을의 소문과 전문 판매품을 한곳에서 정의한다.</summary>
    public static class TownServicePolicy
    {
        private static readonly HashSet<string> CommonItems = new HashSet<string>(StringComparer.Ordinal)
        { "minor_potion", "potion", "salve" };

        private static readonly Dictionary<string, string[]> Specialties =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "prologue_theater", new[] { "tonic" } },
                { "cinder_city", new[] { "ink", "tonic" } },
                { "frost_port", new[] { "rubber_boat", "greater_potion" } },
                { "white_archive", new[] { "greater_potion", "ink" } },
                { "howl_village", new[] { "focus_elixir", "greater_potion" } },
                { "thread_market", new[] { "silver_ink", "focus_elixir" } },
                { "erased_station", new[] { "silver_ink", "ink", "focus_elixir" } }
            };

        private static readonly Dictionary<string, string> Rumors =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "prologue_theater", "town_rumor_prologue" },
                { "cinder_city", "town_rumor_cinder" },
                { "frost_port", "town_rumor_frost_port" },
                { "white_archive", "town_rumor_white_archive" },
                { "howl_village", "town_rumor_howl" },
                { "thread_market", "town_rumor_thread" },
                { "erased_station", "town_rumor_station" }
            };

        public static bool IsConfigured(RegionData region) =>
            region != null && region.isSettlement && Specialties.ContainsKey(region.regionId) &&
            Rumors.ContainsKey(region.regionId);

        public static bool AllowsItem(RegionData region, ItemData item)
        {
            if (!IsConfigured(region) || item == null || item.isEventOnly ||
                item.shopUnlockAct > Mathf.Max(1, region.act)) return false;
            return CommonItems.Contains(item.itemId) || IsSpecialty(region, item.itemId);
        }

        public static bool IsSpecialty(RegionData region, string itemId)
        {
            return region != null && itemId != null &&
                   Specialties.TryGetValue(region.regionId, out string[] ids) && ids.Contains(itemId);
        }

        public static string RumorDialogueId(RegionData region)
        {
            return region != null && Rumors.TryGetValue(region.regionId, out string id) ? id : string.Empty;
        }

        public static IReadOnlyList<string> SpecialtyItemIds(RegionData region)
        {
            return region != null && Specialties.TryGetValue(region.regionId, out string[] ids)
                ? ids : Array.Empty<string>();
        }
    }

    public class TownHealerNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private string regionId;
        [SerializeField, Min(0)] private int restCost = 30;

        public void Interact(PlayerController player)
        {
            RegionData current = RegionRepository.GetByScene(SaveManager.Current?.mapId);
            if (current == null || current.regionId != regionId || !current.isSettlement) return;
            bool success = SaveManager.TryRestParty(restCost, out string message);
            if (success) SaveManager.Instance?.Save();
            SaveFeedbackController.Instance?.ShowMessage(message);
        }
    }

    public class TownRumorNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private string regionId;
        [SerializeField] private string dialogueId;
        [SerializeField] private Color accent = new Color(.72f, .62f, 1f, 1f);

        public void Interact(PlayerController player)
        {
            RegionData current = RegionRepository.GetByScene(SaveManager.Current?.mapId);
            if (current == null || current.regionId != regionId || !current.isSettlement) return;
            DialogueController dialogue = DialogueController.Instance;
            if (dialogue == null || dialogue.IsPlaying) return;
            dialogue.Play(dialogueId, accent, () =>
            {
                SaveManager.SetFlag("heard_rumor_" + regionId);
                SaveManager.Instance?.Save();
            });
        }
    }
}
