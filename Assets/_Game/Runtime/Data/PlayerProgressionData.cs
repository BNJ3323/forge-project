using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.Runtime
{
    [Serializable]
    public class LevelEntry
    {
        public int level;
        public int health;
        public int damage;
        [Tooltip("XP cumulée requise pour atteindre ce niveau.")]
        public int xpRequired;
        [Tooltip("Déblocage obtenu à ce niveau (texte libre).")]
        public string unlock;
    }

    [CreateAssetMenu(menuName = "Forge/Player Progression", fileName = "PlayerProgression")]
    public class PlayerProgressionData : ScriptableObject
    {
        public List<LevelEntry> levels = new List<LevelEntry>();

        public LevelEntry Get(int level)
        {
            foreach (var l in levels)
                if (l != null && l.level == level) return l;
            return null;
        }
    }
}
