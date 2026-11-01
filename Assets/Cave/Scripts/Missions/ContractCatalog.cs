using System;
using UnityEngine;

namespace Cave.Missions
{
    [CreateAssetMenu(menuName = "Cave/Contracts/Contract Catalog", fileName = "ContractCatalog")]
    public sealed class ContractCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Contracts/ContractCatalog";
        public const string HerbalistsRequestId = "herbalists_request";
        public const string SalvageRecoveryId = "salvage_recovery";
        public const string BreakersTrialId = "breakers_trial";
        public const string VerminPurgeId = "vermin_purge";
        public const string HazardSweepId = "hazard_sweep";
        public const string CaravanProtectionId = "caravan_protection";
        public const string AxiomProvingGroundId = "axiom_proving_ground";
        public const string SovereignGauntletId = "sovereign_gauntlet";

        [SerializeField] private ContractDefinition[] contracts;

        public ContractDefinition[] Contracts => contracts;

        public ContractDefinition Find(string contractId)
        {
            if (contracts == null || string.IsNullOrEmpty(contractId)) return null;
            for (int i = 0; i < contracts.Length; i++)
            {
                ContractDefinition definition = contracts[i];
                if (definition != null
                    && string.Equals(definition.Id, contractId, StringComparison.Ordinal)) return definition;
            }
            return null;
        }

        public static ContractCatalog LoadOrCreateDefaults()
        {
            ContractCatalog catalog = Resources.Load<ContractCatalog>(ResourcesPath);
            return catalog != null ? catalog : CreateRuntimeDefaults();
        }

        public static ContractCatalog CreateRuntimeDefaults()
        {
            ContractCatalog catalog = CreateInstance<ContractCatalog>();
            catalog.hideFlags = HideFlags.DontSave;
            catalog.contracts = CreateApprovedRoster();
            return catalog;
        }

        public static ContractDefinition[] CreateApprovedRoster()
        {
            return new[]
            {
                ContractDefinition.Create(
                    HerbalistsRequestId, "HERBALIST'S REQUEST", ContractTier.Initiate,
                    "Gather three contract-scoped flora samples.",
                    "Exploration, interaction, and safe resource recovery.", 10,
                    RecoveryContractObjectiveType.FloraSamples, 3, 75f, 40f),
                ContractDefinition.Create(
                    SalvageRecoveryId, "SALVAGE RECOVERY", ContractTier.Initiate,
                    "Retrieve three marked supply caches.",
                    "Movement, route planning, and interaction.", 13,
                    RecoveryContractObjectiveType.SupplyCaches, 3, 90f, 45f),
                ContractDefinition.Create(
                    BreakersTrialId, "BREAKER'S TRIAL", ContractTier.Initiate,
                    "Break marked environmental targets.",
                    "Environmental destruction and impact timing.", 15),
                ContractDefinition.Create(
                    VerminPurgeId, "VERMIN PURGE", ContractTier.Initiate,
                    "Clear a contained vermin infestation.",
                    "Baseline combat and target prioritization.", 18),
                ContractDefinition.Create(
                    HazardSweepId, "HAZARD SWEEP", ContractTier.Adventurer,
                    "Traverse and neutralize a hazardous route.",
                    "Hazard reading, movement, and mitigation.", 40),
                ContractDefinition.Create(
                    CaravanProtectionId, "CARAVAN PROTECTION", ContractTier.Adventurer,
                    "Escort a vulnerable caravan through danger.",
                    "Protection, positioning, and threat control.", 49),
                ContractDefinition.Create(
                    AxiomProvingGroundId, "AXIOM PROVING GROUND", ContractTier.Veteran,
                    "Complete a controlled Axiom challenge.",
                    "Axiom state, rate, acceleration, and intervention.", 135),
                ContractDefinition.Create(
                    SovereignGauntletId, "SOVEREIGN GAUNTLET", ContractTier.Veteran,
                    "Survive the Sovereign's advanced trial.",
                    "Integrated mastery of Cave's combat systems.", 180)
            };
        }
    }
}
