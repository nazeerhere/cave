using System.Collections.Generic;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Focused deterministic coverage for the Sprint 3 information core.</summary>
    public static class KnowledgeArchitectureVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return VerifyIsolation(out failure)
                && VerifyPerceptionAndSharing(out failure)
                && VerifyOneHopAndFormationBoundary(out failure)
                && VerifyFreshnessAndConfidence(out failure)
                && VerifyCommandSeparation(out failure);
        }

        private static bool VerifyIsolation(out string failure)
        {
            KnowledgeStore observer = new KnowledgeStore("observer");
            KnowledgeStore other = new KnowledgeStore("other");
            observer.Observe(KnowledgeFactType.PlayerLocation, KnowledgeSubject.Player, new Vector2(2f, 3f), 0f);
            bool valid = observer.CreateSnapshot(0f).Has(KnowledgeFactType.PlayerLocation)
                && !other.CreateSnapshot(0f).Has(KnowledgeFactType.PlayerLocation);
            failure = valid ? null : "Observed fact leaked into an unrelated actor.";
            return valid;
        }

        private static bool VerifyPerceptionAndSharing(out string failure)
        {
            KnowledgeStore observer = new KnowledgeStore("eye");
            KnowledgeStore recipient = new KnowledgeStore("bandit");
            KnowledgeFact fact = observer.Observe(
                KnowledgeFactType.PlayerLowStamina,
                KnowledgeSubject.Player,
                default,
                2f,
                0.8f);
            int delivered = KnowledgeSharing.ShareOneHop(
                fact,
                new List<KnowledgeStore> { recipient },
                2.1f);
            KnowledgeSnapshot snapshot = recipient.CreateSnapshot(2.1f);
            KnowledgeFact received = snapshot.Facts[0];
            bool valid = delivered == 1
                && received.Channel == KnowledgeChannel.Shared
                && received.OriginalObserverId == "eye"
                && received.ProvenanceId == fact.ProvenanceId
                && Mathf.Approximately(received.Confidence, 0.8f);
            failure = valid ? null : "Shared fact did not preserve channel, provenance, or confidence.";
            return valid;
        }

        private static bool VerifyOneHopAndFormationBoundary(out string failure)
        {
            KnowledgeStore observer = new KnowledgeStore("observer");
            KnowledgeStore formationARecipient = new KnowledgeStore("formation-a");
            KnowledgeStore formationBRecipient = new KnowledgeStore("formation-b");
            KnowledgeFact fact = observer.Observe(
                KnowledgeFactType.PlayerRetreating,
                KnowledgeSubject.Player,
                default,
                3f);
            KnowledgeSharing.ShareOneHop(fact, new List<KnowledgeStore> { formationARecipient }, 3.1f);
            KnowledgeFact shared = formationARecipient.CreateSnapshot(3.1f).Facts[0];
            int recursiveDelivery = KnowledgeSharing.ShareOneHop(
                shared,
                new List<KnowledgeStore> { formationBRecipient },
                3.2f);
            bool valid = recursiveDelivery == 0
                && !formationBRecipient.CreateSnapshot(3.2f).Has(KnowledgeFactType.PlayerRetreating);
            failure = valid ? null : "Shared knowledge relayed beyond one formation hop.";
            return valid;
        }

        private static bool VerifyFreshnessAndConfidence(out string failure)
        {
            KnowledgeStore observer = new KnowledgeStore("observer");
            KnowledgeFact fact = observer.Observe(
                KnowledgeFactType.PlayerGuarding,
                KnowledgeSubject.Player,
                default,
                5f,
                0.65f);
            KnowledgeSnapshot fresh = observer.CreateSnapshot(5.1f);
            bool freshValid = fresh.Count == 1
                && fresh.Facts[0].FreshnessAt(5.1f) == KnowledgeFreshness.Fresh
                && Mathf.Approximately(fresh.Facts[0].Confidence, 0.65f);
            bool expired = observer.CreateSnapshot(5f + KnowledgeFactPolicy.LifetimeFor(KnowledgeFactType.PlayerGuarding) + 0.01f).Count == 0;
            failure = freshValid && expired ? null : "Freshness or confidence policy was not preserved.";
            return failure == null;
        }

        private static bool VerifyCommandSeparation(out string failure)
        {
            KnowledgeStore member = new KnowledgeStore("member");
            KnowledgeFact command = member.ReceiveCommand(CombatTacticalIntent.Pressure, "ctc", 7f);
            bool valid = command.Type == KnowledgeFactType.TacticalIntent
                && command.Channel == KnowledgeChannel.Command
                && command.TacticalIntent == CombatTacticalIntent.Pressure
                && !command.CanShareOnce;
            failure = valid ? null : "CTC command was represented as shareable factual knowledge.";
            return valid;
        }
    }
}
