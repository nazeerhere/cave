using System;
using System.Collections.Generic;
using Cave.Domain;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Diagnostics
{
    public enum RuntimeInspectorDetail { Basic = 0, Full = 1 }
    public interface IRuntimeInspectorProvider { string Section { get; } void Draw(RuntimeInspectorDetail detail); }

    /// <summary>Read-only in-game presentation of existing diagnostic owners.
    /// The only writes are the explicit developer controls at the top.</summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeInspector : MonoBehaviour
    {
        private enum Tab { Overview, AiCtc, Knowledge, Domain, ResolverTrace, Performance, PoolsVfx }
        [SerializeField] private bool visible;
        [SerializeField] private RuntimeInspectorDetail detail = RuntimeInspectorDetail.Basic;
        [SerializeField] private GameObject selectedEntity;
        [SerializeField, Min(.1f)] private float refreshInterval = .35f;
        private static readonly List<IRuntimeInspectorProvider> providers = new List<IRuntimeInspectorProvider>(8);
        private readonly List<CombatTacticalMember> members = new List<CombatTacticalMember>();
        private readonly List<LocalizedDomainExecutionCoordinator> coordinators = new List<LocalizedDomainExecutionCoordinator>();
        private float nextRefresh;
        private Rect windowRect = new Rect(12f, 12f, 640f, 690f);
        private Vector2 scroll;
        private Tab tab;
        private int selectedTrace = -1;

        public static RuntimeInspector Active { get; private set; }
        public bool Visible { get => visible && DeveloperDiagnosticsSettings.Available; set => visible = value; }
        public RuntimeInspectorDetail Detail { get => detail; set => detail = value; }
        public static void Register(IRuntimeInspectorProvider provider) { if (provider != null && !providers.Contains(provider)) providers.Add(provider); }
        public static void Unregister(IRuntimeInspectorProvider provider) { if (provider != null) providers.Remove(provider); }
        public void SetSelectedEntity(GameObject entity) { selectedEntity = entity; }

        private void Awake()
        {
            if (Active != null && Active != this) { Destroy(this); return; }
            Active = this;
        }
        private void OnDestroy() { if (Active == this) Active = null; }
        private void Update()
        {
            if (!Visible || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            if (tab == Tab.AiCtc || tab == Tab.Knowledge) RefreshMembers();
            if (tab == Tab.Domain) RefreshDomainCoordinators();
        }
        private void OnGUI()
        {
            if (!Visible) return;
            windowRect.width = Mathf.Min(640f, Screen.width - 24f);
            windowRect.height = Mathf.Min(690f, Screen.height - 24f);
            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "CAVE DEVELOPER DIAGNOSTICS  [F10]");
        }
        private void DrawWindow(int _)
        {
            DrawControls();
            GUILayout.BeginHorizontal();
            DrawTab(Tab.Overview, "OVERVIEW"); DrawTab(Tab.AiCtc, "AI / CTC"); DrawTab(Tab.Knowledge, "KNOWLEDGE");
            DrawTab(Tab.Domain, "DOMAIN"); DrawTab(Tab.ResolverTrace, "RESOLVER TRACE"); DrawTab(Tab.Performance, "PERFORMANCE"); DrawTab(Tab.PoolsVfx, "POOLS / VFX");
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            if (tab == Tab.Overview) DrawOverview();
            else if (tab == Tab.AiCtc) DrawAi();
            else if (tab == Tab.Knowledge) DrawKnowledge();
            else if (tab == Tab.Domain) DrawDomain();
            else if (tab == Tab.ResolverTrace) DrawTrace();
            else if (tab == Tab.Performance) DrawPerformance();
            else DrawPools();
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }
        private void DrawControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CTC " + (DeveloperDiagnosticsSettings.CtcEnabled ? "ON" : "OFF"))) DeveloperDiagnosticsSettings.CtcEnabled = !DeveloperDiagnosticsSettings.CtcEnabled;
            if (GUILayout.Button("Jev " + (DeveloperDiagnosticsSettings.JevEnabled ? "ON" : "OFF"))) DeveloperDiagnosticsSettings.JevEnabled = !DeveloperDiagnosticsSettings.JevEnabled;
            if (GUILayout.Button("Inspector " + DeveloperDiagnosticsSettings.Detail)) DeveloperDiagnosticsSettings.Detail = detail == RuntimeInspectorDetail.Basic ? RuntimeInspectorDetail.Full : RuntimeInspectorDetail.Basic;
            if (GUILayout.Button("Trace " + DeveloperDiagnosticsSettings.TraceMode)) DeveloperDiagnosticsSettings.TraceMode = NextTrace(DeveloperDiagnosticsSettings.TraceMode);
            if (GUILayout.Button("Governor " + GovernorName(DeveloperDiagnosticsSettings.GovernorControl))) DeveloperDiagnosticsSettings.GovernorControl = NextGovernor(DeveloperDiagnosticsSettings.GovernorControl);
            GUILayout.EndHorizontal();
        }
        private void DrawTab(Tab value, string label) { if (GUILayout.Toggle(tab == value, label, GUI.skin.button)) { if (tab != value) { tab = value; scroll = Vector2.zero; } } }
        private void DrawOverview()
        {
            ResourceGovernorSnapshot snapshot = GovernorSnapshot();
            Label("FPS " + snapshot.FramesPerSecond.ToString("0") + "  FRAME " + snapshot.FrameMilliseconds.ToString("0.0") + "ms  GOVERNOR " + snapshot.Mode + (snapshot.IsForced ? " (FORCED)" : " (AUTO)"));
            Label("MOBS " + RuntimeTelemetry.ActiveMobs + "  FORMATIONS " + RuntimeTelemetry.ActiveFormations + "  PROJECTILES " + RuntimeTelemetry.Get(RuntimeWorkCategory.Projectile).Active + "  VFX " + RuntimeTelemetry.Get(RuntimeWorkCategory.Vfx).Active);
            Label("CTC " + (DeveloperDiagnosticsSettings.CtcEnabled ? "ON" : "OFF") + "  JEV " + (DeveloperDiagnosticsSettings.JevEnabled ? "ON" : "OFF") + "  TRACE " + ResolverTraceService.Mode + " (" + ResolverTraceService.Count + "/" + ResolverTraceService.Capacity + ")");
            Label("Use F10 to close. The overlay does not pause gameplay.");
        }
        private void DrawAi()
        {
            RefreshMembersIfNeeded(); DrawEntitySelector(); CombatTacticalMember member = SelectedMember();
            if (member == null) { Label("No registered tactical mob is selected."); return; }
            CombatTacticalFormation formation = member.Formation; MobPsychologySnapshot psyche = member.PsychologySnapshot;
            Label("MOB " + member.MemberId + "  FORMATION " + (formation != null ? formation.FormationId : "none") + "  INTENT " + member.CurrentIntent);
            Label("Aggression " + psyche.Aggression.ToString("0.00") + "  Confidence " + psyche.Confidence.ToString("0.00") + "  Fear " + psyche.Fear.ToString("0.00") + "  Cooperation " + psyche.Cooperation.ToString("0.00"));
            if (formation != null) Label("CTC " + formation.IsCoordinatorEnabled + "  Jev " + formation.IsJevEnabled + "  dirty " + formation.IsPsychologyDirty + "  request " + formation.IsPsychologyRequestInFlight + "  pending evidence " + formation.PendingPsychologyEvidenceCount + "  last " + formation.LastPsychologyInterpretationTime.ToString("0.00"));
            Label("Interpretation source: deterministic fallback unless the formation's optional Jev provider is available.");
        }
        private void DrawKnowledge()
        {
            RefreshMembersIfNeeded(); DrawEntitySelector(); CombatTacticalMember member = SelectedMember();
            if (member == null) { Label("No registered tactical mob is selected."); return; }
            KnowledgeSnapshot knowledge = member.KnowledgeSnapshot;
            Label("WHY DOES " + member.MemberId + " KNOW THIS?  facts=" + knowledge.Facts.Count);
            for (int index = 0; index < knowledge.Facts.Count; index++)
            {
                KnowledgeFact fact = knowledge.Facts[index];
                Label(fact.Type + "  channel=" + fact.Channel + "  subject=" + fact.Subject + "  observer=" + fact.OriginalObserverId + "  age=" + (Time.time - fact.ObservedAt).ToString("0.0") + "  freshness=" + fact.FreshnessAt(Time.time) + "  confidence=" + fact.Confidence.ToString("0.00") + "  provenance=" + fact.ProvenanceId);
            }
        }
        private void DrawDomain()
        {
            RefreshCoordinatorsIfNeeded(); Label("LIVE AXIOM DOMAIN CARRIERS " + DomainRuntimeCarrierRegistry.Count + "  LOCALIZED COORDINATORS " + coordinators.Count);
            PlayerDomainReserve reserve = FindObjectOfType<PlayerDomainReserve>();
            if (reserve != null) Label("PLAYER RESERVE " + reserve.AvailableReserve.ToString("0.00") + "/" + reserve.TotalReserve.ToString("0.00") + "  committed=" + reserve.CommittedReserve.ToString("0.00"));
            for (int index = 0; index < coordinators.Count; index++)
            {
                LocalizedDomainExecutionCoordinator coordinator = coordinators[index]; LocalizedDomainExecutionBatchResult result = coordinator != null ? coordinator.LastResult : null;
                if (result == null) { Label("Localized coordinator pending=" + (coordinator != null ? coordinator.PendingRequestCount.ToString() : "0") + "; no completed batch."); continue; }
                Label("BATCH " + result.GenerationId + "  requests=" + result.RequestCount + "  proposals=" + result.Proposals.Count + "  rejected=" + result.Rejections.Count + "  committed=" + result.Succeeded);
                if (result.Arbitration != null) Label("Arbitration outcomes=" + result.Arbitration.Outcomes.Count + " contested=" + result.Arbitration.HasActualConflicts + " baseline deferrals=" + result.Arbitration.HasBaselineDeferrals);
            }
            Label("Domain presentation is read-only; it never creates an activation or changes a law.");
        }
        private void DrawTrace()
        {
            if (ResolverTraceService.Mode == ResolverTraceMode.Off) { Label("Resolver Trace is OFF. Enable SUMMARY or FULL to capture resolution history."); return; }
            IReadOnlyList<ResolverTraceRecord> records = ResolverTraceService.Snapshot(); Label("TRACE MODE " + ResolverTraceService.Mode + "  newest first  (" + records.Count + "/" + ResolverTraceService.Capacity + ")");
            int last = records.Count - 1; int first = Mathf.Max(0, records.Count - 12);
            for (int index = last; index >= first; index--) { ResolverTraceRecord record = records[index]; if (GUILayout.Button("#" + record.ResolutionId + " parent=" + (record.ParentResolutionId.HasValue ? record.ParentResolutionId.Value.ToString() : "-") + " seq=" + record.OriginatingInteractionSequence + " " + record.RequestKind + " " + record.Phenomenon + " " + record.Operation + " " + (record.Succeeded ? "SUCCESS" : "REJECTED"))) selectedTrace = index; }
            if (selectedTrace < 0 || selectedTrace >= records.Count) return;
            ResolverTraceRecord selected = records[selectedTrace];
            Label("IDENTITY  id=" + selected.ResolutionId + " parent=" + (selected.ParentResolutionId.HasValue ? selected.ParentResolutionId.Value.ToString() : "-") + " interaction=" + selected.OriginatingInteractionSequence + " at=" + selected.Timestamp.ToString("0.000"));
            Label("REQUEST  expression=" + (selected.Expression.HasValue ? selected.Expression.Value.ToString() : "none") + " phenomenon=" + selected.Phenomenon + " operation=" + selected.Operation + " magnitude=" + selected.Magnitude.ToString("0.###") + " result=" + selected.Succeeded + " rejection=" + selected.Rejection);
            Label("LAW EVALUATION candidates=" + selected.Laws.Count + " (FULL mode includes candidates and rejection reasons)");
            for (int index = 0; index < selected.Laws.Count; index++) { ResolverTraceLaw law = selected.Laws[index]; Label("  " + law.Expression + " / " + law.Phenomenon + " / " + law.Territory + " eligible=" + law.Eligible + " rejection=" + law.Rejection + " " + law.Detail); }
            Label("BEFORE / AFTER transitions=" + selected.Transitions.Count);
            for (int index = 0; index < selected.Transitions.Count; index++) Label("  " + selected.Transitions[index].ToString());
            Label("ARBITRATION traces=" + selected.Conflicts.Count);
            for (int index = 0; index < selected.Conflicts.Count; index++) { DomainConflictTrace trace = selected.Conflicts[index]; Label("  jurisdiction=" + trace.JurisdictionStatus + " specificity=" + trace.SpecificityStatus + " authority=" + trace.AuthorityStatus + " complexity=" + trace.ComplexityStatus + " outcome=" + trace.Outcome + " winner=" + (trace.SelectedParticipantId ?? "baseline/stalemate")); }
        }
        private void DrawPerformance()
        {
            ResourceGovernorSnapshot snapshot = GovernorSnapshot();
            Label("Frame " + snapshot.FrameMilliseconds.ToString("0.00") + "ms / " + snapshot.FramesPerSecond.ToString("0.0") + "fps; governor=" + snapshot.Mode + " control=" + DeveloperDiagnosticsSettings.GovernorControl);
            Label("CTC evaluations=" + RuntimeTelemetry.CtcEvaluations + " avg=" + RuntimeTelemetry.AverageCtcMilliseconds.ToString("0.000") + "ms; Resolver evaluations=" + RuntimeTelemetry.ResolverEvaluations + " avg=" + RuntimeTelemetry.AverageResolverMilliseconds.ToString("0.000") + "ms");
            Label("Jev=" + DeveloperDiagnosticsSettings.JevEnabled + " Trace=" + ResolverTraceService.Mode + " Projectiles=" + RuntimeTelemetry.Get(RuntimeWorkCategory.Projectile).Active + " VFX=" + RuntimeTelemetry.Get(RuntimeWorkCategory.Vfx).Active);
        }
        private void DrawPools() { DrawWork("PROJECTILES", RuntimeWorkCategory.Projectile); DrawWork("VFX", RuntimeWorkCategory.Vfx); DrawWork("IMPACTS", RuntimeWorkCategory.Impact); DrawWork("TEMPORARY", RuntimeWorkCategory.TemporaryObject); }
        private void DrawWork(string name, RuntimeWorkCategory category) { RuntimeWorkTelemetry value = RuntimeTelemetry.Get(category); Label(name + " active=" + value.Active + " available=" + value.Available + " peak=" + value.PeakActive + " misses=" + value.Misses + " expansions=" + value.Expansions + " suppressed cosmetic=" + value.SuppressedCosmetic); }
        private void DrawEntitySelector() { GUILayout.BeginHorizontal(); if (GUILayout.Button("<", GUILayout.Width(28f))) MoveSelection(-1); CombatTacticalMember member = SelectedMember(); Label(member != null ? "Selected: " + member.MemberId + " (" + (member.Formation != null ? member.Formation.FormationId : "no formation") + ")" : "Selected: none"); if (GUILayout.Button(">", GUILayout.Width(28f))) MoveSelection(1); GUILayout.EndHorizontal(); }
        private void RefreshMembersIfNeeded() { if (members.Count == 0) RefreshMembers(); }
        private void RefreshMembers() { members.Clear(); CombatTacticalMember[] found = FindObjectsOfType<CombatTacticalMember>(); Array.Sort(found, (left, right) => string.CompareOrdinal(left != null ? left.MemberId : null, right != null ? right.MemberId : null)); for (int index = 0; index < found.Length; index++) if (found[index] != null && found[index].isActiveAndEnabled) members.Add(found[index]); if (SelectedMember() == null && members.Count > 0) selectedEntity = members[0].gameObject; }
        private void RefreshCoordinatorsIfNeeded() { if (coordinators.Count == 0) RefreshDomainCoordinators(); }
        private void RefreshDomainCoordinators() { coordinators.Clear(); LocalizedDomainExecutionCoordinator[] found = FindObjectsOfType<LocalizedDomainExecutionCoordinator>(); for (int index = 0; index < found.Length; index++) if (found[index] != null && found[index].isActiveAndEnabled) coordinators.Add(found[index]); }
        private CombatTacticalMember SelectedMember() { return selectedEntity != null ? selectedEntity.GetComponent<CombatTacticalMember>() : null; }
        private void MoveSelection(int direction) { if (members.Count == 0) return; int current = members.IndexOf(SelectedMember()); int next = current < 0 ? 0 : (current + direction + members.Count) % members.Count; selectedEntity = members[next].gameObject; }
        private static ResourceGovernorSnapshot GovernorSnapshot() { ResourceGovernor governor = ResourceGovernor.Active; return governor != null ? governor.Snapshot : new ResourceGovernorSnapshot(ResourceGovernorMode.Normal, Time.unscaledDeltaTime * 1000f, 1f / Mathf.Max(.0001f, Time.unscaledDeltaTime), false); }
        private static ResolverTraceMode NextTrace(ResolverTraceMode value) { return value == ResolverTraceMode.Off ? ResolverTraceMode.Summary : value == ResolverTraceMode.Summary ? ResolverTraceMode.Full : ResolverTraceMode.Off; }
        private static ResourceGovernorControl NextGovernor(ResourceGovernorControl value) { return value == ResourceGovernorControl.Auto ? ResourceGovernorControl.ForceNormal : value == ResourceGovernorControl.ForceNormal ? ResourceGovernorControl.ForceDegraded : value == ResourceGovernorControl.ForceDegraded ? ResourceGovernorControl.ForceCritical : ResourceGovernorControl.Auto; }
        private static string GovernorName(ResourceGovernorControl value) { return value == ResourceGovernorControl.ForceNormal ? "NORMAL" : value == ResourceGovernorControl.ForceDegraded ? "DEGRADED" : value == ResourceGovernorControl.ForceCritical ? "CRITICAL" : "AUTO"; }
        private static void Label(string text) { GUILayout.Label(text, GUI.skin.label); }
    }
}
