#if UNITY_EDITOR
using System.Collections.Generic;
using ShadowTheater.Data;

namespace ShadowTheater.EditorTools
{
    public static class TrainingItemRegression
    {
        public static void Validate(List<string> errors)
        {
            string[] ids = { "rehearsal_script", "annotated_script", "complete_script" };
            int[] starts = { 1, 20, 50 }, caps = { 20, 50, 80 };
            for (int i = 0; i < ids.Length; i++)
            {
                var rule = TrainingItemPolicy.Find(ids[i]);
                if (rule == null || rule.minimumLevel != starts[i] || rule.maximumLevel != caps[i])
                { errors.Add("경험치 대본 성장 구간 누락: " + ids[i]); continue; }
                if (TrainingItemPolicy.TryGrant(rule, caps[i], 0, 2400, ShadowInstance.ExperienceToNext, out _, out _))
                    errors.Add("상한 레벨에서 대본 사용 허용: " + ids[i]);
                int level = caps[i] - 1;
                if (!TrainingItemPolicy.TryGrant(rule, level, ShadowInstance.ExperienceToNext(level) - 7,
                    2400, ShadowInstance.ExperienceToNext, out int granted, out _) || granted != 7)
                    errors.Add("대본 상한 초과 경험치 지급: " + ids[i]);
            }
        }
    }
}
#endif
