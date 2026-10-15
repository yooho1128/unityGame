using System;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    public readonly struct BossInvestigationSnapshot
    {
        public readonly string bossShadowId, bossName, weaknessHint, phaseHint, bossRegionId, bossRegionName, sourceTownName;
        public readonly ShadowElement weakness;
        public readonly int cluesFound, totalClues;
        public BossInvestigationSnapshot(string id, string name, ShadowElement weakness, string weaknessHint,
            string phaseHint, int found, int total, string bossRegionId = "", string bossRegionName = "", string sourceTownName = "")
        { bossShadowId = id; bossName = name; this.weakness = weakness; this.weaknessHint = weaknessHint;
          this.phaseHint = phaseHint; cluesFound = found; totalClues = total; this.bossRegionId = bossRegionId;
          this.bossRegionName = bossRegionName; this.sourceTownName = sourceTownName; }
    }

    /// <summary>마을 조사 NPC의 단서를 보스 속성·페이즈 데이터와 연결한다.</summary>
    public static class BossInvestigationService
    {
        public const int TotalClues = 3;
        private const string Prefix = "boss_clue_";

        public static BossInvestigationSnapshot ForBoss(ShadowData boss)
        {
            if (boss == null) return default;
            BossPhaseDefinition phase = BossPhaseRepository.Get(boss.shadowId);
            ShadowElement weakness = WeaknessOf(boss.element);
            int found = Mathf.Clamp(SaveManager.GetFlag(Prefix + boss.shadowId), 0, TotalClues);
            RegionData bossRegion = RegionRepository.Ordered().FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.bossShadowName) && Normalize(x.bossShadowName) == Normalize(boss.displayName));
            RegionData town = bossRegion == null ? null : RegionRepository.Ordered().FirstOrDefault(x => x.act == bossRegion.act && x.isSettlement);
            return new BossInvestigationSnapshot(boss.shadowId, boss.displayName, weakness,
                WeaknessHint(weakness), PhaseHint(phase), found, TotalClues, bossRegion?.regionId ?? string.Empty,
                bossRegion?.displayName ?? "미확인 지역", town?.displayName ?? "떠돌이 조사관");
        }

        public static IReadOnlyList<BossInvestigationSnapshot> JournalEntries()
        {
            if (ShadowDatabase.Instance == null) return Array.Empty<BossInvestigationSnapshot>();
            return RegionRepository.Ordered().Where(x => !string.IsNullOrWhiteSpace(x.bossShadowName))
                .Select(x => FindByName(x.bossShadowName)).Where(x => x != null)
                .GroupBy(x => x.shadowId).Select(x => ForBoss(x.First())).ToArray();
        }

        public static string ClueText(BossInvestigationSnapshot info, int clueIndex)
        {
            if (clueIndex < 1 || clueIndex > TotalClues) return string.Empty;
            if (clueIndex > info.cluesFound) return "아직 발견하지 못한 단서";
            return clueIndex == 1 ? info.weaknessHint
                : clueIndex == 2 ? info.phaseHint
                : $"약점은 {ElementName(info.weakness)}. 완전 조사 보정이 활성화된다.";
        }

        public static string ClueSource(BossInvestigationSnapshot info, int clueIndex) => clueIndex == 1
            ? $"목격자 · {info.sourceTownName}" : clueIndex == 2
                ? $"무대 기록관 · {info.bossRegionName}" : $"현장 조사 · {info.bossRegionName}";

        public static int ClueCount(string bossShadowId) =>
            Mathf.Clamp(SaveManager.GetFlag(Prefix + bossShadowId), 0, TotalClues);

        public static bool DiscoverNext(ShadowData boss, out string clue)
        {
            clue = string.Empty;
            if (boss == null) return false;
            BossInvestigationSnapshot info = ForBoss(boss);
            if (info.cluesFound >= TotalClues)
            {
                clue = $"{info.bossName} 조사는 이미 끝났다. 약점은 {ElementName(info.weakness)}이다.";
                return false;
            }
            int next = info.cluesFound + 1;
            SaveManager.SetFlag(Prefix + boss.shadowId, next);
            clue = next == 1
                ? $"목격자는 {info.bossName}의 흔적 주변에서 {info.weaknessHint}고 말했다."
                : next == 2 ? $"남겨진 무대 기록을 대조했다. {info.phaseHint}"
                : $"조사가 완성됐다. {info.bossName}의 약점은 {ElementName(info.weakness)}. 전투 준비 보정이 활성화된다.";
            return true;
        }

        public static ShadowData NextBossForAct(int act)
        {
            if (ShadowDatabase.Instance == null) return null;
            return RegionRepository.Ordered().Where(x => x.act == act && !string.IsNullOrWhiteSpace(x.bossShadowName))
                .Select(x => FindByName(x.bossShadowName)).Where(x => x != null && BossPhaseRepository.Get(x.shadowId) != null)
                .OrderBy(x => ClueCount(x.shadowId)).FirstOrDefault(x => ClueCount(x.shadowId) < TotalClues)
                ?? RegionRepository.Ordered().Where(x => x.act == act && !string.IsNullOrWhiteSpace(x.bossShadowName))
                    .Select(x => FindByName(x.bossShadowName)).FirstOrDefault(x => x != null);
        }

        public static int PreparationLevel(ShadowData boss) => boss == null ? 0 : ClueCount(boss.shadowId);
        public static int ReducePhaseFp(int value, int preparationLevel) => Mathf.Max(0, value - (preparationLevel >= 3 ? 1 : 0));
        public static float PhaseHealMultiplier(int preparationLevel) => preparationLevel >= 3 ? .5f : 1f;

        public static ShadowElement WeaknessOf(ShadowElement element)
        {
            switch (element)
            {
                case ShadowElement.Flame: return ShadowElement.Shade;
                case ShadowElement.Frost: return ShadowElement.Flame;
                case ShadowElement.Shade: return ShadowElement.Frost;
                default: return ShadowElement.None;
            }
        }

        private static ShadowData FindByName(string name)
        {
            string key = Normalize(name);
            return ShadowDatabase.Instance.shadows.FirstOrDefault(x => x != null && Normalize(x.displayName) == key);
        }
        private static string Normalize(string value) => new string((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '·').ToArray());
        private static string WeaknessHint(ShadowElement element) => element == ShadowElement.Flame ? "타지 않은 불씨가 역풍을 기다린다"
            : element == ShadowElement.Frost ? "얼음 아래 서리가 더 차가운 숨을 낸다"
            : element == ShadowElement.Shade ? "그림자 가장자리만 유난히 짙게 흔들린다" : "어느 색의 흔적도 남지 않는다";
        private static string PhaseHint(BossPhaseDefinition phase) => phase == null ? "체력이 절반 아래로 내려가면 숨겨진 장면이 시작된다."
            : $"체력이 약 {Mathf.RoundToInt(phase.hpThreshold * 100f)}%가 되면 [{phase.PhaseName}]이 시작된다.";
        public static string ElementName(ShadowElement value) => value == ShadowElement.Flame ? "불꽃"
            : value == ShadowElement.Frost ? "서리" : value == ShadowElement.Shade ? "그림자" : "무속성";
    }

    public class BossInvestigatorNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private int act;
        public void Interact(PlayerController player)
        {
            ShadowData boss = BossInvestigationService.NextBossForAct(act);
            if (boss == null) { SaveFeedbackController.Instance?.ShowMessage("이 생활권에서 더 조사할 보스 흔적은 없습니다."); return; }
            BossInvestigationService.DiscoverNext(boss, out string clue);
            SaveManager.Instance?.Save();
            SaveFeedbackController.Instance?.ShowMessage(clue);
        }
    }
}
