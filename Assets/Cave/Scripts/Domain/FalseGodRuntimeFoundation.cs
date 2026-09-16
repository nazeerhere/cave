using Cave.Combat;
using Cave.Interactions;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Minimal owner-side foundation for the future False God. It owns one
    /// Claim authority network and currently exposes only Crystal Manifestation;
    /// future boss abilities can consume the same explicit seams without a
    /// second ownership model.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionIdentity))]
    public sealed class FalseGodRuntimeFoundation : MonoBehaviour
    {
        [SerializeField, Min(1)] private int baseAuthorityStrength = 2;

        private InteractionIdentity authorityIdentity;
        private ClaimAuthorityNetwork authorityNetwork;
        private Damageable damageable;
        private bool initialized;
        private bool cleanedUp;
        private bool deathSubscribed;

        public InteractionIdentity AuthorityIdentity => authorityIdentity;
        public ClaimAuthorityNetwork AuthorityNetwork => authorityNetwork;
        public ClaimLoadSnapshot ClaimLoad => authorityNetwork != null
            ? authorityNetwork.Load
            : default;
        public int ActiveClaimAnchors => authorityNetwork != null ? authorityNetwork.ActiveAnchorCount : 0;
        public int ActiveClaimTerritories => authorityNetwork != null ? authorityNetwork.ActiveTerritoryCount : 0;

        private void Awake()
        {
            InitializeRuntime();
        }

        /// <summary>
        /// Explicit runtime-safe initialization for temporary development
        /// shells and normal MonoBehaviour lifecycle alike. It avoids editor
        /// tests invoking Unity magic methods while preserving the same owner
        /// identity/network setup used in play mode.
        /// </summary>
        public bool InitializeRuntime()
        {
            if (initialized && !cleanedUp)
            {
                return true;
            }

            authorityIdentity = InteractionRuntime.TrackSpawn(
                gameObject,
                InteractionTraits.None,
                InteractionOwnership.Claim,
                gameObject,
                false,
                baseAuthorityStrength);
            authorityNetwork = GetComponent<ClaimAuthorityNetwork>();
            if (authorityNetwork == null)
            {
                authorityNetwork = gameObject.AddComponent<ClaimAuthorityNetwork>();
            }

            authorityNetwork.InitializeRuntime(gameObject);
            damageable = GetComponent<Damageable>();
            SubscribeDeath();
            cleanedUp = false;
            initialized = authorityIdentity != null && authorityNetwork != null;
            return initialized;
        }

        private void OnEnable()
        {
            SubscribeDeath();
        }

        private void OnDisable()
        {
            UnsubscribeDeath();

            CleanupAuthority();
        }

        private void OnDestroy()
        {
            CleanupAuthority();
        }

        /// <summary>
        /// The sole implemented future-boss ability seam. It does not grant
        /// gameplay effects beyond a destructible anchor and eligible territory.
        /// </summary>
        public ClaimCrystal ManifestCrystal(
            Vector2 position,
            int authorityStrength,
            float territoryRadius,
            float lifetime = 0f)
        {
            if (!initialized || cleanedUp)
            {
                return null;
            }

            return ClaimCrystalManifestation.Manifest(new ClaimCrystalManifestationRequest(
                authorityNetwork,
                gameObject,
                position,
                Mathf.Max(baseAuthorityStrength, authorityStrength),
                territoryRadius,
                lifetime));
        }

        public void CleanupAuthority()
        {
            if (cleanedUp)
            {
                return;
            }

            cleanedUp = true;
            authorityNetwork?.DeactivateAllAuthority();
        }

        private void HandleAuthorityOwnerDied()
        {
            CleanupAuthority();
        }

        private void SubscribeDeath()
        {
            if (deathSubscribed || damageable == null)
            {
                return;
            }

            deathSubscribed = true;
            damageable.Died += HandleAuthorityOwnerDied;
        }

        private void UnsubscribeDeath()
        {
            if (!deathSubscribed || damageable == null)
            {
                return;
            }

            deathSubscribed = false;
            damageable.Died -= HandleAuthorityOwnerDied;
        }
    }
}
