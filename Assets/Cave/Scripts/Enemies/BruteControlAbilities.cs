using System.Collections;
using Cave.Audio;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// The Brute's additive body-control kit. It deliberately owns only hook,
    /// reel and pin behaviour; standard pickaxe strikes remain EnemyMeleeCombat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BruteControlAbilities : MonoBehaviour, IEnemyInterruptible, IEnemySkillEvolutionReceiver
    {
        [Header("Base Hook / Drag")]
        [SerializeField, Min(0.1f)] private float hookRange = 2.55f;
        [SerializeField, Min(0.01f)] private float hookWindup = 0.26f;
        [SerializeField, Min(0.05f)] private float dragDuration = 0.48f;
        [SerializeField, Min(0f)] private float dragSpeed = 9f;
        [SerializeField, Min(0)] private int hookDamage = 1;
        [SerializeField, Min(0f)] private float hookCooldown = 2.35f;

        [Header("Pin / Wall Pressure")]
        [SerializeField, Min(0.1f)] private float pinRange = 1.2f;
        [SerializeField, Min(0.01f)] private float pinDuration = 0.42f;
        [SerializeField, Min(0)] private int pinDamage = 1;
        [SerializeField, Min(0f)] private float pinCooldown = 3.4f;
        [SerializeField, Min(0.1f)] private float wallCheckDistance = 0.75f;
        [SerializeField, Min(0)] private int wallPinBonusDamage = 1;
        [SerializeField] private LayerMask worldMask = ~0;

        [Header("Evolved Chain Hook")]
        [SerializeField, Min(1f)] private float chainHookRange = 6f;
        [SerializeField, Min(0.01f)] private float chainWindup = 0.42f;
        [SerializeField, Min(0.1f)] private float chainTravelSpeed = 12f;
        [SerializeField, Min(0.1f)] private float chainReelDuration = 0.7f;
        [SerializeField, Min(0f)] private float chainReelSpeed = 11f;
        [SerializeField, Min(0f)] private float chainCooldown = 5.5f;
        [SerializeField, Min(0.05f)] private float chainProjectileRadius = 0.16f;
        [SerializeField] private Sprite chainPickaxeSprite;

        [Header("Optional VFX Hooks")]
        [SerializeField] private GameObject hookConnectionVfx;
        [SerializeField] private GameObject pinVfx;
        [SerializeField] private GameObject chainLaunchVfx;
        [SerializeField] private GameObject chainReelImpactVfx;

        [Header("Pin Presentation")]
        [SerializeField] private Transform pickaxeTransform;
        [SerializeField, Min(0.01f)] private float pinStrikeDuration = 0.08f;
        [SerializeField] private Vector3 pinForwardLocalOffset = new Vector3(0.7f, 0f, 0f);
        [SerializeField] private float pinLocalRotationOffset = -22f;
        [SerializeField, Min(0.01f)] private float pinReturnDuration = 0.12f;
        [SerializeField] private GameObject pinImpactXPrefab;
        [SerializeField] private Vector2 pinImpactPlayerOffset = new Vector2(0f, 0.15f);

        [Header("Optional Audio Hooks")]
        [SerializeField] private AudioClip grabClip;
        [SerializeField] private AudioClip chainThrowClip;
        [SerializeField] private AudioClip chainReelClip;
        [SerializeField] private AudioClip chainBreakClip;

        [Header("Runtime (Read Only)")]
        [SerializeField] private bool chainHookUnlocked;
        [SerializeField] private bool isControllingPlayer;
        [SerializeField] private string currentAction;

        private EnemyController movement;
        private EnemyStagger stagger;
        private Damageable damageable;
        private Coroutine actionRoutine;
        private float nextHookTime;
        private float nextPinTime;
        private float nextChainTime;
        private LineRenderer tether;
        private Transform chainHead;
        private PlayerHealth activeTarget;
        private Coroutine pinPresentationRoutine;
        private GameObject pinImpactInstance;
        private Vector3 authoredPickaxeLocalPosition;
        private Quaternion authoredPickaxeLocalRotation;
        private Vector3 authoredPickaxeLocalScale;
        private bool pickaxePresentationCached;

        public bool IsBusy => actionRoutine != null;
        public bool ChainHookUnlocked => chainHookUnlocked;
        public float HookRange => hookRange;
        public float ChainHookRange => chainHookRange;
        public float PinRange => pinRange;

        private void Awake()
        {
            movement = GetComponent<EnemyController>();
            stagger = GetComponent<EnemyStagger>();
            damageable = GetComponent<Damageable>();
            CachePickaxePresentation();
        }

        private void OnEnable()
        {
            if (stagger != null)
            {
                stagger.Staggered += HandleStaggered;
            }

            if (damageable != null)
            {
                damageable.Died += Interrupt;
            }
        }

        private void OnDisable()
        {
            if (stagger != null)
            {
                stagger.Staggered -= HandleStaggered;
            }

            if (damageable != null)
            {
                damageable.Died -= Interrupt;
            }

            Interrupt();
        }

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            chainHookUnlocked = stage >= EnemyEvolutionStage.EvolutionOne;
        }

        public bool TryUseHookDrag(PlayerHealth target)
        {
            if (!CanStart(target, hookRange, nextHookTime))
            {
                return false;
            }

            ObserveTarget(target);
            actionRoutine = StartCoroutine(PerformDirectHook(target));
            return true;
        }

        public bool TryUsePin(PlayerHealth target, bool wallPressure)
        {
            if (!CanStart(target, pinRange, nextPinTime))
            {
                return false;
            }

            ObserveTarget(target);
            actionRoutine = StartCoroutine(PerformPin(target, wallPressure));
            return true;
        }

        public bool TryUseChainHook(PlayerHealth target)
        {
            if (!chainHookUnlocked || !CanStart(target, chainHookRange, nextChainTime))
            {
                return false;
            }

            ObserveTarget(target);
            actionRoutine = StartCoroutine(PerformChainHook(target));
            return true;
        }

        public bool IsPlayerNearWall(PlayerHealth target)
        {
            if (target == null)
            {
                return false;
            }

            float direction = Mathf.Sign(target.transform.position.x - transform.position.x);
            if (Mathf.Abs(direction) < 0.01f)
            {
                direction = 1f;
            }

            RaycastHit2D[] hits = Physics2D.RaycastAll(
                target.transform.position,
                Vector2.right * direction,
                wallCheckDistance,
                worldMask);
            foreach (RaycastHit2D hit in hits)
            {
                if (IsWorldBlocker(hit.collider))
                {
                    return true;
                }
            }

            return false;
        }

        public void Interrupt()
        {
            if (actionRoutine != null)
            {
                StopCoroutine(actionRoutine);
                actionRoutine = null;
            }

            isControllingPlayer = false;
            currentAction = string.Empty;
            ClearTether();
            StopPinPresentation(true);
            StopObservingTarget();
        }

        private bool CanStart(PlayerHealth target, float range, float nextReadyTime)
        {
            return !IsBusy
                && Time.time >= nextReadyTime
                && target != null
                && target.gameObject.activeInHierarchy
                && target.CurrentHealth > 0
                && (stagger == null || stagger.CanAct)
                && Mathf.Abs(target.transform.position.x - transform.position.x) <= range;
        }

        private IEnumerator PerformDirectHook(PlayerHealth target)
        {
            currentAction = "Hook / Drag";
            movement?.SetCombatMovementIntent(0f, hookWindup + dragDuration);
            yield return new WaitForSeconds(hookWindup);
            if (!CanContinue(target))
            {
                EndAction(ref nextHookTime, hookCooldown);
                yield break;
            }

            if (RejectByPlayerDefense(target))
            {
                EndAction(ref nextHookTime, hookCooldown);
                yield break;
            }

            target.TryTakeDamage(hookDamage, new DamageContext(gameObject, DamageTrait.Melee));
            Spawn(hookConnectionVfx, target.transform.position);
            CaveSfx.PlayClip(grabClip, 0.7f);
            yield return ReelPlayer(target, dragDuration, dragSpeed);
            EndAction(ref nextHookTime, hookCooldown);
        }

        private IEnumerator PerformPin(PlayerHealth target, bool wallPressure)
        {
            currentAction = wallPressure ? "Wall Pin" : "Pin";
            movement?.SetCombatMovementIntent(0f, pinDuration);
            yield return new WaitForSeconds(0.12f);
            if (!CanContinue(target))
            {
                EndAction(ref nextPinTime, pinCooldown);
                yield break;
            }

            if (!RejectByPlayerDefense(target))
            {
                int resolvedDamage = pinDamage + (wallPressure ? wallPinBonusDamage : 0);
                target.TryTakeDamage(resolvedDamage, new DamageContext(gameObject, DamageTrait.Melee));
                // Pin is a movement-control state, not a stun. Keep combat input
                // (Basic, Heavy, Guard, Parry, GB, projectile) available while
                // PlayerController prevents ordinary locomotion and neutral Dash.
                target.GetComponent<PlayerController>()?.ApplyExternalControlLock(pinDuration);
                Spawn(pinVfx, target.transform.position);
                BeginPinPresentation(target);
                CaveSfx.PlayClip(grabClip, 0.8f);
            }

            yield return new WaitForSeconds(pinDuration);
            EndAction(ref nextPinTime, pinCooldown);
        }

        private IEnumerator PerformChainHook(PlayerHealth target)
        {
            currentAction = "Chain Hook Windup";
            movement?.SetCombatMovementIntent(0f, chainWindup + chainReelDuration);
            yield return new WaitForSeconds(chainWindup);
            if (!CanContinue(target))
            {
                EndAction(ref nextChainTime, chainCooldown);
                yield break;
            }

            Spawn(chainLaunchVfx, transform.position);
            CaveSfx.PlayClip(chainThrowClip, 0.8f);
            Vector2 start = transform.position;
            Vector2 destination = target.transform.position;
            float maxTravel = Mathf.Min(chainHookRange, Vector2.Distance(start, destination));
            Vector2 direction = (destination - start).normalized;
            float travelled = 0f;
            Vector2 projectilePosition = start;
            CreateTether(projectilePosition);

            while (travelled < maxTravel && CanContinue(target))
            {
                float step = chainTravelSpeed * Time.deltaTime;
                RaycastHit2D[] obstacles = Physics2D.CircleCastAll(
                    projectilePosition,
                    chainProjectileRadius,
                    direction,
                    step,
                    worldMask);
                bool hitWorld = false;
                foreach (RaycastHit2D obstacle in obstacles)
                {
                    if (IsWorldBlocker(obstacle.collider))
                    {
                        hitWorld = true;
                        break;
                    }
                }

                if (hitWorld)
                {
                    BreakChain();
                    EndAction(ref nextChainTime, chainCooldown);
                    yield break;
                }

                projectilePosition += direction * step;
                travelled += step;
                UpdateTether(projectilePosition);
                if (Vector2.Distance(projectilePosition, target.transform.position) <= chainProjectileRadius + 0.45f)
                {
                    break;
                }

                yield return null;
            }

            if (!CanContinue(target) || RejectByPlayerDefense(target))
            {
                BreakChain();
                EndAction(ref nextChainTime, chainCooldown);
                yield break;
            }

            currentAction = "Chain Reel";
            Spawn(hookConnectionVfx, target.transform.position);
            CaveSfx.PlayClip(chainReelClip, 0.7f);
            yield return ReelPlayer(target, chainReelDuration, chainReelSpeed);
            Spawn(chainReelImpactVfx, target.transform.position);
            ClearTether();
            EndAction(ref nextChainTime, chainCooldown);
        }

        private IEnumerator ReelPlayer(PlayerHealth target, float duration, float speed)
        {
            Rigidbody2D targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
            PlayerController controller = target != null ? target.GetComponent<PlayerController>() : null;
            float endTime = Time.time + duration;
            isControllingPlayer = true;
            while (Time.time < endTime && CanContinue(target))
            {
                Vector2 delta = transform.position - target.transform.position;
                if (Mathf.Abs(delta.x) <= pinRange * 0.75f)
                {
                    break;
                }

                float horizontalDirection = Mathf.Sign(delta.x);
                controller?.ApplyExternalKnockback(
                    new Vector2(horizontalDirection * speed, targetBody != null ? targetBody.velocity.y : 0f),
                    0.08f);
                UpdateTether(target.transform.position);
                yield return new WaitForFixedUpdate();
            }

            isControllingPlayer = false;
        }

        private bool RejectByPlayerDefense(PlayerHealth target)
        {
            SidewaysParryAttack defense = target != null ? target.GetComponent<SidewaysParryAttack>() : null;
            if (defense == null || !defense.IsActive)
            {
                return false;
            }

            // This is intentionally a clean rejection, not a separate parry rule:
            // guard or an active parry stance denies a hook before it attaches.
            BreakChain();
            return true;
        }

        private bool CanContinue(PlayerHealth target)
        {
            return target != null
                && target.gameObject.activeInHierarchy
                && target.CurrentHealth > 0
                && (damageable == null || damageable.CurrentHealth > 0)
                && (stagger == null || stagger.CanAct);
        }

        private bool IsWorldBlocker(Collider2D candidate)
        {
            return candidate != null
                && !candidate.isTrigger
                && !candidate.transform.IsChildOf(transform)
                && candidate.GetComponentInParent<PlayerHealth>() == null
                && candidate.GetComponentInParent<EnemyController>() == null
                && candidate.GetComponentInParent<EnemyArchetypeProfile>() == null;
        }

        private void CreateTether(Vector2 end)
        {
            if (tether == null)
            {
                GameObject tetherObject = new GameObject("Brute Chain Tether");
                tetherObject.transform.SetParent(transform, false);
                tether = tetherObject.AddComponent<LineRenderer>();
                tether.useWorldSpace = true;
                tether.positionCount = 2;
                tether.startWidth = 0.05f;
                tether.endWidth = 0.05f;
                tether.material = new Material(Shader.Find("Sprites/Default"));
                tether.startColor = new Color(0.25f, 0.22f, 0.18f, 1f);
                tether.endColor = tether.startColor;
                tether.sortingOrder = 3;

                GameObject headObject = new GameObject("Pickaxe Chain Head");
                headObject.transform.SetParent(tetherObject.transform, false);
                SpriteRenderer headRenderer = headObject.AddComponent<SpriteRenderer>();
                headRenderer.sprite = chainPickaxeSprite;
                headRenderer.color = chainPickaxeSprite != null
                    ? Color.white
                    : new Color(0.8f, 0.65f, 0.3f, 1f);
                headRenderer.sortingOrder = 4;
                chainHead = headObject.transform;

                // A simple fallback glyph makes an unassigned pickaxe slot still
                // readable in Play Mode without touching vendor sprites.
                if (chainPickaxeSprite == null)
                {
                    LineRenderer fallbackHead = headObject.AddComponent<LineRenderer>();
                    fallbackHead.material = tether.material;
                    fallbackHead.useWorldSpace = false;
                    fallbackHead.loop = true;
                    fallbackHead.positionCount = 4;
                    fallbackHead.startWidth = 0.06f;
                    fallbackHead.endWidth = 0.06f;
                    fallbackHead.startColor = new Color(0.85f, 0.64f, 0.24f, 1f);
                    fallbackHead.endColor = fallbackHead.startColor;
                    fallbackHead.sortingOrder = 4;
                    fallbackHead.SetPosition(0, new Vector3(0f, 0.18f));
                    fallbackHead.SetPosition(1, new Vector3(0.18f, 0f));
                    fallbackHead.SetPosition(2, new Vector3(0f, -0.18f));
                    fallbackHead.SetPosition(3, new Vector3(-0.18f, 0f));
                }
            }

            UpdateTether(end);
        }

        private void UpdateTether(Vector2 end)
        {
            if (tether == null)
            {
                return;
            }

            tether.SetPosition(0, transform.position);
            tether.SetPosition(1, end);
            if (chainHead != null)
            {
                chainHead.position = end;
            }
        }

        private void BreakChain()
        {
            if (tether != null)
            {
                CaveSfx.PlayClip(chainBreakClip, 0.65f);
            }

            ClearTether();
        }

        private void ClearTether()
        {
            if (tether == null)
            {
                return;
            }

            if (tether.material != null)
            {
                Destroy(tether.material);
            }

            Destroy(tether.gameObject);
            tether = null;
            chainHead = null;
        }

        private void EndAction(ref float cooldown, float duration)
        {
            cooldown = Time.time + duration;
            isControllingPlayer = false;
            currentAction = string.Empty;
            actionRoutine = null;
            StopObservingTarget();
        }

        private void CachePickaxePresentation()
        {
            if (pickaxeTransform == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child != transform
                        && child.name.ToLowerInvariant().Contains("pickaxe"))
                    {
                        pickaxeTransform = child;
                        break;
                    }
                }
            }

            if (pickaxeTransform == null)
            {
                return;
            }

            authoredPickaxeLocalPosition = pickaxeTransform.localPosition;
            authoredPickaxeLocalRotation = pickaxeTransform.localRotation;
            authoredPickaxeLocalScale = pickaxeTransform.localScale;
            pickaxePresentationCached = true;
        }

        private void BeginPinPresentation(PlayerHealth target)
        {
            if (!pickaxePresentationCached)
            {
                CachePickaxePresentation();
            }

            StopPinPresentation(true);
            if (pickaxeTransform == null || target == null)
            {
                return;
            }

            pinPresentationRoutine = StartCoroutine(PerformPinPresentation(target));
        }

        private IEnumerator PerformPinPresentation(PlayerHealth target)
        {
            float localDirection = Mathf.Sign(
                transform.InverseTransformDirection(target.transform.position - transform.position).x);
            if (Mathf.Abs(localDirection) < 0.01f)
            {
                localDirection = 1f;
            }

            Vector3 strikePosition = authoredPickaxeLocalPosition + new Vector3(
                Mathf.Abs(pinForwardLocalOffset.x) * localDirection,
                pinForwardLocalOffset.y,
                pinForwardLocalOffset.z);
            Quaternion strikeRotation = authoredPickaxeLocalRotation
                * Quaternion.Euler(0f, 0f, pinLocalRotationOffset * localDirection);
            yield return LerpPickaxe(
                authoredPickaxeLocalPosition,
                authoredPickaxeLocalRotation,
                strikePosition,
                strikeRotation,
                pinStrikeDuration);

            SpawnPinImpact(target);
            float holdEndsAt = Time.time + Mathf.Max(0f, pinDuration - pinStrikeDuration);
            while (Time.time < holdEndsAt
                && target != null
                && target.gameObject.activeInHierarchy)
            {
                yield return null;
            }

            DestroyPinImpact();
            yield return LerpPickaxe(
                pickaxeTransform.localPosition,
                pickaxeTransform.localRotation,
                authoredPickaxeLocalPosition,
                authoredPickaxeLocalRotation,
                pinReturnDuration);
            RestorePickaxePresentation();
            pinPresentationRoutine = null;
        }

        private IEnumerator LerpPickaxe(
            Vector3 startPosition,
            Quaternion startRotation,
            Vector3 endPosition,
            Quaternion endRotation,
            float duration)
        {
            if (pickaxeTransform == null)
            {
                yield break;
            }

            float startTime = Time.time;
            while (Time.time < startTime + duration)
            {
                float progress = Mathf.Clamp01((Time.time - startTime) / duration);
                progress = progress * progress * (3f - 2f * progress);
                pickaxeTransform.localPosition = Vector3.Lerp(startPosition, endPosition, progress);
                pickaxeTransform.localRotation = Quaternion.Slerp(startRotation, endRotation, progress);
                yield return null;
            }

            pickaxeTransform.localPosition = endPosition;
            pickaxeTransform.localRotation = endRotation;
        }

        private void SpawnPinImpact(PlayerHealth target)
        {
            if (pinImpactXPrefab == null || target == null)
            {
                return;
            }

            Collider2D targetCollider = target.GetComponent<Collider2D>();
            Vector3 contactPoint = targetCollider != null && pickaxeTransform != null
                ? targetCollider.ClosestPoint(pickaxeTransform.position)
                : target.transform.position;
            pinImpactInstance = Instantiate(pinImpactXPrefab, contactPoint, Quaternion.identity);
            pinImpactInstance.transform.SetParent(target.transform, true);
            pinImpactInstance.transform.localPosition += (Vector3)pinImpactPlayerOffset;
        }

        private void StopPinPresentation(bool restoreImmediately)
        {
            if (pinPresentationRoutine != null)
            {
                StopCoroutine(pinPresentationRoutine);
                pinPresentationRoutine = null;
            }

            DestroyPinImpact();
            if (restoreImmediately)
            {
                RestorePickaxePresentation();
            }
        }

        private void DestroyPinImpact()
        {
            if (pinImpactInstance != null)
            {
                Destroy(pinImpactInstance);
                pinImpactInstance = null;
            }
        }

        private void RestorePickaxePresentation()
        {
            if (!pickaxePresentationCached || pickaxeTransform == null)
            {
                return;
            }

            pickaxeTransform.localPosition = authoredPickaxeLocalPosition;
            pickaxeTransform.localRotation = authoredPickaxeLocalRotation;
            pickaxeTransform.localScale = authoredPickaxeLocalScale;
        }

        private void ObserveTarget(PlayerHealth target)
        {
            StopObservingTarget();
            activeTarget = target;
            if (activeTarget != null)
            {
                activeTarget.Died += Interrupt;
            }
        }

        private void StopObservingTarget()
        {
            if (activeTarget != null)
            {
                activeTarget.Died -= Interrupt;
                activeTarget = null;
            }
        }

        private static void Spawn(GameObject prefab, Vector2 position)
        {
            if (prefab != null)
            {
                Instantiate(prefab, position, Quaternion.identity);
            }
        }

        private void HandleStaggered(StaggerStrength strength, float duration)
        {
            Interrupt();
        }
    }
}
