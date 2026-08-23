using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class DetectiveBrain : MobBrainBase
    {
        [Header("Fallback Range Bands")]
        [SerializeField, Min(0f)] private float minimumRange = 4f;
        [SerializeField, Min(0f)] private float preferredRange = 6f;
        [SerializeField, Min(0f)] private float maximumRange = 9f;
        [SerializeField, Min(0f)] private float preferredRangeTolerance = 0.75f;
        [SerializeField, Min(0f)] private float repositionSpeed = 2.6f;

        [Header("Ranged Repositioning")]
        [SerializeField, Min(0f)] private float strafeDuration = 0.45f;
        [SerializeField, Min(0f)] private float repositionCooldown = 1.4f;
        [SerializeField] private LayerMask lineOfSightBlockingLayers;
        [SerializeField] private bool drawPreferredRange = true;

        private EnemyPoisonShooter poisonShooter;
        private EnemyKeepDistance keepDistance;
        private float strafeUntil;
        private float nextStrafeTime;
        private float strafeDirection;

        private float MinimumRange => keepDistance != null ? keepDistance.MinimumRange : minimumRange;
        private float PreferredRange => keepDistance != null ? keepDistance.PreferredRange : preferredRange;
        private float MaximumRange => keepDistance != null ? keepDistance.MaximumRange : maximumRange;

        protected override void ConfigureCapabilities()
        {
            poisonShooter = GetComponentInChildren<EnemyPoisonShooter>(true);
            keepDistance = GetComponent<EnemyKeepDistance>();
        }

        protected override void SetCapabilityBrainControl(bool controlled)
        {
            poisonShooter?.SetBrainControlled(controlled);
            keepDistance?.SetBrainControlled(controlled);
        }

        protected override bool IsCapabilityBusy()
        {
            return poisonShooter != null && poisonShooter.IsBusy;
        }

        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            float distance = Mathf.Abs(toPlayer.x);
            float towardPlayer = Mathf.Sign(toPlayer.x);
            if (distance < MinimumRange)
            {
                Move(-towardPlayer, repositionSpeed, MobBrainState.Reposition, "Retreat to minimum range");
                return;
            }

            if (distance > MaximumRange)
            {
                Move(towardPlayer, repositionSpeed, MobBrainState.Chase, "Approach firing range");
                return;
            }

            if (poisonShooter != null
                && HasClearLineOfFire(player)
                && poisonShooter.TryUse(player.transform))
            {
                HoldPosition(MobBrainState.Attack, "Fire poison projectile");
                return;
            }

            if (distance < PreferredRange - preferredRangeTolerance)
            {
                Move(-towardPlayer, repositionSpeed, MobBrainState.Reposition, "Open preferred range");
                return;
            }

            if (distance > PreferredRange + preferredRangeTolerance)
            {
                Move(towardPlayer, repositionSpeed, MobBrainState.Reposition, "Close preferred range");
                return;
            }

            if (Time.time >= nextStrafeTime)
            {
                strafeDirection = Random.value < 0.5f ? -1f : 1f;
                strafeUntil = Time.time + strafeDuration;
                nextStrafeTime = strafeUntil + repositionCooldown;
            }

            if (Time.time < strafeUntil)
            {
                Move(strafeDirection, repositionSpeed, MobBrainState.Reposition, "Short ranged strafe");
            }
            else
            {
                HoldPosition(MobBrainState.Reposition, "Hold preferred range");
            }
        }

        private bool HasClearLineOfFire(PlayerHealth player)
        {
            if (lineOfSightBlockingLayers.value == 0)
            {
                return true;
            }

            RaycastHit2D hit = Physics2D.Linecast(
                transform.position,
                player.transform.position,
                lineOfSightBlockingLayers);
            return hit.collider == null
                || hit.collider.GetComponentInParent<PlayerHealth>() == player;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!drawPreferredRange)
            {
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, PreferredRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, MinimumRange);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, MaximumRange);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            preferredRange = Mathf.Max(minimumRange, preferredRange);
            maximumRange = Mathf.Max(preferredRange, maximumRange);
        }
    }
}
