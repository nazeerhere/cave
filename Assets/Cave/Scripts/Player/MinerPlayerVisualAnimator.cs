using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.InputSystem;
using Cave.Projectiles;
using UnityEngine;

namespace Cave.Player
{
    /// <summary>
    /// Presentation-only Miner animation driver. Existing gameplay components remain authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinerPlayerVisualAnimator : MonoBehaviour
    {
        private const string ResourcePath = "Player/MinerFullActionSheet";
        private const string VisualName = "Miner Body Visual";

        [Header("Presentation")]
        [SerializeField] private Vector3 localOffset = new Vector3(0.288f, -1.053f, 0f);
        [SerializeField] private Vector3 localScale = new Vector3(3.5f, 2.5f, 1f);
        [SerializeField] private bool sourceFacesRight = true;

        [Header("Timing")]
        [SerializeField, Min(1f)] private float idleFramesPerSecond = 4f;
        [SerializeField, Min(1f)] private float runFramesPerSecond = 10f;
        [SerializeField, Min(1f)] private float spinFramesPerSecond = 12f;
        [SerializeField, Min(1f)] private float dashFramesPerSecond = 12f;
        [SerializeField, Min(0.01f)] private float hitDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float castDuration = 0.35f;
        [SerializeField, Min(0.01f)] private float interactDuration = 0.35f;
        [SerializeField, Min(0.01f)] private float landingDuration = 0.15f;
        [SerializeField, Min(0.1f)] private float knockbackSpeedThreshold = 8f;
        [SerializeField, Min(0f)] private float runSpeedThreshold = 0.1f;

        private static readonly Dictionary<string, Sprite[]> FrameCache =
            new Dictionary<string, Sprite[]>(StringComparer.Ordinal);
        private static bool cacheAttempted;

        private SpriteRenderer visualRenderer;
        private SpriteRenderer replacedBodyRenderer;
        private PlayerController playerController;
        private PlayerAimDirection aimDirection;
        private ChargedAttack chargedAttack;
        private SpinSwordAttack spinAttack;
        private SidewaysParryAttack defense;
        private PlayerGuardBreak guardBreak;
        private PlayerDash dash;
        private PlayerCrowdResponse crowdResponse;
        private PlayerCrossStep crossStep;
        private PlayerBrace brace;
        private PlayerHealth health;
        private PlayerProjectileLauncher projectileLauncher;
        private PlayerSpecialMode specialMode;
        private PlayerFlightBash flightBash;
        private Rigidbody2D body;
        private VisualState currentState;
        private Sprite[] currentFrames;
        private float stateStartedAt;
        private float hitUntil;
        private float castUntil;
        private float interactUntil;
        private float landingUntil;
        private bool wasGrounded;
        private bool isDead;
        private Sprite displayedSprite;

        private enum VisualState
        {
            Idle,
            Run,
            Jump,
            Landing,
            Heavy,
            Spin,
            Guard,
            Parry,
            GuardBreak,
            Dash,
            Hit,
            Knockback,
            Death,
            Interact,
            Cast,
            Brace
        }

        private void Awake()
        {
            CacheFrames();
            if (!FrameCache.ContainsKey("Miner_Idle_"))
            {
                enabled = false;
                Debug.LogWarning("Approved Miner body sprites could not be loaded from Resources/" + ResourcePath + ".", this);
                return;
            }

            playerController = GetComponent<PlayerController>();
            aimDirection = GetComponent<PlayerAimDirection>();
            chargedAttack = GetComponent<ChargedAttack>();
            spinAttack = GetComponent<SpinSwordAttack>();
            defense = GetComponent<SidewaysParryAttack>();
            guardBreak = GetComponent<PlayerGuardBreak>();
            dash = GetComponent<PlayerDash>();
            crowdResponse = GetComponent<PlayerCrowdResponse>();
            crossStep = GetComponent<PlayerCrossStep>();
            brace = GetComponent<PlayerBrace>();
            health = GetComponent<PlayerHealth>();
            projectileLauncher = GetComponent<PlayerProjectileLauncher>();
            specialMode = GetComponent<PlayerSpecialMode>();
            flightBash = GetComponent<PlayerFlightBash>();
            body = GetComponent<Rigidbody2D>();
            wasGrounded = playerController != null && playerController.IsGrounded;
            stateStartedAt = Time.time;
            currentFrames = Frames("Miner_Idle_");
            CreatePresentationRenderer();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.DamageTaken -= HandleDamageTaken;
                health.DamageTaken += HandleDamageTaken;
                health.Died -= HandleDied;
                health.Died += HandleDied;
                health.Respawned -= HandleRespawned;
                health.Respawned += HandleRespawned;
            }

            if (projectileLauncher != null)
            {
                projectileLauncher.ProjectileFired -= HandleProjectileFired;
                projectileLauncher.ProjectileFired += HandleProjectileFired;
            }

            if (specialMode != null)
            {
                specialMode.SpecialModeChanged -= HandleModeChanged;
                specialMode.SpecialModeChanged += HandleModeChanged;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.DamageTaken -= HandleDamageTaken;
                health.Died -= HandleDied;
                health.Respawned -= HandleRespawned;
            }

            if (projectileLauncher != null)
            {
                projectileLauncher.ProjectileFired -= HandleProjectileFired;
            }

            if (specialMode != null)
            {
                specialMode.SpecialModeChanged -= HandleModeChanged;
            }
        }

        private void Update()
        {
            bool grounded = playerController != null && playerController.IsGrounded;
            if (!wasGrounded && grounded)
            {
                landingUntil = Time.time + landingDuration;
            }

            wasGrounded = grounded;
            if (GameInput.InteractPressed)
            {
                interactUntil = Time.time + interactDuration;
            }
        }

        private void LateUpdate()
        {
            if (visualRenderer == null)
            {
                return;
            }

            VisualState nextState = ResolveState();
            if (nextState != currentState)
            {
                currentState = nextState;
                currentFrames = FramesForState(currentState);
                stateStartedAt = Time.time;
            }

            if (aimDirection != null && Mathf.Abs(aimDirection.FacingDirection.x) > 0.001f)
            {
                bool flip = sourceFacesRight
                    ? aimDirection.FacingDirection.x < 0f
                    : aimDirection.FacingDirection.x > 0f;
                if (visualRenderer.flipX != flip)
                {
                    visualRenderer.flipX = flip;
                }
            }

            Sprite nextSprite = ResolveCurrentFrame();
            if (nextSprite != displayedSprite)
            {
                displayedSprite = nextSprite;
                visualRenderer.sprite = nextSprite;
            }
        }

        private void CreatePresentationRenderer()
        {
            Transform existing = transform.Find(VisualName);
            GameObject visualObject = existing != null ? existing.gameObject : new GameObject(VisualName);
            if (existing == null)
            {
                visualObject.transform.SetParent(transform, false);
            }

            visualObject.transform.localPosition = localOffset;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = localScale;
            visualRenderer = visualObject.GetComponent<SpriteRenderer>();
            if (visualRenderer == null)
            {
                visualRenderer = visualObject.AddComponent<SpriteRenderer>();
            }

            replacedBodyRenderer = FindPrimaryBodyRenderer();
            if (replacedBodyRenderer != null)
            {
                visualRenderer.sortingLayerID = replacedBodyRenderer.sortingLayerID;
                visualRenderer.sortingOrder = replacedBodyRenderer.sortingOrder;
                visualRenderer.sharedMaterial = replacedBodyRenderer.sharedMaterial;
                replacedBodyRenderer.enabled = false;
            }

            displayedSprite = currentFrames[0];
            visualRenderer.sprite = displayedSprite;
        }

        private SpriteRenderer FindPrimaryBodyRenderer()
        {
            SpriteRenderer selected = null;
            float selectedArea = float.NegativeInfinity;
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                SpriteRenderer candidate = renderers[index];
                if (candidate == visualRenderer || candidate.sprite == null || IsSeparatePresentation(candidate.transform))
                {
                    continue;
                }

                Vector2 size = candidate.sprite.bounds.size;
                float area = size.x * size.y;
                if (area > selectedArea)
                {
                    selected = candidate;
                    selectedArea = area;
                }
            }

            return selected;
        }

        private bool IsSeparatePresentation(Transform candidate)
        {
            for (Transform current = candidate; current != null && current != transform; current = current.parent)
            {
                string lower = current.name.ToLowerInvariant();
                if (lower.Contains("sword") || lower.Contains("weapon") || lower.Contains("hitbox")
                    || lower.Contains("projectile") || lower.Contains("effect") || lower.Contains("vfx")
                    || lower.Contains("parry") || lower.Contains("charge"))
                {
                    return true;
                }
            }

            return false;
        }

        private VisualState ResolveState()
        {
            if (isDead || (health != null && health.CurrentHealth <= 0)) return VisualState.Death;
            if (Time.time < hitUntil && body != null && body.velocity.magnitude >= knockbackSpeedThreshold) return VisualState.Knockback;
            if (Time.time < hitUntil) return VisualState.Hit;
            if (brace != null && brace.IsBraced) return VisualState.Brace;
            if ((crossStep != null && crossStep.IsCrossStepping)
                || (dash != null && dash.IsDashing)
                || (crowdResponse != null && crowdResponse.IsSlipping)) return VisualState.Dash;
            if (guardBreak != null && (guardBreak.IsGuardBreaking || guardBreak.IsBashing)) return VisualState.GuardBreak;
            if (defense != null)
            {
                if (defense.CurrentPhase == ParryPhase.Perfect
                    || defense.CurrentPhase == ParryPhase.Normal
                    || defense.CurrentPhase == ParryPhase.Late) return VisualState.Parry;
                if (defense.IsGuardHeld) return VisualState.Guard;
            }
            if (spinAttack != null && spinAttack.IsAttacking) return VisualState.Spin;
            if ((chargedAttack != null && (chargedAttack.IsCharging || chargedAttack.IsAttacking))
                || (flightBash != null && flightBash.IsBashing)) return VisualState.Heavy;
            if (Time.time < interactUntil) return VisualState.Interact;
            if (Time.time < castUntil) return VisualState.Cast;
            if (playerController != null && !playerController.IsGrounded) return VisualState.Jump;
            if (Time.time < landingUntil) return VisualState.Landing;
            return body != null && Mathf.Abs(body.velocity.x) > runSpeedThreshold ? VisualState.Run : VisualState.Idle;
        }

        private Sprite[] FramesForState(VisualState state)
        {
            switch (state)
            {
                case VisualState.Run: return Frames("Miner_Run_");
                case VisualState.Jump: return Frames("Miner_Jump_");
                case VisualState.Landing: return Frames("Miner_Landing_");
                case VisualState.Heavy: return Frames("Miner_Heavy_");
                case VisualState.Spin: return Frames("Miner_Spin_");
                case VisualState.Guard: return Frames("Miner_Guard_");
                case VisualState.Parry: return Frames("Miner_Parry_");
                case VisualState.GuardBreak: return Frames("Miner_GuardBreak_");
                case VisualState.Dash: return Frames("Miner_Dash_");
                case VisualState.Hit: return Frames("Miner_Hit_");
                case VisualState.Knockback: return Frames("Miner_Knockback_");
                case VisualState.Death: return Frames("Miner_Death_");
                case VisualState.Interact: return Frames("Miner_Interact_");
                case VisualState.Cast: return Frames("Miner_Cast_");
                case VisualState.Brace: return Frames("Miner_Brace_");
                default: return Frames("Miner_Idle_");
            }
        }

        private Sprite ResolveCurrentFrame()
        {
            if (currentFrames == null || currentFrames.Length == 0) return null;
            float framesPerSecond;
            bool loop;
            switch (currentState)
            {
                case VisualState.Run: framesPerSecond = runFramesPerSecond; loop = true; break;
                case VisualState.Jump: framesPerSecond = 8f; loop = true; break;
                case VisualState.Landing: framesPerSecond = currentFrames.Length / landingDuration; loop = false; break;
                case VisualState.Heavy: framesPerSecond = 10f; loop = false; break;
                case VisualState.Spin: framesPerSecond = spinFramesPerSecond; loop = true; break;
                case VisualState.Guard: framesPerSecond = 3f; loop = true; break;
                case VisualState.Parry: framesPerSecond = 12f; loop = false; break;
                case VisualState.GuardBreak: framesPerSecond = 10f; loop = false; break;
                case VisualState.Dash: framesPerSecond = dashFramesPerSecond; loop = true; break;
                case VisualState.Hit: framesPerSecond = currentFrames.Length / hitDuration; loop = false; break;
                case VisualState.Knockback: framesPerSecond = 10f; loop = false; break;
                case VisualState.Death: framesPerSecond = 5f; loop = false; break;
                case VisualState.Interact: framesPerSecond = currentFrames.Length / interactDuration; loop = false; break;
                case VisualState.Cast: framesPerSecond = currentFrames.Length / castDuration; loop = false; break;
                case VisualState.Brace: framesPerSecond = 2f; loop = true; break;
                default: framesPerSecond = idleFramesPerSecond; loop = true; break;
            }
            int frame = Mathf.FloorToInt((Time.time - stateStartedAt) * Mathf.Max(1f, framesPerSecond));
            frame = loop ? frame % currentFrames.Length : Mathf.Min(frame, currentFrames.Length - 1);
            return currentFrames[Mathf.Max(0, frame)];
        }

        private static Sprite[] Frames(string prefix)
        {
            Sprite[] frames;
            return FrameCache.TryGetValue(prefix, out frames) ? frames : Array.Empty<Sprite>();
        }

        private static void CacheFrames()
        {
            if (cacheAttempted) return;
            cacheAttempted = true;
            Sprite[] loaded = Resources.LoadAll<Sprite>(ResourcePath);
            var grouped = new Dictionary<string, List<Sprite>>(StringComparer.Ordinal);
            for (int index = 0; index < loaded.Length; index++)
            {
                Sprite sprite = loaded[index];
                int separator = sprite.name.LastIndexOf('_');
                if (separator < 0) continue;
                string prefix = sprite.name.Substring(0, separator + 1);
                List<Sprite> list;
                if (!grouped.TryGetValue(prefix, out list))
                {
                    list = new List<Sprite>();
                    grouped.Add(prefix, list);
                }
                list.Add(sprite);
            }

            foreach (KeyValuePair<string, List<Sprite>> pair in grouped)
            {
                pair.Value.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
                FrameCache[pair.Key] = pair.Value.ToArray();
            }
        }

        private void HandleDamageTaken() { hitUntil = Mathf.Max(hitUntil, Time.time + hitDuration); }
        private void HandleDied() { isDead = true; }
        private void HandleRespawned() { isDead = false; hitUntil = 0f; }
        private void HandleProjectileFired(Vector2 _) { castUntil = Mathf.Max(castUntil, Time.time + castDuration); }
        private void HandleModeChanged(SpecialMode _) { castUntil = Mathf.Max(castUntil, Time.time + castDuration); }
    }
}
