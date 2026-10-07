namespace Cave.Gambit
{
    public enum SovereignGambitObjectiveKind { None, ProtectSovereign, DisruptCheckContributor, PressureCriticalConnector, PressureAnchor, HoldStrategicPosition, RecoverStrategicDefense }
    /// <summary>Fact-derived objective only; it cannot mutate Gambit or command actors.</summary>
    public readonly struct SovereignGambitStrategicObjective
    {
        public SovereignGambitStrategicObjective(SovereignGambitObjectiveKind kind,string targetId,int priority){Kind=kind;TargetId=targetId;Priority=priority;}
        public SovereignGambitObjectiveKind Kind { get; } public string TargetId { get; } public int Priority { get; }
    }
    public static class SovereignGambitStrategicDirector
    {
        public static SovereignGambitStrategicObjective Resolve(SovereignGambitTacticalContext context)
        {
            if(context==null)return default;
            if(context.EnemySovereignInCheck&&context.CheckContributors.Count>0)
            {
                StrategicCheckContributorFact contributor=context.CheckContributors[0];
                return new SovereignGambitStrategicObjective(SovereignGambitObjectiveKind.DisruptCheckContributor,contributor.SourceId,100);
            }
            if(context.PlayerAnchorInCheck)return new SovereignGambitStrategicObjective(SovereignGambitObjectiveKind.PressureAnchor,"player-anchor",80);
            return default;
        }
    }
}
