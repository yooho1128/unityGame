using System;
using ShadowTheater.Core;
using ShadowTheater.Runtime;
using UnityEngine;

namespace ShadowTheater.Battle
{
    public sealed class BattleManager : MonoBehaviour
    {
        [Header("Rules")]
        [Min(1)]
        [SerializeField] private int maximumFocus = 5;
        [Min(1)]
        [SerializeField] private int basicAttackFocusGain = 1;

        public BattleState State { get; private set; } = BattleState.None;
        public ShadowRuntime Player { get; private set; }
        public ShadowRuntime Enemy { get; private set; }

        public event Action<BattleState> StateChanged;
        public event Action<ShadowRuntime, ShadowRuntime, BattleActionType> ActionResolved;

        public void StartBattle(ShadowRuntime player, ShadowRuntime enemy)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (enemy == null)
            {
                throw new ArgumentNullException(nameof(enemy));
            }

            Player = player;
            Enemy = enemy;
            SetState(BattleState.Starting);

            // Turn order, encounter presentation, and save integration are intentionally
            // left for the first playable battle implementation.
            SetState(BattleState.PlayerTurn);
        }

        public bool TryPlayerBasicAttack()
        {
            if (State != BattleState.PlayerTurn || Player.IsDefeated || Enemy.IsDefeated)
            {
                return false;
            }

            SetState(BattleState.Processing);
            ResolveBasicAttack(Player, Enemy);
            ActionResolved?.Invoke(Player, Enemy, BattleActionType.Attack);

            if (TryFinishBattle())
            {
                return true;
            }

            SetState(BattleState.EnemyTurn);
            return true;
        }

        public bool TryEnemyBasicAttack()
        {
            if (State != BattleState.EnemyTurn || Player.IsDefeated || Enemy.IsDefeated)
            {
                return false;
            }

            SetState(BattleState.Processing);
            ResolveBasicAttack(Enemy, Player);
            ActionResolved?.Invoke(Enemy, Player, BattleActionType.Attack);

            if (TryFinishBattle())
            {
                return true;
            }

            SetState(BattleState.PlayerTurn);
            return true;
        }

        public void CancelBattle()
        {
            Player = null;
            Enemy = null;
            SetState(BattleState.None);
        }

        private void ResolveBasicAttack(ShadowRuntime attacker, ShadowRuntime defender)
        {
            int rawDamage = attacker.Data.BaseAttack - defender.Data.BaseDefense;
            int damage = Mathf.Max(1, rawDamage);

            defender.TakeDamage(damage);
            attacker.GainFocus(basicAttackFocusGain, maximumFocus);
        }

        private bool TryFinishBattle()
        {
            if (Enemy.IsDefeated)
            {
                SetState(BattleState.Victory);
                return true;
            }

            if (Player.IsDefeated)
            {
                SetState(BattleState.Defeat);
                return true;
            }

            return false;
        }

        private void SetState(BattleState nextState)
        {
            State = nextState;
            StateChanged?.Invoke(State);
        }
    }
}
