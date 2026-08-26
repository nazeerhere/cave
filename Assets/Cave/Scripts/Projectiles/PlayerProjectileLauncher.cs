using System;
using Cave.Audio;
using Cave.Combat;
using Cave.InputSystem;
using Cave.Player;
using Cave.Progression;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cave.Projectiles
{
    [RequireComponent(typeof(PlayerMana), typeof(PlayerSpecialMode), typeof(PlayerDamageBoost))]
    [RequireComponent(typeof(PlayerAimDirection))]
    public sealed class PlayerProjectileLauncher : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField] private PlayerProjectile projectilePrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Vector2 spawnOffset = new Vector2(0.8f, 0.1f);
        [FormerlySerializedAs("fireCooldown")]
        [SerializeField, Min(0.05f)] private float heldFireInterval = 0.35f;
        [SerializeField, Min(0.01f)] private float manaCost = 10f;

        private PlayerMana playerMana;
        private PlayerSpecialMode specialMode;
        private PlayerDamageBoost damageBoost;
        private PlayerResourceMastery resourceMastery;
        private PlayerSpecialModeUpgradeState upgradeState;
        private PlayerAimDirection aimDirection;
        private PlayerController playerController;
        private PlayerCombatFlow combatFlow;
        private float nextFireTime;

        public event Action<string> FeedbackRequested;
        public event Action<Vector2> ProjectileFired;

        public float ManaCost => manaCost;

        internal void UseProjectilePrefabIfMissing(PlayerProjectile defaultPrefab)
        {
            if (projectilePrefab == null)
            {
                projectilePrefab = defaultPrefab;
            }
        }

        private void Awake()
        {
            playerMana = GetComponent<PlayerMana>();
            specialMode = GetComponent<PlayerSpecialMode>();
            damageBoost = GetComponent<PlayerDamageBoost>();
            resourceMastery = GetComponent<PlayerResourceMastery>();
            upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
            aimDirection = GetComponent<PlayerAimDirection>();
            playerController = GetComponent<PlayerController>();
            combatFlow = GetComponent<PlayerCombatFlow>();
            EnsureSpawnPoint();
        }

        private void Update()
        {
            if (GameInput.FireProjectilePressed)
            {
                TryFire(true);
            }
            else if (GameInput.FireProjectileHeld && Time.time >= nextFireTime)
            {
                TryFire(false);
            }
        }

        public bool TryFire()
        {
            return TryFire(true);
        }

        private bool TryFire(bool allowResourceFeedback)
        {
            PlayerGuardBreak guardBreak = GetComponent<PlayerGuardBreak>();
            if ((guardBreak != null && !guardBreak.CanUseCombatActions)
                || projectilePrefab == null
                || Time.time < nextFireTime)
            {
                return false;
            }

            if (!playerMana.TrySpendMana(manaCost))
            {
                nextFireTime = Time.time + heldFireInterval;
                if (allowResourceFeedback)
                {
                    FeedbackRequested?.Invoke("Not enough mana.");
                }

                return false;
            }

            if (aimDirection == null)
            {
                aimDirection = GetComponent<PlayerAimDirection>();
            }

            Vector2 direction = aimDirection != null ? aimDirection.ReadDirection() : Vector2.right;
            if (IsGroundedPureDownAim(direction))
            {
                direction = aimDirection != null ? aimDirection.FacingDirection : Vector2.right;
            }

            Vector2 localSpawnPosition = direction * Mathf.Abs(spawnOffset.x) + Vector2.up * spawnOffset.y;
            spawnPoint.localPosition = new Vector3(localSpawnPosition.x, localSpawnPosition.y, 0f);

            float projectileAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            PlayerProjectile projectile = Instantiate(
                projectilePrefab,
                spawnPoint.position,
                Quaternion.Euler(0f, 0f, projectileAngle));
            int projectileDamage = damageBoost.ResolveProjectileDamage(projectile.BaseDamage);
            if (resourceMastery == null)
            {
                resourceMastery = GetComponent<PlayerResourceMastery>();
            }

            DamageContext damageContext = resourceMastery != null
                ? resourceMastery.CreateManaDamageContext()
                : default;
            if (upgradeState == null)
            {
                upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
            }

            int currentTier = upgradeState != null
                ? upgradeState.GetCurrentTier(specialMode.CurrentMode)
                : 1;
            if (upgradeState != null && upgradeState.Settings != null)
            {
                projectileDamage += upgradeState.Settings.GetProjectileDamageBonus(
                    specialMode.CurrentMode,
                    currentTier);
            }

            int maximumEnemyHits = ResolveMaximumEnemyHits(specialMode.CurrentMode, currentTier);
            FrenzyBreakActivation frenzyActivation = null;
            if (combatFlow == null)
            {
                combatFlow = GetComponent<PlayerCombatFlow>();
            }

            combatFlow?.TryCommitFrenzyBreak(
                FrenzyBreakAttackKind.Projectile,
                out frenzyActivation);
            projectile.SetFrenzyBreakActivation(frenzyActivation);
            projectile.Launch(
                direction,
                specialMode.CurrentMode,
                projectileDamage,
                damageContext,
                maximumEnemyHits,
                currentTier,
                upgradeState != null ? upgradeState.Settings : null);
            CaveSfx.Play(CaveSfxCue.Shot, 0.8f);
            nextFireTime = Time.time + heldFireInterval;
            ProjectileFired?.Invoke(direction);
            return true;
        }

        private bool IsGroundedPureDownAim(Vector2 direction)
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            return playerController != null
                && playerController.IsGrounded
                && Mathf.Abs(direction.x) < 0.01f
                && direction.y < -0.99f;
        }

        private int ResolveMaximumEnemyHits(SpecialMode mode, int tier)
        {
            if (upgradeState == null
                || upgradeState.Settings == null
                || tier < 2)
            {
                return 1;
            }

            return upgradeState.Settings.GetProjectilePierceCount(mode, tier);
        }

        private void EnsureSpawnPoint()
        {
            if (spawnPoint != null)
            {
                return;
            }

            GameObject spawnPointObject = new GameObject("Player Projectile Spawn");
            spawnPointObject.transform.SetParent(transform, false);
            spawnPointObject.transform.localPosition = new Vector3(spawnOffset.x, spawnOffset.y, 0f);
            spawnPoint = spawnPointObject.transform;
        }
    }
}
