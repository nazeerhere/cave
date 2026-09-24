using System;
using Cave.Combat;
using Cave.Enemies;
using Cave.Player;
using UnityEngine;

namespace Cave.Domain
{
    public enum ClaimWraithEchoAffinity { Poison, Slow, Stun }
    public enum ClaimWraithAction { Idle, MoveFloat, BasicAttack, Block, GuardBreak, ShadowStep, Hurt, Death }

    /// <summary>Lightweight Claim overlay for a regular Skeleton instance.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkeletonBrain), typeof(EnemyMeleeCombat), typeof(Damageable))]
    public sealed class ClaimWraith : MonoBehaviour
    {
        [SerializeField] private ClaimWraithEchoAffinity echoAffinity;
        [SerializeField] private ClaimWraithAction currentAction;
        [SerializeField] private bool spawnedByBuapahShadowDash;
        [SerializeField, Min(0f)] private float nextShadowStepTime;
        [SerializeField, Min(0f)] private float nextHardControlTime;
        [SerializeField, Min(0.1f)] private float shadowStepCooldown = 5f;
        [SerializeField, Min(0.1f)] private float shadowStepMinimumDistance = 2.5f;
        [SerializeField, Min(0f)] private float stunInternalCooldown = 4f;

        private EnemyMeleeCombat melee;
        private EnemyDefenseController defense;
        private EnemyStagger stagger;
        private Damageable health;
        private bool shadowStepping;

        public ClaimWraithEchoAffinity EchoAffinity => echoAffinity;
        public ClaimWraithAction CurrentAction => currentAction;
        public bool ShadowStepReady => !shadowStepping && Time.time >= nextShadowStepTime;
        public bool SpawnedByBuapahShadowDash => spawnedByBuapahShadowDash;

        private void Awake()
        {
            melee = GetComponent<EnemyMeleeCombat>();
            defense = GetComponent<EnemyDefenseController>();
            stagger = GetComponent<EnemyStagger>();
            health = GetComponent<Damageable>();
            echoAffinity = (ClaimWraithEchoAffinity)UnityEngine.Random.Range(0, 3);
            currentAction = ClaimWraithAction.Idle;
        }

        private void OnEnable()
        {
            ConfigureBaselineCombat();
            if (melee != null) melee.AttackHitResolved += ApplyAffinity;
            if (health != null) health.Died += HandleDeath;
        }

        private void Start()
        {
            // SkeletonInheritance initializes in Awake/Start; apply Wraith
            // baseline after it without altering the regular Skeleton contract.
            ConfigureBaselineCombat();
        }

        private void ConfigureBaselineCombat()
        {
            melee?.ConfigureSkeletonGuardBreak(true);
            defense?.ConfigureSkeletonBlock(0.22f, 0.22f, 0.25f, 1.1f);
        }

        private void OnDisable()
        {
            if (melee != null) melee.AttackHitResolved -= ApplyAffinity;
            if (health != null) health.Died -= HandleDeath;
            shadowStepping = false;
        }

        public void MarkSpawnedByBuapahShadowDash()
        {
            spawnedByBuapahShadowDash = true;
        }

        /// <summary>Called by SkeletonBrain before ordinary pursuit; it never replaces normal melee.</summary>
        public bool TryShadowStep(PlayerHealth target, Vector2 toPlayer)
        {
            if (target == null || shadowStepping || Time.time < nextShadowStepTime
                || (health != null && health.CurrentHealth <= 0) || (stagger != null && !stagger.CanAct)
                || (melee != null && melee.IsAttacking) || (defense != null && defense.IsActivelyBlocking)
                || toPlayer.magnitude < shadowStepMinimumDistance || UnityEngine.Random.value > 0.12f)
            {
                return false;
            }

            Vector2 destination = (Vector2)target.transform.position + new Vector2(
                UnityEngine.Random.value < 0.5f ? -1.65f : 1.65f, 0f);
            if (Physics2D.OverlapCircle(destination, 0.25f) != null)
            {
                return false;
            }

            shadowStepping = true;
            nextShadowStepTime = Time.time + shadowStepCooldown;
            currentAction = ClaimWraithAction.ShadowStep;
            ClaimWraithPresentation presentation = GetComponent<ClaimWraithPresentation>();
            presentation?.SetAction(currentAction);
            StartCoroutine(PerformShadowStep(destination));
            return true;
        }

        private System.Collections.IEnumerator PerformShadowStep(Vector2 destination)
        {
            yield return new WaitForSeconds(0.22f);
            transform.position = destination;
            yield return new WaitForSeconds(0.12f);
            shadowStepping = false;
            currentAction = ClaimWraithAction.Idle;
            GetComponent<ClaimWraithPresentation>()?.SetAction(currentAction);
        }

        private void ApplyAffinity(PlayerHealth target, DamageTrait traits)
        {
            if (target == null) return;
            switch (echoAffinity)
            {
                case ClaimWraithEchoAffinity.Poison:
                    PlayerPoisonStatus poison = target.GetComponent<PlayerPoisonStatus>() ?? target.gameObject.AddComponent<PlayerPoisonStatus>();
                    poison.ApplyPoison(1, 1f, 3f, gameObject);
                    break;
                case ClaimWraithEchoAffinity.Slow:
                    PlayerSlowStatus slow = target.GetComponent<PlayerSlowStatus>() ?? target.gameObject.AddComponent<PlayerSlowStatus>();
                    slow.ApplySlow(0.78f, 1.5f);
                    break;
                case ClaimWraithEchoAffinity.Stun:
                    if (Time.time >= nextHardControlTime)
                    {
                        PlayerStunStatus stun = target.GetComponent<PlayerStunStatus>() ?? target.gameObject.AddComponent<PlayerStunStatus>();
                        stun.ApplyStun(0.28f);
                        nextHardControlTime = Time.time + stunInternalCooldown;
                    }
                    break;
            }
        }

        private void HandleDeath()
        {
            currentAction = ClaimWraithAction.Death;
            GetComponent<ClaimWraithPresentation>()?.SetAction(currentAction);
        }
    }
}
