using Cave.Combat;
using Cave.Diagnostics;
using Cave.Enemies;
using Cave.World;
using UnityEngine;

namespace Cave.Player
{
    public enum SwordCosmeticSelection
    {
        Default,
        Sword1,
        Sword2,
        Sword3,
        // Appended so existing PlayerPrefs selections retain their numeric identity.
        AstralNocturne,
        HeavensDefiance,
        AbyssalOssuary
    }

    /// <summary>Persistent, presentation-only cosmetic selection for the existing sword renderer.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSwordCosmetics : MonoBehaviour
    {
        public const float UpgradedSwordMultiplier = 1.2f;
        public const int SelectionCount = (int)SwordCosmeticSelection.AbyssalOssuary + 1;
        private const string Unlock1Key = "Cave.CosmeticSword1.Unlocked";
        private const string Unlock2Key = "Cave.CosmeticSword2.Unlocked";
        private const string Unlock3Key = "Cave.CosmeticSword3.Unlocked";
        private const string SelectionKey = "Cave.CosmeticSword.Selected";
        private const EnemyArchetype RequiredRunArchetypes = EnemyArchetype.Melee
            | EnemyArchetype.Ranged | EnemyArchetype.Tank | EnemyArchetype.Support | EnemyArchetype.Swarm;

        [Header("Test / Appearance")]
        [SerializeField] private SwordCosmeticSelection selectedAppearance;
        [SerializeField] private bool enableDebugCycleKey = true;
        [SerializeField] private KeyCode debugCycleKey = KeyCode.F8;

        [Header("Visual Calibration")]
        [SerializeField] private Vector3 sword1Offset = Vector3.zero;
        [SerializeField] private Vector3 sword2Offset = Vector3.zero;
        [SerializeField] private Vector3 sword3Offset = Vector3.zero;
        [SerializeField] private float sword1Rotation = -90f;
        [SerializeField] private float sword2Rotation = -90f;
        [SerializeField] private float sword3Rotation = -90f;
        [SerializeField, Min(0.01f)] private float sword1Scale = 0.72f;
        [SerializeField, Min(0.01f)] private float sword2Scale = 0.72f;
        [SerializeField, Min(0.01f)] private float sword3Scale = 0.72f;
        [Header("Editable Visual Prefabs")]
        [SerializeField] private GameObject sword1VisualPrefab;
        [SerializeField] private GameObject sword2VisualPrefab;
        [SerializeField] private GameObject sword3VisualPrefab;

        private SpinSwordAttack spinAttack;
        private SpriteRenderer swordRenderer;
        private Sprite defaultSprite;
        private SpriteDrawMode defaultDrawMode;
        private Vector2 defaultSize;
        private Vector3 authoredPosition;
        private Quaternion authoredRotation;
        private Vector3 authoredScale;
        private Sprite[] cosmeticSprites;
        private EnemyArchetype defeatedThisRun;
        private WorldDifficultyManager difficulty;
        private bool visualApplied;
        private GameObject activeVisualInstance;

        public SwordCosmeticSelection SelectedAppearance => selectedAppearance;
        public bool Sword1Unlocked => PlayerPrefs.GetInt(Unlock1Key, 0) != 0;
        public bool Sword2Unlocked => PlayerPrefs.GetInt(Unlock2Key, 0) != 0;
        public bool Sword3Unlocked => PlayerPrefs.GetInt(Unlock3Key, 0) != 0;
        public bool IsUpgradedSword => IsUpgraded(selectedAppearance);
        public float EffectiveReachMultiplier => ReachMultiplierFor(selectedAppearance);

        public static bool IsUpgraded(SwordCosmeticSelection selection)
        {
            return selection == SwordCosmeticSelection.Sword1
                || selection == SwordCosmeticSelection.Sword2
                || selection == SwordCosmeticSelection.Sword3;
        }

        public static float ReachMultiplierFor(SwordCosmeticSelection selection)
        {
            return IsUpgraded(selection) ? UpgradedSwordMultiplier : 1f;
        }

        /// <summary>Legendary entries are visual-only two-handed replacements for the existing sword presentation.</summary>
        public static bool IsLegendaryWeapon(SwordCosmeticSelection selection)
        {
            return selection == SwordCosmeticSelection.AstralNocturne
                || selection == SwordCosmeticSelection.HeavensDefiance
                || selection == SwordCosmeticSelection.AbyssalOssuary;
        }

        public static bool IsTwoHandedWeapon(SwordCosmeticSelection selection)
        {
            return IsLegendaryWeapon(selection);
        }

        public static string DisplayNameFor(SwordCosmeticSelection selection)
        {
            switch (selection)
            {
                case SwordCosmeticSelection.AstralNocturne:
                    return "ASTRAL NOCTURNE";
                case SwordCosmeticSelection.HeavensDefiance:
                    return "HEAVEN'S DEFIANCE";
                case SwordCosmeticSelection.AbyssalOssuary:
                    return "ABYSSAL OSSUARY";
                case SwordCosmeticSelection.Sword1:
                case SwordCosmeticSelection.Sword2:
                case SwordCosmeticSelection.Sword3:
                    return "SWORD " + (int)selection;
                default:
                    return "DEFAULT SWORD";
            }
        }

        public static string DescriptionFor(SwordCosmeticSelection selection)
        {
            switch (selection)
            {
                case SwordCosmeticSelection.AstralNocturne:
                    return "Celestial Rose Scythe\nTwo-handed cosmetic weapon.\nNo gameplay stats, hitboxes, or timing change.";
                case SwordCosmeticSelection.HeavensDefiance:
                    return "Celestial Angel Poleaxe\nTwo-handed cosmetic weapon.\nNo gameplay stats, hitboxes, or timing change.";
                case SwordCosmeticSelection.AbyssalOssuary:
                    return "Leviathan Double Crescent Axe\nTwo-handed cosmetic weapon.\nNo gameplay stats, hitboxes, or timing change.";
                case SwordCosmeticSelection.Default:
                    return "A reliable blade for the depths.";
                default:
                    return "Existing sword cosmetic.\nReach multiplier: 1.20×.";
            }
        }

        public static string ResourcePathFor(SwordCosmeticSelection selection)
        {
            switch (selection)
            {
                case SwordCosmeticSelection.Sword1:
                    return "Cosmetics/Swords/Sword01";
                case SwordCosmeticSelection.Sword2:
                    return "Cosmetics/Swords/Sword02";
                case SwordCosmeticSelection.Sword3:
                    return "Cosmetics/Swords/Sword03";
                case SwordCosmeticSelection.AstralNocturne:
                    return "Cosmetics/Weapons/Astral_Nocturne";
                case SwordCosmeticSelection.HeavensDefiance:
                    return "Cosmetics/Weapons/Heavens_Defiance";
                case SwordCosmeticSelection.AbyssalOssuary:
                    return "Cosmetics/Weapons/Abyssal_Ossuary";
                default:
                    return null;
            }
        }

        public static Sprite LoadSpriteFor(SwordCosmeticSelection selection)
        {
            string path = ResourcePathFor(selection);
            return string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
        }

        public static void EnsureInstalled(GameObject player)
        {
            if (player != null && player.GetComponent<PlayerSwordCosmetics>() == null)
            {
                player.AddComponent<PlayerSwordCosmetics>();
            }
        }

        /// <summary>Called only by the combat death path for an actual player killing blow.</summary>
        public static void NotifyPlayerDefeatedEnemy(Damageable victim, DamageContext context)
        {
            if (!context.IsPlayerDamage || victim == null || context.Source == null)
            {
                return;
            }

            context.Source.GetComponent<PlayerSwordCosmetics>()?.RecordEnemyDefeat(victim);
        }

        private void Awake()
        {
            spinAttack = GetComponent<SpinSwordAttack>();
            GameObject swordObject = spinAttack != null ? spinAttack.SwordVisualObject : null;
            swordRenderer = swordObject != null
                ? swordObject.GetComponentInChildren<SpriteRenderer>(true)
                : null;
            if (swordRenderer == null)
            {
                enabled = false;
                Debug.LogWarning("Sword cosmetics could not find the existing SpinSwordAttack sword renderer.", this);
                return;
            }

            defaultSprite = swordRenderer.sprite;
            defaultDrawMode = swordRenderer.drawMode;
            defaultSize = swordRenderer.size;
            authoredPosition = swordRenderer.transform.localPosition;
            authoredRotation = swordRenderer.transform.localRotation;
            authoredScale = swordRenderer.transform.localScale;
            cosmeticSprites = new[]
            {
                null,
                LoadSpriteFor(SwordCosmeticSelection.Sword1),
                LoadSpriteFor(SwordCosmeticSelection.Sword2),
                LoadSpriteFor(SwordCosmeticSelection.Sword3),
                LoadSpriteFor(SwordCosmeticSelection.AstralNocturne),
                LoadSpriteFor(SwordCosmeticSelection.HeavensDefiance),
                LoadSpriteFor(SwordCosmeticSelection.AbyssalOssuary)
            };
            selectedAppearance = (SwordCosmeticSelection)Mathf.Clamp(
                PlayerPrefs.GetInt(SelectionKey, (int)selectedAppearance),
                0,
                SelectionCount - 1);
            if (!IsAvailable(selectedAppearance))
            {
                selectedAppearance = SwordCosmeticSelection.Default;
                SaveSelection(selectedAppearance);
            }
            ApplySelection(selectedAppearance, false);
        }

        private void OnEnable()
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Died -= ResetRunArchetypes;
                health.Died += ResetRunArchetypes;
            }
            DeveloperDiagnosticsSettings.Changed -= HandleDeveloperSettingsChanged;
            DeveloperDiagnosticsSettings.Changed += HandleDeveloperSettingsChanged;
            RevertUnavailableSelectionIfNeeded();
        }

        private void Start()
        {
            difficulty = FindObjectOfType<WorldDifficultyManager>();
            if (difficulty != null)
            {
                difficulty.DifficultyChanged -= CheckWorldLevelUnlock;
                difficulty.DifficultyChanged += CheckWorldLevelUnlock;
                CheckWorldLevelUnlock();
            }
        }

        private void Update()
        {
            if (enableDebugCycleKey && Input.GetKeyDown(debugCycleKey))
            {
                Select((SwordCosmeticSelection)(((int)selectedAppearance + 1) % SelectionCount));
            }
        }

        public bool Select(SwordCosmeticSelection selection)
        {
            if (!IsAvailable(selection))
            {
                return false;
            }

            return ApplySelection(selection, true);
        }

        private void RecordEnemyDefeat(Damageable victim)
        {
            SkeletonInheritance skeleton = victim.GetComponent<SkeletonInheritance>();
            if (skeleton != null && skeleton.Rank == SkeletonRank.General)
            {
                Unlock(Unlock1Key);
            }

            EnemyArchetypeProfile profile = victim.GetComponent<EnemyArchetypeProfile>();
            if (profile == null)
            {
                return;
            }

            defeatedThisRun |= profile.Archetypes;
            if ((defeatedThisRun & RequiredRunArchetypes) == RequiredRunArchetypes)
            {
                Unlock(Unlock2Key);
            }
        }

        private bool ApplySelection(SwordCosmeticSelection selection, bool save)
        {
            if (swordRenderer == null)
            {
                return false;
            }

            int index = (int)selection;
            Sprite sprite = index >= 0 && index < cosmeticSprites.Length
                ? cosmeticSprites[index]
                : null;
            if (selection != SwordCosmeticSelection.Default && sprite == null
                && VisualPrefabFor(selection) == null)
            {
                Debug.LogWarning("Requested sword cosmetic sprite is unavailable: " + selection + ".", this);
                return false;
            }

            if (visualApplied && selectedAppearance == selection)
            {
                if (save)
                {
                    SaveSelection(selection);
                }

                return true;
            }

            selectedAppearance = selection;
            spinAttack?.SetWeaponReachMultiplier(ReachMultiplierFor(selection));
            if (activeVisualInstance != null)
            {
                activeVisualInstance.SetActive(false);
                Destroy(activeVisualInstance);
                activeVisualInstance = null;
            }
            GameObject visualPrefab = VisualPrefabFor(selection);
            if (selection != SwordCosmeticSelection.Default && visualPrefab != null)
            {
                Transform anchor = swordRenderer.transform.parent != null
                    ? swordRenderer.transform.parent
                    : swordRenderer.transform;
                activeVisualInstance = Instantiate(visualPrefab, anchor);
                activeVisualInstance.name = visualPrefab.name;
                activeVisualInstance.transform.localPosition += authoredPosition + OffsetFor(selection);
                activeVisualInstance.transform.localRotation = authoredRotation
                    * activeVisualInstance.transform.localRotation
                    * Quaternion.Euler(0f, 0f, RotationFor(selection));
                activeVisualInstance.transform.localScale = Vector3.Scale(authoredScale,
                    activeVisualInstance.transform.localScale) * ScaleFor(selection);
                swordRenderer.enabled = false;
                visualApplied = true;
                if (save) SaveSelection(selection);
                return true;
            }
            swordRenderer.enabled = true;
            Sprite desiredSprite = selection == SwordCosmeticSelection.Default ? defaultSprite : sprite;
            if (swordRenderer.sprite != desiredSprite)
            {
                swordRenderer.sprite = desiredSprite;
            }

            swordRenderer.drawMode = selection == SwordCosmeticSelection.Default
                ? defaultDrawMode
                : SpriteDrawMode.Simple;
            swordRenderer.size = defaultSize;
            swordRenderer.transform.localPosition = authoredPosition + OffsetFor(selection);
            swordRenderer.transform.localRotation = authoredRotation
                * Quaternion.Euler(0f, 0f, RotationFor(selection));
            swordRenderer.transform.localScale = authoredScale * ScaleFor(selection);
            visualApplied = true;
            if (save)
            {
                SaveSelection(selection);
            }

            return true;
        }

        private static void SaveSelection(SwordCosmeticSelection selection)
        {
            PlayerPrefs.SetInt(SelectionKey, (int)selection);
            PlayerPrefs.Save();
        }

        public bool IsUnlocked(SwordCosmeticSelection selection)
        {
            switch (selection)
            {
                case SwordCosmeticSelection.Sword1:
                    return Sword1Unlocked;
                case SwordCosmeticSelection.Sword2:
                    return Sword2Unlocked;
                case SwordCosmeticSelection.Sword3:
                    return Sword3Unlocked;
                case SwordCosmeticSelection.Default:
                    return true;
                default:
                    // Production unlock requirements are intentionally not invented.
                    // The existing development cosmetics override exposes these entries.
                    return false;
            }
        }

        public bool IsAvailable(SwordCosmeticSelection selection)
        {
            return IsAvailableForPolicy(selection, IsUnlocked(selection),
                DeveloperDiagnosticsSettings.EffectiveUnlockAllCosmetics);
        }

        /// <summary>Pure counterpart of the live availability decision for verification.</summary>
        internal static bool IsAvailableForPolicy(SwordCosmeticSelection selection,
            bool actuallyUnlocked, bool developerUnlockAllCosmetics)
        {
            return selection >= SwordCosmeticSelection.Default
                && selection <= SwordCosmeticSelection.AbyssalOssuary
                && CosmeticAvailability.IsAvailable(actuallyUnlocked, developerUnlockAllCosmetics);
        }

        private void CheckWorldLevelUnlock()
        {
            if (difficulty != null && difficulty.DifficultyTier >= 10)
            {
                Unlock(Unlock3Key);
            }
        }

        private static void Unlock(string key)
        {
            if (PlayerPrefs.GetInt(key, 0) != 0)
            {
                return;
            }

            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        private void ResetRunArchetypes()
        {
            defeatedThisRun = EnemyArchetype.None;
        }

        private Vector3 OffsetFor(SwordCosmeticSelection selection)
        {
            if (selection == SwordCosmeticSelection.Sword1) return sword1Offset;
            if (selection == SwordCosmeticSelection.Sword2) return sword2Offset;
            if (selection == SwordCosmeticSelection.Sword3) return sword3Offset;

            // Their source pivots are authored at the two-handed grip. These offsets
            // only make the existing sword hand anchor line up with that grip.
            if (selection == SwordCosmeticSelection.AstralNocturne) return new Vector3(0f, -0.02f, 0f);
            if (selection == SwordCosmeticSelection.HeavensDefiance) return new Vector3(0f, -0.04f, 0f);
            return Vector3.zero;
        }

        private float RotationFor(SwordCosmeticSelection selection)
        {
            if (selection == SwordCosmeticSelection.Sword1) return sword1Rotation;
            if (selection == SwordCosmeticSelection.Sword2) return sword2Rotation;
            if (selection == SwordCosmeticSelection.Sword3) return sword3Rotation;
            return IsLegendaryWeapon(selection) ? -90f : 0f;
        }

        private float ScaleFor(SwordCosmeticSelection selection)
        {
            if (selection == SwordCosmeticSelection.Sword1) return sword1Scale * UpgradedSwordMultiplier;
            if (selection == SwordCosmeticSelection.Sword2) return sword2Scale * UpgradedSwordMultiplier;
            if (selection == SwordCosmeticSelection.Sword3) return sword3Scale * UpgradedSwordMultiplier;

            // Normalized, uniform visual calibration only. It affects neither the
            // existing attack collider nor its reach multiplier.
            if (selection == SwordCosmeticSelection.AstralNocturne) return .93f;
            if (selection == SwordCosmeticSelection.HeavensDefiance) return .93f;
            return selection == SwordCosmeticSelection.AbyssalOssuary ? 1.1f : 1f;
        }

        private GameObject VisualPrefabFor(SwordCosmeticSelection selection)
        {
            if (selection == SwordCosmeticSelection.Sword1) return sword1VisualPrefab;
            if (selection == SwordCosmeticSelection.Sword2) return sword2VisualPrefab;
            return selection == SwordCosmeticSelection.Sword3 ? sword3VisualPrefab : null;
        }

        private void OnDisable()
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Died -= ResetRunArchetypes;
            }

            if (difficulty != null)
            {
                difficulty.DifficultyChanged -= CheckWorldLevelUnlock;
            }
            DeveloperDiagnosticsSettings.Changed -= HandleDeveloperSettingsChanged;
        }

        private void HandleDeveloperSettingsChanged()
        {
            RevertUnavailableSelectionIfNeeded();
        }

        private void RevertUnavailableSelectionIfNeeded()
        {
            if (IsAvailable(selectedAppearance) || selectedAppearance == SwordCosmeticSelection.Default) return;
            ApplySelection(SwordCosmeticSelection.Default, true);
        }
    }
}
