using System.Collections.Generic;
using UnityEngine;

namespace Cave.UI
{
    public static class NexusSkillArt
    {
        private const string Root = "UI/NexusSkill/Icons/";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Sprite sprite;
            if (Cache.TryGetValue(name, out sprite)) return sprite;
            sprite = Resources.Load<Sprite>(Root + name);
            Cache[name] = sprite;
            return sprite;
        }
    }
}
