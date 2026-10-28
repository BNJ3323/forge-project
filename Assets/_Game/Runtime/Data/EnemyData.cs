using UnityEngine;

namespace Forge.Runtime
{
    [CreateAssetMenu(menuName = "Forge/Enemy", fileName = "Enemy_New")]
    public class EnemyData : ScriptableObject
    {
        public string displayName;

        [Tooltip("Prefab de l'ennemi. Si vide, un cube de substitution est utilisé.")]
        public GameObject prefab;

        public bool isBoss;
        public EnemyBehavior behavior;

        [Header("Stats de base (étage 1)")]
        [Min(1)] public int baseHealth = 20;
        [Min(0)] public int baseDamage = 3;
        [Min(0)] public float baseSpeed = 2f;

        [Tooltip("Augmentation des PV et dégâts, en %, à chaque étage supplémentaire.")]
        [Min(0)] public float floorScalingPercent = 40f;

        [Header("Apparition")]
        [Range(1, 3)] public int firstFloor = 1;
        [Range(1, 3)] public int lastFloor = 3;

        [Header("Butin")]
        public LootTableData lootTable;

        public int HealthAt(int floor)
        {
            return Mathf.RoundToInt(baseHealth * Scale(floor));
        }

        public int DamageAt(int floor)
        {
            return Mathf.RoundToInt(baseDamage * Scale(floor));
        }

        private float Scale(int floor)
        {
            return 1f + floorScalingPercent / 100f * Mathf.Max(0, floor - 1);
        }
    }
}
