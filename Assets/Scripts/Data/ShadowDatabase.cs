using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.Data
{
    /// <summary>
    /// shadowId / skillId / itemId → SO 조회 테이블. SaveData(JSON)에는 ID만 저장하고 로드 시 여기서 SO를 찾는다.
    /// Resources/ShadowDatabase.asset 하나만 만들어 두면 됨.
    /// </summary>
    [CreateAssetMenu(fileName = "ShadowDatabase", menuName = "ShadowTheater/Shadow Database", order = 10)]
    public class ShadowDatabase : ScriptableObject
    {
        public List<ShadowData> shadows = new List<ShadowData>();
        public List<SkillData> skills = new List<SkillData>();
        public List<ItemData> items = new List<ItemData>();

        private Dictionary<string, ShadowData> _shadowMap;
        private Dictionary<string, SkillData> _skillMap;
        private Dictionary<string, ItemData> _itemMap;

        private static ShadowDatabase _instance;
        public static ShadowDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ShadowDatabase>("ShadowDatabase");
                    if (_instance == null) Debug.LogError("[ShadowDatabase] Resources/ShadowDatabase.asset 이 없습니다.");
                }
                return _instance;
            }
        }

        public ShadowData GetShadow(string id)
        {
            if (_shadowMap == null) Build();
            return id != null && _shadowMap.TryGetValue(id, out var d) ? d : null;
        }

        public SkillData GetSkill(string id)
        {
            if (_skillMap == null) Build();
            return id != null && _skillMap.TryGetValue(id, out var s) ? s : null;
        }

        public ItemData GetItem(string id)
        {
            if (_itemMap == null) Build();
            return id != null && _itemMap.TryGetValue(id, out var i) ? i : null;
        }

        private void Build()
        {
            _shadowMap = new Dictionary<string, ShadowData>();
            foreach (var s in shadows)
            {
                if (s == null) continue;
                if (_shadowMap.ContainsKey(s.shadowId)) Debug.LogWarning($"[ShadowDatabase] 중복 shadowId: {s.shadowId}");
                else _shadowMap.Add(s.shadowId, s);
            }

            _skillMap = new Dictionary<string, SkillData>();
            foreach (var s in skills)
                if (s != null && !_skillMap.ContainsKey(s.skillId)) _skillMap.Add(s.skillId, s);

            _itemMap = new Dictionary<string, ItemData>();
            foreach (var i in items)
                if (i != null && !_itemMap.ContainsKey(i.itemId)) _itemMap.Add(i.itemId, i);
        }

        private void OnEnable() { _shadowMap = null; _skillMap = null; _itemMap = null; }
    }
}
