using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using Cave.Progression;
using UnityEngine;

namespace Cave.Enemies
{
    public enum CorruptionAlteration
    {
        None,
        Frenzied,
        Armored,
        Regenerative,
        Relentless,
        Enlarged,
        Quickened,
        StaggerResistant
    }

    public enum CorruptionElement
    {
        None,
        Fire,
        Frost,
        Magic
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyCorruptionLifecycle : MonoBehaviour
    {
        private static readonly List<CorruptionAlteration> AlterationBag =
            new List<CorruptionAlteration>();
        private static CorruptionAlteration lastDraw;

        [Header("Mutation Strengths")]
        [SerializeField, Min(1f)] private float armoredHealthMultiplier = 1.6f;
        [SerializeField, Min(1f)] private float frenziedDamageMultiplier = 1.3f;
        [SerializeField, Min(1f)] private float frenziedSpeedMultiplier = 1.1f;
        [SerializeField, Min(0f)] private float regenerationHealthPerSecond = 0.6f;
        [SerializeField, Range(0.1f, 1f)] private float relentlessStaggerDurationMultiplier = 0.65f;
        [SerializeField, Min(1f)] private float enlargedScaleMultiplier = 1.18f;
        [SerializeField, Min(1f)] private float enlargedHealthMultiplier = 1.25f;
        [SerializeField, Min(1f)] private float enlargedDamageMultiplier = 1.15f;
        [SerializeField, Min(1f)] private float quickenedSpeedMultiplier = 1.25f;
        [SerializeField, Range(0f, 0.9f)] private float staggerResistance = 0.5f;

        [Header("Debug (Read Only)")]
        [SerializeField, Min(1)] private int currentLife = 1;
        [SerializeField, Min(0)] private int corruptedRevivalCount;
        [SerializeField] private bool lowHealthRollResolved;
        [SerializeField] private bool corrupted;
        [SerializeField] private bool revivalPending;
        [SerializeField] private bool finalDeath;
        [SerializeField] private CorruptionAlteration selectedAlteration;
        [SerializeField] private CorruptionElement selectedElement;
        [SerializeField] private bool lastFireFrostReaction;

        private Damageable damageable;
        private EnemyArchetypeProfile archetype;
        private Vector3 authoredScale;
        private float regenerationCarry;
        private bool lowHealthTransformation;
        private AuthoredCorruptedPrefabBinding authoredPrefabBinding;

        public int CurrentLife => currentLife;
        public CorruptionAlteration SelectedAlteration => selectedAlteration;
        public CorruptionElement SelectedElement => selectedElement;
        public bool IsCorrupted => corrupted;
        public bool IsRegenerating => corrupted
            && selectedAlteration == CorruptionAlteration.Regenerative
            && damageable != null
            && damageable.CurrentHealth < damageable.MaximumHealth;
        public bool IsFrenzied => corrupted && selectedAlteration == CorruptionAlteration.Frenzied;
        public bool SuppressStandardRespawn => revivalPending || finalDeath;
        public bool SuppressDeathRewards => revivalPending;
        public bool LastFireFrostReaction
        {
            get => lastFireFrostReaction;
            internal set => lastFireFrostReaction = value;
        }

        /// <summary>
        /// Replaces this living enemy with its explicitly authored corrupted form.
        /// This is a same-life visual/mechanical handoff: it never invokes a death
        /// event, schedules a revival, or grants death rewards.
        /// </summary>
        public bool TryTransformIntoAuthoredPrefabForWizardCorruption(
            float healthFraction,
            int wizardCorruptionStage,
            float wizardPermanentSlow)
        {
            if (corrupted || !HasAuthoredReplacement())
            {
                return false;
            }

            Transform parent = transform.parent;
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            float preservedHealthFraction = Mathf.Clamp01(healthFraction);

            // Disable first so coordinator/identity registrations from the
            // replacement cannot overlap with the outgoing living instance.
            gameObject.SetActive(false);
            GameObject replacementObject;
            try
            {
                replacementObject = Instantiate(
                    authoredPrefabBinding.AuthoredCorruptedPrefab,
                    position,
                    rotation,
                    parent);
            }
            catch
            {
                gameObject.SetActive(true);
                return false;
            }

            Damageable replacementDamageable = replacementObject.GetComponent<Damageable>();
            if (replacementDamageable == null)
            {
                Destroy(replacementObject);
                gameObject.SetActive(true);
                return false;
            }

            EnemyCorruptionLifecycle replacement =
                replacementObject.GetComponent<EnemyCorruptionLifecycle>();
            if (replacement == null)
            {
                replacement = replacementObject.AddComponent<EnemyCorruptionLifecycle>();
            }

            replacement.ReceiveWizardCorruptionTransformationState(
                preservedHealthFraction,
                wizardCorruptionStage,
                wizardPermanentSlow);
            Destroy(gameObject);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            AlterationBag.Clear();
            lastDraw = CorruptionAlteration.None;
        }

        public static void EnsureOnExistingEnemies()
        {
            foreach (Damageable candidate in FindObjectsOfType<Damageable>())
            {
                if (candidate == null
                    || candidate.GetComponent<DetectiveTower>() != null
                    || candidate.GetComponent<EnemyCorruptionLifecycle>() != null
                    || !IsEligibleEnemy(candidate.gameObject))
                {
                    continue;
                }

                candidate.gameObject.AddComponent<EnemyCorruptionLifecycle>();
            }
        }

        private static bool IsEligibleEnemy(GameObject candidate)
        {
            return candidate.GetComponent<EnemyDifficultyScaler>() != null
                || candidate.GetComponent<EnemyArchetypeProfile>() != null
                || candidate.GetComponent<MobBrainBase>() != null
                || candidate.GetComponent<EnemyController>() != null
                || candidate.GetComponent<FlyingSwarmController>() != null
                || candidate.GetComponent<WizardFlightMotor>() != null
                || candidate.GetComponent<BossPhaseController>() != null;
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            archetype = GetComponent<EnemyArchetypeProfile>();
            authoredPrefabBinding = GetComponent<AuthoredCorruptedPrefabBinding>();
            authoredScale = transform.localScale;
            damageable.DamageResolved += HandleDamageResolved;
            EnemyWorldStatusIndicators.EnsureOn(gameObject);
        }

        private void Update()
        {
            if (!corrupted
                || selectedAlteration != CorruptionAlteration.Regenerative
                || damageable.CurrentHealth <= 0
                || damageable.CurrentHealth >= damageable.MaximumHealth)
            {
                regenerationCarry = 0f;
                return;
            }

            regenerationCarry += regenerationHealthPerSecond * Time.deltaTime;
            int wholeHealth = Mathf.FloorToInt(regenerationCarry);
            if (wholeHealth > 0)
            {
                int restored = damageable.RestoreHealthResolved(wholeHealth);
                regenerationCarry -= restored;
            }
        }

        public void PrepareForDeath()
        {
            PlayerCurseController curses = PlayerCurseController.Active;
            if (corrupted && (curses == null || !curses.CavesGlareActive))
            {
                finalDeath = true;
                return;
            }

            if (curses == null || !curses.CavesGlareActive)
            {
                return;
            }

            if (corrupted)
            {
                if (lowHealthTransformation && curses.AllowRevivalAfterLowHealthCorruption
                    && CanReviveAgain(curses) && !revivalPending)
                {
                    ScheduleRevival(curses);
                }
                else
                {
                    finalDeath = true;
                }

                return;
            }

            bool boss = GetComponent<BossPhaseController>() != null;
            float chance = boss ? 1f : curses.OrdinaryRevivalCorruptionChance;
            if (Random.value <= chance)
            {
                if (!HasAuthoredReplacement())
                {
                    Debug.LogWarning(
                        "The Cave's Glare selected " + name
                        + " for corruption, but no authored corrupted prefab binding is assigned. "
                        + "Skipping revival and resolving this as the final death.",
                        this);
                    finalDeath = true;
                    return;
                }

                SelectMutation(boss, curses);
                ScheduleRevival(curses);
            }
        }

        internal void ResetForUncorruptedStandardSpawn()
        {
            if (currentLife != 1
                || corrupted
                || revivalPending
                || selectedAlteration != CorruptionAlteration.None)
            {
                return;
            }

            lowHealthRollResolved = false;
            lowHealthTransformation = false;
            finalDeath = false;
        }

        private void HandleDamageResolved(DamageContext _, bool blocked, int appliedDamage)
        {
            if (blocked || appliedDamage <= 0 || corrupted || lowHealthRollResolved)
            {
                return;
            }

            PlayerCurseController curses = PlayerCurseController.Active;
            if (curses == null || !curses.CavesGlareActive || damageable.MaximumHealth <= 0)
            {
                return;
            }

            float healthFraction = damageable.CurrentHealth / (float)damageable.MaximumHealth;
            if (healthFraction > curses.LowHealthCorruptionThreshold || damageable.CurrentHealth <= 0)
            {
                return;
            }

            lowHealthRollResolved = true;
            bool boss = GetComponent<BossPhaseController>() != null;
            if (boss || Random.value <= curses.LowHealthCorruptionChance)
            {
                lowHealthTransformation = true;
                SelectMutation(boss, curses);
                // A bound authored form must remain visually untouched until the
                // death/revival handoff. Generic tint/scale mutation remains only
                // for legacy enemies without an authored replacement.
                if (!HasAuthoredReplacement())
                {
                    ApplyMutation(false, curses);
                }
            }
        }

        private void ScheduleRevival(PlayerCurseController curses)
        {
            revivalPending = true;
            CorruptionRevivalScheduler.Schedule(this, curses.CorruptedRevivalDelay);
        }

        private bool CanReviveAgain(PlayerCurseController curses)
        {
            return curses != null
                && (curses.MaximumCorruptedRevivals == 0
                    || corruptedRevivalCount < curses.MaximumCorruptedRevivals);
        }

        internal void CompleteRevival()
        {
            if (this == null || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                return;
            }

            if (!HasAuthoredReplacement())
            {
                revivalPending = false;
                finalDeath = true;
                return;
            }

            GameObject replacementObject = Instantiate(
                authoredPrefabBinding.AuthoredCorruptedPrefab,
                transform.position,
                transform.rotation,
                transform.parent);
            EnemyCorruptionLifecycle replacement = replacementObject.GetComponent<EnemyCorruptionLifecycle>();
            if (replacement == null)
            {
                replacement = replacementObject.AddComponent<EnemyCorruptionLifecycle>();
            }

            replacement.ReceiveAuthoredReplacementState(
                selectedAlteration,
                selectedElement,
                authoredPrefabBinding.GrantGeneralShardOnFinalDeath,
                corruptedRevivalCount + 1);
            revivalPending = false;
            finalDeath = true;
            // The original remains dead. The authored prefab owns its visual and
            // standard setup; only approved mechanical corruption state transfers.
        }

        internal void ReceiveAuthoredReplacementState(
            CorruptionAlteration alteration,
            CorruptionElement element,
            bool grantBruteShard,
            int inheritedCorruptedRevivalCount)
        {
            currentLife = Mathf.Max(2, inheritedCorruptedRevivalCount + 1);
            corruptedRevivalCount = Mathf.Max(0, inheritedCorruptedRevivalCount);
            revivalPending = false;
            finalDeath = false;
            corrupted = true;
            selectedAlteration = alteration;
            selectedElement = element;
            // A revived authored corruption remains eligible for the next
            // Cave's Glare revival according to the configured cap.
            lowHealthTransformation = true;
            lowHealthRollResolved = true;
            EnemyDifficultyScaler scaler = GetComponent<EnemyDifficultyScaler>();
            scaler?.ApplyForSpawn();
            GetComponent<EnemyController>()?.ResetForRespawn();
            GetComponent<FlyingSwarmController>()?.ResetForRespawn();
            GetComponent<WizardFlightMotor>()?.ResetForRespawn();
            damageable.RestoreToFullHealth();
            ApplyMutation(true, PlayerCurseController.Active, true);
            // This is a presentation-only hook for the imported Light Bandit
            // recover clip. It does not change replacement or curse behavior.
            GetComponent<LightBanditAnimator>()?.PlayRecover();
            bool isBrute = GetComponentInChildren<EnemyMeleeCombat>(true) != null
                && GetComponentInChildren<EnemyMeleeCombat>(true).IsBrutePreset;
            if ((grantBruteShard || isBrute)
                && GetComponent<CorruptedBruteGeneralShardReward>() == null)
            {
                gameObject.AddComponent<CorruptedBruteGeneralShardReward>();
            }
        }

        private void ReceiveWizardCorruptionTransformationState(
            float healthFraction,
            int wizardCorruptionStage,
            float wizardPermanentSlow)
        {
            // The prefab is the canonical presentation. Only current-life health
            // ratio and the existing Wizard corruption slow are carried over.
            currentLife = 1;
            revivalPending = false;
            finalDeath = false;
            corrupted = true;
            lowHealthTransformation = false;
            lowHealthRollResolved = true;
            selectedAlteration = CorruptionAlteration.None;
            selectedElement = CorruptionElement.None;
            EnemyDifficultyScaler scaler = GetComponent<EnemyDifficultyScaler>();
            scaler?.ApplyForSpawn();
            damageable.SetRuntimeMaximumHealthPreservingRatio(
                damageable.MaximumHealth);
            int targetHealth = Mathf.Clamp(
                Mathf.CeilToInt(damageable.MaximumHealth * Mathf.Clamp01(healthFraction)),
                1,
                damageable.MaximumHealth);
            damageable.RestoreHealthResolved(damageable.MaximumHealth);
            damageable.TakeDamage(
                Mathf.Max(0, damageable.MaximumHealth - targetHealth),
                default);
            GetComponent<WizardHealingCorruption>()?.ReceiveTransferredState(
                wizardCorruptionStage,
                wizardPermanentSlow);
        }

        private void SelectMutation(bool boss, PlayerCurseController curses)
        {
            selectedAlteration = DrawAlteration(BuildEligibleAlterations(boss));
            selectedElement = Random.value <= curses.ElementalCorruptionChance
                ? (CorruptionElement)Random.Range(1, 4)
                : CorruptionElement.None;
        }

        private List<CorruptionAlteration> BuildEligibleAlterations(bool boss)
        {
            if (archetype == null)
            {
                archetype = GetComponent<EnemyArchetypeProfile>();
            }

            bool support = archetype != null && archetype.Includes(EnemyArchetype.Support);
            bool ranged = archetype != null && archetype.Includes(EnemyArchetype.Ranged);
            bool tank = archetype != null && archetype.Includes(EnemyArchetype.Tank);
            List<CorruptionAlteration> eligible = new List<CorruptionAlteration>
            {
                CorruptionAlteration.Frenzied,
                CorruptionAlteration.Armored,
                CorruptionAlteration.Regenerative,
                CorruptionAlteration.Relentless,
                CorruptionAlteration.Quickened
            };
            if (!boss)
            {
                eligible.Add(CorruptionAlteration.Enlarged);
            }

            EnemyStagger stagger = GetComponent<EnemyStagger>();
            if (!boss && !tank && !support && !ranged && stagger != null && stagger.IsStaggerEligible)
            {
                eligible.Add(CorruptionAlteration.StaggerResistant);
            }

            // Small archetype weighting without making the result deterministic.
            if (tank)
            {
                eligible.Add(CorruptionAlteration.Armored);
            }
            else if (ranged || support)
            {
                eligible.Add(CorruptionAlteration.Quickened);
            }
            else
            {
                eligible.Add(CorruptionAlteration.Frenzied);
            }

            return eligible;
        }

        private static CorruptionAlteration DrawAlteration(List<CorruptionAlteration> eligible)
        {
            if (eligible == null || eligible.Count == 0)
            {
                return CorruptionAlteration.None;
            }

            if (AlterationBag.Count == 0)
            {
                AlterationBag.AddRange(eligible);
                for (int index = AlterationBag.Count - 1; index > 0; index--)
                {
                    int swap = Random.Range(0, index + 1);
                    CorruptionAlteration value = AlterationBag[index];
                    AlterationBag[index] = AlterationBag[swap];
                    AlterationBag[swap] = value;
                }
            }

            for (int index = AlterationBag.Count - 1; index >= 0; index--)
            {
                CorruptionAlteration candidate = AlterationBag[index];
                if (!eligible.Contains(candidate) || (candidate == lastDraw && eligible.Count > 1))
                {
                    continue;
                }

                AlterationBag.RemoveAt(index);
                lastDraw = candidate;
                return candidate;
            }

            AlterationBag.Clear();
            CorruptionAlteration fallback = eligible[Random.Range(0, eligible.Count)];
            lastDraw = fallback;
            return fallback;
        }

        private void ApplyMutation(
            bool restoreToFull,
            PlayerCurseController curses,
            bool preserveAuthoredVisuals = false)
        {
            corrupted = true;
            if (!preserveAuthoredVisuals)
            {
                transform.localScale = authoredScale;
            }
            EnemyDamageModifiers damageModifiers = GetComponent<EnemyDamageModifiers>();
            if (damageModifiers == null)
            {
                damageModifiers = gameObject.AddComponent<EnemyDamageModifiers>();
            }

            float healthMultiplier = 1f;
            float damageMultiplier = 1f;
            float speedMultiplier = 1f;
            switch (selectedAlteration)
            {
                case CorruptionAlteration.Frenzied:
                    damageMultiplier = frenziedDamageMultiplier;
                    speedMultiplier = frenziedSpeedMultiplier;
                    break;
                case CorruptionAlteration.Armored:
                    healthMultiplier = armoredHealthMultiplier;
                    break;
                case CorruptionAlteration.Relentless:
                    GetComponent<EnemyStagger>()?.SetDurationMultiplier(
                        relentlessStaggerDurationMultiplier);
                    break;
                case CorruptionAlteration.Enlarged:
                    if (!preserveAuthoredVisuals)
                    {
                        transform.localScale = Vector3.Scale(
                            authoredScale,
                            new Vector3(enlargedScaleMultiplier, enlargedScaleMultiplier, 1f));
                    }
                    healthMultiplier = enlargedHealthMultiplier;
                    damageMultiplier = enlargedDamageMultiplier;
                    break;
                case CorruptionAlteration.Quickened:
                    speedMultiplier = quickenedSpeedMultiplier;
                    break;
                case CorruptionAlteration.StaggerResistant:
                    GetComponent<EnemyStagger>()?.SetStaggerResistance(staggerResistance);
                    break;
            }

            damageModifiers.SetCorruptionDamageMultiplier(damageMultiplier);
            GetComponent<EnemyController>()?.SetCorruptionSpeedMultiplier(speedMultiplier);
            GetComponent<FlyingSwarmController>()?.SetCorruptionSpeedMultiplier(speedMultiplier);
            GetComponent<WizardFlightMotor>()?.SetCorruptionSpeedMultiplier(speedMultiplier);
            int mutatedMaximum = Mathf.Max(
                1,
                Mathf.CeilToInt(damageable.MaximumHealth * healthMultiplier));
            if (restoreToFull)
            {
                damageable.SetRuntimeMaximumHealth(mutatedMaximum, true);
            }
            else
            {
                damageable.SetRuntimeMaximumHealthPreservingRatio(mutatedMaximum);
            }

            EnemyElementalEmpowerment elemental = GetComponent<EnemyElementalEmpowerment>();
            if (elemental == null && selectedElement != CorruptionElement.None)
            {
                elemental = gameObject.AddComponent<EnemyElementalEmpowerment>();
            }

            elemental?.SetCorruptionElement(selectedElement, this);
            if (!preserveAuthoredVisuals)
            {
                damageable.SetPersistentTint(new Color(0.78f, 0.48f, 1f, 1f));
            }
            if (curses != null && curses.CorruptionTransformationVfxPrefab != null)
            {
                GameObject vfx = Instantiate(
                    curses.CorruptionTransformationVfxPrefab,
                    transform.position,
                    Quaternion.identity);
                Destroy(vfx, 3f);
            }
            else
            {
                CombatShapeEffect.Create(
                    transform.position,
                    CombatShape.Diamond,
                    0.8f,
                    new Color(0.7f, 0.2f, 1f, 0.9f),
                    0.65f);
            }
        }

        private void OnDestroy()
        {
            if (damageable != null)
            {
                damageable.DamageResolved -= HandleDamageResolved;
            }
        }

        private bool HasAuthoredReplacement()
        {
            if (authoredPrefabBinding == null)
            {
                authoredPrefabBinding = GetComponent<AuthoredCorruptedPrefabBinding>();
            }

            return authoredPrefabBinding != null && authoredPrefabBinding.HasValidMapping;
        }
    }

}
