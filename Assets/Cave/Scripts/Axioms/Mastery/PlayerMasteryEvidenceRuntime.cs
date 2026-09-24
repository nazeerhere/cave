using System;
using Cave.Domain;
using UnityEngine;

namespace Cave.Axioms.Mastery
{
    /// <summary>Single player-owned, run-local authority for immutable Domain mastery evidence.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMasteryEvidenceRuntime : MonoBehaviour
    {
        private PlayerMasteryEvidenceState state = PlayerMasteryEvidenceState.Empty;

        public event Action<PlayerMasteryEvidenceState> Changed;
        public PlayerMasteryEvidenceState Snapshot => state;

        public static PlayerMasteryEvidenceRuntime EnsureOn(GameObject owner)
        {
            return owner == null ? null
                : owner.GetComponent<PlayerMasteryEvidenceRuntime>()
                    ?? owner.AddComponent<PlayerMasteryEvidenceRuntime>();
        }

        public void Submit(PhenomenonMasteryEvidenceSubmission submission, PlayerMasteryPolicy policy)
        {
            if (submission == null) return;
            state = state.Submit(submission, policy ?? PlayerMasteryPolicy.Default);
            DomainProgression.TryAdvanceComplexityCapacity(state, policy ?? PlayerMasteryPolicy.Default);
            Changed?.Invoke(state);
        }

        public void SubmitFrenzy(FrenzyMasterySample sample, PlayerMasteryPolicy policy)
        {
            if (sample == null) return;
            float before = state.FrenzyEvidence;
            state = state.SubmitFrenzy(sample, policy ?? PlayerMasteryPolicy.Default);
            DomainProgression.TryAdvanceComplexityCapacity(state, policy ?? PlayerMasteryPolicy.Default);
#if UNITY_EDITOR
            Debug.Log(
                "[Cave][FrenzyMasteryTrace] evidence submitted"
                + " before=" + before.ToString("0.##")
                + " activeDuration=" + sample.ActiveDuration.ToString("0.##")
                + " criticalHits=" + sample.CriticalHits
                + " kills=" + sample.Kills
                + " after=" + state.FrenzyEvidence.ToString("0.##")
                + " threshold=" + (policy ?? PlayerMasteryPolicy.Default).FrenzyThreshold.ToString("0.##"),
                this);
#endif
            Changed?.Invoke(state);
        }

        /// <summary>Explicit lifecycle seam; no death/reset policy is chosen here.</summary>
        public void ResetForNewRun()
        {
            state = PlayerMasteryEvidenceState.Empty;
            Changed?.Invoke(state);
        }
    }
}
