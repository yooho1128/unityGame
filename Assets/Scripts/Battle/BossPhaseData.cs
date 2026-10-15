using System;
using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Battle
{
    [Serializable]
    public class BossPhaseCatalogData
    {
        public List<BossPhaseDefinition> phases = new List<BossPhaseDefinition>();
    }

    [Serializable]
    public class BossPhaseDefinition
    {
        public string phaseId;
        public string bossShadowId;
        [Range(.1f, .9f)] public float hpThreshold = .5f;
        public string message;
        [Range(0, 5)] public int enemyFpGain = 2;
        [Range(0, 5)] public int playerFpDrain;
        [Range(0f, .5f)] public float healRatio;
        public bool cleanseBoss;
        public string playerStatus;
        [Range(1, 5)] public int statusDuration = 2;

        public bool TryGetPlayerStatus(out StatusEffectType status) =>
            Enum.TryParse(playerStatus, true, out status) && status != StatusEffectType.None;

        public string PhaseName
        {
            get
            {
                switch (phaseId)
                {
                    case "captain_last_order": return "마지막 명령";
                    case "guard_missing_head": return "잃어버린 왕의 목소리";
                    case "king_ash_crown": return "되살아난 재의 왕관";
                    case "drake_swallowed_chapter": return "삼켜진 마지막 장";
                    case "librarian_reversed_ending": return "뒤집힌 결말";
                    case "archive_infinite_index": return "무한 색인";
                    case "bloodmane_final_hunt": return "마지막 사냥";
                    case "beast_devours_night": return "빛을 삼킨 밤";
                    case "primadonna_wire_cadenza": return "광란의 카덴차";
                    case "puppeteer_empty_hands": return "빈손의 조종";
                    case "warden_zero_sentence": return "제0호 형량";
                    case "subject_overflow": return "원한 과부하";
                    case "nox_black_revision": return "검은 개정";
                    case "painter_drowned_palette": return "가라앉은 팔레트";
                    case "keeper_funeral_bell": return "모든 바다의 조종";
                    case "manager_endless_interlude": return "끝없는 막간";
                    case "rival_shared_script": return "겹쳐 쓴 각본";
                    case "outside_script_blank_stage": return "백지 무대";
                    default: return "숨겨진 장면";
                }
            }
        }
    }

    public static class BossPhaseRepository
    {
        private const string ResourcePath = "Data/BossPhaseCatalog";
        private static Dictionary<string, BossPhaseDefinition> _byShadowId;

        public static BossPhaseDefinition Get(string shadowId)
        {
            EnsureLoaded();
            return shadowId != null && _byShadowId.TryGetValue(shadowId, out BossPhaseDefinition phase)
                ? phase : null;
        }

        public static void Reload() => _byShadowId = null;

        private static void EnsureLoaded()
        {
            if (_byShadowId != null) return;
            _byShadowId = new Dictionary<string, BossPhaseDefinition>(StringComparer.Ordinal);
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) return;
            BossPhaseCatalogData catalog = JsonUtility.FromJson<BossPhaseCatalogData>(asset.text);
            foreach (BossPhaseDefinition phase in catalog?.phases ?? new List<BossPhaseDefinition>())
                if (phase != null && !string.IsNullOrWhiteSpace(phase.bossShadowId) &&
                    !_byShadowId.ContainsKey(phase.bossShadowId))
                    _byShadowId.Add(phase.bossShadowId, phase);
        }
    }
}
