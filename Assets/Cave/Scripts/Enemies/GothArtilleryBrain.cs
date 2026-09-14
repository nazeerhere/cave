using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Ranged pressure owner for the Goth. Major actions are mutually exclusive.</summary>
    public sealed class GothArtilleryBrain : MobBrainBase
    {
        [SerializeField, Min(0.1f)] private float preferredRange = 5.4f;
        [SerializeField, Min(0.1f)] private float retreatRange = 3.1f;
        [SerializeField, Min(0f)] private float repositionSpeed = 2.2f;
        private EnemyShooter shooter;
        private GothArtilleryAbilities abilities;
        private EnemyCorruptionLifecycle corruption;
        private int actionCursor;

        protected override void ConfigureCapabilities()
        {
            shooter = shooter != null ? shooter : GetComponent<EnemyShooter>();
            abilities = abilities != null ? abilities : GetComponent<GothArtilleryAbilities>();
            corruption = corruption != null ? corruption : GetComponent<EnemyCorruptionLifecycle>();
            GetComponent<EnemyArchetypeProfile>()?.AddRuntimeArchetype(EnemyArchetype.Ranged);
        }
        protected override void SetCapabilityBrainControl(bool controlled) { shooter?.SetBrainControlled(controlled); }
        protected override bool IsCapabilityBusy() => abilities != null && abilities.IsBusy;
        protected override string BusyDecisionLabel => "Goth artillery action";
        protected override void EvaluateCombat(PlayerHealth player, Vector2 toPlayer)
        {
            float distance = Mathf.Abs(toPlayer.x);
            if (abilities == null) { HoldPosition(MobBrainState.Recover, "Missing Goth abilities"); return; }
            bool corrupted = corruption != null && corruption.IsCorrupted;
            if (distance < retreatRange)
            {
                if (corrupted && abilities.TryRepulse(player.transform)) return;
                Move(-Mathf.Sign(toPlayer.x), repositionSpeed, MobBrainState.Reposition, "Retreat from melee");
                return;
            }
            if (distance > preferredRange + 1.5f) { Move(Mathf.Sign(toPlayer.x), repositionSpeed, MobBrainState.Chase, "Reposition to artillery range"); return; }
            HoldPosition(MobBrainState.Attack, "Artillery cast");
            if (corrupted)
            {
                int choice = actionCursor++ % 5;
                if (choice == 0 && abilities.TryBeam(player.transform)) return;
                if (choice == 1 && abilities.TryFocusOrb(player.transform)) return;
                if (choice == 2 && abilities.TryMeteorStorm(player.transform)) return;
                if (choice == 3 && abilities.TryBurst(player.transform)) return;
                if (choice == 4 && abilities.TryPartitionField(player.transform)) return;
            }
            if (actionCursor++ % 3 == 0 && abilities.TryMeteorFireball(player.transform)) return;
            abilities.TryVolley(player.transform);
        }
    }
}
