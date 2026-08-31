using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController), typeof(EnemyMeleeCombat), typeof(EnemyArchetypeProfile))]
    public sealed class LightBanditBrain : MobBrainBase
    {
        [Header("Skirmisher Movement")]
        [SerializeField, Min(0.1f)] private float pursuitSpeed = 4f;
        [SerializeField, Min(0.1f)] private float cautiousPursuitSpeed = 2.7f;
        [SerializeField, Min(0.1f)] private float contextRadius = 6f;
        [SerializeField, Min(0.1f)] private float retreatVelocityThreshold = 1.2f;
        [SerializeField, Range(0f, 1f)] private float lowStaminaFraction = 0.25f;

        [Header("Decision Weights")]
        [SerializeField, Range(0f, 1f)] private float basePursuitDashChance = 0.35f;
        [SerializeField, Range(0f, 1f)] private float frontlineDashBonus = 0.18f;
        [SerializeField, Range(0f, 1f)] private float eyeRetreatDashBonus = 0.28f;
        [SerializeField, Range(0f, 1f)] private float guardBashChance = 0.48f;
        [SerializeField, Range(0f, 1f)] private float lowStaminaBashBonus = 0.2f;
        [SerializeField, Range(0f, 1f)] private float evasiveResponseChance = 0.38f;
        [SerializeField, Range(0f, 1f)] private float sidestepCounterChance = 0.28f;

        private EnemyMeleeCombat melee;
        private LightBanditCombat skirmisher;
        private Rigidbody2D playerBody;

        protected override void ConfigureCapabilities()
        {
            melee = GetComponent<EnemyMeleeCombat>();
            skirmisher = GetComponent<LightBanditCombat>();
            if (skirmisher == null)
            {
                // The brain can be installed onto an authored Bandit without
                // rebuilding its prefab hierarchy. Existing tuned components
                // remain untouched; this only supplies the missing skirmisher
                // movement capability.
                skirmisher = gameObject.AddComponent<LightBanditCombat>();
            }
            GetComponent<EnemyArchetypeProfile>()?.AddRuntimeArchetype(EnemyArchetype.Melee);
        }

        protected override void SetCapabilityBrainControl(bool controlled)
        {
            melee?.SetBrainControlled(controlled);
        }

        protected override bool IsCapabilityBusy()
        {
            return (melee != null && melee.IsAttacking) || (skirmisher != null && skirmisher.IsBusy);
        }

        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            if (melee == null)
            {
                Move(Mathf.Sign(toPlayer.x), pursuitSpeed, MobBrainState.Chase, "Pursue player");
                return;
            }

            BattlefieldSnapshot context = BattlefieldContext.Read(this, contextRadius);
            bool guardHeld = player.GetComponent<SidewaysParryAttack>()?.IsGuardHeld == true;
            bool braced = player.GetComponent<PlayerBrace>()?.IsBraced == true;
            bool lowStamina = player.GetComponent<SpinSwordAttack>() is SpinSwordAttack stamina
                && stamina.CurrentStamina <= stamina.MaximumStamina * lowStaminaFraction;
            bool retreating = IsPlayerRetreating(player, toPlayer)
                || context.HasSignal(BattlefieldSignal.PlayerRetreating);
            float horizontalDistance = Mathf.Abs(toPlayer.x);

            if (horizontalDistance > melee.BasicEngagementRange)
            {
                float dashChance = basePursuitDashChance
                    + (context.HasTag(BattlefieldArchetypeTag.Frontline) ? frontlineDashBonus : 0f)
                    + (context.HasSignal(BattlefieldSignal.PlayerRetreating) ? eyeRetreatDashBonus : 0f);
                if (retreating && skirmisher != null && Random.value <= Mathf.Clamp01(dashChance))
                {
                    bool bash = (guardHeld || braced || lowStamina)
                        && Random.value <= Mathf.Clamp01(guardBashChance + (lowStamina ? lowStaminaBashBonus : 0f));
                    if (skirmisher.TryPursuitDash(player, bash))
                    {
                        HoldPosition(MobBrainState.Attack, bash ? "Pursuit Dash Bash" : "Pursuit Dash Slash");
                        return;
                    }
                }

                Move(
                    Mathf.Sign(toPlayer.x),
                    context.HasTag(BattlefieldArchetypeTag.Frontline) ? pursuitSpeed : cautiousPursuitSpeed,
                    MobBrainState.Chase,
                    retreating ? "Punish retreat" : "Skirmish pursuit");
                return;
            }

            PlayerAttackState playerAttack = player.GetComponent<PlayerAttackState>();
            if (playerAttack != null && playerAttack.IsActivelyAttacking
                && skirmisher != null && Random.value <= evasiveResponseChance)
            {
                bool backstep = Random.value < 0.45f;
                if ((backstep ? skirmisher.TryBackstep(player) : skirmisher.TrySidestep(player)))
                {
                    HoldPosition(MobBrainState.Reposition, backstep ? "Backstep unsafe pressure" : "Sidestep unsafe pressure");
                    return;
                }
            }

            if (melee.TryUse(EnemyMeleeDecision.Basic, player))
            {
                HoldPosition(MobBrainState.Attack, "Quick Slash");
                return;
            }

            // A sidestep counter uses the same modest Basic capability; it adds no
            // separate damage path and never displaces the baseline slash priority.
            if (skirmisher != null && Random.value <= sidestepCounterChance && skirmisher.TrySidestep(player))
            {
                HoldPosition(MobBrainState.Reposition, "Sidestep counter setup");
                return;
            }

            Move(Mathf.Sign(toPlayer.x), pursuitSpeed * 0.5f, MobBrainState.Chase, "Re-enter quick slash range");
        }

        private bool IsPlayerRetreating(PlayerHealth player, Vector2 toPlayer)
        {
            if (playerBody == null || playerBody.gameObject != player.gameObject)
            {
                playerBody = player.GetComponent<Rigidbody2D>();
            }

            return playerBody != null
                && Mathf.Abs(playerBody.velocity.x) >= retreatVelocityThreshold
                && Mathf.Sign(playerBody.velocity.x) == Mathf.Sign(toPlayer.x);
        }
    }
}
