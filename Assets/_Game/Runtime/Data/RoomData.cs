using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.Runtime
{
    [Serializable]
    public class RoomLink
    {
        public Direction direction;
        public RoomData target;
    }

    [Serializable]
    public class EnemySpawn
    {
        public EnemyData enemy;
        public Vector2Int cell;
    }

    [Serializable]
    public class ChestSpawn
    {
        public LootTableData lootTable;
        public Vector2Int cell;
    }

    [CreateAssetMenu(menuName = "Forge/Room", fileName = "Room_F1_01")]
    public class RoomData : ScriptableObject
    {
        [Range(1, 3)] public int floor = 1;
        [Min(1)] public int indexInFloor = 1;

        [Tooltip("Dimensions en cases (largeur x hauteur).")]
        public Vector2Int size = new Vector2Int(12, 8);

        [Tooltip("Portes présentes dans la salle.")]
        public DoorMask doors;

        [Tooltip("Salle atteinte par chaque porte.")]
        public List<RoomLink> links = new List<RoomLink>();

        public List<EnemySpawn> enemies = new List<EnemySpawn>();
        public List<ChestSpawn> chests = new List<ChestSpawn>();

        [Range(1, 5)] public int difficulty = 1;
        public List<string> tags = new List<string>();

        public bool HasDoor(Direction d)
        {
            return (doors & d.ToMask()) != 0;
        }

        public RoomData GetLink(Direction d)
        {
            foreach (var l in links)
                if (l != null && l.direction == d) return l.target;
            return null;
        }

        public bool HasBoss()
        {
            foreach (var s in enemies)
                if (s != null && s.enemy != null && s.enemy.isBoss) return true;
            return false;
        }
    }
}
