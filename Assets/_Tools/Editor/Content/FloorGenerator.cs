using System.Collections.Generic;
using System.Linq;
using Forge.Runtime;
using UnityEditor;
using UnityEngine;

namespace Forge.EditorTools
{
    public class FloorGenerationSettings
    {
        public int floorNumber = 1;
        public int roomCount = 10;
        public int columns = 5;
        public EnemyData boss;
        public bool populate = true;
        public bool resetExisting = false;
        public int seed = 42;
    }

    /// <summary>
    /// Génère un étage complet : salles nommées selon la convention, portes et liens réciproques,
    /// difficulté progressive, ennemis et coffres placés. Le designer retouche ensuite dans le Room Editor.
    /// </summary>
    public static class FloorGenerator
    {
        public static FloorData Generate(FloorGenerationSettings s)
        {
            int count = Mathf.Clamp(s.roomCount, 2, 30);
            int cols = Mathf.Clamp(s.columns, 1, count);
            var rng = new System.Random(s.seed + s.floorNumber * 1000);

            bool floorCreated;
            var floor = ForgeAssets.LoadOrCreate<FloorData>(ForgePaths.Floors, "Floor_" + s.floorNumber, out floorCreated);
            floor.floorNumber = s.floorNumber;

            var rooms = new List<RoomData>();
            var newRooms = 0;
            for (int i = 0; i < count; i++)
            {
                bool created;
                var room = ForgeAssets.LoadOrCreate<RoomData>(ForgePaths.Rooms, ForgeNaming.RoomName(s.floorNumber, i + 1), out created);
                if (created) newRooms++;
                room.floor = s.floorNumber;
                room.indexInFloor = i + 1;
                bool isBoss = i == count - 1;
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                room.difficulty = isBoss ? 5 : Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1, 3, t)) + (s.floorNumber - 1), 1, 5);
                if (created || s.resetExisting)
                {
                    room.size = isBoss ? new Vector2Int(16, 10) : new Vector2Int(12, 8);
                    room.enemies = new List<EnemySpawn>();
                    room.chests = new List<ChestSpawn>();
                    room.tags = new List<string>();
                    if (i == 0) room.tags.Add("entrance");
                    if (isBoss) room.tags.Add("boss");
                }
                rooms.Add(room);
            }

            // Portes et liens : chaque salle est reliée à ses voisines de la grille (garantit portes réciproques + accessibilité).
            for (int i = 0; i < count; i++)
            {
                var room = rooms[i];
                room.links = new List<RoomLink>();
                room.doors = DoorMask.None;
                int x = i % cols, y = i / cols;
                foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
                {
                    var o = d.ToOffset();
                    int nx = x + o.x, ny = y + o.y;
                    if (nx < 0 || nx >= cols || ny < 0) continue;
                    int ni = ny * cols + nx;
                    if (ni >= count) continue;
                    room.doors |= d.ToMask();
                    room.links.Add(new RoomLink { direction = d, target = rooms[ni] });
                }
            }

            floor.rooms = rooms;
            floor.entrance = rooms[0];

            if (s.populate) Populate(floor, rooms, s, rng);

            foreach (var r in rooms) ForgeAssets.Dirty(r);
            ForgeAssets.Dirty(floor);
            AssetDatabase.SaveAssets();

            ForgeMetrics.Record("Génération d'étage", count, 240f);
            return floor;
        }

        private static void Populate(FloorData floor, List<RoomData> rooms, FloorGenerationSettings s, System.Random rng)
        {
            var pool = ForgeAssets.FindAll<EnemyData>()
                .Where(e => !e.isBoss && e.firstFloor <= s.floorNumber && e.lastFloor >= s.floorNumber).ToList();
            var chestTable = ForgeAssets.FindByName<LootTableData>("Loot_Chest_F" + s.floorNumber);

            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (room.enemies.Count > 0 || room.chests.Count > 0) continue; // ne jamais écraser le travail d'un designer
                bool isBoss = i == rooms.Count - 1;
                var used = new HashSet<Vector2Int>();

                if (isBoss)
                {
                    if (s.boss != null)
                    {
                        var c = new Vector2Int(room.size.x / 2, room.size.y / 2);
                        room.enemies.Add(new EnemySpawn { enemy = s.boss, cell = c });
                        used.Add(c);
                    }
                }
                else if (i > 0 && pool.Count > 0)
                {
                    int n = Mathf.Clamp(room.difficulty + 1, 2, 6);
                    for (int k = 0; k < n; k++)
                    {
                        var cell = FreeCell(room, used, rng);
                        room.enemies.Add(new EnemySpawn { enemy = pool[rng.Next(pool.Count)], cell = cell });
                    }
                }

                if (!isBoss && i > 0 && i % 3 == 0 && chestTable != null)
                    room.chests.Add(new ChestSpawn { lootTable = chestTable, cell = FreeCell(room, used, rng) });
            }
        }

        private static Vector2Int FreeCell(RoomData room, HashSet<Vector2Int> used, System.Random rng)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var c = new Vector2Int(rng.Next(1, Mathf.Max(2, room.size.x - 1)), rng.Next(1, Mathf.Max(2, room.size.y - 1)));
                if (used.Add(c)) return c;
            }
            return new Vector2Int(1, 1);
        }
    }

    public class FloorGeneratorWindow : EditorWindow
    {
        private readonly FloorGenerationSettings _s = new FloorGenerationSettings();
        private string _message = "";
        private MessageType _type;

        [MenuItem("Forge/Générateur d'étage", false, 12)]
        public static void Open()
        {
            GetWindow<FloorGeneratorWindow>("Forge – Générateur d'étage").minSize = new Vector2(380, 320);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Crée en un clic un étage entier : salles nommées Room_F<étage>_NN, portes réciproques, " +
                "difficulté progressive, ennemis et coffres. Retouchez ensuite chaque salle dans le Room Editor.",
                MessageType.Info);

            _s.floorNumber = EditorGUILayout.IntSlider(new GUIContent("Étage", "Numéro de l'étage (1 à 3)."), _s.floorNumber, 1, 3);
            _s.roomCount = EditorGUILayout.IntSlider(new GUIContent("Nombre de salles", "10 par étage dans le GDD."), _s.roomCount, 2, 30);
            _s.columns = EditorGUILayout.IntSlider(new GUIContent("Colonnes de la grille", "Disposition des salles en grille."), _s.columns, 1, 10);
            _s.boss = (EnemyData)EditorGUILayout.ObjectField(new GUIContent("Boss de l'étage", "Placé dans la dernière salle."), _s.boss, typeof(EnemyData), false);
            _s.populate = EditorGUILayout.Toggle(new GUIContent("Placer ennemis et coffres", "Remplit les salles vides (n'écrase jamais une salle déjà remplie)."), _s.populate);
            _s.resetExisting = EditorGUILayout.Toggle(new GUIContent("Réinitialiser les salles existantes", "Vide tailles/ennemis/coffres des salles qui existent déjà."), _s.resetExisting);
            _s.seed = EditorGUILayout.IntField(new GUIContent("Graine aléatoire", "Même graine = même résultat."), _s.seed);

            if (_s.boss != null && !_s.boss.isBoss)
                EditorGUILayout.HelpBox("« " + _s.boss.name + " » n'est pas coché comme boss : la règle « un boss par étage » sera signalée.", MessageType.Warning);

            EditorGUILayout.Space();
            if (GUILayout.Button("Générer l'étage " + _s.floorNumber, GUILayout.Height(30)))
            {
                if (_s.resetExisting && !EditorUtility.DisplayDialog("Réinitialiser ?", "Les salles existantes de cet étage seront vidées. Continuer ?", "Oui", "Annuler")) return;
                var floor = FloorGenerator.Generate(_s);
                _message = "Étage généré : " + floor.name + " (" + floor.rooms.Count + " salles). Lancez la validation pour vérifier.";
                _type = MessageType.Info;
                Selection.activeObject = floor;
            }
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, _type);
        }
    }
}
