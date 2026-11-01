using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Domain;
using UnityEngine;

namespace Cave.Environmental
{
    public enum StructuralDamageState { Intact, Damaged, Critical, Broken, Regenerating }
    public enum EnvironmentalMaterialFamily { Wood, Stone, Crystal, Metal, FungalTissue, MineralizedBiologicalTissue, PhenomenalBiologicalTissue }
    public enum EnvironmentalCollisionBehavior { Static, Movable, FallingHazard }

    [Serializable]
    public struct PhenomenonStructuralResponse
    {
        public LawPhenomenon phenomenon;
        [Min(0f)] public float integrityMultiplier;
        [Range(-1f, 1f)] public float stabilityChange;
        public bool canTriggerStoredRelease;
    }

    public readonly struct StructuralImpactContext
    {
        public readonly GameObject Source;
        public readonly Vector2 Position;
        public readonly Vector2 Direction;
        public readonly float Magnitude;
        public readonly DamageContext Damage;
        public readonly bool IsProjectile;
        public readonly bool IsArea;
        public readonly int EventId;

        public StructuralImpactContext(GameObject source, Vector2 position, Vector2 direction, float magnitude,
            DamageContext damage, bool projectile, bool area, int eventId)
        {
            Source = source; Position = position; Direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right;
            Magnitude = Mathf.Max(0f, magnitude); Damage = damage; IsProjectile = projectile; IsArea = area; EventId = eventId;
        }
        public bool IsHeavy => Damage.HasTrait(DamageTrait.Heavy) || Damage.HasTrait(DamageTrait.StaggerHeavy);
    }

    public interface IStructuralImpactReceiver { bool ApplyStructuralImpact(StructuralImpactContext impact); }

    /// <summary>Single routing seam for combat, collision, and authored environmental interactions.</summary>
    public static class StructuralImpactRouter
    {
        private static readonly Collider2D[] Results = new Collider2D[64];
        private static readonly HashSet<int> Seen = new HashSet<int>();

        public static int ApplyArea(StructuralImpactContext impact, float radius)
        {
            int count = Physics2D.OverlapCircleNonAlloc(impact.Position, Mathf.Max(0f, radius), Results);
            Seen.Clear(); int applied = 0;
            for (int i = 0; i < count; i++)
            {
                IStructuralImpactReceiver receiver = Results[i] != null ? Results[i].GetComponentInParent<EnvironmentalDestructible>() : null;
                if (receiver == null) continue;
                Component component = receiver as Component;
                int id = component != null ? component.GetInstanceID() : receiver.GetHashCode();
                if (Seen.Add(id) && receiver.ApplyStructuralImpact(impact)) applied++;
            }
            return applied;
        }

        public static bool Apply(Collider2D collider, StructuralImpactContext impact)
        {
            IStructuralImpactReceiver receiver = collider != null ? collider.GetComponentInParent<EnvironmentalDestructible>() : null;
            return receiver != null && receiver.ApplyStructuralImpact(impact);
        }
    }

    /// <summary>Structural integrity is deliberately independent of combat HP.</summary>
    public sealed class EnvironmentalDestructible : MonoBehaviour, IStructuralImpactReceiver
    {
        [SerializeField] private EnvironmentalMaterialProfile material;
        [SerializeField, Min(.01f)] private float maximumIntegrity = 3f;
        [SerializeField, Range(0f, 1f)] private float damagedThreshold = .66f;
        [SerializeField, Range(0f, 1f)] private float criticalThreshold = .33f;
        [SerializeField] private bool progressionCritical;
        [SerializeField] private bool regenerates;
        [SerializeField] private bool vulnerableWhileRegenerating;
        [SerializeField, Min(0f)] private float regenerationDelay = 12f;
        [SerializeField, Min(.01f)] private float regenerationSeconds = 3f;
        [SerializeField] private Collider2D[] structuralColliders;
        [SerializeField] private GameObject[] intactVisuals;
        [SerializeField] private GameObject[] damagedVisuals;
        [SerializeField] private GameObject[] brokenVisuals;
        [SerializeField] private GameObject[] regeneratingVisuals;

        private float integrity;
        private float regenerateAt = -1f;
        private float regenerationStartedAt = -1f;
        private int lastEventId = int.MinValue;
        private bool runtimeInitialized;
        public StructuralDamageState State { get; private set; }
        public float Integrity => integrity;
        public event Action<EnvironmentalDestructible, StructuralDamageState> StateChanged;

        private void Awake() { InitializeRuntimeState(); }

        public void InitializeRuntimeState()
        {
            if (runtimeInitialized) return;
            runtimeInitialized = true;
            State = StructuralDamageState.Broken; // forces initial visual/collider authoring application.
            RestoreIntact();
        }
        private void Update()
        {
            if (State == StructuralDamageState.Broken && regenerates && Time.time >= regenerateAt && CanRestoreCollider())
            {
                SetState(StructuralDamageState.Regenerating);
                regenerationStartedAt = Time.time;
            }
            else if (State == StructuralDamageState.Regenerating)
            {
                float progress = Mathf.Clamp01((Time.time - regenerationStartedAt) / regenerationSeconds);
                ApplyRegenerationStage(progress);
                if (progress >= 1f && CanRestoreCollider()) RestoreIntact();
            }
        }

        public bool ApplyStructuralImpact(StructuralImpactContext impact)
        {
            InitializeRuntimeState();
            if (progressionCritical || State == StructuralDamageState.Broken
                || (State == StructuralDamageState.Regenerating && !vulnerableWhileRegenerating)) return false;
            if (impact.EventId != 0 && impact.EventId == lastEventId) return false;
            lastEventId = impact.EventId;
            float resolved = material != null ? material.ResolveImpact(impact.Magnitude, impact.IsHeavy) : impact.Magnitude;
            EnvironmentalReinforcement reinforcement = GetComponent<EnvironmentalReinforcement>();
            if (reinforcement != null)
            {
                resolved *= reinforcement.IncomingImpactMultiplier;
            }
            if (resolved <= 0f) return false;
            integrity = Mathf.Max(0f, integrity - resolved);
            TransitionForIntegrity();
            return true;
        }

        public bool ApplyPhenomenon(LawPhenomenon phenomenon, float magnitude, GameObject source, int eventId)
        {
            PhenomenonStructuralResponse response = material != null ? material.ResponseFor(phenomenon)
                : new PhenomenonStructuralResponse { phenomenon = phenomenon, integrityMultiplier = 1f };
            return ApplyStructuralImpact(new StructuralImpactContext(source, transform.position, Vector2.up,
                magnitude * Mathf.Max(0f, response.integrityMultiplier + response.stabilityChange),
                default, false, true, eventId));
        }

        public void RestoreIntact()
        {
            integrity = maximumIntegrity; regenerateAt = -1f; regenerationStartedAt = -1f;
            SetState(StructuralDamageState.Intact);
        }

        /// <summary>
        /// Bounded material restoration for authorised environmental caretakers.
        /// Permanently broken objects remain terminal: this never creates or
        /// resurrects a destroyed structure.
        /// </summary>
        public bool TryRestoreIntegrity(float amount)
        {
            InitializeRuntimeState();
            if (amount <= 0f || State == StructuralDamageState.Broken)
            {
                return false;
            }

            float before = integrity;
            integrity = Mathf.Min(maximumIntegrity, integrity + amount);
            if (integrity <= before + .0001f)
            {
                return false;
            }

            TransitionForIntegrity();
            return true;
        }

        private void TransitionForIntegrity()
        {
            float ratio = maximumIntegrity <= 0f ? 0f : integrity / maximumIntegrity;
            if (integrity <= 0f) { SetState(StructuralDamageState.Broken); regenerateAt = regenerates ? Time.time + regenerationDelay : -1f; return; }
            SetState(ratio <= criticalThreshold ? StructuralDamageState.Critical : ratio <= damagedThreshold ? StructuralDamageState.Damaged : StructuralDamageState.Intact);
        }

        private void SetState(StructuralDamageState next) { if (State == next) return; State = next; ApplyVisuals(); StateChanged?.Invoke(this, next); }
        private void ApplyVisuals()
        {
            SetActive(intactVisuals, State == StructuralDamageState.Intact);
            SetActive(damagedVisuals, State == StructuralDamageState.Damaged || State == StructuralDamageState.Critical);
            SetActive(brokenVisuals, State == StructuralDamageState.Broken);
            SetActive(regeneratingVisuals, false);
            if (structuralColliders != null) for (int i = 0; i < structuralColliders.Length; i++) if (structuralColliders[i] != null) structuralColliders[i].enabled = State != StructuralDamageState.Broken && State != StructuralDamageState.Regenerating;
        }
        private void ApplyRegenerationStage(float progress)
        {
            if (regeneratingVisuals == null || regeneratingVisuals.Length == 0) return;
            int activeIndex = Mathf.Min(regeneratingVisuals.Length - 1,
                Mathf.FloorToInt(Mathf.Clamp01(progress) * regeneratingVisuals.Length));
            for (int i = 0; i < regeneratingVisuals.Length; i++)
                if (regeneratingVisuals[i] != null) regeneratingVisuals[i].SetActive(i == activeIndex);
        }
        private bool CanRestoreCollider()
        {
            if (structuralColliders == null) return true;
            for (int i = 0; i < structuralColliders.Length; i++)
            {
                Collider2D c = structuralColliders[i]; if (c == null) continue;
                Collider2D[] occupants = Physics2D.OverlapBoxAll(c.bounds.center, c.bounds.size * .92f, 0f);
                for (int j = 0; j < occupants.Length; j++) if (occupants[j] != null && occupants[j] != c && occupants[j].GetComponentInParent<EnvironmentalDestructible>() == null) return false;
            }
            return true;
        }
        private static void SetActive(GameObject[] objects, bool value) { if (objects != null) for (int i = 0; i < objects.Length; i++) if (objects[i] != null) objects[i].SetActive(value); }
    }

    /// <summary>Lets carts and knocked bodies turn bounded Rigidbody2D collisions into structural impacts.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnvironmentalImpactBody : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 4f;
        [SerializeField, Min(0f)] private float impactMultiplier = .2f;
        private Rigidbody2D body;
        private void Awake() { body = GetComponent<Rigidbody2D>(); }
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || body == null || body.velocity.magnitude < minimumImpactSpeed) return;
            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            StructuralImpactRouter.Apply(collision.collider, new StructuralImpactContext(gameObject, point, body.velocity, body.velocity.magnitude * impactMultiplier, default, false, false, GetInstanceID() ^ Time.frameCount));
        }
    }
}
