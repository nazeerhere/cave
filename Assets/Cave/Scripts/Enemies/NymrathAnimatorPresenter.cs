using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Presentation follows confirmed gameplay lifecycle events; no
    /// Animator transition controls any Nymrath gameplay decision.</summary>
    [RequireComponent(typeof(NymrathCore), typeof(Animator))]
    public sealed class NymrathAnimatorPresenter : MonoBehaviour
    {
        private NymrathCore core;
        private Animator animator;
        private Damageable damageable;
        private EnemyStagger stagger;
        private Rigidbody2D body;

        private void Awake()
        {
            core = GetComponent<NymrathCore>();
            animator = GetComponent<Animator>();
            damageable = GetComponent<Damageable>();
            stagger = GetComponent<EnemyStagger>();
            body = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            if (core == null) core = GetComponent<NymrathCore>();
            if (core == null) return;
            core.RestorationWeaveStarted += PlayRestoration;
            core.RestorationWeaveCompleted += PlayIdle;
            core.LivingBastionActivated += PlayBastion;
            core.BurrowEntered += PlayBurrowEntry;
            core.BurrowEmerged += PlayBurrowEmergence;
            core.BurrowAmbushResolved += PlayBurrowAmbush;
            core.TendrilLashStarted += PlayTendril;
            core.EternalBloomStarted += PlayBloom;
            core.PreservationPrisonApplied += PlayPrison;
            core.SpectralStampedeCommitted += PlayStampede;
            if (damageable == null) damageable = GetComponent<Damageable>();
            if (stagger == null) stagger = GetComponent<EnemyStagger>();
            if (damageable != null) damageable.Died += PlayDeath;
            if (stagger != null) stagger.Staggered += PlayHit;
        }

        private void OnDisable()
        {
            if (core == null) return;
            core.RestorationWeaveStarted -= PlayRestoration;
            core.RestorationWeaveCompleted -= PlayIdle;
            core.LivingBastionActivated -= PlayBastion;
            core.BurrowEntered -= PlayBurrowEntry;
            core.BurrowEmerged -= PlayBurrowEmergence;
            core.BurrowAmbushResolved -= PlayBurrowAmbush;
            core.TendrilLashStarted -= PlayTendril;
            core.EternalBloomStarted -= PlayBloom;
            core.PreservationPrisonApplied -= PlayPrison;
            core.SpectralStampedeCommitted -= PlayStampede;
            if (damageable != null) damageable.Died -= PlayDeath;
            if (stagger != null) stagger.Staggered -= PlayHit;
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            if (body == null) body = GetComponent<Rigidbody2D>();
            animator.SetBool("Moving", body != null && (core == null || !core.IsBusy) && Mathf.Abs(body.velocity.x) > .01f);
        }

        private void PlayRestoration() { Trigger("RestorationWeave"); }
        private void PlayBastion() { Trigger("LivingBastion"); }
        private void PlayBurrowEntry() { Trigger("BurrowEntry"); }
        private void PlayBurrowEmergence() { Trigger("BurrowEmergence"); }
        private void PlayBurrowAmbush() { Trigger("BurrowAmbush"); }
        private void PlayTendril() { Trigger("TendrilLash"); }
        private void PlayBloom() { Trigger("EternalBloom"); }
        private void PlayPrison() { Trigger("PreservationPrison"); }
        private void PlayStampede() { Trigger("SpectralStampede"); }
        private void PlayHit(StaggerStrength _, float __) { Trigger("Hit"); }
        private void PlayDeath() { Trigger("Death"); }
        private void PlayIdle()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Play("Idle", 0, 0f);
            }
        }

        private void Trigger(string trigger)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger(trigger);
            }
        }
    }
}
