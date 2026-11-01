using System;
using Cave.Axioms.Mastery;
using Cave.Combat;
using Cave.Diagnostics;
using Cave.InputSystem;
using Cave.Player;
using UnityEngine;

namespace Cave.Projectiles
{
    public enum ManifestationOrigin
    {
        Body = 0,
        Anchor = 1,
        Orbital = 2
    }

    public static class ManifestationOriginPolicy
    {
        public const float RemoteOriginUpkeepMultiplier = .25f;

        public static float ResolveUpkeep(
            ManifestationOrigin origin,
            int activeTier,
            float tierOne,
            float perAdditionalTier)
        {
            if (origin == ManifestationOrigin.Body) return 0f;
            float authored = Mathf.Max(0f, tierOne)
                + Mathf.Max(0, activeTier - 1) * Mathf.Max(0f, perAdditionalTier);
            return authored * RemoteOriginUpkeepMultiplier;
        }

        public static Vector2 ResolveLaunchDirection(Vector2 manifestation, Vector2 player)
        {
            Vector2 delta = player - manifestation;
            return delta.sqrMagnitude > .0001f ? delta.normalized : Vector2.zero;
        }

        public static float ResolveOrbitalHorizontalOffset(float elapsed, float cycle, float extent)
        {
            return Mathf.Sin(Mathf.Max(0f, elapsed) / Mathf.Max(.1f, cycle)
                * Mathf.PI * 2f) * Mathf.Max(0f, extent);
        }
    }

    /// <summary>
    /// Persistent normal-progression record for the Manifestation Seed. Normal
    /// acquisition is intentionally left behind this explicit seam until its
    /// production reward is approved; developer entitlement never writes it.
    /// </summary>
    public static class ManifestationProgression
    {
        public const string SeedKey = "Cave.Manifestation.Seed.Acquired.v1";
        public static event Action Changed;
        public static bool HasSeed => PlayerPrefs.GetInt(SeedKey, 0) != 0;

        public static bool GrantSeed()
        {
            if (HasSeed) return false;
            PlayerPrefs.SetInt(SeedKey, 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public static void ResetForNewGame()
        {
            if (!PlayerPrefs.HasKey(SeedKey)) return;
            PlayerPrefs.DeleteKey(SeedKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Sole player-owned authority for ordinary-projectile origin selection,
    /// deployment, upkeep and presentation. It owns no Fire Pattern cadence,
    /// projectile cost, damage, hit or Heavy behavior.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMana), typeof(PlayerSpecialModeUpgradeState))]
    public sealed class PlayerManifestationOrigin : MonoBehaviour
    {
        private const string SelectionKey = "Cave.Manifestation.Origin.Selection.v1";

        [Header("Deployment")]
        [SerializeField, Min(0f)] private float deploymentManaCost = 20f;
        [SerializeField] private Vector2 anchorFacingOffset = new Vector2(4f, 1.5f);
        [SerializeField, Min(.05f)] private float placementClearanceRadius = .35f;
        [SerializeField] private LayerMask solidPlacementLayers = Physics2D.DefaultRaycastLayers;

        [Header("Orbital — provisional tuning")]
        [SerializeField, Min(0f)] private float orbitalHeight = 6f;
        [SerializeField, Min(0f)] private float orbitalHorizontalExtent = 10f;
        [SerializeField, Min(.1f)] private float orbitalTraversalCycle = 6f;

        [Header("Upkeep")]
        [SerializeField, Min(0f)] private float tierOneUpkeep = 8f;
        [SerializeField, Min(0f)] private float upkeepPerAdditionalTier = 2f;

        [Header("Provisional normal unlock thresholds")]
        [SerializeField, Range(0f, 1f)] private float anchorPhaseMasteryRequired = .35f;
        [SerializeField, Range(0f, 1f)] private float orbitalPhaseMasteryRequired = .65f;

        [Header("Presentation")]
        [SerializeField] private string anchorSpriteResource =
            "Projectiles/Manifestations/Anchor_Basilisk_Gate";
        [SerializeField] private string orbitalSpriteResource =
            "Projectiles/Manifestations/Orbital_Gilded_Illusion";
        [SerializeField, Min(.1f)] private float anchorVisualScale = 1f;
        [SerializeField, Min(.1f)] private float orbitalVisualScale = 1f;
        [SerializeField] private int sortingOrder = 7;

        [SerializeField] private ManifestationOrigin selectedOrigin = ManifestationOrigin.Body;
        [SerializeField] private ManifestationOrigin deployedOrigin = ManifestationOrigin.Body;

        private PlayerMana mana;
        private PlayerSpecialMode specialMode;
        private PlayerSpecialModeUpgradeState upgrades;
        private AxiomMasteryState mastery;
        private PlayerHealth health;
        private PlayerProjectileLauncher launcher;
        private Transform presentation;
        private ManifestationOriginVisual presentationVisual;
        private ManifestationOriginVisual retiringVisual;
        private float orbitalStartedAt;

        public event Action Changed;
        public event Action<string> FeedbackRequested;

        public ManifestationOrigin SelectedOrigin => selectedOrigin;
        public ManifestationOrigin DeployedOrigin => IsDeployed ? deployedOrigin : ManifestationOrigin.Body;
        public bool IsDeployed => presentation != null && deployedOrigin != ManifestationOrigin.Body;
        public float DeploymentManaCost => deploymentManaCost;
        public float CurrentUpkeepPerSecond => ResolveUpkeep(
            IsDeployed ? deployedOrigin : selectedOrigin,
            CurrentSkillPathTier);
        public int CurrentSkillPathTier => upgrades != null && specialMode != null
            ? upgrades.GetCurrentTier(specialMode.CurrentMode)
            : 1;
        public float PhaseMastery => mastery != null ? mastery.Get(MasteryDomain.Phase) : 0f;
        public bool HasNormalSeed => ManifestationProgression.HasSeed;
        public bool DevelopmentOverride => DeveloperDiagnosticsSettings.EffectiveUnlockManifestations;
        public string RecallBinding => GameInput.Bindings.GetBinding(GameAction.RecallManifestation).Primary.ToString();

        public static PlayerManifestationOrigin EnsureOn(GameObject owner)
        {
            if (owner == null) return null;
            PlayerManifestationOrigin state = owner.GetComponent<PlayerManifestationOrigin>();
            return state != null ? state : owner.AddComponent<PlayerManifestationOrigin>();
        }

        private void Awake()
        {
            mana = GetComponent<PlayerMana>();
            specialMode = GetComponent<PlayerSpecialMode>();
            upgrades = GetComponent<PlayerSpecialModeUpgradeState>();
            mastery = AxiomMasteryState.EnsureOn(gameObject);
            health = GetComponent<PlayerHealth>();
            launcher = GetComponent<PlayerProjectileLauncher>();
            selectedOrigin = (ManifestationOrigin)Mathf.Clamp(
                PlayerPrefs.GetInt(SelectionKey, (int)selectedOrigin),
                (int)ManifestationOrigin.Body,
                (int)ManifestationOrigin.Orbital);
            deployedOrigin = ManifestationOrigin.Body;
            if (!IsUnlocked(selectedOrigin)) selectedOrigin = ManifestationOrigin.Body;
        }

        private void OnEnable()
        {
            if (health != null) health.Died += HandleDeath;
            if (launcher != null) launcher.ProjectileCommitted += HandleProjectileCommitted;
            DeveloperDiagnosticsSettings.Changed += HandleEntitlementChanged;
            ManifestationProgression.Changed += HandleEntitlementChanged;
            if (mastery != null) mastery.MasteryChanged += HandleMasteryChanged;
            if (specialMode != null) specialMode.SpecialModeChanged += HandleSkillPathChanged;
            if (upgrades != null)
            {
                upgrades.Tier2OwnershipChanged += HandleSkillPathChanged;
                upgrades.Tier3OwnershipChanged += HandleSkillPathChanged;
            }
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= HandleDeath;
            if (launcher != null) launcher.ProjectileCommitted -= HandleProjectileCommitted;
            DeveloperDiagnosticsSettings.Changed -= HandleEntitlementChanged;
            ManifestationProgression.Changed -= HandleEntitlementChanged;
            if (mastery != null) mastery.MasteryChanged -= HandleMasteryChanged;
            if (specialMode != null) specialMode.SpecialModeChanged -= HandleSkillPathChanged;
            if (upgrades != null)
            {
                upgrades.Tier2OwnershipChanged -= HandleSkillPathChanged;
                upgrades.Tier3OwnershipChanged -= HandleSkillPathChanged;
            }
            Recall(false);
        }

        private void Update()
        {
            if (GameInput.RecallManifestationPressed)
            {
                Recall(true);
            }

            if (!IsDeployed) return;

            if (!IsUnlocked(deployedOrigin))
            {
                Recall(false);
                FeedbackRequested?.Invoke("Manifestation access is no longer available.");
                return;
            }

            if (deployedOrigin == ManifestationOrigin.Orbital)
            {
                UpdateOrbitalPosition(Time.time);
            }

            float upkeep = CurrentUpkeepPerSecond * Mathf.Max(0f, Time.deltaTime);
            if (upkeep <= 0f) return;
            if (mana == null || !mana.TrySpendFixedMana(upkeep))
            {
                Recall(false);
                FeedbackRequested?.Invoke("Manifestation recalled — insufficient mana for upkeep.");
            }
        }

        public bool Select(ManifestationOrigin origin)
        {
            if (origin < ManifestationOrigin.Body || origin > ManifestationOrigin.Orbital)
                return false;
            if (!IsUnlocked(origin))
            {
                FeedbackRequested?.Invoke(RequirementText(origin));
                return false;
            }

            if (selectedOrigin == origin) return true;
            Recall(false);
            selectedOrigin = origin;
            PlayerPrefs.SetInt(SelectionKey, (int)selectedOrigin);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Returns true only when the ordinary Fire press belongs to deployment.
        /// It remains true on failure so the press can never fall through to Body fire.
        /// </summary>
        public bool TryConsumeDeploymentPress()
        {
            if (selectedOrigin == ManifestationOrigin.Body || IsDeployed) return false;
            if (!IsUnlocked(selectedOrigin))
            {
                FeedbackRequested?.Invoke(RequirementText(selectedOrigin));
                return true;
            }
            if (mana == null || mana.CurrentMana < deploymentManaCost)
            {
                FeedbackRequested?.Invoke("Need " + deploymentManaCost.ToString("0") + " mana to deploy "
                    + DisplayName(selectedOrigin) + ".");
                return true;
            }

            Vector2 position;
            if (!TryResolveDeploymentPosition(selectedOrigin, out position))
            {
                FeedbackRequested?.Invoke("No safe manifestation placement is available.");
                return true;
            }
            if (!mana.TrySpendFixedMana(deploymentManaCost))
            {
                FeedbackRequested?.Invoke("Manifestation deployment failed without spending mana.");
                return true;
            }

            CreatePresentation(selectedOrigin, position);
            FeedbackRequested?.Invoke(DisplayName(selectedOrigin) + " deployed. Recall: " + RecallBinding + ".");
            Changed?.Invoke();
            return true;
        }

        /// <summary>Reads one committed ordinary-shot launch context.</summary>
        public bool TryGetLaunchContext(out Vector2 position, out Vector2 direction)
        {
            position = transform.position;
            direction = Vector2.right;
            if (!IsDeployed || presentation == null) return false;

            Vector2 manifested = presentation.position;
            direction = ManifestationOriginPolicy.ResolveLaunchDirection(manifested, transform.position);
            if (direction.sqrMagnitude <= .0001f) return false;
            return TryResolveSafeMuzzle(manifested, direction, out position);
        }

        public bool IsUnlocked(ManifestationOrigin origin)
        {
            if (origin == ManifestationOrigin.Body) return true;
            if (DevelopmentOverride) return true;
            return IsNormallyUnlocked(origin);
        }

        public bool IsNormallyUnlocked(ManifestationOrigin origin)
        {
            if (origin == ManifestationOrigin.Body) return true;
            if (!ManifestationProgression.HasSeed) return false;
            float required = origin == ManifestationOrigin.Anchor
                ? anchorPhaseMasteryRequired
                : orbitalPhaseMasteryRequired;
            return PhaseMastery >= required;
        }

        public string RequirementText(ManifestationOrigin origin)
        {
            if (origin == ManifestationOrigin.Body) return "Always available.";
            float required = origin == ManifestationOrigin.Anchor
                ? anchorPhaseMasteryRequired
                : orbitalPhaseMasteryRequired;
            return "Requires Manifestation Seed and Phase mastery "
                + Mathf.RoundToInt(required * 100f) + "% (provisional).";
        }

        public void Recall(bool showFeedback)
        {
            if (presentationVisual != null)
            {
                if (retiringVisual != null && retiringVisual != presentationVisual)
                    Destroy(retiringVisual.gameObject);
                retiringVisual = presentationVisual;
                presentationVisual.PlayRecall();
            }
            else if (presentation != null)
            {
                Destroy(presentation.gameObject);
            }
            bool wasDeployed = deployedOrigin != ManifestationOrigin.Body;
            presentation = null;
            presentationVisual = null;
            deployedOrigin = ManifestationOrigin.Body;
            if (!wasDeployed) return;
            if (showFeedback) FeedbackRequested?.Invoke("Manifestation recalled.");
            Changed?.Invoke();
        }

        public float ResolveUpkeep(ManifestationOrigin origin, int tier)
        {
            return ManifestationOriginPolicy.ResolveUpkeep(
                origin, tier, tierOneUpkeep, upkeepPerAdditionalTier);
        }

        /// <summary>Explicit new-game reset; ordinary death only recalls the live projection.</summary>
        public void ResetForNewGame()
        {
            Recall(false);
            ManifestationProgression.ResetForNewGame();
            selectedOrigin = ManifestationOrigin.Body;
            PlayerPrefs.DeleteKey(SelectionKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private bool TryResolveDeploymentPosition(ManifestationOrigin origin, out Vector2 position)
        {
            if (origin == ManifestationOrigin.Orbital)
            {
                position = (Vector2)transform.position + Vector2.up * orbitalHeight;
                return IsMuzzleClear(position);
            }

            PlayerAimDirection aim = GetComponent<PlayerAimDirection>();
            float facing = aim != null && aim.FacingDirection.x < 0f ? -1f : 1f;
            Vector2 baseOffset = new Vector2(Mathf.Abs(anchorFacingOffset.x) * facing, anchorFacingOffset.y);
            Vector2[] candidates =
            {
                (Vector2)transform.position + baseOffset,
                (Vector2)transform.position + new Vector2(baseOffset.x * .75f, baseOffset.y + 1f),
                (Vector2)transform.position + new Vector2(baseOffset.x * .5f, baseOffset.y + 2f)
            };
            for (int index = 0; index < candidates.Length; index++)
            {
                if (IsMuzzleClear(candidates[index]))
                {
                    position = candidates[index];
                    return true;
                }
            }
            position = transform.position;
            return false;
        }

        private bool TryResolveSafeMuzzle(Vector2 requested, Vector2 direction, out Vector2 safe)
        {
            safe = requested;
            if (IsMuzzleClear(safe)) return true;
            // Move only toward the owning player, preserving the manifestation's
            // firing line while avoiding a shot born inside solid geometry.
            for (int step = 1; step <= 12; step++)
            {
                safe = requested + direction * (step * .25f);
                if (IsMuzzleClear(safe)) return true;
            }
            return false;
        }

        private bool IsMuzzleClear(Vector2 position)
        {
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(
                position, placementClearanceRadius, solidPlacementLayers.value);
            for (int index = 0; index < overlaps.Length; index++)
            {
                Collider2D candidate = overlaps[index];
                if (candidate == null || candidate.isTrigger) continue;
                if (candidate.transform == transform || candidate.transform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }

        private void CreatePresentation(ManifestationOrigin origin, Vector2 position)
        {
            Recall(false);
            if (retiringVisual != null)
            {
                Destroy(retiringVisual.gameObject);
                retiringVisual = null;
            }
            GameObject visual = new GameObject("Manifestation " + DisplayName(origin));
            visual.transform.position = position;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = Resources.Load<Sprite>(origin == ManifestationOrigin.Anchor
                ? anchorSpriteResource : orbitalSpriteResource);
            if (renderer.sprite == null)
            {
                renderer.sprite = Resources.Load<Sprite>(origin == ManifestationOrigin.Anchor
                    ? "UI/NexusSkill/Icons/Anchor" : "UI/NexusSkill/Icons/Orbital");
                Debug.LogError("[Cave] Approved manifestation sprite is missing for " + origin
                    + "; using the legacy UI icon as a presentation fallback.", this);
            }
            presentationVisual = visual.AddComponent<ManifestationOriginVisual>();
            presentationVisual.Configure(renderer, origin,
                origin == ManifestationOrigin.Anchor ? anchorVisualScale : orbitalVisualScale,
                sortingOrder);
            presentation = visual.transform;
            deployedOrigin = origin;
            orbitalStartedAt = Time.time;
            if (origin == ManifestationOrigin.Orbital) UpdateOrbitalPosition(orbitalStartedAt);
        }

        private void UpdateOrbitalPosition(float now)
        {
            if (presentation == null) return;
            float x = ManifestationOriginPolicy.ResolveOrbitalHorizontalOffset(
                now - orbitalStartedAt, orbitalTraversalCycle, orbitalHorizontalExtent);
            presentation.position = (Vector2)transform.position + new Vector2(x, orbitalHeight);
        }

        private void HandleDeath() => Recall(false);
        private void HandleProjectileCommitted(SpecialMode mode, bool isHeavy)
        {
            if (!isHeavy && presentationVisual != null)
                presentationVisual.PlayFiringResponse();
        }
        private void HandleMasteryChanged(MasteryDomain domain, float value)
        {
            if (domain == MasteryDomain.Phase) HandleEntitlementChanged();
        }
        private void HandleSkillPathChanged(SpecialMode mode) => Changed?.Invoke();
        private void HandleEntitlementChanged()
        {
            if (IsDeployed && !IsUnlocked(deployedOrigin)) Recall(false);
            if (!IsUnlocked(selectedOrigin))
            {
                selectedOrigin = ManifestationOrigin.Body;
                PlayerPrefs.SetInt(SelectionKey, (int)selectedOrigin);
                PlayerPrefs.Save();
            }
            Changed?.Invoke();
        }

        public static string DisplayName(ManifestationOrigin origin)
        {
            return origin == ManifestationOrigin.Anchor ? "ANCHOR — BASILISK GATE"
                : origin == ManifestationOrigin.Orbital ? "ORBITAL — GILDED ILLUSION"
                : "BODY";
        }
    }
}
