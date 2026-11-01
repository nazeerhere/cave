using System;
using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.FieldControl;
using Cave.InputSystem;
using Cave.Player;
using UnityEngine;

namespace Cave.Gambit
{
    public enum SovereignTacticalCommandId
    {
        None = 0,
        Probe = 1,
        Guard = 2,
        EstablishLink = 3,
        Surge = 4,
        Sever = 5,
        Reposition = 6
    }

    public enum TacticalCommandExecutionStatus
    {
        Succeeded,
        MatchInactive,
        CommandQueued,
        NoTarget,
        TargetInvalid,
        TargetRecovering,
        InsufficientEnergy,
        LinkRequired,
        DestinationInvalid,
        ChannelAlreadyActive
    }

    public readonly struct TacticalCommandExecutionResult
    {
        public TacticalCommandExecutionResult(TacticalCommandExecutionStatus status,string message)
        { Status=status;Message=message??string.Empty; }
        public TacticalCommandExecutionStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status==TacticalCommandExecutionStatus.Succeeded;
    }

    public readonly struct TacticalCommandDefinition
    {
        public TacticalCommandDefinition(SovereignTacticalCommandId id,string name,string symbol,
            string description,float energy,float recovery,float effect)
        { Id=id;Name=name;Symbol=symbol;Description=description;EnergyCost=energy;RecoverySeconds=recovery;EffectSeconds=effect; }
        public SovereignTacticalCommandId Id { get; }
        public string Name { get; }
        public string Symbol { get; }
        public string Description { get; }
        public float EnergyCost { get; }
        public float RecoverySeconds { get; }
        public float EffectSeconds { get; }
    }

    /// <summary>Pure deterministic four-active/two-queued rotation authority.</summary>
    public sealed class SovereignTacticalCommandHand
    {
        private readonly SovereignTacticalCommandId[] hand = new SovereignTacticalCommandId[4];
        private readonly Queue<SovereignTacticalCommandId> queue = new Queue<SovereignTacticalCommandId>(2);

        public SovereignTacticalCommandHand() { Reset(); }
        public IReadOnlyList<SovereignTacticalCommandId> Hand => Array.AsReadOnly(hand);
        public IReadOnlyList<SovereignTacticalCommandId> Queue => new List<SovereignTacticalCommandId>(queue).AsReadOnly();

        public void Reset()
        {
            hand[0]=SovereignTacticalCommandId.Probe;
            hand[1]=SovereignTacticalCommandId.Guard;
            hand[2]=SovereignTacticalCommandId.EstablishLink;
            hand[3]=SovereignTacticalCommandId.Surge;
            queue.Clear();queue.Enqueue(SovereignTacticalCommandId.Sever);queue.Enqueue(SovereignTacticalCommandId.Reposition);
        }

        public bool IsAvailable(SovereignTacticalCommandId id)
        { for(int i=0;i<hand.Length;i++)if(hand[i]==id)return true;return false; }
        public bool IsQueued(SovereignTacticalCommandId id) => queue.Contains(id);
        public SovereignTacticalCommandId GetHandAt(int index) => index>=0&&index<hand.Length?hand[index]:SovereignTacticalCommandId.None;
        public SovereignTacticalCommandId GetQueueAt(int index)
        {
            if(index<0||index>=queue.Count)return SovereignTacticalCommandId.None;
            int current=0;foreach(SovereignTacticalCommandId id in queue){if(current++==index)return id;}return SovereignTacticalCommandId.None;
        }

        public bool RotateAfterSuccess(SovereignTacticalCommandId executed,out SovereignTacticalCommandId replacement)
        {
            replacement=SovereignTacticalCommandId.None;
            int slot=-1;for(int i=0;i<hand.Length;i++)if(hand[i]==executed){slot=i;break;}
            if(slot<0||queue.Count==0)return false;
            replacement=queue.Dequeue();hand[slot]=replacement;queue.Enqueue(executed);return true;
        }
    }

    public readonly struct TacticalDiskPresentation
    {
        public TacticalDiskPresentation(ulong id,string label,float current,float maximum,bool operational,
            bool connected,float recovery,string effects)
        { CreationOrder=id;Label=label;CurrentEnergy=current;MaximumEnergy=maximum;Operational=operational;Connected=connected;RecoveryRemaining=recovery;ActiveEffects=effects; }
        public ulong CreationOrder { get; }
        public string Label { get; }
        public float CurrentEnergy { get; }
        public float MaximumEnergy { get; }
        public bool Operational { get; }
        public bool Connected { get; }
        public float RecoveryRemaining { get; }
        public string ActiveEffects { get; }
    }

    [DisallowMultipleComponent]
    public sealed class SovereignGambitTacticalCommandController : MonoBehaviour
    {
        [Header("Provisional command tuning")]
        [SerializeField,Min(0f)] private float probeEnergy=.4f;
        [SerializeField,Min(0f)] private float probeRecovery=3f;
        [SerializeField,Min(0f)] private float probeDuration=4f;
        [SerializeField,Min(0f)] private float guardEnergy=.8f;
        [SerializeField,Min(0f)] private float guardRecovery=8f;
        [SerializeField,Min(0f)] private float guardDuration=5f;
        [SerializeField,Min(0f)] private float linkEnergyPerEndpoint=.4f;
        [SerializeField,Min(0f)] private float linkRecovery=8f;
        [SerializeField,Min(0f)] private float linkDuration=6f;
        [SerializeField,Min(0f)] private float severEnergy=.2f;
        [SerializeField,Min(0f)] private float severRecovery=6f;
        [SerializeField,Min(0f)] private float severDuration=6f;
        [SerializeField,Min(0f)] private float surgeEnergy=1.6f;
        [SerializeField,Min(0f)] private float surgeRecovery=12f;
        [SerializeField,Min(0f)] private float surgeDuration=3f;
        [SerializeField,Min(0f)] private float repositionEnergy=1.2f;
        [SerializeField,Min(0f)] private float repositionRecovery=10f;
        [SerializeField,Min(0f)] private float repositionChannel=1f;
        [SerializeField,Min(.1f)] private float repositionTargetRange=6f;
        [SerializeField,Min(.1f)] private float probeRadiusMultiplier=1.5f;
        [SerializeField] private LayerMask relocationGroundLayers=~0;

        private readonly SovereignTacticalCommandHand commandHand=new SovereignTacticalCommandHand();
        private readonly Dictionary<ulong,float> recoveryUntil=new Dictionary<ulong,float>(3);
        private readonly Dictionary<ulong,float> probeUntil=new Dictionary<ulong,float>(3);
        private readonly Damageable[] threatBuffer=new Damageable[128];
        private readonly List<SovereignGambitTrapPiece> pieceBuffer=new List<SovereignGambitTrapPiece>(3);
        private SovereignGambitTrapMatchBridge bridge;
        private SovereignGambitRuntime runtime;
        private PlayerLandmineInventory inventory;
        private SovereignTacticalCommandId selectedCommand=SovereignTacticalCommandId.Probe;
        private ulong selectedPrimary;
        private ulong selectedSecondary;
        private Camera worldCamera;
        private Vector2 previousPointer;
        private Coroutine repositionRoutine;
        private ulong repositioningDisk;
        private int lastPresentationSignature;
        private bool matchActive;

        public event Action StateChanged;
        public event Action<bool> MatchAvailabilityChanged;
        public event Action<string> FeedbackChanged;

        public bool IsMatchActive => matchActive&&runtime!=null&&runtime.Phase==GambitMatchPhase.Active;
        public SovereignTacticalCommandId SelectedCommand => selectedCommand;
        public ulong SelectedPrimaryCreationOrder => selectedPrimary;
        public ulong SelectedSecondaryCreationOrder => selectedSecondary;
        public IReadOnlyList<SovereignTacticalCommandId> AvailableCommands => commandHand.Hand;
        public IReadOnlyList<SovereignTacticalCommandId> QueuedCommands => commandHand.Queue;
        public SovereignTacticalCommandId GetAvailableCommandAt(int index) => commandHand.GetHandAt(index);
        public SovereignTacticalCommandId GetQueuedCommandAt(int index) => commandHand.GetQueueAt(index);
        public string LastFeedback { get; private set; }=string.Empty;

        public bool Bind(SovereignGambitTrapMatchBridge matchBridge)
        {
            if(matchBridge==null||matchBridge.Runtime==null)return false;
            Unbind();bridge=matchBridge;runtime=matchBridge.Runtime;
            inventory=GetComponent<PlayerLandmineInventory>();
            runtime.PhaseChanged+=HandlePhaseChanged;
            HandlePhaseChanged(runtime.Phase);
            return true;
        }

        public void Unbind(SovereignGambitTrapMatchBridge expected)
        { if(expected==null||ReferenceEquals(expected,bridge))Unbind(); }

        public void Unbind()
        {
            if(runtime!=null)runtime.PhaseChanged-=HandlePhaseChanged;
            CleanupMatchState();bridge=null;runtime=null;inventory=null;
        }

        public bool IsCommandAvailable(SovereignTacticalCommandId id) => IsMatchActive&&commandHand.IsAvailable(id);
        public bool IsCommandQueued(SovereignTacticalCommandId id) => IsMatchActive&&commandHand.IsQueued(id);

        public TacticalCommandDefinition GetDefinition(SovereignTacticalCommandId id)
        {
            switch(id)
            {
                case SovereignTacticalCommandId.Probe:return new TacticalCommandDefinition(id,"PROBE","◉","Recon pulse: reveal nearby registered threats.",probeEnergy,probeRecovery,probeDuration);
                case SovereignTacticalCommandId.Guard:return new TacticalCommandDefinition(id,"GUARD","◆","Fortify one disk's pulse and physical defense.",guardEnergy,guardRecovery,guardDuration);
                case SovereignTacticalCommandId.EstablishLink:return new TacticalCommandDefinition(id,"REINFORCE LINK","⌁","Reinforce one existing physical FieldNetwork link.",linkEnergyPerEndpoint*2f,linkRecovery,linkDuration);
                case SovereignTacticalCommandId.Sever:return new TacticalCommandDefinition(id,"SEVER","╳","Suppress one existing physical connection.",severEnergy,severRecovery,severDuration);
                case SovereignTacticalCommandId.Surge:return new TacticalCommandDefinition(id,"SURGE","↯","Accelerate and strengthen actual local pulses.",surgeEnergy,surgeRecovery,surgeDuration);
                case SovereignTacticalCommandId.Reposition:return new TacticalCommandDefinition(id,"REPOSITION","➤","Channel to relocate the existing disk safely.",repositionEnergy,repositionRecovery,repositionChannel);
                default:return new TacticalCommandDefinition(id,"NO COMMAND","—","No tactical command selected.",0f,0f,0f);
            }
        }

        public bool TrySelectCommand(SovereignTacticalCommandId id)
        {
            if(!IsCommandAvailable(id))return false;
            selectedCommand=id;RefreshTargets(true);StateChanged?.Invoke();return true;
        }

        public TacticalCommandExecutionResult TryExecuteSelected()
        { return TryExecute(selectedCommand); }

        public TacticalCommandExecutionResult TryExecute(SovereignTacticalCommandId command)
        {
            TacticalCommandExecutionResult rejection=ValidateCommon(command,out SovereignGambitTrapPiece first);
            if(rejection.Status!=TacticalCommandExecutionStatus.Succeeded)return Report(rejection);
            TacticalCommandDefinition definition=GetDefinition(command);
            SovereignGambitTrapPiece second=null;
            bool linkCommand=command==SovereignTacticalCommandId.EstablishLink||command==SovereignTacticalCommandId.Sever;
            if(linkCommand)
            {
                if(!TryResolvePiece(selectedSecondary,out second)||ReferenceEquals(first,second)
                    ||inventory==null||inventory.FieldNetwork==null
                    ||!inventory.FieldNetwork.AreLinked(first.PhysicalTrap.FieldNode,second.PhysicalTrap.FieldNode))
                    return Report(new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.LinkRequired,"Select two physically connected disks."));
                if(GetRecoveryRemaining(second.PhysicalTrap.CreationOrder)>0f)
                    return Report(new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.TargetRecovering,"Linked disk is recovering."));
            }
            if(command==SovereignTacticalCommandId.Reposition&&repositionRoutine!=null)
                return Report(new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.ChannelAlreadyActive,"Relocation channel already active."));

            Vector2 relocation=default;
            if(command==SovereignTacticalCommandId.Reposition&&!TryResolveRelocation(first.PhysicalTrap,out relocation))
                return Report(new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.DestinationInvalid,"No valid nearby relocation point."));

            if(linkCommand&&command==SovereignTacticalCommandId.EstablishLink)
            {
                if(!first.PhysicalTrap.FieldNode.CanSpendEnergy(linkEnergyPerEndpoint)
                    ||!second.PhysicalTrap.FieldNode.CanSpendEnergy(linkEnergyPerEndpoint))
                    return Report(new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.InsufficientEnergy,"Both endpoints require 0.4 energy."));
                first.PhysicalTrap.FieldNode.TryDrainEnergy(linkEnergyPerEndpoint);
                second.PhysicalTrap.FieldNode.TryDrainEnergy(linkEnergyPerEndpoint);
            }
            else if(!first.PhysicalTrap.FieldNode.TryDrainEnergy(definition.EnergyCost))
                return Report(new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.InsufficientEnergy,"Insufficient disk energy."));

            bool established=ApplyCommand(command,first,second,relocation,definition);
            if(!established)
                return Report(new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.TargetInvalid,"Command effect could not be established."));

            SetRecovery(first.PhysicalTrap.CreationOrder,definition.RecoverySeconds);
            if(command==SovereignTacticalCommandId.EstablishLink&&second!=null)
                SetRecovery(second.PhysicalTrap.CreationOrder,definition.RecoverySeconds);
            SovereignTacticalCommandId replacement;
            commandHand.RotateAfterSuccess(command,out replacement);
            selectedCommand=replacement;
            RefreshTargets(true);
            StateChanged?.Invoke();
            return Report(new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.Succeeded,definition.Name+" executed."));
        }

        private TacticalCommandExecutionResult ValidateCommon(SovereignTacticalCommandId command,out SovereignGambitTrapPiece first)
        {
            first=null;
            if(!IsMatchActive)return new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.MatchInactive,"No active Sovereign Gambit match.");
            if(!commandHand.IsAvailable(command))return new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.CommandQueued,"Command is queued.");
            if(!TryResolvePiece(selectedPrimary,out first))return new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.NoTarget,"Select an operational disk.");
            if(!first.IsPhysicallyOperational)return new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.TargetInvalid,"Selected disk is not operational.");
            if(GetRecoveryRemaining(first.PhysicalTrap.CreationOrder)>0f)return new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.TargetRecovering,"Selected disk is recovering.");
            if(command==SovereignTacticalCommandId.Reposition&&Vector2.Distance(transform.position,first.transform.position)>repositionTargetRange)
                return new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.TargetInvalid,"Disk is outside reposition range.");
            return new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.Succeeded,string.Empty);
        }

        private bool ApplyCommand(SovereignTacticalCommandId command,SovereignGambitTrapPiece first,
            SovereignGambitTrapPiece second,Vector2 relocation,TacticalCommandDefinition definition)
        {
            PlayerLandmine mine=first.PhysicalTrap;
            switch(command)
            {
                case SovereignTacticalCommandId.Probe:probeUntil[mine.CreationOrder]=Time.time+definition.EffectSeconds;EmitProbe(mine,definition.EffectSeconds);return true;
                case SovereignTacticalCommandId.Guard:mine.ApplyTacticalGuard(definition.EffectSeconds);return true;
                case SovereignTacticalCommandId.EstablishLink:
                    return inventory.FieldNetwork.TryReinforceLink(mine.FieldNode,second.PhysicalTrap.FieldNode,
                        definition.EffectSeconds,1.5f,.75f);
                case SovereignTacticalCommandId.Sever:
                    return inventory.FieldNetwork.TrySuppressLink(mine.FieldNode,second.PhysicalTrap.FieldNode,definition.EffectSeconds);
                case SovereignTacticalCommandId.Surge:mine.ApplyTacticalSurge(definition.EffectSeconds);return true;
                case SovereignTacticalCommandId.Reposition:
                    repositioningDisk=mine.CreationOrder;repositionRoutine=StartCoroutine(RelocateAfterChannel(mine,relocation,definition.EffectSeconds));return true;
                default:return false;
            }
        }

        private void EmitProbe(PlayerLandmine mine,float duration)
        {
            float radius=mine.LocalPulseRadius*probeRadiusMultiplier;
            int count=EnemyThreatRegistry.CopyActive(threatBuffer);
            float radiusSquared=radius*radius;
            for(int index=0;index<count;index++)
            {
                Damageable threat=threatBuffer[index];threatBuffer[index]=null;
                if(threat==null||threat.CurrentHealth<=0||threat.GetComponent<EnemyArchetypeProfile>()==null)continue;
                if(((Vector2)threat.transform.position-(Vector2)mine.transform.position).sqrMagnitude>radiusSquared)continue;
                GambitProbeHighlight highlight=threat.GetComponent<GambitProbeHighlight>();
                if(highlight==null)highlight=threat.gameObject.AddComponent<GambitProbeHighlight>();
                highlight.Show(duration);
            }
        }

        private IEnumerator RelocateAfterChannel(PlayerLandmine mine,Vector2 destination,float duration)
        {
            yield return new WaitForSeconds(Mathf.Max(.01f,duration));
            if(IsMatchActive&&mine!=null&&mine.IsOperational&&IsRelocationPointClear(mine,destination))
            {
                mine.transform.position=destination;
                inventory?.FieldNetwork?.NotifyNodeMoved(mine.FieldNode);
                bridge?.RefreshTopology();
            }
            repositionRoutine=null;repositioningDisk=0UL;StateChanged?.Invoke();
        }

        private bool TryResolveRelocation(PlayerLandmine mine,out Vector2 destination)
        {
            destination=mine.transform.position;
            Vector2 requested;
            if(worldCamera==null)worldCamera=Camera.main;
            Vector2 direction=GameInput.ReadRawEquipmentSelectionDirection();
            if(worldCamera!=null)requested=worldCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            else requested=(Vector2)mine.transform.position+(direction.sqrMagnitude>.1f?direction.normalized:Vector2.right)*3f;
            if(Vector2.Distance(transform.position,requested)>repositionTargetRange)return false;
            RaycastHit2D[] hits=Physics2D.RaycastAll(requested+Vector2.up*1.5f,Vector2.down,3f,relocationGroundLayers);
            bool grounded=false;
            for(int index=0;index<hits.Length;index++)
            {
                Collider2D collider=hits[index].collider;
                if(collider==null||collider.isTrigger||collider.transform.IsChildOf(mine.transform)||collider.transform.IsChildOf(transform))continue;
                destination=hits[index].point+Vector2.up*.08f;grounded=true;break;
            }
            return grounded&&IsRelocationPointClear(mine,destination);
        }

        private bool IsRelocationPointClear(PlayerLandmine mine,Vector2 destination)
        {
            Collider2D[] overlaps=Physics2D.OverlapCircleAll(destination+Vector2.up*.18f,.16f,relocationGroundLayers);
            for(int index=0;index<overlaps.Length;index++)
            {
                Collider2D collider=overlaps[index];
                if(collider==null||collider.isTrigger||collider.transform.IsChildOf(mine.transform)||collider.transform.IsChildOf(transform))continue;
                return false;
            }
            return Vector2.Distance(transform.position,destination)<=repositionTargetRange;
        }

        public float GetRecoveryRemaining(ulong creationOrder)
        {
            float until;return recoveryUntil.TryGetValue(creationOrder,out until)?Mathf.Max(0f,until-Time.time):0f;
        }

        public bool TryGetSelectedDisk(out TacticalDiskPresentation presentation)
        {
            SovereignGambitTrapPiece piece;
            if(!TryResolvePiece(selectedPrimary,out piece)){presentation=default;return false;}
            PlayerLandmine mine=piece.PhysicalTrap;FieldNode node=mine.FieldNode;
            presentation=new TacticalDiskPresentation(mine.CreationOrder,ResolveLabel(mine.CreationOrder),
                node!=null?node.CurrentEnergy:0f,node!=null?node.MaximumEnergy:0f,mine.IsOperational,
                piece.IsConnectedToAnchor,GetRecoveryRemaining(mine.CreationOrder),ResolveEffects(mine));return true;
        }

        private string ResolveEffects(PlayerLandmine mine)
        {
            string value=mine.TacticalGuardActive?"GUARD":string.Empty;
            if(mine.TacticalSurgeActive)value+=(value.Length>0?" • ":string.Empty)+"SURGE";
            float probeEnd;if(probeUntil.TryGetValue(mine.CreationOrder,out probeEnd)&&probeEnd>Time.time)
                value+=(value.Length>0?" • ":string.Empty)+"PROBE";
            if(repositioningDisk==mine.CreationOrder)
                value+=(value.Length>0?" • ":string.Empty)+"REPOSITION";
            FieldLinkTacticalEffect linkEffects=inventory!=null&&inventory.FieldNetwork!=null
                ?inventory.FieldNetwork.GetTacticalEffects(mine.FieldNode):FieldLinkTacticalEffect.None;
            if((linkEffects&FieldLinkTacticalEffect.Reinforced)!=0)value+=(value.Length>0?" • ":string.Empty)+"LINK+";
            if((linkEffects&FieldLinkTacticalEffect.Suppressed)!=0)value+=(value.Length>0?" • ":string.Empty)+"SEVER";
            return value.Length>0?value:"NONE";
        }

        private string ResolveLabel(ulong id)
        {
            if(bridge==null)return "—";bridge.CopyPieces(pieceBuffer);
            for(int index=0;index<pieceBuffer.Count;index++)if(pieceBuffer[index].PhysicalTrap.CreationOrder==id)return ((char)('A'+index)).ToString();
            return "—";
        }

        private void SetRecovery(ulong id,float seconds) { recoveryUntil[id]=Time.time+Mathf.Max(0f,seconds); }

        private bool TryResolvePiece(ulong id,out SovereignGambitTrapPiece piece)
        { piece=null;return bridge!=null&&id>0UL&&bridge.TryGetPiece(id,out piece)&&piece!=null; }

        private TacticalCommandExecutionResult Report(TacticalCommandExecutionResult result)
        { LastFeedback=result.Message;FeedbackChanged?.Invoke(LastFeedback);if(!result.Succeeded)StateChanged?.Invoke();return result; }

        private void Update()
        {
            if(!IsMatchActive)return;
            RefreshTargets(false);
            int signature=ComputePresentationSignature();
            if(signature!=lastPresentationSignature){lastPresentationSignature=signature;StateChanged?.Invoke();}
        }

        private void RefreshTargets(bool force)
        {
            if(bridge==null)return;
            bridge.CopyPieces(pieceBuffer);
            if(pieceBuffer.Count==0){SetSelectedTargets(0UL,0UL);return;}
            Vector2 pointer=UnityEngine.Input.mousePosition;
            Vector2 direction=GameInput.ReadRawEquipmentSelectionDirection();
            bool pointerMoved=(pointer-previousPointer).sqrMagnitude>16f;previousPointer=pointer;
            bool linkSelection=selectedCommand==SovereignTacticalCommandId.EstablishLink||selectedCommand==SovereignTacticalCommandId.Sever;
            if(linkSelection&&inventory!=null&&inventory.FieldNetwork!=null)
            {
                if(worldCamera==null)worldCamera=Camera.main;
                Vector2 world=worldCamera!=null?(Vector2)worldCamera.ScreenToWorldPoint(pointer):(Vector2)transform.position;
                float best=float.MaxValue;ulong pairFirst=0UL,pairSecond=0UL;
                for(int firstIndex=0;firstIndex<pieceBuffer.Count;firstIndex++)
                for(int secondIndex=firstIndex+1;secondIndex<pieceBuffer.Count;secondIndex++)
                {
                    SovereignGambitTrapPiece firstPiece=pieceBuffer[firstIndex],secondPiece=pieceBuffer[secondIndex];
                    if(!inventory.FieldNetwork.AreLinked(firstPiece.PhysicalTrap.FieldNode,secondPiece.PhysicalTrap.FieldNode))continue;
                    Vector2 midpoint=((Vector2)firstPiece.transform.position+(Vector2)secondPiece.transform.position)*.5f;
                    float score=pointerMoved?(midpoint-world).sqrMagnitude:
                        direction.sqrMagnitude>.25f?1f-Vector2.Dot(direction.normalized,(midpoint-(Vector2)transform.position).normalized):
                        firstPiece.PhysicalTrap.CreationOrder==selectedPrimary&&secondPiece.PhysicalTrap.CreationOrder==selectedSecondary?-1f:firstIndex*10f+secondIndex;
                    if(score<best){best=score;pairFirst=firstPiece.PhysicalTrap.CreationOrder;pairSecond=secondPiece.PhysicalTrap.CreationOrder;}
                }
                if(force||pairFirst!=selectedPrimary||pairSecond!=selectedSecondary)SetSelectedTargets(pairFirst,pairSecond);
                return;
            }
            SovereignGambitTrapPiece chosen=null;
            if(pointerMoved)
            {
                if(worldCamera==null)worldCamera=Camera.main;
                if(worldCamera!=null)
                {
                    Vector2 world=worldCamera.ScreenToWorldPoint(pointer);float best=float.MaxValue;
                    for(int i=0;i<pieceBuffer.Count;i++){float score=((Vector2)pieceBuffer[i].transform.position-world).sqrMagnitude;if(score<best){best=score;chosen=pieceBuffer[i];}}
                }
            }
            else if(direction.sqrMagnitude>.25f)
            {
                float best=-2f;Vector2 origin=transform.position;
                for(int i=0;i<pieceBuffer.Count;i++){Vector2 delta=((Vector2)pieceBuffer[i].transform.position-origin).normalized;float score=Vector2.Dot(direction.normalized,delta);if(score>best){best=score;chosen=pieceBuffer[i];}}
            }
            if(chosen==null&&!TryResolvePiece(selectedPrimary,out chosen))chosen=pieceBuffer[0];
            ulong primary=chosen!=null?chosen.PhysicalTrap.CreationOrder:0UL;
            ulong secondary=0UL;
            if(force||primary!=selectedPrimary||secondary!=selectedSecondary)SetSelectedTargets(primary,secondary);
        }

        private void SetSelectedTargets(ulong primary,ulong secondary)
        {
            if(bridge!=null)
            {
                bridge.CopyPieces(pieceBuffer);
                for(int i=0;i<pieceBuffer.Count;i++)pieceBuffer[i].PhysicalTrap.SetTacticalSelected(
                    pieceBuffer[i].PhysicalTrap.CreationOrder==primary||pieceBuffer[i].PhysicalTrap.CreationOrder==secondary);
            }
            selectedPrimary=primary;selectedSecondary=secondary;StateChanged?.Invoke();
        }

        private int ComputePresentationSignature()
        {
            unchecked
            {
                int value=(int)selectedPrimary^(int)selectedSecondary;
                TacticalDiskPresentation disk;if(TryGetSelectedDisk(out disk))
                { value=value*397^Mathf.CeilToInt(disk.CurrentEnergy*10f);value=value*397^Mathf.CeilToInt(disk.RecoveryRemaining*10f); }
                return value;
            }
        }

        private void HandlePhaseChanged(GambitMatchPhase phase)
        {
            bool active=phase==GambitMatchPhase.Active;
            if(active&&!matchActive)
            {
                commandHand.Reset();recoveryUntil.Clear();selectedCommand=SovereignTacticalCommandId.Probe;
                matchActive=true;RefreshTargets(true);MatchAvailabilityChanged?.Invoke(true);StateChanged?.Invoke();
            }
            else if(!active&&matchActive)CleanupMatchState();
        }

        private void CleanupMatchState()
        {
            bool wasActive=matchActive;matchActive=false;
            if(repositionRoutine!=null){StopCoroutine(repositionRoutine);repositionRoutine=null;}
            if(bridge!=null)
            {
                bridge.CopyPieces(pieceBuffer);
                for(int i=0;i<pieceBuffer.Count;i++)pieceBuffer[i].PhysicalTrap?.ClearTacticalModifiers();
            }
            inventory?.FieldNetwork?.ClearTemporaryTacticalState();
            recoveryUntil.Clear();probeUntil.Clear();repositioningDisk=0UL;selectedPrimary=0UL;selectedSecondary=0UL;LastFeedback=string.Empty;
            if(wasActive)MatchAvailabilityChanged?.Invoke(false);StateChanged?.Invoke();
        }

        private void OnDisable() { Unbind(); }
    }

    [DisallowMultipleComponent]
    public sealed class GambitProbeHighlight : MonoBehaviour
    {
        private LineRenderer ring;
        private Material material;
        private float visibleUntil;
        public void Show(float duration)
        {
            visibleUntil=Mathf.Max(visibleUntil,Time.time+Mathf.Max(0f,duration));EnsureRing();ring.enabled=true;
        }
        private void Update(){if(ring!=null&&ring.enabled&&Time.time>=visibleUntil)ring.enabled=false;}
        private void EnsureRing()
        {
            if(ring!=null)return;ring=gameObject.AddComponent<LineRenderer>();material=new Material(Shader.Find("Sprites/Default"));ring.material=material;
            ring.loop=true;ring.useWorldSpace=false;ring.positionCount=24;ring.startWidth=.035f;ring.endWidth=.035f;ring.sortingOrder=50;
            Color color=new Color(.15f,.95f,1f,.9f);ring.startColor=color;ring.endColor=color;
            for(int i=0;i<ring.positionCount;i++){float a=i/(float)ring.positionCount*Mathf.PI*2f;ring.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a))*.55f);}
        }
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
