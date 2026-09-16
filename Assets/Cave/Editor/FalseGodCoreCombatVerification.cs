using System.Collections.Generic;
using Cave.Combat;
using Cave.Domain;
using Cave.Interactions;
using Cave.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>
    /// Development-only, no-asset verification for Sprint 4's first Claim
    /// combat loop. It creates a temporary shell and tears every object down
    /// before returning; no prefab, scene, or spawn catalog is touched.
    /// </summary>
    public static class FalseGodCoreCombatVerification
    {
        [MenuItem("Tools/Cave/Verification/Run False God Core Combat Verification")]
        private static void RunFromMenu()
        {
            if (TryVerify(out string report))
            {
                Debug.Log("[Cave] False God core combat verification passed: " + report);
                return;
            }

            Debug.LogError("[Cave] False God core combat verification failed: " + report);
        }

        public static bool TryVerify(out string report)
        {
            FalseGodCombatController shell = null;
            GameObject projectileObject = null;
            GameObject invalidProjectileObject = null;
            int ownershipChanged = 0;
            System.Action<InteractionEvent> listener = interactionEvent =>
            {
                if (interactionEvent.Kind == InteractionEventKind.OwnershipChanged)
                {
                    ownershipChanged++;
                }
            };

            InteractionEventBus.EventEmitted += listener;
            try
            {
                shell = FalseGodDevelopmentShell.Create(Vector2.zero);
                FalseGodRuntimeFoundation foundation = shell.GetComponent<FalseGodRuntimeFoundation>();
                CrystalRainAbility rain = shell.GetComponent<CrystalRainAbility>();
                CausalBeamAbility beam = shell.GetComponent<CausalBeamAbility>();
                ClaimedProjectileHold hold = shell.GetComponent<ClaimedProjectileHold>();
                ClaimAuthorityNetwork network = foundation != null ? foundation.AuthorityNetwork : null;

                bool initialized = shell != null
                    && foundation != null
                    && network != null
                    && rain != null
                    && beam != null
                    && hold != null;

                int requestedCrystals = rain != null ? rain.MaximumCrystalsPerCast : 0;
                int manifested = rain != null ? rain.ExecuteImmediatelyForVerification(new Vector2(2f, 0f)) : 0;
                bool boundedRain = manifested > 0 && manifested <= requestedCrystals;
                bool territoriesCreated = network != null
                    && network.ActiveAnchorCount == manifested
                    && network.ActiveTerritoryCount == manifested
                    && network.Load.TotalRelationships == manifested * 2;

                CausalBeamRoute initialRoute = beam != null
                    ? beam.BuildRouteForVerification(new Vector2(10f, 0f))
                    : default;
                bool routedThroughOwnedAnchor = initialRoute.RelayCount > 0
                    && initialRoute.UsesOnlyAuthorityAnchors;

                List<ClaimAnchor> activeAnchors = new List<ClaimAnchor>();
                network?.CopyActiveAnchors(activeAnchors);
                ClaimAnchor relay = null;
                for (int index = 0; index < activeAnchors.Count; index++)
                {
                    ClaimAnchor candidate = activeAnchors[index];
                    if (candidate != null && Mathf.Abs(candidate.transform.position.y) < 0.1f)
                    {
                        relay = candidate;
                        break;
                    }
                }

                if (relay == null && activeAnchors.Count > 0)
                {
                    relay = activeAnchors[0];
                }
                relay?.Deactivate();
                CausalBeamRoute inactiveRoute = beam != null
                    ? beam.BuildRouteForVerification(new Vector2(10f, 0f))
                    : default;
                bool inactiveIgnored = relay == null || inactiveRoute.RelayCount < initialRoute.RelayCount;

                ClaimCrystal appropriationCrystal = foundation != null
                    ? foundation.ManifestCrystal(new Vector2(1f, 0f), 3, 2f)
                    : null;
                bool appropriationTerritoryCreated = appropriationCrystal != null
                    && appropriationCrystal.Territory != null
                    && appropriationCrystal.Territory.IsActive;

                PlayerProjectile projectile = CreateVerificationProjectile(
                    "False God Appropriation Verification Projectile",
                    new Vector2(1f, 0f),
                    out projectileObject);
                InteractionIdentity projectileIdentity = projectile != null ? projectile.InteractionIdentity : null;
                bool enteredTerritory = appropriationCrystal != null
                    && projectileIdentity != null
                    && appropriationCrystal.Territory.RegisterOccupantForVerification(projectileIdentity);
                ClaimResult captureResult = default;
                bool captured = false;
                if (shell != null)
                {
                    captured = shell.TryAppropriate(projectile, out captureResult);
                }
                bool captureSucceeded = captured
                    && captureResult.Succeeded
                    && projectileIdentity != null
                    && projectileIdentity.Ownership == InteractionOwnership.Claim
                    && projectile != null
                    && projectile.IsClaimSuspended
                    && hold.CapturedCount == 1;
                ClaimLoadSnapshot loadAfterCapture = network != null ? network.Load : default;
                bool networkRegisteredCapture = loadAfterCapture.ClaimedObjects == 1;

                PlayerProjectile invalidProjectile = CreateVerificationProjectile(
                    "False God Invalid Appropriation Verification Projectile",
                    new Vector2(8f, 0f),
                    out invalidProjectileObject);
                ClaimResult invalidResult = default;
                bool invalidRejected = shell != null
                    && !shell.TryAppropriate(invalidProjectile, out invalidResult)
                    && (invalidProjectile == null
                        || invalidProjectile.InteractionIdentity == null
                        || invalidProjectile.InteractionIdentity.Ownership == InteractionOwnership.Player)
                    && !invalidResult.Succeeded;

                ClaimLoadSnapshot loadBeforeCrystalDestruction = network != null ? network.Load : default;
                ClaimAnchor destroyedAnchor = appropriationCrystal != null ? appropriationCrystal.Anchor : null;
                if (appropriationCrystal != null)
                {
                    // Retire through the crystal's normal authority cleanup
                    // first, then use the edit-mode-safe physical destroy API
                    // for this temporary verifier object.
                    appropriationCrystal.RetireAuthority();
                    Object.DestroyImmediate(appropriationCrystal.gameObject);
                }

                List<ClaimAnchor> anchorsAfterCrystalDestruction = new List<ClaimAnchor>();
                network?.CopyActiveAnchors(anchorsAfterCrystalDestruction);
                CausalBeamRoute routeAfterCrystalDestruction = beam != null
                    ? beam.BuildRouteForVerification(new Vector2(10f, 0f))
                    : default;
                bool loadReducedAfterCrystalDestruction = network != null
                    && network.ActiveAnchorCount == loadBeforeCrystalDestruction.ActiveAnchors - 1
                    && network.ActiveTerritoryCount == loadBeforeCrystalDestruction.ActiveTerritories - 1
                    && network.ClaimedObjectCount == loadBeforeCrystalDestruction.ClaimedObjects
                    && !anchorsAfterCrystalDestruction.Contains(destroyedAnchor)
                    && routeAfterCrystalDestruction.RelayCount == 0;
                ClaimLoadSnapshot loadAfterCrystalDestruction = network != null ? network.Load : default;

                Damageable shellDamageable = shell != null ? shell.GetComponent<Damageable>() : null;
                shellDamageable?.TakeDamage(999, new DamageContext(shell.gameObject, DamageTrait.Direct));
                bool deathCleanedAuthority = network != null
                    && network.ActiveAnchorCount == 0
                    && network.ActiveTerritoryCount == 0
                    && network.ClaimedObjectCount == 0
                    && !shell.gameObject.activeSelf
                    && hold.CapturedCount == 0;
                ClaimLoadSnapshot loadAfterDeath = network != null ? network.Load : default;

                bool passed = initialized
                    && boundedRain
                    && territoriesCreated
                    && routedThroughOwnedAnchor
                    && inactiveIgnored
                    && appropriationTerritoryCreated
                    && enteredTerritory
                    && captureSucceeded
                    && networkRegisteredCapture
                    && invalidRejected
                    && loadReducedAfterCrystalDestruction
                    && ownershipChanged == 1
                    && deathCleanedAuthority;
                string firstFailure = FirstFailure(
                    initialized,
                    boundedRain,
                    territoriesCreated,
                    routedThroughOwnedAnchor,
                    inactiveIgnored,
                    appropriationTerritoryCreated,
                    enteredTerritory,
                    captureSucceeded,
                    networkRegisteredCapture,
                    invalidRejected,
                    loadReducedAfterCrystalDestruction,
                    ownershipChanged == 1,
                    deathCleanedAuthority);
                report = passed
                    ? "temporary shell initialized; bounded Crystal Rain established Claim territories/load; Causal Beam used only active owned anchors; explicit territorial Appropriation captured one PlayerProjectile; cleanup retired authority and capture state."
                    : "FAILED at " + firstFailure + ". "
                        + "initialized=" + initialized
                        + "; manifested=" + manifested + "/" + requestedCrystals
                        + "; territoriesCreated=" + territoriesCreated
                        + "; initialRelays=" + initialRoute.RelayCount
                        + "; initialRouteOwned=" + initialRoute.UsesOnlyAuthorityAnchors
                        + "; inactiveRelays=" + inactiveRoute.RelayCount
                        + "; appropriationTerritoryCreated=" + appropriationTerritoryCreated
                        + "; enteredTerritory=" + enteredTerritory
                        + "; captured=" + captured
                        + "; captureSucceeded=" + captureSucceeded
                        + "; loadAfterCapture=(anchors=" + loadAfterCapture.ActiveAnchors
                        + ",territories=" + loadAfterCapture.ActiveTerritories
                        + ",claimed=" + loadAfterCapture.ClaimedObjects + ")"
                        + "; invalidRejected=" + invalidRejected
                        + "; ownershipChanged=" + ownershipChanged
                        + "; loadReduced=" + loadReducedAfterCrystalDestruction
                        + "; loadBeforeCrystalDestruction=(anchors=" + loadBeforeCrystalDestruction.ActiveAnchors
                        + ",territories=" + loadBeforeCrystalDestruction.ActiveTerritories
                        + ",claimed=" + loadBeforeCrystalDestruction.ClaimedObjects + ")"
                        + "; loadAfterCrystalDestruction=(anchors=" + loadAfterCrystalDestruction.ActiveAnchors
                        + ",territories=" + loadAfterCrystalDestruction.ActiveTerritories
                        + ",claimed=" + loadAfterCrystalDestruction.ClaimedObjects + ")"
                        + "; relaysAfterCrystalDestruction=" + routeAfterCrystalDestruction.RelayCount
                        + "; deathCleaned=" + deathCleanedAuthority
                        + "; loadAfterDeath=(anchors=" + loadAfterDeath.ActiveAnchors
                        + ",territories=" + loadAfterDeath.ActiveTerritories
                        + ",claimed=" + loadAfterDeath.ClaimedObjects + ")"
                        + "; heldAfterDeath=" + (hold != null ? hold.CapturedCount : -1) + ".";
                return passed;
            }
            finally
            {
                InteractionEventBus.EventEmitted -= listener;
                if (projectileObject != null)
                {
                    Object.DestroyImmediate(projectileObject);
                }

                if (invalidProjectileObject != null)
                {
                    Object.DestroyImmediate(invalidProjectileObject);
                }

                if (shell != null)
                {
                    Object.DestroyImmediate(shell.gameObject);
                }
            }
        }

        private static PlayerProjectile CreateVerificationProjectile(
            string name,
            Vector2 position,
            out GameObject projectileObject)
        {
            projectileObject = new GameObject(name);
            projectileObject.transform.position = position;
            Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            PlayerProjectile projectile = projectileObject.AddComponent<PlayerProjectile>();
            // Launch performs its own runtime-reference validation, so this
            // follows normal construction without editor SendMessage/lifecycle
            // simulation.
            projectile.Launch(Vector2.right, default, 1);
            return projectile;
        }

        private static string FirstFailure(
            bool initialized,
            bool boundedRain,
            bool territoriesCreated,
            bool routedThroughOwnedAnchor,
            bool inactiveIgnored,
            bool appropriationTerritoryCreated,
            bool enteredTerritory,
            bool captureSucceeded,
            bool networkRegisteredCapture,
            bool invalidRejected,
            bool loadReducedAfterCrystalDestruction,
            bool ownershipChangedOnce,
            bool deathCleanedAuthority)
        {
            if (!initialized) return "ShellInitialization";
            if (!boundedRain) return "CrystalRainBound";
            if (!territoriesCreated) return "CrystalRainTerritories";
            if (!routedThroughOwnedAnchor) return "CausalBeamOwnedRelay";
            if (!inactiveIgnored) return "CausalBeamInactiveRelayFiltering";
            if (!appropriationTerritoryCreated) return "AppropriationTerritoryCreation";
            if (!enteredTerritory) return "AppropriationTerritorialEvidence";
            if (!captureSucceeded) return "AppropriationCaptureState";
            if (!networkRegisteredCapture) return "AppropriationNetworkClaimedObjectRegistration";
            if (!invalidRejected) return "AppropriationInvalidProvenanceRejection";
            if (!loadReducedAfterCrystalDestruction) return "ClaimLoadAfterCrystalDestruction";
            if (!ownershipChangedOnce) return "AppropriationOwnershipChangedOnce";
            if (!deathCleanedAuthority) return "BossDeathCleanup";
            return "Unknown";
        }
    }
}
