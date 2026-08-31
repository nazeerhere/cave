using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class WizardHealingCorruption : MonoBehaviour
    {
        [Header("Current Life (Read Only)")]
        [SerializeField, Min(0f)] private float normalizedHealthRestored;
        [SerializeField, Range(0, 4)] private int currentStage;
        [SerializeField, Range(0f, 1f)] private float currentPermanentSlow;

        private Damageable damageable;
        private GameObject stageVfx;

        public float NormalizedHealthRestored => normalizedHealthRestored;
        public int CurrentStage => currentStage;
        public float CurrentPermanentSlow => currentPermanentSlow;

        public bool TryTransformToAuthoredPrefabWhenFullyCorrupted()
        {
            if (damageable == null || currentStage < 4 || damageable.MaximumHealth <= 0)
            {
                return false;
            }

            EnemyCorruptionLifecycle lifecycle =
                GetComponent<EnemyCorruptionLifecycle>();
            return lifecycle != null && lifecycle.TryTransformIntoAuthoredPrefabForWizardCorruption(
                damageable.CurrentHealth / (float)damageable.MaximumHealth,
                currentStage,
                currentPermanentSlow);
        }

        public void ReceiveTransferredState(int stage, float permanentSlow)
        {
            currentStage = Mathf.Clamp(stage, 0, 4);
            currentPermanentSlow = Mathf.Clamp01(permanentSlow);
            normalizedHealthRestored = currentStage;
            ApplyMovementMultiplier(1f - currentPermanentSlow);
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
        }

        public void RecordActualHealing(
            int actualHealthRestored,
            Vector4 stageThresholds,
            Vector4 stageSlowPercentages,
            Color extremeTint,
            float maximumSlow,
            GameObject corruptionStageVfxPrefab)
        {
            if (actualHealthRestored <= 0 || damageable == null || damageable.MaximumHealth <= 0)
            {
                return;
            }

            normalizedHealthRestored += actualHealthRestored
                / (float)Mathf.Max(1, damageable.MaximumHealth);
            int stage = ResolveStage(normalizedHealthRestored, stageThresholds);
            float requestedSlow = ResolveStageValue(stage, stageSlowPercentages);
            currentStage = stage;
            currentPermanentSlow = Mathf.Clamp(requestedSlow, 0f, Mathf.Clamp01(maximumSlow));
            ApplyMovementMultiplier(1f - currentPermanentSlow);

            float tintProgress = stage <= 0 ? 0f : stage / 4f;
            damageable.SetPersistentTint(Color.Lerp(Color.white, extremeTint, tintProgress));
            if (stage > 0 && stageVfx == null && corruptionStageVfxPrefab != null)
            {
                stageVfx = Instantiate(
                    corruptionStageVfxPrefab,
                    transform.position,
                    Quaternion.identity,
                    transform);
                stageVfx.transform.localPosition = Vector3.zero;
            }
        }

        private static int ResolveStage(float accumulation, Vector4 thresholds)
        {
            if (accumulation >= thresholds.w)
            {
                return 4;
            }

            if (accumulation >= thresholds.z)
            {
                return 3;
            }

            if (accumulation >= thresholds.y)
            {
                return 2;
            }

            return accumulation >= thresholds.x ? 1 : 0;
        }

        private static float ResolveStageValue(int stage, Vector4 values)
        {
            switch (stage)
            {
                case 1:
                    return values.x;
                case 2:
                    return values.y;
                case 3:
                    return values.z;
                case 4:
                    return values.w;
                default:
                    return 0f;
            }
        }

        private void ApplyMovementMultiplier(float multiplier)
        {
            GetComponent<EnemyController>()?.SetCorruptionSpeedMultiplier(multiplier);
            GetComponent<FlyingSwarmController>()?.SetCorruptionSpeedMultiplier(multiplier);
            GetComponent<WizardFlightMotor>()?.SetCorruptionSpeedMultiplier(multiplier);
        }

        private void ResetLifeState()
        {
            normalizedHealthRestored = 0f;
            currentStage = 0;
            currentPermanentSlow = 0f;
            ApplyMovementMultiplier(1f);
            damageable?.SetPersistentTint(Color.white);
            if (stageVfx != null)
            {
                Destroy(stageVfx);
                stageVfx = null;
            }
        }

        private void OnDisable()
        {
            ResetLifeState();
        }
    }
}
