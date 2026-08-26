using System;
using System.Collections.Generic;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum DetectiveResearchPhase
    {
        Idle,
        Collecting,
        Predicting,
        Outcome,
        Cooldown
    }

    public enum DetectiveResearchOutcome
    {
        None,
        Success,
        Corrupted,
        Expired
    }

    [DisallowMultipleComponent]
    public sealed class DetectiveEncounterCoordinator : MonoBehaviour
    {
        [Header("Tower Construction")]
        [SerializeField] private DetectiveTower towerPrefab;
        [SerializeField] private LayerMask groundLayers;
        [SerializeField, Min(0.1f)] private float fullConstructionDuration = 8f;
        [SerializeField, Range(0.05f, 1f)] private float soloConstructionRate = 0.33f;
        [SerializeField, Min(0.1f)] private float builderPositionTolerance = 1.15f;
        [SerializeField, Min(0f)] private float constructionFailureResetDelay = 3f;
        [SerializeField, Min(0f)] private float failedBuilderRecovery = 1.1f;
        [SerializeField, Min(0f)] private float towerRebuildDelay = 8f;

        [Header("Tower Influence")]
        [SerializeField, Min(1)] private int towerMaximumHealth = 8;
        [SerializeField, Min(0.5f)] private float towerInitialRadius = 4.5f;
        [SerializeField, Min(0.5f)] private float towerMaximumRadius = 7.5f;
        [SerializeField, Min(0f)] private float towerRadiusGrowthPerSecond = 0.08f;
        [SerializeField] private DetectiveTowerDebuffStage[] recoveryMultipliersByResearchStage =
        {
            new DetectiveTowerDebuffStage(0.65f, 0.7f),
            new DetectiveTowerDebuffStage(0.35f, 0.7f),
            new DetectiveTowerDebuffStage(0.35f, 0.45f),
            new DetectiveTowerDebuffStage(0.2f, 0.3f)
        };
        [SerializeField] private float[] manaCostMultipliersByResearchStage =
            { 1.10f, 1.20f, 1.35f, 1.50f };
        [SerializeField] private float[] dropRateMultipliersByResearchStage =
            { 0.90f, 0.75f, 0.55f, 0.35f };

        [Header("Player Research")]
        [SerializeField, Min(0.1f)] private float minimumObservationRange = 3.5f;
        [SerializeField, Min(0.1f)] private float maximumObservationRange = 5.5f;
        [SerializeField, Min(0f)] private float minimumObservationRangeBonus = 0.5f;
        [SerializeField, Min(0f)] private float maximumObservationRangeBonus = 1.5f;
        [SerializeField, Min(0.1f)] private float minimumObservationDuration = 6f;
        [SerializeField, Min(0.1f)] private float maximumObservationDuration = 10f;
        [SerializeField, Min(0f)] private float researchCycleCooldown = 2f;
        [SerializeField, Range(2, 5)] private int normalPatternLength = 3;
        [SerializeField, Range(3, 5)] private int maximumPatternLength = 5;
        [SerializeField, Min(1)] private int repeatedMoveEventsPerSample = 3;
        [SerializeField, Min(0f)] private float successfulResearchReward = 8f;
        [SerializeField] private float[] researchStageThresholds = { 8f, 18f, 32f };
        [SerializeField, Min(0f)] private float researchOutcomeDisplayDuration = 1.2f;
        [SerializeField, Min(0f)] private float tacticalBiasDuration = 20f;
        [SerializeField, Range(0f, 1f)] private float sacrificialIntentChance = 0.2f;
        [SerializeField, Min(0f)] private float minimumRoleCommitment = 5f;

        [Header("Clone Authority")]
        [SerializeField, Min(0)] private int maximumActiveClones = 3;
        [SerializeField, Min(0f)] private float globalCloneCooldown = 25f;

        [Header("Current Encounter State (Read Only)")]
        [SerializeField, Min(0)] private int realDetectiveCount;
        [SerializeField, Min(0)] private int activeCloneCount;
        [SerializeField] private DetectiveTower activeTower;
        [SerializeField, Range(0f, 1f)] private float constructionProgress;
        [SerializeField, Min(0f)] private float accumulatedResearch;
        [SerializeField, Min(0)] private int currentResearchStage;
        [SerializeField] private Vector2 constructionPosition;
        [SerializeField] private DetectiveResearchPhase researchPhase;
        [SerializeField] private DetectiveResearchOutcome lastResearchOutcome;
        [SerializeField] private List<PlayerActionCategory> currentObservedPattern =
            new List<PlayerActionCategory>();
        [SerializeField] private PlayerActionCategory predictedNextAction;
        [SerializeField, Range(-1f, 1f)] private float tacticalHorizontalBias;
        [SerializeField, Min(0f)] private float researchCycleObservationTime;
        [SerializeField, Min(0)] private int consecutiveMoveRepetitions;
        [SerializeField] private bool researchHudVisible;
        [SerializeField, Min(0f)] private float globalCloneCooldownRemaining;

        private readonly HashSet<DetectiveIdentity> realDetectives =
            new HashSet<DetectiveIdentity>();
        private readonly HashSet<DetectiveIdentity> clones =
            new HashSet<DetectiveIdentity>();
        private readonly List<DetectiveIdentity> orderedRealDetectives =
            new List<DetectiveIdentity>();
        private readonly Dictionary<DetectiveIdentity, DetectiveIntent> intentions =
            new Dictionary<DetectiveIdentity, DetectiveIntent>();
        private readonly Dictionary<DetectiveIdentity, float> roleLockedUntil =
            new Dictionary<DetectiveIdentity, float>();
        private readonly Dictionary<DetectiveIdentity, float> unavailableUntil =
            new Dictionary<DetectiveIdentity, float>();
        private readonly Dictionary<DetectiveIdentity, float> builderReadyUntil =
            new Dictionary<DetectiveIdentity, float>();
        private readonly Dictionary<DetectiveIdentity, float> researcherObservingUntil =
            new Dictionary<DetectiveIdentity, float>();

        private float nextRoleEvaluationTime;
        private float constructionPausedSince = -1f;
        private float nextTowerBuildTime;
        private float nextSacrificialChoiceTime;
        private bool rolesDirty = true;
        private int reservedCloneSlots;
        private PlayerActionObserver playerActionObserver;
        private PlayerHealth observedPlayer;
        private float nextResearchCycleTime;
        private float outcomeHidesAt;
        private float tacticalBiasExpiresAt;
        private float lastObservedHorizontalDirection;
        private float nextGlobalCloneTime;

        public event Action ResearchDisplayChanged;
        public event Action TowerStatusChanged;

        public int RealDetectiveCount => realDetectiveCount;
        public int ActiveCloneCount => activeCloneCount;
        public int MaximumActiveClones => maximumActiveClones;
        public float GlobalCloneCooldown => globalCloneCooldown;
        public DetectiveTower ActiveTower => activeTower;
        public float ConstructionProgress => constructionProgress;
        public float AccumulatedResearch => accumulatedResearch;
        public int CurrentResearchStage => currentResearchStage;
        public float MinimumObservationRange => minimumObservationRange
            + minimumObservationRangeBonus;
        public float MaximumObservationRange => maximumObservationRange
            + maximumObservationRangeBonus;
        public float FailedBuilderRecovery => failedBuilderRecovery;
        public DetectiveResearchPhase ResearchPhase => researchPhase;
        public DetectiveResearchOutcome LastResearchOutcome => lastResearchOutcome;
        public IReadOnlyList<PlayerActionCategory> CurrentObservedPattern =>
            currentObservedPattern;
        public PlayerActionCategory PredictedNextAction => predictedNextAction;
        public bool ResearchHudVisible => researchHudVisible;
        public bool IsActivelyBeingStudied => HasActiveResearchObservation()
            && (researchPhase == DetectiveResearchPhase.Collecting
                || researchPhase == DetectiveResearchPhase.Predicting);
        public int RequiredPatternLength => ResolveRequiredPatternLength();
        public float TacticalHorizontalBias => Time.time < tacticalBiasExpiresAt
            ? tacticalHorizontalBias
            : 0f;

        public static DetectiveEncounterCoordinator GetOrCreate()
        {
            DetectiveEncounterCoordinator existing =
                FindObjectOfType<DetectiveEncounterCoordinator>();
            if (existing != null)
            {
                return existing;
            }

            GameObject coordinatorObject = new GameObject("Detective Encounter Coordinator");
            return coordinatorObject.AddComponent<DetectiveEncounterCoordinator>();
        }

        internal void Register(DetectiveIdentity identity)
        {
            if (identity == null)
            {
                return;
            }

            if (identity.IsClone)
            {
                clones.Add(identity);
                realDetectives.Remove(identity);
                reservedCloneSlots = Mathf.Max(0, reservedCloneSlots - 1);
            }
            else
            {
                realDetectives.Add(identity);
                clones.Remove(identity);
            }

            rolesDirty = true;
            RefreshCounts();
        }

        internal void Unregister(DetectiveIdentity identity)
        {
            if (identity == null)
            {
                return;
            }

            realDetectives.Remove(identity);
            clones.Remove(identity);
            intentions.Remove(identity);
            roleLockedUntil.Remove(identity);
            unavailableUntil.Remove(identity);
            builderReadyUntil.Remove(identity);
            researcherObservingUntil.Remove(identity);
            rolesDirty = true;
            RefreshCounts();
        }

        public DetectiveIntent GetIntent(DetectiveIdentity identity)
        {
            if (identity == null || identity.IsClone)
            {
                return DetectiveIntent.Bait;
            }

            EvaluateRolesIfNeeded();
            return intentions.TryGetValue(identity, out DetectiveIntent intent)
                ? intent
                : DetectiveIntent.Fighter;
        }

        public bool TryGetConstructionPosition(
            DetectiveIdentity identity,
            out Vector2 position)
        {
            position = constructionPosition;
            return identity != null
                && GetIntent(identity) == DetectiveIntent.Builder
                && activeTower == null
                && realDetectiveCount >= 2;
        }

        public void ReportBuilderReady(DetectiveIdentity identity)
        {
            if (identity != null && GetIntent(identity) == DetectiveIntent.Builder)
            {
                builderReadyUntil[identity] = Time.time + 0.35f;
            }
        }

        public void ReportResearchObservation(DetectiveIdentity identity, bool observing)
        {
            if (identity == null || GetIntent(identity) != DetectiveIntent.Researcher)
            {
                return;
            }

            if (observing)
            {
                researcherObservingUntil[identity] = Time.time + 0.35f;
            }
            else
            {
                researcherObservingUntil.Remove(identity);
            }
        }

        public void AbandonCurrentRole(DetectiveIdentity identity, float recovery)
        {
            if (identity == null)
            {
                return;
            }

            intentions.Remove(identity);
            builderReadyUntil.Remove(identity);
            researcherObservingUntil.Remove(identity);
            unavailableUntil[identity] = Time.time + Mathf.Max(0f, recovery);
            rolesDirty = true;
        }

        public void RequestTemporaryIntent(
            DetectiveIdentity identity,
            DetectiveIntent intent,
            float duration)
        {
            if (identity == null || !IsRoleAvailable(identity))
            {
                return;
            }

            DetectiveIntent current = GetIntent(identity);
            if (current == DetectiveIntent.Builder
                || current == DetectiveIntent.Researcher
                || current == DetectiveIntent.Sacrificial)
            {
                return;
            }

            intentions[identity] = intent;
            roleLockedUntil[identity] = Time.time + Mathf.Max(0f, duration);
        }

        public bool TryReserveCloneSlot()
        {
            RemoveMissingMembers();
            if (Time.time < nextGlobalCloneTime
                || clones.Count + reservedCloneSlots >= maximumActiveClones)
            {
                return false;
            }

            reservedCloneSlots++;
            nextGlobalCloneTime = Time.time + globalCloneCooldown;
            activeCloneCount = clones.Count + reservedCloneSlots;
            return true;
        }

        public void ReleaseReservedCloneSlot()
        {
            reservedCloneSlots = Mathf.Max(0, reservedCloneSlots - 1);
            RefreshCounts();
        }

        public void NotifyTowerDestroyed(DetectiveTower tower)
        {
            if (tower == null || activeTower != tower)
            {
                return;
            }

            activeTower = null;
            accumulatedResearch = 0f;
            currentResearchStage = 0;
            tacticalHorizontalBias = 0f;
            tacticalBiasExpiresAt = 0f;
            ResetResearchCycle(false);
            constructionProgress = 0f;
            constructionPausedSince = -1f;
            nextTowerBuildTime = Time.time + towerRebuildDelay;
            rolesDirty = true;
            TowerStatusChanged?.Invoke();
        }

        private void Update()
        {
            globalCloneCooldownRemaining = Mathf.Max(0f, nextGlobalCloneTime - Time.time);
            RemoveMissingMembers();
            EnsurePlayerActionObserver();
            EvaluateRolesIfNeeded();
            UpdateConstruction();
            UpdateResearch();
            RefreshCounts();
        }

        private void EvaluateRolesIfNeeded()
        {
            if (!rolesDirty && Time.time < nextRoleEvaluationTime)
            {
                return;
            }

            nextRoleEvaluationTime = Time.time + 0.5f;
            rolesDirty = false;
            BuildOrderedRealList();
            if (orderedRealDetectives.Count == 0)
            {
                intentions.Clear();
                return;
            }

            RemoveInvalidIntentions();
            if (activeTower == null)
            {
                AssignConstructionRoles();
            }
            else
            {
                AssignPostConstructionRoles();
            }
        }

        private void AssignConstructionRoles()
        {
            int count = orderedRealDetectives.Count;
            if (count < 2 || Time.time < nextTowerBuildTime)
            {
                foreach (DetectiveIdentity detective in orderedRealDetectives)
                {
                    AssignIntent(detective, DetectiveIntent.Fighter, false);
                }

                return;
            }

            if (constructionProgress <= 0f)
            {
                constructionPosition = ResolveConstructionPosition();
            }

            int requiredBuilders = count >= 3 ? 2 : 1;
            int assignedBuilders = 0;
            foreach (DetectiveIdentity detective in orderedRealDetectives)
            {
                if (assignedBuilders < requiredBuilders && IsRoleAvailable(detective))
                {
                    AssignIntent(detective, DetectiveIntent.Builder, true);
                    assignedBuilders++;
                }
                else if (assignedBuilders >= requiredBuilders)
                {
                    AssignIntent(
                        detective,
                        assignedBuilders == requiredBuilders
                            ? DetectiveIntent.Cover
                            : DetectiveIntent.Interceptor,
                        false);
                    assignedBuilders++;
                }
                else
                {
                    AssignIntent(detective, DetectiveIntent.Fighter, false);
                }
            }
        }

        private void AssignPostConstructionRoles()
        {
            DetectiveIdentity researcher = FindExistingIntent(DetectiveIntent.Researcher);
            if (researcher == null && currentResearchStage < recoveryMultipliersByResearchStage.Length - 1)
            {
                DetectiveIdentity candidate = FindFirstAvailable();
                if (candidate != null)
                {
                    bool chooseSacrifice = orderedRealDetectives.Count >= 2
                        && Time.time >= nextSacrificialChoiceTime
                        && UnityEngine.Random.value < sacrificialIntentChance;
                    AssignIntent(
                        candidate,
                        chooseSacrifice
                            ? DetectiveIntent.Sacrificial
                            : DetectiveIntent.Researcher,
                        true);
                    nextSacrificialChoiceTime = Time.time + minimumRoleCommitment;
                    researcher = chooseSacrifice ? null : candidate;
                }
            }

            int freeIndex = 0;
            foreach (DetectiveIdentity detective in orderedRealDetectives)
            {
                if (intentions.TryGetValue(detective, out DetectiveIntent existing)
                    && (existing == DetectiveIntent.Researcher
                        || existing == DetectiveIntent.Sacrificial))
                {
                    continue;
                }

                DetectiveIntent freeIntent = freeIndex == 0
                    ? DetectiveIntent.Interceptor
                    : freeIndex == 1
                        ? DetectiveIntent.Cover
                        : DetectiveIntent.Fighter;
                AssignIntent(detective, freeIntent, false);
                freeIndex++;
            }
        }

        private void UpdateConstruction()
        {
            if (activeTower != null || realDetectiveCount < 2 || Time.time < nextTowerBuildTime)
            {
                return;
            }

            int requiredBuilders = realDetectiveCount >= 3 ? 2 : 1;
            int readyBuilders = 0;
            foreach (KeyValuePair<DetectiveIdentity, DetectiveIntent> pair in intentions)
            {
                if (pair.Value == DetectiveIntent.Builder
                    && pair.Key != null
                    && pair.Key.CanAct
                    && builderReadyUntil.TryGetValue(pair.Key, out float readyUntil)
                    && Time.time <= readyUntil
                    && Vector2.Distance(pair.Key.transform.position, constructionPosition)
                        <= builderPositionTolerance)
                {
                    readyBuilders++;
                }
            }

            if (readyBuilders < requiredBuilders)
            {
                if (constructionProgress > 0f && constructionPausedSince < 0f)
                {
                    constructionPausedSince = Time.time;
                }

                if (constructionPausedSince >= 0f
                    && Time.time - constructionPausedSince >= constructionFailureResetDelay)
                {
                    FailConstruction();
                }

                return;
            }

            constructionPausedSince = -1f;
            float rate = requiredBuilders == 1 ? soloConstructionRate : 1f;
            constructionProgress = Mathf.Clamp01(
                constructionProgress + Time.deltaTime * rate / fullConstructionDuration);
            if (constructionProgress >= 1f)
            {
                CompleteTower();
            }
        }

        private void UpdateResearch()
        {
            if (Time.time >= tacticalBiasExpiresAt)
            {
                tacticalHorizontalBias = 0f;
            }

            if (researchPhase == DetectiveResearchPhase.Outcome
                && Time.time >= outcomeHidesAt)
            {
                ResetResearchCycle(true);
            }

            if (activeTower == null)
            {
                ResetResearchCycle(false);
                return;
            }

            DetectiveIdentity researcher = FindExistingIntent(DetectiveIntent.Researcher);
            bool observing = researcher != null
                && researcher.CanAct
                && researcherObservingUntil.TryGetValue(researcher, out float observingUntil)
                && Time.time <= observingUntil;
            if (!observing)
            {
                if (researchPhase != DetectiveResearchPhase.Outcome)
                {
                    SetResearchHudVisible(false);
                }

                return;
            }

            if (researchPhase == DetectiveResearchPhase.Outcome)
            {
                SetResearchHudVisible(true);
                return;
            }

            if (researchPhase == DetectiveResearchPhase.Cooldown)
            {
                if (Time.time < nextResearchCycleTime)
                {
                    SetResearchHudVisible(false);
                    return;
                }

                BeginResearchCycle();
            }

            if (researchPhase == DetectiveResearchPhase.Idle)
            {
                BeginResearchCycle();
            }

            SetResearchHudVisible(true);
            if (researchPhase != DetectiveResearchPhase.Collecting)
            {
                return;
            }

            researchCycleObservationTime += Time.deltaTime;
            bool enoughActions = currentObservedPattern.Count >= RequiredPatternLength;
            if (enoughActions
                && researchCycleObservationTime >= minimumObservationDuration)
            {
                BeginPrediction();
            }
            else if (researchCycleObservationTime >= maximumObservationDuration)
            {
                ExpireResearchCycle();
            }
        }

        private void EnsurePlayerActionObserver()
        {
            if (playerActionObserver != null)
            {
                return;
            }

            observedPlayer = FindObjectOfType<PlayerHealth>();
            if (observedPlayer == null)
            {
                return;
            }

            playerActionObserver = observedPlayer.GetComponent<PlayerActionObserver>();
            if (playerActionObserver == null)
            {
                playerActionObserver = observedPlayer.gameObject.AddComponent<PlayerActionObserver>();
            }

            playerActionObserver.MeaningfulAction -= HandlePlayerAction;
            playerActionObserver.MeaningfulAction += HandlePlayerAction;
        }

        private void HandlePlayerAction(PlayerActionObservation observation)
        {
            if (!HasActiveResearchObservation())
            {
                return;
            }

            if (Mathf.Abs(observation.HorizontalDirection) > 0.01f)
            {
                lastObservedHorizontalDirection = observation.HorizontalDirection;
            }

            if (researchPhase == DetectiveResearchPhase.Collecting)
            {
                RecordObservedAction(observation.Category);
                return;
            }

            if (researchPhase != DetectiveResearchPhase.Predicting)
            {
                return;
            }

            consecutiveMoveRepetitions = 0;
            bool success = observation.Category == predictedNextAction;
            lastResearchOutcome = success
                ? DetectiveResearchOutcome.Success
                : DetectiveResearchOutcome.Corrupted;
            if (success)
            {
                accumulatedResearch += successfulResearchReward;
                RefreshResearchStage();
            }

            float learnedDirection = Mathf.Abs(lastObservedHorizontalDirection) > 0.01f
                ? Mathf.Sign(lastObservedHorizontalDirection)
                : 0f;
            tacticalHorizontalBias = success ? learnedDirection : -learnedDirection;
            tacticalBiasExpiresAt = Time.time + tacticalBiasDuration;
            researchPhase = DetectiveResearchPhase.Outcome;
            outcomeHidesAt = Time.time + researchOutcomeDisplayDuration;
            SetResearchHudVisible(true);
            ResearchDisplayChanged?.Invoke();
        }

        private void RecordObservedAction(PlayerActionCategory category)
        {
            if (currentObservedPattern.Count >= maximumPatternLength)
            {
                return;
            }

            bool isMove = category == PlayerActionCategory.Reposition;
            bool previousRecordedActionWasMove = currentObservedPattern.Count > 0
                && currentObservedPattern[currentObservedPattern.Count - 1]
                    == PlayerActionCategory.Reposition;
            if (isMove && previousRecordedActionWasMove)
            {
                consecutiveMoveRepetitions++;
                if (consecutiveMoveRepetitions < repeatedMoveEventsPerSample)
                {
                    return;
                }

                consecutiveMoveRepetitions = 0;
            }
            else
            {
                consecutiveMoveRepetitions = 0;
            }

            currentObservedPattern.Add(category);
            ResearchDisplayChanged?.Invoke();
        }

        private void BeginPrediction()
        {
            if (currentObservedPattern.Count == 0)
            {
                ExpireResearchCycle();
                return;
            }

            consecutiveMoveRepetitions = 0;
            predictedNextAction = currentObservedPattern[0];
            researchPhase = DetectiveResearchPhase.Predicting;
            ResearchDisplayChanged?.Invoke();
        }

        private void ExpireResearchCycle()
        {
            consecutiveMoveRepetitions = 0;
            predictedNextAction = default;
            lastResearchOutcome = DetectiveResearchOutcome.Expired;
            researchPhase = DetectiveResearchPhase.Outcome;
            outcomeHidesAt = Time.time + researchOutcomeDisplayDuration;
            SetResearchHudVisible(true);
            ResearchDisplayChanged?.Invoke();
        }

        private bool HasActiveResearchObservation()
        {
            if (activeTower == null)
            {
                return false;
            }

            DetectiveIdentity researcher = FindExistingIntent(DetectiveIntent.Researcher);
            return researcher != null
                && researcher.CanAct
                && researcherObservingUntil.TryGetValue(researcher, out float observingUntil)
                && Time.time <= observingUntil;
        }

        private void BeginResearchCycle()
        {
            currentObservedPattern.Clear();
            lastResearchOutcome = DetectiveResearchOutcome.None;
            predictedNextAction = default;
            researchCycleObservationTime = 0f;
            consecutiveMoveRepetitions = 0;
            lastObservedHorizontalDirection = 0f;
            researchPhase = DetectiveResearchPhase.Collecting;
            SetResearchHudVisible(true);
            ResearchDisplayChanged?.Invoke();
        }

        private void ResetResearchCycle(bool startCooldown)
        {
            bool changed = researchPhase != (startCooldown
                    ? DetectiveResearchPhase.Cooldown
                    : DetectiveResearchPhase.Idle)
                || currentObservedPattern.Count > 0
                || lastResearchOutcome != DetectiveResearchOutcome.None
                || researchHudVisible;
            currentObservedPattern.Clear();
            lastResearchOutcome = DetectiveResearchOutcome.None;
            predictedNextAction = default;
            researchCycleObservationTime = 0f;
            consecutiveMoveRepetitions = 0;
            researchPhase = startCooldown
                ? DetectiveResearchPhase.Cooldown
                : DetectiveResearchPhase.Idle;
            if (startCooldown)
            {
                nextResearchCycleTime = Time.time + researchCycleCooldown;
            }

            researchHudVisible = false;
            if (changed)
            {
                ResearchDisplayChanged?.Invoke();
            }
        }

        private void RefreshResearchStage()
        {
            int resolvedStage = 0;
            for (int index = 0; index < researchStageThresholds.Length; index++)
            {
                if (accumulatedResearch >= researchStageThresholds[index])
                {
                    resolvedStage = index + 1;
                }
            }

            resolvedStage = Mathf.Clamp(
                resolvedStage,
                0,
                recoveryMultipliersByResearchStage.Length - 1);
            if (resolvedStage == currentResearchStage)
            {
                return;
            }

            currentResearchStage = resolvedStage;
            activeTower?.ApplyResearchStage(currentResearchStage);
            TowerStatusChanged?.Invoke();
        }

        private int ResolveRequiredPatternLength()
        {
            PlayerCurseController curse = observedPlayer != null
                ? observedPlayer.GetComponent<PlayerCurseController>()
                : PlayerCurseController.Active;
            int curseBonus = curse != null ? curse.AdditionalResearchActions : 0;
            return Mathf.Clamp(
                normalPatternLength + curseBonus,
                2,
                maximumPatternLength);
        }

        private void SetResearchHudVisible(bool visible)
        {
            if (researchHudVisible == visible)
            {
                return;
            }

            researchHudVisible = visible;
            ResearchDisplayChanged?.Invoke();
        }

        private void CompleteTower()
        {
            DetectiveTower tower;
            if (towerPrefab != null)
            {
                tower = Instantiate(towerPrefab, constructionPosition, Quaternion.identity);
            }
            else
            {
                GameObject towerObject = new GameObject("Detective Control Tower");
                towerObject.transform.position = constructionPosition;
                tower = towerObject.AddComponent<DetectiveTower>();
            }

            activeTower = tower;
            activeTower.Initialize(
                this,
                towerMaximumHealth,
                towerInitialRadius,
                towerMaximumRadius,
                towerRadiusGrowthPerSecond,
                recoveryMultipliersByResearchStage,
                manaCostMultipliersByResearchStage,
                dropRateMultipliersByResearchStage);
            activeTower.ApplyResearchStage(currentResearchStage);
            constructionProgress = 0f;
            constructionPausedSince = -1f;
            intentions.Clear();
            builderReadyUntil.Clear();
            rolesDirty = true;
            TowerStatusChanged?.Invoke();
        }

        private void FailConstruction()
        {
            List<DetectiveIdentity> failedBuilders = new List<DetectiveIdentity>();
            foreach (KeyValuePair<DetectiveIdentity, DetectiveIntent> pair in intentions)
            {
                if (pair.Value == DetectiveIntent.Builder && pair.Key != null)
                {
                    failedBuilders.Add(pair.Key);
                }
            }

            foreach (DetectiveIdentity builder in failedBuilders)
            {
                intentions.Remove(builder);
                builderReadyUntil.Remove(builder);
                unavailableUntil[builder] = Time.time + failedBuilderRecovery;
            }

            constructionProgress = 0f;
            constructionPausedSince = -1f;
            nextTowerBuildTime = Time.time + failedBuilderRecovery;
            rolesDirty = true;
        }

        private Vector2 ResolveConstructionPosition()
        {
            Vector2 average = Vector2.zero;
            foreach (DetectiveIdentity detective in orderedRealDetectives)
            {
                average += (Vector2)detective.transform.position;
            }

            average /= Mathf.Max(1, orderedRealDetectives.Count);
            int mask = groundLayers.value != 0
                ? groundLayers.value
                : LayerMask.GetMask("Ground");
            if (mask == 0)
            {
                return average;
            }

            RaycastHit2D hit = Physics2D.Raycast(
                average + Vector2.up * 2f,
                Vector2.down,
                5f,
                mask);
            return hit.collider != null ? hit.point : average;
        }

        private void BuildOrderedRealList()
        {
            orderedRealDetectives.Clear();
            foreach (DetectiveIdentity detective in realDetectives)
            {
                if (detective != null
                    && detective.gameObject.activeInHierarchy
                    && detective.Damageable != null
                    && detective.Damageable.CurrentHealth > 0)
                {
                    orderedRealDetectives.Add(detective);
                }
            }

            orderedRealDetectives.Sort(
                (left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));
            realDetectiveCount = orderedRealDetectives.Count;
        }

        private void RemoveMissingMembers()
        {
            realDetectives.RemoveWhere(
                value => value == null || !value.gameObject.activeInHierarchy);
            clones.RemoveWhere(
                value => value == null || !value.gameObject.activeInHierarchy);
        }

        private void RemoveInvalidIntentions()
        {
            List<DetectiveIdentity> stale = new List<DetectiveIdentity>();
            foreach (DetectiveIdentity identity in intentions.Keys)
            {
                if (identity == null || !realDetectives.Contains(identity) || !identity.CanAct)
                {
                    stale.Add(identity);
                }
            }

            foreach (DetectiveIdentity identity in stale)
            {
                intentions.Remove(identity);
                builderReadyUntil.Remove(identity);
                researcherObservingUntil.Remove(identity);
            }
        }

        private DetectiveIdentity FindExistingIntent(DetectiveIntent intent)
        {
            foreach (KeyValuePair<DetectiveIdentity, DetectiveIntent> pair in intentions)
            {
                if (pair.Value == intent && pair.Key != null && pair.Key.CanAct)
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private DetectiveIdentity FindFirstAvailable()
        {
            foreach (DetectiveIdentity detective in orderedRealDetectives)
            {
                if (IsRoleAvailable(detective))
                {
                    return detective;
                }
            }

            return null;
        }

        private bool IsRoleAvailable(DetectiveIdentity detective)
        {
            return detective != null
                && detective.CanAct
                && (!unavailableUntil.TryGetValue(detective, out float unavailable)
                    || Time.time >= unavailable);
        }

        private void AssignIntent(
            DetectiveIdentity detective,
            DetectiveIntent intent,
            bool lockRole)
        {
            if (detective == null || !IsRoleAvailable(detective))
            {
                return;
            }

            if (intentions.TryGetValue(detective, out DetectiveIntent existing)
                && roleLockedUntil.TryGetValue(detective, out float lockedUntil)
                && Time.time < lockedUntil
                && existing != intent)
            {
                return;
            }

            intentions[detective] = intent;
            if (lockRole)
            {
                roleLockedUntil[detective] = Time.time + minimumRoleCommitment;
            }
        }

        private void RefreshCounts()
        {
            realDetectiveCount = 0;
            foreach (DetectiveIdentity detective in realDetectives)
            {
                if (detective != null && detective.gameObject.activeInHierarchy)
                {
                    realDetectiveCount++;
                }
            }

            activeCloneCount = reservedCloneSlots;
            foreach (DetectiveIdentity clone in clones)
            {
                if (clone != null && clone.gameObject.activeInHierarchy)
                {
                    activeCloneCount++;
                }
            }
        }

        private void OnValidate()
        {
            towerMaximumRadius = Mathf.Max(towerInitialRadius, towerMaximumRadius);
            maximumObservationRange = Mathf.Max(
                minimumObservationRange,
                maximumObservationRange);
            fullConstructionDuration = Mathf.Max(0.1f, fullConstructionDuration);
            maximumObservationDuration = Mathf.Max(
                minimumObservationDuration,
                maximumObservationDuration);
            maximumPatternLength = Mathf.Clamp(maximumPatternLength, 3, 5);
            repeatedMoveEventsPerSample = Mathf.Max(1, repeatedMoveEventsPerSample);
            normalPatternLength = Mathf.Clamp(
                normalPatternLength,
                2,
                maximumPatternLength);
        }

        private void OnDestroy()
        {
            if (playerActionObserver != null)
            {
                playerActionObserver.MeaningfulAction -= HandlePlayerAction;
            }
        }
    }
}
