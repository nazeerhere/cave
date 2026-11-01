using System;
using Cave.Axioms.Frequency;
using Cave.Inventory;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    public enum ResonantCarapaceState { Unequipped, Active, Damaged, Depleted, ResonanceDisabled, TerminallyFractured }
    public enum ResonantCarapaceIntegrityStage { Depleted, Critical, Compromised, Cracked, Sealed }

    /// <summary>Pure integer policy retained separately so the runtime owner never loses damage.</summary>
    public static class ResonantCarapacePolicy
    {
        public static int ResolveNormalAbsorption(int incomingDamage, int availableDurability, float shareFraction)
        {
            int incoming = Mathf.Max(0, incomingDamage);
            int intended = Mathf.FloorToInt(incoming * Mathf.Clamp01(shareFraction));
            return Mathf.Min(Mathf.Max(0, availableDurability), intended);
        }

        public static float ResolveDeepBraceDiversion(float existingManaRecovery)
        {
            return Mathf.Max(0f, existingManaRecovery) * .5f;
        }

        public static float ResolvePhysicalControlMultiplier(float resistance)
        {
            return 1f - Mathf.Clamp01(resistance);
        }

        public static float ResolveStagedPhysicalResistance(float baseResistance, float stageEffectiveness)
        {
            return Mathf.Clamp01(baseResistance) * Mathf.Clamp01(stageEffectiveness);
        }

        public static float ResolveRestorationManaCost(float maximumMana, float costFraction)
        {
            return Mathf.Max(0f, maximumMana) * Mathf.Clamp01(costFraction);
        }

        public static ResonantCarapaceIntegrityStage ResolveIntegrityStage(
            int currentDurability,
            int maximumDurability,
            float sealedMinimum,
            float crackedMinimum,
            float compromisedMinimum)
        {
            if (currentDurability <= 0 || maximumDurability <= 0)
            {
                return ResonantCarapaceIntegrityStage.Depleted;
            }

            float ratio = Mathf.Clamp01(currentDurability / (float)maximumDurability);
            if (ratio >= Mathf.Clamp01(sealedMinimum)) return ResonantCarapaceIntegrityStage.Sealed;
            if (ratio >= Mathf.Clamp01(crackedMinimum)) return ResonantCarapaceIntegrityStage.Cracked;
            if (ratio >= Mathf.Clamp01(compromisedMinimum)) return ResonantCarapaceIntegrityStage.Compromised;
            return ResonantCarapaceIntegrityStage.Critical;
        }

        public static int ResolveRestorationAmount(int maximumDurability, float restorationFraction)
        {
            return Mathf.Max(0, Mathf.FloorToInt(
                Mathf.Max(0, maximumDurability) * Mathf.Clamp01(restorationFraction)));
        }

        public static bool CanRestoreWithBurst(
            bool isEquipped,
            bool terminallyFractured,
            bool resonanceDisabled,
            int currentDurability,
            int maximumDurability,
            float currentMana,
            float manaCost)
        {
            return isEquipped
                && !terminallyFractured
                && !resonanceDisabled
                && currentDurability >= 0
                && currentDurability < Mathf.Max(1, maximumDurability)
                && manaCost > 0f
                && currentMana >= manaCost;
        }
    }

    /// <summary>Single owner of the equipped Resonant Carapace item and durability.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerResonantCarapace : MonoBehaviour, IResonantCarapaceInventorySource
    {
        [Header("Core Tuning")]
        [SerializeField, Min(1)] private int baseMaximumDurability = 100;
        [SerializeField, Range(.01f, 1f)] private float emergencyHealthFraction = .15f;
        [SerializeField, Min(.01f)] private float repairPerDivertedMana = 1f;
        [Header("Progressive Integrity")]
        [SerializeField, Range(0f, 1f)] private float sealedMinimumIntegrity = .75f;
        [SerializeField, Range(0f, 1f)] private float crackedMinimumIntegrity = .5f;
        [SerializeField, Range(0f, 1f)] private float compromisedMinimumIntegrity = .25f;
        [SerializeField, Range(0f, 1f)] private float sealedDamageShare = .5f;
        [SerializeField, Range(0f, 1f)] private float crackedDamageShare = .4f;
        [SerializeField, Range(0f, 1f)] private float compromisedDamageShare = .3f;
        [SerializeField, Range(0f, 1f)] private float criticalDamageShare = .15f;
        [SerializeField, Range(0f, 1f)] private float sealedPhysicalEffectiveness = 1f;
        [SerializeField, Range(0f, 1f)] private float crackedPhysicalEffectiveness = .75f;
        [SerializeField, Range(0f, 1f)] private float compromisedPhysicalEffectiveness = .5f;
        [SerializeField, Range(0f, 1f)] private float criticalPhysicalEffectiveness = .25f;
        [Header("Restoration Burst")]
        [SerializeField, Range(0f, 1f)] private float restorationBurstManaFraction = .5f;
        [SerializeField, Range(0f, 1f)] private float restorationBurstDurabilityFraction = .25f;
        [Header("Physical Control")]
        [SerializeField, Range(0f, 1f)] private float resistancePerAnchoredLatticeLevel = .08f;
        [Header("Runtime")]
        [SerializeField] private bool owned;
        [SerializeField] private bool equipped;
        [SerializeField, Min(0)] private int durability;
        [SerializeField] private bool terminallyFractured;
        [SerializeField] private ResonantCarapaceState state;

        private PlayerHealth health;
        private PlayerPermanentProgression progression;
        private GuardResonanceState resonance;
        private bool wasResonanceDisabled;

        public event Action StateChanged;
        /// <summary>Raised only after this owner has accepted durability damage.</summary>
        public event Action<int> DamageAbsorbed;
        /// <summary>Raised once after an approved Deep-Brace restoration transaction.</summary>
        public event Action<int> RestorationBurst;
        /// <summary>Raised once when this item is terminally destroyed.</summary>
        public event Action TerminalFractured;
        public bool IsOwned => owned && !terminallyFractured;
        public bool IsEquipped => IsOwned && equipped;
        public int CurrentDurability => durability;
        public int MaximumDurability
        {
            get
            {
                if (progression == null) progression = GetComponent<PlayerPermanentProgression>();
                return Mathf.Max(1, baseMaximumDurability + (progression != null ? progression.StructuralReinforcementLevel * progression.StructuralReinforcementDurabilityPerLevel : 0));
            }
        }
        public ResonantCarapaceState State { get { RefreshState(); return state; } }
        public float IntegrityRatio => Mathf.Clamp01(durability / (float)MaximumDurability);
        public ResonantCarapaceIntegrityStage IntegrityStage => ResonantCarapacePolicy.ResolveIntegrityStage(
            durability, MaximumDurability, sealedMinimumIntegrity, crackedMinimumIntegrity, compromisedMinimumIntegrity);
        public float StageDamageShare => DamageShareFor(IntegrityStage);
        public float EffectiveDamageShare => IsOperational ? StageDamageShare : 0f;
        public float PhysicalStageEffectiveness => PhysicalEffectivenessFor(IntegrityStage);
        public bool IsOperational => IsEquipped && durability > 0 && !IsResonanceDisabled;
        public bool IsResonanceDisabled
        {
            get
            {
                if (resonance == null) resonance = GetComponent<GuardResonanceState>();
                return IsEquipped && resonance != null && resonance.IsDestabilized;
            }
        }
        public float PhysicalControlResistance
        {
            get
            {
                if (progression == null) progression = GetComponent<PlayerPermanentProgression>();
                return IsOperational
                    ? ResonantCarapacePolicy.ResolveStagedPhysicalResistance(
                        (progression != null ? progression.AnchoredLatticeLevel : 0) * resistancePerAnchoredLatticeLevel,
                        PhysicalStageEffectiveness)
                    : 0f;
            }
        }
        public string ConditionText => !IsOwned
            ? (terminallyFractured ? "Terminally fractured — acquire a replacement." : "Unequipped — no Carapace owned.")
            : IsResonanceDisabled ? "Resonance Disabled — durability preserved: " + durability + " / " + MaximumDurability
            : durability <= 0 ? "Depleted — repair through Deep Brace."
            : "Equipped: " + durability + " / " + MaximumDurability + " durability (" + IntegrityStage + ")";

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            progression = GetComponent<PlayerPermanentProgression>();
            resonance = GetComponent<GuardResonanceState>();
            durability = Mathf.Clamp(durability, 0, MaximumDurability);
            RefreshState();
        }
        private void OnEnable() { if (health != null) health.Died += HandleDeath; }
        private void OnDisable() { if (health != null) health.Died -= HandleDeath; }
        private void Update() { RefreshState(); }

        public bool TryGrantAndEquipForDevelopment()
        {
            owned = true;
            equipped = true;
            terminallyFractured = false;
            durability = MaximumDurability;
            Changed();
            return true;
        }
        public bool TryEquip()
        {
            if (!IsOwned || equipped) return false;
            equipped = true;
            Changed();
            return true;
        }
        public bool TryUnequip()
        {
            if (!equipped) return false;
            equipped = false;
            Changed();
            return true;
        }

        /// <summary>Called after Guard, Shield, Curse, and Phase have resolved an accepted hit.</summary>
        public int ResolveIncomingDamage(int incomingDamage, int currentHealth, int maximumHealth)
        {
            int incoming = Mathf.Max(0, incomingDamage);
            if (!IsOperational || incoming == 0) return incoming;
            // Capture the stage once before the hit. Crossing a threshold during
            // this transaction never changes the split halfway through it.
            float damageShare = StageDamageShare;
            int intendedAbsorb = ResonantCarapacePolicy.ResolveNormalAbsorption(incoming, durability, damageShare);
            int playerDamage = incoming - intendedAbsorb;
            int threshold = Mathf.CeilToInt(Mathf.Max(1, maximumHealth) * emergencyHealthFraction);
            if (currentHealth - playerDamage < threshold)
            {
                TerminalFracture();
                return 0;
            }
            if (intendedAbsorb > 0)
            {
                durability -= intendedAbsorb;
                Changed();
                DamageAbsorbed?.Invoke(intendedAbsorb);
            }
            return playerDamage;
        }

        /// <summary>Diverts only an already-calculated Deep Brace mana contribution.</summary>
        public float DivertDeepBraceMana(float availableMana)
        {
            if (!IsEquipped || terminallyFractured || durability >= MaximumDurability || availableMana <= 0f) return 0f;
            float diverted = ResonantCarapacePolicy.ResolveDeepBraceDiversion(availableMana);
            if (progression == null) progression = GetComponent<PlayerPermanentProgression>();
            float efficiency = 1f + (progression != null ? progression.LivingRegenerationLevel * progression.LivingRegenerationEfficiencyPerLevel : 0f);
            int repaired = Mathf.FloorToInt(diverted * repairPerDivertedMana * efficiency);
            if (repaired > 0)
            {
                durability = Mathf.Min(MaximumDurability, durability + repaired);
                Changed();
            }
            return diverted;
        }

        /// <summary>
        /// Performs the atomic Deep-Brace Burst resource transaction. Deep Brace
        /// itself remains the input/state authority; this owner only validates
        /// equipment state, spends exact stored Mana, and mutates durability.
        /// </summary>
        public bool TryRestoreWithBurst(PlayerMana mana, out int restoredDurability)
        {
            restoredDurability = 0;
            if (mana == null)
            {
                return false;
            }

            float manaCost = ResonantCarapacePolicy.ResolveRestorationManaCost(
                mana.MaximumMana, restorationBurstManaFraction);
            if (!ResonantCarapacePolicy.CanRestoreWithBurst(
                IsEquipped, terminallyFractured, IsResonanceDisabled,
                durability, MaximumDurability, mana.CurrentMana, manaCost))
            {
                return false;
            }
            int requestedRestoration = ResonantCarapacePolicy.ResolveRestorationAmount(
                MaximumDurability, restorationBurstDurabilityFraction);
            if (requestedRestoration <= 0 || !mana.TrySpendFixedMana(manaCost))
            {
                return false;
            }

            int before = durability;
            durability = Mathf.Min(MaximumDurability, durability + requestedRestoration);
            restoredDurability = durability - before;
            Changed();
            RestorationBurst?.Invoke(restoredDurability);
            return true;
        }

        private float DamageShareFor(ResonantCarapaceIntegrityStage integrityStage)
        {
            switch (integrityStage)
            {
                case ResonantCarapaceIntegrityStage.Sealed: return sealedDamageShare;
                case ResonantCarapaceIntegrityStage.Cracked: return crackedDamageShare;
                case ResonantCarapaceIntegrityStage.Compromised: return compromisedDamageShare;
                case ResonantCarapaceIntegrityStage.Critical: return criticalDamageShare;
                default: return 0f;
            }
        }

        private float PhysicalEffectivenessFor(ResonantCarapaceIntegrityStage integrityStage)
        {
            switch (integrityStage)
            {
                case ResonantCarapaceIntegrityStage.Sealed: return sealedPhysicalEffectiveness;
                case ResonantCarapaceIntegrityStage.Cracked: return crackedPhysicalEffectiveness;
                case ResonantCarapaceIntegrityStage.Compromised: return compromisedPhysicalEffectiveness;
                case ResonantCarapaceIntegrityStage.Critical: return criticalPhysicalEffectiveness;
                default: return 0f;
            }
        }

        public void TerminalFracture()
        {
            if (!IsEquipped || terminallyFractured) return;
            durability = 0;
            owned = false;
            equipped = false;
            terminallyFractured = true;
            Changed();
            TerminalFractured?.Invoke();
        }
        /// <summary>Explicit new-game seam; ordinary death uses HandleDeath instead.</summary>
        public void ResetForNewGame()
        {
            owned = false;
            equipped = false;
            durability = 0;
            terminallyFractured = false;
            Changed();
        }
        private void HandleDeath()
        {
            if (!owned && !equipped) return;
            owned = false;
            equipped = false;
            durability = 0;
            terminallyFractured = false;
            Changed();
        }
        private void RefreshState()
        {
            ResonantCarapaceState next = terminallyFractured ? ResonantCarapaceState.TerminallyFractured
                : !IsEquipped ? ResonantCarapaceState.Unequipped
                : IsResonanceDisabled ? ResonantCarapaceState.ResonanceDisabled
                : durability <= 0 ? ResonantCarapaceState.Depleted
                : durability < MaximumDurability ? ResonantCarapaceState.Damaged
                : ResonantCarapaceState.Active;
            bool resonanceDisabled = next == ResonantCarapaceState.ResonanceDisabled;
            if (next != state || resonanceDisabled != wasResonanceDisabled) { state = next; wasResonanceDisabled = resonanceDisabled; StateChanged?.Invoke(); }
        }
        private void Changed()
        {
            ResonantCarapaceState before = state;
            RefreshState();
            if (before == state) StateChanged?.Invoke();
        }

        private void OnValidate()
        {
            sealedMinimumIntegrity = Mathf.Clamp01(sealedMinimumIntegrity);
            crackedMinimumIntegrity = Mathf.Clamp(crackedMinimumIntegrity, 0f, sealedMinimumIntegrity);
            compromisedMinimumIntegrity = Mathf.Clamp(compromisedMinimumIntegrity, 0f, crackedMinimumIntegrity);
        }
    }
}
