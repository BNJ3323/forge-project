using System;
using UnityEngine;

namespace Forge.Runtime
{
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    public enum ItemType { Weapon, Armor, Accessory, Consumable, Key }

    public enum EnemyBehavior { Melee, Ranged, Charger, Boss }

    public enum Direction { North, East, South, West }

    [Flags]
    public enum DoorMask { None = 0, North = 1, East = 2, South = 4, West = 8 }

    public static class DirectionExtensions
    {
        public static Direction Opposite(this Direction d)
        {
            return (Direction)(((int)d + 2) % 4);
        }

        public static DoorMask ToMask(this Direction d)
        {
            return (DoorMask)(1 << (int)d);
        }

        /// <summary>Décalage dans la grille d'un étage (Nord = +Y).</summary>
        public static Vector2Int ToOffset(this Direction d)
        {
            switch (d)
            {
                case Direction.North: return new Vector2Int(0, 1);
                case Direction.East: return new Vector2Int(1, 0);
                case Direction.South: return new Vector2Int(0, -1);
                default: return new Vector2Int(-1, 0);
            }
        }

        public static string ToFrench(this Direction d)
        {
            switch (d)
            {
                case Direction.North: return "Nord";
                case Direction.East: return "Est";
                case Direction.South: return "Sud";
                default: return "Ouest";
            }
        }
    }
}
