using System;
using Cave.Missions;
using UnityEngine;

namespace Cave.Gambit
{
    public static class SovereignGambitStandardMatchVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return VerifyMissionRouting(out failure)
                && VerifyPlayableGeometry(out failure)
                && VerifyProtectionAndCheckmate(out failure)
                && VerifyAnchorDefeatAndCleanup(out failure)
                && VerifyStrategicCounterplaySelection(out failure)
                && VerifyExistingSuites(out failure);
        }

        private static bool VerifyMissionRouting(out string failure)
        {
            bool pass = MissionControllerSelection.ResolveControllerType(CaveGameMode.SovereignGambit)
                    == typeof(SovereignGambitMissionController)
                && MissionControllerSelection.ResolveControllerType(CaveGameMode.ClassicSweep)
                    == typeof(ClassicSweepMissionController)
                && MissionControllerSelection.ResolveControllerType(CaveGameMode.CorruptionPurge)
                    == typeof(CorruptionPurgeMissionController)
                && MissionControllerSelection.ResolveControllerType(CaveGameMode.CorruptBounty)
                    == typeof(CorruptBountyMissionController)
                && MissionControllerSelection.ResolveControllerType(CaveGameMode.Containment)
                    == typeof(ContainmentMissionController);
            failure = pass ? null : "Mission mode routing changed or Gambit still falls through.";
            return pass;
        }

        private static bool VerifyPlayableGeometry(out string failure)
        {
            float reach = SovereignGambitMissionRules.ResolveMaximumNetworkReach(18f, 3.6f);
            bool pass = Mathf.Approximately(reach, 57.6f)
                && reach >= SovereignGambitMissionRules.MinimumSovereignDistance
                && SovereignGambitMissionRules.MaximumPhysicalDisks == 3
                && !SovereignGambitMissionRules.IsSovereignSocketReachable(5.99f, reach)
                && SovereignGambitMissionRules.IsSovereignSocketReachable(57.6f, reach)
                && !SovereignGambitMissionRules.IsSovereignSocketReachable(57.61f, reach);
            failure = pass ? null : "Three-disk physical reach policy is not deterministic.";
            return pass;
        }

        private static bool VerifyProtectionAndCheckmate(out string failure)
        {
            SovereignGambitRuntime runtime = Active("phase3-checkmate");
            bool protectedBeforeCheck = runtime.EnemySovereignProtectionActive
                && !SovereignStrategicProtection.AllowsDamage(runtime.EnemySovereignProtectionActive)
                && !runtime.ReportEnemySovereignDefeated();
            StrategicNodeId node = new StrategicNodeId("phase3-disk");
            runtime.RegisterOrUpdateNode(new StrategicTerritoryNode(node, GambitSide.Player, Vector2.right));
            runtime.ConnectNodes(runtime.TerritoryGraph.RootId, node);
            runtime.RegisterInfluence(new StrategicInfluence(
                "phase3-physical", node, GambitSide.Player, runtime.EnemySovereignTarget, true, true));
            runtime.Evaluate(1f);
            bool check = runtime.EnemySovereignInCheck
                && SovereignStrategicProtection.AllowsDamage(runtime.EnemySovereignProtectionActive);
            bool checkmate = runtime.ReportEnemySovereignDefeated()
                && runtime.Phase == GambitMatchPhase.Victory
                && !runtime.ReportEnemySovereignDefeated();
            bool pass = protectedBeforeCheck && check && checkmate;
            failure = pass ? null : "Protection, Check, or exactly-once Checkmate failed.";
            return pass;
        }

        private static bool VerifyAnchorDefeatAndCleanup(out string failure)
        {
            SovereignGambitRuntime defeated = Active("phase3-defeat");
            defeated.InvalidatePlayerAnchor();
            bool defeat = defeated.Phase == GambitMatchPhase.Defeat && !defeated.PlayerAnchorValid;
            defeated.End();
            bool ended = defeated.Phase == GambitMatchPhase.Ended && !defeated.PlayerAnchorValid;

            SovereignGambitRuntime victory = Active("phase3-clean-victory");
            StrategicNodeId node = new StrategicNodeId("winning-disk");
            victory.RegisterOrUpdateNode(new StrategicTerritoryNode(node, GambitSide.Player, Vector2.one));
            victory.ConnectNodes(victory.TerritoryGraph.RootId, node);
            victory.RegisterInfluence(new StrategicInfluence(
                "winning-influence", node, GambitSide.Player, victory.EnemySovereignTarget, true, true));
            victory.Evaluate(2f);
            bool won = victory.ReportEnemySovereignDefeated();
            victory.End();
            bool cleanup = won && victory.Phase == GambitMatchPhase.Ended
                && victory.TerritorySnapshot.Nodes.Count == 0;
            bool pass = defeat && ended && cleanup
                && SovereignGambitMissionRules.IsTerminalMissionState(MissionLifecycleState.Success)
                && SovereignGambitMissionRules.IsTerminalMissionState(MissionLifecycleState.Failure)
                && !SovereignGambitMissionRules.IsTerminalMissionState(MissionLifecycleState.Active);
            failure = pass ? null : "Anchor defeat or terminal cleanup ownership failed.";
            return pass;
        }

        private static bool VerifyStrategicCounterplaySelection(out string failure)
        {
            SovereignGambitRuntime runtime = Active("phase3-director");
            SovereignGambitStrategicObjective initial = SovereignGambitStrategicDirector.Resolve(
                SovereignGambitTacticalContextFactory.Create(runtime));
            StrategicNodeId first = new StrategicNodeId("connector-a");
            StrategicNodeId second = new StrategicNodeId("connector-b");
            runtime.RegisterOrUpdateNode(new StrategicTerritoryNode(first, GambitSide.Player, Vector2.right));
            runtime.RegisterOrUpdateNode(new StrategicTerritoryNode(second, GambitSide.Player, Vector2.right * 2f));
            runtime.ConnectNodes(runtime.TerritoryGraph.RootId, first);
            runtime.ConnectNodes(first, second);
            SovereignGambitStrategicObjective connector = SovereignGambitStrategicDirector.Resolve(
                SovereignGambitTacticalContextFactory.Create(runtime));
            runtime.RegisterInfluence(new StrategicInfluence(
                "check-source", second, GambitSide.Player, runtime.EnemySovereignTarget, true, true));
            runtime.Evaluate(1f);
            SovereignGambitStrategicObjective contributor = SovereignGambitStrategicDirector.Resolve(
                SovereignGambitTacticalContextFactory.Create(runtime));
            bool pass = initial.Kind == SovereignGambitObjectiveKind.PressureAnchor
                && connector.Kind == SovereignGambitObjectiveKind.PressureCriticalConnector
                && connector.TargetId == first.Value
                && contributor.Kind == SovereignGambitObjectiveKind.DisruptCheckContributor
                && contributor.TargetId == "check-source";
            failure = pass ? null : "Bounded strategic objective selection is incorrect.";
            return pass;
        }

        private static bool VerifyExistingSuites(out string failure)
        {
            if (!SovereignGambitFoundationVerification.TryRunAll(out failure)) return false;
            if (!SovereignGambitTrapPieceVerification.TryRunAll(out failure)) return false;
            try
            {
                bool pass = SovereignGambitTacticalVerification.RunPure();
                failure = pass ? null : "Existing tactical pure verification returned false.";
                return pass;
            }
            catch (Exception exception)
            {
                failure = "Existing tactical verification failed: " + exception.Message;
                return false;
            }
        }

        private static SovereignGambitRuntime Active(string id)
        {
            SovereignGambitRuntime runtime = new SovereignGambitRuntime(id);
            SovereignAnchorPlacementResult placement = runtime.TryPlaceAnchor(
                new SovereignAnchorPlacementRequest(id, new StrategicNodeId(id + ":anchor"), Vector2.zero, Vector2.one),
                new AcceptedPlacement());
            if (!placement.Accepted
                || !runtime.RegisterEnemySovereign(new StrategicTargetId(id + ":sovereign"))
                || !runtime.Activate())
                throw new InvalidOperationException("Phase 3 verification match could not activate.");
            return runtime;
        }

        private sealed class AcceptedPlacement : ISovereignAnchorPlacementValidator
        {
            public SovereignAnchorPlacementRejection Validate(SovereignAnchorPlacementRequest request)
                => SovereignAnchorPlacementRejection.None;
        }
    }
}
