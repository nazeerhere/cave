using System;
using Cave.Axioms;
using Cave.Axioms.Control;
using Cave.Axioms.Mastery;
using Cave.Combat;
using UnityEngine;

namespace Cave.Player
{
    [RequireComponent(typeof(PlayerCurrency))]
    public sealed class PlayerSpecialMode : MonoBehaviour
    {
        [SerializeField] private SpecialMode startingMode = SpecialMode.SlowShot;

        [Header("Currency Switch Costs")]
        [SerializeField, Min(0)] private int slowShotSwitchCost = 3;
        [SerializeField, Min(0)] private int burnShotSwitchCost = 3;
        [SerializeField, Min(0)] private int flightSwitchCost = 5;
        [SerializeField, Min(0)] private int damageBoostSwitchCost = 5;

        private PlayerCurrency playerCurrency;

        public event Action<SpecialMode> SpecialModeChanged;

        public SpecialMode CurrentMode { get; private set; }

        private void Awake()
        {
            playerCurrency = GetComponent<PlayerCurrency>();
            CurrentMode = startingMode;
            // One-time player installation point; avoids prefab YAML edits and
            // never attaches telemetry to enemies or PlayerHealth.
            if (GetComponent<PlayerMovementTelemetry>() == null)
            {
                gameObject.AddComponent<PlayerMovementTelemetry>();
            }

            if (GetComponent<AxiomRuntimeState>() == null)
            {
                gameObject.AddComponent<AxiomRuntimeState>();
            }

            if (GetComponent<AxiomPlayerMovementControl>() == null)
            {
                gameObject.AddComponent<AxiomPlayerMovementControl>();
            }
            AxiomMasteryState.EnsureOn(gameObject);
            AxiomControlState control = AxiomControlState.EnsureOn(gameObject);
            control.InterventionSucceeded -= HandleAxiomCorrection;
            control.InterventionSucceeded += HandleAxiomCorrection;
        }

        private void HandleAxiomCorrection(AxiomKind kind, AxiomErrorState error, float quality, float timestamp)
        {
            MasteryDomain domain;
            if (AxiomMasteryState.TryDomain(kind, out domain))
                AxiomMasteryState.EnsureOn(gameObject).Record(new MasteryEvidence(domain, MasteryEvidenceKind.Correction, quality, Mathf.Max(.1f,error.Magnitude), timestamp));
        }

        public bool TrySwitchMode(SpecialMode newMode)
        {
            if (newMode == CurrentMode)
            {
                return true;
            }

            int switchCost = GetSwitchCost(newMode);
            if (!playerCurrency.TrySpend(switchCost))
            {
                return false;
            }

            CurrentMode = newMode;
            SpecialModeChanged?.Invoke(CurrentMode);
            return true;
        }

        public int GetSwitchCost(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return slowShotSwitchCost;
                case SpecialMode.BurnShot:
                    return burnShotSwitchCost;
                case SpecialMode.Flight:
                    return flightSwitchCost;
                case SpecialMode.DamageBoost:
                    return damageBoostSwitchCost;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public void ResetRunMode()
        {
            if (CurrentMode == startingMode)
            {
                return;
            }

            CurrentMode = startingMode;
            SpecialModeChanged?.Invoke(CurrentMode);
        }
    }
}
