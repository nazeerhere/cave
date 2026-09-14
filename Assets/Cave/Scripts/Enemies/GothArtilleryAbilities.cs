using System;
using System.Collections;
using Cave.Combat;
using Cave.FieldControl;
using Cave.Hazards;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum GothActionState { Idle, Volley, MeteorFireball, AntimatterBurst, Beam, FocusOrb, MeteorStorm, Repulse, PartitionField }
    public enum GothActionPhase { None, Startup, Active, Recovery }

    [DisallowMultipleComponent]
    public sealed class GothArtilleryAbilities : MonoBehaviour, IEnemySkillEvolutionReceiver
    {
        [Header("References")]
        [SerializeField] private EnemyShooter shooter;
        [SerializeField] private GothVfxPlayback vfx;
        [SerializeField] private Transform firePoint;
        [SerializeField] private BombProjectile meteorBombPrefab;
        [SerializeField] private HazardWarning meteorWarningPrefab;
        [SerializeField] private Transform hazardContainer;
        [SerializeField] private LayerMask playerLayers = 1;

        [Header("Approved VFX frames")]
        [SerializeField] private Sprite[] fireballFrames;
        [SerializeField] private Sprite[] fireballSequenceFrames;
        [SerializeField] private Sprite[] meteorFrames;
        [SerializeField] private Sprite[] antimatterPreludeFrames;
        [SerializeField] private Sprite[] burstFrames;
        [SerializeField] private Sprite[] beamStartFrames;
        [SerializeField] private Sprite[] beamLoopFrames;
        [SerializeField] private Sprite[] beamEndFrames;
        [SerializeField] private Sprite[] focusTravelFrames;
        [SerializeField] private Sprite[] focusChargeFrames;
        [SerializeField] private Sprite[] meteorStormFrames;

        [Header("High pressure tuning")]
        [SerializeField, Min(0.01f)] private float volleySpacing = .18f;
        [SerializeField, Min(0.1f)] private float volleyCooldown = 3.5f;
        [SerializeField, Min(0.1f)] private float meteorCooldown = 7f;
        [SerializeField, Min(0.1f)] private float burstCooldown = 6f;
        [SerializeField, Min(0.1f)] private float beamCooldown = 9f;
        [SerializeField, Min(0.1f)] private float focusCooldown = 12f;
        [SerializeField, Min(0.1f)] private float stormCooldown = 22f;

        [Header("Additive Field Control")]
        [SerializeField] private LayerMask fieldTargetLayers = 1;
        [SerializeField, Min(0.1f)] private float repulseRadius = 2.35f;
        [SerializeField, Min(0f)] private float repulseForce = 8f;
        [SerializeField, Min(0)] private int repulseDamage;
        [SerializeField, Min(0f)] private float repulseWindup = .28f;
        [SerializeField, Min(0f)] private float repulseRecovery = .22f;
        [SerializeField, Min(0.1f)] private float repulseCooldown = 7f;
        [SerializeField, Min(0.1f)] private float partitionCooldown = 11f;
        [SerializeField, Min(0f)] private float partitionWindup = .45f;
        [SerializeField, Min(0.1f)] private float partitionAnchorSpacing = 4f;
        [SerializeField, Min(0.1f)] private float partitionLifetime = 8f;
        [SerializeField, Min(1)] private int partitionAnchorHealth = 4;
        [SerializeField, Min(0.1f)] private float partitionAnchorEnergy = 7f;
        [SerializeField, Min(0.1f)] private float partitionMaximumDistance = 7f;
        [SerializeField, Min(0.01f)] private float partitionBaseStrength = 13f;
        [SerializeField, Min(0.01f)] private float partitionDistanceOffset = 1f;
        [SerializeField, Min(0.1f)] private float partitionDistanceExponent = 1.2f;
        [Header("Editable Field Presentation")]
        [SerializeField] private GameObject repulseVisualPrefab;
        [SerializeField] private GameObject partitionAnchorVisualPrefab;
        [SerializeField] private GameObject partitionLinkVisualPrefab;

        private EnemyEvolutionStage evolution;
        private float nextVolley;
        private float nextMeteor;
        private float nextBurst;
        private float nextBeam;
        private float nextFocus;
        private float nextStorm;
        private float nextRepulse;
        private float nextPartition;
        private Coroutine actionRoutine;
        private int activeMeteorSequences;
        private FieldNetwork partitionNetwork;
        private readonly System.Collections.Generic.List<FieldNode> partitionNodes = new System.Collections.Generic.List<FieldNode>(2);
        public GothActionState CurrentAction { get; private set; }
        public GothActionPhase CurrentPhase { get; private set; }
        public bool IsBusy => actionRoutine != null;
        public event Action<GothActionState> ActionChanged;

        private void Awake()
        {
            shooter = shooter != null ? shooter : GetComponent<EnemyShooter>();
            vfx = vfx != null ? vfx : GetComponent<GothVfxPlayback>();
            firePoint = firePoint != null ? firePoint : transform;
            partitionNetwork = GetComponent<FieldNetwork>();
            if (partitionNetwork == null) partitionNetwork = gameObject.AddComponent<FieldNetwork>();
            partitionNetwork.Configure(FieldOwnerTeam.Enemy, fieldTargetLayers, 2, partitionMaximumDistance,
                partitionBaseStrength, partitionDistanceOffset, partitionDistanceExponent);
            partitionNetwork.SetLinkVisualPrefab(partitionLinkVisualPrefab);
        }

        public void ApplyEvolution(EnemyEvolutionStage stage) => evolution = stage;

        public bool TryVolley(Transform target)
        {
            if (IsBusy || Time.time < nextVolley || target == null) return false;
            nextVolley = Time.time + volleyCooldown;
            Begin(GothActionState.Volley, VolleyRoutine(target));
            return true;
        }

        public bool TryMeteorFireball(Transform target)
        {
            if (IsBusy || Time.time < nextMeteor || target == null) return false;
            nextMeteor = Time.time + meteorCooldown;
            Begin(GothActionState.MeteorFireball, MeteorRoutine(target));
            return true;
        }

        public bool TryBurst(Transform target)
        {
            if (IsBusy || Time.time < nextBurst || target == null) return false;
            nextBurst = Time.time + burstCooldown;
            Begin(GothActionState.AntimatterBurst, BurstRoutine(target));
            return true;
        }

        public bool TryBeam(Transform target)
        {
            if (IsBusy || Time.time < nextBeam || target == null) return false;
            nextBeam = Time.time + beamCooldown;
            Begin(GothActionState.Beam, BeamRoutine(target));
            return true;
        }

        public bool TryFocusOrb(Transform target)
        {
            if (IsBusy || Time.time < nextFocus || target == null) return false;
            nextFocus = Time.time + focusCooldown;
            Begin(GothActionState.FocusOrb, FocusRoutine(target));
            return true;
        }

        public bool TryMeteorStorm(Transform target)
        {
            if (evolution != EnemyEvolutionStage.EvolutionTwo || IsBusy || Time.time < nextStorm || target == null) return false;
            nextStorm = Time.time + stormCooldown;
            Begin(GothActionState.MeteorStorm, StormRoutine(target));
            return true;
        }

        public bool TryRepulse(Transform target)
        {
            if (IsBusy || Time.time < nextRepulse || target == null) return false;
            nextRepulse = Time.time + repulseCooldown;
            Begin(GothActionState.Repulse, RepulseRoutine(target));
            return true;
        }

        public bool TryPartitionField(Transform target)
        {
            PrunePartitionNodes();
            if (IsBusy || Time.time < nextPartition || target == null || partitionNodes.Count > 0) return false;
            nextPartition = Time.time + partitionCooldown;
            Begin(GothActionState.PartitionField, PartitionRoutine(target));
            return true;
        }

        private void Begin(GothActionState state, IEnumerator routine)
        {
            CurrentAction = state;
            CurrentPhase = GothActionPhase.Startup;
            ActionChanged?.Invoke(state);
            actionRoutine = StartCoroutine(Complete(routine));
        }

        private IEnumerator Complete(IEnumerator routine)
        {
            yield return StartCoroutine(routine);
            CurrentAction = GothActionState.Idle;
            CurrentPhase = GothActionPhase.None;
            actionRoutine = null;
            ActionChanged?.Invoke(CurrentAction);
        }

        private IEnumerator VolleyRoutine(Transform target)
        {
            for (int index = 0; index < 4; index++)
            {
                if (target == null) yield break;
                CurrentPhase = GothActionPhase.Active;
                Sprite[] volleyFrames = index % 2 == 0 || fireballSequenceFrames == null || fireballSequenceFrames.Length == 0
                    ? fireballFrames : fireballSequenceFrames;
                float travel = Vector2.Distance(firePoint.position, target.position) / Mathf.Max(.01f, shooter != null ? shooter.BaseProjectileSpeed : 5f);
                vfx?.PlayTravel(volleyFrames, firePoint.position, target.position, travel, 12f, .65f);
                shooter?.TryFireImmediate(target);
                yield return new WaitForSeconds(volleySpacing);
            }
        }

        private IEnumerator MeteorRoutine(Transform target)
        {
            vfx?.Play(meteorFrames, firePoint.position, 12f, 1.1f);
            yield return new WaitForSeconds(.7f);
            if (target == null) yield break;
            CurrentPhase = GothActionPhase.Active;
            Vector2 impactPoint = target.position;
            float travel = Vector2.Distance(firePoint.position, impactPoint) / 6.5f;
            vfx?.PlayTravel(meteorFrames, firePoint.position, impactPoint, travel, 12f, 1.1f);
            shooter?.TryFireImmediate(target, .38f, 2f);
            yield return new WaitForSeconds(Mathf.Max(.05f, travel));
            DamageInRadius(impactPoint, 1.2f, 2);
        }

        private IEnumerator BurstRoutine(Transform target)
        {
            vfx?.Play(antimatterPreludeFrames, firePoint.position, 12f, .85f);
            yield return new WaitForSeconds(.65f);
            CurrentPhase = GothActionPhase.Active;
            vfx?.Play(burstFrames, target != null ? target.position : transform.position, 12f, 1.1f);
            DamageInRadius(target != null ? target.position : transform.position, 2f, 2);
        }

        private IEnumerator BeamRoutine(Transform target)
        {
            vfx?.Play(antimatterPreludeFrames, firePoint.position, 12f, .8f);
            float trackingEnds = Time.time + .85f;
            Vector2 direction = Vector2.right;
            while (Time.time < trackingEnds)
            {
                if (target != null) direction = ((Vector2)target.position - (Vector2)firePoint.position).normalized;
                yield return null;
            }
            CurrentPhase = GothActionPhase.Active;
            vfx?.PlayBeam(beamStartFrames, beamLoopFrames, beamEndFrames, firePoint.position + (Vector3)direction * 1.2f,
                direction, 10f, 1.2f, 1.6f);
            float activeEnds = Time.time + 1.6f;
            float nextTick = Time.time;
            while (Time.time < activeEnds)
            {
                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + .4f;
                    DamageBeam(direction);
                }
                yield return null;
            }
            CurrentPhase = GothActionPhase.Recovery;
            yield return new WaitForSeconds(.3f);
        }

        private IEnumerator FocusRoutine(Transform target)
        {
            Vector2 endpoint = target != null ? target.position : transform.position;
            Vector2 start = firePoint.position;
            float distance = Vector2.Distance(start, endpoint);
            float travel = distance / 1.6f;
            vfx?.PlayTravel(focusTravelFrames, start, endpoint, travel, 10f, .8f);
            yield return new WaitForSeconds(travel);
            CurrentPhase = GothActionPhase.Active;
            vfx?.Play(focusChargeFrames, endpoint, focusChargeFrames != null ? focusChargeFrames.Length / 2.5f : 3f, 1f);
            yield return new WaitForSeconds(2.5f);
            vfx?.Play(antimatterPreludeFrames, endpoint, 12f, .9f);
            CreateScorchedPath(start, endpoint);
        }

        private IEnumerator StormRoutine(Transform target)
        {
            CurrentPhase = GothActionPhase.Active;
            vfx?.Play(meteorStormFrames, transform.position, 10f, 1.3f, true, 12f);
            for (int index = 0; index < 8; index++)
            {
                while (activeMeteorSequences >= 3)
                {
                    yield return null;
                }
                Vector3 at = target != null && index % 2 == 0
                    ? target.position + new Vector3(UnityEngine.Random.Range(-1.6f, 1.6f), 0f)
                    : transform.position + new Vector3(UnityEngine.Random.Range(-5f, 5f), 0f);
                StartCoroutine(StormBombSequence(at));
                yield return new WaitForSeconds(1.35f);
            }
            yield return new WaitForSeconds(1.2f);
        }

        private IEnumerator RepulseRoutine(Transform target)
        {
            Vector2 center = transform.position;
            AreaPulseEffect.Create(center, repulseRadius * .35f, new Color(.25f, .75f, 1f, .55f), repulseWindup);
            yield return new WaitForSeconds(repulseWindup);
            CurrentPhase = GothActionPhase.Active;
            SpawnPresentation(repulseVisualPrefab, center, repulseWindup + .35f);
            AreaPulseEffect.Create(center, repulseRadius, new Color(.75f, .25f, 1f, .75f), .25f);
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, repulseRadius, fieldTargetLayers);
            for (int index = 0; index < hits.Length; index++)
            {
                PlayerHealth player = hits[index].GetComponentInParent<PlayerHealth>();
                if (player == null) continue;
                Vector2 away = (Vector2)player.transform.position - center;
                if (away.sqrMagnitude < .001f) away = Vector2.right;
                player.GetComponent<PlayerController>()?.ApplyExternalKnockback(away.normalized * repulseForce, .08f);
                if (repulseDamage > 0) player.TryTakeDamage(repulseDamage,
                    new DamageContext(gameObject, DamageTrait.Direct | DamageTrait.AreaOfEffect));
            }
            CurrentPhase = GothActionPhase.Recovery;
            yield return new WaitForSeconds(repulseRecovery);
        }

        private IEnumerator PartitionRoutine(Transform target)
        {
            vfx?.Play(antimatterPreludeFrames, firePoint.position, 12f, .75f);
            yield return new WaitForSeconds(partitionWindup);
            if (target == null) yield break;
            CurrentPhase = GothActionPhase.Active;
            Vector2 center = target.position;
            CreatePartitionAnchor(center + Vector2.left * (partitionAnchorSpacing * .5f));
            CreatePartitionAnchor(center + Vector2.right * (partitionAnchorSpacing * .5f));
            AreaPulseEffect.Create(center, partitionAnchorSpacing * .5f, new Color(.55f, .2f, 1f, .6f), .3f);
            yield return new WaitForSeconds(.25f);
            CurrentPhase = GothActionPhase.Recovery;
        }

        private void CreatePartitionAnchor(Vector2 position)
        {
            GameObject anchor = new GameObject("Anti-Pyre Partition Anchor");
            anchor.transform.position = position;
            CircleCollider2D collider = anchor.AddComponent<CircleCollider2D>();
            collider.radius = .22f;
            collider.isTrigger = true;
            if (partitionAnchorVisualPrefab != null)
            {
                GameObject visual = Instantiate(partitionAnchorVisualPrefab, anchor.transform);
                visual.name = "Partition Anchor Visual";
            }
            FieldNode node = anchor.AddComponent<FieldNode>();
            node.Configure(partitionNetwork, FieldOwnerTeam.Enemy, partitionAnchorHealth,
                partitionAnchorEnergy, partitionLifetime, (ulong)(Time.frameCount + partitionNodes.Count));
            partitionNodes.Add(node);
        }

        private static void SpawnPresentation(GameObject prefab, Vector3 position, float lifetime)
        {
            if (prefab == null) return;
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            Destroy(instance, Mathf.Max(0.05f, lifetime));
        }

        private void PrunePartitionNodes()
        {
            for (int index = partitionNodes.Count - 1; index >= 0; index--)
            {
                if (partitionNodes[index] == null || !partitionNodes[index].gameObject.activeInHierarchy)
                    partitionNodes.RemoveAt(index);
            }
        }

        private void ClearPartition()
        {
            for (int index = 0; index < partitionNodes.Count; index++)
            {
                if (partitionNodes[index] != null) Destroy(partitionNodes[index].gameObject);
            }
            partitionNodes.Clear();
        }

        private IEnumerator StormBombSequence(Vector3 at)
        {
            activeMeteorSequences++;
            if (meteorBombPrefab == null || meteorWarningPrefab == null)
            {
                Debug.LogWarning("[Cave] Goth Meteor Storm requires the existing Bomb and BombWarning prefabs.", this);
                activeMeteorSequences--;
                yield break;
            }
            HazardWarning warning = Instantiate(meteorWarningPrefab, at, Quaternion.identity, hazardContainer);
            yield return new WaitForSeconds(1f);
            BombProjectile bomb = Instantiate(meteorBombPrefab, at + Vector3.up * 6f, Quaternion.identity, hazardContainer);
            bomb.Initialize(warning, () => activeMeteorSequences = Mathf.Max(0, activeMeteorSequences - 1));
        }

        private void CreateScorchedPath(Vector2 start, Vector2 end)
        {
            GameObject path = new GameObject("Goth Focus Scorched Path");
            GothScorchedPath effect = path.AddComponent<GothScorchedPath>();
            effect.Initialize(gameObject, start, end, playerLayers, 8f, .5f, .65f);
        }

        private void DamageBeam(Vector2 direction)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(firePoint.position + (Vector3)direction * 2f, 2.1f, playerLayers);
            for (int index = 0; index < hits.Length; index++)
            {
                PlayerHealth health = hits[index].GetComponentInParent<PlayerHealth>();
                if (health == null) continue;
                Vector2 toPlayer = (Vector2)health.transform.position - (Vector2)firePoint.position;
                if (Vector2.Dot(toPlayer.normalized, direction) < .7f) continue;
                health.TryTakeDamage(1, new DamageContext(gameObject, DamageTrait.Direct | DamageTrait.AreaOfEffect));
                Rigidbody2D playerBody = health.GetComponent<Rigidbody2D>();
                if (playerBody == null || Vector2.Dot(playerBody.velocity, direction) >= -.1f)
                {
                    health.GetComponent<PlayerController>()?.ApplyExternalKnockback(direction * 2.1f, .05f);
                }
            }
        }

        private void DamageInRadius(Vector2 position, float radius, int damage)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius, playerLayers);
            for (int index = 0; index < hits.Length; index++)
            {
                hits[index].GetComponentInParent<PlayerHealth>()?.TryTakeDamage(damage,
                    new DamageContext(gameObject, DamageTrait.Direct | DamageTrait.AreaOfEffect));
            }
        }

        public void Configure(EnemyShooter configuredShooter, GothVfxPlayback configuredVfx, Transform configuredFirePoint,
            BombProjectile configuredBomb, HazardWarning configuredWarning, Transform configuredHazardContainer,
            Sprite[] fireball, Sprite[] fireballSequence, Sprite[] meteor, Sprite[] prelude, Sprite[] burst, Sprite[] beamStart, Sprite[] beamLoop,
            Sprite[] beamEnd, Sprite[] focusTravel, Sprite[] focusCharge, Sprite[] storm)
        {
            shooter = configuredShooter; vfx = configuredVfx; firePoint = configuredFirePoint;
            meteorBombPrefab = configuredBomb; meteorWarningPrefab = configuredWarning; hazardContainer = configuredHazardContainer;
            fireballFrames = fireball; fireballSequenceFrames = fireballSequence; meteorFrames = meteor; antimatterPreludeFrames = prelude; burstFrames = burst;
            beamStartFrames = beamStart; beamLoopFrames = beamLoop; beamEndFrames = beamEnd;
            focusTravelFrames = focusTravel; focusChargeFrames = focusCharge; meteorStormFrames = storm;
        }

        private void OnDisable() { ClearPartition(); }
    }

    public sealed class GothScorchedPath : MonoBehaviour
    {
        private GameObject owner; private Vector2 start; private Vector2 end; private LayerMask playerLayers; private float width; private float cadence; private float nextTick;
        public void Initialize(GameObject source, Vector2 from, Vector2 to, LayerMask mask, float lifetime, float pathWidth, float cadence)
        { owner = source; start = from; end = to; playerLayers = mask; width = pathWidth; this.cadence = Mathf.Max(.01f, cadence); nextTick = Time.time; Invoke(nameof(Expire), lifetime); }
        private void Update()
        { if (Time.time < nextTick) return; nextTick = Time.time + cadence; Collider2D[] hits = Physics2D.OverlapCircleAll((start + end) * .5f, Vector2.Distance(start,end) * .5f + width, playerLayers); for (int i=0;i<hits.Length;i++) { Vector2 p=hits[i].transform.position; Vector2 line=end-start; float t=line.sqrMagnitude > .001f ? Mathf.Clamp01(Vector2.Dot(p-start,line)/line.sqrMagnitude) : 0f; if (Vector2.Distance(p,start+line*t)<=width) hits[i].GetComponentInParent<PlayerHealth>()?.TryTakeDamage(1,new DamageContext(owner,DamageTrait.Direct|DamageTrait.AreaOfEffect)); } }
        private void Expire(){ Destroy(gameObject); }
    }
}
