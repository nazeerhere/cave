using System;
using System.Collections.Generic;

namespace Cave.Enemies
{
    /// <summary>Session-local discovery data; the menu owns presentation.</summary>
    public static class EnemyLedger
    {
        private static readonly HashSet<string> Encountered = new HashSet<string>();
        private static readonly Dictionary<string, HashSet<string>> WitnessedAbilities =
            new Dictionary<string, HashSet<string>>();

        public static event Action Changed;

        public static void RecordEncounter(MobBrainBase brain)
        {
            if (brain != null && Encountered.Add(brain.GetType().Name))
            {
                Changed?.Invoke();
            }
        }

        public static void RecordAbility(MobBrainBase brain, string abilityId)
        {
            if (brain == null || string.IsNullOrEmpty(abilityId))
            {
                return;
            }

            string enemyId = brain.GetType().Name;
            RecordEncounter(brain);
            if (!WitnessedAbilities.TryGetValue(enemyId, out HashSet<string> abilities))
            {
                abilities = new HashSet<string>();
                WitnessedAbilities[enemyId] = abilities;
            }

            if (abilities.Add(abilityId))
            {
                Changed?.Invoke();
            }
        }

        public static bool IsEncountered(string enemyId) => Encountered.Contains(enemyId);

        public static bool HasWitnessedAbility(string enemyId, string abilityId)
        {
            return WitnessedAbilities.TryGetValue(enemyId, out HashSet<string> abilities)
                && abilities.Contains(abilityId);
        }

        public static string BuildSummary()
        {
            string[] entries =
            {
                "LightBanditBrain|LIGHT BANDIT|Melee skirmisher. Watches for retreats and attacks from movement.",
                "EyeBrain|EYE|Ranged observer. Gaze and watcher pressure punish exposure.",
                "BruteBrain|BRUTE|Frontline control. Guard Break and heavy pressure disrupt defense.",
                "WizardBrain|WIZARD|Support caster. Heals, buffs, and repositions behind allies.",
                "DetectiveBrain|DETECTIVE|Ranged zoning. Poison and range control constrain recovery.",
                "NecromancerBrain|NECROMANCER|Support summoner. Skeleton pressure protects its casting line.",
                "TrollBrain|TROLL|Tank. Deliberate heavy pressure, defense, and evolved stomp."
            };

            string result = "ENEMY LEDGER\n\n";
            foreach (string entry in entries)
            {
                string[] parts = entry.Split('|');
                bool unlocked = IsEncountered(parts[0]);
                result += unlocked
                    ? parts[1] + "\n" + parts[2] + "\n\n"
                    : "???\nEncounter this enemy to reveal its role.\n\n";
            }

            return result;
        }
    }
}
