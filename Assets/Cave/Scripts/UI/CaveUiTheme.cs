using UnityEngine;

namespace Cave.UI
{
    public static class CaveUiTheme
    {
        public static readonly Color Surface = new Color(0.025f, 0.035f, 0.055f, 0.94f);
        public static readonly Color SurfaceRaised = new Color(0.055f, 0.075f, 0.105f, 0.98f);
        public static readonly Color Border = new Color(0.16f, 0.48f, 0.62f, 0.95f);
        public static readonly Color BorderBright = new Color(0.28f, 0.82f, 0.96f, 1f);
        public static readonly Color Gold = new Color(0.96f, 0.72f, 0.25f, 1f);
        public static readonly Color PrimaryText = new Color(0.92f, 0.97f, 1f, 1f);
        public static readonly Color SecondaryText = new Color(0.65f, 0.78f, 0.86f, 1f);

        public static readonly Color Health = new Color(0.9f, 0.18f, 0.15f, 1f);
        public static readonly Color Stamina = new Color(0.18f, 0.78f, 0.92f, 1f);
        public static readonly Color Mana = new Color(0.58f, 0.32f, 0.96f, 1f);
        public static readonly Color Currency = Gold;

        public static readonly Color HealthTrack = new Color(0.20f, 0.055f, 0.065f, 1f);
        public static readonly Color StaminaTrack = new Color(0.045f, 0.14f, 0.18f, 1f);
        public static readonly Color ManaTrack = new Color(0.105f, 0.065f, 0.17f, 1f);
    }
}
