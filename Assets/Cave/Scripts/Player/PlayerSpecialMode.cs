using System;
using Cave.Axioms;
using Cave.Axioms.Control;
using Cave.Axioms.Mastery;
using Cave.Combat;
using Cave.Domain;
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
            PlayerMasteryEvidenceRuntime.EnsureOn(gameObject);
            PlayerDomainLawCollection.EnsureOn(gameObject);
            AxiomControlState control = AxiomControlState.EnsureOn(gameObject);
            control.InterventionSucceeded -= HandleAxiomCorrection;
            control.InterventionSucceeded += HandleAxiomCorrection;
            control.TemporalStageChanged -= HandleTemporalStageChanged;
            control.TemporalStageChanged += HandleTemporalStageChanged;
        }

        private void OnDestroy()
        {
            AxiomControlState control = GetComponent<AxiomControlState>();
            if (control == null) return;
            control.InterventionSucceeded -= HandleAxiomCorrection;
            control.TemporalStageChanged -= HandleTemporalStageChanged;
        }

        private void HandleAxiomCorrection(AxiomKind kind, AxiomErrorState error, float quality, float timestamp)
        {
            MasteryDomain domain;
            if (AxiomMasteryState.TryDomain(kind, out domain))
            {
                AxiomMasteryState mastery = AxiomMasteryState.EnsureOn(gameObject);
                int context = ((int)kind * 10) + (int)error.ErrorKind;
                mastery.Record(new MasteryEvidence(domain, MasteryEvidenceKind.Correction, quality, Mathf.Max(.1f,error.Magnitude), timestamp, context));
                if (quality >= .8f)
                {
                    mastery.Record(new MasteryEvidence(domain, MasteryEvidenceKind.Quality, quality, Mathf.Max(.1f,error.Magnitude), timestamp, context));
                }
            }
        }

        private void HandleTemporalStageChanged(AxiomKind kind, AxiomTemporalLockStage current, AxiomTemporalLockStage previous)
        {
            if (current <= previous || current == AxiomTemporalLockStage.Uncontrolled)
            {
                return;
            }

            MasteryDomain domain;
            if (!AxiomMasteryState.TryDomain(kind, out domain)) return;
            float quality = current == AxiomTemporalLockStage.Acceleration ? 1f
                : current == AxiomTemporalLockStage.Rate ? .9f : .8f;
            AxiomMasteryState.EnsureOn(gameObject).Record(new MasteryEvidence(
                domain,
                MasteryEvidenceKind.Regulation,
                quality,
                (float)current,
                Time.time,
                ((int)kind * 10) + (int)current));
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
