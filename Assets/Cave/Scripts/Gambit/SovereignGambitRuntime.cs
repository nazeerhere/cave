using System;
using System.Collections.Generic;
using Cave.Combat;
using UnityEngine;

namespace Cave.Gambit
{
    public readonly struct GambitStrategicEvent
    {
        public GambitStrategicEvent(GambitStrategicEventKind kind,string detail) { Kind=kind;Detail=detail; }
        public GambitStrategicEventKind Kind { get; } public string Detail { get; }
    }
    public sealed class SovereignStrategicProtectionState
    {
        public SovereignStrategicProtectionState(float recoveryDelaySeconds=0f) { RecoveryDelaySeconds=Mathf.Max(0f,recoveryDelaySeconds); IsActive=true; }
        public float RecoveryDelaySeconds { get; } public bool IsActive { get; private set; } private float recoverAt=-1f;
        public void Update(bool inCheck,float now) { if(inCheck){IsActive=false;recoverAt=-1f;return;}if(!IsActive){if(recoverAt<0f)recoverAt=now+RecoveryDelaySeconds;if(now>=recoverAt)IsActive=true;} }
        public void Reset() { IsActive=true;recoverAt=-1f; }
    }

    /// <summary>One match-local authority. It has no static current instance and no scene discovery.</summary>
    public sealed class SovereignGambitRuntime
    {
        private readonly StrategicTerritoryGraph graph=new StrategicTerritoryGraph();
        private readonly Dictionary<string,StrategicInfluence> influences=new Dictionary<string,StrategicInfluence>();
        private readonly Dictionary<string,StrategicDefenseCoverage> blockers=new Dictionary<string,StrategicDefenseCoverage>();
        private readonly Dictionary<StrategicTargetId,StrategicInfluenceRegion> targetRegions=new Dictionary<StrategicTargetId,StrategicInfluenceRegion>();
        private readonly SovereignStrategicProtectionState protection;
        private StrategicTargetId playerAnchorTarget;
        private StrategicTargetId enemySovereignTarget;
        private SovereignAnchorLifecycle anchorLifecycle;
        private bool enemySovereignInCheck;
        private bool playerAnchorInCheck;
        private bool checkmateEmitted;
        public SovereignGambitRuntime(string matchId,float protectionRecoveryDelaySeconds=0f)
        { MatchId=matchId;Phase=GambitMatchPhase.Setup;protection=new SovereignStrategicProtectionState(protectionRecoveryDelaySeconds);anchorLifecycle=SovereignAnchorLifecycle.Unplaced; }
        public string MatchId { get; } public GambitMatchPhase Phase { get; private set; } public StrategicTerritoryGraph TerritoryGraph => graph;
        public StrategicTerritorySnapshot TerritorySnapshot => graph.Snapshot();
        public SovereignAnchorLifecycle AnchorLifecycle => anchorLifecycle; public bool EnemySovereignInCheck => enemySovereignInCheck; public bool PlayerAnchorInCheck => playerAnchorInCheck; public bool EnemySovereignProtectionActive => protection.IsActive; public bool PlayerAnchorValid => anchorLifecycle==SovereignAnchorLifecycle.Placed||anchorLifecycle==SovereignAnchorLifecycle.Active;
        public StrategicTargetId PlayerAnchorTarget => playerAnchorTarget; public StrategicTargetId EnemySovereignTarget => enemySovereignTarget;
        public event Action<GambitStrategicEvent> StrategicEventRaised; public event Action<GambitMatchPhase> PhaseChanged;

        public SovereignAnchorPlacementResult TryPlaceAnchor(SovereignAnchorPlacementRequest request,ISovereignAnchorPlacementValidator validator)
        {
            if(Phase!=GambitMatchPhase.Setup||anchorLifecycle!=SovereignAnchorLifecycle.Unplaced||request.MatchId!=MatchId||!request.RootId.IsValid)return new SovereignAnchorPlacementResult(false,anchorLifecycle!=SovereignAnchorLifecycle.Unplaced?SovereignAnchorPlacementRejection.AlreadyPlaced:SovereignAnchorPlacementRejection.InvalidRequest);
            anchorLifecycle=SovereignAnchorLifecycle.PlacementRequested;SovereignAnchorPlacementRejection rejection=validator==null?SovereignAnchorPlacementRejection.RuntimeUnavailable:validator.Validate(request);
            if(rejection!=SovereignAnchorPlacementRejection.None){anchorLifecycle=SovereignAnchorLifecycle.Unplaced;return new SovereignAnchorPlacementResult(false,rejection);}
            if(!graph.TrySetRoot(new StrategicTerritoryNode(request.RootId,GambitSide.Player,request.Position,true))){anchorLifecycle=SovereignAnchorLifecycle.Unplaced;return new SovereignAnchorPlacementResult(false,SovereignAnchorPlacementRejection.InvalidRequest);}
            playerAnchorTarget=new StrategicTargetId(request.RootId.Value);anchorLifecycle=SovereignAnchorLifecycle.Placed;return new SovereignAnchorPlacementResult(true,SovereignAnchorPlacementRejection.None);
        }
        public bool RegisterEnemySovereign(StrategicTargetId target) => RegisterEnemySovereign(target,default);
        /// <summary>Registers the existing Sovereign target and, when supplied, its strategic region for real influence qualification.</summary>
        public bool RegisterEnemySovereign(StrategicTargetId target,StrategicInfluenceRegion region)
        {
            if(!target.IsValid||enemySovereignTarget.IsValid)return false;
            enemySovereignTarget=target;if(region.IsValid)targetRegions[target]=region;return true;
        }
        public bool SetEnemySovereignRegion(StrategicInfluenceRegion region)
        {
            if(!enemySovereignTarget.IsValid||!region.IsValid)return false;targetRegions[enemySovereignTarget]=region;return true;
        }
        public bool TryGetEnemySovereignRegion(out StrategicInfluenceRegion region) => targetRegions.TryGetValue(enemySovereignTarget,out region);
        public bool Activate() { if(Phase!=GambitMatchPhase.Setup||!PlayerAnchorValid||!enemySovereignTarget.IsValid)return false;anchorLifecycle=SovereignAnchorLifecycle.Active;SetPhase(GambitMatchPhase.Active);return true; }
        public bool RegisterOrUpdateNode(StrategicTerritoryNode node) => Phase==GambitMatchPhase.Active&&graph.AddOrUpdate(node);
        public bool RemoveNode(StrategicNodeId node) { bool changed=graph.Remove(node);if(changed)Emit(GambitStrategicEventKind.StrategicNodeLost,node.ToString());return changed; }
        public bool SetNodeActive(StrategicNodeId node,bool active) { bool changed=graph.SetActive(node,active);if(changed&&!active)Emit(GambitStrategicEventKind.StrategicNodeLost,node.ToString());return changed; }
        public bool ConnectNodes(StrategicNodeId first,StrategicNodeId second) => graph.Connect(first,second);
        public bool DisconnectNodes(StrategicNodeId first,StrategicNodeId second) { bool changed=graph.Disconnect(first,second);if(changed)Emit(GambitStrategicEventKind.StrategicConnectionBroken,first.ToString()+"->"+second.ToString());return changed; }
        public bool RegisterInfluence(StrategicInfluence influence) { if(string.IsNullOrEmpty(influence.SourceId))return false;influences[influence.SourceId]=influence;return true; }
        public bool RemoveInfluence(string sourceId) => !string.IsNullOrEmpty(sourceId)&&influences.Remove(sourceId);
        public bool RegisterBlocker(StrategicDefenseCoverage blocker) { if(string.IsNullOrEmpty(blocker.BlockerId))return false;blockers[blocker.BlockerId]=blocker;return true; }
        public bool RemoveBlocker(string blockerId) => !string.IsNullOrEmpty(blockerId)&&blockers.Remove(blockerId);
        public void Evaluate(float now)
        {
            if(Phase!=GambitMatchPhase.Active)return;
            GambitBattlefieldSnapshot snapshot=new GambitBattlefieldSnapshot(PlayerAnchorValid,playerAnchorTarget,enemySovereignTarget,graph.Snapshot(),Ordered(influences),Ordered(blockers));
            bool previousEnemy=enemySovereignInCheck,previousPlayer=playerAnchorInCheck;
            GambitCheckResolutionResult enemy=SovereignGambitCheckResolver.ResolveEnemySovereign(snapshot);GambitCheckResolutionResult player=SovereignGambitCheckResolver.ResolvePlayerAnchor(snapshot);
            enemySovereignInCheck=enemy.IsCheck;playerAnchorInCheck=player.IsCheck;protection.Update(enemySovereignInCheck,now);
            if(previousEnemy!=enemySovereignInCheck)Emit(enemySovereignInCheck?GambitStrategicEventKind.SovereignCheckStarted:GambitStrategicEventKind.SovereignCheckBroken,"enemy-sovereign");
            if(previousPlayer!=playerAnchorInCheck)Emit(playerAnchorInCheck?GambitStrategicEventKind.PlayerAnchorCheckStarted:GambitStrategicEventKind.PlayerAnchorCheckBroken,"player-anchor");
        }
        public GambitCheckResolutionResult ExplainEnemySovereignCheck() => SovereignGambitCheckResolver.ResolveEnemySovereign(CurrentSnapshot());
        public GambitCheckResolutionResult ExplainPlayerAnchorCheck() => SovereignGambitCheckResolver.ResolvePlayerAnchor(CurrentSnapshot());
        /// <summary>Read-only strategic facts for tactical consumers.  The
        /// runtime remains the sole owner of graph/check state.</summary>
        public SovereignGambitStrategicAnalysis AnalyzeStrategicState() => SovereignGambitStrategicAnalyzer.Analyze(CurrentSnapshot());
        public bool ReportEnemySovereignDefeated()
        {
            if(Phase!=GambitMatchPhase.Active||!enemySovereignInCheck){Emit(GambitStrategicEventKind.InvalidSovereignDefeat,"check-required");return false;}
            if(checkmateEmitted)return false;checkmateEmitted=true;Emit(GambitStrategicEventKind.Checkmate,"enemy-sovereign");SetPhase(GambitMatchPhase.Victory);return true;
        }
        public void InvalidatePlayerAnchor() { if(!PlayerAnchorValid)return;anchorLifecycle=SovereignAnchorLifecycle.Invalidated;playerAnchorInCheck=false;SetPhase(GambitMatchPhase.Defeat); }
        public void End() { if(Phase==GambitMatchPhase.Ended)return;anchorLifecycle=SovereignAnchorLifecycle.MatchEnded;influences.Clear();blockers.Clear();targetRegions.Clear();graph.Clear();enemySovereignInCheck=false;playerAnchorInCheck=false;protection.Reset();playerAnchorTarget=default;enemySovereignTarget=default;SetPhase(GambitMatchPhase.Ended); }
        private GambitBattlefieldSnapshot CurrentSnapshot() => new GambitBattlefieldSnapshot(PlayerAnchorValid,playerAnchorTarget,enemySovereignTarget,graph.Snapshot(),Ordered(influences),Ordered(blockers));
        private static IReadOnlyList<T> Ordered<T>(Dictionary<string,T> values) { List<string> keys=new List<string>(values.Keys);keys.Sort(StringComparer.Ordinal);List<T> result=new List<T>(keys.Count);for(int i=0;i<keys.Count;i++)result.Add(values[keys[i]]);return result.AsReadOnly(); }
        private void SetPhase(GambitMatchPhase phase) { if(Phase==phase)return;Phase=phase;PhaseChanged?.Invoke(phase); }
        private void Emit(GambitStrategicEventKind kind,string detail) { StrategicEventRaised?.Invoke(new GambitStrategicEvent(kind,detail)); }
    }

    /// <summary>Centralized optional gate for a Gambit Sovereign's Damageable. It never changes ordinary enemies.</summary>
    [DisallowMultipleComponent]
    public sealed class SovereignStrategicProtection : MonoBehaviour
    {
        private SovereignGambitRuntime runtime;
        public void Bind(SovereignGambitRuntime value) { runtime=value; }
        public bool IsStrategicallyProtected => runtime!=null&&runtime.EnemySovereignProtectionActive;
        public bool CanReceiveCombatDamageFromPlayer(DamageContext context) => runtime==null||!context.IsPlayerDamage||!runtime.EnemySovereignProtectionActive;
    }

    /// <summary>Opt-in physical Anchor bridge. Its transform supplies only location; topology remains runtime-owned.</summary>
    [DisallowMultipleComponent]
    public sealed class SovereignGambitAnchor : MonoBehaviour
    {
        [SerializeField] private string anchorId;
        [SerializeField] private Vector2 footprint=Vector2.one;
        private SovereignGambitRuntime runtime;
        public SovereignAnchorLifecycle Lifecycle => runtime!=null?runtime.AnchorLifecycle:SovereignAnchorLifecycle.Unplaced;
        public bool TryPlace(SovereignGambitRuntime match,ISovereignAnchorPlacementValidator validator)
        {
            if(match==null||string.IsNullOrEmpty(anchorId))return false;runtime=match;
            return runtime.TryPlaceAnchor(new SovereignAnchorPlacementRequest(runtime.MatchId,new StrategicNodeId(anchorId),transform.position,footprint),validator).Accepted;
        }
        private void OnDestroy() { if(runtime!=null&&runtime.PlayerAnchorValid)runtime.InvalidatePlayerAnchor(); }
    }

    /// <summary>Opt-in physical enemy Sovereign bridge. Damageable remains the health owner; Gambit only validates the defeat.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class SovereignGambitSovereign : MonoBehaviour
    {
        [SerializeField] private string sovereignId;
        [SerializeField,Min(.05f)] private float strategicRegionRadius=1f;
        private SovereignGambitRuntime runtime;
        private Damageable damageable;
        private bool subscribed;
        public bool Bind(SovereignGambitRuntime match)
        {
            if(match==null||string.IsNullOrEmpty(sovereignId))return false;runtime=match;damageable=damageable!=null?damageable:GetComponent<Damageable>();
            SovereignStrategicProtection protection=GetComponent<SovereignStrategicProtection>();if(protection==null)protection=gameObject.AddComponent<SovereignStrategicProtection>();protection.Bind(runtime);
            Subscribe();return runtime.RegisterEnemySovereign(new StrategicTargetId(sovereignId),new StrategicInfluenceRegion(transform.position,strategicRegionRadius));
        }
        public bool RefreshStrategicRegion() => runtime!=null&&runtime.SetEnemySovereignRegion(new StrategicInfluenceRegion(transform.position,strategicRegionRadius));
        private void OnEnable() { Subscribe(); }
        private void OnDisable() { if(subscribed&&damageable!=null)damageable.Died-=HandleDied;subscribed=false; }
        private void Subscribe() { damageable=damageable!=null?damageable:GetComponent<Damageable>();if(subscribed||damageable==null)return;damageable.Died+=HandleDied;subscribed=true; }
        private void HandleDied() { runtime?.ReportEnemySovereignDefeated(); }
    }
}
