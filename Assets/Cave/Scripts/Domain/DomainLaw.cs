using System;

namespace Cave.Domain
{
    /// <summary>
    /// Authoritative expression vocabulary for a single Domain Law. This is
    /// intentionally separate from the serialized legacy <see cref="PowerExpression"/>
    /// placeholder vocabulary.
    /// </summary>
    public enum LawExpression
    {
        Projectile = 1,
        Frenzy = 2
    }

    /// <summary>
    /// Authoritative ordinary-phenomenon vocabulary for a single Domain Law.
    /// Claim remains a specialized authority system and is deliberately absent.
    /// </summary>
    public enum LawPhenomenon
    {
        Heat = 1,
        Flow = 2,
        Mass = 3,
        Compression = 4,
        Potential = 5,
        Resonance = 6,
        Phase = 7,
        Order = 8
    }

    /// <summary>
    /// Authoritative territory-principle vocabulary for a single Domain Law.
    /// This is intentionally separate from the serialized legacy
    /// <see cref="TerritoryPrinciple"/> placeholder vocabulary.
    /// </summary>
    public enum LawTerritoryPrinciple
    {
        Accumulation = 1,
        Propagation = 2,
        Synchronization = 3,
        Catalysis = 4,
        Interference = 5,
        Reversal = 6
    }

    /// <summary>Structural rejection reasons for <see cref="DomainLaw"/> creation.</summary>
    public enum LawValidationRejectionReason
    {
        None = 0,
        InvalidExpression = 1,
        InvalidPhenomenon = 2,
        InvalidTerritoryPrinciple = 3
    }

    /// <summary>
    /// Immutable result of structural Law validation. This deliberately says
    /// nothing about a player's mastery, seed, reserve, or runtime eligibility.
    /// </summary>
    public struct LawValidationResult
    {
        private readonly LawValidationRejectionReason rejectionReason;

        private LawValidationResult(LawValidationRejectionReason reason)
        {
            rejectionReason = reason;
        }

        public bool IsValid => rejectionReason == LawValidationRejectionReason.None;
        public LawValidationRejectionReason RejectionReason => rejectionReason;

        public static LawValidationResult Valid()
        {
            return new LawValidationResult(LawValidationRejectionReason.None);
        }

        public static LawValidationResult Rejected(LawValidationRejectionReason reason)
        {
            return new LawValidationResult(reason);
        }
    }

    /// <summary>
    /// Immutable structural declaration of one Domain Law: exactly one
    /// expression, one ordinary phenomenon, and one territory principle.
    /// It performs no gameplay resolution or scene access.
    /// </summary>
    public sealed class DomainLaw : IEquatable<DomainLaw>
    {
        private readonly LawExpression expression;
        private readonly LawPhenomenon phenomenon;
        private readonly LawTerritoryPrinciple territoryPrinciple;

        private DomainLaw(
            LawExpression expression,
            LawPhenomenon phenomenon,
            LawTerritoryPrinciple territoryPrinciple)
        {
            this.expression = expression;
            this.phenomenon = phenomenon;
            this.territoryPrinciple = territoryPrinciple;
        }

        public LawExpression Expression => expression;
        public LawPhenomenon Phenomenon => phenomenon;
        public LawTerritoryPrinciple TerritoryPrinciple => territoryPrinciple;

        /// <summary>
        /// Validates the structural triple without evaluating any player or
        /// runtime conditions.
        /// </summary>
        public static LawValidationResult Validate(
            LawExpression expression,
            LawPhenomenon phenomenon,
            LawTerritoryPrinciple territoryPrinciple)
        {
            if (!Enum.IsDefined(typeof(LawExpression), expression))
            {
                return LawValidationResult.Rejected(LawValidationRejectionReason.InvalidExpression);
            }

            if (!Enum.IsDefined(typeof(LawPhenomenon), phenomenon))
            {
                return LawValidationResult.Rejected(LawValidationRejectionReason.InvalidPhenomenon);
            }

            if (!Enum.IsDefined(typeof(LawTerritoryPrinciple), territoryPrinciple))
            {
                return LawValidationResult.Rejected(LawValidationRejectionReason.InvalidTerritoryPrinciple);
            }

            return LawValidationResult.Valid();
        }

        /// <summary>
        /// Creates a structurally valid immutable Law. Invalid enum values
        /// produce no Law and return a deterministic rejection result.
        /// </summary>
        public static bool TryCreate(
            LawExpression expression,
            LawPhenomenon phenomenon,
            LawTerritoryPrinciple territoryPrinciple,
            out DomainLaw law,
            out LawValidationResult validation)
        {
            validation = Validate(expression, phenomenon, territoryPrinciple);
            if (!validation.IsValid)
            {
                law = null;
                return false;
            }

            law = new DomainLaw(expression, phenomenon, territoryPrinciple);
            return true;
        }

        public bool Equals(DomainLaw other)
        {
            return other != null
                && expression == other.expression
                && phenomenon == other.phenomenon
                && territoryPrinciple == other.territoryPrinciple;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DomainLaw);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)expression;
                hash = (hash * 397) ^ (int)phenomenon;
                hash = (hash * 397) ^ (int)territoryPrinciple;
                return hash;
            }
        }

        public override string ToString()
        {
            return expression + " + " + phenomenon + " + " + territoryPrinciple;
        }
    }
}
