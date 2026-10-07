using System.Collections.Generic;
using Cave.Axioms;
using Cave.Axioms.Control;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Narrow event adapter from already-resolved combat, defense, and Axiom
    /// control outcomes into the one player-owned Domain Charge ledger. It
    /// never subscribes to the ledger, so it cannot create a reward self-loop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDomainChargeRewards : MonoBehaviour
    {
        [Header("Initial Charge reward profile")]
        [SerializeField, Min(0f)] private float ordinaryHitCharge = 1f;
        [SerializeField, Min(0f)] private float heavyHitCharge = 3f;
        [SerializeField, Min(0f)] private float killCharge = 4f;
        [SerializeField, Min(0f)] private float parryCharge = 5f;
        [SerializeField, Min(0f)] private float perfectParryCharge = 8f;
        [SerializeField, Min(0f)] private float guardBreakCharge = 5f;
        [SerializeField, Min(0f)] private float controlCharge = 4f;
        [SerializeField, Min(0f)] private float controlledEquilibriumCharge = 8f;
        [Header("Bounded anti-farming")]
        [SerializeField, Min(1)] private int fullRewardEventsPerTargetAction = 5;
        [SerializeField, Min(.01f)] private float repeatWindowSeconds = 12f;
        [SerializeField, Range(.01f, 1f)] private float minimumRepeatMultiplier = .25f;
        [SerializeField, Min(.01f)] private float controlToEquilibriumWindow = 5f;

        private PlayerDomainReserve reserve;
        private PlayerDomainManifestation manifestation;
        private SpinSwordAttack ordinaryAttack;
        private ChargedAttack heavyAttack;
        private SidewaysParryAttack parry;
        private PlayerGuardBreak guardBreak;
        private AxiomControlState control;
        private readonly Dictionary<AxiomKind, bool> equilibrium = new Dictionary<AxiomKind, bool>();
        private readonly Dictionary<AxiomKind, float> lastSuccessfulControl = new Dictionary<AxiomKind, float>();
        private readonly Dictionary<string, RepeatRecord> repeats = new Dictionary<string, RepeatRecord>();

        private struct RepeatRecord { public int Count; public float StartedAt; }

        public static PlayerDomainChargeRewards EnsureOn(GameObject owner)
        {
            return owner == null ? null : owner.GetComponent<PlayerDomainChargeRewards>()
                ?? owner.AddComponent<PlayerDomainChargeRewards>();
        }

        private void Awake()
        {
            Resolve();
            Subscribe();
        }

        private void OnDestroy() { Unsubscribe(); }

        private void Update()
        {
            Resolve();
            if (control == null) return;
            foreach (AxiomKind kind in System.Enum.GetValues(typeof(AxiomKind)))
            {
                bool isControlled = control.IsControlledEquilibrium(kind);
                bool wasControlled;
                equilibrium.TryGetValue(kind, out wasControlled);
                float lastControl;
                bool hasRecentPlayerControl = lastSuccessfulControl.TryGetValue(kind, out lastControl)
                    && Time.time - lastControl <= controlToEquilibriumWindow;
                if (isControlled && !wasControlled && hasRecentPlayerControl)
                {
                    Award("axiom-equilibrium:" + (int)kind + ":" + Time.frameCount,
                        controlledEquilibriumCharge, null, "equilibrium");
                }
                equilibrium[kind] = isControlled;
            }
        }

        private void Resolve()
        {
            if (reserve == null) reserve = PlayerDomainReserve.EnsureOn(gameObject);
            if (manifestation == null) manifestation = GetComponent<PlayerDomainManifestation>();
            if (ordinaryAttack == null) ordinaryAttack = GetComponent<SpinSwordAttack>();
            if (heavyAttack == null) heavyAttack = GetComponent<ChargedAttack>();
            if (parry == null) parry = GetComponent<SidewaysParryAttack>();
            if (guardBreak == null) guardBreak = GetComponent<PlayerGuardBreak>();
            if (control == null) control = GetComponent<AxiomControlState>();
        }

        private void Subscribe()
        {
            if (ordinaryAttack != null) ordinaryAttack.OrdinaryTargetHit += HandleOrdinaryHit;
            if (heavyAttack != null) heavyAttack.HeavyTargetResolved += HandleHeavyHit;
            if (parry != null) parry.DefenseSucceeded += HandleDefense;
            if (guardBreak != null) guardBreak.OffensiveGuardBreakTargetSucceeded += HandleGuardBreak;
            if (control != null) control.InterventionResolved += HandleControlResolved;
        }

        private void Unsubscribe()
        {
            if (ordinaryAttack != null) ordinaryAttack.OrdinaryTargetHit -= HandleOrdinaryHit;
            if (heavyAttack != null) heavyAttack.HeavyTargetResolved -= HandleHeavyHit;
            if (parry != null) parry.DefenseSucceeded -= HandleDefense;
            if (guardBreak != null) guardBreak.OffensiveGuardBreakTargetSucceeded -= HandleGuardBreak;
            if (control != null) control.InterventionResolved -= HandleControlResolved;
        }

        private void HandleOrdinaryHit(Damageable target, bool killed, bool wasHostile)
        {
            if (!wasHostile) return;
            string id = "ordinary:" + Time.frameCount + ":" + (target != null ? target.GetInstanceID().ToString() : "none");
            Award(id, ordinaryHitCharge + (killed ? killCharge : 0f), target, "ordinary");
        }

        private void HandleHeavyHit(Damageable target, bool killed, bool wasHostile)
        {
            if (!wasHostile) return;
            string id = "heavy:" + Time.frameCount + ":" + (target != null ? target.GetInstanceID().ToString() : "none");
            Award(id, heavyHitCharge + (killed ? killCharge : 0f), target, "heavy");
        }

        private void HandleDefense(PlayerDefenseQuality quality)
        {
            Award("defense:" + Time.frameCount + ":" + (int)quality,
                quality == PlayerDefenseQuality.PerfectParry ? perfectParryCharge : parryCharge,
                null, "defense");
        }

        private void HandleGuardBreak(Damageable target)
        {
            Award("guard-break:" + Time.frameCount + ":" + (target != null ? target.GetInstanceID().ToString() : "none"), guardBreakCharge, target, "guard-break");
        }

        private void HandleControlResolved(AxiomControlInterventionOutcome outcome)
        {
            if (!outcome.Succeeded) return;
            lastSuccessfulControl[outcome.Kind] = outcome.Timestamp;
            Award("axiom-control:" + outcome.Sequence, controlCharge, null, "axiom-control");
        }

        private void Award(string eventId, float amount, Damageable target, string action)
        {
            // Central manifested effects cannot extend the same manifestation.
            if (manifestation != null && manifestation.IsActive) return;
            reserve?.TryAwardCharge(eventId, amount * RepeatMultiplier(target, action));
        }

        private float RepeatMultiplier(Damageable target, string action)
        {
            if (target == null || string.IsNullOrEmpty(action)) return 1f;
            string key = target.GetInstanceID() + ":" + action;
            RepeatRecord record;
            if (!repeats.TryGetValue(key, out record) || Time.time - record.StartedAt > repeatWindowSeconds)
            {
                record = new RepeatRecord { Count = 0, StartedAt = Time.time };
            }

            record.Count++;
            repeats[key] = record;
            if (record.Count <= fullRewardEventsPerTargetAction) return 1f;
            int diminishedEvents = record.Count - fullRewardEventsPerTargetAction;
            return Mathf.Max(minimumRepeatMultiplier, 1f / (1f + diminishedEvents));
        }
    }
}
