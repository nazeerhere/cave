using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.Player;
using UnityEngine;

namespace Cave.Missions
{
    public abstract class MissionControllerBase : MonoBehaviour
    {
        protected MissionRunContext Context { get; private set; }
        public event Action<string, string> ObjectiveChanged;

        public virtual void Initialize(MissionRunContext context)
        {
            Context = context;
            context.SetState(MissionLifecycleState.Active);
        }

        protected void Publish(string title, string detail) { ObjectiveChanged?.Invoke(title, detail); }
        protected void PrimaryComplete(bool extraction)
        {
            Context.SetState(MissionLifecycleState.PrimaryObjectiveComplete);
            if (extraction)
            {
                Context.SetState(MissionLifecycleState.Extraction);
                ExtractionPoint.ActivateScenePoint(this);
            }
            else Context.SetState(MissionLifecycleState.Success);
        }
        protected void Fail(string reason) { Publish("MISSION FAILED", reason); Context.SetState(MissionLifecycleState.Failure); }
    }

    public sealed class ClassicSweepMissionController : MissionControllerBase
    {
        public override void Initialize(MissionRunContext context)
        {
            base.Initialize(context);
            Publish("CLASSIC SWEEP", "Clear the map's normal encounters.");
        }
        public void NotifyMapCleared() { PrimaryComplete(false); }
    }

    public sealed class CorruptionSourceSocket : MonoBehaviour
    {
        [SerializeField] private CorruptionSource sourcePrefab;
        public CorruptionSource Spawn()
        {
            CorruptionSource source = sourcePrefab != null
                ? Instantiate(sourcePrefab, transform.position, Quaternion.identity, transform)
                : new GameObject("Corruption Source").AddComponent<CorruptionSource>();
            if (sourcePrefab == null) source.transform.position = transform.position;
            return source;
        }
    }

    [RequireComponent(typeof(Damageable), typeof(CircleCollider2D))]
    public sealed class CorruptionSource : MonoBehaviour
    {
        private Damageable damageable;
        public event Action<CorruptionSource> Destroyed;
        private void Awake()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy"); if (enemyLayer >= 0) gameObject.layer = enemyLayer;
            CircleCollider2D collider = GetComponent<CircleCollider2D>(); collider.radius = .5f; collider.isTrigger = false;
            damageable = GetComponent<Damageable>(); damageable.SetRuntimeMaximumHealth(2, true); damageable.Died += HandleDied;
            if (GetComponent<SpriteRenderer>() == null)
            {
                SpriteRenderer visual = gameObject.AddComponent<SpriteRenderer>();
                visual.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd"); visual.color = new Color(.65f, .1f, .85f, .9f); visual.sortingOrder = 8;
                transform.localScale = Vector3.one * .75f;
            }
        }
        private void HandleDied() { Destroyed?.Invoke(this); }
        private void OnDestroy() { if (damageable != null) damageable.Died -= HandleDied; }
    }

    public sealed class CorruptionPurgeMissionController : MissionControllerBase
    {
        [SerializeField, Min(1)] private int sourceCount = 3;
        [SerializeField, Min(1f)] private float timeLimit = 240f;
        private readonly List<CorruptionSource> active = new List<CorruptionSource>(8);
        private float endsAt;
        private float nextTimerPublish;
        private int total;

        public override void Initialize(MissionRunContext context)
        {
            base.Initialize(context);
            CorruptionSourceSocket[] sockets = FindObjectsOfType<CorruptionSourceSocket>(true);
            if (sockets.Length == 0) { Fail("No corruption source sockets are authored in this map."); return; }
            System.Random random = new System.Random(context.RunSeed);
            int count = Mathf.Min(sourceCount, sockets.Length);
            for (int i = 0; i < count; i++)
            {
                int pick = i + random.Next(sockets.Length - i);
                CorruptionSourceSocket swap = sockets[i]; sockets[i] = sockets[pick]; sockets[pick] = swap;
                CorruptionSource source = sockets[i].Spawn();
                source.Destroyed += HandleDestroyed;
                active.Add(source);
            }
            total = active.Count;
            endsAt = Time.time + timeLimit;
            nextTimerPublish = Time.time;
            PublishProgress();
        }

        private void Update()
        {
            if (Context == null || Context.State != MissionLifecycleState.Active) return;
            if (Time.time >= endsAt) { Fail("Corruption overwhelmed the expedition."); return; }
            if (Time.time >= nextTimerPublish) { nextTimerPublish = Time.time + 1f; PublishProgress(); }
        }

        private void HandleDestroyed(CorruptionSource source)
        {
            source.Destroyed -= HandleDestroyed;
            active.Remove(source);
            if (active.Count == 0) PrimaryComplete(true); else PublishProgress();
        }

        private void PublishProgress() { Publish("CORRUPTION PURGE", "Sources: " + (total - active.Count) + " / " + total + "    Time: " + Mathf.CeilToInt(Mathf.Max(0f, endsAt - Time.time))); }
        private void OnDestroy() { for (int i = 0; i < active.Count; i++) if (active[i] != null) active[i].Destroyed -= HandleDestroyed; }
    }

    public sealed class BountyTarget : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float healthMultiplier = 1.5f;
        private Damageable damageable;
        public event Action<BountyTarget> Defeated;
        public void ApplyBoundedModifier()
        {
            damageable = GetComponent<Damageable>();
            if (damageable == null) return;
            damageable.SetRuntimeMaximumHealth(Mathf.CeilToInt(damageable.MaximumHealth * healthMultiplier), true);
            damageable.Died += HandleDied;
            GameObject marker = new GameObject("Bounty Marker"); marker.transform.SetParent(transform, false); marker.transform.localPosition = Vector3.up * 1.5f;
            TextMesh text = marker.AddComponent<TextMesh>(); text.text = "BOUNTY"; text.anchor = TextAnchor.MiddleCenter; text.fontSize = 36; text.characterSize = .035f; text.color = new Color(1f, .35f, .8f, 1f);
            text.GetComponent<MeshRenderer>().sortingOrder = 30;
        }
        private void HandleDied() { Defeated?.Invoke(this); }
        private void OnDestroy() { if (damageable != null) damageable.Died -= HandleDied; }
    }

    public sealed class BountyCore : MonoBehaviour
    {
        public event Action Claimed;
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerHealth>() == null) return;
            Claimed?.Invoke(); Destroy(gameObject);
        }
    }

    public sealed class CorruptBountyMissionController : MissionControllerBase
    {
        private BountyTarget target;
        public override void Initialize(MissionRunContext context)
        {
            base.Initialize(context);
            Damageable[] candidates = FindObjectsOfType<Damageable>(true);
            List<Damageable> eligible = new List<Damageable>(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!candidates[i].gameObject.activeInHierarchy || candidates[i].GetComponent<EnemyArchetypeProfile>() == null) continue;
                eligible.Add(candidates[i]);
            }
            if (eligible.Count > 0)
            {
                Damageable selected = eligible[new System.Random(context.RunSeed).Next(eligible.Count)];
                target = selected.gameObject.AddComponent<BountyTarget>();
                target.Defeated += HandleDefeated;
                target.ApplyBoundedModifier();
                Publish("CORRUPT BOUNTY", "Locate the marked target.");
                return;
            }
            Fail("No eligible bounty target exists in this map.");
        }
        private void HandleDefeated(BountyTarget defeated)
        {
            Vector3 position = defeated.transform.position;
            GameObject coreObject = new GameObject("Bounty Core"); coreObject.transform.position = position;
            CircleCollider2D trigger = coreObject.AddComponent<CircleCollider2D>(); trigger.isTrigger = true; trigger.radius = .45f;
            BountyCore core = coreObject.AddComponent<BountyCore>(); core.Claimed += HandleClaimed;
            Publish("CORRUPT BOUNTY", "Claim the bounty.");
        }
        private void HandleClaimed() { Publish("CORRUPT BOUNTY", "Reach extraction."); PrimaryComplete(true); }
    }

    public sealed class ContainmentSocket : MonoBehaviour { }

    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class ContainmentSite : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float defenseDuration = 45f;
        private float endsAt;
        private bool active;
        public event Action Completed;
        public event Action Activated;
        public bool IsActive => active;
        public float Remaining => active ? Mathf.Max(0f, endsAt - Time.time) : defenseDuration;
        private void Awake() { CircleCollider2D trigger = GetComponent<CircleCollider2D>(); trigger.isTrigger = true; trigger.radius = 1.2f; }
        public void Arm() { active = false; }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (active || other.GetComponentInParent<PlayerHealth>() == null) return;
            active = true; endsAt = Time.time + defenseDuration; Activated?.Invoke();
        }
        private void Update() { if (active && Time.time >= endsAt) { active = false; Completed?.Invoke(); } }
    }

    public sealed class ContainmentMissionController : MissionControllerBase
    {
        [SerializeField] private bool requireExtractionAfterDefense;
        private ContainmentSite site;
        private float nextPublish;
        public override void Initialize(MissionRunContext context)
        {
            base.Initialize(context);
            ContainmentSocket socket = FindObjectOfType<ContainmentSocket>();
            if (socket == null) { Fail("No containment socket is authored in this map."); return; }
            site = socket.GetComponent<ContainmentSite>();
            if (site == null) site = socket.gameObject.AddComponent<ContainmentSite>();
            site.Completed += HandleComplete; site.Activated += HandleActivated; site.Arm(); nextPublish = Time.time;
            Publish("CONTAINMENT", "Enter the containment site to begin.");
        }
        private void Update()
        {
            if (site == null || !site.IsActive || Context == null || Context.State != MissionLifecycleState.Active || Time.time < nextPublish) return;
            nextPublish = Time.time + 1f; Publish("CONTAINMENT", "Hold the site: " + Mathf.CeilToInt(site.Remaining));
        }
        private void HandleComplete() { PrimaryComplete(requireExtractionAfterDefense); }
        private void HandleActivated() { Publish("CONTAINMENT", "Hold the site."); }
        private void OnDestroy() { if (site != null) { site.Completed -= HandleComplete; site.Activated -= HandleActivated; } }
    }

    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class ExtractionPoint : MonoBehaviour
    {
        private bool activePoint;
        private void Awake() { GetComponent<Collider2D>().isTrigger = true; gameObject.SetActive(false); }
        public void Activate() { gameObject.SetActive(true); activePoint = true; }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!activePoint || other.GetComponentInParent<PlayerHealth>() == null) return;
            activePoint = false; MissionRunContext.Current.SetState(MissionLifecycleState.Success);
        }
        public static void ActivateScenePoint(Component owner)
        {
            ExtractionPoint point = FindObjectOfType<ExtractionPoint>(true);
            if (point != null) point.Activate(); else Debug.LogWarning("Mission requires extraction, but no ExtractionPoint is authored.", owner);
        }
    }
}
