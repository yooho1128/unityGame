using System;
using System.Collections.Generic;
using ShadowTheater.Core;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Runtime
{
    [Serializable]
    public sealed class ShadowRuntime
    {
        [SerializeField] private ShadowData data;
        [Min(1)]
        [SerializeField] private int level = 1;
        [SerializeField] private int currentHealth;
        [Min(0)]
        [SerializeField] private int currentFocus;
        [SerializeField] private List<StatusEffectType> statusEffects = new List<StatusEffectType>();

        public ShadowData Data => data;
        public int Level => level;
        public int MaxHealth => data == null ? 0 : data.BaseMaxHealth;
        public int CurrentHealth => currentHealth;
        public int CurrentFocus => currentFocus;
        public bool IsDefeated => currentHealth <= 0;
        public IReadOnlyList<StatusEffectType> StatusEffects => statusEffects;

        public ShadowRuntime(ShadowData data, int level = 1)
        {
            Initialize(data, level);
        }

        public void Initialize(ShadowData shadowData, int startingLevel = 1)
        {
            data = shadowData;
            level = Mathf.Max(1, startingLevel);
            currentHealth = MaxHealth;
            currentFocus = 0;
            statusEffects.Clear();
        }

        public void RestoreForBattle()
        {
            currentHealth = MaxHealth;
            currentFocus = 0;
            statusEffects.Clear();
        }

        public void TakeDamage(int amount)
        {
            currentHealth = Mathf.Clamp(currentHealth - Mathf.Max(0, amount), 0, MaxHealth);
        }

        public void Heal(int amount)
        {
            currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0, amount), 0, MaxHealth);
        }

        public void GainFocus(int amount, int maximumFocus)
        {
            currentFocus = Mathf.Clamp(currentFocus + Mathf.Max(0, amount), 0, maximumFocus);
        }

        public bool TrySpendFocus(int amount)
        {
            int cost = Mathf.Max(0, amount);
            if (currentFocus < cost)
            {
                return false;
            }

            currentFocus -= cost;
            return true;
        }

        public void AddStatusEffect(StatusEffectType effect)
        {
            if (effect != StatusEffectType.None && !statusEffects.Contains(effect))
            {
                statusEffects.Add(effect);
            }
        }

        public void RemoveStatusEffect(StatusEffectType effect)
        {
            statusEffects.Remove(effect);
        }
    }
}
