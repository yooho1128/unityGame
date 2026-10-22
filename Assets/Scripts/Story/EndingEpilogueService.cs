using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.UI;

namespace ShadowTheater.Story
{
    /// <summary>마지막 선택 외에 동행, 마을, 수집과 이전 회차를 결말 후일담으로 보존한다.</summary>
    public static class EndingEpilogueService
    {
        public static string Build(EndingDefinition ending)
        {
            SaveData save = SaveManager.Current;
            if (save == null || ending == null) return string.Empty;
            ShadowInstance companion = CompanionBondService.CurrentCompanion();
            string companionName = companion != null ? L10n.Text(companion.DisplayName) : "이름 없는 그림자";
            int restoredTowns = save.townRestorations?.Count(x => x != null &&
                TownRestorationService.Stage(x.regionId) >= 3) ?? 0;
            int trueNames = (save.party?.Count(x => x?.memoryStage == MemoryStage.TrueName) ?? 0) +
                            (save.storage?.Count(x => x?.memoryStage == MemoryStage.TrueName) ?? 0);
            string townLine = restoredTowns >= 7
                ? "일곱 마을은 각자의 이름으로 새 공연을 준비했다."
                : restoredTowns > 0 ? $"되살아난 {restoredTowns}개 마을에서 다시 등불이 켜졌다."
                : "아직 불이 돌아오지 않은 마을들은 다음 회차의 발걸음을 기다렸다.";
            string collectionLine = trueNames > 0
                ? $"진명을 되찾은 {trueNames}명의 배역이 자신의 후일담을 직접 기록했다."
                : "각본집의 빈 진명란은 아직 선택되지 않은 가능성으로 남았다.";
            return $"{companionName}은(는) 마지막 장면 뒤에도 극단주의 곁에 남았다.\n{townLine}\n{collectionLine}";
        }

        public static string Hint(EndingDefinition ending)
        {
            if (ending?.requirements == null) return "마지막 무대에서 지금까지의 선택이 결말을 정합니다.";
            foreach (EndingFlagRequirement requirement in ending.requirements)
            {
                int current = SaveManager.GetFlag(requirement.flag);
                bool met = current >= requirement.minValue &&
                           (requirement.maxValue == 0 || current <= requirement.maxValue);
                if (met) continue;
                if (requirement.flag == "ending_archive_count") return "여러 회차의 서로 다른 결말이 한 권의 각본으로 모여야 합니다.";
                if (requirement.flag.Contains("memory")) return "지우기보다 기억하기를 선택한 흔적이 더 필요합니다.";
                if (requirement.flag.Contains("compassion")) return "그림자가 스스로 선택할 시간을 허락해 보세요.";
                if (requirement.flag.Contains("control")) return "완벽한 통제와 자유 사이에서 다른 답을 찾아보세요.";
                return "아직 마지막 무대로 이어지는 중요한 장면이 기록되지 않았습니다.";
            }
            return "이 결말에 닿을 선택의 흔적이 모두 모였습니다.";
        }

        public static EndingRecordData Latest(string endingId) => SaveManager.Current?.endingHistory?
            .Where(x => x != null && x.endingId == endingId).OrderByDescending(x => x.cycle).FirstOrDefault();
    }
}
