using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Deterministic bounded queue/flush seam for localized Domain requests.
    /// It evaluates no Territory math and owns no mutation: it merely stages
    /// existing executor proposals into one existing arbitration/commit batch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalizedDomainExecutionCoordinator : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximumRequestsPerFlush = 16;
        private readonly List<LocalizedDomainCarrier> pending = new List<LocalizedDomainCarrier>();
        private readonly LocalizedDomainRuntimeExecutor executor = new LocalizedDomainRuntimeExecutor();
        private ulong nextGeneration;
        private bool flushing;

        public LocalizedDomainExecutionBatchResult LastResult { get; private set; }
        public int PendingRequestCount => pending.Count;

        public static LocalizedDomainExecutionCoordinator EnsureOn(PlayerDomainReserve owner)
        {
            if (owner == null) return null;
            LocalizedDomainExecutionCoordinator coordinator = owner.GetComponent<LocalizedDomainExecutionCoordinator>();
            return coordinator != null ? coordinator : owner.gameObject.AddComponent<LocalizedDomainExecutionCoordinator>();
        }

        /// <summary>Queues an immutable activation descriptor. The next explicit
        /// bounded flush—not a time window—defines its execution generation.</summary>
        public bool Enqueue(LocalizedDomainCarrier carrier)
        {
            if (carrier == null || carrier.BoundLaw == null || carrier.BoundLaw.Law == null) return false;
            for (int index = 0; index < pending.Count; index++) if (ReferenceEquals(pending[index], carrier)) return false;
            pending.Add(carrier);
            return true;
        }

        private void LateUpdate() { Flush(Time.time); }

        /// <summary>
        /// Testable deterministic flush boundary. Timestamp is used solely for
        /// existing carrier-lifetime and runtime-target validation; it never
        /// groups requests by elapsed time.
        /// </summary>
        public LocalizedDomainExecutionBatchResult Flush(float timestamp)
        {
            if (flushing || pending.Count == 0) return LastResult;
            flushing = true;
            try
            {
                int count = Mathf.Min(Mathf.Max(1, maximumRequestsPerFlush), pending.Count);
                List<LocalizedDomainCarrier> drained = pending.GetRange(0, count);
                pending.RemoveRange(0, count);
                drained.Sort((left, right) => string.CompareOrdinal(left != null ? left.Id : null, right != null ? right.Id : null));
                string generationId = "localized-domain:" + gameObject.GetInstanceID() + ":" + (++nextGeneration);
                List<LocalizedDomainPreparedProposal> proposals = new List<LocalizedDomainPreparedProposal>();
                List<LocalizedDomainExecutionRejectionReason> rejected = new List<LocalizedDomainExecutionRejectionReason>();
                for (int index = 0; index < drained.Count; index++)
                {
                    LocalizedDomainPreparationResult preparation = executor.Prepare(drained[index], timestamp, generationId);
                    if (!preparation.Succeeded) { rejected.Add(preparation.RejectionReason); continue; }
                    for (int proposal = 0; proposal < preparation.Proposals.Count; proposal++) proposals.Add(preparation.Proposals[proposal]);
                }

                // Interference has already been prepared only when its ordinary
                // pulse focal has distinct registered semantic contributors.
                // The coordinator merely batches the resulting proposals.
                List<DomainConflictParticipant> participants = new List<DomainConflictParticipant>();
                for (int index = 0; index < proposals.Count; index++) participants.Add(proposals[index].Participant);
                CrossDomainArbitrationResult arbitration = participants.Count > 0 ? DomainConflictArbitrator.Arbitrate(participants) : null;
                ArbitratedDomainCommitPlan plan = arbitration != null ? ArbitratedDomainCommitPlanBuilder.Build(arbitration) : null;
                ArbitratedDomainRuntimeCommitResult commit = plan != null && plan.IsCommittable
                    ? new ArbitratedDomainRuntimeCommitter().Commit(plan, DomainRuntimeCarrierRegistry.CreateAccessor(timestamp)) : null;
                LastResult = new LocalizedDomainExecutionBatchResult(generationId, drained.Count, proposals.AsReadOnly(),
                    rejected.AsReadOnly(), arbitration, plan, commit);
                return LastResult;
            }
            finally { flushing = false; }
        }
    }

    public sealed class LocalizedDomainExecutionBatchResult
    {
        internal LocalizedDomainExecutionBatchResult(string generationId, int requestCount,
            IReadOnlyList<LocalizedDomainPreparedProposal> proposals,
            IReadOnlyList<LocalizedDomainExecutionRejectionReason> rejected,
            CrossDomainArbitrationResult arbitration, ArbitratedDomainCommitPlan plan,
            ArbitratedDomainRuntimeCommitResult commit)
        { GenerationId = generationId; RequestCount = requestCount; Proposals = proposals; Rejections = rejected; Arbitration = arbitration; CommitPlan = plan; Commit = commit; }
        public string GenerationId { get; }
        public int RequestCount { get; }
        public IReadOnlyList<LocalizedDomainPreparedProposal> Proposals { get; }
        public IReadOnlyList<LocalizedDomainExecutionRejectionReason> Rejections { get; }
        public CrossDomainArbitrationResult Arbitration { get; }
        public ArbitratedDomainCommitPlan CommitPlan { get; }
        public ArbitratedDomainRuntimeCommitResult Commit { get; }
        public bool Succeeded => Commit != null && Commit.Succeeded;
    }
}
