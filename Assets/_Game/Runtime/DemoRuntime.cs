using System.Collections.Generic;
using UnityEngine;

namespace Forge.Runtime
{
    /// <summary>
    /// Runtime minimal : charge et affiche le contenu produit par les outils
    /// (salles, ennemis, coffres, butin, dialogues). Aucune logique de gameplay.
    /// </summary>
    public class DemoRuntime : MonoBehaviour
    {
        public List<FloorData> floors = new List<FloorData>();
        public DialogueData[] dialogues;
        public PlayerProgressionData progression;

        private RoomData _current;
        private FloorData _currentFloor;
        private GameObject _roomRoot;
        private readonly System.Random _rng = new System.Random();
        private string _log = "";
        private DialogueData _activeDialogue;
        private DialogueNode _activeNode;
        private Material _mat;

        private void Start()
        {
            _mat = new Material(Shader.Find("Sprites/Default"));
            if (floors.Count > 0) LoadFloor(floors[0]);
        }

        private void LoadFloor(FloorData floor)
        {
            _currentFloor = floor;
            LoadRoom(floor != null ? floor.entrance : null);
        }

        private void LoadRoom(RoomData room)
        {
            if (_roomRoot != null) Destroy(_roomRoot);
            _current = room;
            if (room == null) return;

            _roomRoot = new GameObject("Room_" + room.name);
            Spawn("Floor", new Vector3(room.size.x / 2f, room.size.y / 2f, 1f),
                  new Vector3(room.size.x, room.size.y, 0.1f), new Color(0.2f, 0.2f, 0.25f));

            foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
            {
                if (!room.HasDoor(d)) continue;
                Vector3 pos = new Vector3(room.size.x / 2f, room.size.y / 2f, 0f);
                Vector2Int o = d.ToOffset();
                pos += new Vector3(o.x * room.size.x / 2f, o.y * room.size.y / 2f, 0f);
                Spawn("Door_" + d, pos, new Vector3(1.2f, 1.2f, 0.1f), new Color(0.9f, 0.7f, 0.2f));
            }

            foreach (var s in room.enemies)
            {
                if (s == null || s.enemy == null) continue;
                Color c = s.enemy.isBoss ? new Color(0.8f, 0.1f, 0.6f) : new Color(0.9f, 0.2f, 0.2f);
                float size = s.enemy.isBoss ? 1.6f : 0.8f;
                if (s.enemy.prefab != null)
                {
                    var go = Instantiate(s.enemy.prefab, _roomRoot.transform);
                    go.transform.position = new Vector3(s.cell.x + 0.5f, s.cell.y + 0.5f, 0f);
                }
                else
                {
                    Spawn(s.enemy.name, new Vector3(s.cell.x + 0.5f, s.cell.y + 0.5f, 0f), Vector3.one * size, c);
                }
            }

            foreach (var ch in room.chests)
                if (ch != null)
                    Spawn("Chest", new Vector3(ch.cell.x + 0.5f, ch.cell.y + 0.5f, 0f),
                          new Vector3(0.9f, 0.6f, 0.1f), new Color(0.3f, 0.8f, 0.3f));

            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(room.size.y, room.size.x * 0.6f) * 0.6f + 1f;
                cam.transform.position = new Vector3(room.size.x / 2f, room.size.y / 2f, -10f);
                cam.backgroundColor = Color.black;
                cam.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        private void Spawn(string objName, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objName;
            go.transform.SetParent(_roomRoot.transform, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.material = new Material(_mat) { color = color };
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 380, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("<b>Forge – Runtime de démonstration</b>", Rich());

            if (_activeDialogue != null) { DrawDialogue(); GUILayout.EndArea(); return; }

            foreach (var f in floors)
                if (f != null && GUILayout.Button("Étage " + f.floorNumber + " (" + f.rooms.Count + " salles)"))
                    LoadFloor(f);

            if (_current != null)
            {
                GUILayout.Space(6);
                GUILayout.Label("Salle : " + _current.name + "  |  difficulté " + _current.difficulty);
                GUILayout.Label("Ennemis : " + DescribeEnemies(_current));
                foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
                {
                    var target = _current.GetLink(d);
                    if (_current.HasDoor(d) && target != null &&
                        GUILayout.Button("Porte " + d.ToFrench() + " → " + target.name))
                        LoadRoom(target);
                }
                foreach (var ch in _current.chests)
                    if (ch != null && ch.lootTable != null && GUILayout.Button("Ouvrir coffre (" + ch.lootTable.name + ")"))
                    {
                        var item = ch.lootTable.Roll(_rng);
                        _log = item != null ? "Butin : " + item.displayName + " [" + item.rarity + "]" : "Coffre vide";
                    }
                foreach (var s in _current.enemies)
                    if (s != null && s.enemy != null && s.enemy.lootTable != null &&
                        GUILayout.Button("Tuer " + s.enemy.displayName + " (butin)"))
                    {
                        var item = s.enemy.lootTable.Roll(_rng);
                        _log = item != null ? "Butin : " + item.displayName + " [" + item.rarity + "]" : "Aucun butin";
                    }
            }

            if (dialogues != null && dialogues.Length > 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("PNJ :");
                foreach (var d in dialogues)
                    if (d != null && GUILayout.Button("Parler à " + d.npcName))
                    {
                        _activeDialogue = d;
                        _activeNode = d.Find(d.startNodeId);
                    }
            }

            GUILayout.Space(6);
            GUILayout.Label(_log);
            GUILayout.EndArea();
        }

        private void DrawDialogue()
        {
            if (_activeNode == null)
            {
                GUILayout.Label("Fin de la conversation.");
                if (GUILayout.Button("Fermer")) _activeDialogue = null;
                return;
            }
            GUILayout.Label("<b>" + _activeNode.speaker + "</b>", Rich());
            GUILayout.Label(_activeNode.text);
            foreach (var c in _activeNode.choices)
                if (c != null && GUILayout.Button(c.text))
                    _activeNode = string.IsNullOrEmpty(c.nextNodeId) ? null : _activeDialogue.Find(c.nextNodeId);
            if (_activeNode != null && _activeNode.choices.Count == 0 && GUILayout.Button("Fermer"))
                _activeDialogue = null;
        }

        private static string DescribeEnemies(RoomData room)
        {
            if (room.enemies.Count == 0) return "aucun";
            var names = new List<string>();
            foreach (var s in room.enemies)
                if (s != null && s.enemy != null) names.Add(s.enemy.displayName);
            return string.Join(", ", names);
        }

        private static GUIStyle Rich()
        {
            return new GUIStyle(GUI.skin.label) { richText = true };
        }
    }
}
