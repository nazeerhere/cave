using Cave.Combat;
using Cave.Interactions;
using Cave.Player;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Development-scene host only. It has no spawn-table, mission, or
    /// progression references and deliberately creates the existing shell at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FalseGodPlaytestEncounter : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Vector2 playerSpawn = new Vector2(-4f, 0f);
        [SerializeField] private Vector2 bossSpawn = new Vector2(4f, 0f);
        [SerializeField] private bool startOnAwake = true;

        private GameObject playerInstance;
        private FalseGodCombatController boss;
        private FalseGodRuntimeFoundation foundation;
        private bool finalTransitionRequested;

        public FalseGodCombatController Boss => boss;

        private void Awake()
        {
            if (startOnAwake)
            {
                ResetEncounter();
            }
        }

        private void Update()
        {
            if (!Debug.isDebugBuild)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F1)) ResetEncounter();
            if (Input.GetKeyDown(KeyCode.F2)) StartProphet();
            if (Input.GetKeyDown(KeyCode.F3)) StartMonk();
            if (Input.GetKeyDown(KeyCode.F4)) RestorePlayerHealth();
            if (Input.GetKeyDown(KeyCode.F5)) ClearClaimObjects();
            if (Input.GetKeyDown(KeyCode.F6)) DestroyActiveCrystals();
        }

        public void Configure(GameObject requestedPlayerPrefab)
        {
            playerPrefab = requestedPlayerPrefab;
        }

        public void ResetEncounter()
        {
            DestroyRuntimeObject(boss != null ? boss.gameObject : null);
            DestroyRuntimeObject(playerInstance);
            finalTransitionRequested = false;

            if (playerPrefab != null)
            {
                playerInstance = Instantiate(playerPrefab, playerSpawn, Quaternion.identity);
                playerInstance.name = "Playtest Player";
            }

            boss = FalseGodDevelopmentShell.Create(bossSpawn, true);
            boss.gameObject.AddComponent<FalseGodProphetPresentation>();
            foundation = boss.GetComponent<FalseGodRuntimeFoundation>();
            boss.SetCombatTarget(playerInstance != null ? playerInstance.transform : null);
            FalseGodFormController form = boss.GetComponent<FalseGodFormController>();
            if (form != null)
            {
                form.TrueBodyDimensionTransitionRequested -= HandleFinalTransition;
                form.TrueBodyDimensionTransitionRequested += HandleFinalTransition;
            }
        }

        public void StartProphet()
        {
            ResetEncounter();
        }

        public void StartMonk()
        {
            if (boss == null)
            {
                ResetEncounter();
            }

            FalseGodFormController form = boss != null ? boss.GetComponent<FalseGodFormController>() : null;
            if (form != null && form.CurrentForm == FalseGodForm.Prophet)
            {
                form.RequestMonkAscension();
            }
        }

        public void RestorePlayerHealth()
        {
            PlayerHealth health = playerInstance != null ? playerInstance.GetComponent<PlayerHealth>() : null;
            if (health != null)
            {
                health.RestoreHealth(health.MaxHealth);
            }
        }

        public void ClearClaimObjects()
        {
            if (foundation != null)
            {
                foundation.CleanupAuthority();
            }
        }

        public void DestroyActiveCrystals()
        {
            if (foundation == null || foundation.AuthorityNetwork == null)
            {
                return;
            }

            System.Collections.Generic.List<ClaimAnchor> anchors = new System.Collections.Generic.List<ClaimAnchor>(8);
            foundation.AuthorityNetwork.CopyActiveAnchors(anchors);
            for (int index = 0; index < anchors.Count; index++)
            {
                ClaimAnchor anchor = anchors[index];
                if (anchor != null && anchor.GetComponent<ClaimCrystal>() != null)
                {
                    Destroy(anchor.gameObject);
                }
            }
        }

        private void OnGUI()
        {
            if (!Debug.isDebugBuild || boss == null)
            {
                return;
            }

            ClaimLoadSnapshot load = boss.ClaimLoad;
            GUI.Box(new Rect(12f, 12f, 430f, 162f), "False God Development Encounter");
            GUI.Label(new Rect(24f, 38f, 400f, 22f), "Form: " + boss.CurrentForm + " | Cast: " + boss.CurrentAbility);
            GUI.Label(new Rect(24f, 60f, 400f, 22f), "Claim Load: " + load.TotalRelationships + " | Anchors: " + load.ActiveAnchors + " | Territories: " + load.ActiveTerritories);
            GUI.Label(new Rect(24f, 82f, 400f, 22f), "Claimed Objects: " + load.ClaimedObjects + " | Final pending: " + finalTransitionRequested);
            GUI.Label(new Rect(24f, 112f, 400f, 22f), "F1 Reset  F2 Prophet  F3 Monk  F4 Heal  F5 Clear Claim  F6 Break Crystals");
        }

        private void HandleFinalTransition()
        {
            finalTransitionRequested = true;
        }

        private static void DestroyRuntimeObject(GameObject target)
        {
            if (target != null)
            {
                Destroy(target);
            }
        }
    }
}
