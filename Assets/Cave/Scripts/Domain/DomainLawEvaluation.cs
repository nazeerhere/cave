namespace Cave.Domain
{
    /// <summary>
    /// Immutable runtime evidence supplied by a future bridge. It deliberately
    /// contains no scene object, animation, input, or component reference.
    /// </summary>
    public sealed class LawExpressionContext
    {
        public LawExpressionContext(bool hasExpression, LawExpression expression, bool frenzyActive)
        {
            HasExpression = hasExpression;
            Expression = expression;
            FrenzyActive = frenzyActive;
        }

        public bool HasExpression { get; }
        public LawExpression Expression { get; }
        public bool FrenzyActive { get; }
    }

    /// <summary>Deterministic outcomes for evaluation of one candidate Domain Law.</summary>
    public enum LawEvaluationRejectionReason
    {
        None = 0,
        InvalidLaw = 1,
        ExpressionUnavailable = 2,
        InvalidExpressionContext = 3,
        ExpressionMismatch = 4,
        FrenzyInactive = 5,
        PhenomenonMismatch = 6,
        TerritoryBehaviorNotImplemented = 7,
        NoDefinedInverse = 8,
        InvalidBaseRequest = 9,
        BaseResolutionRejected = 10
    }

    /// <summary>Immutable request to evaluate one Law against one Add or Remove intent.</summary>
    public sealed class LawEvaluationRequest
    {
        public LawEvaluationRequest(
            DomainLaw candidateLaw,
            PhenomenonOperationRequest originalRequest,
            LawExpressionContext expressionContext)
        {
            CandidateLaw = candidateLaw;
            OriginalRequest = originalRequest;
            ExpressionContext = expressionContext;
        }

        public DomainLaw CandidateLaw { get; }
        public PhenomenonOperationRequest OriginalRequest { get; }
        public LawExpressionContext ExpressionContext { get; }
    }

    /// <summary>Immutable request to evaluate one Law against one Transfer intent.</summary>
    public sealed class LawTransferEvaluationRequest
    {
        public LawTransferEvaluationRequest(
            DomainLaw candidateLaw,
            PhenomenonTransferRequest originalRequest,
            LawExpressionContext expressionContext)
        {
            CandidateLaw = candidateLaw;
            OriginalRequest = originalRequest;
            ExpressionContext = expressionContext;
        }

        public DomainLaw CandidateLaw { get; }
        public PhenomenonTransferRequest OriginalRequest { get; }
        public LawExpressionContext ExpressionContext { get; }
    }

    /// <summary>
    /// Immutable, trace-ready result for considering one candidate Law. Eligible
    /// means structural, expression, and phenomenon checks passed; Succeeded
    /// additionally requires a defined Territory transformation and base result.
    /// </summary>
    public sealed class LawEvaluationResult
    {
        internal LawEvaluationResult(
            DomainLaw candidateLaw,
            LawExpressionContext expressionContext,
            PhenomenonOperationKind originalOperation,
            LawPhenomenon originalPhenomenon,
            float originalMagnitude,
            bool eligible,
            bool succeeded,
            LawEvaluationRejectionReason rejectionReason,
            bool hasEffectiveOperation,
            PhenomenonOperationKind effectiveOperation,
            float effectiveMagnitude,
            bool territoryTransformed,
            PhenomenonResolutionResult baseResult)
        {
            CandidateLaw = candidateLaw;
            ExpressionContext = expressionContext;
            OriginalOperation = originalOperation;
            OriginalPhenomenon = originalPhenomenon;
            OriginalMagnitude = originalMagnitude;
            Eligible = eligible;
            Succeeded = succeeded;
            RejectionReason = rejectionReason;
            HasEffectiveOperation = hasEffectiveOperation;
            EffectiveOperation = effectiveOperation;
            EffectiveMagnitude = effectiveMagnitude;
            TerritoryTransformed = territoryTransformed;
            BaseResult = baseResult;
        }

        public DomainLaw CandidateLaw { get; }
        public LawExpressionContext ExpressionContext { get; }
        public LawTerritoryPrinciple? TerritoryPrinciple => CandidateLaw != null
            ? (LawTerritoryPrinciple?)CandidateLaw.TerritoryPrinciple
            : null;
        public PhenomenonOperationKind OriginalOperation { get; }
        public LawPhenomenon OriginalPhenomenon { get; }
        public float OriginalMagnitude { get; }
        public bool Eligible { get; }
        public bool Succeeded { get; }
        public LawEvaluationRejectionReason RejectionReason { get; }
        public bool HasEffectiveOperation { get; }
        public PhenomenonOperationKind EffectiveOperation { get; }
        public float EffectiveMagnitude { get; }
        public bool TerritoryTransformed { get; }
        public PhenomenonResolutionResult BaseResult { get; }
    }

    /// <summary>
    /// Pure single-Law evaluation. It does not establish baseline fallback: an
    /// ineligible Law simply reports why it could not modify the original intent.
    /// </summary>
    public static class DomainLawEvaluator
    {
        public static LawEvaluationResult Evaluate(LawEvaluationRequest request)
        {
            DomainLaw law = request != null ? request.CandidateLaw : null;
            LawExpressionContext context = request != null ? request.ExpressionContext : null;
            PhenomenonOperationRequest original = request != null ? request.OriginalRequest : null;
            PhenomenonOperationKind operation = original != null
                ? original.Operation
                : default(PhenomenonOperationKind);
            LawPhenomenon phenomenon = original != null
                ? original.Phenomenon
                : default(LawPhenomenon);
            float magnitude = original != null ? original.Magnitude : 0f;

            LawEvaluationRejectionReason rejection;
            if (!TryEvaluateEligibility(law, context, out rejection))
            {
                return Rejected(law, context, operation, phenomenon, magnitude, false, rejection);
            }

            if (original == null)
            {
                return Rejected(law, context, operation, phenomenon, magnitude, false,
                    LawEvaluationRejectionReason.InvalidBaseRequest);
            }

            if (law.Phenomenon != phenomenon)
            {
                return Rejected(law, context, operation, phenomenon, magnitude, false,
                    LawEvaluationRejectionReason.PhenomenonMismatch);
            }

            PhenomenonResolutionResult originalValidation = PhenomenonOperationResolver.Resolve(original);
            if (!originalValidation.Succeeded)
            {
                return Rejected(law, context, operation, phenomenon, magnitude, true,
                    LawEvaluationRejectionReason.InvalidBaseRequest);
            }

            if (law.TerritoryPrinciple != LawTerritoryPrinciple.Reversal)
            {
                return Rejected(law, context, operation, phenomenon, magnitude, true,
                    LawEvaluationRejectionReason.TerritoryBehaviorNotImplemented);
            }

            PhenomenonOperationKind effectiveOperation;
            if (!TryGetReversedOperation(operation, out effectiveOperation))
            {
                return Rejected(law, context, operation, phenomenon, magnitude, true,
                    LawEvaluationRejectionReason.NoDefinedInverse);
            }

            PhenomenonOperationRequest effectiveRequest = new PhenomenonOperationRequest(
                effectiveOperation,
                phenomenon,
                magnitude,
                original.Target);
            PhenomenonResolutionResult baseResult = PhenomenonOperationResolver.Resolve(effectiveRequest);
            if (!baseResult.Succeeded)
            {
                return new LawEvaluationResult(
                    law,
                    context,
                    operation,
                    phenomenon,
                    magnitude,
                    true,
                    false,
                    LawEvaluationRejectionReason.BaseResolutionRejected,
                    true,
                    effectiveOperation,
                    magnitude,
                    true,
                    baseResult);
            }

            return new LawEvaluationResult(
                law,
                context,
                operation,
                phenomenon,
                magnitude,
                true,
                true,
                LawEvaluationRejectionReason.None,
                true,
                effectiveOperation,
                magnitude,
                true,
                baseResult);
        }

        public static LawEvaluationResult EvaluateTransfer(LawTransferEvaluationRequest request)
        {
            DomainLaw law = request != null ? request.CandidateLaw : null;
            LawExpressionContext context = request != null ? request.ExpressionContext : null;
            PhenomenonTransferRequest original = request != null ? request.OriginalRequest : null;
            LawPhenomenon phenomenon = original != null
                ? original.Phenomenon
                : default(LawPhenomenon);
            float magnitude = original != null ? original.Magnitude : 0f;

            LawEvaluationRejectionReason rejection;
            if (!TryEvaluateEligibility(law, context, out rejection))
            {
                return Rejected(law, context, PhenomenonOperationKind.Transfer, phenomenon, magnitude, false, rejection);
            }

            if (original == null)
            {
                return Rejected(law, context, PhenomenonOperationKind.Transfer, phenomenon, magnitude, false,
                    LawEvaluationRejectionReason.InvalidBaseRequest);
            }

            if (law.Phenomenon != phenomenon)
            {
                return Rejected(law, context, PhenomenonOperationKind.Transfer, phenomenon, magnitude, false,
                    LawEvaluationRejectionReason.PhenomenonMismatch);
            }

            PhenomenonResolutionResult originalValidation = PhenomenonOperationResolver.ResolveTransfer(original);
            if (!originalValidation.Succeeded)
            {
                return Rejected(law, context, PhenomenonOperationKind.Transfer, phenomenon, magnitude, true,
                    LawEvaluationRejectionReason.InvalidBaseRequest);
            }

            if (law.TerritoryPrinciple != LawTerritoryPrinciple.Reversal)
            {
                return Rejected(law, context, PhenomenonOperationKind.Transfer, phenomenon, magnitude, true,
                    LawEvaluationRejectionReason.TerritoryBehaviorNotImplemented);
            }

            return Rejected(law, context, PhenomenonOperationKind.Transfer, phenomenon, magnitude, true,
                LawEvaluationRejectionReason.NoDefinedInverse);
        }

        internal static bool TryEvaluateEligibility(
            DomainLaw law,
            LawExpressionContext context,
            out LawEvaluationRejectionReason rejection)
        {
            if (law == null)
            {
                rejection = LawEvaluationRejectionReason.InvalidLaw;
                return false;
            }

            LawValidationResult lawValidation = DomainLaw.Validate(
                law.Expression,
                law.Phenomenon,
                law.TerritoryPrinciple);
            if (!lawValidation.IsValid)
            {
                rejection = LawEvaluationRejectionReason.InvalidLaw;
                return false;
            }

            if (context == null || !context.HasExpression)
            {
                rejection = LawEvaluationRejectionReason.ExpressionUnavailable;
                return false;
            }

            if (!IsDefinedExpression(context.Expression))
            {
                rejection = LawEvaluationRejectionReason.InvalidExpressionContext;
                return false;
            }

            if (context.Expression != law.Expression)
            {
                rejection = LawEvaluationRejectionReason.ExpressionMismatch;
                return false;
            }

            if (law.Expression == LawExpression.Frenzy && !context.FrenzyActive)
            {
                rejection = LawEvaluationRejectionReason.FrenzyInactive;
                return false;
            }

            rejection = LawEvaluationRejectionReason.None;
            return true;
        }

        private static bool IsDefinedExpression(LawExpression expression)
        {
            return expression == LawExpression.Projectile || expression == LawExpression.Frenzy;
        }

        /// <summary>Shared pure Reversal transform seam; no base semantic resolution occurs here.</summary>
        internal static bool TryGetReversedOperation(
            PhenomenonOperationKind operation,
            out PhenomenonOperationKind effectiveOperation)
        {
            if (operation == PhenomenonOperationKind.Add)
            {
                effectiveOperation = PhenomenonOperationKind.Remove;
                return true;
            }

            if (operation == PhenomenonOperationKind.Remove)
            {
                effectiveOperation = PhenomenonOperationKind.Add;
                return true;
            }

            effectiveOperation = default(PhenomenonOperationKind);
            return false;
        }

        private static LawEvaluationResult Rejected(
            DomainLaw law,
            LawExpressionContext context,
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float magnitude,
            bool eligible,
            LawEvaluationRejectionReason rejection)
        {
            return new LawEvaluationResult(
                law,
                context,
                operation,
                phenomenon,
                magnitude,
                eligible,
                false,
                rejection,
                false,
                default(PhenomenonOperationKind),
                0f,
                false,
                null);
        }
    }
}
