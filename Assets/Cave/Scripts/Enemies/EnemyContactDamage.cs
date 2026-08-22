using System.Collections;
using System.Collections.Generic;
using Cave.Audio;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public sealed class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField, Min(1)] private int contactDamage = 1;
        [SerializeField, Min(0f)] private float contactCooldown = 0.75f;
        [SerializeField] private DamageTrait damageTraits = DamageTrait.Direct;

        [Header("Readable Melee Windup")]
        [SerializeField, Min(0f)] private float normalAttackWindup = 0.18f;
        [SerializeField] private Color normalTelegraphColor = new Color(0.35f, 0.7f, 1f, 1f);

        [Header("Optional Feint")]
        [SerializeField, Range(0f, 1f)] private float feintChance;
        [SerializeField, Min(0f)] private float feintDelay = 0.3f;
        [SerializeField] private Color feintCueColor = new Color(0.75f, 0.75f, 0.75f, 1f);

        [Header("Optional Guard Break")]
        [SerializeField, Range(0f, 1f)] private float guardBreakChance;
        [SerializeField, Min(0f)] private float guardBreakWindup = 0.55f;
        [SerializeField, Min(1)] private int guardBreakDamage = 2;
        [SerializeField] private Color guardBreakTelegraphColor = new Color(1f, 0.2f, 0.12f, 1f);

        private float nextDamageTime;
        private int runtimeContactDamage;
        private int runtimeGuardBreakDamage;
        private float archetypeDamageMultiplier = 1f;
        private readonly Dictionary<PlayerHealth, int> contactCounts = new Dictionary<PlayerHealth, int>();
        private SpriteRenderer[] feedbackRenderers;
        private Color[] restingColors;
        private Transform feedbackRoot;
        private Vector3 restingScale;
        private Coroutine attackRoutine;

        public int BaseContactDamage => contactDamage;
        public DamageTrait DamageTraits => damageTraits;

        private void Awake()
        {
            runtimeContactDamage = contactDamage;
            runtimeGuardBreakDamage = guardBreakDamage;
            Damageable owner = GetComponentInParent<Damageable>();
            feedbackRoot = owner != null ? owner.transform : transform;
            feedbackRenderers = feedbackRoot.GetComponentsInChildren<SpriteRenderer>(true);
            restingColors = new Color[feedbackRenderers.Length];
            for (int index = 0; index < feedbackRenderers.Length; index++)
            {
                restingColors[index] = feedbackRenderers[index].color;
            }

            restingScale = feedbackRoot.localScale;
        }

        public void SetRuntimeDamage(int damage)
        {
            runtimeContactDamage = Mathf.Max(1, damage);
            float difficultyRatio = runtimeContactDamage / (float)Mathf.Max(1, contactDamage);
            runtimeGuardBreakDamage = Mathf.Max(
                1,
                Mathf.RoundToInt(guardBreakDamage * difficultyRatio));
        }

        public void SetArchetypeDamageMultiplier(float multiplier)
        {
            archetypeDamageMultiplier = Mathf.Max(0f, multiplier);
        }

        public void AddRuntimeDamageTraits(DamageTrait traits)
        {
            damageTraits |= traits;
        }

        public void ConfigureGuardBreak(float chance, float windup, int damage)
        {
            guardBreakChance = Mathf.Clamp01(chance);
            guardBreakWindup = Mathf.Max(0f, windup);
            guardBreakDamage = Mathf.Max(1, damage);
            runtimeGuardBreakDamage = guardBreakDamage;
        }

        private void OnEnable()
        {
            nextDamageTime = 0f;
            contactCounts.Clear();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            PlayerHealth playerHealth = collision.collider.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
            {
                return;
            }

            contactCounts.TryGetValue(playerHealth, out int count);
            contactCounts[playerHealth] = count + 1;
            TryBeginAttack(playerHealth);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            PlayerHealth playerHealth = collision.collider.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                TryBeginAttack(playerHealth);
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            PlayerHealth playerHealth = collision.collider.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null || !contactCounts.TryGetValue(playerHealth, out int count))
            {
                return;
            }

            if (count > 1)
            {
                contactCounts[playerHealth] = count - 1;
            }
            else
            {
                contactCounts.Remove(playerHealth);
            }
        }

        private void TryBeginAttack(PlayerHealth target)
        {
            if (attackRoutine != null || Time.time < nextDamageTime || !IsStillTouching(target))
            {
                return;
            }

            attackRoutine = StartCoroutine(PerformAttack(target, ChooseAttackIntent()));
        }

        private IEnumerator PerformAttack(PlayerHealth target, MeleeAttackIntent intent)
        {
            if (intent == MeleeAttackIntent.Feint)
            {
                ShowTelegraph(normalTelegraphColor, 1.06f);
                yield return new WaitForSeconds(normalAttackWindup * 0.65f);
                ShowTelegraph(feintCueColor, 0.94f);
                yield return new WaitForSeconds(feintDelay);
                ShowTelegraph(normalTelegraphColor, 1.06f);
                yield return new WaitForSeconds(normalAttackWindup);
                DealDamageIfTouching(target, false);
            }
            else if (intent == MeleeAttackIntent.GuardBreak)
            {
                ShowTelegraph(guardBreakTelegraphColor, 1.14f);
                yield return new WaitForSeconds(guardBreakWindup);
                DealDamageIfTouching(target, true);
            }
            else
            {
                ShowTelegraph(normalTelegraphColor, 1.06f);
                yield return new WaitForSeconds(normalAttackWindup);
                DealDamageIfTouching(target, false);
            }

            RestoreFeedback();
            nextDamageTime = Time.time + contactCooldown;
            attackRoutine = null;
        }

        private MeleeAttackIntent ChooseAttackIntent()
        {
            if (guardBreakChance > 0f && Random.value < guardBreakChance)
            {
                return MeleeAttackIntent.GuardBreak;
            }

            return feintChance > 0f && Random.value < feintChance
                ? MeleeAttackIntent.Feint
                : MeleeAttackIntent.Normal;
        }

        private void DealDamageIfTouching(PlayerHealth target, bool isGuardBreak)
        {
            if (!IsStillTouching(target))
            {
                return;
            }

            int baseDamage = isGuardBreak ? runtimeGuardBreakDamage : runtimeContactDamage;
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * archetypeDamageMultiplier));
            EnemyDamageModifiers modifiers = GetComponentInParent<EnemyDamageModifiers>();
            if (modifiers != null)
            {
                damage = modifiers.ResolveDamage(damage);
            }
            DamageTrait traits = damageTraits | DamageTrait.Direct | DamageTrait.Melee;
            if (isGuardBreak)
            {
                traits |= DamageTrait.GuardBreak;
            }

            CaveSfx.Play(CaveSfxCue.Whoosh, isGuardBreak ? 0.85f : 0.55f);
            target.TryTakeDamage(damage, new DamageContext(gameObject, traits));
        }

        private bool IsStillTouching(PlayerHealth target)
        {
            return target != null
                && target.gameObject.activeInHierarchy
                && contactCounts.TryGetValue(target, out int count)
                && count > 0;
        }

        private void ShowTelegraph(Color color, float scale)
        {
            feedbackRoot.localScale = restingScale * scale;
            for (int index = 0; index < feedbackRenderers.Length; index++)
            {
                if (feedbackRenderers[index] != null)
                {
                    feedbackRenderers[index].color = color;
                }
            }
        }

        private void RestoreFeedback()
        {
            if (feedbackRoot != null)
            {
                feedbackRoot.localScale = restingScale;
            }

            for (int index = 0; index < feedbackRenderers.Length; index++)
            {
                if (feedbackRenderers[index] != null)
                {
                    feedbackRenderers[index].color = restingColors[index];
                }
            }
        }

        private void OnDisable()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            contactCounts.Clear();
            RestoreFeedback();
        }

        private enum MeleeAttackIntent
        {
            Normal,
            Feint,
            GuardBreak
        }
    }
}
