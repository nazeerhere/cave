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

        [Header("Visual Facing")]
        [SerializeField] private SpriteRenderer bruteVisual;
        [SerializeField] private Transform corruptionVisualRoot;
        [SerializeField] private Transform weaponVisualRoot;
        [SerializeField, Min(0f)] private float facingVelocityThreshold = 0.05f;
        [SerializeField] private bool sourceSpriteFacesRight = true;

        [Header("Body Control")]
        [SerializeField, Min(0.1f)] private float hookDecisionRange = 2.55f;
        [SerializeField, Min(0.1f)] private float chainHookDecisionRange = 6f;
        [SerializeField, Range(0f, 1f)] private float lowStaminaFraction = 0.25f;
        [SerializeField, Min(0f)] private float lowStaminaControlBonus = 0.75f;
        [SerializeField, Min(0f)] private float wallPinBonus = 0.9f;

        [Header("Guard Break / Guard Mixup")]
        [SerializeField, Min(0f)] private float baseGuardBreakWeight = 1.15f;
        [SerializeField, Min(0f)] private float guardBreakRepeatCooldown = 0.7f;
        [SerializeField, Range(0f, 1f)] private float failedGuardBreakWeightMultiplier = 0.25f;
        [SerializeField, Range(0f, 1f)] private float closePressureGuardChance = 0.45f;
        [SerializeField, Min(0.01f)] private float closePressureGuardDuration = 0.4f;
        [SerializeField, Min(0f)] private float guardRepeatCooldown = 1.4f;

        [Header("Attack Weights")]
        [SerializeField, Min(0f)] private float basicWeight = 1f;
        [SerializeField, Min(0f)] private float chargedWeight = 0.25f;
        [SerializeField, Min(0f)] private float committedPlayerChargeBonus = 0.55f;
        [SerializeField, Min(0f)] private float guardedFeintWeight = 0.35f;
        [SerializeField, Min(0f)] private float guardedBreakWeight = 0.6f;

        private EnemyMeleeCombat melee;
        private BruteControlAbilities control;
        private EnemyDefenseController bruteDefense;
        private float nextGuardBreakPreferenceTime;
        private float nextGuardPreferenceTime;
        private Rigidbody2D body;
        private EnemyVisualChildFacing corruptionFacing;

        protected override void ConfigureCapabilities()
        {
            melee = GetComponentInChildren<EnemyMeleeCombat>(true);
            body = GetComponent<Rigidbody2D>();
            if (bruteVisual == null)
            {
                bruteVisual = GetComponent<SpriteRenderer>();
            }

            if (corruptionVisualRoot == null)
            {
                corruptionVisualRoot = FindVisualChild("corruption");
            }

            if (weaponVisualRoot == null)
            {
                weaponVisualRoot = FindVisualChild("pickaxe");
            }

            if (bruteVisual == null
                && corruptionVisualRoot == null
                && weaponVisualRoot == null)
            {
                bruteVisual = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (corruptionVisualRoot != null || weaponVisualRoot != null)
            {
                corruptionFacing = GetComponent<EnemyVisualChildFacing>();
                if (corruptionFacing == null)
                {
                    corruptionFacing = gameObject.AddComponent<EnemyVisualChildFacing>();
                }

                corruptionFacing.Configure(
                    corruptionVisualRoot,
                    weaponVisualRoot,
                    sourceSpriteFacesRight);
            }
            control = GetComponent<BruteControlAbilities>();
            if (control == null)
            {
                // BruteBrain is also installed by EnemyDifficultyScaler at runtime.
                // Adding this Cave-owned capability keeps that path functional while
                // leaving authored prefab children and references untouched.
                control = gameObject.AddComponent<BruteControlAbilities>();
            }

            bruteDefense = GetComponent<EnemyDefenseController>();
            if (bruteDefense == null)
            {
                // Brute brains can be installed at runtime by EnemyDifficultyScaler.
                // Add the existing shared capability only for that missing case;
                // authored defense tuning is never overwritten.
                bruteDefense = gameObject.AddComponent<EnemyDefenseController>();
                bruteDefense.ConfigureBruteDefaults();
            }
            else if (bruteDefense.EffectiveBlockChance <= 0f)
            {
                // A nonfunctional Custom defense was the other way a Brute could
                // silently never Guard. Preserve all timing/presentation tuning,
                // but give that component the role's authored probability preset.
                bruteDefense.ConfigurePreset(EnemyDefensePreset.Brute);
            }
        }

        protected override void SetCapabilityBrainControl(bool controlled)
        {
            melee?.SetBrainControlled(controlled);
        }

        protected override bool IsCapabilityBusy()
        {
            return (melee != null && melee.IsAttacking)
                || (control != null && control.IsBusy);
        }

        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            float horizontalDistance = Mathf.Abs(toPlayer.x);
            if (bruteDefense != null
                && (bruteDefense.CurrentState == EnemyDefenseState.Blocking
                    || bruteDefense.CurrentState == EnemyDefenseState.Parrying
                    || bruteDefense.CurrentState == EnemyDefenseState.Recovering))
            {
                HoldPosition(MobBrainState.Defend, bruteDefense.CurrentState.ToString());
                return;
            }
            if (melee == null)
            {
                Move(Mathf.Sign(toPlayer.x), pursuitSpeed, MobBrainState.Chase, "Pursue player");
                return;
            }

            bool playerLowStamina = IsPlayerLowStamina(player);
            bool playerNearWall = control != null && control.IsPlayerNearWall(player);
            if (control != null
                && horizontalDistance > attackRange
                && horizontalDistance <= chainHookDecisionRange
                && control.TryUseChainHook(player))
            {
                HoldPosition(MobBrainState.Attack, "Throw Chain Hook");
                return;
            }

            if (control != null
                && horizontalDistance > attackRange
                && horizontalDistance <= hookDecisionRange
                && (playerLowStamina || Random.value < 0.55f)
                && control.TryUseHookDrag(player))
            {
                HoldPosition(MobBrainState.Attack, "Hook / Drag");
                return;
            }

            float basicEngagementRange = Mathf.Max(0.1f, melee.BasicEngagementRange);
            if (horizontalDistance > basicEngagementRange)
            {
                Move(Mathf.Sign(toPlayer.x), pursuitSpeed, MobBrainState.Chase, "Close grappling range");
                return;
            }

            PlayerAttackState playerAttack = player.GetComponent<PlayerAttackState>();
            bool playerCommitted = playerAttack != null && playerAttack.IsActivelyAttacking;
            SidewaysParryAttack playerGuard = player.GetComponent<SidewaysParryAttack>();
            bool sustainedClosePressure = playerCommitted
                || (playerGuard != null && playerGuard.IsGuardHeld);
            if (bruteDefense != null
                && sustainedClosePressure
                && Time.time >= nextGuardPreferenceTime
                && Random.value <= closePressureGuardChance
                && bruteDefense.TryEnterDefensivePosture(closePressureGuardDuration))
            {
                nextGuardPreferenceTime = Time.time + guardRepeatCooldown;
                HoldPosition(MobBrainState.Defend, "Guard under close pressure");
                return;
            }

            if (control != null
                && (playerNearWall || playerLowStamina)
                && Random.value < (playerNearWall ? 0.7f : 0.38f)
                && control.TryUsePin(player, playerNearWall))
            {
                HoldPosition(MobBrainState.Attack, playerNearWall ? "Wall Pin" : "Pin pressure");
                return;
            }

            HoldPosition(MobBrainState.Attack, "Choose melee action");
            EnemyMeleeDecision decision = ChooseAttack(player, playerLowStamina, playerNearWall);
            if (melee.TryUse(decision, player))
            {
                if (decision == EnemyMeleeDecision.GuardBreak)
                {
                    nextGuardBreakPreferenceTime = Time.time + guardBreakRepeatCooldown;
                }
                SetState(MobBrainState.Attack, decision.ToString());
                return;
            }

            if (decision == EnemyMeleeDecision.GuardBreak)
            {
                nextGuardBreakPreferenceTime = Time.time + guardBreakRepeatCooldown;
            }

            if (melee.LastUseRejection == EnemyMeleeUseRejection.OutOfRange)
            {
                Move(Mathf.Sign(toPlayer.x), pursuitSpeed, MobBrainState.Chase, "Advance after rejected melee");
                return;
            }

            if (decision != EnemyMeleeDecision.Basic
                && melee.TryUse(EnemyMeleeDecision.Basic, player))
            {
                SetState(MobBrainState.Attack, "Basic fallback");
                return;
            }

            Move(
                Mathf.Sign(toPlayer.x),
                pursuitSpeed * 0.55f,
                MobBrainState.Chase,
                "Maintain close pressure while melee is unavailable");
        }

        private EnemyMeleeDecision ChooseAttack(
            PlayerHealth player,
            bool playerLowStamina,
            bool playerNearWall)
        {
            SidewaysParryAttack guard = player.GetComponent<SidewaysParryAttack>();
            bool guardHeld = guard != null && guard.IsGuardHeld;
            PlayerAttackState playerAttack = player.GetComponent<PlayerAttackState>();
            bool playerCommitted = playerAttack != null && playerAttack.IsActivelyAttacking;

            float charged = chargedWeight + (playerCommitted ? committedPlayerChargeBonus : 0f);
            float feint = guardHeld ? guardedFeintWeight : 0f;
            float guardBreak = baseGuardBreakWeight
                + (guardHeld ? guardedBreakWeight : 0f)
                + (playerLowStamina ? lowStaminaControlBonus : 0f)
                + (playerNearWall ? wallPinBonus * 0.35f : 0f);
            if (Time.time < nextGuardBreakPreferenceTime)
            {
                guardBreak *= failedGuardBreakWeightMultiplier;
            }
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

        private bool IsPlayerLowStamina(PlayerHealth player)
        {
            SpinSwordAttack stamina = player != null ? player.GetComponent<SpinSwordAttack>() : null;
            return stamina != null
                && stamina.CurrentStamina <= stamina.MaximumStamina * lowStaminaFraction;
        }

        private void LateUpdate()
        {
            if (body != null && Mathf.Abs(body.velocity.x) >= facingVelocityThreshold)
            {
                FaceVisual(body.velocity.x);
            }
            else if (Target != null && IsCapabilityBusy())
            {
                FaceVisual(Target.transform.position.x - transform.position.x);
            }
        }

        private void FaceVisual(float horizontalDirection)
        {
            if (Mathf.Abs(horizontalDirection) < facingVelocityThreshold)
            {
                return;
            }

            bool faceRight = horizontalDirection > 0f;
            if (bruteVisual != null)
            {
                bruteVisual.flipX = sourceSpriteFacesRight ? !faceRight : faceRight;
            }

            corruptionFacing?.Face(horizontalDirection);
            melee?.SetCombatFacing(horizontalDirection);
        }

        private Transform FindVisualChild(string fragment)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child != transform && child.name.ToLowerInvariant().Contains(fragment))
                {
                    return child;
                }
            }

            return null;
        }
    }
}
