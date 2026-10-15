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

        private static readonly Dictionary<string, string> RumorQuests =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "prologue_theater", "rumor_forgotten_tent" },
                { "cinder_city", "rumor_rooftop_rehearsal" },
                { "frost_port", "rumor_bubble_cave" },
                { "white_archive", "rumor_shadow_well" },
                { "howl_village", "rumor_sealed_mask" },
                { "thread_market", "rumor_toy_store" },
                { "erased_station", "rumor_silent_foyer" }
            };

        private static readonly Dictionary<string, string> HiddenRumorSources =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "forgotten_tent", "prologue_theater" }, { "silent_clown_stage", "prologue_theater" },
                { "ashes_bell_tower", "prologue_theater" },
                { "rooftop_rehearsal", "cinder_city" }, { "sealed_small_theater", "cinder_city" },
                { "bubble_cave", "frost_port" }, { "whale_back_stage", "frost_port" },
                { "shadow_well", "white_archive" }, { "matte_projection_room", "white_archive" },
                { "sealed_mask_room", "howl_village" }, { "moon_eater_ritual", "howl_village" },
                { "inverted_moon_path", "howl_village" }, { "windless_echo_cavern", "howl_village" },
                { "abandoned_toy_store", "thread_market" }, { "clockwork_heart_stage", "thread_market" },
                { "silent_foyer", "erased_station" }, { "ghost_orchestra_pit", "erased_station" }
            };

        private static readonly Dictionary<string, string> HiddenHints =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "prologue_theater", "지워지지 않는 수레바퀴 자국과 세 번의 종소리가 같은 방향을 가리킨다." },
                { "cinder_city", "찢어진 포스터의 시선과 벽 뒤의 짧은 박수가 위쪽 샛길을 암시한다." },
                { "frost_port", "빈 배의 이름이 담긴 거품과 돌아오지 않는 귀항 신호를 따라가야 한다." },
                { "white_archive", "낮은 울음에 켜지는 갈대 등불과 물을 거슬러 오르는 글자가 길을 비춘다." },
                { "howl_village", "달빛을 거스르는 발자국과 바람 없는 날갯소리 사이에 숨은 길이 있다." },
                { "thread_market", "벌판의 붉은 실과 한 박자로 맞춰지는 태엽 소리가 같은 곳으로 이어진다." },
                { "erased_station", "표 없는 승객이 기다리는 벽과 보이지 않는 악단의 첫 숨을 살펴야 한다." }
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

        public static string RumorQuestId(string regionId) =>
            regionId != null && RumorQuests.TryGetValue(regionId, out string id) ? id : string.Empty;

        public static IReadOnlyList<string> SpecialtyItemIds(RegionData region)
        {
            return region != null && Specialties.TryGetValue(region.regionId, out string[] ids)
                ? ids : Array.Empty<string>();
        }

        public static string RumorSourceRegionId(RegionData hiddenRegion)
        {
            return hiddenRegion != null && HiddenRumorSources.TryGetValue(hiddenRegion.regionId, out string id)
                ? id : string.Empty;
        }

        public static bool CanShowHiddenHint(RegionData hiddenRegion)
        {
            SaveData save = SaveManager.Current;
            string source = RumorSourceRegionId(hiddenRegion);
            if (save == null || hiddenRegion?.isHidden != true || string.IsNullOrEmpty(source)) return false;
            bool heardRumor = SaveManager.HasFlag("heard_rumor_" + source);
            bool requirementMet = string.IsNullOrEmpty(hiddenRegion.requiredFlag) ||
                                  SaveManager.HasFlag(hiddenRegion.requiredFlag);
            bool adjacentKnown = (hiddenRegion.connectedRegionIds ?? Array.Empty<string>()).Any(id =>
                save.visitedRegionIds.Contains(id) || save.unlockedRegionIds.Contains(id) ||
                RegionRepository.Get(id)?.sceneName == save.mapId);
            return ShouldShowHiddenHint(heardRumor, requirementMet, adjacentKnown);
        }

        public static bool ShouldShowHiddenHint(bool heardRumor, bool requirementMet, bool adjacentKnown) =>
            heardRumor && requirementMet && adjacentKnown;

        public static string HiddenMapHint(RegionData hiddenRegion)
        {
            string source = RumorSourceRegionId(hiddenRegion);
            return HiddenHints.TryGetValue(source, out string hint)
                ? hint : "인접 지역의 소문과 환경 단서를 더 살펴봐야 한다.";
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
                string questId = TownServicePolicy.RumorQuestId(regionId);
                if (!string.IsNullOrEmpty(questId)) QuestManager.Instance?.TryStartQuest(questId, false);
                SaveManager.Instance?.Save();
            });
        }
    }
}
