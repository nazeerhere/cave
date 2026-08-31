using System.Collections;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum LightBanditAction
    {
        None,
        Backstep,
        Sidestep,
        DashSlashWindup,
        DashBashWindup,
        DashFeint,
        DashSlash,
        DashBash
    }

    /// <summary>Short, readable movement commitments for the Light Bandit's skirmisher kit.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController), typeof(Rigidbody2D))]
    public sealed class LightBanditCombat : MonoBehaviour
    {
        [Header("Evasion")]
        [SerializeField, Min(0.05f)] private float evadeDuration = 0.16f;
        [SerializeField, Min(0.1f)] private float backstepSpeed = 7f;
        [SerializeField, Min(0.1f)] private float sidestepSpeed = 6f;
        [SerializeField, Min(0f)] private float evadeCooldown = 0.75f;

        [Header("Pursuit Dash")]
        [SerializeField, Min(0.05f)] private float dashStartup = 0.16f;
        [SerializeField, Min(0.05f)] private float dashCommitDuration = 0.26f;
        [SerializeField, Min(0.1f)] private float dashSpeed = 10f;
        [SerializeField, Min(0.1f)] private float dashSlashRange = 1.15f;
        [SerializeField, Min(1)] private int dashSlashDamage = 1;
        [SerializeField, Min(1)] private int dashBashDamage = 1;
        [SerializeField, Min(0.05f)] private float dashRecovery = 0.42f;
        [SerializeField, Min(0f)] private float sharedDashCooldown = 2f;
        [SerializeField, Range(0f, 1f)] private float dashFeintChance = 0.18f;

        [Header("Runtime (Read Only)")]
        [SerializeField] private bool isBusy;
        [SerializeField] private LightBanditAction currentAction;

        private EnemyController movement;
        private Coroutine routine;
        private float nextEvadeAt;
        private float nextDashAt;

        public bool IsBusy => isBusy;
        public LightBanditAction CurrentAction => currentAction;
        public bool CanEvade => !isBusy && Time.time >= nextEvadeAt;
        public bool CanDash => !isBusy && Time.time >= nextDashAt;

        private void Awake()
        {
            movement = GetComponent<EnemyController>();
        }

        public bool TryBackstep(PlayerHealth target)
        {
            return TryEvade(target, true);
        }

        public bool TrySidestep(PlayerHealth target)
        {
            return TryEvade(target, false);
        }

        public bool TryPursuitDash(PlayerHealth target, bool bash)
        {
            if (!CanDash || target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            routine = StartCoroutine(PerformDash(target, bash));
            return true;
        }

        private bool TryEvade(PlayerHealth target, bool backstep)
        {
            if (!CanEvade || target == null)
            {
                return false;
            }

            float fromPlayer = Mathf.Sign(transform.position.x - target.transform.position.x);
            if (Mathf.Approximately(fromPlayer, 0f))
            {
                fromPlayer = Random.value < 0.5f ? -1f : 1f;
            }

            // A sidestep changes flank direction rather than always retreating.
            float direction = backstep ? fromPlayer : -fromPlayer;
            routine = StartCoroutine(PerformEvade(direction, backstep));
            return true;
        }

        private IEnumerator PerformEvade(float direction, bool backstep)
        {
            isBusy = true;
            currentAction = backstep ? LightBanditAction.Backstep : LightBanditAction.Sidestep;
            movement.BeginCommittedMovement(
                direction,
                backstep ? backstepSpeed : sidestepSpeed,
                evadeDuration);
            yield return new WaitForSeconds(evadeDuration);
            movement.CancelCommittedMovement();
            nextEvadeAt = Time.time + evadeCooldown;
            isBusy = false;
            currentAction = LightBanditAction.None;
            routine = null;
        }

        private IEnumerator PerformDash(PlayerHealth target, bool bash)
        {
            isBusy = true;
            currentAction = bash ? LightBanditAction.DashBashWindup : LightBanditAction.DashSlashWindup;
            yield return new WaitForSeconds(dashStartup);

            if (target == null || !target.gameObject.activeInHierarchy)
            {
                EndDash();
                yield break;
            }

            // Feints are decided before commitment. After this point the dash is
            // intentionally not allowed to react to late player input.
            if (Random.value < dashFeintChance)
            {
                currentAction = LightBanditAction.DashFeint;
                float away = Mathf.Sign(transform.position.x - target.transform.position.x);
                movement.BeginCommittedMovement(away == 0f ? 1f : away, backstepSpeed, evadeDuration);
                yield return new WaitForSeconds(evadeDuration);
                EndDash();
                yield break;
            }

            float direction = Mathf.Sign(target.transform.position.x - transform.position.x);
            movement.BeginCommittedMovement(direction == 0f ? 1f : direction, dashSpeed, dashCommitDuration);
            currentAction = bash ? LightBanditAction.DashBash : LightBanditAction.DashSlash;
            yield return new WaitForSeconds(dashCommitDuration);
            movement.CancelCommittedMovement();

            if (target != null && target.gameObject.activeInHierarchy
                && Mathf.Abs(target.transform.position.x - transform.position.x) <= dashSlashRange)
            {
                DamageTrait traits = DamageTrait.Melee;
                if (bash)
                {
                    traits |= DamageTrait.GuardBreak;
                }

                target.TryTakeDamage(bash ? dashBashDamage : dashSlashDamage, new DamageContext(gameObject, traits));
            }

            yield return new WaitForSeconds(dashRecovery);
            EndDash();
        }

        private void EndDash()
        {
            movement?.CancelCommittedMovement();
            nextDashAt = Time.time + sharedDashCooldown;
            isBusy = false;
            currentAction = LightBanditAction.None;
            routine = null;
        }

        private void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            movement?.CancelCommittedMovement();
            isBusy = false;
            currentAction = LightBanditAction.None;
        }
    }
}
