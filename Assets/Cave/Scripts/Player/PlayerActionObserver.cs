using System;
using Cave.Combat;
using Cave.Projectiles;
using UnityEngine;

namespace Cave.Player
{
    public enum PlayerActionCategory
    {
        Attack,
        Defend,
        Evade,
        Projectile,
        Reposition,
        Special
    }

    public readonly struct PlayerActionObservation
    {
        public PlayerActionObservation(PlayerActionCategory category, float horizontalDirection)
        {
            Category = category;
            HorizontalDirection = Mathf.Abs(horizontalDirection) > 0.01f
                ? Mathf.Sign(horizontalDirection)
                : 0f;
        }

        public PlayerActionCategory Category { get; }
        public float HorizontalDirection { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerActionObserver : MonoBehaviour
    {
        [Header("Meaningful Reposition")]
        [SerializeField, Min(0.1f)] private float repositionDistance = 1.5f;
        [SerializeField, Min(0f)] private float repositionMinimumDuration = 0.4f;
        [SerializeField, Min(0f)] private float repositionReportCooldown = 1.5f;

        private PlayerAttackState attackState;
        private PlayerController playerController;
        private PlayerDash dash;
        private PlayerFlightBash groundSmash;
        private PlayerProjectileLauncher projectileLauncher;
        private Rigidbody2D body;
        private Vector2 repositionOrigin;
        private float repositionStartedAt;
        private float nextRepositionReportTime;
        private bool wasAttacking;
        private bool wasDefending;
        private bool wasEvading;
        private bool wasUsingSpecial;

        public event Action<PlayerActionObservation> MeaningfulAction;

        private void Awake()
        {
            attackState = GetComponent<PlayerAttackState>();
            playerController = GetComponent<PlayerController>();
            dash = GetComponent<PlayerDash>();
            groundSmash = GetComponent<PlayerFlightBash>();
            projectileLauncher = GetComponent<PlayerProjectileLauncher>();
            body = GetComponent<Rigidbody2D>();
            ResetTracking();
        }

        private void OnEnable()
        {
            if (projectileLauncher == null)
            {
                projectileLauncher = GetComponent<PlayerProjectileLauncher>();
            }

            if (projectileLauncher != null)
            {
                projectileLauncher.ProjectileFired -= HandleProjectileFired;
                projectileLauncher.ProjectileFired += HandleProjectileFired;
            }

            BindJumpSource();

            ResetTracking();
        }

        private void Update()
        {
            RefreshDependencies();
            bool usingSpecial = groundSmash != null && groundSmash.IsGroundSmashing;
            bool evading = !usingSpecial
                && ((dash != null && dash.IsDashing)
                    || (attackState != null && attackState.IsSlipping));
            bool attacking = !usingSpecial
                && attackState != null
                && attackState.IsActivelyAttacking;
            bool defending = attackState != null && attackState.IsDefending;

            if (usingSpecial && !wasUsingSpecial)
            {
                Report(PlayerActionCategory.Special);
            }
            else if (evading && !wasEvading)
            {
                Report(PlayerActionCategory.Evade);
            }
            else if (attacking && !wasAttacking)
            {
                Report(PlayerActionCategory.Attack);
            }
            else if (defending && !wasDefending)
            {
                Report(PlayerActionCategory.Defend);
            }

            wasUsingSpecial = usingSpecial;
            wasEvading = evading;
            wasAttacking = attacking;
            wasDefending = defending;

            UpdateReposition(attacking || defending || evading || usingSpecial);
        }

        private void UpdateReposition(bool committedAction)
        {
            Vector2 position = transform.position;
            bool movingHorizontally = body != null && Mathf.Abs(body.velocity.x) > 0.25f;
            if (committedAction || !movingHorizontally)
            {
                repositionOrigin = position;
                repositionStartedAt = Time.time;
                return;
            }

            if (Time.time < nextRepositionReportTime
                || Time.time - repositionStartedAt < repositionMinimumDuration
                || Mathf.Abs(position.x - repositionOrigin.x) < repositionDistance)
            {
                return;
            }

            Report(
                PlayerActionCategory.Reposition,
                Mathf.Sign(position.x - repositionOrigin.x));
            nextRepositionReportTime = Time.time + repositionReportCooldown;
            repositionOrigin = position;
            repositionStartedAt = Time.time;
        }

        private void HandleProjectileFired(Vector2 direction)
        {
            Report(PlayerActionCategory.Projectile, direction.x);
        }

        private void HandleJumped()
        {
            Report(PlayerActionCategory.Reposition);
        }

        private void Report(PlayerActionCategory category, float direction = 0f)
        {
            if (Mathf.Abs(direction) <= 0.01f && body != null)
            {
                direction = body.velocity.x;
            }

            MeaningfulAction?.Invoke(new PlayerActionObservation(category, direction));
            repositionOrigin = transform.position;
            repositionStartedAt = Time.time;
        }

        private void RefreshDependencies()
        {
            attackState = attackState != null ? attackState : GetComponent<PlayerAttackState>();
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
                BindJumpSource();
            }

            dash = dash != null ? dash : GetComponent<PlayerDash>();
            groundSmash = groundSmash != null ? groundSmash : GetComponent<PlayerFlightBash>();
            body = body != null ? body : GetComponent<Rigidbody2D>();
            if (projectileLauncher == null)
            {
                projectileLauncher = GetComponent<PlayerProjectileLauncher>();
                if (projectileLauncher != null)
                {
                    projectileLauncher.ProjectileFired -= HandleProjectileFired;
                    projectileLauncher.ProjectileFired += HandleProjectileFired;
                }
            }
        }

        private void BindJumpSource()
        {
            if (playerController == null)
            {
                return;
            }

            playerController.Jumped -= HandleJumped;
            playerController.Jumped += HandleJumped;
        }

        private void ResetTracking()
        {
            repositionOrigin = transform.position;
            repositionStartedAt = Time.time;
            nextRepositionReportTime = 0f;
            wasAttacking = attackState != null && attackState.IsActivelyAttacking;
            wasDefending = attackState != null && attackState.IsDefending;
            wasEvading = (dash != null && dash.IsDashing)
                || (attackState != null && attackState.IsSlipping);
            wasUsingSpecial = groundSmash != null && groundSmash.IsGroundSmashing;
        }

        private void OnDisable()
        {
            if (projectileLauncher != null)
            {
                projectileLauncher.ProjectileFired -= HandleProjectileFired;
            }

            if (playerController != null)
            {
                playerController.Jumped -= HandleJumped;
            }
        }
    }
}
