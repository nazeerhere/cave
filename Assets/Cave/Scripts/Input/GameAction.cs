namespace Cave.InputSystem
{
    public enum GameAction
    {
        MoveLeft,
        MoveRight,
        Jump,
        BasicAttack,
        ChargedAttack,
        Parry,
        GuardBreak,
        FireProjectile,
        UseHealthPotion,
        UseManaPotion,
        PlaceLandmine,
        UseDistraction,
        Interact,
        SummonCurseAltar,
        Dash,
        Pause,
        // Appended to preserve the serialized values of every established action.
        Nexus,
        // Dedicated manifestation recall. Appended so every established serialized
        // action keeps its original numeric value.
        RecallManifestation,
        // Unified equipment controls. These remain appended so older serialized
        // action values and PlayerPrefs keys never shift.
        SelectEquipment,
        ActivateEquipment
    }

    public enum BindingSlot
    {
        Primary,
        Secondary
    }
}
