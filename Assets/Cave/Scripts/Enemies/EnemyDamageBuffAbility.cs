using System.Collections;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable), typeof(EnemyArchetypeProfile))]
    public sealed class EnemyDamageBuffAbility : MonoBehaviour,
        IEnemyInterruptible,
        IEnemySkillEvolutionReceiver
    {
        [SerializeField] private EnemyDamageModifierType buffType = EnemyDamageModifierType.NecromancerBuff;
        [SerializeField, Range(0f, 2f)] private float additiveDamageBonus = 0.25f;
        [SerializeField, Min(0.1f)] private float buffRange = 5f;
        [SerializeField, Min(0.1f)] private float buffDuration = 6f;
        [SerializeField, Min(0f)] private float buffCooldown = 4f;
        [SerializeField, Min(0f)] private float castWindup = 0.35f;
        [SerializeField] private LayerMask allyLayers = ~0;
        [SerializeField] private bool canBuffSelf;
        [SerializeField] private bool preferNonSupportTargets = true;
        [SerializeField] private Color castColor = new Color(0.72f, 0.25f, 1f, 0.85f);
        [SerializeField] private GameObject castVfxPrefab;
        [SerializeField] private GameObject targetBuffVfxPrefab;

        [SerializeField] private Vector2 castVfxOffset = Vector2.zero;
        [SerializeField] private Vector2 targetBuffVfxOffset = Vector2.zero;

        [SerializeField] private float castVfxScale = 1f;
        [SerializeField] private float targetBuffVfxScale = 1f;
        [SerializeField, Min(0f)] private float targetBuffVfxBehindZOffset = 0.05f;
        [SerializeField, Min(0f)] private float targetBuffVfxCleanupDelay = 0.75f;

        [Header("Necromancer Support Priority")]
        [SerializeField, Min(0f)] private float ownedSkeletonBonus = 0.25f;
        [SerializeField, Min(0f)] private float generalRankBonus = 5f;
        [SerializeField, Min(0f)] private float witnessedDeathWeight = 0.3f;
        [SerializeField, Min(0f)] private float resolvedStrengthWeight = 0.75f;
        [SerializeField, Min(0f)] private float tankArchetypeBonus = 2.5f;
        [SerializeField, Min(0f)] private float bruteCombatBonus = 1.25f;
        [SerializeField, Min(0f)] private float meleeArchetypeBonus = 1.25f;
        [SerializeField, Min(0f)] private float rangedArchetypeBonus = 1f;
        [SerializeField, Min(0f)] private float maximumDistancePenalty = 0.2f;

        [Header("Skill Evolution")]
        [SerializeField, Range(0f, 0.25f)] private float evolutionOneBonusIncrease = 0.05f;
        [SerializeField, Range(0f, 0.25f)] private float evolutionTwoBonusIncrease = 0.05f;
        [SerializeField, Range(0f, 1f)] private float maximumAdditiveDamageBonus = 0.45f;
        [SerializeField, Range(1f, 2f)] private float evolutionOneRangeMultiplier = 1.15f;
        [SerializeField, Range(1f, 2f)] private float evolutionTwoRangeMultiplier = 1.3f;
        [SerializeField, Range(1f, 2f)] private float evolutionTwoDurationMultiplier = 1.2f;

        private Damageable self;
        private EnemyStagger stagger;
        private float nextCastTime;
        private float castCompletesAt;
        private Damageable pendingTarget;
        private EnemyHealAbility healAbility;
        private SwarmCaller swarmCaller;
        private EnemyEvolutionStage evolutionStage;
        private bool brainControlled;
        private GameObject priorityOwner;

        public bool IsCasting => pendingTarget != null;
        public Damageable CurrentTarget => pendingTarget;
        public bool IsReady => pendingTarget == null
            && Time.time >= nextCastTime
            && (stagger == null || stagger.CanAct);

        public bool HasEligibleTarget()
        {
            return FindTarget() != null;
        }

        private void SpawnVfx(
            GameObject prefab,
            Vector3 position,
            Vector2 offset,
            float scale)
        {
            if (prefab == null)
            {
                return;
            }

            GameObject vfx = Instantiate(
                prefab,
                position + (Vector3)offset,
                Quaternion.identity);

            vfx.transform.localScale *= scale;
        }

        private void Awake()
        {
            self = GetComponent<Damageable>();
            stagger = GetComponent<EnemyStagger>();
            healAbility = GetComponent<EnemyHealAbility>();
            swarmCaller = GetComponent<SwarmCaller>();
            EnemyArchetypeProfile profile = GetComponent<EnemyArchetypeProfile>();
            if (profile == null)
            {
                profile = gameObject.AddComponent<EnemyArchetypeProfile>();
            }

            profile.AddRuntimeArchetype(EnemyArchetype.Support);
        }

        private void Update()
        {
            if (pendingTarget != null)
            {
                if (Time.time >= castCompletesAt)
                {
                    CompleteCast();
                }

                return;
            }

            if (brainControlled)
            {
                return;
            }

            TryUse();
        }

        public void ConfigureNecromancerSupport(
            float damageBonus,
            float range,
            float duration,
            float cooldown,
            float windup,
            LayerMask targetLayers,
            GameObject skeletonOwner)
        {
            buffType = EnemyDamageModifierType.NecromancerBuff;
            additiveDamageBonus = Mathf.Clamp(damageBonus, 0f, 2f);
            buffRange = Mathf.Max(0.1f, range);
            buffDuration = Mathf.Max(0.1f, duration);
            buffCooldown = Mathf.Max(0f, cooldown);
            castWindup = Mathf.Max(0f, windup);
            allyLayers = targetLayers;
            canBuffSelf = false;
            preferNonSupportTargets = true;
            priorityOwner = skeletonOwner;
        }

        public bool TryUse()
        {
            if (!IsReady)
            {
                return false;
            }

            if (!brainControlled
                && ((healAbility != null && (healAbility.IsCasting || healAbility.HasEligibleTarget()))
                    || (swarmCaller != null && swarmCaller.ShouldPrioritizeSummon)))
            {
                nextCastTime = Time.time + 0.15f;
                return false;
            }

            Damageable target = FindTarget();
            if (target == null)
            {
                nextCastTime = Time.time + 0.25f;
                return false;
            }

            pendingTarget = target;
            castCompletesAt = Time.time + castWindup;
            SpawnVfx(
                castVfxPrefab,
                transform.position,
                castVfxOffset,
                castVfxScale
            );
            return true;
        }

        public void SetBrainControlled(bool controlled)
        {
            brainControlled = controlled;
        }

        private Damageable FindTarget()
        {
            if (priorityOwner != null)
            {
                return EnemySupportTargeting.FindHighestValueUnbuffedDamageTarget(
                    transform.position,
                    EffectiveBuffRange,
                    allyLayers,
                    self,
                    canBuffSelf,
                    buffType,
                    priorityOwner,
                    ownedSkeletonBonus,
                    generalRankBonus,
                    witnessedDeathWeight,
                    resolvedStrengthWeight,
                    tankArchetypeBonus,
                    bruteCombatBonus,
                    meleeArchetypeBonus,
                    rangedArchetypeBonus,
                    maximumDistancePenalty);
            }

            return EnemySupportTargeting.FindUnbuffedDamageTarget(
                transform.position,
                EffectiveBuffRange,
                allyLayers,
                self,
                canBuffSelf,
                preferNonSupportTargets,
                buffType);
        }

        private void CompleteCast()
        {
            Damageable target = pendingTarget;
            pendingTarget = null;
            nextCastTime = Time.time + buffCooldown;
            if (target == null
                || !target.gameObject.activeInHierarchy
                || ((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude
                    > EffectiveBuffRange * EffectiveBuffRange)
            {
                return;
            }

            EnemyDamageModifiers modifiers = target.GetComponent<EnemyDamageModifiers>();
            if (modifiers == null)
            {
                modifiers = target.gameObject.AddComponent<EnemyDamageModifiers>();
            }

            modifiers.ApplyModifier(
                buffType,
                EffectiveDamageBonus,
                EffectiveBuffDuration,
                gameObject);
            EnemyBuffVfxAttachment.Show(
                target,
                buffType,
                targetBuffVfxPrefab,
                targetBuffVfxOffset,
                targetBuffVfxScale,
                targetBuffVfxBehindZOffset,
                EffectiveBuffDuration,
                targetBuffVfxCleanupDelay);
        }

        private float EffectiveDamageBonus => Mathf.Min(
            maximumAdditiveDamageBonus,
            additiveDamageBonus
                + (evolutionStage >= EnemyEvolutionStage.EvolutionOne ? evolutionOneBonusIncrease : 0f)
                + (evolutionStage >= EnemyEvolutionStage.EvolutionTwo ? evolutionTwoBonusIncrease : 0f));

        private float EffectiveBuffRange => buffRange * (evolutionStage == EnemyEvolutionStage.EvolutionTwo
            ? evolutionTwoRangeMultiplier
            : evolutionStage == EnemyEvolutionStage.EvolutionOne
                ? evolutionOneRangeMultiplier
                : 1f);

        private float EffectiveBuffDuration => buffDuration * (evolutionStage == EnemyEvolutionStage.EvolutionTwo
            ? evolutionTwoDurationMultiplier
            : 1f);

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            evolutionStage = stage;
        }

        public void Interrupt()
        {
            pendingTarget = null;
            nextCastTime = Mathf.Max(nextCastTime, Time.time + buffCooldown * 0.5f);
        }

        private void OnDisable()
        {
            pendingTarget = null;
            nextCastTime = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = castColor;
            Gizmos.DrawWireSphere(transform.position, buffRange);
        }
    }

    // Lives on the buffed target so target disable/death always owns VFX cleanup.
    // One attachment per modifier type prevents a refresh from leaving duplicate loops.
    internal sealed class EnemyBuffVfxAttachment : MonoBehaviour
    {
        private EnemyDamageModifierType modifierType;
        private Damageable damageable;
        private GameObject effectInstance;
        private Coroutine lifetimeRoutine;
        private bool configured;
        private bool subscribed;

        internal static void Show(
            Damageable target,
            EnemyDamageModifierType type,
            GameObject prefab,
            Vector2 localOffset,
            float scale,
            float behindZOffset,
            float duration,
            float cleanupDelay)
        {
            if (target == null || prefab == null)
            {
                return;
            }

            EnemyBuffVfxAttachment attachment = null;
            EnemyBuffVfxAttachment[] existing =
                target.GetComponents<EnemyBuffVfxAttachment>();
            foreach (EnemyBuffVfxAttachment candidate in existing)
            {
                if (candidate != null && candidate.configured && candidate.modifierType == type)
                {
                    attachment = candidate;
                    break;
                }
            }

            if (attachment == null)
            {
                attachment = target.gameObject.AddComponent<EnemyBuffVfxAttachment>();
            }

            attachment.Play(
                type,
                prefab,
                localOffset,
                scale,
                behindZOffset,
                duration,
                cleanupDelay);
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Play(
            EnemyDamageModifierType type,
            GameObject prefab,
            Vector2 localOffset,
            float scale,
            float behindZOffset,
            float duration,
            float cleanupDelay)
        {
            CleanupImmediately();
            modifierType = type;
            configured = true;

            SpriteRenderer primaryRenderer = FindPrimarySpriteRenderer();
            effectInstance = Instantiate(prefab, transform, false);
            effectInstance.name = prefab.name + " (Target Buff)";
            effectInstance.transform.localPosition = new Vector3(
                localOffset.x,
                localOffset.y,
                Mathf.Abs(behindZOffset));
            effectInstance.transform.localScale =
                prefab.transform.localScale * Mathf.Max(0f, scale);

            ConfigureFollowingParticles(effectInstance);
            PlaceRenderersBehindTarget(effectInstance, primaryRenderer);
            lifetimeRoutine = StartCoroutine(ExpireAfter(
                Mathf.Max(0.01f, duration),
                Mathf.Max(0f, cleanupDelay)));
        }

        private IEnumerator ExpireAfter(float duration, float cleanupDelay)
        {
            yield return new WaitForSeconds(duration);
            lifetimeRoutine = null;

            if (effectInstance == null)
            {
                Destroy(this);
                yield break;
            }

            bool hasParticles = StopParticleEmission(effectInstance);
            if (hasParticles && cleanupDelay > 0f)
            {
                yield return new WaitForSeconds(cleanupDelay);
            }

            GameObject expiredEffect = effectInstance;
            effectInstance = null;
            Destroy(expiredEffect);
            Destroy(this);
        }

        private static void ConfigureFollowingParticles(GameObject effect)
        {
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }

        private static bool StopParticleEmission(GameObject effect)
        {
            ParticleSystem[] particleSystems =
                effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particles in particleSystems)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            return particleSystems.Length > 0;
        }

        private static void PlaceRenderersBehindTarget(
            GameObject effect,
            SpriteRenderer primaryRenderer)
        {
            if (primaryRenderer == null)
            {
                return;
            }

            int behindOrder = primaryRenderer.sortingOrder - 1;
            foreach (ParticleSystemRenderer renderer in
                effect.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                renderer.sortingLayerID = primaryRenderer.sortingLayerID;
                renderer.sortingOrder = behindOrder;
            }

            foreach (SpriteRenderer renderer in
                effect.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sortingLayerID = primaryRenderer.sortingLayerID;
                renderer.sortingOrder = behindOrder;
            }
        }

        private SpriteRenderer FindPrimarySpriteRenderer()
        {
            SpriteRenderer selected = null;
            float selectedArea = 0f;
            foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (candidate == null || candidate.sprite == null)
                {
                    continue;
                }

                Vector3 size = candidate.bounds.size;
                float area = Mathf.Abs(size.x * size.y);
                if (selected == null || area > selectedArea)
                {
                    selected = candidate;
                    selectedArea = area;
                }
            }

            return selected;
        }

        private void Subscribe()
        {
            if (subscribed || damageable == null)
            {
                return;
            }

            damageable.Died += HandleTargetDied;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || damageable == null)
            {
                return;
            }

            damageable.Died -= HandleTargetDied;
            subscribed = false;
        }

        private void HandleTargetDied()
        {
            CleanupImmediately();
            Destroy(this);
        }

        private void CleanupImmediately()
        {
            if (lifetimeRoutine != null)
            {
                StopCoroutine(lifetimeRoutine);
                lifetimeRoutine = null;
            }

            if (effectInstance != null)
            {
                Destroy(effectInstance);
                effectInstance = null;
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            CleanupImmediately();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            CleanupImmediately();
        }
    }
}
