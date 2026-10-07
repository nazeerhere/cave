#if UNITY_EDITOR
using System;
using Cave.Axioms;
using Cave.Axioms.Control;
using Cave.Axioms.Elemental;
using Cave.Axioms.Frequency;
using Cave.Axioms.Mastery;
using Cave.Axioms.Phase;
using Cave.Axioms.Thresholds;
using Cave.Domain;
using Cave.Diagnostics;
using Cave.Gambit;
using Cave.Enemies;
using Cave.Player;
using Cave.Projectiles;
using Cave.UI;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Batch-safe combined regression entry point for the Axiom/Domain foundation.</summary>
    public static class AxiomDomainRegressionSweep
    {
        private delegate bool Verifier(out string failure);

        [MenuItem("Tools/Cave/Verification/Run Axiom + Domain Regression Sweep")]
        public static void RunBatch()
        {
            Verify("Three-State Axiom Dynamics", ThreeStateAxiomDynamicsVerification.TryRunAll);
            Verify("Authoritative Three-State Control", AuthoritativeThreeStateControlVerification.TryRunAll);
            Verify("Axiom Reactive Status Presentation", AxiomReactiveStatusPresentationVerification.TryRunAll);
            Verify("Axiom Status Presentation", VerifyAxiomStatusPresentation);
            Verify("Domain Contextual UI", DomainContextualUiVerification.TryRunAll);
            Verify("Domain Test Override", DomainTestOverrideVerification.TryRunAll);
            Verify("Axiom Mastery Integration", AxiomMasteryIntegrationVerification.TryRunAll);
            Verify("Expanded Phenomenon Runtime", ExpandedPhenomenonRuntimeVerification.TryRunAll);
            Verify("Phenomenon Threshold Foundation", AxiomPhenomenonThresholdVerification.TryRunAll);
            Verify("Axiom Opportunity Resolver", AxiomOpportunityResolverVerification.TryRunAll);
            Verify("Axiom Dynamics Control Unification", AxiomDynamicsControlUnificationVerification.TryRunAll);
            Check("Axiom dynamics", AxiomDynamicsVerification.VerifyAll().Passed);
            Check("Temporal control", AxiomTemporalControlVerification.VerifyAll());
            Verify("Scalar trajectory tracker", ScalarTrajectoryTrackerVerification.TryRunAll);
            Check("Elemental application", ElementalAxiomApplicationVerification.VerifyAll());
            Verify("Player mastery evidence", PlayerMasteryEvidenceVerification.TryRunAll);
            Verify("Runtime Axiom", AxiomRuntimeVerification.TryRunAll);
            Check("Cross-system Axiom", AxiomCrossSystemVerification.VerifyAll().Passed);
            Verify("Phase combat", PhaseCombatVerification.TryRunAll);
            Verify("Oblivion Trap gameplay", OblivionTrapGameplayVerification.TryRunAll);
            Verify("Heavy projectile / follow-ups", HeavyProjectileSprintVerification.TryRunAll);
            Verify("Player sword upgrade", PlayerSwordUpgradeVerification.TryRunAll);
            Verify("Guard resonance", GuardResonanceVerification.TryRunAll);
            Verify("Mob psychology", MobPsychologyVerification.TryRunAll);
            Verify("Knowledge architecture", KnowledgeArchitectureVerification.TryRunAll);
            Verify("Combat tactical coordinator", CombatTacticalCoordinatorVerification.TryRunAll);
            Verify("Diagnostics infrastructure", DiagnosticsInfrastructureVerification.TryRunAll);
            Verify("Sovereign Gambit foundation", SovereignGambitFoundationVerification.TryRunAll);
            Verify("Sovereign Gambit trap pieces", SovereignGambitTrapPieceVerification.TryRunAll);

            Verify("Complexity progression", DomainComplexityCapacityProgressionVerification.TryRunAll);
            Verify("Domain authoring eligibility", DomainAuthoringEligibilityVerification.TryRunAll);
            Verify("Domain law authoring", DomainLawAuthoringVerification.TryRunAll);
            Verify("LAW", DomainLawVerification.TryRunAll);
            Verify("SEMANTICS", PhenomenonSemanticsVerification.TryRunAll);
            Verify("PHENOMENON STATE MODEL", PhenomenonStateModelVerification.TryRunAll);
            Verify("OPERATIONS", PhenomenonOperationsVerification.TryRunAll);
            Verify("LAW EVALUATION", DomainLawEvaluationVerification.TryRunAll);
            Verify("TOPOLOGY / PROPAGATION", PhenomenonTopologyVerification.TryRunAll);
            Verify("CATALYSIS", CatalysisEvaluationVerification.TryRunAll);
            Verify("ACCUMULATION", AccumulationEvaluationVerification.TryRunAll);
            Verify("RELATIONSHIP GROUP", PhenomenonRelationshipGroupVerification.TryRunAll);
            Verify("SYNCHRONIZATION", SynchronizationEvaluationVerification.TryRunAll);
            Verify("INTERFERENCE", InterferenceEvaluationVerification.TryRunAll);
            Verify("DOMAIN COMPOSITION / COMPLEXITY", DomainCompositionVerification.TryRunAll);
            Verify("DOMAIN RESOLUTION PLANNER", DomainResolutionPlannerVerification.TryRunAll);
            Verify("DOMAIN ORCHESTRATION", DomainOrchestrationContractsVerification.TryRunAll);
            Verify("DOMAIN GENERATION 0", DomainGenerationZeroExecutorVerification.TryRunAll);
            Verify("DOMAIN GENERATION 1", DomainGenerationOneExecutorVerification.TryRunAll);
            Verify("DOMAIN GENERATION 2", DomainGenerationTwoExecutorVerification.TryRunAll);
            Verify("RESOLVED GENERATION 1", DomainResolvedGenerationOneVerification.TryRunAll);
            Verify("PROPOSAL COMPOSER", DomainProposalComposerVerification.TryRunAll);
            Verify("PROPOSAL COMPOSITION", DomainProposalCompositionIntegrationVerification.TryRunAll);
            Verify("RUNTIME COMMIT", DomainRuntimeCommitVerification.TryRunAll);
            Verify("DOMAIN SEMANTIC CODEC", AxiomDomainSemanticCodecVerification.TryRunAll);
            Verify("DOMAIN RUNTIME WORLD BRIDGE", DomainRuntimeWorldBridgeVerification.TryRunAll);
            Verify("CONFLICT ARBITRATION", DomainConflictArbitrationVerification.TryRunAll);
            Verify("CLAIM AUTHORITY SEAM", ClaimDomainAuthorityAdapterVerification.TryRunAll);
            Verify("ARBITRATED RUNTIME COMMIT", ArbitratedDomainRuntimeCommitVerification.TryRunAll);
            Verify("LOCALIZED DOMAIN EXECUTION COORDINATOR", LocalizedDomainExecutionCoordinatorVerification.TryRunAll);
            Debug.Log("[Cave] AXIOM + DOMAIN REGRESSION SWEEP PASS");
        }

        private static bool VerifyAxiomStatusPresentation(out string failure)
        {
            return AxiomStatusPresentationVerification.TryRunAll(out failure)
                && AxiomStatusPresentationVerification.TryVerifyUnityCellSeam(out failure);
        }

        private static void Verify(string name, Verifier verifier)
        {
            string failure;
            if (!verifier(out failure))
            {
                throw new InvalidOperationException(name + " verification failed: " + failure);
            }
        }

        private static void Check(string name, bool passed)
        {
            if (!passed)
            {
                throw new InvalidOperationException(name + " verification failed.");
            }
        }
    }
}
#endif
