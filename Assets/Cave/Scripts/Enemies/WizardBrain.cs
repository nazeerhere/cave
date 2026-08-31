using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum WizardBrainState
    {
        Patrol,
        SupportPosition,
        Cast,
        Retreat,
        Attack,
        ReturnToPatrol
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(WizardFlightMotor), typeof(WizardSupportAbilities))]
    public sealed class WizardBrain : MonoBehaviour
    {
        [Header("Flight / Awareness")]
        [SerializeField, Min(0.1f)] private float detectionRange = 12f;
        [SerializeField, Min(0.1f)] private float loseTargetRange = 16f;
        [SerializeField, Min(0.1f)] private float immediateDangerRange = 2.25f;
        [SerializeField, Min(0.1f)] private float preferredSupportRange = 5.5f;
        [SerializeField, Min(0f)] private float supportRangeTolerance = 1.25f;
        [SerializeField, Min(0f)] private float hoverAboveFrontline = 1.4f;
        [SerializeField, Min(0f)] private float behindFrontlineDistance = 1.25f;
        [SerializeField, Min(0.1f)] private float maximumHeightAbovePlayer = 2.5f;
        [SerializeField, Min(0.02f)] private float decisionInterval = 0.15f;
        [SerializeField, Min(0f)] private float patrolRadius = 2f;
        [SerializeField, Min(0f)] private float patrolHoverAmplitude = 0.5f;

        [Header("Debug (Read Only)")]
        [SerializeField] private WizardBrainState currentState;
        [SerializeField] private string currentDecision;

        private WizardFlightMotor flight;
        private WizardSupportAbilities support;
        private EnemyShooter projectile;
        private Damageable damageable;
        private PlayerHealth player;
        private Vector2 spawnPosition;
        private float nextDecisionTime;

        public WizardBrainState CurrentState => currentState;
        public string CurrentDecision => currentDecision;

        private void Awake()
        {
            flight = GetComponent<WizardFlightMotor>();
            support = GetComponent<WizardSupportAbilities>();
            projectile = GetComponentInChildren<EnemyShooter>(true);
            damageable = GetComponent<Damageable>();
            spawnPosition = transform.position;
            projectile?.SetBrainControlled(true);
            projectile?.EnableWizardEcho();
            GetComponent<EnemyHealAbility>()?.SetBrainControlled(true);
            GetComponent<EnemyDamageBuffAbility>()?.SetBrainControlled(true);
            EnemyArchetypeProfile profile = GetComponent<EnemyArchetypeProfile>();
            if (profile == null)
            {
                profile = gameObject.AddComponent<EnemyArchetypeProfile>();
            }

            profile.AddRuntimeArchetype(EnemyArchetype.Support | EnemyArchetype.Ranged);
            if (damageable != null)
            {
                damageable.DamageResolved += HandleDamageResolved;
            }
        }

        private void Update()
        {
            EnsurePlayer();
            if (support != null && support.IsCasting)
            {
                flight.HoldPosition();
                SetDecision(WizardBrainState.Cast, support.CurrentAction.ToString());
                return;
            }

            if (projectile != null && projectile.IsBusy)
            {
                flight.HoldPosition();
                SetDecision(WizardBrainState.Attack, "Weak projectile cast");
                return;
            }

            if (Time.time < nextDecisionTime)
            {
                return;
            }

            nextDecisionTime = Time.time + decisionInterval;
            if (player == null)
            {
                Patrol();
                return;
            }

            float playerDistance = Vector2.Distance(transform.position, player.transform.position);
            if (playerDistance > loseTargetRange)
            {
                ReturnToPatrol();
                return;
            }

            if (playerDistance < immediateDangerRange
                && support != null
                && support.TryBeginSelfTeleport(player))
            {
                flight.HoldPosition();
                SetDecision(WizardBrainState.Retreat, "Emergency self teleport");
                return;
            }

            Damageable criticalAlly = support != null ? support.FindCriticalAlly() : null;
            if (criticalAlly != null
                && playerDistance < preferredSupportRange
                && support.TryBeginAllyTeleport(criticalAlly, player))
            {
                flight.HoldPosition();
                SetDecision(WizardBrainState.Cast, "Extract critical ally");
                return;
            }

            if (support != null && support.TryBeginHeal())
            {
                flight.HoldPosition();
                SetDecision(WizardBrainState.Cast, "Channel heal");
                return;
            }

            if (support != null && support.TryBeginDispel())
            {
                flight.HoldPosition();
                SetDecision(WizardBrainState.Cast, "Dispel removable status");
                return;
            }

            if (support != null
                && playerDistance <= preferredSupportRange + supportRangeTolerance
                && support.TryBeginForcePlayerTeleport(player))
            {
                flight.HoldPosition();
                SetDecision(WizardBrainState.Cast, "Force low-Mana player teleport");
                return;
            }

            if (support != null && support.TryBeginEmpower())
            {
                flight.HoldPosition();
                SetDecision(WizardBrainState.Cast, "Elemental ally empower");
                return;
            }

            if (support != null
                && playerDistance >= preferredSupportRange - supportRangeTolerance
                && support.TryBeginEyePairSummon())
            {
                flight.HoldPosition();
                SetDecision(WizardBrainState.Cast, "Summon evolved Eye pair");
                return;
            }

            if (NeedsReposition(playerDistance))
            {
                MoveToSupportPosition();
                return;
            }

            if (projectile != null && projectile.TryUse(player.transform))
            {
                flight.HoldPosition();
                SetDecision(WizardBrainState.Attack, "Weak projectile fallback");
                return;
            }

            MoveToSupportPosition();
        }

        private bool NeedsReposition(float distance)
        {
            return distance < preferredSupportRange - supportRangeTolerance
                || distance > preferredSupportRange + supportRangeTolerance;
        }

        private void MoveToSupportPosition()
        {
            Vector2 playerPosition = player.transform.position;
            Damageable frontline = FindFrontlineAlly(playerPosition);
            Vector2 anchor = frontline != null
                ? (Vector2)frontline.transform.position
                : playerPosition;
            float awaySign = Mathf.Sign(anchor.x - playerPosition.x);
            if (Mathf.Approximately(awaySign, 0f))
            {
                awaySign = Mathf.Sign(transform.position.x - playerPosition.x);
            }

            if (Mathf.Approximately(awaySign, 0f))
            {
                awaySign = 1f;
            }

            Vector2 desired = frontline != null
                ? anchor + new Vector2(awaySign * behindFrontlineDistance, hoverAboveFrontline)
                : playerPosition + new Vector2(awaySign * preferredSupportRange, hoverAboveFrontline);
            desired.y = Mathf.Clamp(
                desired.y,
                playerPosition.y + 0.6f,
                playerPosition.y + maximumHeightAbovePlayer);
            flight.MoveTo(desired);
            SetDecision(WizardBrainState.SupportPosition, "Hover behind/above frontline");
        }

        private Damageable FindFrontlineAlly(Vector2 playerPosition)
        {
            List<Damageable> allies = EnemySupportTargeting.CollectAllies(
                transform.position,
                detectionRange,
                ~0,
                damageable,
                false);
            Damageable selected = null;
            float bestDistance = float.PositiveInfinity;
            foreach (Damageable ally in allies)
            {
                EnemyArchetypeProfile profile = ally.GetComponent<EnemyArchetypeProfile>();
                if (profile != null && profile.Includes(EnemyArchetype.Support))
                {
                    continue;
                }

                float distance = ((Vector2)ally.transform.position - playerPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    selected = ally;
                    bestDistance = distance;
                }
            }

            return selected;
        }

        private void Patrol()
        {
            float phase = Time.time * 0.55f + Mathf.Abs(GetInstanceID() % 11);
            Vector2 destination = spawnPosition + new Vector2(
                Mathf.Sin(phase) * patrolRadius,
                Mathf.Cos(phase * 0.7f) * patrolHoverAmplitude);
            flight.MoveTo(destination);
            SetDecision(WizardBrainState.Patrol, "Controlled hover patrol");
        }

        private void ReturnToPatrol()
        {
            if (((Vector2)transform.position - spawnPosition).sqrMagnitude > 0.25f)
            {
                flight.MoveTo(spawnPosition);
                SetDecision(WizardBrainState.ReturnToPatrol, "Return to hover origin");
            }
            else
            {
                player = null;
                Patrol();
            }
        }

        private void EnsurePlayer()
        {
            if (player != null && player.gameObject.activeInHierarchy)
            {
                return;
            }

            PlayerHealth candidate = FindObjectOfType<PlayerHealth>();
            if (candidate != null
                && Vector2.Distance(transform.position, candidate.transform.position) <= detectionRange)
            {
                player = candidate;
            }
        }

        private void HandleDamageResolved(DamageContext context, bool blocked, int appliedDamage)
        {
            if (!blocked && appliedDamage > 0)
            {
                projectile?.Interrupt();
            }
        }

        private void SetDecision(WizardBrainState state, string decision)
        {
            currentState = state;
            currentDecision = decision;
        }

        private void OnDisable()
        {
            flight?.HoldPosition();
            support?.Interrupt();
            projectile?.Interrupt();
            player = null;
        }

        private void OnDestroy()
        {
            if (damageable != null)
            {
                damageable.DamageResolved -= HandleDamageResolved;
            }
        }

        private void OnValidate()
        {
            loseTargetRange = Mathf.Max(detectionRange, loseTargetRange);
            preferredSupportRange = Mathf.Max(immediateDangerRange, preferredSupportRange);
            maximumHeightAbovePlayer = Mathf.Max(0.6f, maximumHeightAbovePlayer);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.45f, 0.3f, 1f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, preferredSupportRange);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, immediateDangerRange);
        }
    }
}
