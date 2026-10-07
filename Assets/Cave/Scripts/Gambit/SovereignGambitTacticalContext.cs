using System.Collections.Generic;

namespace Cave.Gambit
{
    /// <summary>Immutable fact-only boundary for an existing tactical system.
    /// It issues no orders and cannot mutate Check, topology, or combat.</summary>
    public sealed class SovereignGambitTacticalContext
    {
        internal SovereignGambitTacticalContext(bool enemyCheck,bool anchorCheck,bool protection,
            IReadOnlyList<StrategicCheckContributorFact> contributors,IReadOnlyList<StrategicConnectorFact> connectors)
        { EnemySovereignInCheck=enemyCheck; PlayerAnchorInCheck=anchorCheck; SovereignProtectionActive=protection; CheckContributors=contributors; Connectors=connectors; }
        public bool EnemySovereignInCheck { get; } public bool PlayerAnchorInCheck { get; } public bool SovereignProtectionActive { get; }
        public IReadOnlyList<StrategicCheckContributorFact> CheckContributors { get; }
        public IReadOnlyList<StrategicConnectorFact> Connectors { get; }
    }
    public static class SovereignGambitTacticalContextFactory
    {
        public static SovereignGambitTacticalContext Create(SovereignGambitRuntime runtime)
        {
            SovereignGambitStrategicAnalysis analysis=runtime!=null?runtime.AnalyzeStrategicState():null;
            return new SovereignGambitTacticalContext(runtime!=null&&runtime.EnemySovereignInCheck,
                runtime!=null&&runtime.PlayerAnchorInCheck,runtime!=null&&runtime.EnemySovereignProtectionActive,
                analysis!=null?analysis.CheckContributors:new List<StrategicCheckContributorFact>().AsReadOnly(),
                analysis!=null?analysis.Connectors:new List<StrategicConnectorFact>().AsReadOnly());
        }
    }
}
