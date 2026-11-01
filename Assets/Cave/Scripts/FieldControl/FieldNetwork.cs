using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.Player;
using UnityEngine;

namespace Cave.FieldControl
{
    public enum FieldOwnerTeam { Player, Enemy }
    [Flags]
    public enum FieldLinkTacticalEffect { None=0, Reinforced=1, Suppressed=2 }

    [DisallowMultipleComponent]
    public sealed class FieldResistance : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.95f)] private float forceResistance;
        public float ForceResistance => forceResistance;
    }

    [DisallowMultipleComponent]
    public sealed class FieldNode : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximumHealth = 4;
        [SerializeField, Min(0.1f)] private float maximumEnergy = 8f;
        [SerializeField, Min(0f)] private float lifetime = 14f;
        [SerializeField] private FieldOwnerTeam ownerTeam;

        private Damageable damageable;
        private FieldNetwork network;
        private bool configured;
        private bool depletionScheduled;
        private float currentEnergy;
        private ulong creationOrder;
        private float tacticalGuardUntil;
        private float tacticalGuardDamageMultiplier = 1f;

        public event Action<FieldNode> StateChanged;
        public event Action<FieldNode> EnergyChanged;
        public FieldOwnerTeam OwnerTeam => ownerTeam;
        public int CurrentHealth => damageable != null ? damageable.CurrentHealth : 0;
        public int MaximumHealth => damageable != null ? damageable.MaximumHealth : maximumHealth;
        public float CurrentEnergy => currentEnergy;
        public float MaximumEnergy => maximumEnergy;
        public float HealthFraction => MaximumHealth > 0 ? CurrentHealth / (float)MaximumHealth : 0f;
        public float EnergyFraction => maximumEnergy > 0f ? currentEnergy / maximumEnergy : 0f;
        public float SurvivabilityFraction => Mathf.Min(HealthFraction, EnergyFraction);
        public ulong CreationOrder => creationOrder;
        /// <summary>Owning physical topology authority. Read-only for adapters.</summary>
        public FieldNetwork Network => network;
        public bool CanContribute => configured && isActiveAndEnabled && CurrentHealth > 0 && currentEnergy > 0.001f;

        public void Configure(FieldNetwork ownerNetwork, FieldOwnerTeam team, int health, float energy, float duration, ulong order)
        {
            network = ownerNetwork;
            ownerTeam = team;
            maximumHealth = Mathf.Max(1, health);
            maximumEnergy = Mathf.Max(0.1f, energy);
            lifetime = Mathf.Max(0f, duration);
            creationOrder = order;
            EnsureDamageable();
            damageable.SetRuntimeMaximumHealth(maximumHealth, true);
            currentEnergy = maximumEnergy;
            configured = true;
            network?.Register(this);
            if (lifetime > 0f) Invoke(nameof(Expire), lifetime);
        }

        public void DrainEnergy(float amount)
        {
            TryDrainEnergy(amount);
        }

        public bool CanSpendEnergy(float amount)
        {
            return CanContribute && amount >= 0f
                && (amount <= 0f || currentEnergy - amount > 0.001f);
        }

        /// <summary>Atomic typed spend seam for match-scoped commands. It never regenerates energy.</summary>
        public bool TryDrainEnergy(float amount)
        {
            if (!CanSpendEnergy(amount)) return false;
            if (amount <= 0f) return true;
            float before = currentEnergy;
            currentEnergy = Mathf.Max(0f, currentEnergy - amount);
            if (!Mathf.Approximately(before, currentEnergy)) EnergyChanged?.Invoke(this);
            if (before > 0.001f && currentEnergy <= 0.001f)
            {
                StateChanged?.Invoke(this);
                if (!depletionScheduled)
                {
                    depletionScheduled = true;
                    Invoke(nameof(Expire), 0f);
                }
            }
            return true;
        }

        public void ApplyTacticalGuard(float duration, float damageMultiplier)
        {
            tacticalGuardUntil = Mathf.Max(tacticalGuardUntil, Time.time + Mathf.Max(0f, duration));
            tacticalGuardDamageMultiplier = Mathf.Clamp(damageMultiplier, 0.05f, 1f);
            StateChanged?.Invoke(this);
        }

        public void ClearTacticalGuard()
        {
            tacticalGuardUntil = 0f;
            tacticalGuardDamageMultiplier = 1f;
            StateChanged?.Invoke(this);
        }

        public bool TacticalGuardActive => Time.time < tacticalGuardUntil;

        public int ResolveIncomingPhysicalDamage(int amount, DamageContext context)
        {
            return ResolveGuardedDamage(amount, TacticalGuardActive, tacticalGuardDamageMultiplier,
                context.HasTrait(DamageTrait.Periodic));
        }

        internal static int ResolveGuardedDamage(int amount, bool active, float multiplier, bool periodic)
        {
            if (amount <= 0 || !active || periodic) return Mathf.Max(0, amount);
            return Mathf.Max(0, Mathf.CeilToInt(amount * Mathf.Clamp(multiplier, 0.05f, 1f)));
        }

        public void Sacrifice()
        {
            if (!configured) return;
            CancelInvoke(nameof(Expire));
            StateChanged?.Invoke(this);
            network?.Unregister(this);
        }

        private void Awake() { EnsureDamageable(); }

        private void EnsureDamageable()
        {
            if (damageable == null) damageable = GetComponent<Damageable>();
            if (damageable == null) damageable = gameObject.AddComponent<Damageable>();
            damageable.Died -= HandleDestroyed;
            damageable.Died += HandleDestroyed;
        }

        private void HandleDestroyed()
        {
            StateChanged?.Invoke(this);
            network?.Unregister(this);
        }

        private void Expire()
        {
            StateChanged?.Invoke(this);
            network?.Unregister(this);
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (configured) network?.Unregister(this);
        }

        private void OnDestroy()
        {
            if (damageable != null) damageable.Died -= HandleDestroyed;
            network?.Unregister(this);
        }
    }

    [DisallowMultipleComponent]
    public sealed class FieldNetwork : MonoBehaviour
    {
        [Header("Topology")]
        [SerializeField, Min(1)] private int maximumNodes = 3;
        [SerializeField, Min(0.1f)] private float maximumLinkDistance = 9f;
        [SerializeField, Min(0.01f)] private float collinearTolerance = 0.45f;

        [Header("Strength")]
        [SerializeField, Min(0.01f)] private float baseStrength = 12f;
        [SerializeField, Min(0.01f)] private float distanceOffset = 1f;
        [SerializeField, Min(0.1f)] private float distanceExponent = 1.25f;
        [SerializeField, Min(0f)] private float energyDrainPerLinkedNode = 0.16f;
        [SerializeField, Min(0.05f)] private float energyTickInterval = 0.25f;
        [SerializeField, Min(0.05f)] private float segmentWidth = 0.18f;
        [SerializeField] private LayerMask affectedLayers = ~0;
        [SerializeField] private FieldOwnerTeam ownerTeam;
        [SerializeField] private Color strongColor = new Color(0.2f, 0.85f, 1f, 0.8f);
        [SerializeField] private Color weakColor = new Color(0.85f, 0.25f, 1f, 0.45f);
        [SerializeField] private GameObject linkVisualPrefab;

        private readonly List<FieldNode> nodes = new List<FieldNode>(3);
        private readonly List<FieldLink> links = new List<FieldLink>(3);
        private readonly Dictionary<FieldLinkIdentity, TemporaryLinkState> tacticalLinks =
            new Dictionary<FieldLinkIdentity, TemporaryLinkState>(3);
        private readonly List<FieldLinkIdentity> tacticalLinkKeys = new List<FieldLinkIdentity>(3);
        private readonly List<FieldLinkIdentity> tacticalLinkRemovals = new List<FieldLinkIdentity>(3);
        private bool topologyDirty;
        private float nextEnergyTick;

        /// <summary>Raised after the authoritative disk-link set has been rebuilt.</summary>
        public event Action TopologyChanged;
        public int ActiveNodeCount => nodes.Count;
        public int ActiveLinkCount => links.Count;
        /// <summary>Monotonic only for this live network instance.  Consumers use it
        /// to invalidate topology projections; it is not a gameplay priority.</summary>
        public uint TopologyVersion { get; private set; }
        public FieldOwnerTeam OwnerTeam => ownerTeam;
        public float MaximumLinkDistance => maximumLinkDistance;

        public readonly struct FieldLinkIdentity : IEquatable<FieldLinkIdentity>
        {
            public FieldLinkIdentity(ulong first, ulong second)
            {
                if (first <= second) { First = first; Second = second; }
                else { First = second; Second = first; }
            }
            public ulong First { get; }
            public ulong Second { get; }
            public bool IsValid => First > 0UL && Second > 0UL && First != Second;
            public bool Equals(FieldLinkIdentity other) => First == other.First && Second == other.Second;
            public override bool Equals(object obj) => obj is FieldLinkIdentity other && Equals(other);
            public override int GetHashCode() => unchecked((First.GetHashCode() * 397) ^ Second.GetHashCode());
        }

        private struct TemporaryLinkState
        {
            public float SuppressedUntil;
            public float ReinforcedUntil;
            public float ForceMultiplier;
            public float UpkeepMultiplier;
        }

        internal static bool IsWithinLinkDistance(Vector2 first, Vector2 second, float maximumDistance)
        {
            return Vector2.Distance(first, second) <= Mathf.Max(0.1f, maximumDistance);
        }

        internal static float ResolveEnergyDrain(float drainPerLinkedNode, float interval, int linkCount)
        {
            return Mathf.Max(0f, drainPerLinkedNode)
                * Mathf.Max(0f, interval)
                * Mathf.Max(0, linkCount);
        }

        /// <summary>Read-only gameplay-link query for adapters. FieldNetwork remains the owner of link creation and force behavior.</summary>
        public bool AreLinked(FieldNode first,FieldNode second)
        {
            if(first==null||second==null)return false;
            for(int index=0;index<links.Count;index++)if(links[index]!=null&&links[index].Connects(first,second))return true;
            return false;
        }

        public bool TryReinforceLink(FieldNode first, FieldNode second, float duration,
            float forceMultiplier, float upkeepMultiplier)
        {
            if (!AreLinked(first, second)) return false;
            FieldLinkIdentity id = Identity(first, second);
            if (!id.IsValid || IsSuppressed(id, Time.time)) return false;
            TemporaryLinkState state;
            tacticalLinks.TryGetValue(id, out state);
            state.ReinforcedUntil = Mathf.Max(state.ReinforcedUntil, Time.time + Mathf.Max(0f, duration));
            state.ForceMultiplier = Mathf.Max(1f, forceMultiplier);
            state.UpkeepMultiplier = Mathf.Clamp(upkeepMultiplier, 0f, 1f);
            tacticalLinks[id] = state;
            RefreshLinkModifiers();
            TopologyChanged?.Invoke();
            return true;
        }

        public bool TrySuppressLink(FieldNode first, FieldNode second, float duration)
        {
            if (!AreLinked(first, second)) return false;
            FieldLinkIdentity id = Identity(first, second);
            if (!id.IsValid || IsSuppressed(id, Time.time)) return false;
            TemporaryLinkState state;
            tacticalLinks.TryGetValue(id, out state);
            state.SuppressedUntil = Time.time + Mathf.Max(0f, duration);
            tacticalLinks[id] = state;
            topologyDirty = true;
            RebuildIfNeeded();
            return true;
        }

        public bool IsLinkSuppressed(FieldNode first, FieldNode second)
        {
            return IsSuppressed(Identity(first, second), Time.time);
        }

        public FieldLinkTacticalEffect GetTacticalEffects(FieldNode node)
        {
            if(node==null)return FieldLinkTacticalEffect.None;
            FieldLinkTacticalEffect result=FieldLinkTacticalEffect.None;
            foreach(KeyValuePair<FieldLinkIdentity,TemporaryLinkState> pair in tacticalLinks)
            {
                if(pair.Key.First!=node.CreationOrder&&pair.Key.Second!=node.CreationOrder)continue;
                if(pair.Value.ReinforcedUntil>Time.time)result|=FieldLinkTacticalEffect.Reinforced;
                if(pair.Value.SuppressedUntil>Time.time)result|=FieldLinkTacticalEffect.Suppressed;
            }
            return result;
        }

        public void NotifyNodeMoved(FieldNode node)
        {
            if (node == null || !nodes.Contains(node)) return;
            topologyDirty = true;
            RebuildIfNeeded();
        }

        public void ClearTemporaryTacticalState()
        {
            if (tacticalLinks.Count == 0) return;
            tacticalLinks.Clear();
            topologyDirty = true;
            RebuildIfNeeded();
        }

        /// <summary>Returns the current physical nodes in deterministic placement
        /// order.  It deliberately exposes neither mutable topology nor links.</summary>
        public IReadOnlyList<FieldNode> SnapshotNodes()
        {
            RebuildIfNeeded();
            List<FieldNode> result = new List<FieldNode>(nodes);
            result.Sort((left, right) => left.CreationOrder.CompareTo(right.CreationOrder));
            return result.AsReadOnly();
        }

        /// <summary>Returns only directly linked physical neighbours.  Domain
        /// adapters must map these nodes to live carriers separately.</summary>
        public IReadOnlyList<FieldNode> SnapshotLinkedNodes(FieldNode node)
        {
            RebuildIfNeeded();
            List<FieldNode> result = new List<FieldNode>();
            if (node == null) return result.AsReadOnly();
            for (int index = 0; index < nodes.Count; index++)
            {
                FieldNode candidate = nodes[index];
                if (candidate != null && !ReferenceEquals(candidate, node) && AreLinked(node, candidate)) result.Add(candidate);
            }
            result.Sort((left, right) => left.CreationOrder.CompareTo(right.CreationOrder));
            return result.AsReadOnly();
        }

        public void Configure(FieldOwnerTeam team, LayerMask targetLayers, int nodeCap, float maxDistance, float strength, float offset, float exponent)
        {
            ownerTeam = team;
            affectedLayers = targetLayers;
            maximumNodes = Mathf.Clamp(nodeCap, 1, 3);
            maximumLinkDistance = Mathf.Max(0.1f, maxDistance);
            baseStrength = Mathf.Max(0.01f, strength);
            distanceOffset = Mathf.Max(0.01f, offset);
            distanceExponent = Mathf.Max(0.1f, exponent);
        }

        public void SetLinkVisualPrefab(GameObject prefab) { linkVisualPrefab = prefab; }

        public bool Register(FieldNode node)
        {
            if (node == null || node.OwnerTeam != ownerTeam || nodes.Contains(node)) return false;
            if (nodes.Count >= maximumNodes) return false;
            nodes.Add(node);
            node.StateChanged += HandleNodeStateChanged;
            topologyDirty = true;
            RebuildIfNeeded();
            return true;
        }

        public void Unregister(FieldNode node)
        {
            if (node == null || !nodes.Remove(node)) return;
            node.StateChanged -= HandleNodeStateChanged;
            topologyDirty = true;
            RebuildIfNeeded();
        }

        private void Update()
        {
            ExpireTemporaryLinkStates();
            RebuildIfNeeded();
            if (links.Count == 0 || Time.time < nextEnergyTick) return;
            nextEnergyTick = Time.time + energyTickInterval;
            float weightedLinks = 0f;
            for (int index = 0; index < links.Count; index++)
                if (links[index] != null) weightedLinks += links[index].UpkeepMultiplier;
            float drain = Mathf.Max(0f, energyDrainPerLinkedNode)
                * Mathf.Max(0f, energyTickInterval) * weightedLinks;
            for (int index = 0; index < nodes.Count; index++) nodes[index]?.DrainEnergy(drain);
            for (int index = 0; index < links.Count; index++) links[index]?.RefreshStrength();
        }

        private void OnEnable() { topologyDirty = true; }

        private void HandleNodeStateChanged(FieldNode _) { topologyDirty = true; }

        private void RebuildIfNeeded()
        {
            if (!topologyDirty) return;
            topologyDirty = false;
            for (int index = nodes.Count - 1; index >= 0; index--)
            {
                if (nodes[index] == null || !nodes[index].CanContribute)
                {
                    if (nodes[index] != null) nodes[index].StateChanged -= HandleNodeStateChanged;
                    nodes.RemoveAt(index);
                }
            }
            ClearLinks();
            if (nodes.Count == 2) AddLink(nodes[0], nodes[1]);
            else if (nodes.Count == 3) BuildThreeNodeTopology();
            TopologyVersion++;
            TopologyChanged?.Invoke();
        }

        private void BuildThreeNodeTopology()
        {
            FieldNode first = nodes[0]; FieldNode second = nodes[1]; FieldNode third = nodes[2];
            float d01 = ((Vector2)first.transform.position - (Vector2)second.transform.position).sqrMagnitude;
            float d12 = ((Vector2)second.transform.position - (Vector2)third.transform.position).sqrMagnitude;
            float d20 = ((Vector2)third.transform.position - (Vector2)first.transform.position).sqrMagnitude;
            if (d12 > d01 && d12 >= d20) { first = nodes[1]; second = nodes[2]; third = nodes[0]; }
            else if (d20 > d01 && d20 > d12) { first = nodes[2]; second = nodes[0]; third = nodes[1]; }
            Vector2 line = (Vector2)second.transform.position - (Vector2)first.transform.position;
            float distance = line.magnitude;
            float deviation = distance > 0.001f
                ? Mathf.Abs(Vector2.Perpendicular(line / distance).x * ((Vector2)third.transform.position - (Vector2)first.transform.position).x
                    + Vector2.Perpendicular(line / distance).y * ((Vector2)third.transform.position - (Vector2)first.transform.position).y)
                : float.MaxValue;
            if (deviation <= collinearTolerance)
            {
                Vector2 axis = line.normalized;
                nodes.Sort((left, right) => Vector2.Dot(left.transform.position, axis)
                    .CompareTo(Vector2.Dot(right.transform.position, axis)));
                AddLink(nodes[0], nodes[1]);
                AddLink(nodes[1], nodes[2]);
                return;
            }
            AddLink(first, second);
            AddLink(second, third);
            AddLink(third, first);
        }

        private void AddLink(FieldNode a, FieldNode b)
        {
            if (a == null || b == null || !IsWithinLinkDistance(
                a.transform.position,
                b.transform.position,
                maximumLinkDistance)) return;
            FieldLinkIdentity id = Identity(a, b);
            if (IsSuppressed(id, Time.time)) return;
            TemporaryLinkState state;
            bool reinforced = tacticalLinks.TryGetValue(id, out state)
                && state.ReinforcedUntil > Time.time;
            GameObject linkObject = new GameObject("Field Link");
            linkObject.transform.SetParent(transform, false);
            FieldLink link = linkObject.AddComponent<FieldLink>();
            link.Configure(a, b, ownerTeam, affectedLayers, baseStrength, distanceOffset, distanceExponent,
                segmentWidth, strongColor, weakColor, linkVisualPrefab,
                reinforced ? Mathf.Max(1f, state.ForceMultiplier) : 1f,
                reinforced ? Mathf.Clamp(state.UpkeepMultiplier, 0f, 1f) : 1f);
            links.Add(link);
        }

        private static FieldLinkIdentity Identity(FieldNode first, FieldNode second)
        {
            return first == null || second == null
                ? default
                : new FieldLinkIdentity(first.CreationOrder, second.CreationOrder);
        }

        private bool IsSuppressed(FieldLinkIdentity id, float now)
        {
            TemporaryLinkState state;
            return id.IsValid && tacticalLinks.TryGetValue(id, out state)
                && state.SuppressedUntil > now;
        }

        private void ExpireTemporaryLinkStates()
        {
            if (tacticalLinks.Count == 0) return;
            bool rebuild = false;
            tacticalLinkKeys.Clear();tacticalLinkRemovals.Clear();
            foreach(FieldLinkIdentity key in tacticalLinks.Keys)tacticalLinkKeys.Add(key);
            for (int index = 0; index < tacticalLinkKeys.Count; index++)
            {
                FieldLinkIdentity key=tacticalLinkKeys[index];
                TemporaryLinkState state = tacticalLinks[key];
                if (state.SuppressedUntil > 0f && state.SuppressedUntil <= Time.time)
                {
                    state.SuppressedUntil = 0f;
                    rebuild = true;
                }
                if (state.ReinforcedUntil > 0f && state.ReinforcedUntil <= Time.time)
                {
                    state.ReinforcedUntil = 0f;
                    rebuild = true;
                }
                if (state.SuppressedUntil <= 0f && state.ReinforcedUntil <= 0f)
                {
                    tacticalLinkRemovals.Add(key);
                }
                else tacticalLinks[key] = state;
            }
            for (int index = 0; index < tacticalLinkRemovals.Count; index++) tacticalLinks.Remove(tacticalLinkRemovals[index]);
            if (rebuild) { topologyDirty = true; RebuildIfNeeded(); }
        }

        private void RefreshLinkModifiers()
        {
            for (int index = 0; index < links.Count; index++)
            {
                FieldLink link = links[index];
                if (link == null) continue;
                TemporaryLinkState state;
                bool active = tacticalLinks.TryGetValue(link.Identity, out state)
                    && state.ReinforcedUntil > Time.time;
                link.SetTemporaryModifiers(active ? state.ForceMultiplier : 1f,
                    active ? state.UpkeepMultiplier : 1f);
            }
        }

        private void ClearLinks()
        {
            for (int index = 0; index < links.Count; index++)
            {
                if (links[index] != null) Destroy(links[index].gameObject);
            }
            links.Clear();
        }

        private void OnDisable() { ClearLinks(); }

        private void OnDestroy()
        {
            for (int index = 0; index < nodes.Count; index++)
            {
                if (nodes[index] != null) nodes[index].StateChanged -= HandleNodeStateChanged;
            }
            ClearLinks();
        }
    }

    [DisallowMultipleComponent]
    public sealed class FieldLink : MonoBehaviour
    {
        private readonly Dictionary<int, float> nextForceTimes = new Dictionary<int, float>(8);
        private FieldNode first;
        private FieldNode second;
        private FieldOwnerTeam ownerTeam;
        private LayerMask affectedLayers;
        private float baseStrength;
        private float distanceOffset;
        private float distanceExponent;
        private float width;
        private Color strongColor;
        private Color weakColor;
        private BoxCollider2D trigger;
        private LineRenderer visual;
        private OblivionDiskFieldLinkVisual approvedDiskVisual;
        private Material material;
        private float strength;
        private float forceMultiplier = 1f;
        private float upkeepMultiplier = 1f;

        public float Strength => strength;
        public float UpkeepMultiplier => upkeepMultiplier;
        public FieldNetwork.FieldLinkIdentity Identity => new FieldNetwork.FieldLinkIdentity(
            first != null ? first.CreationOrder : 0UL,
            second != null ? second.CreationOrder : 0UL);
        public bool Connects(FieldNode a,FieldNode b) => (ReferenceEquals(first,a)&&ReferenceEquals(second,b))||(ReferenceEquals(first,b)&&ReferenceEquals(second,a));

        public void Configure(FieldNode a, FieldNode b, FieldOwnerTeam team, LayerMask layers, float baseForce, float offset, float exponent, float fieldWidth, Color strong, Color weak, GameObject visualPrefab = null, float temporaryForceMultiplier = 1f, float temporaryUpkeepMultiplier = 1f)
        {
            first = a; second = b; ownerTeam = team; affectedLayers = layers;
            baseStrength = baseForce; distanceOffset = offset; distanceExponent = exponent; width = fieldWidth;
            strongColor = strong; weakColor = weak;
            forceMultiplier = Mathf.Max(0f, temporaryForceMultiplier);
            upkeepMultiplier = Mathf.Max(0f, temporaryUpkeepMultiplier);
            EnsurePresentation(visualPrefab);
            RefreshStrength();
        }

        public void RefreshStrength()
        {
            if (first == null || second == null || !first.CanContribute || !second.CanContribute)
            {
                Destroy(gameObject);
                return;
            }
            Vector2 delta = (Vector2)second.transform.position - (Vector2)first.transform.position;
            float distance = delta.magnitude;
            strength = baseStrength / Mathf.Pow(distance + distanceOffset, distanceExponent);
            strength *= Mathf.Min(first.EnergyFraction, second.EnergyFraction);
            strength *= forceMultiplier;
            transform.position = ((Vector2)first.transform.position + (Vector2)second.transform.position) * .5f;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            trigger.size = new Vector2(distance, width);
            visual.SetPosition(0, first.transform.position);
            visual.SetPosition(1, second.transform.position);
            float normalized = Mathf.Clamp01(strength / Mathf.Max(.01f, baseStrength / Mathf.Pow(2f + distanceOffset, distanceExponent)));
            Color color = Color.Lerp(weakColor, strongColor, normalized);
            visual.startColor = color; visual.endColor = color;
            visual.startWidth = Mathf.Lerp(.025f, .09f, normalized);
            visual.endWidth = visual.startWidth;
            if (approvedDiskVisual != null)
            {
                approvedDiskVisual.Refresh(distance, visual.startWidth, color);
            }
        }

        public void SetTemporaryModifiers(float force, float upkeep)
        {
            forceMultiplier = Mathf.Max(0f, force);
            upkeepMultiplier = Mathf.Max(0f, upkeep);
            RefreshStrength();
        }

        private void EnsurePresentation(GameObject visualPrefab)
        {
            trigger = gameObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            if (visualPrefab != null)
            {
                GameObject instance = Instantiate(visualPrefab, transform);
                instance.name = "Field Link Visual";
                visual = instance.GetComponentInChildren<LineRenderer>(true);
                approvedDiskVisual = instance.GetComponentInChildren<OblivionDiskFieldLinkVisual>(true);
            }
            if (visual == null)
            {
                visual = gameObject.AddComponent<LineRenderer>();
                material = new Material(Shader.Find("Sprites/Default"));
                visual.material = material;
            }
            visual.useWorldSpace = true;
            visual.positionCount = 2;
            visual.sortingOrder = 7;
            if (approvedDiskVisual != null) approvedDiskVisual.Configure(visual);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (other == null || ((1 << other.gameObject.layer) & affectedLayers.value) == 0 || strength <= 0.001f) return;
            if (ownerTeam == FieldOwnerTeam.Enemy)
            {
                PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
                if (player == null) return;
                ApplyPlayerForce(player);
                return;
            }
            Damageable enemy = other.GetComponentInParent<Damageable>();
            if (enemy == null || enemy.GetComponent<EnemyArchetypeProfile>() == null) return;
            ApplyEnemyForce(enemy);
        }

        private void ApplyPlayerForce(PlayerHealth player)
        {
            int id = player.GetInstanceID();
            if (nextForceTimes.TryGetValue(id, out float next) && Time.time < next) return;
            nextForceTimes[id] = Time.time + .12f;
            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null) return;
            controller.ApplyExternalKnockback(ResolvePush(player.transform.position) * strength, .03f);
        }

        private void ApplyEnemyForce(Damageable enemy)
        {
            int id = enemy.GetInstanceID();
            if (nextForceTimes.TryGetValue(id, out float next) && Time.time < next) return;
            nextForceTimes[id] = Time.time + .12f;
            float resistance = enemy.GetComponent<FieldResistance>()?.ForceResistance ?? 0f;
            // Tank resistance remains authoritative in KnockbackReceiver; this optional
            // component is reserved for future field-specific resistance.
            enemy.GetComponent<KnockbackReceiver>()?.ApplyKnockback(
                ResolvePush(enemy.transform.position) * strength * (1f - resistance));
        }

        private Vector2 ResolvePush(Vector2 target)
        {
            Vector2 axis = ((Vector2)second.transform.position - (Vector2)first.transform.position).normalized;
            Vector2 normal = new Vector2(-axis.y, axis.x);
            return Vector2.Dot(target - (Vector2)transform.position, normal) >= 0f ? normal : -normal;
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
