using System.Collections;
using System.Collections.Generic;
using Cave.Audio;
using Cave.Enemies;
using Cave.InputSystem;
using Cave.Player;
using Cave.Projectiles;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cave.Combat
{
    public enum ParryPhase
    {
        Ready,
        Perfect,
        Normal,
        Late,
        Broken,
        Guard
    }

    public enum PlayerDefenseQuality
    {
        None,
        Block,
        NormalParry,
        PerfectParry
    }

    public sealed class SidewaysParryAttack : MonoBehaviour
    {
        [Header("Held Parry Timing")]
        [SerializeField, Min(0.01f)] private float perfectParryDuration = 0.07f;
        [SerializeField, Min(0.02f)] private float normalParryEndTime = 0.18f;
        [SerializeField, Min(0.03f)] private float lateParryEndTime = 0.30f;
        [SerializeField, Min(0f)] private float cooldown = 0.45f;

        [Header("Timing Scaling / Chain Parry")]
        [SerializeField, Min(0.1f)] private float parryTimingMultiplier = 1.3f;
        [SerializeField, Min(0f)] private float successfulParryExtension = 0.12f;
        [SerializeField, Min(0f)] private float perfectParryExtension = 0.18f;
        [SerializeField, Min(0f)] private float maximumChainExtension = 0.45f;

        [Header("Melee Guard")]
        [SerializeField, Min(0f)] private float sustainedGuardStaminaDrainPerSecond = 20f;
        [SerializeField, Min(0f)] private float blockedHitStaminaCost = 6f;
        [SerializeField, Min(0.05f)] private float counterWindowDuration = 0.7f;
        [SerializeField, Range(0f, 1f)] private float lateMeleeDamageReduction = 0.5f;
        [SerializeField, Min(0f)] private float normalMeleeKnockback = 6f;
        [SerializeField, Min(0f)] private float perfectMeleeKnockback = 10f;
        [SerializeField, Min(0f)] private float normalEnemyStaggerDuration = 0.35f;
        [SerializeField, Min(0f)] private float perfectEnemyStaggerDuration = 0.6f;

        [Header("Successful Parry Resource Rewards")]
        [SerializeField, Range(0f, 1f)] private float normalStaminaRestoreFraction = 0.08f;
        [SerializeField, Range(0f, 1f)] private float normalManaRestoreFraction = 0.03f;
        [SerializeField, Range(0f, 1f)] private float perfectStaminaRestoreFraction = 0.15f;
        [SerializeField, Range(0f, 1f)] private float perfectManaRestoreFraction = 0.08f;

        [Header("Perfect Parry")]
        [SerializeField, Min(1f)] private float perfectReflectionSpeedMultiplier = 1.4f;
        [SerializeField, Min(1f)] private float perfectReflectionDamageMultiplier = 2f;
        [SerializeField, Min(0.01f)] private float perfectFeedbackDuration = 0.12f;
        [SerializeField] private GameObject perfectParryVfxPrefab;
        [SerializeField, Min(0.01f)] private Vector2 perfectParryVfxOffset = Vector2.zero;
        [SerializeField, Min(0.01f)] float perfectParryVfxScale = 1f;
        [SerializeField] private Color perfectFeedbackColor = new Color(1f, 0.9f, 0.35f, 1f);

        [Header("Parry Stage Feedback")]
        [SerializeField] private Color perfectStageColor = new Color(0.35f, 0.95f, 1f, 1f);
        [SerializeField] private Color normalStageColor = new Color(0.3f, 0.65f, 1f, 1f);
        [SerializeField] private Color lateStageColor = new Color(1f, 0.55f, 0.18f, 1f);
        [SerializeField] private Color brokenStageColor = new Color(1f, 0.18f, 0.12f, 1f);
        [SerializeField, Min(1f)] private float perfectStageScale = 1.14f;
        [SerializeField, Min(1f)] private float normalStageScale = 1.08f;
        [SerializeField, Range(0.5f, 1f)] private float lateStageScale = 0.94f;
        [SerializeField, Min(0.01f)] private float brokenFeedbackDuration = 0.16f;
        [SerializeField, Min(0.1f)] private float successfulParryPulseRadius = 0.75f;
        [SerializeField, Min(0.1f)] private float perfectParryPulseRadius = 1.15f;
        [SerializeField] private Transform successfulParryEffectAnchor;
        [SerializeField] private Vector2 successfulParryEffectOffset = new Vector2(0f, 0.15f);

        [Header("Normal / Late Deflection")]
        [FormerlySerializedAs("reflectionSpeedMultiplier")]
        [SerializeField, Min(1f)] private float normalReflectionSpeedMultiplier = 1.2f;
        [SerializeField] private float lateDeflectHorizontalOffset = 0.6f;
        [SerializeField] private float lateDeflectVerticalOffset = -1f;
        [SerializeField, Min(0f)] private float horizontalOffset = 1.1f;

        [Header("References")]
        [SerializeField] private Transform parryTransform;
        [SerializeField] private GameObject parryVisual;
        [SerializeField] private Collider2D parryCollider;

        [Header("Current State (Read Only)")]
        [SerializeField] private ParryPhase currentPhase = ParryPhase.Ready;
        [SerializeField, Min(0f)] private float currentChainExtension;
        [SerializeField] private PlayerDefenseQuality counterWindowQuality;
        [SerializeField, Min(0f)] private float counterWindowRemaining;

        private readonly HashSet<IPlayerParryableProjectile> parriedProjectiles =
            new HashSet<IPlayerParryableProjectile>();
        private float facingDirection = 1f;
        private float stanceStartedAt;
        private float nextStanceTime;
        private Vector3 restingParryPosition;
        private Vector3 restingParryScale;
        private SpriteRenderer[] feedbackRenderers;
        private Color[] restingFeedbackColors;
        private Coroutine feedbackRoutine;
        private PlayerMana playerMana;
        private SpinSwordAttack stamina;
        private PlayerGuardBreak guardBreak;

        public event System.Action<PlayerDefenseQuality> DefenseSucceeded;

        public ParryPhase CurrentPhase => currentPhase;
        public bool IsActive => currentPhase == ParryPhase.Perfect
            || currentPhase == ParryPhase.Normal
            || currentPhase == ParryPhase.Late
            || currentPhase == ParryPhase.Guard;
        public bool IsGuardHeld => IsActive && GameInput.ParryHeld;
        public bool IsCounterWindowActive => counterWindowQuality != PlayerDefenseQuality.None
            && Time.time <= counterWindowExpiresAt;

        private float counterWindowExpiresAt;

        private void Awake()
        {
            playerMana = GetComponent<PlayerMana>();
            stamina = GetComponent<SpinSwordAttack>();
            guardBreak = GetComponent<PlayerGuardBreak>();

            if (parryTransform != null)
            {
                restingParryPosition = parryTransform.localPosition;
                restingParryScale = parryTransform.localScale;
            }

            GameObject feedbackVisual = stamina != null && stamina.SwordVisualObject != null
                ? stamina.SwordVisualObject
                : parryVisual;
            feedbackRenderers = feedbackVisual != null
                ? feedbackVisual.GetComponentsInChildren<SpriteRenderer>(true)
                : new SpriteRenderer[0];
            restingFeedbackColors = new Color[feedbackRenderers.Length];
            for (int index = 0; index < feedbackRenderers.Length; index++)
            {
                restingFeedbackColors[index] = feedbackRenderers[index].color;
            }

            SetStanceVisualActive(false);
        }

        private void Update()
        {
            counterWindowRemaining = IsCounterWindowActive
                ? Mathf.Max(0f, counterWindowExpiresAt - Time.time)
                : 0f;
            if (!IsCounterWindowActive)
            {
                counterWindowQuality = PlayerDefenseQuality.None;
            }

            if (guardBreak == null)
            {
                guardBreak = GetComponent<PlayerGuardBreak>();
            }

            if (guardBreak != null && !guardBreak.CanUseCombatActions)
            {
                if (IsActive)
                {
                    BreakGuard();
                }

                return;
            }

            float horizontalInput = GameInput.Horizontal;
            if (!Mathf.Approximately(horizontalInput, 0f))
            {
                facingDirection = Mathf.Sign(horizontalInput);
            }

            if (currentPhase == ParryPhase.Ready)
            {
                if (GameInput.ParryPressed && Time.time >= nextStanceTime)
                {
                    BeginStance();
                }

                return;
            }

            if (currentPhase == ParryPhase.Broken)
            {
                if (GameInput.ParryReleased || !GameInput.ParryHeld)
                {
                    SetPhase(ParryPhase.Ready);
                }

                return;
            }

            if (GameInput.ParryReleased || !GameInput.ParryHeld)
            {
                EndStance(false);
                return;
            }

            UpdateHeldPhase(Time.time - stanceStartedAt);

            if (currentPhase == ParryPhase.Guard
                && (stamina == null
                    || !stamina.TrySpendStamina(
                        sustainedGuardStaminaDrainPerSecond * Time.deltaTime)))
            {
                BreakGuard();
            }
        }

        public bool TryGetCounterWindow(out PlayerDefenseQuality quality)
        {
            quality = IsCounterWindowActive
                ? counterWindowQuality
                : PlayerDefenseQuality.None;
            return quality != PlayerDefenseQuality.None;
        }

        public bool TryConsumeCounterWindow(out PlayerDefenseQuality quality)
        {
            if (!TryGetCounterWindow(out quality))
            {
                return false;
            }

            counterWindowQuality = PlayerDefenseQuality.None;
            counterWindowExpiresAt = 0f;
            counterWindowRemaining = 0f;
            if (IsActive)
            {
                EndStance(false);
            }

            return true;
        }

        public bool TryParry(IPlayerParryableProjectile projectile)
        {
            if (!IsActive || projectile == null || !projectile.CanBeParried || parriedProjectiles.Contains(projectile))
            {
                return false;
            }

            Vector2 fallbackDirection = new Vector2(facingDirection, 0f);
            bool succeeded;
            switch (currentPhase)
            {
                case ParryPhase.Perfect:
                    succeeded = projectile.Parry(
                        gameObject,
                        fallbackDirection,
                        perfectReflectionSpeedMultiplier,
                        perfectReflectionDamageMultiplier);
                    break;
                case ParryPhase.Normal:
                    succeeded = projectile.Parry(
                        gameObject,
                        fallbackDirection,
                        normalReflectionSpeedMultiplier,
                        1f);
                    break;
                case ParryPhase.Late:
                    Vector2 targetPoint = (Vector2)transform.position + new Vector2(
                        facingDirection * lateDeflectHorizontalOffset,
                        lateDeflectVerticalOffset);
                    succeeded = projectile.DeflectToGround(gameObject, targetPoint);
                    break;
                case ParryPhase.Guard:
                    Vector2 guardedTargetPoint = (Vector2)transform.position + new Vector2(
                        facingDirection * lateDeflectHorizontalOffset,
                        lateDeflectVerticalOffset);
                    succeeded = projectile.DeflectToGround(gameObject, guardedTargetPoint);
                    break;
                default:
                    return false;
            }

            if (succeeded)
            {
                parriedProjectiles.Add(projectile);
                if (currentPhase == ParryPhase.Guard)
                {
                    CompleteSuccessfulDefense(PlayerDefenseQuality.Block);
                }
                else
                {
                    CompleteSuccessfulParry(currentPhase);
                }
            }

            return succeeded;
        }

        public bool TryGuardMelee(ref int incomingDamage, DamageContext damageContext)
        {
            if (!damageContext.HasTrait(DamageTrait.Melee))
            {
                return false;
            }

            if (damageContext.HasTrait(DamageTrait.GuardBreak))
            {
                if (IsActive || GameInput.ParryHeld)
                {
                    BreakGuard();
                }

                return false;
            }

            if (!IsActive
                || damageContext.HasTrait(DamageTrait.AreaOfEffect)
                || damageContext.HasTrait(DamageTrait.Piercing))
            {
                return false;
            }

            switch (currentPhase)
            {
                case ParryPhase.Perfect:
                    ApplyMeleeStagger(
                        damageContext.Source,
                        StaggerStrength.Heavy,
                        perfectMeleeKnockback,
                        perfectEnemyStaggerDuration);
                    CompleteSuccessfulParry(ParryPhase.Perfect);
                    return true;
                case ParryPhase.Normal:
                    ApplyMeleeStagger(
                        damageContext.Source,
                        StaggerStrength.Normal,
                        normalMeleeKnockback,
                        normalEnemyStaggerDuration);
                    CompleteSuccessfulParry(ParryPhase.Normal);
                    return true;
                case ParryPhase.Late:
                    incomingDamage = Mathf.Max(
                        1,
                        Mathf.CeilToInt(incomingDamage * (1f - lateMeleeDamageReduction)));
                    AreaPulseEffect.Create(
                        transform.position,
                        successfulParryPulseRadius * 0.6f,
                        lateStageColor,
                        perfectFeedbackDuration);
                    return false;
                case ParryPhase.Guard:
                    if (stamina == null)
                    {
                        stamina = GetComponent<SpinSwordAttack>();
                    }

                    if (stamina == null || !stamina.TrySpendStamina(blockedHitStaminaCost))
                    {
                        BreakGuard();
                        return false;
                    }

                    incomingDamage = 0;
                    CompleteSuccessfulDefense(PlayerDefenseQuality.Block);
                    return true;
                default:
                    return false;
            }
        }

        public void BreakGuard()
        {
            if (currentPhase == ParryPhase.Broken)
            {
                return;
            }

            AreaPulseEffect.Create(
                transform.position,
                successfulParryPulseRadius * 0.7f,
                brokenStageColor,
                brokenFeedbackDuration);
            CaveSfx.Play(CaveSfxCue.Hit, 0.8f);
            EndStance(true);
        }

        private void BeginStance()
        {
            parriedProjectiles.Clear();
            currentChainExtension = 0f;
            stanceStartedAt = Time.time;
            SetPhase(ParryPhase.Perfect);
            if (parryTransform != null)
            {
                parryTransform.localPosition = new Vector3(
                    facingDirection * horizontalOffset,
                    restingParryPosition.y,
                    restingParryPosition.z);
            }

            SetStanceVisualActive(true);
        }

        private void EndStance(bool broken)
        {
            SetStanceVisualActive(false);
            ResetFeedback();
            RestoreParryTransform();
            currentChainExtension = 0f;
            nextStanceTime = Time.time + cooldown;
            if (broken)
            {
                currentPhase = ParryPhase.Broken;
                PlayFeedbackPulse(brokenStageColor, lateStageScale, brokenFeedbackDuration);
            }
            else
            {
                SetPhase(ParryPhase.Ready);
            }
        }

        private void RestoreParryResources(bool isPerfect)
        {
            if (playerMana == null)
            {
                playerMana = GetComponent<PlayerMana>();
            }

            if (stamina == null)
            {
                stamina = GetComponent<SpinSwordAttack>();
            }

            float staminaFraction = isPerfect
                ? perfectStaminaRestoreFraction
                : normalStaminaRestoreFraction;
            float manaFraction = isPerfect
                ? perfectManaRestoreFraction
                : normalManaRestoreFraction;
            if (stamina != null)
            {
                stamina.RestoreStamina(stamina.MaximumStamina * staminaFraction);
            }

            if (playerMana != null)
            {
                playerMana.RestoreMana(playerMana.MaximumMana * manaFraction);
            }
        }

        private void CompleteSuccessfulParry(ParryPhase phase)
        {
            bool isPerfect = phase == ParryPhase.Perfect;
            RestoreParryResources(isPerfect);
            if (isPerfect)
            {
                if (perfectParryVfxPrefab != null)
                    {
                        Vector3 spawnPosition =
                            transform.position +
                            new Vector3(
                                perfectParryVfxOffset.x,
                                perfectParryVfxOffset.y,
                                0f
                            );

                        GameObject vfx = Instantiate(
                            perfectParryVfxPrefab,
                            spawnPosition,
                            Quaternion.identity
                        );

                        vfx.transform.localScale *= perfectParryVfxScale;

                        Destroy(vfx, 1.5f);
                    }
            }

            float extension = isPerfect ? perfectParryExtension : successfulParryExtension;
            currentChainExtension = Mathf.Min(
                maximumChainExtension,
                currentChainExtension + extension);
            PlaySuccessfulParryFeedback(phase);
            CompleteSuccessfulDefense(isPerfect
                ? PlayerDefenseQuality.PerfectParry
                : PlayerDefenseQuality.NormalParry);
        }

        private void CompleteSuccessfulDefense(PlayerDefenseQuality quality)
        {
            if (!IsCounterWindowActive || quality > counterWindowQuality)
            {
                counterWindowQuality = quality;
            }

            counterWindowExpiresAt = Mathf.Max(
                counterWindowExpiresAt,
                Time.time + counterWindowDuration);
            counterWindowRemaining = Mathf.Max(0f, counterWindowExpiresAt - Time.time);
            DefenseSucceeded?.Invoke(quality);
        }

        private void UpdateHeldPhase(float elapsed)
        {
            float perfectEnd = perfectParryDuration * parryTimingMultiplier + currentChainExtension;
            float normalEnd = normalParryEndTime * parryTimingMultiplier + currentChainExtension;
            float lateEnd = lateParryEndTime * parryTimingMultiplier + currentChainExtension;

            if (currentPhase == ParryPhase.Perfect && elapsed >= perfectEnd)
            {
                SetPhase(ParryPhase.Normal);
            }

            if (currentPhase == ParryPhase.Normal && elapsed >= normalEnd)
            {
                SetPhase(ParryPhase.Late);
            }

            if (currentPhase == ParryPhase.Late && elapsed > lateEnd)
            {
                SetPhase(ParryPhase.Guard);
            }
        }

        private void ApplyMeleeStagger(
            GameObject source,
            StaggerStrength strength,
            float knockback,
            float staggerDuration)
        {
            if (source == null)
            {
                return;
            }

            EnemyStagger stagger = source.GetComponentInParent<EnemyStagger>();
            if (stagger == null || !stagger.TryStagger(strength, staggerDuration))
            {
                return;
            }

            if (knockback <= 0f)
            {
                return;
            }

            KnockbackReceiver receiver = source.GetComponentInParent<KnockbackReceiver>();
            if (receiver == null)
            {
                return;
            }

            Vector2 direction = source.transform.position - transform.position;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            receiver.ApplyKnockback((direction.normalized + Vector2.up * 0.15f).normalized * knockback);
        }

        private void PlaySuccessfulParryFeedback(ParryPhase phase)
        {
            bool isPerfect = phase == ParryPhase.Perfect;
            Color color = isPerfect ? perfectFeedbackColor : normalStageColor;
            float scale = isPerfect ? perfectStageScale * 1.08f : normalStageScale;
            float radius = isPerfect ? perfectParryPulseRadius : successfulParryPulseRadius;
            AreaPulseEffect.Create(GetSuccessfulParryEffectPosition(), radius, color, perfectFeedbackDuration);
            CaveSfx.Play(CaveSfxCue.Whoosh, isPerfect ? 1f : 0.8f);
            if (isPerfect)
            {
                CaveSfx.Play(CaveSfxCue.Bonus, 0.7f);
            }
            PlayFeedbackPulse(color, scale, perfectFeedbackDuration);
        }

        private Vector2 GetSuccessfulParryEffectPosition()
        {
            if (successfulParryEffectAnchor != null)
            {
                return (Vector2)successfulParryEffectAnchor.position
                    + new Vector2(
                        successfulParryEffectOffset.x * facingDirection,
                        successfulParryEffectOffset.y);
            }

            if (parryTransform != null)
            {
                return (Vector2)parryTransform.position
                    + new Vector2(
                        successfulParryEffectOffset.x * facingDirection,
                        successfulParryEffectOffset.y);
            }

            if (stamina != null && stamina.SwordVisualObject != null)
            {
                SpriteRenderer swordRenderer = stamina.SwordVisualObject
                    .GetComponentInChildren<SpriteRenderer>(true);
                if (swordRenderer != null)
                {
                    Bounds bounds = swordRenderer.bounds;
                    return (Vector2)bounds.center
                        + new Vector2(
                            bounds.extents.x * facingDirection,
                            bounds.extents.y * 0.65f)
                        + new Vector2(
                            successfulParryEffectOffset.x * facingDirection,
                            successfulParryEffectOffset.y);
                }
            }

            return (Vector2)transform.position + successfulParryEffectOffset;
        }

        private void PlayFeedbackPulse(Color color, float scale, float duration)
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackRoutine = StartCoroutine(FeedbackPulseRoutine(color, scale, duration));
        }

        private IEnumerator FeedbackPulseRoutine(Color color, float scale, float duration)
        {
            SetFeedbackColor(color);
            SetFeedbackScale(scale);

            yield return new WaitForSeconds(duration);

            feedbackRoutine = null;
            RestoreFeedbackBaseline();
            ApplyPhaseFeedback();
        }

        private void ResetFeedback()
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
                feedbackRoutine = null;
            }

            RestoreFeedbackBaseline();
        }

        private void RestoreFeedbackBaseline()
        {
            SetFeedbackScale(1f);

            for (int index = 0; index < feedbackRenderers.Length; index++)
            {
                if (feedbackRenderers[index] != null)
                {
                    feedbackRenderers[index].color = restingFeedbackColors[index];
                }
            }
        }

        private void SetPhase(ParryPhase phase)
        {
            if (currentPhase == phase)
            {
                return;
            }

            currentPhase = phase;
            ApplyPhaseFeedback();
        }

        private void ApplyPhaseFeedback()
        {
            if (feedbackRoutine != null)
            {
                return;
            }

            switch (currentPhase)
            {
                case ParryPhase.Perfect:
                    SetFeedbackColor(perfectStageColor);
                    SetFeedbackScale(perfectStageScale);
                    break;
                case ParryPhase.Normal:
                    SetFeedbackColor(normalStageColor);
                    SetFeedbackScale(normalStageScale);
                    break;
                case ParryPhase.Late:
                    SetFeedbackColor(lateStageColor);
                    SetFeedbackScale(lateStageScale);
                    break;
                case ParryPhase.Guard:
                    SetFeedbackColor(normalStageColor);
                    SetFeedbackScale(1f);
                    break;
                default:
                    RestoreFeedbackBaseline();
                    break;
            }
        }

        private void SetFeedbackColor(Color color)
        {
            for (int index = 0; index < feedbackRenderers.Length; index++)
            {
                if (feedbackRenderers[index] != null)
                {
                    feedbackRenderers[index].color = color;
                }
            }
        }

        private void SetFeedbackScale(float multiplier)
        {
            if (parryTransform != null)
            {
                parryTransform.localScale = Vector3.Scale(
                    restingParryScale,
                    new Vector3(multiplier, multiplier, 1f));
            }
        }

        private void RestoreParryTransform()
        {
            if (parryTransform != null)
            {
                parryTransform.localPosition = restingParryPosition;
                parryTransform.localScale = restingParryScale;
            }
        }

        private void SetStanceVisualActive(bool active)
        {
            if (parryVisual != null)
            {
                bool isPersistentSword = stamina != null && parryVisual == stamina.SwordVisualObject;
                if (!isPersistentSword)
                {
                    parryVisual.SetActive(active);
                }
                else if (active)
                {
                    parryVisual.SetActive(true);
                }
            }

            if (parryCollider != null)
            {
                parryCollider.enabled = active;
            }
        }

        private void OnDisable()
        {
            ResetFeedback();
            RestoreParryTransform();
            currentPhase = ParryPhase.Ready;
            currentChainExtension = 0f;
            counterWindowQuality = PlayerDefenseQuality.None;
            counterWindowExpiresAt = 0f;
            counterWindowRemaining = 0f;
            SetStanceVisualActive(false);
        }

        private void OnValidate()
        {
            perfectParryDuration = Mathf.Max(0.01f, perfectParryDuration);
            normalParryEndTime = Mathf.Max(perfectParryDuration + 0.01f, normalParryEndTime);
            lateParryEndTime = Mathf.Max(normalParryEndTime + 0.01f, lateParryEndTime);
            parryTimingMultiplier = Mathf.Max(0.1f, parryTimingMultiplier);
            maximumChainExtension = Mathf.Max(0f, maximumChainExtension);
            successfulParryExtension = Mathf.Min(successfulParryExtension, maximumChainExtension);
            perfectParryExtension = Mathf.Min(perfectParryExtension, maximumChainExtension);
            perfectReflectionDamageMultiplier = Mathf.Max(1f, perfectReflectionDamageMultiplier);
        }
    }
}
