using System.Collections.Generic;
using ShadowTheater.Battle;
using UnityEngine;

namespace ShadowTheater.UI
{
    /// <summary>전투 중 최대 6명의 보유·생존·출전 상태를 한눈에 보여준다.</summary>
    public class BattlePartyStrip : MonoBehaviour
    {
        [SerializeField] private List<BattlePartySlot> slots = new List<BattlePartySlot>();

        public void Bind(IReadOnlyList<BattleUnit> units, int activeIndex)
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i]?.Bind(units != null && i < units.Count ? units[i] : null, i == activeIndex);
        }
    }
}
