using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    public enum StageTwoMobType { None, Skeleton, Brute, Detective, Wizard, Pyre }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyCorruptionLifecycle))]
    public sealed class StageTwoCorruptionState : MonoBehaviour, IEnemyInterruptible
    {
        private static readonly HashSet<StageTwoCorruptionState> ActiveSources = new HashSet<StageTwoCorruptionState>();
        [Header("Transformation")]
        [SerializeField, Range(0.01f, 1f)] private float healthThreshold = 0.35f;
        [SerializeField, Min(1)] private int requiredCompatibleAllies = 2;
        [SerializeField, Min(0.1f)] private float transformationSeconds = 1.5f;
        [SerializeField, Min(0.05f)] private float eligibilityInterval = 0.2f;
        [Header("Collective Network")]
        [SerializeField, Min(0.1f)] private float contributionRadius = 10f;
        [SerializeField, Range(0f, 1f)] private float maximumAdditionalHealth = 0.35f;
        [SerializeField, Range(0f, 1f)] private float maximumAdditionalStrength = 0.30f;
        [SerializeField, Range(0f, 1f)] private float maximumSharedResistance = 0.30f;
        [SerializeField, Min(0f)] private float meleeContribution = 0.08f;
        [SerializeField, Min(0f)] private float rangedContribution = 0.07f;
        [SerializeField, Min(0f)] private float tankContribution = 0.12f;
        [SerializeField, Min(0f)] private float supportContribution = 0.09f;
        [SerializeField, Min(0f)] private float swarmContribution = 0.04f;
        [SerializeField, Min(0f)] private float sameTypeResistanceContribution = 0.10f;
        [Header("Runtime (Read Only)")]
        [SerializeField] private StageTwoMobType mobType;
        [SerializeField] private bool transforming;
        [SerializeField] private bool stageTwoActive;
        [SerializeField, Range(0f, 1f)] private float currentHealthBonus;
        [SerializeField, Range(0f, 1f)] private float currentStrengthBonus;

        private Damageable damageable;
        private EnemyCorruptionLifecycle corruption;
        private EnemyDamageModifiers damageModifiers;
        private Coroutine transformation;
        private float nextEligibilityCheck;
        private float nextNetworkRefresh;
        private int networkBaseMaximumHealth;
        private FrenzyGeometryStructure transformationGeometry;
        private readonly HashSet<StageTwoCollectiveReceiver> recipients = new HashSet<StageTwoCollectiveReceiver>();

        public StageTwoMobType MobType => mobType;
        public bool IsStageTwoActive => stageTwoActive && IsLiving;
        public bool IsTransforming => transforming;
        public float CurrentHealthBonus => currentHealthBonus;
        public float CurrentStrengthBonus => currentStrengthBonus;
        private bool IsLiving => isActiveAndEnabled && damageable != null && damageable.CurrentHealth > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { ActiveSources.Clear(); }

        public static StageTwoCorruptionState EnsureOn(GameObject owner)
        {
            if (owner == null) return null;
            StageTwoCorruptionState state = owner.GetComponent<StageTwoCorruptionState>();
            return state != null ? state : owner.AddComponent<StageTwoCorruptionState>();
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            corruption = GetComponent<EnemyCorruptionLifecycle>();
            damageModifiers = GetComponent<EnemyDamageModifiers>();
            if (damageModifiers == null) damageModifiers = gameObject.AddComponent<EnemyDamageModifiers>();
            mobType = ResolveMobType(gameObject);
            damageable.Died += HandleDeath;
        }

        private void Update()
        {
            if (!stageTwoActive)
            {
                if (!transforming && Time.time >= nextEligibilityCheck)
                {
                    nextEligibilityCheck = Time.time + eligibilityInterval;
                    if (CanBeginTransformation()) transformation = StartCoroutine(TransformRoutine());
                }
                return;
            }
            if (Time.time >= nextNetworkRefresh)
            {
                nextNetworkRefresh = Time.time + eligibilityInterval;
                RefreshNetwork();
            }
        }

        public bool CanBeginTransformation()
        {
            return IsLiving && !stageTwoActive && !transforming && corruption != null && corruption.IsCorrupted
                && mobType != StageTwoMobType.None && mobType != StageTwoMobType.Pyre
                && damageable.CurrentHealth / (float)Mathf.Max(1, damageable.MaximumHealth) <= healthThreshold
                && CountCompatibleAllies() >= requiredCompatibleAllies;
        }

        private IEnumerator TransformRoutine()
        {
            transforming = true;
            transformationGeometry = gameObject.AddComponent<FrenzyGeometryStructure>();
            transformationGeometry.Configure(GeometricStructureKind.PhaseCoherence,
                PreGeometricPrinciple.DistributedInteraction, 2, 1f, false);
            CombatShapeEffect.Create(transform.position, CombatShape.Ring, 1.15f,
                new Color(0.75f, 0.15f, 1f, 0.9f), transformationSeconds, 0f, 0.08f);
            yield return new WaitForSeconds(transformationSeconds);
            transformation = null;
            transforming = false;
            if (!CanCompleteTransformation()) { DestroyTransformationGeometry(); yield break; }
            stageTwoActive = true;
            networkBaseMaximumHealth = damageable.MaximumHealth;
            ActiveSources.Add(this);
            DestroyTransformationGeometry();
            InstallVerifiedAbilityKit();
            RefreshAllNetworks();
        }

        private bool CanCompleteTransformation()
        {
            return IsLiving && corruption != null && corruption.IsCorrupted
                && damageable.CurrentHealth / (float)Mathf.Max(1, damageable.MaximumHealth) <= healthThreshold
                && CountCompatibleAllies() >= requiredCompatibleAllies;
        }

        public void Interrupt()
        {
            if (!transforming) return;
            if (transformation != null) StopCoroutine(transformation);
            transformation = null;
            transforming = false;
            nextEligibilityCheck = Time.time + eligibilityInterval;
            DestroyTransformationGeometry();
        }

        private int CountCompatibleAllies()
        {
            int count = 0;
            float radiusSq = contributionRadius * contributionRadius;
            foreach (EnemyTeamBuffState candidate in EnemyTeamBuffState.ActiveStates)
            {
                if (candidate == null || candidate.gameObject == gameObject || !candidate.CanReceive) continue;
                if (((Vector2)(candidate.transform.position - transform.position)).sqrMagnitude > radiusSq) continue;
                if (ResolveMobType(candidate.gameObject) == mobType) count++;
            }
            return count;
        }

        private void RefreshNetwork()
        {
            if (!IsStageTwoActive) { ClearNetwork(); return; }
            float health = 0f;
            float strength = 0f;
            float radiusSq = contributionRadius * contributionRadius;
            HashSet<StageTwoCollectiveReceiver> found = new HashSet<StageTwoCollectiveReceiver>();
            foreach (EnemyTeamBuffState candidate in EnemyTeamBuffState.ActiveStates)
            {
                if (candidate == null || !candidate.CanReceive) continue;
                if (((Vector2)(candidate.transform.position - transform.position)).sqrMagnitude > radiusSq) continue;
                EnemyArchetypeProfile profile = candidate.GetComponent<EnemyArchetypeProfile>();
                float contribution = ResolveArchetypeContribution(profile);
                health += contribution;
                strength += contribution;
                if (ResolveMobType(candidate.gameObject) == mobType)
                {
                    StageTwoCollectiveReceiver receiver = candidate.GetComponent<StageTwoCollectiveReceiver>();
                    if (receiver == null) receiver = candidate.gameObject.AddComponent<StageTwoCollectiveReceiver>();
                    receiver.SetSource(this, Mathf.Min(maximumSharedResistance, sameTypeResistanceContribution));
                    found.Add(receiver);
                }
            }
            foreach (StageTwoCollectiveReceiver old in recipients)
                if (old != null && !found.Contains(old)) old.RemoveSource(this);
            recipients.Clear(); foreach (StageTwoCollectiveReceiver receiver in found) recipients.Add(receiver);
            ApplyBonuses(Mathf.Min(maximumAdditionalHealth, health), Mathf.Min(maximumAdditionalStrength, strength));
        }

        private float ResolveArchetypeContribution(EnemyArchetypeProfile profile)
        {
            if (profile == null) return 0f;
            float value = 0f;
            if (profile.Includes(EnemyArchetype.Melee)) value += meleeContribution;
            if (profile.Includes(EnemyArchetype.Ranged)) value += rangedContribution;
            if (profile.Includes(EnemyArchetype.Tank)) value += tankContribution;
            if (profile.Includes(EnemyArchetype.Support)) value += supportContribution;
            if (profile.Includes(EnemyArchetype.Swarm)) value += swarmContribution;
            return value;
        }

        private void ApplyBonuses(float healthBonus, float strengthBonus)
        {
            if (networkBaseMaximumHealth <= 0)
            {
                networkBaseMaximumHealth = Mathf.Max(1, Mathf.RoundToInt(damageable.MaximumHealth / (1f + currentHealthBonus)));
            }
            currentHealthBonus = healthBonus; currentStrengthBonus = strengthBonus;
            damageable.SetRuntimeMaximumHealthPreservingRatio(Mathf.Max(1,
                Mathf.RoundToInt(networkBaseMaximumHealth * (1f + healthBonus))));
            damageModifiers.SetCollectiveDamageMultiplier(1f + strengthBonus);
        }

        private void ClearNetwork()
        {
            foreach (StageTwoCollectiveReceiver receiver in recipients) if (receiver != null) receiver.RemoveSource(this);
            recipients.Clear();
            if (damageable != null && networkBaseMaximumHealth > 0 && damageable.CurrentHealth > 0)
                damageable.SetRuntimeMaximumHealthPreservingRatio(networkBaseMaximumHealth);
            currentHealthBonus = 0f; currentStrengthBonus = 0f;
            damageModifiers?.SetCollectiveDamageMultiplier(1f);
        }

        private void InstallVerifiedAbilityKit()
        {
            if (mobType == StageTwoMobType.Skeleton && GetComponent<StageTwoSkeletonPhase>() == null) gameObject.AddComponent<StageTwoSkeletonPhase>();
            else if (mobType == StageTwoMobType.Brute && GetComponent<StageTwoBruteProjection>() == null) gameObject.AddComponent<StageTwoBruteProjection>();
            else if (mobType == StageTwoMobType.Wizard && GetComponent<StageTwoWizardInvocation>() == null) gameObject.AddComponent<StageTwoWizardInvocation>();
        }

        private static StageTwoMobType ResolveMobType(GameObject owner)
        {
            if (owner.GetComponent<SkeletonBrain>() != null) return StageTwoMobType.Skeleton;
            if (owner.GetComponent<BruteBrain>() != null) return StageTwoMobType.Brute;
            if (owner.GetComponent<DetectiveBrain>() != null) return StageTwoMobType.Detective;
            if (owner.GetComponent<WizardBrain>() != null) return StageTwoMobType.Wizard;
            return StageTwoMobType.None;
        }

        private void HandleDeath() { ActiveSources.Remove(this); ClearNetwork(); RefreshAllNetworks(); }
        private void OnDisable() { ActiveSources.Remove(this); Interrupt(); ClearNetwork(); RefreshAllNetworks(); }
        private void OnDestroy() { if (damageable != null) damageable.Died -= HandleDeath; }
        private void DestroyTransformationGeometry() { if (transformationGeometry != null) Destroy(transformationGeometry); transformationGeometry = null; }
        private static void RefreshAllNetworks() { foreach (StageTwoCorruptionState source in ActiveSources) if (source != null) source.nextNetworkRefresh = 0f; }
    }

    [DisallowMultipleComponent]
    public sealed class StageTwoCollectiveReceiver : MonoBehaviour
    {
        private readonly Dictionary<StageTwoCorruptionState, float> sources = new Dictionary<StageTwoCorruptionState, float>();
        public float SharedResistance { get; private set; }
        internal void SetSource(StageTwoCorruptionState source, float resistance) { sources[source] = Mathf.Clamp01(resistance); Recompute(); }
        internal void RemoveSource(StageTwoCorruptionState source) { if (sources.Remove(source)) Recompute(); }
        public int ResolveIncomingDamage(int amount) => Mathf.Max(0, Mathf.CeilToInt(Mathf.Max(0, amount) * (1f - SharedResistance)));
        private void Recompute() { SharedResistance = 0f; foreach (float value in sources.Values) SharedResistance = Mathf.Max(SharedResistance, value); }
        private void OnDisable() { sources.Clear(); SharedResistance = 0f; }
    }
}
