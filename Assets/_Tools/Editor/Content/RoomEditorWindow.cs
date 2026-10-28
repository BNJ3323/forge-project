using System.Collections.Generic;
using System.Linq;
using Forge.Runtime;
using UnityEditor;
using UnityEngine;

namespace Forge.EditorTools
{
    /// <summary>
    /// Room Editor : on peint les ennemis et coffres directement sur la grille de la salle.
    /// Clic gauche = placer, clic droit = effacer.
    /// </summary>
    public class RoomEditorWindow : EditorWindow
    {
        private enum Mode { Ennemi, Coffre, Effacer }

        private const float Cell = 26f;

        private RoomData _room;
        private Mode _mode = Mode.Ennemi;
        private EnemyData _enemy;
        private LootTableData _chestTable;
        private Vector2 _scroll;
        private string _message = "";
        private MessageType _type;

        [MenuItem("Forge/Room Editor", false, 11)]
        public static void Open()
        {
            GetWindow<RoomEditorWindow>("Forge – Room Editor").minSize = new Vector2(520, 520);
        }

        private void OnSelectionChange()
        {
            var r = Selection.activeObject as RoomData;
            if (r != null) { _room = r; Repaint(); }
        }

        private void OnGUI()
        {
            DrawHeader();
            if (_room == null)
            {
                EditorGUILayout.HelpBox("Choisissez une salle dans le champ ci-dessus (ou sélectionnez-en une dans le Project), ou créez-en une nouvelle.", MessageType.Info);
                return;
            }

            DrawProperties();
            DrawBrush();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawGrid();
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, _type);
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            _room = (RoomData)EditorGUILayout.ObjectField("Salle", _room, typeof(RoomData), false);
            if (GUILayout.Button("Nouvelle salle", GUILayout.Width(100))) CreateRoom();
            EditorGUILayout.EndHorizontal();
        }

        private void CreateRoom()
        {
            var floors = ForgeAssets.FindAll<RoomData>();
            int floor = 1;
            int index = floors.Count(r => r.floor == floor) + 1;
            bool created;
            string name = ForgeNaming.RoomName(floor, index);
            _room = ForgeAssets.LoadOrCreate<RoomData>(ForgePaths.Rooms, name, out created);
            _room.floor = floor;
            _room.indexInFloor = index;
            ForgeAssets.Dirty(_room);
            AssetDatabase.SaveAssets();
            ForgeMetrics.Record("Création salle", 1, 60f);
            Show("Salle créée : " + name + ". Réglez l'étage et l'index ci-dessous : le nom suit la convention Room_F<étage>_NN.", MessageType.Info);
        }

        private void DrawProperties()
        {
            var so = new SerializedObject(_room);
            so.Update();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(so.FindProperty("floor"));
            EditorGUILayout.PropertyField(so.FindProperty("indexInFloor"), new GUIContent("Index"));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(so.FindProperty("size"));
            EditorGUILayout.PropertyField(so.FindProperty("difficulty"));
            EditorGUILayout.EndHorizontal();
            if (so.ApplyModifiedProperties())
            {
                var s = _room.size;
                _room.size = new Vector2Int(Mathf.Clamp(s.x, 3, 30), Mathf.Clamp(s.y, 3, 30));
                ForgeAssets.Dirty(_room);
            }

            EditorGUILayout.LabelField("Portes (la porte opposée de la salle voisine est créée automatiquement)", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
                DrawDoor(d);
            EditorGUILayout.EndHorizontal();

            var tags = so.FindProperty("tags");
            EditorGUILayout.PropertyField(tags, true);
            so.ApplyModifiedProperties();
        }

        private void DrawDoor(Direction d)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            bool has = _room.HasDoor(d);
            bool want = EditorGUILayout.ToggleLeft(d.ToFrench(), has);
            var target = _room.GetLink(d);
            var newTarget = (RoomData)EditorGUILayout.ObjectField(target, typeof(RoomData), false);

            if (want != has || newTarget != target)
            {
                Undo.RecordObjects(new Object[] { _room }.Concat(target != null ? new Object[] { target } : new Object[0]).Concat(newTarget != null ? new Object[] { newTarget } : new Object[0]).ToArray(), "Modifier porte");
                if (!want) { SetDoor(_room, d, false, null); if (target != null) SetDoor(target, d.Opposite(), false, null); }
                else
                {
                    if (target != null && newTarget != target) SetDoor(target, d.Opposite(), false, null);
                    SetDoor(_room, d, true, newTarget);
                    if (newTarget != null) SetDoor(newTarget, d.Opposite(), true, _room);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private static void SetDoor(RoomData room, Direction d, bool present, RoomData target)
        {
            if (present) room.doors |= d.ToMask(); else room.doors &= ~d.ToMask();
            room.links.RemoveAll(l => l == null || l.direction == d);
            if (present && target != null) room.links.Add(new RoomLink { direction = d, target = target });
            ForgeAssets.Dirty(room);
        }

        private void DrawBrush()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Placer ennemi", "Placer coffre", "Effacer" }, GUILayout.Width(300));
            if (_mode == Mode.Ennemi)
                _enemy = (EnemyData)EditorGUILayout.ObjectField(_enemy, typeof(EnemyData), false);
            else if (_mode == Mode.Coffre)
                _chestTable = (LootTableData)EditorGUILayout.ObjectField(_chestTable, typeof(LootTableData), false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Générer le prefab de la salle", "Crée Assets/_Game/Prefabs/Rooms/<Salle>.prefab")))
            {
                var prefab = RoomPrefabBuilder.BuildAndSave(_room);
                Show("Prefab créé : " + AssetDatabase.GetAssetPath(prefab), MessageType.Info);
                EditorGUIUtility.PingObject(prefab);
            }
            if (GUILayout.Button("Vider les ennemis/coffres"))
            {
                Undo.RecordObject(_room, "Vider la salle");
                _room.enemies.Clear();
                _room.chests.Clear();
                ForgeAssets.Dirty(_room);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Clic gauche : appliquer l'outil · Clic droit : effacer la case", EditorStyles.miniLabel);
        }

        private void DrawGrid()
        {
            var size = _room.size;
            Rect grid = GUILayoutUtility.GetRect(size.x * Cell + 2, size.y * Cell + 2, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(grid, new Color(0.1f, 0.1f, 0.12f));

            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    var r = CellRect(grid, x, y);
                    Color c = ((x + y) % 2 == 0) ? new Color(0.22f, 0.22f, 0.26f) : new Color(0.19f, 0.19f, 0.23f);
                    if (IsDoorCell(x, y)) c = new Color(0.85f, 0.65f, 0.15f);
                    EditorGUI.DrawRect(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), c);
                }

            foreach (var s in _room.enemies)
            {
                if (s == null) continue;
                var r = CellRect(grid, s.cell.x, s.cell.y);
                bool boss = s.enemy != null && s.enemy.isBoss;
                EditorGUI.DrawRect(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), s.enemy == null ? Color.magenta : (boss ? new Color(0.75f, 0.15f, 0.6f) : new Color(0.85f, 0.2f, 0.2f)));
                GUI.Label(r, s.enemy == null ? "?" : Initial(s.enemy.displayName), CenteredWhite());
            }
            foreach (var ch in _room.chests)
            {
                if (ch == null) continue;
                var r = CellRect(grid, ch.cell.x, ch.cell.y);
                EditorGUI.DrawRect(new Rect(r.x + 3, r.y + 5, r.width - 6, r.height - 10), new Color(0.3f, 0.75f, 0.3f));
                GUI.Label(r, "C", CenteredWhite());
            }

            HandleMouse(grid);
        }

        private void HandleMouse(Rect grid)
        {
            var e = Event.current;
            if ((e.type != EventType.MouseDown && e.type != EventType.MouseDrag) || !grid.Contains(e.mousePosition)) return;

            int x = Mathf.FloorToInt((e.mousePosition.x - grid.x) / Cell);
            int row = Mathf.FloorToInt((e.mousePosition.y - grid.y) / Cell);
            int y = _room.size.y - 1 - row;
            if (x < 0 || y < 0 || x >= _room.size.x || y >= _room.size.y) return;
            var cell = new Vector2Int(x, y);

            Undo.RecordObject(_room, "Éditer la salle");
            bool erase = e.button == 1 || _mode == Mode.Effacer;
            _room.enemies.RemoveAll(s => s == null || s.cell == cell);
            _room.chests.RemoveAll(c => c == null || c.cell == cell);

            if (!erase)
            {
                if (_mode == Mode.Ennemi)
                {
                    if (_enemy == null) Show("Choisissez d'abord un ennemi dans le champ à droite de la barre d'outils.", MessageType.Warning);
                    else { _room.enemies.Add(new EnemySpawn { enemy = _enemy, cell = cell }); ForgeMetrics.Record("Placement ennemi/coffre", 1, 15f); }
                }
                else if (_mode == Mode.Coffre)
                {
                    if (_chestTable == null) Show("Choisissez d'abord une table de butin pour le coffre.", MessageType.Warning);
                    else { _room.chests.Add(new ChestSpawn { lootTable = _chestTable, cell = cell }); ForgeMetrics.Record("Placement ennemi/coffre", 1, 15f); }
                }
            }
            ForgeAssets.Dirty(_room);
            e.Use();
            Repaint();
        }

        private bool IsDoorCell(int x, int y)
        {
            int mx = _room.size.x / 2, my = _room.size.y / 2;
            if (_room.HasDoor(Direction.North) && y == _room.size.y - 1 && x == mx) return true;
            if (_room.HasDoor(Direction.South) && y == 0 && x == mx) return true;
            if (_room.HasDoor(Direction.East) && x == _room.size.x - 1 && y == my) return true;
            if (_room.HasDoor(Direction.West) && x == 0 && y == my) return true;
            return false;
        }

        private Rect CellRect(Rect grid, int x, int y)
        {
            return new Rect(grid.x + x * Cell, grid.y + (_room.size.y - 1 - y) * Cell, Cell, Cell);
        }

        private static string Initial(string name)
        {
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }

        private static GUIStyle CenteredWhite()
        {
            var st = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            st.normal.textColor = Color.white;
            return st;
        }

        private void Show(string msg, MessageType t) { _message = msg; _type = t; }
    }

    /// <summary>Automatisation : génère le prefab d'une salle depuis ses données.</summary>
    public static class RoomPrefabBuilder
    {
        public static GameObject BuildAndSave(RoomData room)
        {
            ForgeAssets.EnsureFolder(ForgePaths.RoomPrefabs);
            var root = new GameObject(room.name);

            Create(root.transform, PrimitiveType.Cube, "Floor", new Vector3(room.size.x / 2f, room.size.y / 2f, 0.5f), new Vector3(room.size.x, room.size.y, 0.1f));

            foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
            {
                if (!room.HasDoor(d)) continue;
                var o = d.ToOffset();
                var pos = new Vector3(room.size.x / 2f + o.x * room.size.x / 2f, room.size.y / 2f + o.y * room.size.y / 2f, 0f);
                Create(root.transform, PrimitiveType.Cube, "Door_" + d, pos, new Vector3(1.2f, 1.2f, 0.2f));
            }

            var enemies = new GameObject("Enemies").transform;
            enemies.SetParent(root.transform, false);
            foreach (var s in room.enemies)
            {
                if (s == null || s.enemy == null) continue;
                var pos = new Vector3(s.cell.x + 0.5f, s.cell.y + 0.5f, 0f);
                if (s.enemy.prefab != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(s.enemy.prefab, enemies);
                    inst.transform.position = pos;
                }
                else Create(enemies, PrimitiveType.Cube, s.enemy.name, pos, Vector3.one * (s.enemy.isBoss ? 1.6f : 0.8f));
            }

            var chests = new GameObject("Chests").transform;
            chests.SetParent(root.transform, false);
            foreach (var c in room.chests)
                if (c != null) Create(chests, PrimitiveType.Cube, "Chest", new Vector3(c.cell.x + 0.5f, c.cell.y + 0.5f, 0f), new Vector3(0.9f, 0.6f, 0.2f));

            string path = ForgePaths.RoomPrefabs + "/" + room.name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            ForgeMetrics.Record("Génération prefab de salle", 1, 180f);
            return prefab;
        }

        private static void Create(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        [MenuItem("Forge/Automatisation/Générer les prefabs de toutes les salles", false, 40)]
        public static void BuildAll()
        {
            var rooms = ForgeAssets.FindAll<RoomData>();
            if (rooms.Count == 0) { EditorUtility.DisplayDialog("Prefabs de salles", "Aucune salle trouvée. Générez d'abord un étage.", "OK"); return; }
            for (int i = 0; i < rooms.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Génération des prefabs", rooms[i].name, (float)i / rooms.Count);
                BuildAndSave(rooms[i]);
            }
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Prefabs de salles", rooms.Count + " prefab(s) générés dans " + ForgePaths.RoomPrefabs, "OK");
        }
    }
}
