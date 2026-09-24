using UnityEngine;

namespace Cave.Player
{
    /// <summary>
    /// The Brace-owned resource contribution. It is deliberately separate from
    /// baseline resource regeneration and is evaluated only while a maintained
    /// Full (Brace I) or Deep (Brace II) stage is active.
    /// </summary>
    public struct BraceResourceRecovery
    {
        public BraceResourceRecovery(float stamina, float mana)
        {
            Stamina = stamina;
            Mana = mana;
        }

        public float Stamina { get; }
        public float Mana { get; }
    }

    public static class BraceResourceRecoveryPolicy
    {
        public const float BraceOneStaminaFractionPerSecond = 0.05f;
        public const float BraceTwoStaminaFractionPerSecond = 0.03f;
        public const float BraceTwoManaFractionPerSecond = 0.05f;

        public static BraceResourceRecovery Resolve(
            BraceStage stage,
            float maximumStamina,
            float maximumMana,
            float deltaTime)
        {
            float elapsed = Mathf.Max(0f, deltaTime);
            float stamina = Mathf.Max(0f, maximumStamina);
            float mana = Mathf.Max(0f, maximumMana);

            if (stage == BraceStage.Full)
            {
                return new BraceResourceRecovery(
                    stamina * BraceOneStaminaFractionPerSecond * elapsed,
                    0f);
            }

            if (stage == BraceStage.Deep)
            {
                return new BraceResourceRecovery(
                    stamina * BraceTwoStaminaFractionPerSecond * elapsed,
                    mana * BraceTwoManaFractionPerSecond * elapsed);
            }

            return new BraceResourceRecovery(0f, 0f);
        }
    }
}
