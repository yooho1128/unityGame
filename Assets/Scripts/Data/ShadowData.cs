using System.Collections.Generic;
using ShadowTheater.Core;
using UnityEngine;

namespace ShadowTheater.Data
{
    [CreateAssetMenu(fileName = "Shadow_New", menuName = "Shadow Theater/Data/Shadow")]
    public sealed class ShadowData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string shadowId;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string storySummary;
        [SerializeField] private ShadowElement element = ShadowElement.None;

        [Header("Presentation")]
        [SerializeField] private Sprite portrait;
        [SerializeField] private Sprite battleSprite;
        [SerializeField] private Color pointColor = Color.white;

        [Header("Base Stats")]
        [Min(1)]
        [SerializeField] private int baseMaxHealth = 100;
        [Min(1)]
        [SerializeField] private int baseAttack = 10;
        [Min(1)]
        [SerializeField] private int baseDefense = 10;
        [Min(1)]
        [SerializeField] private int baseSpeed = 10;

        [Header("Skills")]
        [SerializeField] private List<SkillData> skills = new List<SkillData>();

        public string ShadowId => shadowId;
        public string DisplayName => displayName;
        public string StorySummary => storySummary;
        public ShadowElement Element => element;
        public Sprite Portrait => portrait;
        public Sprite BattleSprite => battleSprite;
        public Color PointColor => pointColor;
        public int BaseMaxHealth => baseMaxHealth;
        public int BaseAttack => baseAttack;
        public int BaseDefense => baseDefense;
        public int BaseSpeed => baseSpeed;
        public IReadOnlyList<SkillData> Skills => skills;

        private void OnValidate()
        {
            baseMaxHealth = Mathf.Max(1, baseMaxHealth);
            baseAttack = Mathf.Max(1, baseAttack);
            baseDefense = Mathf.Max(1, baseDefense);
            baseSpeed = Mathf.Max(1, baseSpeed);
        }
    }
}
