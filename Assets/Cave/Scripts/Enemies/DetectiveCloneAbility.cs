using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class DetectiveCloneAbility : MonoBehaviour,
        IEnemyInterruptible,
        IEnemySkillEvolutionReceiver
    {
        [Header("Evolution / Timing")]
        [SerializeField] private EnemyEvolutionStage unlockStage =
            EnemyEvolutionStage.EvolutionOne;
        [SerializeField, Min(0f)] private float cloneCooldown = 25f;
        [SerializeField, Min(0f)] private float creationWindup = 0.35f;
        [SerializeField, Min(0f)] private float creationRecovery = 0.5f;
        [SerializeField, Min(0.1f)] private float spawnOffset = 0.9f;

        [Header("Current State (Read Only)")]
        [SerializeField] private EnemyEvolutionStage currentEvolution;
        [SerializeField] private bool isCreatingClone;

        private DetectiveIdentity identity;
        private EnemyStagger stagger;
        private float completesAt;
        private float recoveringUntil;
        private float nextUseTime;
        private bool slotReserved;

        public bool IsBusy => isCreatingClone || Time.time < recoveringUntil;
        public bool IsUnlocked => currentEvolution >= unlockStage;
        public bool IsReady => !IsBusy
            && Time.time >= nextUseTime
            && IsUnlocked
            && identity != null
            && identity.IsRealDetective
            && identity.CanAct
            && (stagger == null || stagger.CanAct);

        private void Awake()
        {
            identity = GetComponent<DetectiveIdentity>();
            stagger = GetComponent<EnemyStagger>();
        }

        private void Update()
        {
            if (isCreatingClone && Time.time >= completesAt)
            {
                CompleteClone();
            }
        }

        public bool TryUse()
        {
            if (identity == null)
            {
                identity = GetComponent<DetectiveIdentity>();
            }

            DetectiveEncounterCoordinator coordinator = identity != null
                ? identity.Coordinator
                : null;
            if (!IsReady || coordinator == null || !coordinator.TryReserveCloneSlot())
            {
                return false;
            }

            slotReserved = true;
            isCreatingClone = true;
            completesAt = Time.time + creationWindup;
            CombatShapeEffect.Create(
                transform.position + Vector3.up * 0.35f,
                CombatShape.Diamond,
                0.7f,
                new Color(0.25f, 0.85f, 1f, 0.7f),
                Mathf.Max(0.15f, creationWindup));
            return true;
        }

        private void CompleteClone()
        {
            isCreatingClone = false;
            DetectiveEncounterCoordinator coordinator = identity.Coordinator;
            GameObject cloneObject = new GameObject("Detective Clone");
            cloneObject.SetActive(false);
            cloneObject.transform.position = transform.position
                + Vector3.right * (Random.value < 0.5f ? -spawnOffset : spawnOffset);
            int damageableLayer = LayerMask.NameToLayer("Damageable");
            if (damageableLayer >= 0)
            {
                cloneObject.layer = damageableLayer;
            }

            CopyPresentation(cloneObject);
            Rigidbody2D body = cloneObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            BoxCollider2D collider = cloneObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.6f, 1.2f);
            collider.offset = new Vector2(0f, 0.6f);
            cloneObject.AddComponent<Damageable>();
            cloneObject.AddComponent<EnemyArchetypeProfile>()
                .AddRuntimeArchetype(EnemyArchetype.Ranged);
            cloneObject.AddComponent<EnemyController>();
            DetectiveIdentity cloneIdentity = cloneObject.AddComponent<DetectiveIdentity>();
            cloneIdentity.ConfigureClone(coordinator);
            cloneObject.AddComponent<DetectiveCloneMimic>();
            cloneObject.SetActive(true);

            slotReserved = false;
            recoveringUntil = Time.time + creationRecovery;
            nextUseTime = Time.time + cloneCooldown;
        }

        private void CopyPresentation(GameObject cloneObject)
        {
            SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
            if (source != null)
            {
                SpriteRenderer copy = cloneObject.AddComponent<SpriteRenderer>();
                copy.sprite = source.sprite;
                copy.sharedMaterial = source.sharedMaterial;
                copy.flipX = source.flipX;
                copy.sortingLayerID = source.sortingLayerID;
                copy.sortingOrder = source.sortingOrder;
                copy.color = source.color;
            }

            Animator sourceAnimator = GetComponentInChildren<Animator>();
            if (sourceAnimator != null && sourceAnimator.runtimeAnimatorController != null)
            {
                Animator copyAnimator = cloneObject.AddComponent<Animator>();
                copyAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            }
        }

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            currentEvolution = stage;
        }

        public void Interrupt()
        {
            if (!isCreatingClone)
            {
                return;
            }

            isCreatingClone = false;
            ReleaseReservation();
            recoveringUntil = Time.time + creationRecovery;
            nextUseTime = Mathf.Max(nextUseTime, Time.time + cloneCooldown * 0.35f);
        }

        private void ReleaseReservation()
        {
            if (!slotReserved)
            {
                return;
            }

            identity?.Coordinator?.ReleaseReservedCloneSlot();
            slotReserved = false;
        }

        private void OnDisable()
        {
            isCreatingClone = false;
            ReleaseReservation();
        }
    }
}
