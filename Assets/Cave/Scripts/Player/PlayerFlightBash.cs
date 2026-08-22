using System.Collections.Generic;
using Cave.Audio;
using Cave.Combat;
using Cave.Enemies;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerController), typeof(PlayerMana))]
    [RequireComponent(typeof(PlayerSpecialMode), typeof(PlayerSpecialModeUpgradeState))]
    public sealed class PlayerFlightBash : MonoBehaviour
    {
        [Header("Flight Tier 2 Bash")]
        [SerializeField, Min(0f)] private float manaCost = 20f;
        [SerializeField, Min(1)] private int damage = 2;
        [SerializeField, Min(0f)] private float speed = 20f;
        [SerializeField, Min(0.01f)] private float duration = 0.18f;
        [SerializeField, Min(0f)] private float cooldown = 0.35f;
        [SerializeField, Min(0f)] private float knockback = 12f;

        [Header("Flight Tier 3 Shockwave")]
        [SerializeField, Min(0f)] private float tier3AdditionalManaCost;
        [SerializeField, Min(0.1f)] private float shockwaveRadius = 2.5f;
        [SerializeField, Min(1)] private int shockwaveDamage = 2;
        [SerializeField, Min(0f)] private float shockwaveKnockback = 14f;

        private readonly HashSet<Damageable> hitTargets = new HashSet<Damageable>();
        private Rigidbody2D body;
        private PlayerController playerController;
        private PlayerMana playerMana;
        private PlayerSpecialMode specialMode;
        private PlayerSpecialModeUpgradeState upgrades;
        private PlayerResourceMastery resourceMastery;
        private float bashDirection;
        private float bashEndsAt;
        private float nextBashTime;
        private DamageContext damageContext;
        private PlayerDash playerDash;
        private bool shockwaveTriggered;

        public bool IsBashing { get; private set; }
        public bool ShouldHandleDashInput => specialMode != null
            && upgrades != null
            && playerMana != null
            && specialMode.CurrentMode == SpecialMode.Flight
            && upgrades.IsTier2Owned(SpecialMode.Flight)
            && playerController != null
            && !playerController.IsGrounded
            && !IsBashing
            && (playerDash == null || !playerDash.IsDashing)
            && Time.time >= nextBashTime
            && playerMana.CurrentMana >= GetCurrentManaCost();

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            playerController = GetComponent<PlayerController>();
            playerMana = GetComponent<PlayerMana>();
            specialMode = GetComponent<PlayerSpecialMode>();
            upgrades = GetComponent<PlayerSpecialModeUpgradeState>();
            resourceMastery = GetComponent<PlayerResourceMastery>();
            playerDash = GetComponent<PlayerDash>();
        }

        internal void Configure(SpecialModeTier2Settings settings)
        {
            if (settings == null)
            {
                return;
            }

            manaCost = settings.BashManaCost;
            damage = settings.BashDamage;
            speed = settings.BashSpeed;
            duration = settings.BashDuration;
            cooldown = settings.BashCooldown;
            knockback = settings.BashKnockback;
            tier3AdditionalManaCost = settings.Tier3BashAdditionalManaCost;
            shockwaveRadius = settings.BashShockwaveRadius;
            shockwaveDamage = settings.BashShockwaveDamage;
            shockwaveKnockback = settings.BashShockwaveKnockback;
        }

        public bool TryStartBash(float horizontalInput, float fallbackFacingDirection)
        {
            if (!ShouldHandleDashInput
                || IsBashing
                || (playerDash != null && playerDash.IsDashing)
                || Time.time < nextBashTime
                || !playerMana.TrySpendMana(GetCurrentManaCost()))
            {
                return false;
            }

            bashDirection = Mathf.Approximately(horizontalInput, 0f)
                ? Mathf.Sign(fallbackFacingDirection)
                : Mathf.Sign(horizontalInput);
            if (Mathf.Approximately(bashDirection, 0f))
            {
                bashDirection = 1f;
            }

            if (resourceMastery == null)
            {
                resourceMastery = GetComponent<PlayerResourceMastery>();
            }

            damageContext = resourceMastery != null
                ? resourceMastery.CreateManaDamageContext()
                : default;
            hitTargets.Clear();
            shockwaveTriggered = false;
            IsBashing = true;
            bashEndsAt = Time.time + duration;
            nextBashTime = bashEndsAt + cooldown;
            body.velocity = new Vector2(bashDirection * speed, body.velocity.y);
            CaveSfx.Play(CaveSfxCue.Whoosh, 0.85f);
            return true;
        }

        private void FixedUpdate()
        {
            if (!IsBashing)
            {
                return;
            }

            if (specialMode.CurrentMode != SpecialMode.Flight)
            {
                EndBash();
                return;
            }

            if (Time.time >= bashEndsAt)
            {
                EndBash();
                return;
            }

            body.velocity = new Vector2(bashDirection * speed, body.velocity.y);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryDamageTarget(collision.collider);
            TryTriggerShockwave(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryDamageTarget(collision.collider);
            TryTriggerShockwave(collision.collider);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamageTarget(other);
            TryTriggerShockwave(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamageTarget(other);
            TryTriggerShockwave(other);
        }

        private float GetCurrentManaCost()
        {
            bool tier3Owned = upgrades != null && upgrades.IsTier3Owned(SpecialMode.Flight);
            return manaCost + (tier3Owned ? tier3AdditionalManaCost : 0f);
        }

        private void TryTriggerShockwave(Collider2D impactCollider)
        {
            if (!IsBashing
                || shockwaveTriggered
                || upgrades == null
                || !upgrades.IsTier3Owned(SpecialMode.Flight)
                || impactCollider == null)
            {
                return;
            }

            Damageable impactTarget = impactCollider.GetComponentInParent<Damageable>();
            if (impactTarget == null && impactCollider.isTrigger)
            {
                return;
            }

            shockwaveTriggered = true;
            AreaPulseEffect.Create(
                transform.position,
                shockwaveRadius,
                new Color(0.35f, 0.85f, 1f, 0.9f));

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, shockwaveRadius);
            foreach (Collider2D overlap in overlaps)
            {
                Damageable damageable = overlap.GetComponentInParent<Damageable>();
                if (damageable == null || !hitTargets.Add(damageable))
                {
                    continue;
                }

                damageable.TakeDamage(shockwaveDamage, damageContext);
                if (!damageable.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 direction = damageable.transform.position - transform.position;
                if (direction.sqrMagnitude <= 0.001f)
                {
                    direction = new Vector2(bashDirection, 0f);
                }

                damageable.GetComponent<KnockbackReceiver>()
                    ?.ApplyKnockback(direction.normalized * shockwaveKnockback);
            }
        }

        private void TryDamageTarget(Collider2D other)
        {
            if (!IsBashing || other == null)
            {
                return;
            }

            Damageable damageable = other.GetComponentInParent<Damageable>();
            if (damageable == null || !hitTargets.Add(damageable))
            {
                return;
            }

            damageable.TakeDamage(damage, damageContext);
            KnockbackReceiver receiver = damageable.GetComponent<KnockbackReceiver>();
            if (receiver != null && damageable.gameObject.activeInHierarchy)
            {
                receiver.ApplyKnockback(new Vector2(bashDirection * knockback, 0f));
            }
        }

        private void EndBash()
        {
            IsBashing = false;
            hitTargets.Clear();
            shockwaveTriggered = false;
            body.velocity = new Vector2(0f, body.velocity.y);
        }

        private void OnDisable()
        {
            if (IsBashing && body != null)
            {
                EndBash();
            }
        }
    }
}
