using System.Collections.Generic;
using Cave.Combat;
using Cave.Domain;
using Cave.Interactions;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Lifecycle-safe no-asset proof for the Ascendant Monk seams.</summary>
    public static class FalseGodMonkVerification
    {
        [MenuItem("Tools/Cave/Verification/Run False God Monk Verification")]
        private static void RunFromMenu()
        {
            if (TryVerify(out string report))
            {
                Debug.Log("[Cave] False God Monk verification passed: " + report);
            }
            else
            {
                Debug.LogError("[Cave] False God Monk verification failed: " + report);
            }
        }

        public static bool TryVerify(out string report)
        {
            FalseGodCombatController shell = null;
            List<GameObject> cleanup = new List<GameObject>();
            string step = "ShellInitialization";
            try
            {
                shell = FalseGodDevelopmentShell.Create(Vector2.zero, true);
                Damageable health = shell.GetComponent<Damageable>();
                FalseGodRuntimeFoundation foundation = shell.GetComponent<FalseGodRuntimeFoundation>();
                FalseGodFormController form = shell.GetComponent<FalseGodFormController>();
                FalseGodMonkCombat monk = shell.GetComponent<FalseGodMonkCombat>();
                health.InitializeRuntimeState();

                step = "ProphetToMonkTransition";
                health.TakeDamage(999, new DamageContext(null, DamageTrait.Direct));
                bool transitioned = form != null && form.CurrentForm == FalseGodForm.Monk
                    && shell.CurrentForm == FalseGodForm.Monk && shell.gameObject.activeSelf
                    && foundation != null && foundation.InitializeRuntime();
                if (!transitioned)
                {
                    report = Failure(step, true, false);
                    return false;
                }

                step = "ForeignRepossessionRejected";
                InteractionIdentity foreign = CreateIdentity("Foreign", InteractionOwnership.Player, null, true, 1, cleanup);
                ClaimResult foreignResult = default;
                bool rejectsForeign = monk != null && !shell.TryRepossess(foreign, out foreignResult)
                    && !foreignResult.Succeeded;
                if (!rejectsForeign)
                {
                    report = Failure(step, true, false, foreignResult.Rejection.ToString());
                    return false;
                }

                step = "ValidRepossession";
                ClaimCrystal repossessionCrystal = foundation.ManifestCrystal(new Vector2(0.7f, 0f), 3, 1f);
                InteractionIdentity returned = CreateIdentity("Repossession Success Object", InteractionOwnership.Player, null, true, 1, cleanup);
                ClaimResult initialClaim = ClaimResolver.Resolve(new ClaimAttempt(
                    repossessionCrystal.Anchor.Identity, returned, ClaimProvenance.ExistingClaimAnchor, 3));
                bool initialClaimSucceeded = initialClaim.Succeeded && returned.Ownership == InteractionOwnership.Claim;
                bool redirected = InteractionRuntime.TransferResolvedOwnership(
                    returned, InteractionOwnership.Player, null, 1);
                int ownershipEvents = 0;
                System.Action<InteractionEvent> ownershipListener = e =>
                {
                    if (e.Kind == InteractionEventKind.OwnershipChanged && e.Target == returned.gameObject)
                    {
                        ownershipEvents++;
                    }
                };
                InteractionEventBus.EventEmitted += ownershipListener;
                bool repossessed = shell.TryRepossess(returned, out ClaimResult repossessionResult)
                    && initialClaimSucceeded && redirected && returned.HasAuthorityHistory(foundation.gameObject)
                    && repossessionResult.Succeeded && returned.Ownership == InteractionOwnership.Claim
                    && returned.AuthoritySource == foundation.gameObject && ownershipEvents == 1;
                InteractionEventBus.EventEmitted -= ownershipListener;
                if (!repossessed)
                {
                    report = Failure(step, true, false, repossessionResult.Rejection.ToString());
                    return false;
                }

                step = "RepossessionResistance";
                InteractionIdentity resistedTarget = CreateIdentity("Repossession Resistance Object", InteractionOwnership.Player, null, true, 1, cleanup);
                ClaimResult resistedInitialClaim = ClaimResolver.Resolve(new ClaimAttempt(
                    repossessionCrystal.Anchor.Identity, resistedTarget, ClaimProvenance.ExistingClaimAnchor, 3));
                InteractionRuntime.TransferResolvedOwnership(resistedTarget, InteractionOwnership.Player, null, 4);
                ClaimResistance resistance = resistedTarget.gameObject.AddComponent<ClaimResistance>();
                resistance.ConfigureRuntimeResistance(4);
                ClaimResult resisted = default;
                bool resistancePrevails = resistedInitialClaim.Succeeded
                    && !shell.TryRepossess(resistedTarget, out resisted)
                    && resisted.Rejection == ClaimRejectionReason.ResistancePrevails;
                if (!resistancePrevails)
                {
                    report = Failure(step, ClaimRejectionReason.ResistancePrevails, resisted.Rejection);
                    return false;
                }
                cleanup.Add(repossessionCrystal.gameObject);

                step = "ValidTithe";
                ClaimCrystal titheCrystal = foundation.ManifestCrystal(new Vector2(1f, 0f), 3, 1f);
                int loadBeforeTithe = foundation.ClaimLoad.TotalRelationships;
                health.TakeDamage(3, new DamageContext(null, DamageTrait.Direct));
                int healthBeforeTithe = health.CurrentHealth;
                bool tithe = shell.TryTithe(titheCrystal, FalseGodTitheOutcome.Heal, out int titheBenefit)
                    && titheBenefit > 0 && !titheCrystal.IsActive
                    && health.CurrentHealth > healthBeforeTithe;
                if (!tithe)
                {
                    report = Failure(step, true, false, "benefit=" + titheBenefit);
                    return false;
                }

                step = "TitheLoadReduction";
                bool titheLoadReduction = foundation.ClaimLoad.TotalRelationships < loadBeforeTithe;
                if (!titheLoadReduction)
                {
                    report = Failure(step, "< " + loadBeforeTithe, foundation.ClaimLoad.TotalRelationships);
                    return false;
                }

                step = "InvalidTithe";
                bool invalidTithe = !shell.TryTithe(titheCrystal, FalseGodTitheOutcome.Heal, out int invalidBenefit)
                    && invalidBenefit == 0;
                if (!invalidTithe)
                {
                    report = Failure(step, true, false);
                    return false;
                }
                cleanup.Add(titheCrystal.gameObject);

                step = "Reconfiguration";
                ClaimCrystal crystal = foundation.ManifestCrystal(new Vector2(2f, 0f), 3, 1f);
                InteractionOwnership beforeConfigurationOwner = crystal.Anchor.Identity.Ownership;
                bool defensive = shell.TryReconfigure(crystal, ClaimCrystalConfigurationMode.Defensive);
                ClaimCrystalConfiguration configuration = crystal.GetComponent<ClaimCrystalConfiguration>();
                bool reconfigured = defensive && configuration != null
                    && configuration.Mode == ClaimCrystalConfigurationMode.Defensive;
                if (!reconfigured)
                {
                    report = Failure(step, ClaimCrystalConfigurationMode.Defensive,
                        configuration != null ? configuration.Mode : ClaimCrystalConfigurationMode.Relay);
                    return false;
                }

                step = "ReconfigurationOwnershipPreserved";
                bool configurationOwnership = crystal.Anchor.Identity.Ownership == beforeConfigurationOwner
                    && crystal.Anchor.Identity.AuthoritySource == foundation.gameObject;
                if (!configurationOwnership)
                {
                    report = Failure(step, InteractionOwnership.Claim, crystal.Anchor.Identity.Ownership);
                    return false;
                }

                step = "ReconfigurationOffensive";
                bool offensive = shell.TryReconfigure(crystal, ClaimCrystalConfigurationMode.Offensive);
                if (!offensive || configuration.Mode != ClaimCrystalConfigurationMode.Offensive)
                {
                    report = Failure(step, ClaimCrystalConfigurationMode.Offensive, configuration.Mode);
                    return false;
                }
                GameObject foreignOwner = new GameObject("Foreign Claim Authority");
                Damageable foreignHealth = foreignOwner.AddComponent<Damageable>();
                foreignHealth.InitializeRuntimeState();
                FalseGodRuntimeFoundation foreignFoundation = foreignOwner.AddComponent<FalseGodRuntimeFoundation>();
                foreignFoundation.InitializeRuntime();
                ClaimCrystal foreignCrystal = foreignFoundation.ManifestCrystal(new Vector2(3f, 0f), 3, 1f);
                bool rejectsForeignConfiguration = !shell.TryReconfigure(foreignCrystal, ClaimCrystalConfigurationMode.Defensive);
                if (!rejectsForeignConfiguration)
                {
                    report = Failure("ForeignReconfigurationRejected", true, false);
                    return false;
                }
                cleanup.Add(foreignOwner);
                cleanup.Add(foreignCrystal.gameObject);
                cleanup.Add(crystal.gameObject);

                step = "Block";
                bool block = shell.TryBlock() && shell.PresentationState == FalseGodProphetPresentationState.Block;
                shell.GetComponent<FalseGodProphetBlock>()?.Defense?.EndBlockingForDestabilization();
                bool blockEnds = shell.GetComponent<FalseGodProphetBlock>() != null
                    && !shell.GetComponent<FalseGodProphetBlock>().IsBlocking;
                if (!block || !blockEnds)
                {
                    report = Failure("MonkBlock", true, false, "started=" + block + ", ended=" + blockEnds);
                    return false;
                }

                step = "DomainSeedNotGranted";
                bool hasNoDomainSeed = shell.GetComponent<FalseGodDomainSeedReward>() == null;
                if (!hasNoDomainSeed)
                {
                    report = Failure(step, true, false);
                    return false;
                }

                step = "FinalAscensionRequest";
                int finalRequests = 0;
                form.TrueBodyDimensionTransitionRequested += () => finalRequests++;
                health.TakeDamage(999, new DamageContext(null, DamageTrait.Direct));
                bool finalTransition = form.CurrentForm == FalseGodForm.TrueBodyFinalPending
                    && finalRequests == 1 && shell.gameObject.activeSelf;
                if (!finalTransition)
                {
                    report = Failure(step, true, false, "form=" + form.CurrentForm + ", requests=" + finalRequests);
                    return false;
                }

                report = "Prophet-to-Monk transition, provenance-only Repossession, resistance, bounded Tithe/load, typed Reconfiguration, shared Block, and True Body transition verified.";
                return true;
            }
            catch (System.Exception exception)
            {
                report = "EXCEPTION during " + step + ": " + exception.GetType().Name + ": "
                    + exception.Message + "\n" + exception.StackTrace;
                return false;
            }
            finally
            {
                for (int index = 0; index < cleanup.Count; index++)
                {
                    if (cleanup[index] != null)
                    {
                        Object.DestroyImmediate(cleanup[index]);
                    }
                }

                if (shell != null)
                {
                    Object.DestroyImmediate(shell.gameObject);
                }
            }
        }

        private static InteractionIdentity CreateIdentity(
            string name, InteractionOwnership ownership, GameObject source, bool claimable, int strength, List<GameObject> cleanup)
        {
            GameObject subject = new GameObject(name);
            InteractionIdentity identity = InteractionRuntime.TrackSpawn(
                subject, InteractionTraits.Projectile, ownership, source, claimable, strength);
            cleanup.Add(subject);
            return identity;
        }

        private static string Failure(string condition, object expected, object actual, string detail = null)
        {
            return "FAILED at " + condition + "\nexpected=" + expected + "\nactual=" + actual
                + (string.IsNullOrEmpty(detail) ? string.Empty : "\ndetail=" + detail);
        }
    }
}
