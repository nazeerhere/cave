using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class NecromancerBrain : MobBrainBase
    {
        [Header("Support Positioning")]
        [SerializeField, Min(0f)] private float retreatRange = 3.5f;
        [SerializeField, Min(0f)] private float preferredSupportRange = 6f;
        [SerializeField, Min(0f)] private float supportRangeTolerance = 0.75f;
        [SerializeField, Min(0f)] private float repositionSpeed = 2.2f;

        [Header("Support Decisions")]
        [SerializeField, Min(0f)] private float summonDecisionWeight = 1f;
        [SerializeField, Min(0f)] private float buffDecisionWeight = 0.65f;

        private SwarmCaller summon;
        private EnemyDamageBuffAbility buff;

        protected override void ConfigureCapabilities()
        {
            summon = GetComponentInChildren<SwarmCaller>(true);
            buff = GetComponentInChildren<EnemyDamageBuffAbility>(true);
        }

        protected override void SetCapabilityBrainControl(bool controlled)
        {
            summon?.SetBrainControlled(controlled);
            buff?.SetBrainControlled(controlled);
        }

        protected override bool IsCapabilityBusy()
        {
            return (summon != null && summon.IsSummoning)
                || (buff != null && buff.IsCasting);
        }

        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            float distance = Mathf.Abs(toPlayer.x);
            float towardPlayer = Mathf.Sign(toPlayer.x);
            if (distance < retreatRange)
            {
                Move(-towardPlayer, repositionSpeed, MobBrainState.Reposition, "Retreat from player");
                return;
            }

            bool canSummon = summon != null && summon.IsReady;
            bool canBuff = buff != null && buff.IsReady && buff.HasEligibleTarget();
            if (canSummon || canBuff)
            {
                float summonWeight = canSummon ? summonDecisionWeight : 0f;
                float buffWeight = canBuff ? buffDecisionWeight : 0f;
                bool chooseSummon = Random.value * Mathf.Max(0.0001f, summonWeight + buffWeight)
                    < summonWeight;
                bool acted = chooseSummon ? summon.TrySummon() : buff.TryUse();
                if (!acted)
                {
                    acted = chooseSummon ? buff?.TryUse() == true : summon?.TrySummon() == true;
                }

                if (acted)
                {
                    HoldPosition(MobBrainState.Attack, chooseSummon ? "Summon allies" : "Buff ally");
                    return;
                }
            }

            if (distance < preferredSupportRange - supportRangeTolerance)
            {
                Move(-towardPlayer, repositionSpeed, MobBrainState.Reposition, "Recover support distance");
            }
            else if (distance > preferredSupportRange + supportRangeTolerance)
            {
                Move(towardPlayer, repositionSpeed, MobBrainState.Reposition, "Close to support distance");
            }
            else
            {
                HoldPosition(MobBrainState.Reposition, "Maintain support distance");
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            preferredSupportRange = Mathf.Max(retreatRange, preferredSupportRange);
        }
    }
}
