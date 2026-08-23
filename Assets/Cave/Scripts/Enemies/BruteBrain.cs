using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class BruteBrain : MobBrainBase
    {
        [Header("Melee Pressure")]
        [SerializeField, Min(0f)] private float pursuitSpeed = 2.8f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.6f;

        [Header("Attack Weights")]
        [SerializeField, Min(0f)] private float basicWeight = 1f;
        [SerializeField, Min(0f)] private float chargedWeight = 0.25f;
        [SerializeField, Min(0f)] private float committedPlayerChargeBonus = 0.55f;
        [SerializeField, Min(0f)] private float guardedFeintWeight = 0.35f;
        [SerializeField, Min(0f)] private float guardedBreakWeight = 0.6f;

        private EnemyMeleeCombat melee;

        protected override void ConfigureCapabilities()
        {
            melee = GetComponentInChildren<EnemyMeleeCombat>(true);
        }

        protected override void SetCapabilityBrainControl(bool controlled)
        {
            melee?.SetBrainControlled(controlled);
        }

        protected override bool IsCapabilityBusy()
        {
            return melee != null && melee.IsAttacking;
        }

        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            float horizontalDistance = Mathf.Abs(toPlayer.x);
            if (horizontalDistance > attackRange || melee == null)
            {
                Move(Mathf.Sign(toPlayer.x), pursuitSpeed, MobBrainState.Chase, "Pursue player");
                return;
            }

            HoldPosition(MobBrainState.Attack, "Choose melee action");
            EnemyMeleeDecision decision = ChooseAttack(player);
            if (melee.TryUse(decision, player))
            {
                SetState(MobBrainState.Attack, decision.ToString());
                return;
            }

            if (decision != EnemyMeleeDecision.Basic
                && melee.TryUse(EnemyMeleeDecision.Basic, player))
            {
                SetState(MobBrainState.Attack, "Basic fallback");
            }
        }

        private EnemyMeleeDecision ChooseAttack(PlayerHealth player)
        {
            SidewaysParryAttack guard = player.GetComponent<SidewaysParryAttack>();
            bool guardHeld = guard != null && guard.IsGuardHeld;
            PlayerAttackState playerAttack = player.GetComponent<PlayerAttackState>();
            bool playerCommitted = playerAttack != null && playerAttack.IsActivelyAttacking;

            float charged = chargedWeight + (playerCommitted ? committedPlayerChargeBonus : 0f);
            float feint = guardHeld ? guardedFeintWeight : 0f;
            float guardBreak = guardHeld ? guardedBreakWeight : 0f;
            float total = basicWeight + charged + feint + guardBreak;
            float roll = Random.value * Mathf.Max(0.0001f, total);
            if ((roll -= basicWeight) < 0f)
            {
                return EnemyMeleeDecision.Basic;
            }

            if ((roll -= charged) < 0f)
            {
                return EnemyMeleeDecision.Charged;
            }

            return roll < feint
                ? EnemyMeleeDecision.Feint
                : EnemyMeleeDecision.GuardBreak;
        }
    }
}
