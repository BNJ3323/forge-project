using System.Collections.Generic;
using UnityEngine;

namespace Forge.Runtime
{
    [CreateAssetMenu(menuName = "Forge/Item", fileName = "Item_New")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("Nom affiché au joueur.")]
        public string displayName;

        [Tooltip("Icône 2D de l'item. Les fichiers Icon_<Nom> dans Art/Icons sont liés automatiquement.")]
        public Sprite icon;

        public Rarity rarity;
        public ItemType type;

        [Header("Stats")]
        public int bonusHealth;
        public int bonusDamage;
        public int bonusDefense;

        [Tooltip("Effets spéciaux (texte libre, un par ligne).")]
        public List<string> effects = new List<string>();

        [Min(0)] public int price = 10;

        [TextArea] public string description;
    }
}
