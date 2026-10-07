using Cave.Axioms;
using UnityEngine;

namespace Cave.Enemies
{
    [CreateAssetMenu(menuName = "Cave/UI/Axiom Control Overlay Registry", fileName = "AxiomControlOverlayRegistry")]
    public sealed class AxiomControlOverlayRegistry : ScriptableObject
    {
        [SerializeField] private Sprite stateFrame;
        [SerializeField] private Sprite rateFrame;
        [SerializeField] private Sprite accelerationFrame;
        [SerializeField] private Sprite stateBadge;
        [SerializeField] private Sprite rateBadge;
        [SerializeField] private Sprite accelerationBadge;
        [SerializeField] private Sprite upArrow;
        [SerializeField] private Sprite downArrow;
        [SerializeField] private Sprite successMarker;
        [SerializeField] private Sprite failureMarker;

        public Sprite GetFrame(AxiomErrorKind kind)
        {
            switch (kind)
            {
                case AxiomErrorKind.State: return stateFrame;
                case AxiomErrorKind.Rate: return rateFrame;
                case AxiomErrorKind.Acceleration: return accelerationFrame;
                default: return null;
            }
        }

        public Sprite GetMarker(AxiomReactiveMarker marker)
        {
            switch (marker)
            {
                case AxiomReactiveMarker.Up: return upArrow;
                case AxiomReactiveMarker.Down: return downArrow;
                case AxiomReactiveMarker.Success: return successMarker;
                case AxiomReactiveMarker.Failure: return failureMarker;
                default: return null;
            }
        }

        public Sprite GetBadge(AxiomErrorKind kind)
        {
            switch (kind)
            {
                case AxiomErrorKind.State: return stateBadge;
                case AxiomErrorKind.Rate: return rateBadge;
                case AxiomErrorKind.Acceleration: return accelerationBadge;
                default: return null;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(
            Sprite state,
            Sprite rate,
            Sprite acceleration,
            Sprite stateBadgeValue,
            Sprite rateBadgeValue,
            Sprite accelerationBadgeValue,
            Sprite up,
            Sprite down,
            Sprite success,
            Sprite failure)
        {
            stateFrame = state;
            rateFrame = rate;
            accelerationFrame = acceleration;
            stateBadge = stateBadgeValue;
            rateBadge = rateBadgeValue;
            accelerationBadge = accelerationBadgeValue;
            upArrow = up;
            downArrow = down;
            successMarker = success;
            failureMarker = failure;
        }
#endif
    }
}
