using System.Collections.Generic;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    public static class EnemySupportTargeting
    {
        public static Damageable FindLowestHealthTarget(
            Vector2 origin,
            float radius,
            LayerMask allyLayers,
            Damageable self,
            bool canTargetSelf,
            bool preferNonSupport,
            float maximumHealthFraction)
        {
            List<Damageable> candidates = CollectCandidates(
                origin,
                radius,
                allyLayers,
                self,
                canTargetSelf);
            candidates.Sort((left, right) => CompareHealingTargets(
                left,
                right,
                origin,
                preferNonSupport));
            foreach (Damageable candidate in candidates)
            {
                if (candidate.MaximumHealth > 0
                    && candidate.CurrentHealth < candidate.MaximumHealth
                    && candidate.CurrentHealth / (float)candidate.MaximumHealth
                        <= Mathf.Clamp01(maximumHealthFraction))
                {
                    return candidate;
                }
            }

            return null;
        }

        public static Damageable FindUnbuffedDamageTarget(
            Vector2 origin,
            float radius,
            LayerMask allyLayers,
            Damageable self,
            bool canTargetSelf,
            bool preferNonSupport,
            EnemyDamageModifierType modifierType)
        {
            List<Damageable> candidates = CollectCandidates(
                origin,
                radius,
                allyLayers,
                self,
                canTargetSelf);
            candidates.Sort((left, right) => CompareBuffTargets(
                left,
                right,
                origin,
                preferNonSupport));
            foreach (Damageable candidate in candidates)
            {
                EnemyDamageModifiers modifiers = candidate.GetComponent<EnemyDamageModifiers>();
                if (modifiers == null || !modifiers.HasModifier(modifierType))
                {
                    return candidate;
                }
            }

            return null;
        }

        public static Damageable FindHighestValueUnbuffedDamageTarget(
            Vector2 origin,
            float radius,
            LayerMask allyLayers,
            Damageable self,
            bool canTargetSelf,
            EnemyDamageModifierType modifierType,
            GameObject preferredSkeletonOwner,
            float ownedSkeletonBonus,
            float generalRankBonus,
            float witnessedDeathWeight,
            float resolvedStrengthWeight,
            float tankArchetypeBonus,
            float bruteCombatBonus,
            float meleeArchetypeBonus,
            float rangedArchetypeBonus,
            float maximumDistancePenalty)
        {
            List<Damageable> candidates = CollectCandidates(
                origin,
                radius,
                allyLayers,
                self,
                canTargetSelf);
            Damageable selected = null;
            float selectedScore = float.NegativeInfinity;
            foreach (Damageable candidate in candidates)
            {
                EnemyDamageModifiers modifiers = candidate.GetComponent<EnemyDamageModifiers>();
                if (modifiers != null && modifiers.HasModifier(modifierType))
                {
                    continue;
                }

                float score = CalculateDamageSupportValue(
                    candidate,
                    origin,
                    radius,
                    preferredSkeletonOwner,
                    ownedSkeletonBonus,
                    generalRankBonus,
                    witnessedDeathWeight,
                    resolvedStrengthWeight,
                    tankArchetypeBonus,
                    bruteCombatBonus,
                    meleeArchetypeBonus,
                    rangedArchetypeBonus,
                    maximumDistancePenalty);
                if (selected == null
                    || score > selectedScore
                    || (Mathf.Approximately(score, selectedScore)
                        && candidate.GetInstanceID() < selected.GetInstanceID()))
                {
                    selected = candidate;
                    selectedScore = score;
                }
            }

            return selected;
        }

        private static float CalculateDamageSupportValue(
            Damageable candidate,
            Vector2 origin,
            float radius,
            GameObject preferredSkeletonOwner,
            float ownedSkeletonBonus,
            float generalRankBonus,
            float witnessedDeathWeight,
            float resolvedStrengthWeight,
            float tankArchetypeBonus,
            float bruteCombatBonus,
            float meleeArchetypeBonus,
            float rangedArchetypeBonus,
            float maximumDistancePenalty)
        {
            float score = 1f;
            EnemyArchetypeProfile profile = candidate.GetComponent<EnemyArchetypeProfile>();
            if (profile != null)
            {
                if (profile.Includes(EnemyArchetype.Tank))
                {
                    score += Mathf.Max(0f, tankArchetypeBonus);
                }

                if (profile.Includes(EnemyArchetype.Melee))
                {
                    score += Mathf.Max(0f, meleeArchetypeBonus);
                }

                if (profile.Includes(EnemyArchetype.Ranged))
                {
                    score += Mathf.Max(0f, rangedArchetypeBonus);
                }
            }

            EnemyMeleeCombat melee = candidate.GetComponent<EnemyMeleeCombat>();
            if (melee != null && melee.IsBrutePreset)
            {
                score += Mathf.Max(0f, bruteCombatBonus);
            }

            SkeletonInheritance skeleton = candidate.GetComponent<SkeletonInheritance>();
            if (skeleton != null)
            {
                bool owned = preferredSkeletonOwner != null
                    && skeleton.Summoner == preferredSkeletonOwner;
                if (owned)
                {
                    score += Mathf.Max(0f, ownedSkeletonBonus);
                    score += skeleton.CalculateSupportValue(
                        generalRankBonus,
                        witnessedDeathWeight,
                        resolvedStrengthWeight) - 1f;
                }
                else
                {
                    score += skeleton.WitnessedDeaths
                        * Mathf.Max(0f, witnessedDeathWeight) * 0.5f;
                }
            }

            float radiusSquared = Mathf.Max(0.01f, radius * radius);
            float normalizedDistance = Mathf.Clamp01(
                ((Vector2)candidate.transform.position - origin).sqrMagnitude
                / radiusSquared);
            return score - normalizedDistance * Mathf.Max(0f, maximumDistancePenalty);
        }

        private static List<Damageable> CollectCandidates(
            Vector2 origin,
            float radius,
            LayerMask allyLayers,
            Damageable self,
            bool canTargetSelf)
        {
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(origin, radius, allyLayers);
            HashSet<Damageable> unique = new HashSet<Damageable>();
            List<Damageable> candidates = new List<Damageable>();
            foreach (Collider2D overlap in overlaps)
            {
                Damageable candidate = overlap.GetComponentInParent<Damageable>();
                if (candidate == null
                    || candidate.CurrentHealth <= 0
                    || !candidate.gameObject.activeInHierarchy
                    || (!canTargetSelf && candidate == self)
                    || candidate.GetComponent<EnemyArchetypeProfile>() == null
                    || !unique.Add(candidate))
                {
                    continue;
                }

                candidates.Add(candidate);
            }

            return candidates;
        }

        private static int CompareHealingTargets(
            Damageable left,
            Damageable right,
            Vector2 origin,
            bool preferNonSupport)
        {
            if (preferNonSupport)
            {
                int supportComparison = IsSupport(left).CompareTo(IsSupport(right));
                if (supportComparison != 0)
                {
                    return supportComparison;
                }
            }

            float leftFraction = left.CurrentHealth / (float)Mathf.Max(1, left.MaximumHealth);
            float rightFraction = right.CurrentHealth / (float)Mathf.Max(1, right.MaximumHealth);
            int healthComparison = leftFraction.CompareTo(rightFraction);
            return healthComparison != 0
                ? healthComparison
                : CompareDistanceThenIdentity(left, right, origin);
        }

        private static int CompareBuffTargets(
            Damageable left,
            Damageable right,
            Vector2 origin,
            bool preferNonSupport)
        {
            if (preferNonSupport)
            {
                int supportComparison = IsSupport(left).CompareTo(IsSupport(right));
                if (supportComparison != 0)
                {
                    return supportComparison;
                }
            }

            return CompareDistanceThenIdentity(left, right, origin);
        }

        private static int CompareDistanceThenIdentity(Damageable left, Damageable right, Vector2 origin)
        {
            float leftDistance = ((Vector2)left.transform.position - origin).sqrMagnitude;
            float rightDistance = ((Vector2)right.transform.position - origin).sqrMagnitude;
            int distanceComparison = leftDistance.CompareTo(rightDistance);
            return distanceComparison != 0
                ? distanceComparison
                : left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private static bool IsSupport(Damageable damageable)
        {
            EnemyArchetypeProfile profile = damageable.GetComponent<EnemyArchetypeProfile>();
            return profile != null && profile.Includes(EnemyArchetype.Support);
        }
    }
}
