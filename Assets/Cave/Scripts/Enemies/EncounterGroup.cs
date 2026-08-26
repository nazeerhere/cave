using System;
using System.Collections.Generic;
using Cave.Progression;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EncounterGroup : MonoBehaviour
    {
        [SerializeField] private StrategicCombatSettings settings;
        [SerializeField] private WorldDifficultyManager difficultyManager;
        [SerializeField] private EnemyArchetypeProfile[] members;

        [Header("Current Composition (Read Only)")]
        [SerializeField] private EnemyArchetype currentComposition;
        [SerializeField] private bool currentCompositionEligible = true;

        [Header("Skeleton Formation (Read Only)")]
        [SerializeField, Min(0)] private int desiredSkeletonGenerals;
        [SerializeField, Min(0)] private int activeSkeletonGenerals;
        [SerializeField, Min(0)] private int activeLesserSkeletons;
        [SerializeField] private SkeletonInheritance strongestGeneral;
        [SerializeField, Min(0)] private int strongestGeneralWitnessedDeaths;
        [SerializeField] private SkeletonInheritance anchorGeneral;
        [SerializeField] private SkeletonInheritance assaultGeneral;

        private readonly HashSet<SkeletonInheritance> skeletonMembers =
            new HashSet<SkeletonInheritance>();
        private readonly List<SkeletonInheritance> skeletonNotificationSnapshot =
            new List<SkeletonInheritance>();

        public event Action CompositionEligibilityChanged;
        public event Action SkeletonFormationChanged;

        public EnemyArchetype CurrentComposition => currentComposition;
        public bool CurrentCompositionEligible => currentCompositionEligible;
        public int RegisteredSkeletonCount => skeletonMembers.Count;
        public int ActiveSkeletonGenerals => CountRank(SkeletonRank.General);
        public int ActiveLesserSkeletons => CountRank(SkeletonRank.Lesser);
        public int OpenGeneralSlots => Mathf.Max(0, desiredSkeletonGenerals - ActiveSkeletonGenerals);
        public SkeletonInheritance StrongestGeneral => FindStrongestGeneral();
        public SkeletonInheritance AnchorGeneral => FindGeneralWithRole(SkeletonGeneralRole.Anchor);
        public SkeletonInheritance AssaultGeneral => FindGeneralWithRole(SkeletonGeneralRole.Assault);

        internal void RegisterSkeleton(SkeletonInheritance skeleton)
        {
            if (skeleton != null && skeletonMembers.Add(skeleton))
            {
                EnsureGeneralSlots();
                RefreshSkeletonDebug();
                SkeletonFormationChanged?.Invoke();
            }
        }

        internal void UnregisterSkeleton(SkeletonInheritance skeleton)
        {
            if (skeleton != null && skeletonMembers.Remove(skeleton))
            {
                EnsureGeneralSlots();
                SkeletonFormationChanged?.Invoke();
            }
        }

        internal void ConfigureSkeletonFormation(int desiredGenerals)
        {
            desiredSkeletonGenerals = Mathf.Max(0, desiredGenerals);
            EnsureGeneralSlots();
            RefreshSkeletonDebug();
        }

        internal void NotifySkeletonRankChanged()
        {
            AssignGeneralRoles();
            RefreshSkeletonDebug();
            SkeletonFormationChanged?.Invoke();
        }

        internal void ReportSkeletonDeath(SkeletonInheritance deceased)
        {
            if (deceased == null || !skeletonMembers.Remove(deceased))
            {
                return;
            }

            skeletonNotificationSnapshot.Clear();
            skeletonNotificationSnapshot.AddRange(skeletonMembers);
            foreach (SkeletonInheritance survivor in skeletonNotificationSnapshot)
            {
                if (survivor != null && survivor.CanWitnessGroupDeath)
                {
                    survivor.ReceiveWitnessedDeath();
                }
            }

            EnsureGeneralSlots();
            RefreshSkeletonDebug();
            SkeletonFormationChanged?.Invoke();
        }

        internal void EnsureGeneralSlots()
        {
            RemoveMissingSkeletons();
            while (CountRank(SkeletonRank.General) < desiredSkeletonGenerals)
            {
                SkeletonInheritance promotion = FindBestPromotionCandidate();
                if (promotion == null)
                {
                    break;
                }

                promotion.SetRank(SkeletonRank.General);
            }

            AssignGeneralRoles();
        }

        internal bool TryGetLesserFormationReference(
            Vector2 observerPosition,
            float advanceDirection,
            out float frontX,
            out float nearestHorizontalDistance)
        {
            frontX = observerPosition.x;
            nearestHorizontalDistance = float.PositiveInfinity;
            bool found = false;
            float direction = Mathf.Approximately(advanceDirection, 0f)
                ? 1f
                : Mathf.Sign(advanceDirection);
            foreach (SkeletonInheritance skeleton in skeletonMembers)
            {
                if (skeleton == null
                    || skeleton.Rank != SkeletonRank.Lesser
                    || !skeleton.CanWitnessGroupDeath)
                {
                    continue;
                }

                float candidateX = skeleton.transform.position.x;
                float distance = Mathf.Abs(candidateX - observerPosition.x);
                nearestHorizontalDistance = Mathf.Min(nearestHorizontalDistance, distance);
                if (!found
                    || (direction > 0f && candidateX > frontX)
                    || (direction < 0f && candidateX < frontX))
                {
                    frontX = candidateX;
                }

                found = true;
            }

            if (!found)
            {
                nearestHorizontalDistance = 0f;
            }

            return found;
        }

        private void AssignGeneralRoles()
        {
            SkeletonInheritance currentAnchor = null;
            SkeletonInheritance currentAssault = null;
            foreach (SkeletonInheritance skeleton in skeletonMembers)
            {
                if (skeleton == null || !skeleton.CanWitnessGroupDeath)
                {
                    continue;
                }

                if (skeleton.Rank != SkeletonRank.General)
                {
                    skeleton.SetGeneralRole(SkeletonGeneralRole.None);
                    continue;
                }

                if (skeleton.GeneralRole == SkeletonGeneralRole.Anchor)
                {
                    if (currentAnchor == null)
                    {
                        currentAnchor = skeleton;
                    }
                    else
                    {
                        skeleton.SetGeneralRole(SkeletonGeneralRole.None);
                    }
                }
                else if (skeleton.GeneralRole == SkeletonGeneralRole.Assault)
                {
                    if (currentAssault == null)
                    {
                        currentAssault = skeleton;
                    }
                    else
                    {
                        skeleton.SetGeneralRole(SkeletonGeneralRole.None);
                    }
                }
            }

            if (currentAnchor == null)
            {
                if (currentAssault != null)
                {
                    currentAnchor = currentAssault;
                    currentAssault = null;
                }
                else
                {
                    currentAnchor = FindEarliestGeneral(null);
                }

                currentAnchor?.SetGeneralRole(SkeletonGeneralRole.Anchor);
            }

            if (currentAssault == null)
            {
                currentAssault = FindEarliestGeneral(currentAnchor);
                currentAssault?.SetGeneralRole(SkeletonGeneralRole.Assault);
            }

            foreach (SkeletonInheritance skeleton in skeletonMembers)
            {
                if (skeleton == null
                    || skeleton.Rank != SkeletonRank.General
                    || skeleton == currentAnchor
                    || skeleton == currentAssault)
                {
                    continue;
                }

                skeleton.SetGeneralRole(SkeletonGeneralRole.None);
            }
        }

        private SkeletonInheritance FindEarliestGeneral(SkeletonInheritance excluded)
        {
            SkeletonInheritance selected = null;
            foreach (SkeletonInheritance skeleton in skeletonMembers)
            {
                if (skeleton == null
                    || skeleton == excluded
                    || skeleton.Rank != SkeletonRank.General
                    || !skeleton.CanWitnessGroupDeath)
                {
                    continue;
                }

                if (selected == null
                    || skeleton.FormationSequence < selected.FormationSequence
                    || (skeleton.FormationSequence == selected.FormationSequence
                        && skeleton.GetInstanceID() < selected.GetInstanceID()))
                {
                    selected = skeleton;
                }
            }

            return selected;
        }

        private SkeletonInheritance FindGeneralWithRole(SkeletonGeneralRole role)
        {
            foreach (SkeletonInheritance skeleton in skeletonMembers)
            {
                if (skeleton != null
                    && skeleton.CanWitnessGroupDeath
                    && skeleton.Rank == SkeletonRank.General
                    && skeleton.GeneralRole == role)
                {
                    return skeleton;
                }
            }

            return null;
        }

        private SkeletonInheritance FindBestPromotionCandidate()
        {
            SkeletonInheritance best = null;
            foreach (SkeletonInheritance candidate in skeletonMembers)
            {
                if (candidate == null
                    || candidate.Rank != SkeletonRank.Lesser
                    || !candidate.CanWitnessGroupDeath)
                {
                    continue;
                }

                if (best == null || IsBetterPromotionCandidate(candidate, best))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static bool IsBetterPromotionCandidate(
            SkeletonInheritance candidate,
            SkeletonInheritance currentBest)
        {
            if (candidate.WitnessedDeaths != currentBest.WitnessedDeaths)
            {
                return candidate.WitnessedDeaths > currentBest.WitnessedDeaths;
            }

            int strengthComparison = candidate.ResolvedStrengthScore.CompareTo(
                currentBest.ResolvedStrengthScore);
            if (strengthComparison != 0)
            {
                return strengthComparison > 0;
            }

            if (candidate.FormationSequence != currentBest.FormationSequence)
            {
                return candidate.FormationSequence < currentBest.FormationSequence;
            }

            return candidate.GetInstanceID() < currentBest.GetInstanceID();
        }

        private int CountRank(SkeletonRank rank)
        {
            int count = 0;
            foreach (SkeletonInheritance skeleton in skeletonMembers)
            {
                if (skeleton != null
                    && skeleton.CanWitnessGroupDeath
                    && skeleton.Rank == rank)
                {
                    count++;
                }
            }

            return count;
        }

        private SkeletonInheritance FindStrongestGeneral()
        {
            SkeletonInheritance best = null;
            foreach (SkeletonInheritance skeleton in skeletonMembers)
            {
                if (skeleton == null
                    || skeleton.Rank != SkeletonRank.General
                    || !skeleton.CanWitnessGroupDeath)
                {
                    continue;
                }

                if (best == null || IsBetterPromotionCandidate(skeleton, best))
                {
                    best = skeleton;
                }
            }

            return best;
        }

        private void RemoveMissingSkeletons()
        {
            skeletonNotificationSnapshot.Clear();
            foreach (SkeletonInheritance skeleton in skeletonMembers)
            {
                if (skeleton == null)
                {
                    skeletonNotificationSnapshot.Add(skeleton);
                }
            }

            foreach (SkeletonInheritance missing in skeletonNotificationSnapshot)
            {
                skeletonMembers.Remove(missing);
            }
        }

        private void RefreshSkeletonDebug()
        {
            RemoveMissingSkeletons();
            activeSkeletonGenerals = CountRank(SkeletonRank.General);
            activeLesserSkeletons = CountRank(SkeletonRank.Lesser);
            strongestGeneral = FindStrongestGeneral();
            strongestGeneralWitnessedDeaths = strongestGeneral != null
                ? strongestGeneral.WitnessedDeaths
                : 0;
            anchorGeneral = FindGeneralWithRole(SkeletonGeneralRole.Anchor);
            assaultGeneral = FindGeneralWithRole(SkeletonGeneralRole.Assault);
        }

        private void Awake()
        {
            if (members == null || members.Length == 0)
            {
                members = GetComponentsInChildren<EnemyArchetypeProfile>(true);
            }

            Recalculate();
        }

        internal void ConfigureIfMissing(
            StrategicCombatSettings strategicSettings,
            WorldDifficultyManager worldDifficulty)
        {
            if (settings == null)
            {
                settings = strategicSettings;
            }

            if (difficultyManager == null)
            {
                difficultyManager = worldDifficulty;
            }

            Subscribe();
            Recalculate();
        }

        private void Recalculate()
        {
            bool previousEligibility = currentCompositionEligible;
            currentComposition = EnemyArchetype.None;
            if (members != null)
            {
                foreach (EnemyArchetypeProfile member in members)
                {
                    if (member != null)
                    {
                        currentComposition |= member.Archetypes;
                    }
                }
            }

            currentCompositionEligible = IsCompositionEligible(
                currentComposition,
                difficultyManager != null ? difficultyManager.DifficultyTier : 0);
            if (previousEligibility != currentCompositionEligible)
            {
                CompositionEligibilityChanged?.Invoke();
            }
        }

        public bool IsCompositionEligible(EnemyArchetype composition, int difficultyTier)
        {
            if (settings == null)
            {
                return true;
            }

            int distinctStrategies = CountFlags(composition);
            if (distinctStrategies > 1 && difficultyTier < settings.MixedEncounterMinimumTier)
            {
                return false;
            }

            return (!Includes(composition, EnemyArchetype.Melee)
                    || difficultyTier >= settings.MeleeMinimumTier)
                && (!Includes(composition, EnemyArchetype.Ranged)
                    || difficultyTier >= settings.RangedMinimumTier)
                && (!Includes(composition, EnemyArchetype.Support)
                    || difficultyTier >= settings.SupportMinimumTier)
                && (!Includes(composition, EnemyArchetype.Tank)
                    || difficultyTier >= settings.TankMinimumTier)
                && (!Includes(composition, EnemyArchetype.Swarm)
                    || difficultyTier >= settings.SwarmMinimumTier);
        }

        private static bool Includes(EnemyArchetype value, EnemyArchetype flag)
        {
            return (value & flag) != 0;
        }

        private static int CountFlags(EnemyArchetype value)
        {
            int count = 0;
            int bits = (int)value;
            while (bits != 0)
            {
                count += bits & 1;
                bits >>= 1;
            }

            return count;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            if (difficultyManager != null)
            {
                difficultyManager.DifficultyChanged -= Recalculate;
            }
        }

        private void Subscribe()
        {
            if (difficultyManager != null)
            {
                difficultyManager.DifficultyChanged -= Recalculate;
                difficultyManager.DifficultyChanged += Recalculate;
            }
        }
    }
}
