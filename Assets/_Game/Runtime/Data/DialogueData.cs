using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.Runtime
{
    [Serializable]
    public class DialogueChoice
    {
        public string text;
        [Tooltip("Identifiant du noeud suivant. Vide = fin de la conversation.")]
        public string nextNodeId;
        [Tooltip("Condition pour afficher le choix (ex : 'has:Item_RustyKey').")]
        public string condition;
        [Tooltip("Conséquence du choix (ex : 'give:Item_Potion').")]
        public string consequence;
    }

    [Serializable]
    public class DialogueNode
    {
        public string id;
        public string speaker;
        [TextArea] public string text;
        public List<DialogueChoice> choices = new List<DialogueChoice>();
    }

    [CreateAssetMenu(menuName = "Forge/Dialogue", fileName = "Dialogue_New")]
    public class DialogueData : ScriptableObject
    {
        public string npcName;
        public string startNodeId = "start";
        public List<DialogueNode> nodes = new List<DialogueNode>();

        public DialogueNode Find(string id)
        {
            foreach (var n in nodes)
                if (n != null && n.id == id) return n;
            return null;
        }
    }
}
