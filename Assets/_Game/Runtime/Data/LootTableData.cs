using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.Runtime
{
    [Serializable]
    public class LootEntry
    {
        public ItemData item;
        [Min(0)] public float weight = 1f;
    }

    [CreateAssetMenu(menuName = "Forge/Loot Table", fileName = "Loot_New")]
    public class LootTableData : ScriptableObject
    {
        public List<LootEntry> entries = new List<LootEntry>();

        [Header("Conditions d'apparition")]
        [Range(1, 3)] public int minFloor = 1;
        [Range(1, 3)] public int maxFloor = 3;
        [Tooltip("Condition libre (ex : 'boss', 'coffre'). Informatif.")]
        public string condition;

        public float TotalWeight
        {
            get
            {
                float total = 0f;
                foreach (var e in entries)
                    if (e != null && e.item != null && e.weight > 0f) total += e.weight;
                return total;
            }
        }

        public ItemData Roll(System.Random rng)
        {
            float total = TotalWeight;
            if (total <= 0f) return null;

            float pick = (float)(rng.NextDouble() * total);
            foreach (var e in entries)
            {
                if (e == null || e.item == null || e.weight <= 0f) continue;
                pick -= e.weight;
                if (pick <= 0f) return e.item;
            }
            return null;
        }
    }
}
