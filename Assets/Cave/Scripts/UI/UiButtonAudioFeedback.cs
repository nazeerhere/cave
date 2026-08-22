using Cave.Audio;
using UnityEngine.EventSystems;

namespace Cave.UI
{
    public sealed class UiButtonAudioFeedback : UIBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            CaveSfx.Play(CaveSfxCue.ButtonHover, 0.65f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            CaveSfx.Play(CaveSfxCue.ButtonPress, 0.8f);
        }
    }
}
