using UnityEngine;

namespace ShadowTheater.Battle
{
    /// <summary>
    /// 연속 명중으로 관객의 열기를 모으는 플레이어 전용 전투 리듬.
    /// 치명타와 유효 상성은 열기를 한 칸 더 올리고, 가득 차면 현재 피해를 강화한다.
    /// </summary>
    public sealed class BattleMomentum
    {
        public const int Max = 3;
        public const float EncoreDamageMultiplier = 1.25f;

        public int Value { get; private set; }

        public void Reset() => Value = 0;

        /// <summary>공격 결과를 반영한다. 앙코르가 발동하면 피해를 직접 강화하고 true를 반환한다.</summary>
        public bool Apply(ref HitResult hit, bool dealsDamage = true)
        {
            // 회복·상태 전용 기술은 명중 여부와 관계없이 열연에 영향을 주지 않는다.
            if (!dealsDamage) return false;

            if (hit.missed)
            {
                Value = 0;
                return false;
            }

            if (hit.damage <= 0) return false;

            Value = Mathf.Min(Max, Value + 1 + (hit.critical || hit.IsEffective ? 1 : 0));
            if (Value < Max) return false;

            hit.damage = Mathf.Max(hit.damage + 1, Mathf.RoundToInt(hit.damage * EncoreDamageMultiplier));
            Value = 0;
            return true;
        }
    }
}
