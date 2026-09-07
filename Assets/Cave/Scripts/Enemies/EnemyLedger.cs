using System;
using System.Collections.Generic;
using System.Text;

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

        public static string BuildDetailedRosterPage()
        {
            string[] entries =
            {
                "LightBanditBrain|LIGHT BANDIT|MELEE • SKIRMISHER|Fast pressure that punishes retreating players.|Hold ground, parry the committed slash, then punish recovery.",
                "EyeBrain|EYE|RANGED • CONTROL|Gaze and watcher pressure punish prolonged exposure.|Break line of sight and close distance between gaze windows.",
                "BruteBrain|BRUTE|MELEE • FRONTLINE|Heavy armor, slow swings, and a dangerous grab.|GB blocks the Brute's grab. Circle its recovery and punish heavy swings.",
                "WizardBrain|WIZARD|SUPPORT • MAGIC|Heals, buffs, and repositions behind allies.|Pressure the caster, interrupt support casts, and deny safe allies.",
                "DetectiveBrain|DETECTIVE|RANGED • CONTROL|Poison and range control constrain recovery.|Dash out of poison clouds and break line of sight before closing.",
                "NecromancerBrain|NECROMANCER|SUPPORT • SUMMONER|Calls skeleton pressure to protect its casting line.|Interrupt summons, clear minions, then save burst for the caster.",
                "TrollBrain|TROLL|TANK • HEAVY|Deliberate defense and evolved stomp pressure.|Bait the heavy commitment, dodge clear, and punish the landing."
            };

            StringBuilder result = new StringBuilder("<b>ENEMY LEDGER / ROSTER</b>\n<i>Encounter foes to reveal their role and practical counterplay.</i>\n\n");
            for (int index = 0; index < entries.Length; index++)
            {
                string[] parts = entries[index].Split('|');
                if (!IsEncountered(parts[0]))
                {
                    result.Append("<b>???</b>\nEncounter this enemy to reveal its role and counterplay.\n\n");
                    continue;
                }

                result.Append("<b>").Append(parts[1]).Append("</b>\n")
                    .Append("<color=#8ec8e0>").Append(parts[2]).Append("</color>\n")
                    .Append(parts[3]).Append("\n")
                    .Append("<color=#d9ba76>COUNTERPLAY • </color>").Append(parts[4]).Append("\n\n");
            }

            return result.ToString();
        }

        public static string BuildTacticalReferencePage()
        {
            return "<b>SYMBOLS / MEANINGS</b>\n"
                + "<i>Enemy signs, tactical shorthand, and field rules.</i>\n\n"
                + "<b>STATUS EFFECTS</b>\n"
                + "BURNING — takes fire damage over time.\n"
                + "CHILLED — slowed with reduced attack pressure.\n"
                + "POISONED — damage over time; leave poison zones.\n"
                + "STUNNED — unable to act briefly.\n\n"
                + "<b>ENEMY MARKERS</b>\n"
                + "ELITE — stronger than a normal enemy.\n"
                + "SUMMONER — can call or revive minions.\n"
                + "ARMORED — resists damage and stagger.\n"
                + "ENRAGED — increased damage at low health.\n\n"
                + "<b>TACTICAL SHORTHAND</b>\n"
                + "<color=#77c9ef>GB</color> = Guard Break; opens enemy defense.\n"
                + "<color=#d98eff>PP</color> = Perfect Parry; earns a stronger Heavy start.\n"
                + "<color=#e8b15c>LoS</color> = Line of Sight; walls block ranged pressure.\n"
                + "<color=#74d588>DASH</color> = reposition out of area attacks.\n"
                + "<color=#e7a75e>PUNISH</color> = attack after a committed recovery.\n"
                + "POISE = stability; mind stagger before committing.";
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
