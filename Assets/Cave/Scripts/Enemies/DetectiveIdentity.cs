using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    public enum DetectiveIntent
    {
        Fighter,
        Builder,
        Researcher,
        Interceptor,
        Bait,
        Cover,
        Sacrificial
    }

    [DisallowMultipleComponent]
    public sealed class DetectiveIdentity : MonoBehaviour
    {
        [Header("Identity (Read Only)")]
        [SerializeField] private bool isClone;
        [SerializeField] private GameObject specialistOwner;
        [SerializeField] private DetectiveEncounterCoordinator coordinator;

        private bool registered;

        public bool IsClone => isClone;
        public bool IsRealDetective => !isClone;
        public GameObject SpecialistOwner => specialistOwner;
        public DetectiveEncounterCoordinator Coordinator => coordinator;
        public Damageable Damageable { get; private set; }
        public EnemyStagger Stagger { get; private set; }
        public bool CanAct => gameObject.activeInHierarchy
            && Damageable != null
            && Damageable.CurrentHealth > 0
            && (Stagger == null || Stagger.CanAct);

        private void Awake()
        {
            Damageable = GetComponent<Damageable>();
            Stagger = GetComponent<EnemyStagger>();
        }

        private void OnEnable()
        {
            Register();
        }

        public void ConfigureReal(
            GameObject owner,
            DetectiveEncounterCoordinator encounterCoordinator = null)
        {
            Reconfigure(false, owner, encounterCoordinator);
        }

        public void ConfigureClone(DetectiveEncounterCoordinator encounterCoordinator)
        {
            Reconfigure(true, null, encounterCoordinator);
        }

        private void Reconfigure(
            bool clone,
            GameObject owner,
            DetectiveEncounterCoordinator encounterCoordinator)
        {
            Unregister();
            isClone = clone;
            specialistOwner = owner;
            coordinator = encounterCoordinator;
            if (isActiveAndEnabled)
            {
                Register();
            }
        }

        private void Register()
        {
            if (registered)
            {
                return;
            }

            if (coordinator == null)
            {
                coordinator = DetectiveEncounterCoordinator.GetOrCreate();
            }

            registered = coordinator != null;
            coordinator?.Register(this);
        }

        private void Unregister()
        {
            if (!registered)
            {
                return;
            }

            coordinator?.Unregister(this);
            registered = false;
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }
    }
}
