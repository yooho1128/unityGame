using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.Data
{
    /// <summary>
    /// 플레이어가 보유한 그림자 개체 (직렬화 가능 → SaveData JSON에 그대로 들어감).
    /// SO 참조 대신 shadowId만 저장한다.
    /// </summary>
    [Serializable]
    public class ShadowInstance
    {
        public const int MaxLevel = 100;

        public string instanceId;   // 같은 그림자 중복 수집 대비 고유 ID
        public string shadowId;
        public int level = 1;
        public int exp;
        public int currentHp = -1;  // -1 = 최대치로 초기화
        public MemoryStage memoryStage;
        public AwakeningPath awakeningPath;

        [NonSerialized] private ShadowData _data;
        public ShadowData Data => _data != null ? _data : (_data = ShadowDatabase.Instance.GetShadow(shadowId));

        public ShadowInstance() { }

        public ShadowInstance(ShadowData data, int level)
        {
            instanceId = Guid.NewGuid().ToString("N");
            shadowId = data.shadowId;
            _data = data;
            this.level = Mathf.Clamp(level, 1, MaxLevel);
            currentHp = MaxHp;
        }

        public MemoryFormData ActiveForm => Data.GetForm(memoryStage, awakeningPath);
        public int MaxHp => ApplyMultiplier(Data.GetHp(level), ActiveForm?.hpMultiplier ?? 1f);
        public int Atk => ApplyMultiplier(Data.GetAtk(level), ActiveForm?.atkMultiplier ?? 1f);
        public int Def => ApplyMultiplier(Data.GetDef(level), ActiveForm?.defMultiplier ?? 1f);
        public int Spd => ApplyMultiplier(Data.GetSpd(level), ActiveForm?.spdMultiplier ?? 1f);
        public float CritRate => Mathf.Clamp01(Data.critRate + (ActiveForm?.bonusCritRate ?? 0f));
        public float Evasion => Mathf.Clamp01(Data.evasion + (ActiveForm?.bonusEvasion ?? 0f));
        public Sprite Silhouette => ActiveForm != null && ActiveForm.silhouetteSprite != null
            ? ActiveForm.silhouetteSprite : Data.silhouetteSprite;
        public Color AccentColor => ActiveForm != null ? ActiveForm.accentColor : Data.accentColor;
        public string DisplayName => ActiveForm != null && !string.IsNullOrEmpty(ActiveForm.formName)
            ? ActiveForm.formName : Data.displayName;
        public bool IsFainted => currentHp == 0;

        public bool IsMaxLevel => level >= MaxLevel;
        public int ExpToNext => IsMaxLevel ? 0 : 20 + level * level * 5;
        public float ExpProgress => IsMaxLevel ? 1f : Mathf.Clamp01((float)exp / ExpToNext);

        /// <summary>경험치 획득. 레벨업 횟수 반환</summary>
        public int AddExp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return 0;
            int ups = 0;
            exp += amount;
            while (!IsMaxLevel && exp >= ExpToNext)
            {
                int required = ExpToNext;
                exp -= required;
                int prevMax = MaxHp;
                level++;
                currentHp += MaxHp - prevMax; // 오른 만큼 현재 HP도 증가
                ups++;
            }
            if (IsMaxLevel) exp = 0;
            return ups;
        }

        public void FullHeal() => currentHp = MaxHp;

        public void EnsureHp()
        {
            if (currentHp < 0 || currentHp > MaxHp) currentHp = MaxHp;
        }

        public List<SkillData> GetAvailableSkills() => GetAvailableSkills(level);

        public List<SkillData> GetAvailableSkills(int atLevel)
        {
            var result = new List<SkillData>();
            AddSkills(result, Data.skills, atLevel);
            if (memoryStage >= MemoryStage.Restored) AddSkills(result, Data.restoredForm?.bonusSkills, atLevel);
            if (memoryStage == MemoryStage.TrueName) AddSkills(result, ActiveForm?.bonusSkills, atLevel);
            return result;
        }

        private static void AddSkills(List<SkillData> target, List<SkillData> source, int atLevel)
        {
            if (source == null) return;
            foreach (var skill in source)
                if (skill != null && Mathf.Max(1, skill.requiredLevel) <= atLevel && !target.Contains(skill))
                    target.Add(skill);
        }

        private static int ApplyMultiplier(int value, float multiplier) =>
            Mathf.Max(1, Mathf.RoundToInt(value * multiplier));
    }
}
