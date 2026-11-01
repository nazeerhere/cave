using System;
using Cave.Combat;
using Cave.FieldControl;
using Cave.Player;
using UnityEngine;

namespace Cave.Gambit
{
    public static class SovereignGambitTacticalVerification
    {
        public static bool Run()
        {
            RunPure();
            VerifyDefaults();
            Debug.Log("Sovereign Gambit Tactical Verification: PASS");
            return true;
        }

        public static bool RunPure()
        {
            VerifyRotation();
            VerifyScopedModifiers();
            return true;
        }

        private static void VerifyRotation()
        {
            SovereignTacticalCommandHand hand=new SovereignTacticalCommandHand();
            Require(Sequence(hand.Hand,SovereignTacticalCommandId.Probe,SovereignTacticalCommandId.Guard,
                SovereignTacticalCommandId.EstablishLink,SovereignTacticalCommandId.Surge),"Initial hand mismatch.");
            Require(Sequence(hand.Queue,SovereignTacticalCommandId.Sever,SovereignTacticalCommandId.Reposition),"Initial queue mismatch.");
            SovereignTacticalCommandId replacement;
            Require(hand.RotateAfterSuccess(SovereignTacticalCommandId.Guard,out replacement)&&replacement==SovereignTacticalCommandId.Sever,"Successful rotation failed.");
            Require(Sequence(hand.Hand,SovereignTacticalCommandId.Probe,SovereignTacticalCommandId.Sever,
                SovereignTacticalCommandId.EstablishLink,SovereignTacticalCommandId.Surge),"Occupied hand slot was not replaced.");
            Require(Sequence(hand.Queue,SovereignTacticalCommandId.Reposition,SovereignTacticalCommandId.Guard),"Queue order mismatch after rotation.");
            Require(!hand.RotateAfterSuccess(SovereignTacticalCommandId.Guard,out replacement),"Queued command rotated a second time.");
        }

        private static void VerifyScopedModifiers()
        {
            Require(FieldNode.ResolveGuardedDamage(10,true,.6f,false)==6,"Guard damage reduction mismatch.");
            Require(FieldNode.ResolveGuardedDamage(10,true,.6f,true)==10,"Guard changed periodic damage.");
            Require(Mathf.Approximately(PlayerLandmine.ResolveTacticalPulseForce(10f,true,false),14f),"Guard pulse force mismatch.");
            Require(Mathf.Approximately(PlayerLandmine.ResolveTacticalPulseForce(10f,false,true),15f),"Surge pulse force mismatch.");
            Require(Mathf.Approximately(PlayerLandmine.ResolveTacticalPulseCooldown(1f,true),.5f),"Surge cooldown mismatch.");
            FieldNetwork.FieldLinkIdentity first=new FieldNetwork.FieldLinkIdentity(9UL,3UL);
            FieldNetwork.FieldLinkIdentity second=new FieldNetwork.FieldLinkIdentity(3UL,9UL);
            Require(first.Equals(second)&&first.First==3UL&&first.Second==9UL,"Stable link identity mismatch.");
            Require(Mathf.Approximately(FieldNetwork.ResolveEnergyDrain(.16f,.25f,3),.12f),"Baseline network drain changed.");
        }

        private static void VerifyDefaults()
        {
            GameObject host=new GameObject("Tactical Verification Host");
            try
            {
                SovereignGambitTacticalCommandController controller=host.AddComponent<SovereignGambitTacticalCommandController>();
                RequireCost(controller,SovereignTacticalCommandId.Probe,.4f,3f,4f);
                RequireCost(controller,SovereignTacticalCommandId.Guard,.8f,8f,5f);
                RequireCost(controller,SovereignTacticalCommandId.EstablishLink,.8f,8f,6f);
                RequireCost(controller,SovereignTacticalCommandId.Sever,.2f,6f,6f);
                RequireCost(controller,SovereignTacticalCommandId.Surge,1.6f,12f,3f);
                RequireCost(controller,SovereignTacticalCommandId.Reposition,1.2f,10f,1f);
                Require((int)PlayerEquipmentId.TacticalProbe>2,"Tactical IDs renumbered ordinary equipment.");
            }
            finally
            {
                if(Application.isPlaying)UnityEngine.Object.Destroy(host);
                else UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void RequireCost(SovereignGambitTacticalCommandController controller,
            SovereignTacticalCommandId id,float energy,float recovery,float effect)
        {
            TacticalCommandDefinition definition=controller.GetDefinition(id);
            Require(Mathf.Approximately(definition.EnergyCost,energy)
                &&Mathf.Approximately(definition.RecoverySeconds,recovery)
                &&Mathf.Approximately(definition.EffectSeconds,effect),id+" defaults mismatch.");
        }

        private static bool Sequence(System.Collections.Generic.IReadOnlyList<SovereignTacticalCommandId> values,
            params SovereignTacticalCommandId[] expected)
        {
            if(values.Count!=expected.Length)return false;
            for(int i=0;i<expected.Length;i++)if(values[i]!=expected[i])return false;
            return true;
        }

        private static void Require(bool condition,string message)
        { if(!condition)throw new InvalidOperationException(message); }
    }
}
