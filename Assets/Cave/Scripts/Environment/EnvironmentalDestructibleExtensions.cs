using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Domain;
using Cave.Pickups;
using UnityEngine;

namespace Cave.Environmental
{
    /// <summary>Authored relationship: a structure fails only after the configured supports fail.</summary>
    public sealed class StructuralSupportGroup : MonoBehaviour
    {
        [SerializeField] private EnvironmentalDestructible[] requiredSupports;
        [SerializeField] private GameObject[] intactObjects;
        [SerializeField] private GameObject[] collapsedObjects;
        [SerializeField] private Collider2D[] collidersToDisableOnCollapse;

        public bool IsCollapsed { get; private set; }
        public event Action<StructuralSupportGroup> Collapsed;

        private void OnEnable()
        {
            if (requiredSupports != null)
                for (int i = 0; i < requiredSupports.Length; i++)
                    if (requiredSupports[i] != null) requiredSupports[i].StateChanged += HandleSupportStateChanged;
            EvaluateSupports();
        }

        private void OnDisable()
        {
            if (requiredSupports != null)
                for (int i = 0; i < requiredSupports.Length; i++)
                    if (requiredSupports[i] != null) requiredSupports[i].StateChanged -= HandleSupportStateChanged;
        }

        private void HandleSupportStateChanged(EnvironmentalDestructible _, StructuralDamageState __) => EvaluateSupports();

        public void EvaluateSupports()
        {
            if (IsCollapsed || requiredSupports == null || requiredSupports.Length == 0) return;
            for (int i = 0; i < requiredSupports.Length; i++)
                if (requiredSupports[i] == null || requiredSupports[i].State != StructuralDamageState.Broken) return;

            IsCollapsed = true;
            SetActive(intactObjects, false);
            SetActive(collapsedObjects, true);
            if (collidersToDisableOnCollapse != null)
                for (int i = 0; i < collidersToDisableOnCollapse.Length; i++)
                    if (collidersToDisableOnCollapse[i] != null) collidersToDisableOnCollapse[i].enabled = false;
            Collapsed?.Invoke(this);
        }

        private static void SetActive(GameObject[] objects, bool value)
        {
            if (objects == null) return;
            for (int i = 0; i < objects.Length; i++) if (objects[i] != null) objects[i].SetActive(value);
        }
    }

    /// <summary>Single-hit falling consequence.  The object must be armed by an authored attachment failure.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnvironmentalFallingHazard : MonoBehaviour
    {
        [SerializeField] private EnvironmentalDestructible attachment;
        [SerializeField] private Rigidbody2D fallingBody;
        [SerializeField, Min(1)] private int damage = 2;
        [SerializeField] private DamageTrait damageTraits = DamageTrait.AreaOfEffect | DamageTrait.Heavy;
        [SerializeField, Min(0f)] private float minimumFallSpeed = 2f;
        private readonly HashSet<int> hitTargets = new HashSet<int>();
        private bool armed;
        private Vector3 anchoredPosition;
        private Quaternion anchoredRotation;

        private void Awake()
        {
            if (fallingBody == null) fallingBody = GetComponent<Rigidbody2D>();
            anchoredPosition = transform.position;
            anchoredRotation = transform.rotation;
            if (fallingBody != null) { fallingBody.simulated = false; fallingBody.velocity = Vector2.zero; }
        }

        private void OnEnable()
        {
            if (attachment != null) attachment.StateChanged += OnAttachmentStateChanged;
        }

        private void OnDisable()
        {
            if (attachment != null) attachment.StateChanged -= OnAttachmentStateChanged;
        }

        private void OnAttachmentStateChanged(EnvironmentalDestructible _, StructuralDamageState state)
        {
            if (state == StructuralDamageState.Broken && !armed)
            {
                armed = true;
                hitTargets.Clear();
                if (fallingBody != null) fallingBody.simulated = true;
            }
            else if (state == StructuralDamageState.Regenerating || state == StructuralDamageState.Intact)
            {
                armed = false;
                transform.SetPositionAndRotation(anchoredPosition, anchoredRotation);
                if (fallingBody != null) { fallingBody.velocity = Vector2.zero; fallingBody.angularVelocity = 0f; fallingBody.simulated = false; }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!armed || fallingBody == null || fallingBody.velocity.magnitude < minimumFallSpeed) return;
            Damageable target = collision.collider != null ? collision.collider.GetComponentInParent<Damageable>() : null;
            if (target != null && hitTargets.Add(target.GetInstanceID()))
                target.TakeDamageResolved(damage, new DamageContext(gameObject, damageTraits));
            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            StructuralImpactRouter.Apply(collision.collider, new StructuralImpactContext(
                gameObject, point, fallingBody.velocity, fallingBody.velocity.magnitude,
                new DamageContext(gameObject, damageTraits), false, false, GetInstanceID()));
            armed = false; // one physical consequence per authored falling instance.
            fallingBody.velocity = Vector2.zero;
            fallingBody.simulated = false;
        }
    }

    /// <summary>Stores a bounded, observable phenomenon charge.  Discharge is an event so existing semantic owners decide its meaning.</summary>
    [RequireComponent(typeof(EnvironmentalDestructible))]
    public sealed class CrystalPhenomenonStorage : MonoBehaviour
    {
        [SerializeField] private LawPhenomenon primaryAffinity = LawPhenomenon.Potential;
        [SerializeField] private LawPhenomenon secondaryAffinity = LawPhenomenon.Resonance;
        [SerializeField, Min(0f)] private float primaryMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float secondaryMultiplier = 1.15f;
        [SerializeField, Min(0f)] private float maximumStoredMagnitude = 6f;
        [SerializeField] private bool releasesOnShatter = true;
        private readonly Dictionary<LawPhenomenon, float> stored = new Dictionary<LawPhenomenon, float>();
        private EnvironmentalDestructible structural;
        private bool discharged;
        private bool subscribed;

        public event Action<CrystalPhenomenonStorage, LawPhenomenon, float> Discharged;

        private void Awake() { EnsureSubscription(); }
        private void OnEnable() { EnsureSubscription(); }
        private void OnDisable()
        {
            if (structural != null && subscribed) structural.StateChanged -= HandleState;
            subscribed = false;
        }

        public bool ApplyPhenomenon(LawPhenomenon phenomenon, float magnitude, GameObject source, int eventId)
        {
            EnsureSubscription();
            if (structural == null || magnitude <= 0f || structural.State == StructuralDamageState.Broken) return false;
            float multiplier = phenomenon == primaryAffinity ? primaryMultiplier : phenomenon == secondaryAffinity ? secondaryMultiplier : 1f;
            float current;
            stored.TryGetValue(phenomenon, out current);
            stored[phenomenon] = Mathf.Min(maximumStoredMagnitude, current + magnitude * multiplier);
            return structural.ApplyPhenomenon(phenomenon, magnitude * multiplier, source, eventId);
        }

        private void EnsureSubscription()
        {
            if (structural == null) structural = GetComponent<EnvironmentalDestructible>();
            if (structural == null || subscribed) return;
            structural.StateChanged += HandleState;
            subscribed = true;
        }

        private void HandleState(EnvironmentalDestructible _, StructuralDamageState state)
        {
            if (state != StructuralDamageState.Broken) { if (state == StructuralDamageState.Intact) discharged = false; return; }
            if (!releasesOnShatter || discharged) return;
            discharged = true;
            foreach (KeyValuePair<LawPhenomenon, float> entry in stored)
                if (entry.Value > 0f) Discharged?.Invoke(this, entry.Key, entry.Value);
            stored.Clear();
        }
    }

    /// <summary>Environmental reward owner. It shares the mob primary-roll rule and is one-shot for a live encounter.</summary>
    public sealed class EnvironmentalDropper : MonoBehaviour
    {
        [SerializeField] private EnvironmentalDestructible structural;
        [SerializeField] private DropEntry[] entries;
        [SerializeField] private Vector2 spawnOffset = new Vector2(0f, .25f);
        [SerializeField] private bool rewardOncePerEncounter = true;
        private bool rewarded;

        private void Awake() { if (structural == null) structural = GetComponent<EnvironmentalDestructible>(); }
        private void OnEnable() { if (structural != null) structural.StateChanged += HandleState; }
        private void OnDisable() { if (structural != null) structural.StateChanged -= HandleState; }
        private void HandleState(EnvironmentalDestructible _, StructuralDamageState state)
        {
            if (state != StructuralDamageState.Broken || (rewardOncePerEncounter && rewarded) || entries == null) return;
            List<DropEntry> eligible = EnemyDropper.BuildEligibleEntriesByMobRules(entries);
            DropEntry entry = EnemyDropper.ChoosePrimaryByMobRules(eligible,
                EnemyDropper.ResolveOrdinaryPrimaryChanceMultiplier(transform.position));
            if (entry == null || entry.PickupPrefab == null) return;
            rewarded = true;
            Instantiate(entry.PickupPrefab, (Vector2)transform.position + spawnOffset, Quaternion.identity);
        }
    }

    public enum CaveFloraSpecies
    {
        Glowcaps, EmberShelves, VeilMycelium, ShardbranchCoral, BastionCoral,
        WhisperFans, Fluxblooms, PhaseTendrils, SovereignBloom
    }

    public enum FloraAbundance { Excluded, Rare, Low, Medium, High }
    public enum CaveMapRegion { Breach, Settlement, Citadel, Sanctuary, Ruins }

    /// <summary>Approved relative-authoring matrix. It does not spawn or infer scene layouts.</summary>
    public static class FloraDistributionMatrix
    {
        private static readonly FloraAbundance[,] Values =
        {
            { FloraAbundance.High,   FloraAbundance.Medium, FloraAbundance.Low,    FloraAbundance.High,   FloraAbundance.Low },
            { FloraAbundance.Medium, FloraAbundance.Low,    FloraAbundance.High,   FloraAbundance.Excluded, FloraAbundance.Medium },
            { FloraAbundance.High,   FloraAbundance.Medium, FloraAbundance.Low,    FloraAbundance.Low,    FloraAbundance.Excluded },
            { FloraAbundance.Medium, FloraAbundance.Low,    FloraAbundance.Medium, FloraAbundance.Medium, FloraAbundance.High },
            { FloraAbundance.Low,    FloraAbundance.Excluded, FloraAbundance.High, FloraAbundance.Medium, FloraAbundance.High },
            { FloraAbundance.Medium, FloraAbundance.Excluded, FloraAbundance.High, FloraAbundance.High,  FloraAbundance.Medium },
            { FloraAbundance.High,   FloraAbundance.Low,    FloraAbundance.Medium, FloraAbundance.High,   FloraAbundance.High },
            { FloraAbundance.Medium, FloraAbundance.Excluded, FloraAbundance.Low,  FloraAbundance.High,   FloraAbundance.Medium },
            { FloraAbundance.Rare,   FloraAbundance.Excluded, FloraAbundance.Rare, FloraAbundance.Rare,   FloraAbundance.Rare }
        };

        public static FloraAbundance Get(CaveFloraSpecies species, CaveMapRegion region)
        {
            return Values[(int)species, (int)region];
        }
    }

    /// <summary>Lightweight organism composition.  Core failure is terminal; tissue may regenerate independently.</summary>
    public sealed class EnvironmentalFlora : MonoBehaviour
    {
        [SerializeField] private CaveFloraSpecies species;
        [SerializeField] private EnvironmentalDestructible biologicalCore;
        [SerializeField] private EnvironmentalDestructible[] externalTissue;
        [SerializeField, Min(0)] private int propagationLimit = 0;
        [SerializeField, Min(0f)] private float propagationRadius = 0f;
        private bool permanentlyDead;
        private int propagatedChildren;
        private bool coreSubscribed;
        public bool PermanentlyDead => permanentlyDead;
        public CaveFloraSpecies Species => species;
        public int PropagationLimit => propagationLimit;
        public float PropagationRadius => propagationRadius;
        public event Action<EnvironmentalFlora, Vector2> PropagationRequested;

        private void OnEnable() { EnsureCoreSubscription(); }
        private void OnDisable()
        {
            if (biologicalCore != null && coreSubscribed) biologicalCore.StateChanged -= HandleCoreState;
            coreSubscribed = false;
        }
        private void HandleCoreState(EnvironmentalDestructible _, StructuralDamageState state)
        {
            if (state != StructuralDamageState.Broken || permanentlyDead) return;
            permanentlyDead = true;
            if (externalTissue != null)
                for (int i = 0; i < externalTissue.Length; i++)
                    if (externalTissue[i] != null) externalTissue[i].gameObject.SetActive(false);
        }

        /// <summary>Bounded request seam; a colony/scene author owns actual placement and pooling.</summary>
        public bool TryRequestPropagation(Vector2 candidatePosition)
        {
            EnsureCoreSubscription();
            if (permanentlyDead || biologicalCore == null || biologicalCore.State == StructuralDamageState.Broken
                || propagationLimit <= 0 || propagatedChildren >= propagationLimit
                || Vector2.Distance(transform.position, candidatePosition) > propagationRadius)
                return false;
            propagatedChildren++;
            PropagationRequested?.Invoke(this, candidatePosition);
            return true;
        }

        private void EnsureCoreSubscription()
        {
            if (biologicalCore == null || coreSubscribed) return;
            biologicalCore.StateChanged += HandleCoreState;
            coreSubscribed = true;
        }
    }
}
