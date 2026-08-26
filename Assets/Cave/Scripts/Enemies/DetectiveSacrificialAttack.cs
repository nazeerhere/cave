using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class DetectiveSacrificialAttack : MonoBehaviour, IEnemyInterruptible
    {
        [SerializeField, Min(0.1f)] private float commitmentRange = 1.25f;
        [SerializeField, Min(0.1f)] private float blastRadius = 1.8f;
        [SerializeField, Min(1)] private int playerDamage = 2;
        [SerializeField, Min(0f)] private float playerStaggerDuration = 0.8f;
        [SerializeField, Min(1)] private int poisonDamage = 1;
        [SerializeField, Min(0.1f)] private float poisonTickInterval = 1f;
        [SerializeField, Min(0.1f)] private float poisonDuration = 3f;
        [SerializeField, Range(0.5f, 0.8f)] private float finalTelegraph = 0.65f;
        [SerializeField, Min(0f)] private float interruptedRecovery = 1f;
        [SerializeField] private Color telegraphColor = new Color(0.65f, 1f, 0.15f, 0.95f);

        private Damageable damageable;
        private PlayerHealth target;
        private float detonatesAt;
        private float recoveringUntil;
        private bool committed;

        public float CommitmentRange => commitmentRange;
        public bool IsBusy => committed || Time.time < recoveringUntil;
        public bool IsCommitted => committed;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
        }

        private void Update()
        {
            if (committed && Time.time >= detonatesAt)
            {
                Detonate();
            }
        }

        public bool TryCommit(PlayerHealth player)
        {
            if (committed
                || Time.time < recoveringUntil
                || player == null
                || !player.gameObject.activeInHierarchy
                || Vector2.Distance(transform.position, player.transform.position) > commitmentRange)
            {
                return false;
            }

            target = player;
            committed = true;
            detonatesAt = Time.time + finalTelegraph;
            CombatShapeEffect.Create(
                transform.position,
                CombatShape.Ring,
                blastRadius,
                telegraphColor,
                finalTelegraph,
                0f,
                0.12f);
            return true;
        }

        private void Detonate()
        {
            committed = false;
            PlayerHealth resolvedTarget = target != null ? target : FindObjectOfType<PlayerHealth>();
            if (resolvedTarget != null
                && Vector2.Distance(transform.position, resolvedTarget.transform.position)
                    <= blastRadius)
            {
                resolvedTarget.TryTakeDamage(
                    playerDamage,
                    new DamageContext(gameObject, DamageTrait.AreaOfEffect));
                resolvedTarget.GetComponent<PlayerGuardBreak>()
                    ?.ApplyPlayerStagger(playerStaggerDuration);
                PlayerPoisonStatus poison = resolvedTarget.GetComponent<PlayerPoisonStatus>();
                if (poison == null)
                {
                    poison = resolvedTarget.gameObject.AddComponent<PlayerPoisonStatus>();
                }

                poison.ApplyPoison(
                    poisonDamage,
                    poisonTickInterval,
                    poisonDuration,
                    gameObject);
            }

            CombatShapeEffect.Create(
                transform.position,
                CombatShape.Ring,
                blastRadius,
                telegraphColor,
                0.35f,
                0f,
                0.18f);
            target = null;
            if (damageable != null && damageable.CurrentHealth > 0)
            {
                damageable.TakeDamage(
                    damageable.CurrentHealth,
                    new DamageContext(gameObject, DamageTrait.AreaOfEffect));
            }
        }

        public void Interrupt()
        {
            if (!committed)
            {
                return;
            }

            committed = false;
            target = null;
            recoveringUntil = Time.time + interruptedRecovery;
        }

        private void OnDisable()
        {
            committed = false;
            target = null;
            recoveringUntil = 0f;
        }
    }
}
