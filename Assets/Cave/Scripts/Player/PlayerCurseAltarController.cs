using System;
using Cave.Combat;
using Cave.Enemies;
using Cave.InputSystem;
using Cave.UI;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCurseController), typeof(PlayerHealth))]
    public sealed class PlayerCurseAltarController : MonoBehaviour
    {
        [Header("Permanent Spawn Altar")]
        [SerializeField] private bool createPermanentSpawnAltar = true;
        [SerializeField] private Vector2 permanentAltarOffset = new Vector2(2.5f, 0f);

        [Header("Summoned Altar")]
        [SerializeField, Min(1f)] private float summonedAltarLifetime = 25f;
        [SerializeField, Min(0f)] private float summonCooldown = 20f;
        [SerializeField] private Vector2 summonedAltarOffset = new Vector2(2f, 0f);
        [SerializeField, Min(1f)] private float combatPressureRadius = 7f;

        [Header("Interaction")]
        [SerializeField, Min(0.5f)] private float interactionRange = 2.4f;

        private PlayerCurseController curses;
        private PlayerHealth health;
        private Collider2D playerCollider;
        private CurseAltar permanentAltar;
        private CurseAltar summonedAltar;
        private CurseAltar selectedAltar;
        private float nextSummonTime;
        private PlayerCombatFlow combatFlow;

        public event Action SelectionChanged;

        public static bool AnySelectionOpen { get; private set; }
        public float InteractionRange => interactionRange;
        public bool IsSelectionOpen => selectedAltar != null;
        public PlayerCurseController Curses => curses;

        private void Awake()
        {
            curses = GetComponent<PlayerCurseController>();
            health = GetComponent<PlayerHealth>();
            playerCollider = GetComponent<Collider2D>();
            combatFlow = GetComponent<PlayerCombatFlow>();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += HandlePlayerDied;
            }
        }

        private void Start()
        {
            if (createPermanentSpawnAltar)
            {
                Vector2 position = ResolveGroundedPosition(permanentAltarOffset);
                permanentAltar = CurseAltar.Create(this, position, true, 0f);
            }
        }

        private void Update()
        {
            if (selectedAltar != null)
            {
                return;
            }

            if (GameInput.InteractPressed)
            {
                CurseAltar altar = CurseAltar.FindNearest(transform.position, interactionRange);
                if (altar != null
                    && (combatFlow == null || !combatFlow.ShouldPrioritizeFrenzyBreak(true)))
                {
                    OpenSelection(altar);
                    return;
                }
            }

            if (GameInput.SummonCurseAltarPressed)
            {
                TrySummonAltar();
            }
        }

        public bool HasAvailableAltar()
        {
            return selectedAltar == null
                && CurseAltar.FindNearest(transform.position, interactionRange) != null;
        }

        public string GetContextualInteractLabel()
        {
            if (combatFlow == null)
            {
                combatFlow = GetComponent<PlayerCombatFlow>();
            }

            return combatFlow != null && combatFlow.ShouldPrioritizeFrenzyBreak(true)
                ? "FRENZY BREAK"
                : "BARGAIN";
        }

        public bool TrySummonAltar()
        {
            if (summonedAltar != null
                || Time.time < nextSummonTime
                || HostileMobQuery.CountRealHostiles(transform.position, combatPressureRadius) > 0)
            {
                return false;
            }

            summonedAltar = CurseAltar.Create(
                this,
                ResolveGroundedPosition(summonedAltarOffset),
                false,
                summonedAltarLifetime);
            nextSummonTime = Time.time + summonCooldown;
            return true;
        }

        public void ToggleCurse(PlayerCurseType type)
        {
            if (selectedAltar == null || curses == null)
            {
                return;
            }

            curses.SetCurseActive(type, !curses.IsActive(type));
            CurseAltar usedAltar = selectedAltar;
            if (!usedAltar.IsPermanent)
            {
                CloseSelection();
                usedAltar.ConsumeAfterBargain();
            }
            else
            {
                SelectionChanged?.Invoke();
            }
        }

        public void CloseSelection()
        {
            selectedAltar = null;
            AnySelectionOpen = false;
            FindObjectOfType<CurseAltarHud>(true)?.Close();
            GameInput.EnableGameplayAfterInputRelease();
            SelectionChanged?.Invoke();
        }

        internal void NotifyAltarRemoved(CurseAltar altar)
        {
            if (altar == summonedAltar)
            {
                summonedAltar = null;
            }

            if (altar == permanentAltar)
            {
                permanentAltar = null;
            }

            if (altar == selectedAltar)
            {
                selectedAltar = null;
                AnySelectionOpen = false;
                GameInput.EnableGameplayAfterInputRelease();
                SelectionChanged?.Invoke();
            }
        }

        private void OpenSelection(CurseAltar altar)
        {
            if (altar == null || selectedAltar != null)
            {
                return;
            }

            selectedAltar = altar;
            AnySelectionOpen = true;
            CurseAltarHud altarHud = FindObjectOfType<CurseAltarHud>(true);
            if (altarHud == null || !altarHud.TryOpen(this))
            {
                // The visual menu is the authority for whether interaction can
                // lock movement. A missing/failed HUD must remain a no-op.
                selectedAltar = null;
                AnySelectionOpen = false;
                GameInput.EnableGameplayAfterInputRelease();
                return;
            }

            GameInput.SetGameplayInputEnabled(false);
            SelectionChanged?.Invoke();
        }

        private Vector2 ResolveGroundedPosition(Vector2 offset)
        {
            Vector2 position = (Vector2)transform.position + offset;
            if (playerCollider != null)
            {
                position.y = playerCollider.bounds.min.y;
            }

            return position;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandlePlayerDied;
            }

            if (selectedAltar != null)
            {
                CloseSelection();
            }
            else
            {
                AnySelectionOpen = false;
            }
        }

        private void HandlePlayerDied()
        {
            CloseSelection();
            if (summonedAltar != null)
            {
                Destroy(summonedAltar.gameObject);
                summonedAltar = null;
            }
        }
    }
}
