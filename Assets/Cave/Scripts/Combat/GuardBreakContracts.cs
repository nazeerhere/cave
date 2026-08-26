using UnityEngine;

namespace Cave.Combat
{
    public enum EnemyGuardBreakState
    {
        Ready,
        Windup,
        CounterWindow,
        Committed,
        Countered,
        Recovering
    }

    public interface ICounterableGuardBreak
    {
        bool IsGuardBreakInProgress { get; }
        bool IsGuardBreakCounterWindowActive { get; }
        bool TryCounterGuardBreak(GameObject counteringPlayer);
    }
}
