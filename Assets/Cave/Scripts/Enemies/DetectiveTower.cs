using System;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [Serializable]
    public struct DetectiveTowerDebuffStage
    {
        [Range(0f, 1f)] public float HealthRegenerationMultiplier;
        [Range(0f, 1f)] public float StaminaRegenerationMultiplier;

        public DetectiveTowerDebuffStage(float health, float stamina)
        {
            HealthRegenerationMultiplier = health;
            StaminaRegenerationMultiplier = stamina;
        }
    }

    [DisallowMultipleComponent]
    public sealed class DetectiveTower : MonoBehaviour
    {
        private static readonly System.Collections.Generic.HashSet<DetectiveTower> ActiveTowers =
            new System.Collections.Generic.HashSet<DetectiveTower>();

        private DetectiveEncounterCoordinator coordinator;
        private PlayerRecoveryModifiers affectedPlayer;
        private PlayerMana affectedPlayerMana;
        private Damageable damageable;
        private LineRenderer boundary;
        private LineRenderer core;
        private Material boundaryMaterial;
        private Material coreMaterial;
        private float currentRadius;
        private float maximumRadius;
        private float growthPerSecond;
        private DetectiveTowerDebuffStage[] stageMultipliers;
        private float[] manaCostMultipliers;
        private float[] dropRateMultipliers;
        private int researchStage;
        private bool initialized;

        public float CurrentRadius => currentRadius;
        public float MaximumRadius => maximumRadius;
        public float RadiusGrowthPerSecond => growthPerSecond;
        public bool IsAtMaximumRadius => currentRadius >= maximumRadius - 0.01f;
        public int ResearchStage => researchStage;
        public DetectiveTowerDebuffStage CurrentDebuffStage =>
            stageMultipliers != null && stageMultipliers.Length > 0
                ? stageMultipliers[Mathf.Clamp(researchStage, 0, stageMultipliers.Length - 1)]
                : new DetectiveTowerDebuffStage(1f, 1f);
        public float CurrentManaCostMultiplier => ResolveStageValue(manaCostMultipliers, 1f);
        public float CurrentDropRateMultiplier => ResolveStageValue(dropRateMultipliers, 1f);

        public void Initialize(
            DetectiveEncounterCoordinator owner,
            int maximumHealth,
            float initialRadius,
            float requestedMaximumRadius,
            float requestedGrowthPerSecond,
            DetectiveTowerDebuffStage[] recoveryMultipliers,
            float[] requestedManaCostMultipliers,
            float[] requestedDropRateMultipliers)
        {
            coordinator = owner;
            currentRadius = Mathf.Max(0.5f, initialRadius);
            maximumRadius = Mathf.Max(currentRadius, requestedMaximumRadius);
            growthPerSecond = Mathf.Max(0f, requestedGrowthPerSecond);
            stageMultipliers = recoveryMultipliers != null && recoveryMultipliers.Length > 0
                ? recoveryMultipliers
                : new[] { new DetectiveTowerDebuffStage(0.65f, 0.7f) };
            manaCostMultipliers = requestedManaCostMultipliers != null
                && requestedManaCostMultipliers.Length > 0
                    ? requestedManaCostMultipliers
                    : new[] { 1.1f, 1.2f, 1.35f, 1.5f };
            dropRateMultipliers = requestedDropRateMultipliers != null
                && requestedDropRateMultipliers.Length > 0
                    ? requestedDropRateMultipliers
                    : new[] { 0.9f, 0.75f, 0.55f, 0.35f };

            EnsureCombatBody(Mathf.Max(1, maximumHealth));
            EnsureVisuals();
            initialized = true;
            ApplyResearchStage(0);
        }

        public void ApplyResearchStage(int stage)
        {
            researchStage = Mathf.Clamp(stage, 0, stageMultipliers.Length - 1);
            RefreshPlayerModifier();
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            currentRadius = Mathf.Min(
                maximumRadius,
                currentRadius + growthPerSecond * Time.deltaTime);
            UpdateBoundary();
            RefreshPlayerModifier();
        }

        private void RefreshPlayerModifier()
        {
            PlayerHealth player = FindObjectOfType<PlayerHealth>();
            bool playerInside = player != null
                && player.gameObject.activeInHierarchy
                && ((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude
                    <= currentRadius * currentRadius;
            PlayerRecoveryModifiers modifiers = playerInside
                ? player.GetComponent<PlayerRecoveryModifiers>()
                : null;
            PlayerMana playerMana = playerInside ? player.GetComponent<PlayerMana>() : null;
            if (playerInside && modifiers == null)
            {
                modifiers = player.gameObject.AddComponent<PlayerRecoveryModifiers>();
            }

            if (affectedPlayer != null && affectedPlayer != modifiers)
            {
                affectedPlayer.RemoveModifier(this);
            }

            if (affectedPlayerMana != null && affectedPlayerMana != playerMana)
            {
                affectedPlayerMana.RemoveCostModifier(this);
            }

            affectedPlayer = modifiers;
            affectedPlayerMana = playerMana;
            if (affectedPlayer == null)
            {
                return;
            }

            DetectiveTowerDebuffStage multipliers = stageMultipliers[researchStage];
            affectedPlayer.SetModifier(
                this,
                multipliers.HealthRegenerationMultiplier,
                multipliers.StaminaRegenerationMultiplier,
                1f);
            affectedPlayerMana?.SetCostModifier(this, CurrentManaCostMultiplier);
        }

        public static float GetOrdinaryDropRateMultiplier(Vector2 deathPosition)
        {
            float multiplier = 1f;
            foreach (DetectiveTower tower in ActiveTowers)
            {
                if (tower != null
                    && tower.initialized
                    && tower.gameObject.activeInHierarchy
                    && tower.ContainsPoint(deathPosition))
                {
                    multiplier = Mathf.Min(multiplier, tower.CurrentDropRateMultiplier);
                }
            }

            return multiplier;
        }

        public static bool IsPlayerInsideAnyTower(Vector2 playerPosition)
        {
            foreach (DetectiveTower tower in ActiveTowers)
            {
                if (tower != null
                    && tower.initialized
                    && tower.gameObject.activeInHierarchy
                    && tower.ContainsPoint(playerPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsPoint(Vector2 position)
        {
            return ((Vector2)transform.position - position).sqrMagnitude
                <= currentRadius * currentRadius;
        }

        private float ResolveStageValue(float[] values, float fallback)
        {
            return values != null && values.Length > 0
                ? values[Mathf.Clamp(researchStage, 0, values.Length - 1)]
                : fallback;
        }

        private void EnsureCombatBody(int maximumHealth)
        {
            int damageableLayer = LayerMask.NameToLayer("Damageable");
            if (damageableLayer >= 0)
            {
                gameObject.layer = damageableLayer;
            }

            damageable = GetComponent<Damageable>();
            if (damageable == null)
            {
                damageable = gameObject.AddComponent<Damageable>();
            }

            damageable.SetRuntimeMaximumHealth(maximumHealth, true);
            damageable.Died -= HandleDestroyed;
            damageable.Died += HandleDestroyed;

            BoxCollider2D hitbox = GetComponent<BoxCollider2D>();
            if (hitbox == null)
            {
                hitbox = gameObject.AddComponent<BoxCollider2D>();
                hitbox.size = new Vector2(0.9f, 1.8f);
                hitbox.offset = new Vector2(0f, 0.9f);
                hitbox.isTrigger = true;
            }
        }

        private void EnsureVisuals()
        {
            boundary = GetComponent<LineRenderer>();
            if (boundary == null)
            {
                boundaryMaterial = new Material(Shader.Find("Sprites/Default"));
                boundary = gameObject.AddComponent<LineRenderer>();
                boundary.material = boundaryMaterial;
                boundary.useWorldSpace = false;
                boundary.loop = true;
                boundary.positionCount = 64;
                boundary.startWidth = 0.12f;
                boundary.endWidth = 0.12f;
                boundary.startColor = new Color(0.5f, 0.2f, 0.85f, 0.82f);
                boundary.endColor = boundary.startColor;
                boundary.sortingOrder = 4;
            }

            Transform existingCore = transform.Find("Tower Core Visual");
            GameObject coreObject = existingCore != null
                ? existingCore.gameObject
                : new GameObject("Tower Core Visual");
            if (existingCore == null)
            {
                coreObject.transform.SetParent(transform, false);
            }

            core = coreObject.GetComponent<LineRenderer>();
            if (core == null)
            {
                coreMaterial = new Material(Shader.Find("Sprites/Default"));
                core = coreObject.AddComponent<LineRenderer>();
                core.material = coreMaterial;
                core.useWorldSpace = false;
                core.loop = true;
                core.positionCount = 4;
                core.startWidth = 0.14f;
                core.endWidth = 0.14f;
                core.startColor = new Color(0.25f, 0.9f, 1f, 0.95f);
                core.endColor = core.startColor;
                core.sortingOrder = 5;
                core.SetPosition(0, new Vector3(0f, 1.7f));
                core.SetPosition(1, new Vector3(0.42f, 0.85f));
                core.SetPosition(2, new Vector3(0f, 0f));
                core.SetPosition(3, new Vector3(-0.42f, 0.85f));
            }

            UpdateBoundary();
        }

        private void UpdateBoundary()
        {
            if (boundary == null)
            {
                return;
            }

            for (int index = 0; index < boundary.positionCount; index++)
            {
                float angle = index / (float)boundary.positionCount * Mathf.PI * 2f;
                boundary.SetPosition(
                    index,
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * currentRadius);
            }
        }

        private void HandleDestroyed()
        {
            RemoveModifier();
            coordinator?.NotifyTowerDestroyed(this);
            Destroy(gameObject);
        }

        private void RemoveModifier()
        {
            if (affectedPlayer != null)
            {
                affectedPlayer.RemoveModifier(this);
                affectedPlayer = null;
            }

            if (affectedPlayerMana != null)
            {
                affectedPlayerMana.RemoveCostModifier(this);
                affectedPlayerMana = null;
            }
        }

        private void OnDisable()
        {
            ActiveTowers.Remove(this);
            RemoveModifier();
        }

        private void OnEnable()
        {
            ActiveTowers.Add(this);
        }

        private void OnDestroy()
        {
            RemoveModifier();
            if (damageable != null)
            {
                damageable.Died -= HandleDestroyed;
            }

            coordinator?.NotifyTowerDestroyed(this);
            if (boundaryMaterial != null)
            {
                Destroy(boundaryMaterial);
            }

            if (coreMaterial != null)
            {
                Destroy(coreMaterial);
            }
        }
    }
}
