using ShadowTheater.Core;
using UnityEngine;

namespace ShadowTheater.Data
{
    [CreateAssetMenu(fileName = "Skill_New", menuName = "Shadow Theater/Data/Skill")]
    public sealed class SkillData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string skillId;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;

        [Header("Battle")]
        [SerializeField] private SkillType skillType = SkillType.Attack;
        [SerializeField] private SkillTarget target = SkillTarget.Opponent;
        [SerializeField] private ShadowElement element = ShadowElement.None;
        [Min(0)]
        [SerializeField] private int focusCost = 1;
        [Min(0)]
        [SerializeField] private int power = 10;
        [Range(0f, 1f)]
        [SerializeField] private float accuracy = 1f;
        [SerializeField] private StatusEffectType statusEffect = StatusEffectType.None;
        [Range(0f, 1f)]
        [SerializeField] private float statusEffectChance;

        public string SkillId => skillId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public SkillType SkillType => skillType;
        public SkillTarget Target => target;
        public ShadowElement Element => element;
        public int FocusCost => focusCost;
        public int Power => power;
        public float Accuracy => accuracy;
        public StatusEffectType StatusEffect => statusEffect;
        public float StatusEffectChance => statusEffectChance;

        private void OnValidate()
        {
            focusCost = Mathf.Max(0, focusCost);
            power = Mathf.Max(0, power);
        }
    }
}
