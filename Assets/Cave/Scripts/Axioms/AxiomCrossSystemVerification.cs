using Cave.Axioms.Convergence;
using Cave.Axioms.Control;
using Cave.Axioms.Elemental;
using Cave.Axioms.Frequency;
using Cave.Axioms.Mastery;
using Cave.Axioms.Phase;
using Cave.Combat;
using Cave.Player;

namespace Cave.Axioms
{
    /// <summary>
    /// Development-only, allocation-free deterministic coverage for the Axiom
    /// seams that span otherwise actor-local runtime components. It is never
    /// polled by gameplay and is callable from editor/test tooling.
    /// </summary>
    public static class AxiomCrossSystemVerification
    {
        public struct Result
        {
            public bool A_TimingVsStack;
            public bool B_MissedApplication;
            public bool C_LockAcquisition;
            public bool D_LockRecovery;
            public bool E_MasteryEvidence;
            public bool F_RepetitionFloor;
            public bool G_EffectiveCounter;
            public bool H_Resonance;
            public bool I_FrequencyFiltering;
            public bool J_CounterphaseIsolation;
            public bool K_PhaseParity;
            public bool L_Convergence;
            public bool M_FrenzyAmount;

            public bool Passed => A_TimingVsStack && B_MissedApplication && C_LockAcquisition
                && D_LockRecovery && E_MasteryEvidence && F_RepetitionFloor
                && G_EffectiveCounter && H_Resonance && I_FrequencyFiltering
                && J_CounterphaseIsolation && K_PhaseParity && L_Convergence && M_FrenzyAmount;
        }

        public static Result VerifyAll()
        {
            Result result = new Result();
            result.A_TimingVsStack = AxiomTemporalControlVerification.VerifyAll()
                && AxiomDynamicsVerification.VerifyAll().D5RapidVsSpaced;
            result.B_MissedApplication = AxiomTemporalControlVerification.VerifyAll();
            result.C_LockAcquisition = AxiomTemporalControlVerification.VerifyAll();
            result.D_LockRecovery = AxiomTemporalControlVerification.VerifyAll();
            result.E_MasteryEvidence = VerifyMasteryEvidence();
            result.F_RepetitionFloor = VerifyRepetitionFloor();
            result.G_EffectiveCounter = AxiomDynamicsVerification.VerifyAll().D11EffectiveCounter;
            string resonanceFailure;
            result.H_Resonance = GuardResonanceVerification.TryRunAll(out resonanceFailure);
            result.I_FrequencyFiltering = VerifyFrequencyFilter();
            result.J_CounterphaseIsolation = VerifyCounterphaseIsolation();
            result.K_PhaseParity = VerifyPhaseParity();
            result.L_Convergence = VerifyConvergence();
            result.M_FrenzyAmount = VerifyFrenzyAmount();
            return result;
        }

        private static bool VerifyMasteryEvidence()
        {
            MasteryEvidence ordinary = new MasteryEvidence(MasteryDomain.Heat, MasteryEvidenceKind.OrdinaryUse, 1f, 1f, 0f, 1);
            MasteryEvidence correction = new MasteryEvidence(MasteryDomain.Heat, MasteryEvidenceKind.Correction, 1f, 1f, 0f, 2);
            MasteryEvidence quality = new MasteryEvidence(MasteryDomain.Heat, MasteryEvidenceKind.Quality, 1f, 1f, 0f, 3);
            MasteryEvidence regulation = new MasteryEvidence(MasteryDomain.Heat, MasteryEvidenceKind.Regulation, 1f, 1f, 0f, 4);
            float baseGain = AxiomMasteryState.CalculateEvidenceGain(ordinary, 0, false);
            return AxiomMasteryState.CalculateEvidenceGain(correction, 0, false) > baseGain * 4f
                && AxiomMasteryState.CalculateEvidenceGain(quality, 0, false) > baseGain * 4f
                && AxiomMasteryState.CalculateEvidenceGain(regulation, 0, false) > baseGain * 4f;
        }

        private static bool VerifyRepetitionFloor()
        {
            MasteryEvidence evidence = new MasteryEvidence(MasteryDomain.Flow, MasteryEvidenceKind.OrdinaryUse, 1f, 1f, 0f, 9);
            float first = AxiomMasteryState.CalculateEvidenceGain(evidence, 0, false);
            float repeated = AxiomMasteryState.CalculateEvidenceGain(evidence, 999, true);
            float changedContext = AxiomMasteryState.CalculateEvidenceGain(
                new MasteryEvidence(MasteryDomain.Flow, MasteryEvidenceKind.OrdinaryUse, 1f, 1f, 0f, 10), 0, false);
            return repeated >= first * .2f && repeated > 0f && changedContext == first;
        }

        private static bool VerifyFrequencyFilter()
        {
            GuardResonanceSettings settings = new GuardResonanceSettings(.8f, .15f, 2.5f, 4);
            GuardResonanceTracker a = new GuardResonanceTracker();
            GuardResonanceTracker b = new GuardResonanceTracker();
            a.RegisterContact(0f, settings, true);
            a.RegisterContact(.8f, settings, true);
            a.RegisterContact(1.6f, settings, true);
            GuardResonanceContactResult filtered = a.RegisterContact(2.4f, settings, true);
            b.RegisterContact(0f, settings, true);
            GuardResonanceContactResult independent = b.RegisterContact(.8f, settings, true);
            return filtered.Outcome == GuardResonanceContactOutcome.Filtered
                && filtered.Progress == 2 && independent.Progress == 1;
        }

        private static bool VerifyCounterphaseIsolation()
        {
            GuardResonanceSettings settings = new GuardResonanceSettings(.8f, .15f, 2.5f, 4);
            GuardResonanceTracker attackerA = new GuardResonanceTracker();
            GuardResonanceTracker attackerB = new GuardResonanceTracker();
            attackerA.RegisterContact(0f, settings);
            attackerB.RegisterContact(0f, settings);
            attackerA.RegisterContact(.8f, settings);
            attackerB.RegisterContact(.8f, settings);
            attackerA.Reset(); // Equivalent to ClearCadenceSequence(A); B remains untouched.
            return !attackerA.HasReference && attackerB.HasReference
                && attackerB.Progress == 1 && settings.NaturalPeriodSeconds == .8f;
        }

        private static bool VerifyPhaseParity()
        {
            PhaseExposureDefinition even = PhaseCombatRules.ResolveExposure(2, 1.35f, 1.18f, 2.5f, 4f, .6f);
            PhaseExposureDefinition strong = PhaseCombatRules.ResolveExposure(1, 1.35f, 1.18f, 2.5f, 4f, .6f);
            PhaseExposureDefinition weakLong = PhaseCombatRules.ResolveExposure(3, 1.35f, 1.18f, 2.5f, 4f, .6f);
            return !even.IsActive && strong.EffectClass == PhaseEffectClass.Strong
                && weakLong.EffectClass == PhaseEffectClass.WeakLong
                && weakLong.Duration > strong.Duration;
        }

        private static bool VerifyConvergence()
        {
            AxiomConvergenceActionGate gate = new AxiomConvergenceActionGate();
            return !AxiomConvergenceRules.Qualifies(false, true, true, 3)
                && !AxiomConvergenceRules.Qualifies(true, false, true, 3)
                && !AxiomConvergenceRules.Qualifies(true, true, false, 3)
                && !AxiomConvergenceRules.Qualifies(true, true, true, 1)
                && AxiomConvergenceRules.Qualifies(true, true, true, 2)
                && gate.TryConsume(7) && !gate.TryConsume(7) && gate.TryConsume(8);
        }

        private static bool VerifyFrenzyAmount()
        {
            AxiomKind kind;
            bool receiverIsTarget;
            float amount;
            return ElementalAxiomApplicationRules.TryResolveDirectPlayerHit(
                SpecialMode.Flight, true, true, out kind, out receiverIsTarget, out amount)
                && kind == AxiomKind.Flow && amount == 2f;
        }
    }
}
