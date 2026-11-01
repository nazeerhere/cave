using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.Gambit;
using Cave.Player;
using UnityEngine;

namespace Cave.Missions
{
    /// <summary>Pure production policy shared with the Phase 3 verifier.</summary>
    public static class SovereignGambitMissionRules
    {
        public const int MaximumPhysicalDisks = 3;
        public const float MinimumSovereignDistance = 6f;
        public static float ResolveMaximumNetworkReach(float linkDistance, float influenceRadius)
        {
            return Mathf.Max(MinimumSovereignDistance,
                Mathf.Max(0f, linkDistance) * MaximumPhysicalDisks + Mathf.Max(0f, influenceRadius));
        }

        public static bool IsSovereignSocketReachable(float distance, float maximumNetworkReach)
        {
            return distance >= MinimumSovereignDistance
                && distance <= Mathf.Max(0f, maximumNetworkReach);
        }

        public static bool IsTerminalMissionState(MissionLifecycleState state)
        {
            return state == MissionLifecycleState.PrimaryObjectiveComplete
                || state == MissionLifecycleState.Extraction
                || state == MissionLifecycleState.Success
                || state == MissionLifecycleState.Failure
                || state == MissionLifecycleState.None;
        }
    }

    internal sealed class ResolvedAnchorPlacementValidator : ISovereignAnchorPlacementValidator
    {
        private readonly SovereignAnchorPlacementRejection result;
        public ResolvedAnchorPlacementValidator(SovereignAnchorPlacementRejection value) { result = value; }
        public SovereignAnchorPlacementRejection Validate(SovereignAnchorPlacementRequest request) => result;
    }

    /// <summary>
    /// Production host for one Standard Sovereign Gambit expedition. All match
    /// references are instance-scoped; no static current-match authority is
    /// introduced.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SovereignGambitMissionController : MissionControllerBase
    {
        private const float StrategicTickSeconds = .25f;
        private const float DirectorTickSeconds = .75f;
        private const float DefaultDiskInfluenceRadius = 3.6f;

        private readonly List<MissionSpawnIdentity> supportingEnemies = new List<MissionSpawnIdentity>(3);
        private readonly List<GambitStrategicEnemyAgent> strategicAgents = new List<GambitStrategicEnemyAgent>(3);
        private SovereignGambitRuntime runtime;
        private SovereignGambitAnchor anchor;
        private Damageable anchorHealth;
        private SovereignGambitSovereign sovereign;
        private Damageable sovereignHealth;
        private MissionSpawnIdentity sovereignIdentity;
        private SovereignGambitTrapMatchBridge trapBridge;
        private SovereignGambitCosmeticCompletionBridge cosmeticBridge;
        private TextMesh anchorMarker;
        private TextMesh sovereignMarker;
        private float nextStrategicTick;
        private float nextDirectorTick;
        private int lastStatusSignature;
        private Vector2 lastSovereignPosition;
        private bool hasSovereignPosition;
        private bool terminalHandled;
        private bool tearingDown;

        public SovereignGambitRuntime Runtime => runtime;
        public SovereignGambitAnchor Anchor => anchor;
        public SovereignGambitSovereign EnemySovereign => sovereign;
        public bool IsInitialized => runtime != null && runtime.Phase != GambitMatchPhase.Setup;

        public override void Initialize(MissionRunContext context)
        {
            base.Initialize(context);
            MissionSpawnDirector spawnDirector = GetComponent<MissionSpawnDirector>();
            PlayerHealth player = FindObjectOfType<PlayerHealth>();
            PlayerLandmineInventory inventory = player != null
                ? player.GetComponent<PlayerLandmineInventory>()
                : null;
            if (spawnDirector == null || !spawnDirector.IsInitialized)
            {
                FailInitialization("The mission spawn director is unavailable.");
                return;
            }
            if (player == null || inventory == null)
            {
                FailInitialization("The player Oblivion Disk authority is unavailable.");
                return;
            }

            string matchId = "standard-gambit:" + context.RunSeed + ":" + gameObject.scene.name;
            runtime = new SovereignGambitRuntime(matchId);
            runtime.PhaseChanged += HandlePhaseChanged;
            runtime.StrategicEventRaised += HandleStrategicEvent;

            if (!TryCreateAnchor(player, matchId, out string anchorFailure))
            {
                FailInitialization(anchorFailure);
                return;
            }

            float networkReach = SovereignGambitMissionRules.ResolveMaximumNetworkReach(
                inventory.FieldLinkDistance,
                DefaultDiskInfluenceRadius);
            if (!spawnDirector.BeginSovereignGambit(
                    anchor.transform.position,
                    networkReach,
                    out sovereignIdentity,
                    supportingEnemies)
                || sovereignIdentity == null)
            {
                FailInitialization("No authored reachable Sovereign and supporting-pressure setup is available.");
                return;
            }

            sovereignHealth = sovereignIdentity.GetComponent<Damageable>();
            if (sovereignHealth == null)
            {
                FailInitialization("The selected Sovereign has no Damageable authority.");
                return;
            }
            sovereign = sovereignIdentity.GetComponent<SovereignGambitSovereign>();
            if (sovereign == null) sovereign = sovereignIdentity.gameObject.AddComponent<SovereignGambitSovereign>();
            sovereign.Configure(matchId + ":enemy-sovereign", 1.1f);
            if (!sovereign.Bind(runtime))
            {
                FailInitialization("The enemy Sovereign could not register with the match.");
                return;
            }
            sovereignMarker = CreateWorldMarker(sovereign.transform, "Sovereign Strategic Status", 1.8f);

            if (!runtime.Activate())
            {
                FailInitialization("The authoritative Gambit runtime could not activate.");
                return;
            }

            trapBridge = player.GetComponent<SovereignGambitTrapMatchBridge>();
            if (trapBridge == null) trapBridge = player.gameObject.AddComponent<SovereignGambitTrapMatchBridge>();
            if (!trapBridge.Bind(runtime))
            {
                FailInitialization("The physical Oblivion Disk network could not bind to Gambit.");
                return;
            }

            cosmeticBridge = player.GetComponent<SovereignGambitCosmeticCompletionBridge>();
            if (cosmeticBridge == null) cosmeticBridge = player.gameObject.AddComponent<SovereignGambitCosmeticCompletionBridge>();
            cosmeticBridge.Bind(runtime, player.GetComponent<PlayerSkinCosmetics>());
            if (!BindStrategicCounterplay())
            {
                FailInitialization("No supporting enemy exposes the existing movement authority required for strategic counterplay.");
                return;
            }
            nextStrategicTick = Time.time;
            nextDirectorTick = Time.time;
            Publish("SOVEREIGN GAMBIT", "Establish your disk network. Put the enemy Sovereign in Check, then defeat it.");
            RefreshPresentation(true);
        }

        private void Update()
        {
            if (runtime == null || runtime.Phase != GambitMatchPhase.Active) return;
            if (Time.time >= nextStrategicTick)
            {
                nextStrategicTick = Time.time + StrategicTickSeconds;
                Vector2 position = sovereign != null ? (Vector2)sovereign.transform.position : lastSovereignPosition;
                bool moved = !hasSovereignPosition || (position - lastSovereignPosition).sqrMagnitude > .0025f;
                if (moved)
                {
                    hasSovereignPosition = true;
                    lastSovereignPosition = position;
                    sovereign?.RefreshStrategicRegion();
                    trapBridge?.RefreshStrategicInfluences();
                }
                else runtime.Evaluate(Time.time);
                RefreshPresentation(false);
            }
            if (Time.time >= nextDirectorTick)
            {
                nextDirectorTick = Time.time + DirectorTickSeconds;
                RefreshStrategicCounterplay();
            }
        }

        private bool TryCreateAnchor(PlayerHealth player, string matchId, out string failure)
        {
            failure = null;
            if (!TryResolveAnchorPosition(player.transform, out Vector2 position))
            {
                failure = "No valid grounded Sovereign Anchor position exists near the player entry.";
                return false;
            }

            GameObject host = new GameObject("Player Sovereign Anchor");
            host.transform.position = position;
            BoxCollider2D body = host.AddComponent<BoxCollider2D>();
            body.size = new Vector2(.9f, 1.1f);
            Rigidbody2D rigidbody = host.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Static;
            SpriteRenderer visual = host.AddComponent<SpriteRenderer>();
            visual.sprite = Resources.Load<Sprite>("UI/Missions/ModeIcons/MissionMode_Containment");
            visual.color = new Color(.25f, .9f, 1f, .92f);
            visual.sortingOrder = 18;
            host.transform.localScale = Vector3.one * .85f;

            anchorHealth = host.AddComponent<Damageable>();
            anchorHealth.SetRuntimeMaximumHealth(12, true);
            anchorHealth.Died += HandleAnchorDestroyed;
            anchor = host.AddComponent<SovereignGambitAnchor>();
            anchor.Configure(matchId + ":player-anchor", body.size);
            if (!anchor.TryPlace(runtime,
                    new ResolvedAnchorPlacementValidator(SovereignAnchorPlacementRejection.None)))
            {
                anchorHealth.Died -= HandleAnchorDestroyed;
                anchor.ReleaseWithoutInvalidation();
                Destroy(host);
                failure = "The Sovereign Anchor placement contract rejected the resolved position.";
                return false;
            }
            anchorMarker = CreateWorldMarker(host.transform, "Anchor Strategic Status", 1.15f);
            return true;
        }

        private static bool TryResolveAnchorPosition(Transform player, out Vector2 position)
        {
            position = player != null ? (Vector2)player.position : Vector2.zero;
            if (player == null) return false;
            float[] offsets = { -2.5f, 2.5f, -4f, 4f };
            for (int offsetIndex = 0; offsetIndex < offsets.Length; offsetIndex++)
            {
                Vector2 origin = (Vector2)player.position + new Vector2(offsets[offsetIndex], 2f);
                RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 8f);
                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    Collider2D ground = hits[hitIndex].collider;
                    if (ground == null || ground.isTrigger || ground.transform.IsChildOf(player)
                        || ground.GetComponentInParent<Damageable>() != null) continue;
                    Vector2 candidate = hits[hitIndex].point + Vector2.up * .57f;
                    Collider2D[] blocked = Physics2D.OverlapBoxAll(candidate, new Vector2(.82f, .96f), 0f);
                    bool obstruction = false;
                    for (int overlapIndex = 0; overlapIndex < blocked.Length; overlapIndex++)
                    {
                        Collider2D overlap = blocked[overlapIndex];
                        if (overlap == null || overlap == ground || overlap.isTrigger
                            || overlap.transform.IsChildOf(player)) continue;
                        obstruction = true;
                        break;
                    }
                    if (!obstruction) { position = candidate; return true; }
                }
            }
            return false;
        }

        private bool BindStrategicCounterplay()
        {
            for (int index = 0; index < supportingEnemies.Count; index++)
            {
                MissionSpawnIdentity identity = supportingEnemies[index];
                if (identity == null || identity.GetComponent<EnemyController>() == null) continue;
                GambitStrategicEnemyAgent agent = identity.GetComponent<GambitStrategicEnemyAgent>();
                if (agent == null) agent = identity.gameObject.AddComponent<GambitStrategicEnemyAgent>();
                agent.Bind(runtime);
                strategicAgents.Add(agent);
                // One real actor is sufficient for observable counterplay; the
                // remaining support population preserves its normal AI.
                return true;
            }
            return false;
        }

        private void RefreshStrategicCounterplay()
        {
            if (strategicAgents.Count == 0) return;
            SovereignGambitStrategicObjective objective = SovereignGambitStrategicDirector.Resolve(
                SovereignGambitTacticalContextFactory.Create(runtime));
            Transform target = null;
            Damageable targetHealth = null;
            bool anchorTarget = false;
            string label = objective.Kind.ToString();

            if (objective.Kind == SovereignGambitObjectiveKind.DisruptCheckContributor)
            {
                SovereignGambitTrapPiece piece = FindPieceByInfluence(objective.TargetId);
                ResolvePieceTarget(piece, out target, out targetHealth);
            }
            else if (objective.Kind == SovereignGambitObjectiveKind.PressureCriticalConnector)
            {
                SovereignGambitTrapPiece piece = FindPieceByNode(objective.TargetId);
                ResolvePieceTarget(piece, out target, out targetHealth);
            }
            else if (objective.Kind == SovereignGambitObjectiveKind.PressureAnchor)
            {
                target = anchor != null ? anchor.transform : null;
                targetHealth = anchorHealth;
                anchorTarget = true;
            }

            if (target == null)
            {
                target = anchor != null ? anchor.transform : null;
                targetHealth = anchorHealth;
                anchorTarget = true;
                label = "PressureAnchor";
            }
            strategicAgents[0].Assign(target, targetHealth, anchorTarget, label);
        }

        private SovereignGambitTrapPiece FindPieceByInfluence(string sourceId)
        {
            if (trapBridge == null || string.IsNullOrEmpty(sourceId)) return null;
            IReadOnlyList<SovereignGambitTrapPiece> pieces = trapBridge.SnapshotPieces();
            for (int index = 0; index < pieces.Count; index++)
                if (pieces[index] != null && string.Equals(pieces[index].InfluenceProviderId, sourceId, StringComparison.Ordinal)) return pieces[index];
            return null;
        }

        private SovereignGambitTrapPiece FindPieceByNode(string nodeId)
        {
            if (trapBridge == null || string.IsNullOrEmpty(nodeId)) return null;
            IReadOnlyList<SovereignGambitTrapPiece> pieces = trapBridge.SnapshotPieces();
            for (int index = 0; index < pieces.Count; index++)
                if (pieces[index] != null && string.Equals(pieces[index].NodeId.Value, nodeId, StringComparison.Ordinal)) return pieces[index];
            return null;
        }

        private static void ResolvePieceTarget(SovereignGambitTrapPiece piece, out Transform target, out Damageable health)
        {
            target = piece != null ? piece.transform : null;
            health = piece != null && piece.PhysicalTrap != null && piece.PhysicalTrap.FieldNode != null
                ? piece.PhysicalTrap.FieldNode.GetComponent<Damageable>()
                : null;
        }

        private void HandleAnchorDestroyed()
        {
            if (tearingDown || terminalHandled || runtime == null || runtime.Phase != GambitMatchPhase.Active) return;
            runtime.InvalidatePlayerAnchor();
        }

        private void HandleStrategicEvent(GambitStrategicEvent strategicEvent)
        {
            switch (strategicEvent.Kind)
            {
                case GambitStrategicEventKind.SovereignCheckStarted:
                    Publish("CHECK", "Enemy Sovereign protection disabled. Defeat it now.");
                    break;
                case GambitStrategicEventKind.SovereignCheckBroken:
                    Publish("CHECK BROKEN", "The enemy Sovereign is protected again. Restore an Anchor-connected influence.");
                    break;
                case GambitStrategicEventKind.PlayerAnchorCheckStarted:
                    Publish("ANCHOR IN CHECK", "A hostile strategic actor is threatening your Sovereign Anchor.");
                    break;
                case GambitStrategicEventKind.PlayerAnchorCheckBroken:
                    Publish("ANCHOR SECURE", "The hostile pressure on your Anchor has broken.");
                    break;
            }
            RefreshPresentation(true);
        }

        private void HandlePhaseChanged(GambitMatchPhase phase)
        {
            if (terminalHandled) return;
            if (phase == GambitMatchPhase.Victory)
            {
                terminalHandled = true;
                DisableCounterplay();
                Publish("CHECKMATE", "Enemy Sovereign defeated. Standard Gambit complete.");
                PrimaryComplete(false);
            }
            else if (phase == GambitMatchPhase.Defeat)
            {
                terminalHandled = true;
                DisableCounterplay();
                Fail("Your Sovereign Anchor was destroyed.");
            }
        }

        private void RefreshPresentation(bool force)
        {
            if (runtime == null) return;
            int anchorValue = anchorHealth != null ? anchorHealth.CurrentHealth : 0;
            int sovereignValue = sovereignHealth != null ? sovereignHealth.CurrentHealth : 0;
            int signature = (runtime.EnemySovereignInCheck ? 1 : 0)
                | (runtime.PlayerAnchorInCheck ? 2 : 0)
                | (runtime.EnemySovereignProtectionActive ? 4 : 0)
                | (anchorValue << 4)
                | (sovereignValue << 12)
                | ((runtime.TerritorySnapshot.Version & 255) << 20);
            if (!force && signature == lastStatusSignature) return;
            lastStatusSignature = signature;
            if (anchorMarker != null)
            {
                anchorMarker.text = runtime.PlayerAnchorInCheck
                    ? "ANCHOR IN CHECK  " + anchorValue + " / " + anchorHealth.MaximumHealth
                    : "SOVEREIGN ANCHOR  " + anchorValue + " / " + anchorHealth.MaximumHealth;
                anchorMarker.color = runtime.PlayerAnchorInCheck ? new Color(1f, .25f, .2f) : new Color(.3f, .9f, 1f);
            }
            if (sovereignMarker != null)
            {
                sovereignMarker.text = runtime.EnemySovereignInCheck ? "CHECK — VULNERABLE" : "STRATEGICALLY PROTECTED";
                sovereignMarker.color = runtime.EnemySovereignInCheck ? new Color(1f, .35f, .25f) : new Color(.75f, .4f, 1f);
            }
        }

        private static TextMesh CreateWorldMarker(Transform parent, string name, float localY)
        {
            GameObject markerObject = new GameObject(name);
            markerObject.transform.SetParent(parent, false);
            markerObject.transform.localPosition = new Vector3(0f, localY, 0f);
            TextMesh marker = markerObject.AddComponent<TextMesh>();
            marker.anchor = TextAnchor.MiddleCenter;
            marker.alignment = TextAlignment.Center;
            marker.fontSize = 40;
            marker.characterSize = .025f;
            marker.GetComponent<MeshRenderer>().sortingOrder = 40;
            return marker;
        }

        private void FailInitialization(string reason)
        {
            terminalHandled = true;
            CleanupRuntime();
            Fail("Sovereign Gambit could not initialize: " + reason);
        }

        private void DisableCounterplay()
        {
            for (int index = 0; index < strategicAgents.Count; index++) strategicAgents[index]?.Unbind();
        }

        private void CleanupRuntime()
        {
            if (tearingDown) return;
            tearingDown = true;
            DisableCounterplay();
            strategicAgents.Clear();
            trapBridge?.Unbind();
            cosmeticBridge?.Bind(null, null);
            sovereign?.Unbind();
            if (anchorHealth != null) anchorHealth.Died -= HandleAnchorDestroyed;
            anchor?.ReleaseWithoutInvalidation();
            if (runtime != null)
            {
                runtime.PhaseChanged -= HandlePhaseChanged;
                runtime.StrategicEventRaised -= HandleStrategicEvent;
                runtime.End();
            }
            runtime = null;
        }

        private void OnDestroy()
        {
            CleanupRuntime();
        }
    }

    /// <summary>
    /// One opt-in enemy response for Standard Gambit. It requests ordinary
    /// horizontal movement from EnemyController, respects a bounded attack
    /// cadence, and damages the real Anchor/FieldNode authority at contact.
    /// Outside a bound match the component is inert.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class GambitStrategicEnemyAgent : MonoBehaviour, IMobStrategicOverride
    {
        private const float DecisionInterval = .25f;
        private const float AttackInterval = 1.25f;
        private const float AttackRange = 1.15f;
        private EnemyController movement;
        private MobBrainBase brain;
        private EnemyStagger stagger;
        private SovereignGambitRuntime runtime;
        private Transform target;
        private Damageable targetHealth;
        private bool targetIsAnchor;
        private string influenceId;
        private float nextDecision;
        private float nextAttack;
        private bool influenceRegistered;
        [SerializeField] private string currentStrategicObjective;

        public bool IsBound => runtime != null;
        public bool IsDrivingStrategicTarget => runtime != null
            && runtime.Phase == GambitMatchPhase.Active
            && target != null;
        public Transform CurrentTarget => target;
        public string CurrentStrategicObjective => currentStrategicObjective;

        public void Bind(SovereignGambitRuntime match)
        {
            Unbind();
            runtime = match;
            movement = GetComponent<EnemyController>();
            brain = GetComponent<MobBrainBase>();
            stagger = GetComponent<EnemyStagger>();
            brain?.SetModeScopedStrategicOverride(this);
            influenceId = match != null ? match.MatchId + ":enemy-agent:" + GetInstanceID() : null;
        }

        public void Assign(Transform strategicTarget, Damageable healthAuthority, bool anchorTarget, string objective)
        {
            if (runtime == null || runtime.Phase != GambitMatchPhase.Active) return;
            if (target != strategicTarget) RemoveAnchorInfluence();
            target = strategicTarget;
            targetHealth = healthAuthority;
            targetIsAnchor = anchorTarget;
            currentStrategicObjective = objective ?? string.Empty;
        }

        private void Update()
        {
            if (runtime == null || runtime.Phase != GambitMatchPhase.Active || Time.time < nextDecision) return;
            nextDecision = Time.time + DecisionInterval;
            if (stagger != null && !stagger.CanAct)
            {
                movement?.SetCombatMovementIntent(0f, DecisionInterval + .1f);
                RemoveAnchorInfluence();
                return;
            }
            if (target == null || !target.gameObject.activeInHierarchy || targetHealth == null || targetHealth.CurrentHealth <= 0)
            {
                ClearTarget();
                return;
            }

            Vector2 delta = target.position - transform.position;
            float horizontal = Mathf.Abs(delta.x);
            bool inRange = horizontal <= AttackRange && Mathf.Abs(delta.y) <= 1.5f;
            if (!inRange && Mathf.Abs(delta.x) > .05f)
                movement?.SetCombatMovementIntent(Mathf.Sign(delta.x), DecisionInterval + .1f);

            if (targetIsAnchor)
            {
                if (inRange && !influenceRegistered)
                {
                    influenceRegistered = runtime.RegisterInfluence(new StrategicInfluence(
                        influenceId, default, GambitSide.Enemy, runtime.PlayerAnchorTarget,
                        true, true, "enemy:physical-anchor-pressure"));
                    runtime.Evaluate(Time.time);
                }
                else if (!inRange) RemoveAnchorInfluence();
            }

            if (!inRange || Time.time < nextAttack) return;
            nextAttack = Time.time + AttackInterval;
            targetHealth.TakeDamage(1, new DamageContext(gameObject, DamageTrait.Melee | DamageTrait.Direct));
        }

        public void Unbind()
        {
            RemoveAnchorInfluence();
            brain?.SetModeScopedStrategicOverride(null);
            runtime = null;
            movement?.ClearBrainMovement();
            movement = null;
            brain = null;
            stagger = null;
            target = null;
            targetHealth = null;
            currentStrategicObjective = string.Empty;
        }

        private void ClearTarget()
        {
            RemoveAnchorInfluence();
            target = null;
            targetHealth = null;
            targetIsAnchor = false;
            currentStrategicObjective = string.Empty;
        }

        private void RemoveAnchorInfluence()
        {
            if (influenceRegistered && runtime != null && !string.IsNullOrEmpty(influenceId))
            {
                runtime.RemoveInfluence(influenceId);
                if (runtime.Phase == GambitMatchPhase.Active) runtime.Evaluate(Time.time);
            }
            influenceRegistered = false;
        }

        private void OnDisable() { Unbind(); }
    }
}
