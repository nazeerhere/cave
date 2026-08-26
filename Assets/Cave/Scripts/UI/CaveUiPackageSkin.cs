using UnityEngine;

namespace Cave.UI
{
    [CreateAssetMenu(fileName = "CaveUiPackageSkin", menuName = "Cave/UI/Package Skin")]
    public sealed class CaveUiPackageSkin : ScriptableObject
    {
        [Header("Free UI Build Package Elements")]
        [SerializeField] private Sprite horizontalFrame;
        [SerializeField] private Sprite panelFrame;
        [SerializeField] private Sprite iconFrame;
        [SerializeField] private Sprite gem;
        [SerializeField] private Sprite longRod;

        public Sprite HorizontalFrame => horizontalFrame;
        public Sprite PanelFrame => panelFrame;
        public Sprite IconFrame => iconFrame;
        public Sprite Gem => gem;
        public Sprite LongRod => longRod;
    }
}
