using System;
using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Environmental;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum NymrathState { Tending, Alert, Restoring, Reinforcing, Burrowing, Ambushing, Attacking, Interrupted }

    /// <summary>
    /// Shared, separately-spawnable Nymrath/Qilin ability authority. Environment
    /// ownership remains with EnvironmentalDestructible; this component only
    /// requests bounded restoration or structural impacts through its public APIs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyController))]
    public sealed class NymrathCore : MonoBehaviour, IEnemyInterruptible
    {
        private static readonly Collider2D[] EnvironmentalResults = new Collider2D[24];
        private static readonly Collider2D[] PlayerResults = new Collider2D[8];

        [Header("Variant")]
        [SerializeField] private bool corruptedQilin;
        [Header("Awareness / Restoration")]
        [SerializeField, Min(.1f)] private float territorialRadius = 8f;
        [SerializeField, Min(.1f)] private float environmentScanInterval = .5f;
        [SerializeField, Min(.1f)] private float restorationRange = 5f;
        [SerializeField, Min(.01f)] private float restorationDuration = 2.4f;
        [SerializeField, Min(.01f)] private float restorationPerSecond = .8f;
        [SerializeField] private LayerMask environmentLayers = ~0;
        [SerializeField, Min(.01f)] private float bastionDuration = 3f;
        [SerializeField, Min(.01f)] private float bastionWindup = .35f;
        [SerializeField, Range(0f, 1f)] private float bastionImpactMultiplier = .45f;
        [Header("Defense")]
        [SerializeField, Min(.1f)] private float tendrilRange = 4.5f;
        [SerializeField, Min(1)] private int tendrilDamage = 1;
        [SerializeField, Min(0f)] private float tendrilKnockback = 3f;
        [SerializeField, Min(.01f)] private float tendrilWindup = .35f;
        [SerializeField, Min(.1f)] private float burrowRange = 4f;
        [SerializeField, Min(.01f)] private float burrowDuration = .55f;
        [SerializeField] private LayerMask burrowGroundLayers;
        [SerializeField, Min(.1f)] private float ambushRadius = 1.6f;
        [SerializeField, Min(1)] private int ambushDamage = 1;
        [SerializeField, Min(0f)] private float ambushKnockback = 5f;
        [Header("Qilin")]
        [SerializeField, Min(.1f)] private float bloomRadius = 6f;
        [SerializeField, Min(1)] private int bloomTargetLimit = 4;
        [SerializeField, Min(.01f)] private float bloomDuration = 2f;
        [SerializeField, Min(.01f)] private float bloomPerSecond = 1.6f;
        [SerializeField, Min(.1f)] private float stampedeDuration = .7f;
        [SerializeField, Min(.1f)] private float stampedeSpeed = 6f;
        [SerializeField, Min(1)] private int stampedeDamage = 2;
        [SerializeField, Min(0f)] private float stampedeKnockback = 8f;
        [SerializeField, Min(0f)] private float stampedeStructuralForce = 1.25f;
        [SerializeField, Min(.01f)] private float preservationPrisonDuration = 1.1f;

        private readonly HashSet<int> stampedeHits = new HashSet<int>();
        private readonly HashSet<int> restorationCandidateIds = new HashSet<int>();
        private readonly EnvironmentalDestructible[] restorationCandidates = new EnvironmentalDestructible[24];
        private EnemyController movement;
        private Collider2D[] bodyColliders;
        private Renderer[] renderers;
        private Coroutine action;
        private PlayerController prisonTargetController;
        private float nextScanAt;
        private EnvironmentalDestructible restorationTarget;
        [SerializeField] private NymrathState currentState;
        public bool IsCorruptedQilin => corruptedQilin;
        public bool IsBusy => action != null;
        public NymrathState CurrentState => currentState;
        public EnvironmentalDestructible RestorationTarget => restorationTarget;

        public event Action RestorationWeaveStarted;
        public event Action RestorationWeaveCompleted;
        public event Action LivingBastionActivated;
        public event Action BurrowEntered;
        public event Action BurrowEmerged;
        public event Action BurrowAmbushResolved;
        public event Action EternalBloomStarted;
        public event Action PreservationPrisonApplied;
        public event Action SpectralStampedeCommitted;
        public event Action TendrilLashStarted;

        private void Awake()
        {
            movement = GetComponent<EnemyController>();
            bodyColliders = GetComponentsInChildren<Collider2D>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            currentState = NymrathState.Tending;
        }

        private void Update()
        {
            if (action != null || Time.time < nextScanAt) return;
            nextScanAt = Time.time + environmentScanInterval;
            restorationTarget = FindRestorationTarget(territorialRadius);
            currentState = restorationTarget != null ? NymrathState.Alert : NymrathState.Tending;
        }

        public bool TryRestorationWeave()
        {
            if (action != null) return false;
            EnvironmentalDestructible target = restorationTarget ?? FindRestorationTarget(restorationRange);
            if (target == null || Vector2.Distance(transform.position, target.transform.position) > restorationRange) return false;
            restorationCandidates[0] = target;
            action = StartCoroutine(RestoreRoutine(restorationCandidates, 1, restorationDuration, restorationPerSecond, false));
            return true;
        }

        public bool TryLivingBastion()
        {
            if (action != null) return false;
            EnvironmentalDestructible target = FindReinforcementTarget(restorationRange);
            if (target == null) return false;
            // This protects an existing, still-living structure; it does not
            // create geometry or turn restoration into a renamed heal.
            EnvironmentalReinforcement reinforcement = target.GetComponent<EnvironmentalReinforcement>();
            if (reinforcement == null)
            {
                reinforcement = target.gameObject.AddComponent<EnvironmentalReinforcement>();
            }

            action = StartCoroutine(LivingBastionRoutine(reinforcement));
            return true;
        }

        public bool TryTendrilLash(PlayerHealth player)
        {
            if (action != null || player == null || Vector2.Distance(transform.position, player.transform.position) > tendrilRange) return false;
            action = StartCoroutine(TendrilRoutine(player));
            TendrilLashStarted?.Invoke();
            return true;
        }

        public bool TryBurrowAmbush(Vector2 destination)
        {
            if (action != null || !IsValidBurrowDestination(destination)) return false;
            action = StartCoroutine(BurrowRoutine(destination, true));
            return true;
        }

        public bool TryEternalBloom()
        {
            if (!corruptedQilin || action != null) return false;
            int targetCount;
            FindRestorationTargets(bloomRadius, bloomTargetLimit, out targetCount);
            if (targetCount < 2) return false;
            action = StartCoroutine(RestoreRoutine(restorationCandidates, targetCount, bloomDuration, bloomPerSecond, true));
            return true;
        }

        public bool TryPreservationPrison(PlayerHealth player)
        {
            if (!corruptedQilin || action != null || player == null
                || player.GetComponent<PlayerController>() == null) return false;
            action = StartCoroutine(PreservationPrisonRoutine(player));
            return true;
        }

        public bool TrySpectralStampede(float direction)
        {
            if (!corruptedQilin || action != null || Mathf.Abs(direction) < .01f) return false;
            action = StartCoroutine(StampedeRoutine(Mathf.Sign(direction)));
            return true;
        }

        public void Interrupt()
        {
            if (action != null) StopCoroutine(action);
            action = null;
            ReleasePreservationPrison();
            SetBurrowHidden(false);
            movement?.CancelCommittedMovement();
            currentState = NymrathState.Interrupted;
        }

        private IEnumerator TendrilRoutine(PlayerHealth player)
        {
            currentState = NymrathState.Attacking;
            yield return new WaitForSeconds(tendrilWindup);
            if (player != null && Vector2.Distance(transform.position, player.transform.position) <= tendrilRange)
            {
                player.TryTakeDamage(tendrilDamage, new DamageContext(gameObject, DamageTrait.Melee));
                Vector2 away = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
                player.GetComponent<PlayerController>()?.ApplyExternalKnockback(away * tendrilKnockback, .12f);
            }
            action = null; currentState = NymrathState.Alert;
        }

        private IEnumerator PreservationPrisonRoutine(PlayerHealth player)
        {
            currentState = NymrathState.Attacking;
            prisonTargetController = player != null ? player.GetComponent<PlayerController>() : null;
            if (prisonTargetController == null)
            {
                action = null;
                currentState = NymrathState.Alert;
                yield break;
            }

            prisonTargetController.SetScopedMovementLock(this, true);
            PreservationPrisonApplied?.Invoke();
            float endsAt = Time.time + preservationPrisonDuration;
            while (player != null && player.CurrentHealth > 0 && Time.time < endsAt)
            {
                yield return null;
            }

            ReleasePreservationPrison();
            action = null;
            currentState = NymrathState.Alert;
        }

        private IEnumerator LivingBastionRoutine(EnvironmentalReinforcement reinforcement)
        {
            currentState = NymrathState.Reinforcing;
            if (reinforcement != null)
            {
                reinforcement.Apply(this, bastionDuration, bastionImpactMultiplier);
                LivingBastionActivated?.Invoke();
            }

            yield return new WaitForSeconds(bastionWindup);
            action = null;
            currentState = NymrathState.Alert;
        }

        private IEnumerator BurrowRoutine(Vector2 destination, bool ambush)
        {
            currentState = NymrathState.Burrowing;
            BurrowEntered?.Invoke(); SetBurrowHidden(true);
            yield return new WaitForSeconds(burrowDuration * .5f);
            transform.position = destination;
            yield return new WaitForSeconds(burrowDuration * .5f);
            SetBurrowHidden(false); BurrowEmerged?.Invoke();
            if (ambush) ResolveAmbush();
            action = null; currentState = NymrathState.Alert;
        }

        private IEnumerator RestoreRoutine(EnvironmentalDestructible[] targets, int targetCount, float duration, float perSecond, bool bloom)
        {
            currentState = NymrathState.Restoring;
            if (bloom) EternalBloomStarted?.Invoke(); else RestorationWeaveStarted?.Invoke();
            float remaining = duration;
            while (remaining > 0f)
            {
                for (int i = 0; i < targetCount; i++)
                    if (targets[i] != null && targets[i].State != StructuralDamageState.Broken)
                        targets[i].TryRestoreIntegrity(perSecond * Time.deltaTime);
                remaining -= Time.deltaTime;
                yield return null;
            }
            RestorationWeaveCompleted?.Invoke(); action = null; currentState = NymrathState.Tending;
        }

        private IEnumerator StampedeRoutine(float direction)
        {
            currentState = NymrathState.Attacking; stampedeHits.Clear();
            SpectralStampedeCommitted?.Invoke(); movement?.BeginCommittedMovement(direction, stampedeSpeed, stampedeDuration);
            StructuralImpactRouter.ApplyArea(new StructuralImpactContext(
                gameObject, transform.position, Vector2.right * direction,
                stampedeStructuralForce,
                new DamageContext(gameObject, DamageTrait.Melee | DamageTrait.Heavy),
                false, true, GetInstanceID() ^ Time.frameCount), 1f);
            float ends = Time.time + stampedeDuration;
            while (Time.time < ends)
            {
                int count = Physics2D.OverlapCircleNonAlloc(transform.position, 1f, PlayerResults);
                for (int i = 0; i < count; i++)
                {
                    PlayerHealth player = PlayerResults[i] != null ? PlayerResults[i].GetComponentInParent<PlayerHealth>() : null;
                    if (player != null && stampedeHits.Add(player.GetInstanceID()))
                    {
                        player.TryTakeDamage(stampedeDamage, new DamageContext(gameObject, DamageTrait.Melee | DamageTrait.Heavy));
                        player.GetComponent<PlayerController>()?.ApplyExternalKnockback(Vector2.right * direction * stampedeKnockback, .18f);
                    }
                }
                yield return null;
            }
            movement?.CancelCommittedMovement(); action = null; currentState = NymrathState.Alert;
        }

        private void ResolveAmbush()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, ambushRadius, PlayerResults);
            for (int i = 0; i < count; i++)
            {
                PlayerHealth player = PlayerResults[i] != null ? PlayerResults[i].GetComponentInParent<PlayerHealth>() : null;
                if (player == null) continue;
                Vector2 away = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
                player.TryTakeDamage(ambushDamage, new DamageContext(gameObject, DamageTrait.Melee | DamageTrait.AreaOfEffect));
                player.GetComponent<PlayerController>()?.ApplyExternalKnockback(away * ambushKnockback, .15f);
            }
            BurrowAmbushResolved?.Invoke();
        }

        private EnvironmentalDestructible FindRestorationTarget(float radius)
        {
            int count;
            FindRestorationTargets(radius, 1, out count);
            return count > 0 ? restorationCandidates[0] : null;
        }

        private EnvironmentalDestructible FindReinforcementTarget(float radius)
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, radius, EnvironmentalResults, environmentLayers);
            restorationCandidateIds.Clear();
            for (int index = 0; index < count; index++)
            {
                EnvironmentalDestructible target = EnvironmentalResults[index] != null
                    ? EnvironmentalResults[index].GetComponentInParent<EnvironmentalDestructible>()
                    : null;
                if (target == null || target.State == StructuralDamageState.Broken
                    || !restorationCandidateIds.Add(target.GetInstanceID()))
                {
                    continue;
                }

                EnvironmentalFlora flora = target.GetComponentInParent<EnvironmentalFlora>();
                if (flora != null && !flora.PermanentlyDead)
                {
                    return target;
                }
            }

            return null;
        }

        private void FindRestorationTargets(float radius, int limit, out int accepted)
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, radius, EnvironmentalResults, environmentLayers);
            int maximum = Mathf.Min(restorationCandidates.Length, Mathf.Max(0, limit));
            accepted = 0;
            restorationCandidateIds.Clear();
            for (int i = 0; i < count && accepted < maximum; i++)
            {
                EnvironmentalDestructible target = EnvironmentalResults[i] != null ? EnvironmentalResults[i].GetComponentInParent<EnvironmentalDestructible>() : null;
                if (target == null || target.State == StructuralDamageState.Intact || target.State == StructuralDamageState.Broken) continue;
                if (!restorationCandidateIds.Add(target.GetInstanceID())) continue;
                EnvironmentalFlora flora = target.GetComponentInParent<EnvironmentalFlora>();
                if (flora != null && flora.PermanentlyDead) continue;
                restorationCandidates[accepted++] = target;
            }
        }

        private bool IsValidBurrowDestination(Vector2 destination)
        {
            return Vector2.Distance(transform.position, destination) <= burrowRange
                && burrowGroundLayers.value != 0
                && Physics2D.OverlapCircle(destination, .3f, burrowGroundLayers) != null
                && Physics2D.OverlapCircleNonAlloc(destination, .3f, PlayerResults) == 0;
        }

        private void SetBurrowHidden(bool hidden)
        {
            if (bodyColliders != null) for (int i = 0; i < bodyColliders.Length; i++) if (bodyColliders[i] != null) bodyColliders[i].enabled = !hidden;
            if (renderers != null) for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = !hidden;
        }

        private void ReleasePreservationPrison()
        {
            if (prisonTargetController == null)
            {
                return;
            }

            prisonTargetController.SetScopedMovementLock(this, false);
            prisonTargetController = null;
        }

        private void OnDisable() { Interrupt(); }
    }
}
