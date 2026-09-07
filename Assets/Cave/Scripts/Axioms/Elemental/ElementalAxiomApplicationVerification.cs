using Cave.Combat;

namespace Cave.Axioms.Elemental
{
    /// <summary>Deterministic, Unity-free Batch B application-rule verification.</summary>
    public static class ElementalAxiomApplicationVerification
    {
        public static bool VerifyAll()
        {
            return VerifyFireHit()
                && VerifyFireBlockedAndBurnExcluded()
                && VerifyIceHitAndInvalid()
                && VerifyWindAndStrength()
                && VerifyWrongMode()
                && VerifyFrenzyAmount()
                && VerifyDuplicateReceipt();
        }

        public static bool VerifyFireHit()
        {
            AxiomKind kind;
            bool target;
            float amount;
            return ElementalAxiomApplicationRules.TryResolveProjectile(SpecialMode.BurnShot, true, false, out kind, out target, out amount)
                && kind == AxiomKind.Heat && target && Approximately(amount, 1f);
        }

        public static bool VerifyFireBlockedAndBurnExcluded()
        {
            AxiomKind kind;
            bool target;
            float amount;
            return !ElementalAxiomApplicationRules.TryResolveProjectile(SpecialMode.BurnShot, false, false, out kind, out target, out amount)
                && !ElementalAxiomApplicationRules.TryResolveDirectPlayerHit(SpecialMode.BurnShot, true, false, out kind, out target, out amount);
        }

        public static bool VerifyIceHitAndInvalid()
        {
            AxiomKind kind;
            bool target;
            float amount;
            return ElementalAxiomApplicationRules.TryResolveProjectile(SpecialMode.SlowShot, true, false, out kind, out target, out amount)
                && kind == AxiomKind.Order && target && Approximately(amount, 1f)
                && !ElementalAxiomApplicationRules.TryResolveProjectile(SpecialMode.SlowShot, false, false, out kind, out target, out amount);
        }

        public static bool VerifyWindAndStrength()
        {
            AxiomKind kind;
            bool target;
            float amount;
            return ElementalAxiomApplicationRules.TryResolveDirectPlayerHit(SpecialMode.Flight, true, false, out kind, out target, out amount)
                && kind == AxiomKind.Flow && !target && Approximately(amount, 1f)
                && ElementalAxiomApplicationRules.TryResolveDirectPlayerHit(SpecialMode.DamageBoost, true, false, out kind, out target, out amount)
                && kind == AxiomKind.Mass && !target && Approximately(amount, 1f);
        }

        public static bool VerifyWrongMode()
        {
            AxiomKind kind;
            bool target;
            float amount;
            return !ElementalAxiomApplicationRules.TryResolveDirectPlayerHit(SpecialMode.BurnShot, true, false, out kind, out target, out amount)
                && !ElementalAxiomApplicationRules.TryResolveDirectPlayerHit(SpecialMode.SlowShot, true, false, out kind, out target, out amount);
        }

        public static bool VerifyFrenzyAmount()
        {
            AxiomKind kind;
            bool target;
            float amount;
            return ElementalAxiomApplicationRules.TryResolveProjectile(SpecialMode.BurnShot, true, true, out kind, out target, out amount)
                && Approximately(amount, 2f);
        }

        public static bool VerifyDuplicateReceipt()
        {
            ElementalAxiomApplicationReceipt receipt = new ElementalAxiomApplicationReceipt();
            return receipt.TryClaim(42, AxiomKind.Heat)
                && !receipt.TryClaim(42, AxiomKind.Heat)
                && receipt.TryClaim(42, AxiomKind.Order);
        }

        private static bool Approximately(float left, float right)
        {
            float difference = left - right;
            return difference < .0001f && difference > -.0001f;
        }
    }
}
