using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.Player;
using UnityEngine;

namespace Cave.FieldControl
{
    public enum FieldOwnerTeam { Player, Enemy }

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
            if (!CanContribute || amount <= 0f) return;
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
        private bool topologyDirty;
        private float nextEnergyTick;

        public int ActiveNodeCount => nodes.Count;
        public int ActiveLinkCount => links.Count;
        public FieldOwnerTeam OwnerTeam => ownerTeam;

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
            RebuildIfNeeded();
            if (links.Count == 0 || Time.time < nextEnergyTick) return;
            nextEnergyTick = Time.time + energyTickInterval;
            float drain = energyDrainPerLinkedNode * energyTickInterval * links.Count;
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
            if (a == null || b == null || Vector2.Distance(a.transform.position, b.transform.position) > maximumLinkDistance) return;
            GameObject linkObject = new GameObject("Field Link");
            linkObject.transform.SetParent(transform, false);
            FieldLink link = linkObject.AddComponent<FieldLink>();
            link.Configure(a, b, ownerTeam, affectedLayers, baseStrength, distanceOffset, distanceExponent,
                segmentWidth, strongColor, weakColor, linkVisualPrefab);
            links.Add(link);
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

        public float Strength => strength;

        public void Configure(FieldNode a, FieldNode b, FieldOwnerTeam team, LayerMask layers, float baseForce, float offset, float exponent, float fieldWidth, Color strong, Color weak, GameObject visualPrefab = null)
        {
            first = a; second = b; ownerTeam = team; affectedLayers = layers;
            baseStrength = baseForce; distanceOffset = offset; distanceExponent = exponent; width = fieldWidth;
            strongColor = strong; weakColor = weak;
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
