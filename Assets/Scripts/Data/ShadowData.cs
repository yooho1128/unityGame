using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.Data
{
    /// <summary>
    /// 그림자(영웅) 원본 정의 (ScriptableObject). 레벨/현재 HP 등 개체별 값은 ShadowInstance에 저장.
    /// Create > ShadowTheater > Shadow Data
    /// </summary>
    [CreateAssetMenu(fileName = "Shadow_", menuName = "ShadowTheater/Shadow Data", order = 0)]
    public class ShadowData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("세이브/도감 키. 변경 금지")]
        public string shadowId;
        public string displayName;
        public string title;                 // 예: "멸망한 왕국의 기사"
        public ShadowElement element;
        public ShadowRole role;

        [Header("비주얼 (실루엣 + 포인트 컬러)")]
        public Sprite silhouetteSprite;
        [ColorUsage(true, true)] public Color accentColor = Color.white; // 눈/무기 발광 (HDR)
        public RuntimeAnimatorController animator; // 선택. 없으면 DOTween 연출만 사용

        [Header("기본 스탯 (Lv.1)")]
        [Min(1)] public int baseHp = 100;
        [Min(0)] public int baseAtk = 20;
        [Min(0)] public int baseDef = 10;
        [Min(0)] public int baseSpd = 10;
        [Range(0f, 1f)] public float critRate = 0.05f;
        [Range(0f, 1f)] public float evasion;

        [Header("레벨당 성장치")]
        public float hpGrowth = 10f;
        public float atkGrowth = 2f;
        public float defGrowth = 1f;
        public float spdGrowth = 0.5f;

        [Header("스킬")]
        public SkillData basicAttack;        // [공격] 버튼 - FP +1
        public List<SkillData> skills = new List<SkillData>(); // [스킬] 버튼 목록 (최대 4 권장)

        [Header("수집 (각본 기록)")]
        [Tooltip("기본 포획률. 0이면 포획 불가(보스 등)")]
        [Range(0f, 1f)] public float baseCaptureRate = 0.45f;
        public int expReward = 30;
        public int goldReward = 10;

        [Header("도감 Lore")]
        [TextArea(3, 8)] public string loreLocked;    // 미수집 시 표시
        [TextArea(5, 15)] public string loreUnlocked; // 수집 후 해금되는 비극 스토리

        public int GetHp(int level) => Mathf.RoundToInt(baseHp + hpGrowth * (level - 1));
        public int GetAtk(int level) => Mathf.RoundToInt(baseAtk + atkGrowth * (level - 1));
        public int GetDef(int level) => Mathf.RoundToInt(baseDef + defGrowth * (level - 1));
        public int GetSpd(int level) => Mathf.RoundToInt(baseSpd + spdGrowth * (level - 1));

        public bool IsCapturable => baseCaptureRate > 0f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(shadowId)) shadowId = name;
        }
#endif
    }
}
