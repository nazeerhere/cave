using System;

namespace Cave.Domain
{
    /// <summary>Focused deterministic coverage for the one-pool Charge/Reserve contract.</summary>
    public static class DomainChargeVerification
    {
        public static void Verify()
        {
            DomainReserveLedger ledger = new DomainReserveLedger(100f);
            Require(Near(ledger.CurrentCharge, 100f) && Near(ledger.Capacity, 100f), "initial charge");

            DomainReserveAllocation allocation;
            Require(ledger.TryAllocate("disk-1", "player", 15f, out allocation)
                == DomainReserveAllocationRejection.None, "default allocation");
            Require(Near(ledger.CurrentCharge, 85f) && Near(ledger.Capacity, 85f)
                && Near(ledger.Committed, 15f), "allocation changes one pool");
            Require(ledger.Reclaim("disk-1") == DomainReserveAllocationRejection.None, "reclaim");
            Require(Near(ledger.Capacity, 100f) && Near(ledger.CurrentCharge, 85f), "reclaim restores capacity only");
            Require(ledger.TryAward("combat:1", 3f) && !ledger.TryAward("combat:1", 3f), "event dedupe");
            Require(Near(ledger.CurrentCharge, 88f), "award amount");
            Require(ledger.TrySpend(5f) && Near(ledger.CurrentCharge, 83f), "drain");
            ledger.ResetCharge();
            Require(Near(ledger.CurrentCharge, 0f), "death reset");
        }

        private static bool Near(float left, float right) { return Math.Abs(left - right) < .001f; }
        private static void Require(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("Domain Charge verification failed: " + name);
        }
    }
}
