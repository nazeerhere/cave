using System;
using System.Collections.Generic;
using Cave.Axioms.Mastery;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Deterministic coverage for the production authoring contract. Runtime
    /// GameObject ownership is intentionally a thin owner of these pure states;
    /// persistence is exercised without touching a user's PlayerPrefs payload.
    /// </summary>
    public static class DomainLawAuthoringVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyRuntimeOwnershipSurface(out failure)
                || !VerifyLiveMasteryAndCapacityGates(out failure)
                || !VerifyPersistenceAndIdentity(out failure)
                || !VerifyFailedAndDuplicateAuthoringDoNotMutate(out failure)
                || !VerifyReservedExpressionRemainsUnsupported(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyRuntimeOwnershipSurface(out string failure)
        {
            bool valid = typeof(MonoBehaviour).IsAssignableFrom(typeof(PlayerMasteryEvidenceRuntime))
                && typeof(MonoBehaviour).IsAssignableFrom(typeof(PlayerDomainLawCollection))
                && PlayerMasteryEvidenceRuntime.EnsureOn(null) == null
                && PlayerDomainLawCollection.EnsureOn(null) == null;
            failure = valid ? null : "Domain authoring runtime owners no longer expose the expected player-component seam.";
            return valid;
        }

        private static bool VerifyLiveMasteryAndCapacityGates(out string failure)
        {
            PlayerMasteryPolicy policy = PlayerMasteryPolicy.Default;
            DomainLaw candidate = CreateLaw(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation);
            PlayerMasteryEvidenceState mastered = MasteredProjectile(LawPhenomenon.Heat, policy);
            float exactCapacity = DomainComplexityCalculator.Calculate(
                DomainCompositionEditor.Add(DomainComposition.Empty, candidate).Resulting,
                DomainComplexityPolicy.Default).TotalComplexity;

            DomainAuthoringEligibilityResult exact = DomainAuthoringEligibility.Evaluate(
                DomainComposition.Empty,
                candidate,
                new DomainAuthoringContext(true, mastered, exactCapacity, policy, DomainComplexityPolicy.Default));
            DomainAuthoringEligibilityResult overCapacity = DomainAuthoringEligibility.Evaluate(
                DomainComposition.Empty,
                candidate,
                new DomainAuthoringContext(true, mastered, Mathf.Max(0f, exactCapacity - .01f), policy, DomainComplexityPolicy.Default));
            DomainAuthoringEligibilityResult highCapacityWithoutRunMastery = DomainAuthoringEligibility.Evaluate(
                DomainComposition.Empty,
                candidate,
                new DomainAuthoringContext(true, PlayerMasteryEvidenceState.Empty, 22f, policy, DomainComplexityPolicy.Default));

            MasteryChannelReport before = mastered.GetPhenomenonReport(LawPhenomenon.Heat, policy);
            MasteryChannelReport after = mastered.GetPhenomenonReport(LawPhenomenon.Heat, policy);
            bool valid = exact.IsEligible
                && overCapacity.RejectionReason == DomainAuthoringRejectionReason.ComplexityCapacityExceeded
                && highCapacityWithoutRunMastery.RejectionReason == DomainAuthoringRejectionReason.PhenomenonMasteryIncomplete
                && Mathf.Approximately(before.StateEvidence, after.StateEvidence)
                && Mathf.Approximately(before.RateEvidence, after.RateEvidence)
                && Mathf.Approximately(before.AccelerationEvidence, after.AccelerationEvidence)
                && DomainComplexityCapacityProgression.GetCapacity(DomainComplexityCapacityTier.TierIV, DomainComplexityPolicy.Default) == 22f;
            failure = valid ? null : "Live mastery, exact capacity, or current-run gating regressed.";
            return valid;
        }

        private static bool VerifyPersistenceAndIdentity(out string failure)
        {
            DomainLaw law = CreateLaw(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation);
            string id = Guid.NewGuid().ToString("N");
            List<DomainAuthoredLaw> source = new List<DomainAuthoredLaw> { new DomainAuthoredLaw(id, law) };
            string payload = PlayerDomainLawCollection.SerializeForPersistence(source);
            List<DomainAuthoredLaw> reloaded;
            DomainComposition rebuilt;
            bool restored = PlayerDomainLawCollection.TryDeserializeForPersistence(payload, out reloaded, out rebuilt);

            // Each invalid load uses independent outputs. The valid round-trip
            // must remain available for its exact identity assertion.
            List<DomainAuthoredLaw> emptyIdentity;
            DomainComposition emptyIdentityComposition;
            bool rejectsEmptyIdentity = !PlayerDomainLawCollection.TryDeserializeForPersistence(
                PayloadFor("", law), out emptyIdentity, out emptyIdentityComposition);
            List<DomainAuthoredLaw> malformedIdentity;
            DomainComposition malformedIdentityComposition;
            bool rejectsMalformedIdentity = !PlayerDomainLawCollection.TryDeserializeForPersistence(
                PayloadFor("not-a-guid", law), out malformedIdentity, out malformedIdentityComposition);
            List<DomainAuthoredLaw> duplicateIdentity;
            DomainComposition duplicateIdentityComposition;
            bool rejectsDuplicateIdentity = !PlayerDomainLawCollection.TryDeserializeForPersistence(
                DuplicatePayloadFor(id, law), out duplicateIdentity, out duplicateIdentityComposition);
            List<DomainAuthoredLaw> malformedPayload;
            DomainComposition malformedPayloadComposition;
            bool rejectsMalformedPayload = !PlayerDomainLawCollection.TryDeserializeForPersistence(
                "{ definitely-not-json", out malformedPayload, out malformedPayloadComposition);

            string reloadedId = restored && reloaded.Count == 1 ? reloaded[0].Id : "<load failed>";
            bool valid = restored
                && payload.Contains("\"id\":\"" + id + "\"")
                && reloaded.Count == 1
                && reloadedId == id
                && reloaded[0].Law.Equals(law)
                && rebuilt.Count == 1
                && rejectsEmptyIdentity && emptyIdentity.Count == 0 && emptyIdentityComposition.Count == 0
                && rejectsMalformedIdentity && malformedIdentity.Count == 0 && malformedIdentityComposition.Count == 0
                && rejectsDuplicateIdentity && duplicateIdentity.Count == 0 && duplicateIdentityComposition.Count == 0
                && rejectsMalformedPayload && malformedPayload.Count == 0 && malformedPayloadComposition.Count == 0;
            failure = valid
                ? null
                : "Authoring persistence did not preserve GUID identity or fail closed. "
                    + "original=" + id
                    + "; serialized=" + payload
                    + "; reloaded=" + reloadedId + ".";
            return valid;
        }

        private static string PayloadFor(string id, DomainLaw law)
        {
            return "{\"schemaVersion\":1,\"laws\":[{\"id\":\"" + id
                + "\",\"expression\":" + (int)law.Expression
                + ",\"phenomenon\":" + (int)law.Phenomenon
                + ",\"territory\":" + (int)law.TerritoryPrinciple + "}]}";
        }

        private static string DuplicatePayloadFor(string id, DomainLaw law)
        {
            string entry = "{\"id\":\"" + id
                + "\",\"expression\":" + (int)law.Expression
                + ",\"phenomenon\":" + (int)law.Phenomenon
                + ",\"territory\":" + (int)law.TerritoryPrinciple + "}";
            return "{\"schemaVersion\":1,\"laws\":[" + entry + "," + entry + "]}";
        }

        private static bool VerifyFailedAndDuplicateAuthoringDoNotMutate(out string failure)
        {
            PlayerMasteryPolicy policy = PlayerMasteryPolicy.Default;
            DomainLaw first = CreateLaw(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation);
            DomainComposition oneLaw = DomainCompositionEditor.Add(DomainComposition.Empty, first).Resulting;
            PlayerMasteryEvidenceState mastery = MasteredProjectile(LawPhenomenon.Heat, policy);
            DomainAuthoringEligibilityResult duplicate = DomainAuthoringEligibility.Evaluate(
                oneLaw, first, new DomainAuthoringContext(true, mastery, 22f, policy, DomainComplexityPolicy.Default));
            DomainLaw second = CreateLaw(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Accumulation);
            DomainAuthoringEligibilityResult insufficient = DomainAuthoringEligibility.Evaluate(
                oneLaw, second, new DomainAuthoringContext(true, mastery, 0f, policy, DomainComplexityPolicy.Default));
            bool valid = duplicate.RejectionReason == DomainAuthoringRejectionReason.DuplicateLaw
                && insufficient.RejectionReason == DomainAuthoringRejectionReason.ComplexityCapacityExceeded
                && oneLaw.Count == 1
                && duplicate.Current.Count == 1
                && insufficient.Current.Count == 1;
            failure = valid ? null : "Duplicate or failed authoring altered the authoritative composition.";
            return valid;
        }

        private static bool VerifyReservedExpressionRemainsUnsupported(out string failure)
        {
            DomainLaw ignored;
            LawValidationResult validation;
            bool trapAccepted = DomainLaw.TryCreate(
                (LawExpression)3,
                LawPhenomenon.Heat,
                LawTerritoryPrinciple.Propagation,
                out ignored,
                out validation);
            bool valid = !trapAccepted && !validation.IsValid;
            failure = valid ? null : "Reserved Trap expression became an unsupported authoring path.";
            return valid;
        }

        private static PlayerMasteryEvidenceState MasteredProjectile(LawPhenomenon phenomenon, PlayerMasteryPolicy policy)
        {
            PlayerMasteryEvidenceState state = PlayerMasteryEvidenceState.Empty;
            foreach (MasteryEvidenceDimension dimension in (MasteryEvidenceDimension[])Enum.GetValues(typeof(MasteryEvidenceDimension)))
            {
                state = state.Submit(new PhenomenonMasteryEvidenceSubmission(
                    phenomenon, dimension, 1f, true, LawExpression.Projectile), policy);
            }

            return state;
        }

        private static DomainLaw CreateLaw(
            LawExpression expression,
            LawPhenomenon phenomenon,
            LawTerritoryPrinciple territory)
        {
            DomainLaw law;
            LawValidationResult validation;
            if (!DomainLaw.TryCreate(expression, phenomenon, territory, out law, out validation))
            {
                throw new InvalidOperationException("Verification setup created an invalid Domain Law.");
            }

            return law;
        }
    }
}
