using System;
using Cave.Axioms.Mastery;
using Cave.Player;
using UnityEngine;

namespace Cave.Domain
{
    public enum DomainManifestationState
    {
        Dormant = 0,
        Ready = 1,
        Manifesting = 2,
        Active = 3,
        Collapsing = 4,
        Cooldown = 5
    }

    /// <summary>
    /// Player-owned, mobile Domain manifestation authority. It snapshots the
    /// authored composition for observability but deliberately does not execute
    /// Laws: execution remains subject to the existing target/context seams.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDomainManifestation : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] private float activationDuration = .5f;
        [SerializeField, Min(0f)] private float cooldownDuration = 3f;
        [SerializeField, Min(.01f)] private float minimumActivationCharge = 30f;
        [SerializeField, Min(0f)] private float chargeDrainPerSecond = 5f;

        [Header("Radius from Domain mastery")]
        [SerializeField, Min(.1f)] private float minimumRadius = 2.4f;
        [SerializeField, Min(0f)] private float radiusPerMasteredPhenomenon = .35f;
        [SerializeField, Min(.1f)] private float maximumRadius = 5.2f;

        private PlayerDomainReserve reserve;
        private PlayerDomainLawCollection laws;
        private PlayerMasteryEvidenceRuntime evidence;
        private PlayerHealth health;
        private DomainManifestationVisual visual;
        private float stateEndsAt;
        private DomainComposition snapshot = DomainComposition.Empty;

        public event Action<DomainManifestationState> StateChanged;
        public event Action<string> FeedbackRequested;
        public DomainManifestationState State { get; private set; } = DomainManifestationState.Dormant;
        public DomainComposition SnapshotComposition => snapshot;
        public bool IsActive => State == DomainManifestationState.Manifesting || State == DomainManifestationState.Active;
        public float Radius => CalculateRadius(evidence != null ? evidence.Snapshot : PlayerMasteryEvidenceState.Empty);
        public float MinimumActivationCharge => minimumActivationCharge;
        public float DrainPerSecond => chargeDrainPerSecond;

        public static PlayerDomainManifestation EnsureOn(GameObject owner)
        {
            return owner == null ? null : owner.GetComponent<PlayerDomainManifestation>()
                ?? owner.AddComponent<PlayerDomainManifestation>();
        }

        private void Awake()
        {
            ResolveOwners();
            EnsureVisual();
            RefreshReadiness();
        }

        private void OnEnable()
        {
            ResolveOwners();
            if (health != null) health.Died += HandlePlayerDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= HandlePlayerDied;
        }

        private void Update()
        {
            ResolveOwners();
            RefreshReadiness();
            if (State == DomainManifestationState.Manifesting && Time.time >= stateEndsAt)
            {
                SetState(DomainManifestationState.Active);
            }
            else if (State == DomainManifestationState.Active)
            {
                float requestedDrain = chargeDrainPerSecond * Time.deltaTime;
                float payableDrain = Mathf.Min(requestedDrain, reserve != null ? reserve.CurrentCharge : 0f);
                if (payableDrain <= 0f || reserve == null || !reserve.TrySpendCharge(payableDrain))
                {
                    BeginCollapse();
                }
            }
            else if (State == DomainManifestationState.Collapsing && Time.time >= stateEndsAt)
            {
                SetState(DomainManifestationState.Cooldown);
                stateEndsAt = Time.time + cooldownDuration;
            }
            else if (State == DomainManifestationState.Cooldown && Time.time >= stateEndsAt)
            {
                SetState(DomainManifestationState.Dormant);
                RefreshReadiness();
            }

            if (visual != null) visual.Present(IsActive, Radius);
        }

        public bool TryRequestActivation()
        {
            ResolveOwners();
            if (State != DomainManifestationState.Ready)
            {
                FeedbackRequested?.Invoke(State == DomainManifestationState.Cooldown
                    ? "Domain manifestation is cooling down."
                    : "Domain manifestation is unavailable.");
                return false;
            }

            if (!DomainTestOverride.HasDomainAccess)
            {
                FeedbackRequested?.Invoke("A Domain Seed is required.");
                return false;
            }

            if (health != null && health.CurrentHealth <= 0)
            {
                FeedbackRequested?.Invoke("Cannot manifest while defeated.");
                return false;
            }

            if (reserve == null || reserve.CurrentCharge + .0001f < minimumActivationCharge)
            {
                FeedbackRequested?.Invoke("Need " + minimumActivationCharge.ToString("0.##") + " Domain Charge.");
                return false;
            }

            PlayerBrace brace = GetComponent<PlayerBrace>();
            PlayerGuardBreak guardBreak = GetComponent<PlayerGuardBreak>();
            if ((brace != null && brace.IsActionLocked)
                || (guardBreak != null && guardBreak.IsGuardBreaking))
            {
                FeedbackRequested?.Invoke("Cannot manifest during the current action.");
                return false;
            }

            snapshot = laws != null ? laws.Composition : DomainComposition.Empty;
            SetState(DomainManifestationState.Manifesting);
            stateEndsAt = Time.time + activationDuration;
            return true;
        }

        public void Dismiss()
        {
            if (IsActive) BeginCollapse();
        }

        public float CalculateRadius(PlayerMasteryEvidenceState currentEvidence)
        {
            PlayerMasteryEvidenceState source = currentEvidence ?? PlayerMasteryEvidenceState.Empty;
            int mastered = 0;
            Array values = Enum.GetValues(typeof(LawPhenomenon));
            for (int index = 0; index < values.Length; index++)
            {
                LawPhenomenon phenomenon = (LawPhenomenon)values.GetValue(index);
                if (source.GetPhenomenonReport(phenomenon, PlayerMasteryPolicy.Default).IsMastered) mastered++;
            }

            return Mathf.Clamp(minimumRadius + mastered * radiusPerMasteredPhenomenon,
                minimumRadius, Mathf.Max(minimumRadius, maximumRadius));
        }

        private void BeginCollapse()
        {
            if (State == DomainManifestationState.Collapsing || State == DomainManifestationState.Cooldown) return;
            SetState(DomainManifestationState.Collapsing);
            stateEndsAt = Time.time + .12f;
        }

        private void HandlePlayerDied()
        {
            reserve?.ResetExpeditionCharge();
            snapshot = DomainComposition.Empty;
            SetState(DomainManifestationState.Cooldown);
            stateEndsAt = Time.time + cooldownDuration;
        }

        private void SetState(DomainManifestationState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(State);
            if (visual != null) visual.Present(IsActive, Radius);
        }

        private void RefreshReadiness()
        {
            if (State != DomainManifestationState.Dormant && State != DomainManifestationState.Ready) return;
            bool canActivate = reserve != null && reserve.CurrentCharge + .0001f >= minimumActivationCharge;
            SetState(canActivate ? DomainManifestationState.Ready : DomainManifestationState.Dormant);
        }

        private void ResolveOwners()
        {
            if (reserve == null) reserve = PlayerDomainReserve.EnsureOn(gameObject);
            if (laws == null) laws = PlayerDomainLawCollection.EnsureOn(gameObject);
            if (evidence == null) evidence = PlayerMasteryEvidenceRuntime.EnsureOn(gameObject);
            if (health == null) health = GetComponent<PlayerHealth>();
        }

        private void EnsureVisual()
        {
            if (visual != null) return;
            Transform child = transform.Find("Domain Manifestation Visual");
            if (child == null)
            {
                GameObject visualObject = new GameObject("Domain Manifestation Visual");
                visualObject.transform.SetParent(transform, false);
                child = visualObject.transform;
            }

            visual = child.GetComponent<DomainManifestationVisual>();
            if (visual == null) visual = child.gameObject.AddComponent<DomainManifestationVisual>();
            visual.Present(false, Radius);
        }
    }

    /// <summary>Visual-only concentric gold/cobalt manifestation cue with no collider or gameplay authority.</summary>
    [DisallowMultipleComponent]
    internal sealed class DomainManifestationVisual : MonoBehaviour
    {
        private const int Segments = 40;
        private LineRenderer cobalt;
        private LineRenderer gold;

        private void Awake() { EnsureLines(); }

        public void Present(bool visible, float radius)
        {
            EnsureLines();
            cobalt.enabled = visible;
            gold.enabled = visible;
            if (!visible) return;

            SetRing(cobalt, radius, 0f);
            SetRing(gold, radius * .82f, Mathf.PI * .25f);
        }

        private void EnsureLines()
        {
            if (cobalt != null && gold != null) return;
            cobalt = CreateLine("Cobalt Ring", new Color(.13f, .48f, .9f, .35f), .035f);
            gold = CreateLine("Gold Ring", new Color(1f, .73f, .23f, .5f), .025f);
        }

        private LineRenderer CreateLine(string name, Color color, float width)
        {
            GameObject ring = new GameObject(name);
            ring.transform.SetParent(transform, false);
            LineRenderer line = ring.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) line.material = new Material(shader);
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 20;
            return line;
        }

        private static void SetRing(LineRenderer line, float radius, float phase)
        {
            for (int index = 0; index < Segments; index++)
            {
                float angle = phase + (Mathf.PI * 2f * index / Segments);
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }
    }
}
