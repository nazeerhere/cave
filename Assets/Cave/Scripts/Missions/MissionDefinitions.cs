using System;
using Cave.World;
using UnityEngine;

namespace Cave.Missions
{
    public enum CaveGameMode { ClassicSweep, CorruptionPurge, CorruptBounty, Containment }
    public enum MissionLifecycleState { None, Setup, Active, PrimaryObjectiveComplete, Extraction, Success, Failure }

    [Serializable]
    public sealed class CaveModeDefinition
    {
        public CaveGameMode mode;
        public string displayName;
        [TextArea] public string description;
        [TextArea] public string objectiveSummary;
        public Sprite icon;
        public bool expectsExtraction;
    }

    [Serializable]
    public sealed class CaveMapDefinition
    {
        public string mapId;
        public string displayName;
        public string sceneName;
        [TextArea] public string description;
        public Sprite thumbnail;
        public int displayOrder;
        public bool initiallyUnlocked;
        [Min(0)] public int requiredWorldLevel;
        public string requiredProgressionFlag;

        public bool IsUnlocked
        {
            get
            {
                if (!string.IsNullOrEmpty(requiredProgressionFlag)
                    && PlayerPrefs.GetInt(requiredProgressionFlag, 0) == 0) return false;
                if (requiredWorldLevel > 0
                    && WorldDifficultyManager.CurrentDifficultyTier < requiredWorldLevel) return false;
                return initiallyUnlocked || requiredWorldLevel > 0 || !string.IsNullOrEmpty(requiredProgressionFlag);
            }
        }

        public string LockReason
        {
            get
            {
                if (IsUnlocked) return string.Empty;
                if (requiredWorldLevel > 0) return "REQUIRES WORLD LEVEL " + requiredWorldLevel;
                return "LOCKED";
            }
        }
    }

}
