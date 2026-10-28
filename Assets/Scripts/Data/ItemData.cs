using UnityEngine;

namespace ShadowTheater.Data
{
    public enum ItemEffectType
    {
        HealFlat,      // 고정 수치 회복
        HealRatio,     // 최대 HP 비율 회복
        CureStatus,    // 상태 이상 해제
        GainFP,        // FP 충전
        CaptureBoost,  // 각본 기록 성공률 보정 (특수 잉크 등)
        TraversalTool, // 소비하지 않고 필드 이동 경로를 해금하는 핵심 도구
        GainExperience, // 선택한 그림자에게 경험치 지급
        RestoreAndCleanse // 전투 중 최대 HP 비율 회복 + 모든 상태 이상 해제. 기존 enum 값 보존.
    }

    /// <summary>[도구] 행동용 아이템 정의. Create > ShadowTheater > Item Data</summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "ShadowTheater/Item Data", order = 2)]
    public class ItemData : ScriptableObject
    {
        public string itemId;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        public ItemEffectType effectType;
        [Tooltip("HealFlat=회복량, HealRatio/RestoreAndCleanse=0~1 비율, GainFP=FP량, CaptureBoost=배율, GainExperience=경험치")]
        public float value;
        public bool usableInBattle = true;
        [Tooltip("필드의 파티 화면에서 사용할 수 있는 도구인지 여부")]
        public bool usableInField;
        [Tooltip("판매·중복 구매할 수 없는 진행용 도구")]
        public bool isKeyItem;
        [Tooltip("동행 이벤트 등에서만 획득하며 상점 목록에는 표시하지 않음")]
        public bool isEventOnly;
        [Min(0)] public int buyPrice = 50;
        [Min(0)] public int sellPrice = 20;
        [Tooltip("상점에 등장하는 최소 막. 1이면 처음부터 판매")]
        [Range(1, 8)] public int shopUnlockAct = 1;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(itemId)) itemId = name;
            buyPrice = Mathf.Max(0, buyPrice);
            sellPrice = Mathf.Clamp(sellPrice, 0, buyPrice);
            shopUnlockAct = Mathf.Clamp(shopUnlockAct, 1, 8);
        }
#endif
    }
}
