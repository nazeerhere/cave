using Cave.Combat;
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
        Sword3
    }

    /// <summary>Persistent, presentation-only cosmetic selection for the existing sword renderer.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSwordCosmetics : MonoBehaviour
    {
        private const string Unlock1Key = "Cave.CosmeticSword1.Unlocked";
        private const string Unlock2Key = "Cave.CosmeticSword2.Unlocked";
        private const string Unlock3Key = "Cave.CosmeticSword3.Unlocked";
        private const string SelectionKey = "Cave.CosmeticSword.Selected";
        private const EnemyArchetype RequiredRunArchetypes = EnemyArchetype.Melee
            | EnemyArchetype.Ranged | EnemyArchetype.Tank | EnemyArchetype.Support | EnemyArchetype.Swarm;

        [Header("Test / Appearance")]
        [SerializeField] private SwordCosmeticSelection selectedAppearance;
        [SerializeField] private bool allowLockedDebugSelection = true;
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

        public SwordCosmeticSelection SelectedAppearance => selectedAppearance;
        public bool Sword1Unlocked => PlayerPrefs.GetInt(Unlock1Key, 0) != 0;
        public bool Sword2Unlocked => PlayerPrefs.GetInt(Unlock2Key, 0) != 0;
        public bool Sword3Unlocked => PlayerPrefs.GetInt(Unlock3Key, 0) != 0;

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
                Resources.Load<Sprite>("Cosmetics/Swords/Sword01"),
                Resources.Load<Sprite>("Cosmetics/Swords/Sword02"),
                Resources.Load<Sprite>("Cosmetics/Swords/Sword03")
            };
            selectedAppearance = (SwordCosmeticSelection)Mathf.Clamp(
                PlayerPrefs.GetInt(SelectionKey, (int)selectedAppearance),
                0,
                3);
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
                Select((SwordCosmeticSelection)(((int)selectedAppearance + 1) % 4), true);
            }
        }

        public bool Select(SwordCosmeticSelection selection, bool debugOverride = false)
        {
            if (!debugOverride && !IsUnlocked(selection))
            {
                return false;
            }

            if (debugOverride && !allowLockedDebugSelection && !IsUnlocked(selection))
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
            if (selection != SwordCosmeticSelection.Default && sprite == null)
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

        private bool IsUnlocked(SwordCosmeticSelection selection)
        {
            switch (selection)
            {
                case SwordCosmeticSelection.Sword1:
                    return Sword1Unlocked;
                case SwordCosmeticSelection.Sword2:
                    return Sword2Unlocked;
                case SwordCosmeticSelection.Sword3:
                    return Sword3Unlocked;
                default:
                    return true;
            }
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
            return selection == SwordCosmeticSelection.Sword3 ? sword3Offset : Vector3.zero;
        }

        private float RotationFor(SwordCosmeticSelection selection)
        {
            if (selection == SwordCosmeticSelection.Sword1) return sword1Rotation;
            if (selection == SwordCosmeticSelection.Sword2) return sword2Rotation;
            return selection == SwordCosmeticSelection.Sword3 ? sword3Rotation : 0f;
        }

        private float ScaleFor(SwordCosmeticSelection selection)
        {
            if (selection == SwordCosmeticSelection.Sword1) return sword1Scale;
            if (selection == SwordCosmeticSelection.Sword2) return sword2Scale;
            return selection == SwordCosmeticSelection.Sword3 ? sword3Scale : 1f;
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
        }
    }
}
