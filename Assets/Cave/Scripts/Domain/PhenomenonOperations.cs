using System;

namespace Cave.Domain
{
    /// <summary>The intentionally small executable operation vocabulary for base semantic state.</summary>
    public enum PhenomenonOperationKind
    {
        Add = 1,
        Remove = 2,
        Transfer = 3
    }

    /// <summary>Deterministic reasons a pure phenomenon operation can be rejected.</summary>
    public enum PhenomenonResolutionRejectionReason
    {
        None = 0,
        InvalidOperation = 1,
        InvalidPhenomenon = 2,
        NegativeMagnitude = 3,
        InvalidMagnitude = 4,
        SourceStateUnavailable = 5,
        TargetStateUnavailable = 6,
        PhenomenonMismatch = 7,
        InvalidSemanticValue = 8,
        InvalidSemanticState = 9
    }

    /// <summary>
    /// Immutable request for a one-target Add or Remove operation. Transfer has
    /// its own request type because it requires two explicit state references.
    /// </summary>
    public sealed class PhenomenonOperationRequest
    {
        public PhenomenonOperationRequest(
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float magnitude,
            PhenomenonSemanticSnapshot target)
        {
            Operation = operation;
            Phenomenon = phenomenon;
            Magnitude = magnitude;
            Target = target;
        }

        public PhenomenonOperationKind Operation { get; }
        public LawPhenomenon Phenomenon { get; }
        public float Magnitude { get; }
        public PhenomenonSemanticSnapshot Target { get; }
    }

    /// <summary>Immutable request for one same-phenomenon conserved transfer.</summary>
    public sealed class PhenomenonTransferRequest
    {
        public PhenomenonTransferRequest(
            LawPhenomenon phenomenon,
            float magnitude,
            PhenomenonSemanticSnapshot source,
            PhenomenonSemanticSnapshot target)
        {
            Phenomenon = phenomenon;
            Magnitude = magnitude;
            Source = source;
            Target = target;
        }

        public PhenomenonOperationKind Operation => PhenomenonOperationKind.Transfer;
        public LawPhenomenon Phenomenon { get; }
        public float Magnitude { get; }
        public PhenomenonSemanticSnapshot Source { get; }
        public PhenomenonSemanticSnapshot Target { get; }
    }

    /// <summary>Immutable proposed scalar delta; it does not commit to a runtime source.</summary>
    public sealed class PhenomenonStateDelta
    {
        internal PhenomenonStateDelta(LawPhenomenon phenomenon, float amount)
        {
            Phenomenon = phenomenon;
            Amount = amount;
        }

        public LawPhenomenon Phenomenon { get; }
        public float Amount { get; }
    }

    /// <summary>
    /// Immutable inspection result for a pure base operation. Snapshots and
    /// deltas describe a proposal only; no runtime object is ever changed here.
    /// </summary>
    public sealed class PhenomenonResolutionResult
    {
        internal PhenomenonResolutionResult(
            bool succeeded,
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float magnitude,
            PhenomenonResolutionRejectionReason rejectionReason,
            PhenomenonSemanticSnapshot sourceBefore,
            PhenomenonStateDelta sourceDelta,
            PhenomenonSemanticSnapshot sourceAfter,
            PhenomenonSemanticSnapshot targetBefore,
            PhenomenonStateDelta targetDelta,
            PhenomenonSemanticSnapshot targetAfter)
        {
            Succeeded = succeeded;
            Operation = operation;
            Phenomenon = phenomenon;
            Magnitude = magnitude;
            RejectionReason = rejectionReason;
            SourceBefore = sourceBefore;
            SourceDelta = sourceDelta;
            SourceAfter = sourceAfter;
            TargetBefore = targetBefore;
            TargetDelta = targetDelta;
            TargetAfter = targetAfter;
        }

        public bool Succeeded { get; }
        public PhenomenonOperationKind Operation { get; }
        public LawPhenomenon Phenomenon { get; }
        public float Magnitude { get; }
        public PhenomenonResolutionRejectionReason RejectionReason { get; }
        public PhenomenonSemanticSnapshot SourceBefore { get; }
        public PhenomenonStateDelta SourceDelta { get; }
        public PhenomenonSemanticSnapshot SourceAfter { get; }
        public PhenomenonSemanticSnapshot TargetBefore { get; }
        public PhenomenonStateDelta TargetDelta { get; }
        public PhenomenonSemanticSnapshot TargetAfter { get; }
        public bool SourceRegionChanged => SourceBefore != null
            && SourceAfter != null
            && SourceBefore.Region != SourceAfter.Region;
        public bool TargetRegionChanged => TargetBefore != null
            && TargetAfter != null
            && TargetBefore.Region != TargetAfter.Region;
    }

    /// <summary>
    /// Pure base arithmetic beneath future Law evaluation and conflict handling.
    /// Zero magnitude is intentionally a successful explicit zero-delta result;
    /// negative or non-finite magnitude is always rejected.
    /// </summary>
    public static class PhenomenonOperationResolver
    {
        public static PhenomenonResolutionResult Resolve(PhenomenonOperationRequest request)
        {
            if (request == null)
            {
                return Rejected(
                    default(PhenomenonOperationKind),
                    default(LawPhenomenon),
                    0f,
                    PhenomenonResolutionRejectionReason.InvalidOperation,
                    null,
                    null);
            }

            if (request.Operation != PhenomenonOperationKind.Add
                && request.Operation != PhenomenonOperationKind.Remove)
            {
                return Rejected(
                    request.Operation,
                    request.Phenomenon,
                    request.Magnitude,
                    PhenomenonResolutionRejectionReason.InvalidOperation,
                    null,
                    request.Target);
            }

            PhenomenonResolutionRejectionReason rejection;
            if (!ValidatePhenomenon(request.Phenomenon, out rejection))
            {
                return Rejected(request.Operation, request.Phenomenon, request.Magnitude, rejection, null, request.Target);
            }

            if (!ValidateMagnitude(request.Magnitude, out rejection))
            {
                return Rejected(request.Operation, request.Phenomenon, request.Magnitude, rejection, null, request.Target);
            }

            if (!ValidateSnapshot(request.Target, request.Phenomenon, false, out rejection))
            {
                return Rejected(request.Operation, request.Phenomenon, request.Magnitude, rejection, null, request.Target);
            }

            float deltaAmount = request.Operation == PhenomenonOperationKind.Add
                ? request.Magnitude
                : -request.Magnitude;
            PhenomenonSemanticSnapshot targetAfter;
            if (!TryCreateAfter(request.Phenomenon, request.Target.SemanticValue + deltaAmount, out targetAfter, out rejection))
            {
                return Rejected(request.Operation, request.Phenomenon, request.Magnitude, rejection, null, request.Target);
            }

            return new PhenomenonResolutionResult(
                true,
                request.Operation,
                request.Phenomenon,
                request.Magnitude,
                PhenomenonResolutionRejectionReason.None,
                null,
                null,
                null,
                request.Target,
                new PhenomenonStateDelta(request.Phenomenon, deltaAmount),
                targetAfter);
        }

        public static PhenomenonResolutionResult ResolveTransfer(PhenomenonTransferRequest request)
        {
            if (request == null)
            {
                return Rejected(
                    PhenomenonOperationKind.Transfer,
                    default(LawPhenomenon),
                    0f,
                    PhenomenonResolutionRejectionReason.InvalidOperation,
                    null,
                    null);
            }

            PhenomenonResolutionRejectionReason rejection;
            if (!ValidatePhenomenon(request.Phenomenon, out rejection))
            {
                return Rejected(request.Operation, request.Phenomenon, request.Magnitude, rejection, request.Source, request.Target);
            }

            if (!ValidateMagnitude(request.Magnitude, out rejection))
            {
                return Rejected(request.Operation, request.Phenomenon, request.Magnitude, rejection, request.Source, request.Target);
            }

            if (!ValidateSnapshot(request.Source, request.Phenomenon, true, out rejection)
                || !ValidateSnapshot(request.Target, request.Phenomenon, false, out rejection))
            {
                return Rejected(request.Operation, request.Phenomenon, request.Magnitude, rejection, request.Source, request.Target);
            }

            PhenomenonSemanticSnapshot sourceAfter;
            if (!TryCreateAfter(request.Phenomenon, request.Source.SemanticValue - request.Magnitude, out sourceAfter, out rejection))
            {
                return Rejected(request.Operation, request.Phenomenon, request.Magnitude, rejection, request.Source, request.Target);
            }

            PhenomenonSemanticSnapshot targetAfter;
            if (!TryCreateAfter(request.Phenomenon, request.Target.SemanticValue + request.Magnitude, out targetAfter, out rejection))
            {
                return Rejected(request.Operation, request.Phenomenon, request.Magnitude, rejection, request.Source, request.Target);
            }

            return new PhenomenonResolutionResult(
                true,
                request.Operation,
                request.Phenomenon,
                request.Magnitude,
                PhenomenonResolutionRejectionReason.None,
                request.Source,
                new PhenomenonStateDelta(request.Phenomenon, -request.Magnitude),
                sourceAfter,
                request.Target,
                new PhenomenonStateDelta(request.Phenomenon, request.Magnitude),
                targetAfter);
        }

        private static bool ValidatePhenomenon(
            LawPhenomenon phenomenon,
            out PhenomenonResolutionRejectionReason rejection)
        {
            PhenomenonSemanticProfile ignored;
            if (!PhenomenonSemanticClassifier.TryGetProfile(phenomenon, out ignored))
            {
                rejection = PhenomenonResolutionRejectionReason.InvalidPhenomenon;
                return false;
            }

            rejection = PhenomenonResolutionRejectionReason.None;
            return true;
        }

        private static bool ValidateMagnitude(
            float magnitude,
            out PhenomenonResolutionRejectionReason rejection)
        {
            if (float.IsNaN(magnitude) || float.IsInfinity(magnitude))
            {
                rejection = PhenomenonResolutionRejectionReason.InvalidMagnitude;
                return false;
            }

            if (magnitude < 0f)
            {
                rejection = PhenomenonResolutionRejectionReason.NegativeMagnitude;
                return false;
            }

            rejection = PhenomenonResolutionRejectionReason.None;
            return true;
        }

        private static bool ValidateSnapshot(
            PhenomenonSemanticSnapshot snapshot,
            LawPhenomenon expectedPhenomenon,
            bool source,
            out PhenomenonResolutionRejectionReason rejection)
        {
            if (snapshot == null)
            {
                rejection = source
                    ? PhenomenonResolutionRejectionReason.SourceStateUnavailable
                    : PhenomenonResolutionRejectionReason.TargetStateUnavailable;
                return false;
            }

            if (snapshot.Phenomenon != expectedPhenomenon)
            {
                rejection = PhenomenonResolutionRejectionReason.PhenomenonMismatch;
                return false;
            }

            PhenomenonSemanticRegion derivedRegion;
            PhenomenonSemanticResult semanticResult;
            if (!PhenomenonSemanticClassifier.TryClassify(
                snapshot.Phenomenon,
                snapshot.SemanticValue,
                out derivedRegion,
                out semanticResult)
                || !semanticResult.IsValid)
            {
                rejection = PhenomenonResolutionRejectionReason.InvalidSemanticValue;
                return false;
            }

            if (derivedRegion != snapshot.Region)
            {
                rejection = PhenomenonResolutionRejectionReason.InvalidSemanticState;
                return false;
            }

            rejection = PhenomenonResolutionRejectionReason.None;
            return true;
        }

        private static bool TryCreateAfter(
            LawPhenomenon phenomenon,
            float value,
            out PhenomenonSemanticSnapshot snapshot,
            out PhenomenonResolutionRejectionReason rejection)
        {
            PhenomenonSemanticResult semanticResult;
            if (!PhenomenonSemanticClassifier.TryCreateSnapshot(phenomenon, value, out snapshot, out semanticResult))
            {
                rejection = PhenomenonResolutionRejectionReason.InvalidSemanticValue;
                return false;
            }

            rejection = PhenomenonResolutionRejectionReason.None;
            return true;
        }

        private static PhenomenonResolutionResult Rejected(
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float magnitude,
            PhenomenonResolutionRejectionReason rejection,
            PhenomenonSemanticSnapshot source,
            PhenomenonSemanticSnapshot target)
        {
            return new PhenomenonResolutionResult(
                false,
                operation,
                phenomenon,
                magnitude,
                rejection,
                source,
                null,
                null,
                target,
                null,
                null);
        }
    }
}
