using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Shared behaviour policy; Qilin differs through authorised
    /// interventions, not a duplicate combat controller.</summary>
    [RequireComponent(typeof(NymrathCore))]
    public sealed class NymrathBrain : MobBrainBase
    {
        [SerializeField, Min(.1f)] private float normalPursuitRange = 5.5f;
        [SerializeField, Min(.1f)] private float qilinPursuitRange = 8f;
        [SerializeField, Min(.1f)] private float prisonRange = 3.5f;
        [SerializeField, Min(.1f)] private float stampedeMinimumRange = 3f;
        [SerializeField, Min(.1f)] private float attackCooldown = .8f;

        private NymrathCore core;
        private float nextActionAt;

        protected override void Awake()
        {
            core = GetComponent<NymrathCore>();
            base.Awake();
        }

        protected override void ConfigureCapabilities()
        {
        }

        protected override bool IsCapabilityBusy()
        {
            return core != null && core.IsBusy;
        }

        protected override bool TryOverrideIdleBehavior()
        {
            if (core != null && Time.time >= nextActionAt && core.IsCorruptedQilin && core.TryEternalBloom())
            {
                nextActionAt = Time.time + attackCooldown;
                HoldPosition(MobBrainState.Defend, "Eternal Bloom");
                return true;
            }

            if (core != null && Time.time >= nextActionAt && core.TryRestorationWeave())
            {
                nextActionAt = Time.time + attackCooldown;
                HoldPosition(MobBrainState.Defend, "Restoration Weave");
                return true;
            }

            return false;
        }

        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            if (core == null || player == null)
            {
                HoldPosition(MobBrainState.Recover, "Missing Nymrath authority");
                return;
            }

            float distance = toPlayer.magnitude;
            if (Time.time >= nextActionAt)
            {
                if (core.IsCorruptedQilin
                    && distance <= prisonRange
                    && core.TryPreservationPrison(player))
                {
                    nextActionAt = Time.time + attackCooldown;
                    HoldPosition(MobBrainState.Attack, "Preservation Prison");
                    return;
                }

                if (core.IsCorruptedQilin
                    && distance >= stampedeMinimumRange
                    && core.TrySpectralStampede(Mathf.Sign(toPlayer.x)))
                {
                    nextActionAt = Time.time + attackCooldown;
                    HoldPosition(MobBrainState.Attack, "Spectral Stampede");
                    return;
                }

                if (core.TryTendrilLash(player))
                {
                    nextActionAt = Time.time + attackCooldown;
                    HoldPosition(MobBrainState.Attack, "Tendril Lash");
                    return;
                }
            }

            float pursuitRange = core.IsCorruptedQilin ? qilinPursuitRange : normalPursuitRange;
            if (distance <= pursuitRange)
            {
                Move(Mathf.Sign(toPlayer.x), core.IsCorruptedQilin ? 2.3f : 1.5f, MobBrainState.Chase,
                    core.IsCorruptedQilin ? "Territorial pursuit" : "Restrained defense");
            }
            else
            {
                HoldPosition(MobBrainState.Alert, "Preserve territory");
            }
        }
    }
}
