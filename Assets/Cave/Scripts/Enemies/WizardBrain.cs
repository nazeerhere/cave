using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class WizardBrain : MobBrainBase
    {
        [Header("Support Positioning")]
        [SerializeField, Min(0f)] private float retreatRange = 3.75f;
        [SerializeField, Min(0f)] private float preferredSupportRange = 6.5f;
        [SerializeField, Min(0f)] private float supportRangeTolerance = 0.75f;
        [SerializeField, Min(0f)] private float repositionSpeed = 2.1f;

        private EnemyHealAbility heal;
        private EnemyDamageBuffAbility buff;

        protected override void ConfigureCapabilities()
        {
            heal = GetComponentInChildren<EnemyHealAbility>(true);
            buff = GetComponentInChildren<EnemyDamageBuffAbility>(true);
        }

        protected override void SetCapabilityBrainControl(bool controlled)
        {
            heal?.SetBrainControlled(controlled);
            buff?.SetBrainControlled(controlled);
        }

        protected override bool IsCapabilityBusy()
        {
            return (heal != null && heal.IsCasting)
                || (buff != null && buff.IsCasting);
        }

        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            if (heal != null && heal.IsReady && heal.HasEligibleTarget() && heal.TryUse())
            {
                HoldPosition(MobBrainState.Attack, "Heal lowest-health ally");
                return;
            }

            float distance = Mathf.Abs(toPlayer.x);
            float towardPlayer = Mathf.Sign(toPlayer.x);
            if (distance < retreatRange)
            {
                Move(-towardPlayer, repositionSpeed, MobBrainState.Reposition, "Retreat from player");
                return;
            }

            if (buff != null && buff.IsReady && buff.HasEligibleTarget() && buff.TryUse())
            {
                HoldPosition(MobBrainState.Attack, "Buff frontline ally");
                return;
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
