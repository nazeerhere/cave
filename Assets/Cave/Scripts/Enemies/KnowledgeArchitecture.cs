using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cave.Enemies
{
    public enum KnowledgeFactType
    {
        None = 0,
        PlayerLocation = 1,
        PlayerRetreating = 2,
        PlayerLowStamina = 3,
        PlayerGuarding = 4,
        PlayerPressuringAlly = 5,
        TacticalIntent = 6
    }

    public enum KnowledgeSubject
    {
        None = 0,
        Player = 1,
        Self = 2,
        Formation = 3
    }

    public enum KnowledgeChannel
    {
        Self = 0,
        Perceived = 1,
        Shared = 2,
        Command = 3,
        Public = 4
    }

    public enum KnowledgeFreshness
    {
        Fresh = 0,
        Aging = 1,
        Stale = 2,
        Expired = 3
    }

    /// <summary>
    /// Compact typed observation record. Payload remains bounded to the facts
    /// Cave currently proves in gameplay; it is not a debug-string blackboard.
    /// </summary>
    public readonly struct KnowledgeFact
    {
        public KnowledgeFact(
            KnowledgeFactType type,
            KnowledgeSubject subject,
            Vector2 position,
            CombatTacticalIntent tacticalIntent,
            KnowledgeChannel channel,
            string originalObserverId,
            long provenanceId,
            float observedAt,
            float receivedAt,
            float confidence,
            float expiresAt)
        {
            Type = type;
            Subject = subject;
            Position = position;
            TacticalIntent = tacticalIntent;
            Channel = channel;
            OriginalObserverId = originalObserverId;
            ProvenanceId = provenanceId;
            ObservedAt = observedAt;
            ReceivedAt = receivedAt;
            Confidence = Mathf.Clamp01(confidence);
            ExpiresAt = expiresAt;
        }

        public KnowledgeFactType Type { get; }
        public KnowledgeSubject Subject { get; }
        public Vector2 Position { get; }
        public CombatTacticalIntent TacticalIntent { get; }
        public KnowledgeChannel Channel { get; }
        public string OriginalObserverId { get; }
        public long ProvenanceId { get; }
        public float ObservedAt { get; }
        public float ReceivedAt { get; }
        public float Confidence { get; }
        public float ExpiresAt { get; }

        public bool IsExpired(float now) => now >= ExpiresAt;

        public KnowledgeFreshness FreshnessAt(float now)
        {
            if (IsExpired(now))
            {
                return KnowledgeFreshness.Expired;
            }

            float lifetime = Mathf.Max(0.001f, ExpiresAt - ObservedAt);
            float ageFraction = Mathf.Clamp01((now - ObservedAt) / lifetime);
            return ageFraction < 0.33f
                ? KnowledgeFreshness.Fresh
                : ageFraction < 0.75f
                    ? KnowledgeFreshness.Aging
                    : KnowledgeFreshness.Stale;
        }

        public bool CanShareOnce => Channel == KnowledgeChannel.Self
            || Channel == KnowledgeChannel.Perceived;
    }

    public readonly struct KnowledgeDiagnostic
    {
        public KnowledgeDiagnostic(KnowledgeFact fact, float now)
        {
            Fact = fact;
            Age = Mathf.Max(0f, now - fact.ObservedAt);
            Freshness = fact.FreshnessAt(now);
        }

        public KnowledgeFact Fact { get; }
        public float Age { get; }
        public KnowledgeFreshness Freshness { get; }
    }

    /// <summary>Read-only decision input. It deliberately has no world query API.</summary>
    public readonly struct KnowledgeSnapshot
    {
        private readonly IReadOnlyList<KnowledgeFact> facts;

        internal KnowledgeSnapshot(string actorId, IReadOnlyList<KnowledgeFact> facts)
        {
            ActorId = actorId;
            this.facts = facts ?? Array.Empty<KnowledgeFact>();
        }

        public string ActorId { get; }
        public IReadOnlyList<KnowledgeFact> Facts => facts ?? Array.Empty<KnowledgeFact>();
        public int Count => Facts.Count;

        public bool Has(KnowledgeFactType type)
        {
            for (int index = 0; index < Facts.Count; index++)
            {
                if (Facts[index].Type == type)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class KnowledgeFactPolicy
    {
        public static float LifetimeFor(KnowledgeFactType type)
        {
            switch (type)
            {
                case KnowledgeFactType.PlayerLocation:
                    return 1.5f;
                case KnowledgeFactType.PlayerRetreating:
                    return 1.1f;
                case KnowledgeFactType.PlayerLowStamina:
                    return 1.25f;
                case KnowledgeFactType.PlayerGuarding:
                    return 0.6f;
                case KnowledgeFactType.PlayerPressuringAlly:
                    return 0.8f;
                case KnowledgeFactType.TacticalIntent:
                    return 0.5f;
                default:
                    return 1f;
            }
        }
    }

    /// <summary>
    /// Per-actor authoritative storage. It stores only legitimately received
    /// facts and removes expired entries before every consumer-facing snapshot.
    /// </summary>
    public sealed class KnowledgeStore
    {
        private readonly string actorId;
        private readonly List<KnowledgeFact> facts = new List<KnowledgeFact>();
        private long nextProvenanceId = 1;

        public KnowledgeStore(string actorId)
        {
            this.actorId = actorId;
        }

        public string ActorId => actorId;

        public KnowledgeFact Observe(
            KnowledgeFactType type,
            KnowledgeSubject subject,
            Vector2 position,
            float now,
            float confidence = 1f,
            KnowledgeChannel channel = KnowledgeChannel.Perceived)
        {
            if (string.IsNullOrEmpty(actorId)
                || type == KnowledgeFactType.None
                || (channel != KnowledgeChannel.Self && channel != KnowledgeChannel.Perceived
                    && channel != KnowledgeChannel.Public))
            {
                return default;
            }

            KnowledgeFact fact = new KnowledgeFact(
                type,
                subject,
                position,
                CombatTacticalIntent.None,
                channel,
                actorId,
                nextProvenanceId++,
                now,
                now,
                confidence,
                now + KnowledgeFactPolicy.LifetimeFor(type));
            Upsert(fact);
            return fact;
        }

        public KnowledgeFact ReceiveCommand(
            CombatTacticalIntent intent,
            string commanderId,
            float now)
        {
            if (string.IsNullOrEmpty(actorId) || intent == CombatTacticalIntent.None)
            {
                return default;
            }

            KnowledgeFact fact = new KnowledgeFact(
                KnowledgeFactType.TacticalIntent,
                KnowledgeSubject.Formation,
                default,
                intent,
                KnowledgeChannel.Command,
                commanderId,
                nextProvenanceId++,
                now,
                now,
                1f,
                now + KnowledgeFactPolicy.LifetimeFor(KnowledgeFactType.TacticalIntent));
            Upsert(fact);
            return fact;
        }

        public bool ReceiveShared(KnowledgeFact source, float now)
        {
            if (string.IsNullOrEmpty(actorId) || !source.CanShareOnce || source.IsExpired(now))
            {
                return false;
            }

            KnowledgeFact received = new KnowledgeFact(
                source.Type,
                source.Subject,
                source.Position,
                source.TacticalIntent,
                KnowledgeChannel.Shared,
                source.OriginalObserverId,
                source.ProvenanceId,
                source.ObservedAt,
                now,
                source.Confidence,
                source.ExpiresAt);
            Upsert(received);
            return true;
        }

        public KnowledgeSnapshot CreateSnapshot(float now)
        {
            PruneExpired(now);
            KnowledgeFact[] copy = facts.ToArray();
            return new KnowledgeSnapshot(actorId, Array.AsReadOnly(copy));
        }

        public IReadOnlyList<KnowledgeDiagnostic> CreateDiagnostics(float now)
        {
            PruneExpired(now);
            KnowledgeDiagnostic[] diagnostics = new KnowledgeDiagnostic[facts.Count];
            for (int index = 0; index < facts.Count; index++)
            {
                diagnostics[index] = new KnowledgeDiagnostic(facts[index], now);
            }

            return Array.AsReadOnly(diagnostics);
        }

        private void Upsert(KnowledgeFact fact)
        {
            for (int index = 0; index < facts.Count; index++)
            {
                KnowledgeFact existing = facts[index];
                if (existing.Type == fact.Type && existing.Subject == fact.Subject)
                {
                    facts[index] = fact;
                    return;
                }
            }

            facts.Add(fact);
        }

        private void PruneExpired(float now)
        {
            for (int index = facts.Count - 1; index >= 0; index--)
            {
                if (facts[index].IsExpired(now))
                {
                    facts.RemoveAt(index);
                }
            }
        }
    }

    /// <summary>Explicit one-hop transport. Callers define recipients/boundaries.</summary>
    public static class KnowledgeSharing
    {
        public static int ShareOneHop(
            KnowledgeFact source,
            IList<KnowledgeStore> recipients,
            float now)
        {
            if (!source.CanShareOnce || recipients == null)
            {
                return 0;
            }

            int delivered = 0;
            for (int index = 0; index < recipients.Count; index++)
            {
                KnowledgeStore recipient = recipients[index];
                if (recipient != null && recipient.ActorId != source.OriginalObserverId
                    && recipient.ReceiveShared(source, now))
                {
                    delivered++;
                }
            }

            return delivered;
        }
    }

    /// <summary>
    /// Optional runtime owner and diagnostic surface. Missing it never blocks a
    /// brain; CTC members and Eyes opt in only where this sprint has evidence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnowledgeActor : MonoBehaviour
    {
        [SerializeField] private string actorId;
        [SerializeField] private int activeFactCount;
        [SerializeField] private string latestFactSummary;

        private KnowledgeStore store;

        public string ActorId => store != null ? store.ActorId : ResolveActorId();
        internal KnowledgeStore Store => store;

        public static KnowledgeActor EnsureOn(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            KnowledgeActor actor = target.GetComponent<KnowledgeActor>();
            return actor != null ? actor : target.AddComponent<KnowledgeActor>();
        }

        private void Awake()
        {
            EnsureStore();
        }

        public KnowledgeFact ObservePlayerLocation(Vector2 position, float now, float confidence = 1f)
        {
            return Observe(KnowledgeFactType.PlayerLocation, KnowledgeSubject.Player, position, now, confidence);
        }

        public void ObservePlayerSignals(BattlefieldSignal signals, float now, float confidence = 1f)
        {
            ObserveSignal(signals, BattlefieldSignal.PlayerRetreating, KnowledgeFactType.PlayerRetreating, now, confidence);
            ObserveSignal(signals, BattlefieldSignal.PlayerLowStamina, KnowledgeFactType.PlayerLowStamina, now, confidence);
            ObserveSignal(signals, BattlefieldSignal.PlayerGuarding, KnowledgeFactType.PlayerGuarding, now, confidence);
            ObserveSignal(signals, BattlefieldSignal.PlayerPressuringAlly, KnowledgeFactType.PlayerPressuringAlly, now, confidence);
        }

        public KnowledgeFact Observe(
            KnowledgeFactType type,
            KnowledgeSubject subject,
            Vector2 position,
            float now,
            float confidence = 1f)
        {
            EnsureStore();
            KnowledgeFact fact = store.Observe(type, subject, position, now, confidence);
            RefreshDiagnostics(now);
            return fact;
        }

        public KnowledgeSnapshot CreateSnapshot(float now)
        {
            EnsureStore();
            KnowledgeSnapshot snapshot = store.CreateSnapshot(now);
            RefreshDiagnostics(now);
            return snapshot;
        }

        public IReadOnlyList<KnowledgeDiagnostic> CreateDiagnostics(float now)
        {
            EnsureStore();
            return store.CreateDiagnostics(now);
        }

        internal void ReceiveCommand(CombatTacticalIntent intent, string commanderId, float now)
        {
            EnsureStore();
            store.ReceiveCommand(intent, commanderId, now);
            RefreshDiagnostics(now);
        }

        private void ObserveSignal(
            BattlefieldSignal signals,
            BattlefieldSignal signal,
            KnowledgeFactType type,
            float now,
            float confidence)
        {
            if ((signals & signal) != 0)
            {
                Observe(type, KnowledgeSubject.Player, default, now, confidence);
            }
        }

        private void EnsureStore()
        {
            if (store == null)
            {
                store = new KnowledgeStore(ResolveActorId());
            }
        }

        private string ResolveActorId()
        {
            return !string.IsNullOrEmpty(actorId)
                ? actorId
                : gameObject.name + "#" + GetInstanceID();
        }

        private void RefreshDiagnostics(float now)
        {
            if (store == null)
            {
                activeFactCount = 0;
                latestFactSummary = string.Empty;
                return;
            }

            IReadOnlyList<KnowledgeDiagnostic> diagnostics = store.CreateDiagnostics(now);
            activeFactCount = diagnostics.Count;
            latestFactSummary = diagnostics.Count > 0
                ? diagnostics[diagnostics.Count - 1].Fact.Type + " / "
                    + diagnostics[diagnostics.Count - 1].Fact.Channel
                : string.Empty;
        }
    }
}
