using System.Collections;
using Cave.Audio;
using Cave.Combat;
using UnityEngine;

namespace Cave.Player
{
    [RequireComponent(typeof(PlayerHealth), typeof(HitFlash))]
    public sealed class PlayerHitFeedback : MonoBehaviour
    {
        [Header("Damage Pulse")]
        [SerializeField, Min(0.01f)] private float pulseDuration = 0.12f;
        [SerializeField, Min(1f)] private float pulseScale = 1.08f;
        [SerializeField, Min(0.1f)] private float pulseRadius = 0.7f;
        [SerializeField] private Color pulseColor = new Color(1f, 0.18f, 0.12f, 0.9f);

        private PlayerHealth playerHealth;
        private HitFlash hitFlash;
        private Vector3 restingScale;
        private Coroutine pulseRoutine;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            hitFlash = GetComponent<HitFlash>();
            restingScale = transform.localScale;
        }

        private void OnEnable()
        {
            playerHealth.DamageTaken += HandleDamageTaken;
            playerHealth.Respawned += HandleRespawned;
        }

        private void OnDisable()
        {
            playerHealth.DamageTaken -= HandleDamageTaken;
            playerHealth.Respawned -= HandleRespawned;
            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
                pulseRoutine = null;
            }

            transform.localScale = restingScale;
        }

        private void HandleDamageTaken()
        {
            hitFlash.Play();
            CaveSfx.Play(CaveSfxCue.Hit, 0.9f);
            AreaPulseEffect.Create(transform.position, pulseRadius, pulseColor, pulseDuration);
            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
            }

            pulseRoutine = StartCoroutine(PlayScalePulse());
        }

        private void HandleRespawned()
        {
            CaveSfx.Play(CaveSfxCue.Spawn, 0.8f);
        }

        private IEnumerator PlayScalePulse()
        {
            transform.localScale = Vector3.Scale(
                restingScale,
                new Vector3(pulseScale, pulseScale, 1f));
            yield return new WaitForSeconds(pulseDuration);
            transform.localScale = restingScale;
            pulseRoutine = null;
        }
    }
}
