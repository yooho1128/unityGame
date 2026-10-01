using System;
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
        public string instanceId;   // 같은 그림자 중복 수집 대비 고유 ID
        public string shadowId;
        public int level = 1;
        public int exp;
        public int currentHp = -1;  // -1 = 최대치로 초기화

        [NonSerialized] private ShadowData _data;
        public ShadowData Data => _data != null ? _data : (_data = ShadowDatabase.Instance.GetShadow(shadowId));

        public ShadowInstance() { }

        public ShadowInstance(ShadowData data, int level)
        {
            instanceId = Guid.NewGuid().ToString("N");
            shadowId = data.shadowId;
            _data = data;
            this.level = Mathf.Max(1, level);
            currentHp = MaxHp;
        }

        public int MaxHp => Data.GetHp(level);
        public bool IsFainted => currentHp == 0;

        public int ExpToNext => 20 + level * level * 5; // 임시 곡선

        /// <summary>경험치 획득. 레벨업 횟수 반환</summary>
        public int AddExp(int amount)
        {
            int ups = 0;
            exp += amount;
            while (exp >= ExpToNext)
            {
                exp -= ExpToNext;
                int prevMax = MaxHp;
                level++;
                currentHp += MaxHp - prevMax; // 오른 만큼 현재 HP도 증가
                ups++;
            }
            return ups;
        }

        public void FullHeal() => currentHp = MaxHp;

        public void EnsureHp()
        {
            if (currentHp < 0 || currentHp > MaxHp) currentHp = MaxHp;
        }
    }
}
