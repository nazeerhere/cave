using System;
using System.Reflection;
using Cave.Combat;
using Cave.Domain;
using UnityEngine;

namespace Cave.Environmental
{
    /// <summary>Focused deterministic contract checks; no scene authoring or live combat state is mutated.</summary>
    public static class EnvironmentalDestructibleVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyCanonicalPhenomena(out failure)) return false;
            if (!VerifyImpactClassification(out failure)) return false;
            if (!VerifyMaterialProfiles(out failure)) return false;
            if (!VerifyIntegrityAndDeduplication(out failure)) return false;
            if (!VerifyAuthoredSupportCollapse(out failure)) return false;
            if (!VerifyCrystalDischarge(out failure)) return false;
            if (!VerifyFloraContracts(out failure)) return false;
            failure = null;
            return true;
        }

        private static bool VerifyCanonicalPhenomena(out string failure)
        {
            Array values = Enum.GetValues(typeof(LawPhenomenon));
            if (values.Length != 8 || (LawPhenomenon)values.GetValue(0) != LawPhenomenon.Heat
                || (LawPhenomenon)values.GetValue(7) != LawPhenomenon.Order)
            {
                failure = "Environmental profiles must use exactly the eight canonical LawPhenomenon values.";
                return false;
            }
            failure = null;
            return true;
        }

        private static bool VerifyImpactClassification(out string failure)
        {
            StructuralImpactContext normal = new StructuralImpactContext(null, Vector2.zero, Vector2.right, 1f,
                new DamageContext((GameObject)null, DamageTrait.Melee), false, false, 17);
            StructuralImpactContext heavy = new StructuralImpactContext(null, Vector2.zero, Vector2.right, 1f,
                new DamageContext((GameObject)null, DamageTrait.Melee | DamageTrait.Heavy), false, false, 18);
            if (normal.IsHeavy || !heavy.IsHeavy || normal.EventId == heavy.EventId)
            {
                failure = "Structural impact classification or deduplication contract failed.";
                return false;
            }
            failure = null;
            return true;
        }

        private static bool VerifyMaterialProfiles(out string failure)
        {
            EnvironmentalMaterialProfile[] profiles = Resources.LoadAll<EnvironmentalMaterialProfile>("Environment/Materials");
            Array phenomena = Enum.GetValues(typeof(LawPhenomenon));
            if (profiles.Length != Enum.GetValues(typeof(EnvironmentalMaterialFamily)).Length)
            {
                failure = "Every environmental material family requires one loadable profile.";
                return false;
            }
            for (int i = 0; i < profiles.Length; i++)
                foreach (LawPhenomenon phenomenon in phenomena)
                    if (!profiles[i].HasExplicitResponseFor(phenomenon))
                    {
                        failure = profiles[i].name + " has no explicit response for " + phenomenon + ".";
                        return false;
                    }
            EnvironmentalMaterialProfile wood = Resources.Load<EnvironmentalMaterialProfile>("Environment/Materials/Wood");
            if (wood == null || wood.ResolveImpact(1f, true) <= wood.ResolveImpact(1f, false))
            {
                failure = "Heavy material response is not stronger than ordinary impact for Wood.";
                return false;
            }
            failure = null;
            return true;
        }

        private static bool VerifyIntegrityAndDeduplication(out string failure)
        {
            GameObject target = new GameObject("EnvironmentalVerificationIntegrity");
            try
            {
                EnvironmentalDestructible structural = target.AddComponent<EnvironmentalDestructible>();
                StructuralImpactContext impact = new StructuralImpactContext(null, Vector2.zero, Vector2.right,
                    .5f, default, false, false, 41);
                bool first = structural.ApplyStructuralImpact(impact);
                float afterFirst = structural.Integrity;
                bool duplicate = structural.ApplyStructuralImpact(impact);
                SetField(structural, "progressionCritical", true);
                bool protectedHit = structural.ApplyStructuralImpact(new StructuralImpactContext(null, Vector2.zero,
                    Vector2.right, 99f, default, false, false, 42));
                if (!first || duplicate || !Mathf.Approximately(afterFirst, structural.Integrity) || protectedHit)
                {
                    failure = "Integrity, event deduplication, or protected-topology invariant failed.";
                    return false;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
            failure = null;
            return true;
        }

        private static bool VerifyAuthoredSupportCollapse(out string failure)
        {
            GameObject root = new GameObject("EnvironmentalVerificationScaffold");
            try
            {
                EnvironmentalDestructible left = new GameObject("Left").AddComponent<EnvironmentalDestructible>();
                EnvironmentalDestructible right = new GameObject("Right").AddComponent<EnvironmentalDestructible>();
                left.transform.SetParent(root.transform); right.transform.SetParent(root.transform);
                StructuralSupportGroup group = root.AddComponent<StructuralSupportGroup>();
                SetField(group, "requiredSupports", new[] { left, right });
                root.SetActive(false); root.SetActive(true);
                left.ApplyStructuralImpact(new StructuralImpactContext(null, Vector2.zero, Vector2.right, 99f, default, false, false, 51));
                group.EvaluateSupports();
                if (group.IsCollapsed) { failure = "Scaffold collapsed before all authored supports failed."; return false; }
                right.ApplyStructuralImpact(new StructuralImpactContext(null, Vector2.zero, Vector2.right, 99f, default, false, false, 52));
                group.EvaluateSupports();
                if (!group.IsCollapsed) { failure = "Scaffold did not collapse after all authored supports failed."; return false; }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            failure = null;
            return true;
        }

        private static bool VerifyCrystalDischarge(out string failure)
        {
            GameObject crystal = new GameObject("EnvironmentalVerificationCrystal");
            try
            {
                crystal.AddComponent<EnvironmentalDestructible>();
                CrystalPhenomenonStorage storage = crystal.AddComponent<CrystalPhenomenonStorage>();
                int discharges = 0;
                storage.Discharged += (_, phenomenon, magnitude) =>
                {
                    if (phenomenon == LawPhenomenon.Potential && magnitude > 0f) discharges++;
                };
                storage.ApplyPhenomenon(LawPhenomenon.Potential, 99f, null, 61);
                if (discharges != 1)
                {
                    failure = "Crystal shatter did not emit exactly one typed phenomenon discharge.";
                    return false;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(crystal); }
            failure = null;
            return true;
        }

        private static bool VerifyFloraContracts(out string failure)
        {
            Array species = Enum.GetValues(typeof(CaveFloraSpecies));
            if (species.Length != 9
                || FloraDistributionMatrix.Get(CaveFloraSpecies.Glowcaps, CaveMapRegion.Breach) != FloraAbundance.High
                || FloraDistributionMatrix.Get(CaveFloraSpecies.SovereignBloom, CaveMapRegion.Settlement) != FloraAbundance.Excluded)
            {
                failure = "Flora roster contract is incomplete.";
                return false;
            }
            GameObject floraObject = new GameObject("EnvironmentalVerificationFlora");
            try
            {
                EnvironmentalDestructible core = floraObject.AddComponent<EnvironmentalDestructible>();
                EnvironmentalFlora flora = floraObject.AddComponent<EnvironmentalFlora>();
                SetField(flora, "biologicalCore", core);
                SetField(flora, "propagationLimit", 1);
                SetField(flora, "propagationRadius", 2f);
                floraObject.SetActive(false); floraObject.SetActive(true);
                bool first = flora.TryRequestPropagation((Vector2)flora.transform.position + Vector2.right);
                bool second = flora.TryRequestPropagation((Vector2)flora.transform.position + Vector2.left);
                core.ApplyStructuralImpact(new StructuralImpactContext(null, Vector2.zero, Vector2.right, 99f,
                    default, false, false, 71));
                if (!first || second || !flora.PermanentlyDead)
                {
                    failure = "Flora propagation bound or permanent core-death rule failed.";
                    return false;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(floraObject); }
            failure = null;
            return true;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            field.SetValue(target, value);
        }
    }
}
