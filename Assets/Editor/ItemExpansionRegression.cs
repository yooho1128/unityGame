#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Story;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class ItemExpansionRegression
    {
        [Serializable] private class Catalog { public Spec[] items; }
        [Serializable] private class Spec
        {
            public string itemId, effectType;
            public float value;
            public int buyPrice, sellPrice, shopUnlockAct;
            public bool usableInBattle, usableInField;
        }
        public static void Validate(List<string> errors)
        {
            if ((int)ItemEffectType.GainExperience != 6 || (int)ItemEffectType.RestoreAndCleanse != 7)
                errors.Add("도구 효과 enum 저장 호환성 오류");
            var catalog = Resources.Load<TextAsset>("Data/ItemExpansionCatalog");
            var specs = catalog != null ? JsonUtility.FromJson<Catalog>(catalog.text).items : null;
            if (specs == null || specs.Length != 8) { errors.Add("신규 도구 8종 카탈로그 누락"); return; }
            foreach (var spec in specs)
            {
                var item = ScriptableObject.CreateInstance<ItemData>();
                try
                {
                    item.itemId = spec.itemId; item.effectType = (ItemEffectType)Enum.Parse(typeof(ItemEffectType), spec.effectType);
                    item.value = spec.value; item.shopUnlockAct = spec.shopUnlockAct;
                    item.buyPrice = spec.buyPrice; item.sellPrice = spec.sellPrice;
                    item.usableInBattle = spec.usableInBattle; item.usableInField = spec.usableInField;
                    if (spec.sellPrice >= spec.buyPrice || spec.value <= 0f)
                        errors.Add("신규 도구 가격/효과 오류: " + spec.itemId);
                    if (!RegionRepository.All.Any(r => r.isSettlement && TownServicePolicy.AllowsItem(r, item)))
                        errors.Add("신규 도구 상점 획득 경로 누락: " + spec.itemId);
                    if (RegionRepository.All.Any(r => r.isSettlement && r.act < spec.shopUnlockAct &&
                        TownServicePolicy.AllowsItem(r, item)))
                        errors.Add("신규 도구 조기 판매: " + spec.itemId);
                }
                finally { UnityEngine.Object.DestroyImmediate(item); }
            }
            var data = ScriptableObject.CreateInstance<ShadowData>();
            try
            {
                data.shadowId = "restorative_regression"; data.baseHp = 100;
                var unit = new BattleUnit(new ShadowInstance(data, 1), BattleSide.Player);
                if (unit.NeedsRestoration || unit.RestoreAndCleanse(.35f) != 0)
                    errors.Add("정상/만피 대상 복합 회복 차단 오류");
                unit.ApplyStatus(StatusEffectType.Burn, 2);
                unit.ApplyStatus(StatusEffectType.DefenseDown, 2);
                if (!unit.NeedsRestoration || unit.RestoreAndCleanse(.35f) != 0 || unit.HasAnyStatus || unit.Hp != unit.MaxHp)
                    errors.Add("만피 대상 주요 상태/약화 정화 오류");
                unit.TakeDamage(60); unit.ApplyStatus(StatusEffectType.Freeze, 2);
                if (unit.RestoreAndCleanse(.35f) != 35 || unit.Hp != 75 || unit.HasAnyStatus)
                    errors.Add("HP 회복+빙결 해제 오류");
                unit.ApplyStatus(StatusEffectType.SpeedDown, 2);
                if (unit.RestoreAndCleanse(.65f) != 25 || unit.Hp != 100 || unit.HasAnyStatus)
                    errors.Add("복합 회복 초과분 또는 약화 해제 오류");
                unit.TakeDamage(unit.MaxHp);
                if (unit.NeedsRestoration || unit.RestoreAndCleanse(.65f) != 0 || !unit.IsFainted)
                    errors.Add("복합 회복으로 기절 부활 허용됨");
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }
        }
    }
}
#endif
