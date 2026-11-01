using Cave.Gambit;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class SovereignGambitTacticalHud : MonoBehaviour
    {
        private GameObject panel;
        private Text[] hand;
        private Text[] queue;
        private Text disk;
        private Text feedback;
        private SovereignGambitTacticalCommandController controller;

        public void Configure(GameObject root,Text[] handLabels,Text[] queueLabels,
            Text diskLabel,Text feedbackLabel,SovereignGambitTacticalCommandController value)
        { panel=root;hand=handLabels;queue=queueLabels;disk=diskLabel;feedback=feedbackLabel;Bind(value);Refresh(); }

        private void Bind(SovereignGambitTacticalCommandController value)
        {
            if(controller!=null)
            {
                controller.StateChanged-=Refresh;
                controller.MatchAvailabilityChanged-=HandleAvailability;
                controller.FeedbackChanged-=HandleFeedback;
            }
            controller=value;
            if(controller!=null)
            {
                controller.StateChanged+=Refresh;
                controller.MatchAvailabilityChanged+=HandleAvailability;
                controller.FeedbackChanged+=HandleFeedback;
            }
        }

        private void HandleAvailability(bool _) { Refresh(); }
        private void HandleFeedback(string value) { if(feedback!=null)feedback.text=value; }

        private void Refresh()
        {
            if(panel==null)return;
            bool active=controller!=null&&controller.IsMatchActive;panel.SetActive(active);if(!active)return;
            for(int i=0;i<hand.Length;i++)
            {
                SovereignTacticalCommandId id=controller.GetAvailableCommandAt(i);
                TacticalCommandDefinition definition=controller.GetDefinition(id);
                hand[i].text=definition.Symbol+"  "+definition.Name+"\n"+definition.EnergyCost.ToString("0.0")+" E";
                hand[i].color=id==controller.SelectedCommand?CaveUiTheme.BorderBright:CaveUiTheme.PrimaryText;
            }
            for(int i=0;i<queue.Length;i++)
            {
                SovereignTacticalCommandId id=controller.GetQueuedCommandAt(i);
                TacticalCommandDefinition definition=controller.GetDefinition(id);
                queue[i].text=(i+1)+"  "+definition.Symbol+" "+definition.Name;
                queue[i].color=CaveUiTheme.SecondaryText;
            }
            TacticalDiskPresentation selected;
            disk.text=controller.TryGetSelectedDisk(out selected)
                ? "DISK "+selected.Label+"   "+selected.CurrentEnergy.ToString("0.0")+" / "+selected.MaximumEnergy.ToString("0.0")+" E"
                    +"\n"+(selected.Operational?"OPERATIONAL":"OFFLINE")+"  •  "+(selected.Connected?"CONNECTED":"ISOLATED")
                    +"  •  REC "+selected.RecoveryRemaining.ToString("0.0")+"s\nEFFECTS: "+selected.ActiveEffects
                : "NO OPERATIONAL DISK TARGET";
            feedback.text=controller.LastFeedback;
        }

        private void OnDestroy(){Bind(null);}
    }
}
