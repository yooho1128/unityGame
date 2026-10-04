using UnityEngine;

namespace ShadowTheater.Data
{
    /// <summary>
    /// 스킬 정의 (ScriptableObject). 기본 공격도 SkillData 한 개로 만들어 ShadowData.basicAttack에 연결.
    /// Create > ShadowTheater > Skill Data
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_", menuName = "ShadowTheater/Skill Data", order = 1)]
    public class SkillData : ScriptableObject
    {
        [Header("기본 정보")]
        public string skillId;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("FP (공연 열기)")]
        [Tooltip("소비 FP. 기본 공격은 0")]
        [Min(0)] public int fpCost;
        [Tooltip("사용 시 획득 FP. 기본 공격은 1")]
        [Min(0)] public int fpGain;

        [Header("데미지")]
        public DamageType damageType = DamageType.Physical;
        public ShadowElement element = ShadowElement.None;
        [Tooltip("공격력 x 계수. 1.0 = 100%")]
        [Min(0f)] public float damageMultiplier = 1f;
        [Range(0f, 1f)] public float accuracy = 1f;
        [Tooltip("시전자 치명타율에 더해지는 보너스")]
        [Range(0f, 1f)] public float bonusCritRate;
        public SkillTarget target = SkillTarget.Enemy;

        [Header("회복 (Self 대상)")]
        [Tooltip("최대 HP 대비 회복 비율")]
        [Range(0f, 1f)] public float healRatio;

        [Header("상태 이상")]
        public StatusEffectType statusEffect = StatusEffectType.None;
        [Range(0f, 1f)] public float statusChance;
        [Min(0)] public int statusDuration = 2;

        [Header("연출")]
        public bool isUltimate;              // 필살기면 컷인 연출
        public UltimateFxStyle ultimateFxStyle;
        [ColorUsage(true, true)] public Color primaryFxColor = Color.white;
        [ColorUsage(true, true)] public Color secondaryFxColor = Color.white;
        [Range(6, 36)] public int ultimateBurstCount = 18;
        public GameObject fxPrefab;
        public AudioClip sfxClip;
        public AudioClip impactSfxClip;
        [Range(0f, 1f)] public float sfxVolume = 0.9f;
        [Range(0.5f, 2f)] public float sfxPitch = 1f;
        [Tooltip("타격 순간 전체 시간을 멈추는 시간(초)")]
        [Range(0f, 0.15f)] public float hitStopDuration = 0.04f;
        [Tooltip("타격 시 카메라 흔들림 강도")]
        [Min(0f)] public float cameraShake = 0.2f;

        public bool DealsDamage => damageType != DamageType.None && damageMultiplier > 0f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(skillId)) skillId = name;
        }
#endif
    }
}
