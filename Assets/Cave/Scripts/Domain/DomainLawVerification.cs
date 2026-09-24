using System;

namespace Cave.Domain
{
    /// <summary>
    /// Runtime-independent deterministic coverage for the Domain Law schema.
    /// It avoids a test-framework dependency so an editor command, CI wrapper,
    /// or temporary development call can execute the same checks.
    /// </summary>
    public static class DomainLawVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return VerifyExpressionVocabulary(out failure)
                && VerifyPhenomenonVocabulary(out failure)
                && VerifyTerritoryPrincipleVocabulary(out failure)
                && VerifyValidLaws(out failure)
                && VerifyInvalidValues(out failure)
                && VerifyValueSemantics(out failure);
        }

        private static bool VerifyExpressionVocabulary(out string failure)
        {
            Array values = Enum.GetValues(typeof(LawExpression));
            bool recognized = values.Length == 2
                && Enum.IsDefined(typeof(LawExpression), LawExpression.Projectile)
                && Enum.IsDefined(typeof(LawExpression), LawExpression.Frenzy);
            return Expect(recognized && !Contains(Enum.GetNames(typeof(LawExpression)), "Hybrid"),
                "Authoritative Law expressions did not contain exactly Projectile and Frenzy.", out failure);
        }

        private static bool VerifyPhenomenonVocabulary(out string failure)
        {
            Array values = Enum.GetValues(typeof(LawPhenomenon));
            bool recognized = values.Length == 8
                && Enum.IsDefined(typeof(LawPhenomenon), LawPhenomenon.Heat)
                && Enum.IsDefined(typeof(LawPhenomenon), LawPhenomenon.Flow)
                && Enum.IsDefined(typeof(LawPhenomenon), LawPhenomenon.Mass)
                && Enum.IsDefined(typeof(LawPhenomenon), LawPhenomenon.Compression)
                && Enum.IsDefined(typeof(LawPhenomenon), LawPhenomenon.Potential)
                && Enum.IsDefined(typeof(LawPhenomenon), LawPhenomenon.Resonance)
                && Enum.IsDefined(typeof(LawPhenomenon), LawPhenomenon.Phase)
                && Enum.IsDefined(typeof(LawPhenomenon), LawPhenomenon.Order);
            return Expect(recognized && !Contains(Enum.GetNames(typeof(LawPhenomenon)), "StoneglassPrecision"),
                "Authoritative Law phenomena did not contain exactly the locked ordinary set.", out failure);
        }

        private static bool VerifyTerritoryPrincipleVocabulary(out string failure)
        {
            Array values = Enum.GetValues(typeof(LawTerritoryPrinciple));
            bool recognized = values.Length == 6
                && Enum.IsDefined(typeof(LawTerritoryPrinciple), LawTerritoryPrinciple.Accumulation)
                && Enum.IsDefined(typeof(LawTerritoryPrinciple), LawTerritoryPrinciple.Propagation)
                && Enum.IsDefined(typeof(LawTerritoryPrinciple), LawTerritoryPrinciple.Synchronization)
                && Enum.IsDefined(typeof(LawTerritoryPrinciple), LawTerritoryPrinciple.Catalysis)
                && Enum.IsDefined(typeof(LawTerritoryPrinciple), LawTerritoryPrinciple.Interference)
                && Enum.IsDefined(typeof(LawTerritoryPrinciple), LawTerritoryPrinciple.Reversal);
            string[] names = Enum.GetNames(typeof(LawTerritoryPrinciple));
            return Expect(recognized
                && !Contains(names, "Localization")
                && !Contains(names, "Conversion")
                && !Contains(names, "Gradient"),
                "Authoritative Law territory principles did not contain exactly the locked active set.", out failure);
        }

        private static bool VerifyValidLaws(out string failure)
        {
            DomainLaw projectileHeat;
            LawValidationResult projectileHeatValidation;
            DomainLaw frenzyPotential;
            LawValidationResult frenzyPotentialValidation;
            bool projectileCreated = DomainLaw.TryCreate(
                LawExpression.Projectile,
                LawPhenomenon.Heat,
                LawTerritoryPrinciple.Propagation,
                out projectileHeat,
                out projectileHeatValidation);
            bool frenzyCreated = DomainLaw.TryCreate(
                LawExpression.Frenzy,
                LawPhenomenon.Potential,
                LawTerritoryPrinciple.Reversal,
                out frenzyPotential,
                out frenzyPotentialValidation);
            return Expect(projectileCreated
                && projectileHeatValidation.IsValid
                && projectileHeat != null
                && frenzyCreated
                && frenzyPotentialValidation.IsValid
                && frenzyPotential != null,
                "Locked valid Law triples did not construct successfully.", out failure);
        }

        private static bool VerifyInvalidValues(out string failure)
        {
            LawValidationResult expression = DomainLaw.Validate(
                (LawExpression)999,
                LawPhenomenon.Heat,
                LawTerritoryPrinciple.Accumulation);
            LawValidationResult phenomenon = DomainLaw.Validate(
                LawExpression.Projectile,
                (LawPhenomenon)999,
                LawTerritoryPrinciple.Accumulation);
            LawValidationResult territory = DomainLaw.Validate(
                LawExpression.Projectile,
                LawPhenomenon.Heat,
                (LawTerritoryPrinciple)999);
            DomainLaw invalidLaw;
            LawValidationResult invalidCreation;
            bool created = DomainLaw.TryCreate(
                (LawExpression)999,
                LawPhenomenon.Heat,
                LawTerritoryPrinciple.Accumulation,
                out invalidLaw,
                out invalidCreation);

            return Expect(!expression.IsValid
                && expression.RejectionReason == LawValidationRejectionReason.InvalidExpression
                && !phenomenon.IsValid
                && phenomenon.RejectionReason == LawValidationRejectionReason.InvalidPhenomenon
                && !territory.IsValid
                && territory.RejectionReason == LawValidationRejectionReason.InvalidTerritoryPrinciple
                && !created
                && invalidLaw == null
                && invalidCreation.RejectionReason == LawValidationRejectionReason.InvalidExpression,
                "Invalid Law enum values were not rejected deterministically.", out failure);
        }

        private static bool VerifyValueSemantics(out string failure)
        {
            DomainLaw first;
            LawValidationResult firstValidation;
            DomainLaw equal;
            LawValidationResult equalValidation;
            DomainLaw different;
            LawValidationResult differentValidation;
            DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat,
                LawTerritoryPrinciple.Propagation, out first, out firstValidation);
            DomainLaw.TryCreate(LawExpression.Projectile, LawPhenomenon.Heat,
                LawTerritoryPrinciple.Propagation, out equal, out equalValidation);
            DomainLaw.TryCreate(LawExpression.Frenzy, LawPhenomenon.Heat,
                LawTerritoryPrinciple.Propagation, out different, out differentValidation);

            return Expect(firstValidation.IsValid
                && equalValidation.IsValid
                && differentValidation.IsValid
                && first.Equals(equal)
                && first.GetHashCode() == equal.GetHashCode()
                && !first.Equals(different)
                && first.Expression == LawExpression.Projectile
                && first.Phenomenon == LawPhenomenon.Heat
                && first.TerritoryPrinciple == LawTerritoryPrinciple.Propagation,
                "Domain Law equality, hash, or immutable value access was incorrect.", out failure);
        }

        private static bool Expect(bool condition, string message, out string failure)
        {
            failure = condition ? null : message;
            return condition;
        }

        private static bool Contains(string[] values, string expected)
        {
            return Array.IndexOf(values, expected) >= 0;
        }
    }
}
