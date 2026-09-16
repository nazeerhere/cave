using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.Pickups;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    /// <summary>
    /// The short-lived, summoned territory owned by a <see cref="CurseAltar"/>.
    /// It is deliberately event/trigger driven: the registry is only consulted by
    /// an already-resolving hit, kill, or pickup; it never scans the scene for actors.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class CurseAltarZone : MonoBehaviour
    {
        private const int MaximumPoweredCurses = 3;
        private const float FriendlyFireMultiplier = 0.33f;
        private const float AvaricePayoutMultiplier = 1.3f;
        private const float GlareStackMultiplier = 1.15f;
        private const float LightStoneglassMultiplier = 1.10f;
        private const float HeavyStoneglassMultiplier = 1.15f;
        private const float CriticalStaggerMultiplier = 1.25f;
        private const float ResonanceDepositMultiplier = 1.10f;
        private const float HarvestPayoutPerGlareStack = 0.02f;
        private const float MomentumStaggerPerGlareStack = 0.02f;
        private const float ParanoiaDuration = 3f;

        private static readonly HashSet<CurseAltarZone> ActiveZones = new HashSet<CurseAltarZone>();

        [Header("Zone Configuration")]
        [SerializeField, Min(0.5f)] private float radius = 4.5f;
        [SerializeField, Range(0f, 1f)] private float detectiveCriticalChance = 0.2f;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private bool zoneActive;
        [SerializeField] private bool playerInside;
        [SerializeField] private int mobsInside;
        [SerializeField] private int avariceStoredValue;
        [SerializeField] private int glareKillStacks;
        [SerializeField] private float glareDamageMultiplier = 1f;
        [SerializeField] private PlayerCurseType[] poweredCurses = Array.Empty<PlayerCurseType>();
        [SerializeField] private int normalizedCombinationKey;
        [SerializeField] private string normalizedCombinationDebugKey;
        [SerializeField] private CurseAltarResonanceTier resonanceTier;
        [SerializeField] private string resonanceId;
        [SerializeField] private bool resonanceImplemented;
        [SerializeField] private bool pairResonanceActive;
        [SerializeField] private int paranoiaMarkedMobs;
        [SerializeField] private int friendlyFireKillsRecognized;
        [SerializeField] private int resonanceModifiedAvariceDeposits;
        [SerializeField] private float currentAvaricePayoutMultiplier = AvaricePayoutMultiplier;
        [SerializeField] private int resonanceGeneratedGlareStacks;
        [SerializeField] private float currentStoneglassResonanceModifier = 1f;

        private readonly Dictionary<PlayerHealth, int> playerContacts = new Dictionary<PlayerHealth, int>();
        private readonly Dictionary<Damageable, int> mobContacts = new Dictionary<Damageable, int>();
        private readonly HashSet<Damageable> playerParticipatedMobs = new HashSet<Damageable>();
        private readonly HashSet<CurseAltarConfusion> paranoiaMarkers = new HashSet<CurseAltarConfusion>();
        private PlayerCurseAltarController owner;
        private CircleCollider2D zoneCollider;
        private CurseAltarResonance resonance;

        public float Radius => radius;
        public bool IsActive => zoneActive;
        public bool PlayerInside => playerInside;
        public int MobsInside => mobsInside;
        public int AvariceStoredValue => avariceStoredValue;
        public int GlareKillStacks => glareKillStacks;
        public float GlareDamageMultiplier => glareDamageMultiplier;
        public IReadOnlyList<PlayerCurseType> PoweredCurses => poweredCurses;
        public CurseAltarResonance Resonance => resonance;
        public bool PairResonanceActive => pairResonanceActive;
        public int ParanoiaMarkedMobs => paranoiaMarkedMobs;
        public int FriendlyFireKillsRecognized => friendlyFireKillsRecognized;
        public int ResonanceModifiedAvariceDeposits => resonanceModifiedAvariceDeposits;
        public float CurrentAvaricePayoutMultiplier => currentAvaricePayoutMultiplier;
        public int ResonanceGeneratedGlareStacks => resonanceGeneratedGlareStacks;
        public float CurrentStoneglassResonanceModifier => currentStoneglassResonanceModifier;

        /// <summary>Reserved lifecycle hooks for future authored resonance effects.</summary>
        public event Action<CurseAltarResonance> ResonanceActivated;
        public event Action<CurseAltarResonance> ResonanceDeactivated;

        public void Configure(
            PlayerCurseAltarController altarOwner,
            IReadOnlyList<PlayerCurseType> curses,
            float zoneRadius)
        {
            owner = altarOwner;
            radius = Mathf.Max(0.5f, zoneRadius);
            int count = Mathf.Min(MaximumPoweredCurses, curses != null ? curses.Count : 0);
            poweredCurses = new PlayerCurseType[count];
            for (int index = 0; index < count; index++)
            {
                poweredCurses[index] = curses[index];
            }

            resonance = CurseAltarResonanceResolver.Resolve(poweredCurses);
            normalizedCombinationKey = resonance.CombinationKey;
            normalizedCombinationDebugKey = resonance.DebugKey;
            resonanceTier = resonance.Tier;
            resonanceId = resonance.ResonanceId;
            resonanceImplemented = resonance.IsImplemented;
            pairResonanceActive = resonance.Tier == CurseAltarResonanceTier.Pair
                && resonance.PairResonance != CurseAltarPairResonance.None;
            RefreshResonanceModifiers();

            EnsureCollider();
            zoneCollider.radius = radius;
            SetZoneActive(false);
        }

        public void SetZoneActive(bool active)
        {
            EnsureCollider();
            bool wasActive = zoneActive;
            zoneActive = active;
            zoneCollider.enabled = active;
            if (!wasActive && active)
            {
                ResonanceActivated?.Invoke(resonance);
            }
            else if (wasActive && !active)
            {
                ResonanceDeactivated?.Invoke(resonance);
                ResetTransientState();
            }
        }

        public bool HasCurse(PlayerCurseType curse)
        {
            for (int index = 0; index < poweredCurses.Length; index++)
            {
                if (poweredCurses[index] == curse)
                {
                    return true;
                }
            }

            return false;
        }

        public bool ContainsPoint(Vector2 position)
        {
            return zoneActive
                && ((Vector2)transform.position - position).sqrMagnitude <= radius * radius;
        }

        public bool HasPair(CurseAltarPairResonance pair)
        {
            return resonance.Tier == CurseAltarResonanceTier.Pair
                && resonance.PairResonance == pair;
        }

        /// <summary>Binds direct player damage to the one active zone that
        /// currently owns that player.  The bound identity follows the damage
        /// through death and drop processing, avoiding cross-altar pairing.</summary>
        public static DamageContext BindPlayerDamageZone(DamageContext damageContext)
        {
            if (!damageContext.IsPlayerDamage || damageContext.AltarZone != null)
            {
                return damageContext;
            }

            CurseAltarZone zone = FindPlayerZone(damageContext.PlayerSource, null);
            return zone != null ? damageContext.WithAltarZone(zone) : damageContext;
        }

        /// <summary>Resolves the approved, altar-local normal-attack bonuses once per hit.</summary>
        public static int ResolvePlayerAttackDamage(
            DamageContext damageContext,
            Damageable target,
            int incomingDamage,
            out bool altarCritical)
        {
            altarCritical = false;
            if (!damageContext.IsPlayerDamage || incomingDamage <= 0)
            {
                return incomingDamage;
            }

            CurseAltarZone zone = damageContext.AltarZone != null
                ? damageContext.AltarZone
                : FindPlayerZone(damageContext.PlayerSource, null);
            if (zone == null)
            {
                return incomingDamage;
            }

            int resolvedDamage = incomingDamage;
            if (zone.HasCurse(PlayerCurseType.CavesGlare))
            {
                resolvedDamage = Mathf.Max(
                    1,
                    Mathf.RoundToInt(resolvedDamage * zone.glareDamageMultiplier));
            }

            // Existing Frenzy criticals remain authoritative.  The altar only gives
            // ordinary direct player attacks access to that same critical resolver.
            if (zone.HasCurse(PlayerCurseType.Detective)
                && !damageContext.HasTrait(DamageTrait.FrenzyCritical)
                && !damageContext.HasTrait(DamageTrait.AreaOfEffect)
                && (damageContext.HasTrait(DamageTrait.Melee)
                    || damageContext.HasTrait(DamageTrait.Projectile)))
            {
                PlayerCombatFlow combatFlow = damageContext.PlayerSource != null
                    ? damageContext.PlayerSource.GetComponent<PlayerCombatFlow>()
                    : null;
                if (combatFlow != null)
                {
                    altarCritical = combatFlow.TryResolveAltarCriticalDamage(
                        resolvedDamage,
                        target,
                        zone.detectiveCriticalChance,
                        out resolvedDamage);
                }
            }

            return resolvedDamage;
        }

        public static float ResolvePlayerStaggerMultiplier(
            DamageContext damageContext,
            StaggerStrength strength)
        {
            if (!damageContext.IsPlayerDamage
                || !damageContext.HasTrait(DamageTrait.StaggerNormal)
                    && !damageContext.HasTrait(DamageTrait.StaggerHeavy))
            {
                return 1f;
            }

            CurseAltarZone zone = damageContext.AltarZone != null
                ? damageContext.AltarZone
                : FindPlayerZone(damageContext.PlayerSource, PlayerCurseType.Stoneglass);
            if (zone == null || !zone.HasCurse(PlayerCurseType.Stoneglass))
            {
                return 1f;
            }

            float multiplier = strength == StaggerStrength.Heavy
                ? HeavyStoneglassMultiplier
                : LightStoneglassMultiplier;
            if (zone.HasPair(CurseAltarPairResonance.FaultAnalysis)
                && damageContext.IsCritical)
            {
                multiplier *= CriticalStaggerMultiplier;
            }

            if (zone.HasPair(CurseAltarPairResonance.Momentum))
            {
                multiplier += zone.glareKillStacks * MomentumStaggerPerGlareStack;
            }

            return multiplier;
        }

        public static void NotifyPlayerKillingBlow(
            PlayerCurseAltarController controller,
            DamageContext killingBlow,
            Damageable victim)
        {
            CurseAltarZone zone = FindPlayerZone(controller != null ? controller.gameObject : null, PlayerCurseType.CavesGlare);
            if (zone == null || zone.owner != controller)
            {
                return;
            }

            zone.AddGlareStacks(1, false);
            if (zone.HasPair(CurseAltarPairResonance.Execution) && killingBlow.IsCritical)
            {
                zone.AddGlareStacks(1, true);
            }
        }

        /// <summary>Called after a successful player hit so participation and
        /// Paranoia are recorded only for actual applied damage.</summary>
        public static void NotifyPlayerDamageResolved(DamageContext damageContext, Damageable target, int appliedDamage)
        {
            if (!damageContext.IsPlayerDamage || target == null || appliedDamage <= 0)
            {
                return;
            }

            CurseAltarZone zone = damageContext.AltarZone != null
                ? damageContext.AltarZone
                : FindPlayerZone(damageContext.PlayerSource, null);
            if (zone == null || !zone.mobContacts.ContainsKey(target))
            {
                return;
            }

            if (zone.HasPair(CurseAltarPairResonance.Riot))
            {
                zone.playerParticipatedMobs.Add(target);
            }

            if (zone.HasPair(CurseAltarPairResonance.Paranoia) && damageContext.IsCritical)
            {
                zone.ApplyParanoia(target);
            }
        }

        /// <summary>
        /// Applies the reduced Insanity hit to a second enemy only when both actors
        /// are in the same active, snapshot-powered zone.
        /// </summary>
        public static bool TryApplyEnemyFriendlyFire(
            Damageable attacker,
            Damageable victim,
            int normalDamage,
            DamageTrait attackTraits)
        {
            if (attacker == null || victim == null || attacker == victim || normalDamage <= 0)
            {
                return false;
            }

            CurseAltarZone zone = FindSharedEnemyZone(attacker, victim);
            if (zone == null || !zone.HasCurse(PlayerCurseType.Insanity))
            {
                return false;
            }

            int friendlyDamage = Mathf.Max(1, Mathf.RoundToInt(normalDamage * FriendlyFireMultiplier));
            int appliedDamage = victim.TakeDamageResolved(
                friendlyDamage,
                new DamageContext(attacker.gameObject, attackTraits | DamageTrait.Direct)
                    .WithAltarZone(zone));
            if (appliedDamage > 0 && zone.HasPair(CurseAltarPairResonance.ShatteredMadness))
            {
                zone.TryApplyFriendlyFireStagger(victim, attackTraits);
            }

            return true;
        }

        /// <summary>Consumes the event once an enemy has actually died.  The
        /// friendly-fire context can only have been authored by the same zone's
        /// Insanity hit path.</summary>
        public static void NotifyEnemyDied(Damageable victim, DamageContext killingBlow)
        {
            CurseAltarZone zone = killingBlow.AltarZone;
            if (zone == null || !zone.zoneActive || victim == null || killingBlow.IsPlayerDamage)
            {
                return;
            }

            if (!zone.HasCurse(PlayerCurseType.Insanity) || !zone.mobContacts.ContainsKey(victim))
            {
                return;
            }

            zone.friendlyFireKillsRecognized++;
            if (zone.HasPair(CurseAltarPairResonance.Riot)
                && zone.playerInside
                && zone.playerParticipatedMobs.Remove(victim))
            {
                zone.AddGlareStacks(1, true);
            }
        }

        /// <summary>Returns the owning Avarice zone and its one-time stored
        /// value multiplier for a death.  The normal base Avarice path remains
        /// unchanged when this returns false.</summary>
        public static bool TryResolveAvariceDeposit(
            Damageable victim,
            DamageContext killingBlow,
            out CurseAltarZone zone,
            out float depositMultiplier)
        {
            zone = null;
            depositMultiplier = 1f;
            if (victim == null)
            {
                return false;
            }

            CurseAltarZone boundZone = killingBlow.AltarZone;
            if (boundZone != null
                && boundZone.zoneActive
                && boundZone.playerInside
                && boundZone.HasCurse(PlayerCurseType.Avarice)
                && boundZone.mobContacts.ContainsKey(victim))
            {
                if (boundZone.HasPair(CurseAltarPairResonance.BloodMoney)
                    && !killingBlow.IsPlayerDamage)
                {
                    zone = boundZone;
                    return true;
                }

                if (boundZone.HasPair(CurseAltarPairResonance.Bounty)
                    && killingBlow.IsPlayerDamage
                    && killingBlow.IsCritical)
                {
                    zone = boundZone;
                    depositMultiplier = ResonanceDepositMultiplier;
                    return true;
                }
            }

            CurseAltarZone staggerZone = FindMobZone(victim, PlayerCurseType.Avarice);
            if (staggerZone != null
                && staggerZone.HasPair(CurseAltarPairResonance.BreakAndTake)
                && victim.GetComponent<EnemyStagger>()?.IsStaggered == true)
            {
                zone = staggerZone;
                depositMultiplier = ResonanceDepositMultiplier;
                return true;
            }

            return false;
        }

        public static bool TryGetConfusedTarget(GameObject attacker, out Transform target)
        {
            target = null;
            CurseAltarConfusion confusion = attacker != null
                ? attacker.GetComponentInParent<CurseAltarConfusion>()
                : null;
            return confusion != null && confusion.TryGetPreferredTarget(out target);
        }

        internal bool TryGetNearestConfusionTarget(Damageable attacker, out Transform target)
        {
            target = null;
            if (!zoneActive || attacker == null)
            {
                return false;
            }

            float bestDistance = float.PositiveInfinity;
            foreach (KeyValuePair<Damageable, int> contact in mobContacts)
            {
                Damageable candidate = contact.Key;
                if (candidate == null
                    || candidate == attacker
                    || !candidate.gameObject.activeInHierarchy
                    || candidate.CurrentHealth <= 0)
                {
                    continue;
                }

                float distance = ((Vector2)candidate.transform.position - (Vector2)attacker.transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    target = candidate.transform;
                }
            }

            return target != null;
        }

        /// <summary>
        /// Currency is the only currently safe drop type with a serialised value.
        /// Other pickup classes remain untouched so quest/progression/equipment
        /// items cannot be accidentally converted into currency.
        /// </summary>
        public static bool TryAbsorbPickup(PickupBase pickup)
        {
            if (pickup == null || !pickup.TryGetAltarValue(out int value) || value <= 0)
            {
                return false;
            }

            CurseAltarZone receiver = null;
            foreach (CurseAltarZone zone in ActiveZones)
            {
                if (zone != null
                    && zone.zoneActive
                    && zone.playerInside
                    && zone.HasCurse(PlayerCurseType.Avarice)
                    && zone.ContainsPoint(pickup.transform.position)
                    && (receiver == null
                        || ((Vector2)zone.transform.position - (Vector2)pickup.transform.position).sqrMagnitude
                            < ((Vector2)receiver.transform.position - (Vector2)pickup.transform.position).sqrMagnitude))
                {
                    receiver = zone;
                }
            }

            return receiver != null && TryAbsorbPickup(pickup, receiver, 1f);
        }

        /// <summary>Consumes a specific qualifying currency pickup into the
        /// exact zone which owned the death context.  This prevents cross-altar
        /// pair interactions when several zones overlap.</summary>
        public static bool TryAbsorbPickup(
            PickupBase pickup,
            CurseAltarZone receiver,
            float depositMultiplier)
        {
            if (pickup == null
                || receiver == null
                || !receiver.zoneActive
                || !receiver.playerInside
                || !receiver.HasCurse(PlayerCurseType.Avarice)
                || !pickup.TryGetAltarValue(out int value)
                || value <= 0)
            {
                return false;
            }

            int depositedValue = Mathf.Max(1, Mathf.RoundToInt(value * Mathf.Max(0f, depositMultiplier)));
            receiver.avariceStoredValue = Mathf.Min(int.MaxValue - depositedValue, receiver.avariceStoredValue) + depositedValue;
            if (depositMultiplier > 1f)
            {
                receiver.resonanceModifiedAvariceDeposits++;
            }

            return true;
        }

        private static CurseAltarZone FindPlayerZone(GameObject playerObject, PlayerCurseType? requiredCurse)
        {
            if (playerObject == null)
            {
                return null;
            }

            CurseAltarZone nearest = null;
            float nearestDistanceSquared = float.PositiveInfinity;
            foreach (CurseAltarZone zone in ActiveZones)
            {
                if (zone == null
                    || !zone.zoneActive
                    || !zone.playerInside
                    || zone.owner == null
                    || zone.owner.gameObject != playerObject
                    || (requiredCurse.HasValue && !zone.HasCurse(requiredCurse.Value)))
                {
                    continue;
                }

                float distanceSquared = ((Vector2)zone.transform.position - (Vector2)playerObject.transform.position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = zone;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        private static CurseAltarZone FindSharedEnemyZone(Damageable attacker, Damageable victim)
        {
            foreach (CurseAltarZone zone in ActiveZones)
            {
                if (zone != null
                    && zone.zoneActive
                    && zone.mobContacts.ContainsKey(attacker)
                    && zone.mobContacts.ContainsKey(victim))
                {
                    return zone;
                }
            }

            return null;
        }

        private static CurseAltarZone FindMobZone(Damageable mob, PlayerCurseType requiredCurse)
        {
            foreach (CurseAltarZone zone in ActiveZones)
            {
                if (zone != null
                    && zone.zoneActive
                    && zone.playerInside
                    && zone.HasCurse(requiredCurse)
                    && zone.mobContacts.ContainsKey(mob))
                {
                    return zone;
                }
            }

            return null;
        }

        private void TryApplyFriendlyFireStagger(Damageable victim, DamageTrait attackTraits)
        {
            StaggerStrength strength;
            if (attackTraits.HasFlag(DamageTrait.StaggerHeavy))
            {
                strength = StaggerStrength.Heavy;
            }
            else if (attackTraits.HasFlag(DamageTrait.StaggerNormal))
            {
                strength = StaggerStrength.Normal;
            }
            else
            {
                // Never guess a classification for an existing enemy attack.
                return;
            }

            EnemyStagger stagger = victim.GetComponent<EnemyStagger>();
            if (stagger == null)
            {
                return;
            }

            float multiplier = strength == StaggerStrength.Heavy
                ? HeavyStoneglassMultiplier
                : LightStoneglassMultiplier;
            stagger.TryStagger(strength, stagger.GetBaseDuration(strength) * multiplier);
        }

        private void ApplyParanoia(Damageable target)
        {
            CurseAltarConfusion confusion = target.GetComponent<CurseAltarConfusion>();
            if (confusion == null)
            {
                confusion = target.gameObject.AddComponent<CurseAltarConfusion>();
            }

            if (confusion.Apply(this, ParanoiaDuration))
            {
                paranoiaMarkers.Add(confusion);
                paranoiaMarkedMobs = paranoiaMarkers.Count;
            }
        }

        internal void UnregisterParanoia(CurseAltarConfusion confusion)
        {
            if (confusion != null && paranoiaMarkers.Remove(confusion))
            {
                paranoiaMarkedMobs = paranoiaMarkers.Count;
            }
        }

        private void AddGlareStacks(int amount, bool resonanceGenerated)
        {
            if (amount <= 0)
            {
                return;
            }

            glareKillStacks += amount;
            if (resonanceGenerated)
            {
                resonanceGeneratedGlareStacks += amount;
            }

            glareDamageMultiplier = Mathf.Pow(GlareStackMultiplier, glareKillStacks);
            RefreshResonanceModifiers();
        }

        private void RefreshResonanceModifiers()
        {
            currentAvaricePayoutMultiplier = HasPair(CurseAltarPairResonance.Harvest)
                ? AvaricePayoutMultiplier + glareKillStacks * HarvestPayoutPerGlareStack
                : AvaricePayoutMultiplier;
            currentStoneglassResonanceModifier = HasPair(CurseAltarPairResonance.Momentum)
                ? 1f + glareKillStacks * MomentumStaggerPerGlareStack
                : 1f;
        }

        private void Awake()
        {
            EnsureCollider();
        }

        private void OnEnable()
        {
            ActiveZones.Add(this);
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
            SetZoneActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!zoneActive)
            {
                return;
            }

            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player != null && owner != null && player.gameObject == owner.gameObject)
            {
                playerContacts.TryGetValue(player, out int contacts);
                playerContacts[player] = contacts + 1;
                playerInside = true;
                return;
            }

            Damageable mob = other.GetComponentInParent<Damageable>();
            if (mob != null && mob.GetComponent<EnemyArchetypeProfile>() != null)
            {
                mobContacts.TryGetValue(mob, out int contacts);
                mobContacts[mob] = contacts + 1;
                mobsInside = mobContacts.Count;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player != null && playerContacts.TryGetValue(player, out int playerCount))
            {
                if (playerCount <= 1)
                {
                    playerContacts.Remove(player);
                    playerInside = false;
                    PayoutAvariceBank(player);
                    ResetGlareStacks();
                }
                else
                {
                    playerContacts[player] = playerCount - 1;
                }

                return;
            }

            Damageable mob = other.GetComponentInParent<Damageable>();
            if (mob != null && mobContacts.TryGetValue(mob, out int mobCount))
            {
                if (mobCount <= 1)
                {
                    mobContacts.Remove(mob);
                }
                else
                {
                    mobContacts[mob] = mobCount - 1;
                }

                mobsInside = mobContacts.Count;
            }
        }

        private void EnsureCollider()
        {
            if (zoneCollider == null)
            {
                zoneCollider = GetComponent<CircleCollider2D>();
            }

            zoneCollider.isTrigger = true;
            zoneCollider.radius = radius;
        }

        private void PayoutAvariceBank(PlayerHealth player)
        {
            if (avariceStoredValue <= 0 || player == null)
            {
                return;
            }

            int payout = Mathf.Max(1, Mathf.RoundToInt(avariceStoredValue * currentAvaricePayoutMultiplier));
            avariceStoredValue = 0;
            player.GetComponent<PlayerCurrency>()?.AddCurrency(payout);
        }

        private void ResetTransientState()
        {
            playerContacts.Clear();
            mobContacts.Clear();
            playerParticipatedMobs.Clear();
            foreach (CurseAltarConfusion marker in paranoiaMarkers)
            {
                if (marker != null)
                {
                    marker.ClearFromZone(this);
                }
            }

            paranoiaMarkers.Clear();
            paranoiaMarkedMobs = 0;
            playerInside = false;
            mobsInside = 0;
            ResetGlareStacks();
        }

        private void ResetGlareStacks()
        {
            glareKillStacks = 0;
            glareDamageMultiplier = 1f;
            resonanceGeneratedGlareStacks = 0;
            RefreshResonanceModifiers();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = zoneActive ? new Color(0.9f, 0.1f, 0.15f, 0.65f) : new Color(0.9f, 0.7f, 0.15f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
