using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Interactions;
using Cave.Player;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>One-shot, close-pressure answer. It uses normal player Guard Break traits.</summary>
    [DisallowMultipleComponent]
    public sealed class FalseGodClaimBurst : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float windup = 0.45f;
        [SerializeField, Min(0.1f)] private float radius = 3.3f;
        [SerializeField, Min(0f)] private float cooldown = 8f;
        [SerializeField, Min(1)] private int damage = 1;
        private Coroutine routine;
        private float nextUse;

        public bool IsCasting => routine != null;
        public bool TryCast(PlayerHealth target)
        {
            if (target == null || routine != null || Time.time < nextUse
                || Vector2.Distance(transform.position, target.transform.position) > radius) return false;
            nextUse = Time.time + cooldown;
            routine = StartCoroutine(Perform(target));
            return true;
        }

        private IEnumerator Perform(PlayerHealth target)
        {
            FalseGodCombatPresentation.CreateImpactTelegraph(transform.position, radius, windup);
            yield return new WaitForSeconds(windup);
            GetComponent<FalseGodCombatController>()?.ReleasePresentationHold();
            if (target != null && Vector2.Distance(transform.position, target.transform.position) <= radius)
            {
                target.TryTakeDamage(damage, new DamageContext(gameObject, DamageTrait.AreaOfEffect | DamageTrait.GuardBreak));
                target.GetComponent<PlayerGuardBreak>()?.ApplyEnemyGuardBreak(((Vector2)target.transform.position - (Vector2)transform.position).normalized);
            }
            FalseGodCombatPresentation.CreateCrystalImpact(transform.position);
            routine = null;
        }
    }

    /// <summary>Infrequent, network-local relocation; no scene scan or geometry mutation.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FalseGodRuntimeFoundation))]
    public sealed class FalseGodClaimTeleport : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float cooldown = 10f;
        [SerializeField, Min(0.1f)] private float minimumTravel = 2.25f;
        private readonly List<ClaimAnchor> candidates = new List<ClaimAnchor>(8);
        private FalseGodRuntimeFoundation foundation;
        private ClaimAnchor lastDestination;
        private float nextTeleport;

        public bool TryTeleport()
        {
            if (Time.time < nextTeleport) return false;
            if (foundation == null) foundation = GetComponent<FalseGodRuntimeFoundation>();
            if (foundation == null || foundation.AuthorityNetwork == null) return false;
            foundation.AuthorityNetwork.CopyActiveAnchors(candidates);
            for (int index = candidates.Count - 1; index >= 0; index--)
            {
                ClaimAnchor anchor = candidates[index];
                if (anchor == null || !anchor.IsActive || anchor.GetComponent<ClaimCrystal>() == null
                    || Vector2.Distance(transform.position, anchor.transform.position) < minimumTravel) candidates.RemoveAt(index);
            }
            if (candidates.Count == 0) return false;
            int pick = Random.Range(0, candidates.Count);
            if (candidates.Count > 1 && candidates[pick] == lastDestination) pick = (pick + 1) % candidates.Count;
            ClaimAnchor destination = candidates[pick];
            FalseGodCombatPresentation.CreateCaptureCue(transform.position);
            transform.position = destination.transform.position;
            FalseGodCombatPresentation.CreateCaptureCue(transform.position);
            lastDestination = destination;
            nextTeleport = Time.time + cooldown;
            return true;
        }
    }
}
