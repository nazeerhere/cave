using System.Collections.Generic;
using Cave.Combat;
using Cave.Domain;
using Cave.Enemies;
using Cave.Interactions;
using Cave.Player;
using Cave.World;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>
    /// Editor-only, no-asset proof for the Version 1 Prophet shell. It calls
    /// explicit runtime seams and never fakes Unity lifecycle callbacks.
    /// </summary>
    public static class FalseGodProphetVerification
    {
        [MenuItem("Tools/Cave/Verification/Run False God Prophet Verification")]
        private static void RunFromMenu()
        {
            if (TryVerify(out string report))
            {
                Debug.Log("[Cave] False God Prophet verification passed: " + report);
                return;
            }

            Debug.LogError("[Cave] False God Prophet verification failed: " + report);
        }

        public static bool TryVerify(out string report)
        {
            FalseGodCombatController shell = null;
            GameObject playerObject = null;
            List<GameObject> temporaryObjects = new List<GameObject>();
            string step = "ShellInitialization";
            try
            {
                shell = FalseGodDevelopmentShell.Create(Vector2.zero);
                playerObject = CreateVerificationPlayer(new Vector2(4f, 0f));
                PlayerHealth player = playerObject.GetComponent<PlayerHealth>();
                Transform playerTransform = playerObject.transform;
                shell.SetCombatTarget(playerTransform);

                FalseGodRuntimeFoundation foundation = shell.GetComponent<FalseGodRuntimeFoundation>();
                FalseGodProphetBasicCast basic = shell.GetComponent<FalseGodProphetBasicCast>();
                FalseGodDashShadow dash = shell.GetComponent<FalseGodDashShadow>();
                FalseGodCrystalChainHook hook = shell.GetComponent<FalseGodCrystalChainHook>();
                FalseGodTriuneAnchors triune = shell.GetComponent<FalseGodTriuneAnchors>();
                FalseGodProphetBlock block = shell.GetComponent<FalseGodProphetBlock>();
                FalseGodProphetPhaseBoundary boundary = shell.GetComponent<FalseGodProphetPhaseBoundary>();
                Damageable shellHealth = shell.GetComponent<Damageable>();
                shellHealth?.InitializeRuntimeState();

                bool initialized = shell != null && foundation != null && basic != null && dash != null
                    && hook != null && triune != null && block != null && boundary != null && player != null;

                step = "Echo";
                int echoCount = basic != null ? basic.FireEchoImmediatelyForVerification(playerTransform.position) : 0;
                bool echo = echoCount == 2;

                step = "SlowBoltDamageCheck";
                FalseGodProphetProjectile slowProjectile = basic != null
                    ? basic.FireSlowImmediatelyForVerification(playerTransform.position)
                    : null;
                int healthBeforeSlow = player != null ? player.CurrentHealth : 0;
                bool slowApplied = slowProjectile != null
                    && slowProjectile.AppliesSlow
                    && slowProjectile.TryApplyToPlayer(player)
                    && player.CurrentHealth > 0
                    && player.CurrentHealth < healthBeforeSlow
                    && player.GetComponent<PlayerSlowStatus>() != null
                    && player.GetComponent<PlayerSlowStatus>().IsSlowed
                    && Mathf.Approximately(
                        player.GetComponent<PlayerSlowStatus>().ActiveMovementMultiplier,
                        slowProjectile.SlowMovementMultiplier)
                    && player.GetComponent<PlayerController>() != null
                    && Mathf.Approximately(
                        player.GetComponent<PlayerController>().StatusMovementMultiplier,
                        slowProjectile.SlowMovementMultiplier);
                PlayerSlowStatus slowStatus = player.GetComponent<PlayerSlowStatus>();
                slowStatus?.ClearSlow();
                bool slowClearRestoresMovement = slowStatus != null
                    && Mathf.Approximately(slowStatus.ActiveMovementMultiplier, 1f)
                    && Mathf.Approximately(
                        player.GetComponent<PlayerController>().StatusMovementMultiplier,
                        1f);
                slowApplied &= slowClearRestoresMovement;

                step = "DashCommit";
                FalseGodProphetProjectile shadow = dash != null
                    ? dash.FireImmediatelyForVerification(playerTransform.position)
                    : null;
                Vector2 committedDirection = dash != null ? dash.LastCommittedDirection : Vector2.zero;
                playerTransform.position = new Vector2(-4f, 0f);
                bool committedDash = shadow != null
                    && committedDirection == Vector2.right
                    && dash.LastCommittedDirection == committedDirection;

                step = "HookRequiresCrystal";
                bool hookRejectsWithoutCrystal = hook != null && !hook.AttachForVerification(player);
                ClaimCrystal firstCrystal = foundation != null
                    ? foundation.ManifestCrystal(new Vector2(1.5f, 0f), 3, 1.5f)
                    : null;
                bool hookAttached = hook != null && hook.AttachForVerification(player);
                firstCrystal?.RetireAuthority();
                bool crystalDestructionBreaksTether = hookAttached && !hook.IsActive;
                if (firstCrystal != null)
                {
                    temporaryObjects.Add(firstCrystal.gameObject);
                }

                step = "TetherDurability";
                ClaimCrystal secondCrystal = foundation != null
                    ? foundation.ManifestCrystal(new Vector2(1.5f, 0f), 3, 1.5f)
                    : null;
                bool attachedForDurability = hook != null && hook.AttachForVerification(player);
                int durability = hook != null ? hook.RemainingDurability : 0;
                bool tetherBreakable = attachedForDurability && hook.ApplyTetherDamage(durability) && !hook.IsActive;
                if (secondCrystal != null)
                {
                    temporaryObjects.Add(secondCrystal.gameObject);
                }

                step = "Block";
                bool blockStarted = shell.TryBlock()
                    && block != null
                    && block.IsBlocking
                    && block.Defense != null
                    && block.Defense.CurrentState == EnemyDefenseState.Blocking
                    && shell.PresentationState == FalseGodProphetPresentationState.Block;
                bool guardBreakAvailable = blockStarted
                    && block.Defense.TryReceiveGuardBreak(playerObject);
                block?.Defense?.EndBlockingForDestabilization();
                bool blockEnds = block != null
                    && !block.IsBlocking
                    && block.Defense.CurrentState != EnemyDefenseState.Blocking;

                step = "TriuneHeal";
                shellHealth?.TakeDamage(1, new DamageContext(playerObject, DamageTrait.Direct));
                int damagedHealth = shellHealth != null ? shellHealth.CurrentHealth : 0;
                bool createdHeal = triune != null && triune.TryCreate(FalseGodTriuneAnchorKind.Heal);
                int healed = triune != null ? triune.ApplyHealTickForVerification() : 0;
                bool healOnlyWhileActive = createdHeal && shellHealth != null
                    && healed > 0 && shellHealth.CurrentHealth > damagedHealth;
                bool healRemoved = RetireAnchor(triune, FalseGodTriuneAnchorKind.Heal)
                    && triune.ApplyHealTickForVerification() == 0;

                step = "TriuneEmpower";
                bool createdEmpower = triune != null && triune.TryCreate(FalseGodTriuneAnchorKind.Empower);
                bool empoweredOnlyBasicCast = createdEmpower && triune.ResolveBasicCastDamageMultiplier() > 1f;
                bool empowerRemoved = RetireAnchor(triune, FalseGodTriuneAnchorKind.Empower)
                    && Mathf.Approximately(triune.ResolveBasicCastDamageMultiplier(), 1f);

                step = "TriuneSuppress";
                triune?.SetSuppressionTarget(playerTransform);
                bool createdSuppress = triune != null && triune.TryCreate(FalseGodTriuneAnchorKind.Suppress);
                PlayerRecoveryModifiers recovery = playerObject.GetComponent<PlayerRecoveryModifiers>();
                bool suppressesOnlyStaminaRegen = createdSuppress && recovery != null
                    && recovery.StaminaRegenerationMultiplier < 1f
                    && Mathf.Approximately(recovery.HealthRegenerationMultiplier, 1f)
                    && Mathf.Approximately(recovery.ManaRegenerationMultiplier, 1f);
                bool suppressRemoved = RetireAnchor(triune, FalseGodTriuneAnchorKind.Suppress)
                    && recovery != null && Mathf.Approximately(recovery.StaminaRegenerationMultiplier, 1f);

                step = "TriuneUniqueness";
                bool oneOfEach = triune != null
                    && triune.TryCreate(FalseGodTriuneAnchorKind.Heal)
                    && !triune.TryCreate(FalseGodTriuneAnchorKind.Heal)
                    && triune.TryCreate(FalseGodTriuneAnchorKind.Empower)
                    && triune.TryCreate(FalseGodTriuneAnchorKind.Suppress)
                    && triune.ActiveAnchorCount == 3;

                step = "DeathCleanup";
                ClaimCrystal deathCrystal = foundation != null
                    ? foundation.ManifestCrystal(new Vector2(1.3f, 0f), 3, 1.5f)
                    : null;
                bool deathTetherAttached = hook != null && hook.AttachForVerification(player);
                int ascensionRequests = 0;
                if (boundary != null)
                {
                    boundary.AscensionRequested += () => ascensionRequests++;
                }

                bool noPermanentRewardComponent = shell.GetComponent<FalseGodDomainSeedReward>() == null;
                shellHealth?.TakeDamage(999, new DamageContext(playerObject, DamageTrait.Direct));
                bool deathCleans = shellHealth != null && !shell.gameObject.activeSelf
                    && triune.ActiveAnchorCount == 0
                    && !hook.IsActive
                    && deathTetherAttached;
                bool phaseBoundary = boundary != null && boundary.HasRequestedAscension
                    && ascensionRequests == 1 && noPermanentRewardComponent;
                if (deathCrystal != null)
                {
                    temporaryObjects.Add(deathCrystal.gameObject);
                }

                step = "Presentation";
                bool presentation = FalseGodProphetPresentationMap.ForAbility(FalseGodAbilityKind.EchoProjectile) == FalseGodProphetPresentationState.BasicCast
                    && FalseGodProphetPresentationMap.ForAbility(FalseGodAbilityKind.SlowBolt) == FalseGodProphetPresentationState.BasicCast
                    && FalseGodProphetPresentationMap.ForAbility(FalseGodAbilityKind.DashShadow) == FalseGodProphetPresentationState.ShadowCast
                    && FalseGodProphetPresentationMap.ForAbility(FalseGodAbilityKind.CrystalRain) == FalseGodProphetPresentationState.ClaimCommand
                    && FalseGodProphetPresentationMap.ForAbility(FalseGodAbilityKind.CrystalChainHook) == FalseGodProphetPresentationState.ClaimCommand
                    && FalseGodProphetPresentationMap.ForAbility(FalseGodAbilityKind.TriuneAnchors) == FalseGodProphetPresentationState.ClaimCommand
                    && FalseGodProphetPresentationMap.ForAbility(FalseGodAbilityKind.Appropriation) == FalseGodProphetPresentationState.ClaimCommand
                    && FalseGodProphetPresentationMap.ForAbility(FalseGodAbilityKind.CausalBeam) == FalseGodProphetPresentationState.CausalBeam
                    && FalseGodProphetPresentationMap.ForAbility(FalseGodAbilityKind.Block) == FalseGodProphetPresentationState.Block
                    && shell.PresentationState == FalseGodProphetPresentationState.Ascension;

                bool passed = initialized && echo && slowApplied && committedDash
                    && hookRejectsWithoutCrystal && crystalDestructionBreaksTether && tetherBreakable
                    && blockStarted && guardBreakAvailable && blockEnds && healOnlyWhileActive && healRemoved
                    && empoweredOnlyBasicCast && empowerRemoved && suppressesOnlyStaminaRegen && suppressRemoved
                    && oneOfEach && deathCleans && phaseBoundary && presentation;
                report = passed
                    ? "Prophet shell verified: bounded Echo/Slow/Dash basics, Claim-crystal tether response, shared block, Triune effects/cleanup, presentation mapping, and non-permanent Ascension boundary."
                    : BuildFailureReport(initialized, echo, slowApplied, committedDash, hookRejectsWithoutCrystal,
                        crystalDestructionBreaksTether, tetherBreakable, blockStarted, guardBreakAvailable, blockEnds,
                        healOnlyWhileActive, healRemoved, empoweredOnlyBasicCast, empowerRemoved,
                        suppressesOnlyStaminaRegen, suppressRemoved, oneOfEach, deathCleans,
                        phaseBoundary, presentation);
                return passed;
            }
            catch (System.Exception exception)
            {
                report = "EXCEPTION during " + step + ": " + exception.GetType().Name + ": "
                    + exception.Message + "\n" + exception.StackTrace;
                return false;
            }
            finally
            {
                for (int index = 0; index < temporaryObjects.Count; index++)
                {
                    if (temporaryObjects[index] != null)
                    {
                        Object.DestroyImmediate(temporaryObjects[index]);
                    }
                }

                if (playerObject != null)
                {
                    Object.DestroyImmediate(playerObject);
                }

                if (shell != null)
                {
                    Object.DestroyImmediate(shell.gameObject);
                }
            }
        }

        private static GameObject CreateVerificationPlayer(Vector2 position)
        {
            GameObject player = new GameObject("False God Prophet Verification Player");
            player.transform.position = position;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            CircleCollider2D collider = player.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            player.AddComponent<PlayerController>();
            PlayerRespawn respawn = player.AddComponent<PlayerRespawn>();
            PlayerHealth health = player.AddComponent<PlayerHealth>();
            PlayerSlowStatus slowStatus = player.AddComponent<PlayerSlowStatus>();
            respawn.InitializeRuntime();
            health.InitializeRuntime();
            slowStatus.InitializeRuntimeDependencies();
            return player;
        }

        private static bool RetireAnchor(FalseGodTriuneAnchors triune, FalseGodTriuneAnchorKind kind)
        {
            if (triune == null || !triune.TryGetAnchor(kind, out FalseGodTriuneAnchor anchor) || anchor == null)
            {
                return false;
            }

            anchor.Retire();
            return !triune.HasActive(kind);
        }

        private static string BuildFailureReport(params bool[] checks)
        {
            string[] labels =
            {
                "ShellInitialization", "Echo", "SlowBolt", "DashCommit", "HookRequiresCrystal",
                "CrystalBreaksTether", "TetherDurability", "BlockStarts", "GuardBreak", "BlockEnds",
                "Heal", "HealRemoval", "Empower", "EmpowerRemoval", "Suppress",
                "SuppressRemoval", "TriuneUniqueness", "DeathCleanup", "AscensionBoundary", "Presentation"
            };
            for (int index = 0; index < checks.Length && index < labels.Length; index++)
            {
                if (!checks[index])
                {
                    return "FAILED at " + labels[index] + "; expected true, actual false.";
                }
            }

            return "FAILED at unknown condition.";
        }
    }
}
