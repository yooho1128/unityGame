using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>
    /// 특별 이벤트가 아닌 평소 탐험에서도 동행이 지역·시간·날씨를 인지하고 반응하게 한다.
    /// 동일한 하늘에 대한 대사는 하루에 한 번만 기록되어 탐험 흐름을 방해하지 않는다.
    /// </summary>
    public sealed class CompanionAmbientCommentary : MonoBehaviour
    {
        [SerializeField] private string regionId;
        [SerializeField, Min(8)] private int stepsBeforeComment = 28;
        private PlayerController _player;
        private int _steps;

        private void Start()
        {
            _player = PlayerController.Instance;
            if (_player != null) _player.OnStepFinished += OnStepFinished;
        }

        private void OnDestroy()
        {
            if (_player != null) _player.OnStepFinished -= OnStepFinished;
        }

        private void OnStepFinished(Vector2Int _)
        {
            if (++_steps < stepsBeforeComment || TownInteriorController.Instance?.IsInside == true) return;
            RegionData region = RegionRepository.Get(regionId);
            ShadowInstance companion = CompanionBondService.CurrentCompanion();
            if (region == null || companion?.Data == null) return;
            FieldWorldSnapshot sky = FieldWorldState.Current(region);
            string key = $"ambient_comment_{regionId}_{sky.dayIndex}_{(int)sky.timeOfDay}_{(int)sky.weather}";
            if (SaveManager.HasFlag(key)) return;

            SaveManager.SetFlag(key);
            CompanionBondService.AddExperience(companion.instanceId, 2, out _, out int level);
            SaveManager.Instance?.TrySave(false);
            string name = L10n.Text(companion.DisplayName);
            SaveFeedbackController.Instance?.ShowMessage(
                $"{name}: {Comment(companion.Data, region, sky)}\n동행 유대 +2 · 유대 Lv.{level}");
        }

        private static string Comment(ShadowData shadow, RegionData region, FieldWorldSnapshot sky)
        {
            if (region.isSettlement)
            {
                if (TownRestorationService.Stage(region.regionId) >= 3)
                    return "사람들의 발소리가 다시 우리 박자와 맞는다.";
                return "아직 빈 무대가 많아. 그래도 등불은 꺼지지 않았어.";
            }
            switch (sky.weather)
            {
                case FieldWeather.Rain:
                    return shadow.element == ShadowElement.Frost
                        ? "빗방울 사이의 기억이 선명해져. 지금이라면 갈 수 있어."
                        : "물소리가 대사를 지워도, 네 발소리는 놓치지 않을게.";
                case FieldWeather.Mist: return "안개는 길을 지워도 이야기까지 지우지는 못해.";
                case FieldWeather.Ashfall: return "잿빛 발자국이 남네. 돌아올 길도 표시해 두자.";
                case FieldWeather.Snow: return "눈이 소리를 먹고 있어. 평소보다 천천히 보면 단서가 보일 거야.";
                case FieldWeather.MoonWind: return "달바람이 다음 장면을 먼저 넘기려고 해.";
                case FieldWeather.ThreadDust: return "실의 끝이 여기를 향해 휘어져 있어. 누군가 지나갔나 봐.";
                case FieldWeather.InkStorm: return "먹물이 짙어질수록 숨긴 글자는 더 흰 색으로 드러나.";
                case FieldWeather.Starfall: return "별빛이 멈추는 곳이 있어. 희귀한 기억이 가까운가 봐.";
            }
            if (sky.timeOfDay == FieldTimeOfDay.Night) return "밤이 되면 낮에 숨은 무대 표식이 보여.";
            return shadow.role == ShadowRole.SpeedUtility
                ? "바람이 바뀌었어. 앞쪽 길을 먼저 살펴볼게."
                : "이 지역의 박자를 익혔어. 이제 우리 무대처럼 걸을 수 있어.";
        }
    }
}
