using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [System.Flags]
    public enum BattlefieldArchetypeTag
    {
        None = 0,
        Frontline = 1 << 0,
        Skirmisher = 1 << 1,
        Support = 1 << 2,
        Controller = 1 << 3,
        RangedPressure = 1 << 4,
        FlyingSupport = 1 << 5
    }

    [System.Flags]
    public enum BattlefieldSignal
    {
        None = 0,
        PlayerRetreating = 1 << 0,
        PlayerGuarding = 1 << 1,
        PlayerLowStamina = 1 << 2,
        PlayerIsolated = 1 << 3,
        PlayerPressuringAlly = 1 << 4,
        FrontlineEngaged = 1 << 5,
        SupportAvailable = 1 << 6
    }

    public readonly struct BattlefieldSnapshot
    {
        public readonly BattlefieldArchetypeTag NearbyTags;
        public readonly BattlefieldSignal Signals;

        public BattlefieldSnapshot(BattlefieldArchetypeTag nearbyTags, BattlefieldSignal signals)
        {
            NearbyTags = nearbyTags;
            Signals = signals;
        }

        public bool HasTag(BattlefieldArchetypeTag tag) => (NearbyTags & tag) != 0;
        public bool HasSignal(BattlefieldSignal signal) => (Signals & signal) != 0;
    }

    /// <summary>
    /// A small, decision-time view of nearby broad roles. It intentionally does
    /// not encode named enemy pairs or issue commands.
    /// </summary>
    public static class BattlefieldContext
    {
        public static BattlefieldSnapshot Read(Component reader, float radius)
        {
            if (reader == null)
            {
                return default;
            }

            BattlefieldArchetypeTag tags = BattlefieldArchetypeTag.None;
            Collider2D[] nearby = Physics2D.OverlapCircleAll(reader.transform.position, Mathf.Max(0f, radius));
            for (int index = 0; index < nearby.Length; index++)
            {
                Collider2D collider = nearby[index];
                if (collider == null)
                {
                    continue;
                }

                EnemyArchetypeProfile profile = collider.GetComponentInParent<EnemyArchetypeProfile>();
                Transform root = profile != null ? profile.transform : collider.transform.root;
                if (root == reader.transform || root.IsChildOf(reader.transform) || reader.transform.IsChildOf(root))
                {
                    continue;
                }

                tags |= ResolveTags(root.gameObject);
            }

            EyeWatcherNetwork.TryGetSharedContext(reader.transform.position, out BattlefieldSignal signals);
            return new BattlefieldSnapshot(tags, signals);
        }

        public static BattlefieldArchetypeTag ResolveTags(GameObject candidate)
        {
            if (candidate == null)
            {
                return BattlefieldArchetypeTag.None;
            }

            BattlefieldArchetypeTag tags = BattlefieldArchetypeTag.None;
            EnemyArchetypeProfile profile = candidate.GetComponentInParent<EnemyArchetypeProfile>();
            if (profile != null)
            {
                if (profile.Includes(EnemyArchetype.Melee) || profile.Includes(EnemyArchetype.Tank))
                {
                    tags |= BattlefieldArchetypeTag.Frontline;
                }

                if (profile.Includes(EnemyArchetype.Support))
                {
                    tags |= BattlefieldArchetypeTag.Support;
                }

                if (profile.Includes(EnemyArchetype.Ranged))
                {
                    tags |= BattlefieldArchetypeTag.RangedPressure;
                }

                if (profile.Includes(EnemyArchetype.Swarm))
                {
                    tags |= BattlefieldArchetypeTag.Skirmisher;
                }
            }

            if (candidate.GetComponentInParent<LightBanditBrain>() != null)
            {
                tags |= BattlefieldArchetypeTag.Skirmisher;
            }

            if (candidate.GetComponentInParent<EyeBrain>() != null)
            {
                tags |= BattlefieldArchetypeTag.FlyingSupport;
            }

            return tags;
        }

        public static BattlefieldSignal BuildReliablePlayerSignals(
            PlayerHealth player,
            Vector2 observerPosition,
            float retreatSpeedThreshold,
            float lowStaminaFraction)
        {
            if (player == null)
            {
                return BattlefieldSignal.None;
            }

            BattlefieldSignal result = BattlefieldSignal.None;
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            Vector2 away = (Vector2)player.transform.position - observerPosition;
            if (playerBody != null
                && playerBody.velocity.magnitude >= retreatSpeedThreshold
                && Vector2.Dot(playerBody.velocity.normalized, away.normalized) > 0.45f)
            {
                result |= BattlefieldSignal.PlayerRetreating;
            }

            if (player.GetComponent<Cave.Combat.SidewaysParryAttack>()?.IsGuardHeld == true)
            {
                result |= BattlefieldSignal.PlayerGuarding;
            }

            SpinSwordAttack stamina = player.GetComponent<SpinSwordAttack>();
            if (stamina != null
                && stamina.CurrentStamina <= stamina.MaximumStamina * Mathf.Clamp01(lowStaminaFraction))
            {
                result |= BattlefieldSignal.PlayerLowStamina;
            }

            if (player.GetComponent<PlayerAttackState>()?.IsActivelyAttacking == true)
            {
                result |= BattlefieldSignal.PlayerPressuringAlly;
            }

            return result;
        }
    }
}
