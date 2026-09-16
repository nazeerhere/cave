using System;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Single permanent-progression entry point for the Domain Seed. PlayerPrefs
    /// is supported by Unity WebGL browser storage; only the seed unlock is
    /// stored here. Current-run mastery and Domain configuration are not saved.
    /// </summary>
    public static class DomainProgression
    {
        public const string DomainSeedKey = "Cave.Domain.Seed.Acquired";

        public static event Action DomainSeedGranted;

        public static bool HasDomainSeed => PlayerPrefs.GetInt(DomainSeedKey, 0) != 0;

        public static DomainSeed CurrentSeed => new DomainSeed(HasDomainSeed);

        /// <returns>True only when this call newly grants the persistent seed.</returns>
        public static bool GrantDomainSeed()
        {
            if (HasDomainSeed)
            {
                return false;
            }

            PlayerPrefs.SetInt(DomainSeedKey, 1);
            PlayerPrefs.Save();
            DomainSeedGranted?.Invoke();
            return true;
        }
    }
}
