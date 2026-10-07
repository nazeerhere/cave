using Cave.Domain;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Player
{
    public enum SharedDomainActivationAction
    {
        None = 0,
        SummonCurseAltar = 1,
        RequestManifestation = 2,
        DismissManifestation = 3
    }

    /// <summary>Pure decision seam for deterministic shared-input verification.</summary>
    public static class SharedDomainActivationPolicy
    {
        public static bool IsHoldReached(float elapsed, float holdDuration)
        {
            return elapsed >= Mathf.Max(.01f, holdDuration);
        }

        public static SharedDomainActivationAction ResolveRelease(bool pressConsumed)
        {
            return pressConsumed
                ? SharedDomainActivationAction.None
                : SharedDomainActivationAction.SummonCurseAltar;
        }

        public static SharedDomainActivationAction ResolveHold(bool manifestationIsActive)
        {
            return manifestationIsActive
                ? SharedDomainActivationAction.DismissManifestation
                : SharedDomainActivationAction.RequestManifestation;
        }
    }

    /// <summary>
    /// The sole dispatcher for the rebindable Curse Altar action. A press is
    /// either a short altar tap or a consumed Domain hold; it can never become
    /// both after release.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSharedDomainActivationInput : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float domainHoldDuration = 0.6f;

        private PlayerCurseAltarController altar;
        private PlayerDomainManifestation manifestation;
        private bool trackingPress;
        private bool pressConsumed;
        private float pressedAt;

        public float DomainHoldDuration => domainHoldDuration;

        public static PlayerSharedDomainActivationInput EnsureOn(GameObject owner)
        {
            return owner == null ? null : owner.GetComponent<PlayerSharedDomainActivationInput>()
                ?? owner.AddComponent<PlayerSharedDomainActivationInput>();
        }

        private void Awake()
        {
            ResolveOwners();
        }

        private void Update()
        {
            ResolveOwners();
            if (!GameInput.GameplayInputEnabled)
            {
                CancelPendingPress();
                return;
            }

            if (!trackingPress)
            {
                if (GameInput.SummonCurseAltarPressed)
                {
                    trackingPress = true;
                    pressConsumed = false;
                    pressedAt = Time.time;
                }

                return;
            }

            if (!GameInput.SummonCurseAltarHeld)
            {
                if (SharedDomainActivationPolicy.ResolveRelease(pressConsumed)
                    == SharedDomainActivationAction.SummonCurseAltar)
                {
                    altar?.TrySummonAltar();
                }

                CancelPendingPress();
                return;
            }

            if (!pressConsumed && SharedDomainActivationPolicy.IsHoldReached(
                Time.time - pressedAt,
                domainHoldDuration))
            {
                // Consume even when the request is rejected: a long hold must
                // never become an altar summon on release.
                pressConsumed = true;
                if (manifestation == null)
                {
                    return;
                }

                SharedDomainActivationAction action = SharedDomainActivationPolicy.ResolveHold(manifestation.IsActive);
                if (action == SharedDomainActivationAction.DismissManifestation)
                {
                    manifestation.Dismiss();
                }
                else
                {
                    manifestation.TryRequestActivation();
                }
            }
        }

        private void OnDisable()
        {
            CancelPendingPress();
        }

        private void ResolveOwners()
        {
            if (altar == null) altar = GetComponent<PlayerCurseAltarController>();
            if (manifestation == null) manifestation = GetComponent<PlayerDomainManifestation>();
        }

        private void CancelPendingPress()
        {
            trackingPress = false;
            pressConsumed = false;
            pressedAt = 0f;
        }
    }
}
