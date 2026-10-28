using System.Collections.Generic;
using System.IO;
using System.Linq;
using Forge.Runtime;
using UnityEditor;
using UnityEngine;

namespace Forge.EditorTools
{
    /// <summary>
    /// Data Studio : tableau unique pour créer/éditer les items, ennemis, tables de butin,
    /// avec import/export CSV. Pas besoin d'ouvrir 40 inspecteurs un par un.
    /// </summary>
    public class DataStudioWindow : EditorWindow
    {
        private enum Tab { Items, Ennemis, Butin, Dialogues }

        private Tab _tab;
        private Vector2 _scroll;
        private string _search = "";
        private Rarity _rarityFilter;
        private bool _useRarityFilter;
        private string _newName = "";
        private bool _newBoss;
        private LootTableData _selectedLoot;
        private Rarity _fillMin = Rarity.Common, _fillMax = Rarity.Legendary;
        private readonly Dictionary<Object, SerializedObject> _cache = new Dictionary<Object, SerializedObject>();
        private string _message = "";
        private MessageType _messageType = MessageType.None;

        [MenuItem("Forge/Data Studio", false, 10)]
        public static void Open()
        {
            var w = GetWindow<DataStudioWindow>("Forge – Data Studio");
            w.minSize = new Vector2(900, 400);
        }

        private SerializedObject So(Object o)
        {
            SerializedObject so;
            if (!_cache.TryGetValue(o, out so) || so == null || so.targetObject == null)
            {
                so = new SerializedObject(o);
                _cache[o] = so;
            }
            return so;
        }

        private void OnGUI()
        {
            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Items", "Ennemis", "Tables de butin", "Dialogues (CSV)" }, GUILayout.Height(26));
            EditorGUILayout.Space(4);

            switch (_tab)
            {
                case Tab.Items: DrawItems(); break;
                case Tab.Ennemis: DrawEnemies(); break;
                case Tab.Butin: DrawLoot(); break;
                case Tab.Dialogues: DrawDialogues(); break;
            }

            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, _messageType);
        }

        private void Show(string message, MessageType type)
        {
            _message = message;
            _messageType = type;
        }

        // ------------------------------------------------------------------ Items
        private void DrawItems()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Nom", GUILayout.Width(35));
            _newName = EditorGUILayout.TextField(_newName, GUILayout.Width(180));
            if (GUILayout.Button(new GUIContent("+ Nouvel item", "Crée Item_<Nom> dans " + ForgePaths.Items), EditorStyles.toolbarButton, GUILayout.Width(100)))
                CreateItem();
            GUILayout.Space(10);
            if (GUILayout.Button(new GUIContent("Importer CSV…", "Colonnes : name;rarity;type;price;health;damage;defense;effects;description"), EditorStyles.toolbarButton, GUILayout.Width(100)))
                ImportCsv("Importer des items", ContentImporters.ImportItems, "Items");
            if (GUILayout.Button("Exporter CSV…", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                string path = EditorUtility.SaveFilePanel("Exporter les items", "", "items.csv", "csv");
                if (!string.IsNullOrEmpty(path)) { File.WriteAllText(path, ContentImporters.ExportItems()); Show("Items exportés vers " + path, MessageType.Info); }
            }
            GUILayout.FlexibleSpace();
            _useRarityFilter = GUILayout.Toggle(_useRarityFilter, "Rareté :", EditorStyles.toolbarButton, GUILayout.Width(60));
            _rarityFilter = (Rarity)EditorGUILayout.EnumPopup(_rarityFilter, EditorStyles.toolbarPopup, GUILayout.Width(90));
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();

            var items = ForgeAssets.FindAll<ItemData>()
                .Where(i => (_search == "" || i.name.ToLowerInvariant().Contains(_search.ToLowerInvariant()))
                         && (!_useRarityFilter || i.rarity == _rarityFilter)).ToList();

            EditorGUILayout.BeginHorizontal();
            Header("Icône", 46); Header("Nom affiché", 160); Header("Rareté", 90); Header("Type", 90);
            Header("Prix", 50); Header("PV", 45); Header("Dég.", 45); Header("Déf.", 45); Header("Asset", 160);
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var item in items)
            {
                var so = So(item);
                so.Update();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(so.FindProperty("icon"), GUIContent.none, GUILayout.Width(46), GUILayout.Height(18));
                EditorGUILayout.PropertyField(so.FindProperty("displayName"), GUIContent.none, GUILayout.Width(160));
                EditorGUILayout.PropertyField(so.FindProperty("rarity"), GUIContent.none, GUILayout.Width(90));
                EditorGUILayout.PropertyField(so.FindProperty("type"), GUIContent.none, GUILayout.Width(90));
                EditorGUILayout.PropertyField(so.FindProperty("price"), GUIContent.none, GUILayout.Width(50));
                EditorGUILayout.PropertyField(so.FindProperty("bonusHealth"), GUIContent.none, GUILayout.Width(45));
                EditorGUILayout.PropertyField(so.FindProperty("bonusDamage"), GUIContent.none, GUILayout.Width(45));
                EditorGUILayout.PropertyField(so.FindProperty("bonusDefense"), GUIContent.none, GUILayout.Width(45));
                if (GUILayout.Button(item.name, EditorStyles.linkLabel, GUILayout.Width(160))) { Selection.activeObject = item; EditorGUIUtility.PingObject(item); }
                if (GUILayout.Button(new GUIContent("Dupliquer", "Crée une copie de l'item"), GUILayout.Width(70))) DuplicateItem(item);
                EditorGUILayout.EndHorizontal();
                so.ApplyModifiedProperties();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField(items.Count + " item(s) affiché(s) – objectif GDD : 40", EditorStyles.miniLabel);
        }

        private void CreateItem()
        {
            if (string.IsNullOrWhiteSpace(_newName)) { Show("Saisissez un nom d'item avant de cliquer sur « Nouvel item ».", MessageType.Warning); return; }
            bool created;
            var item = ForgeAssets.LoadOrCreate<ItemData>(ForgePaths.Items, ForgeNaming.ItemName(_newName), out created);
            if (!created) { Show("L'item « " + item.name + " » existe déjà.", MessageType.Warning); return; }
            item.displayName = _newName.Trim();
            ForgeAssets.Dirty(item);
            AssetDatabase.SaveAssets();
            ForgeMetrics.Record("Création item (Data Studio)", 1, 45f);
            _newName = "";
            Show("Item créé : " + item.name, MessageType.Info);
        }

        private void DuplicateItem(ItemData source)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(ForgePaths.Items + "/" + source.name + "_Copy.asset");
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path);
            Show("Copie créée : " + Path.GetFileNameWithoutExtension(path) + ". Renommez l'item puis lancez « Forge > Automatisation > Appliquer la convention de nommage ».", MessageType.Info);
            ForgeMetrics.Record("Duplication item", 1, 30f);
        }

        // --------------------------------------------------------------- Ennemis
        private void DrawEnemies()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Nom", GUILayout.Width(35));
            _newName = EditorGUILayout.TextField(_newName, GUILayout.Width(160));
            _newBoss = GUILayout.Toggle(_newBoss, "Boss", EditorStyles.toolbarButton, GUILayout.Width(45));
            if (GUILayout.Button("+ Nouvel ennemi", EditorStyles.toolbarButton, GUILayout.Width(110))) CreateEnemy();
            GUILayout.Space(10);
            if (GUILayout.Button(new GUIContent("Importer CSV…", "Colonnes : name;boss;behavior;health;damage;speed;scaling;firstfloor;lastfloor;loot"), EditorStyles.toolbarButton, GUILayout.Width(100)))
                ImportCsv("Importer des ennemis", ContentImporters.ImportEnemies, "Ennemis");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            Header("Nom affiché", 130); Header("Boss", 35); Header("Comportement", 85); Header("PV", 45); Header("Dég.", 45);
            Header("Vit.", 40); Header("+%/étage", 60); Header("Étages", 90); Header("Prefab", 110); Header("Butin", 130); Header("PV par étage", 110);
            EditorGUILayout.EndHorizontal();

            var enemies = ForgeAssets.FindAll<EnemyData>();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var e in enemies)
            {
                var so = So(e);
                so.Update();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(so.FindProperty("displayName"), GUIContent.none, GUILayout.Width(130));
                EditorGUILayout.PropertyField(so.FindProperty("isBoss"), GUIContent.none, GUILayout.Width(35));
                EditorGUILayout.PropertyField(so.FindProperty("behavior"), GUIContent.none, GUILayout.Width(85));
                EditorGUILayout.PropertyField(so.FindProperty("baseHealth"), GUIContent.none, GUILayout.Width(45));
                EditorGUILayout.PropertyField(so.FindProperty("baseDamage"), GUIContent.none, GUILayout.Width(45));
                EditorGUILayout.PropertyField(so.FindProperty("baseSpeed"), GUIContent.none, GUILayout.Width(40));
                EditorGUILayout.PropertyField(so.FindProperty("floorScalingPercent"), GUIContent.none, GUILayout.Width(60));
                EditorGUILayout.PropertyField(so.FindProperty("firstFloor"), GUIContent.none, GUILayout.Width(43));
                EditorGUILayout.PropertyField(so.FindProperty("lastFloor"), GUIContent.none, GUILayout.Width(43));
                EditorGUILayout.PropertyField(so.FindProperty("prefab"), GUIContent.none, GUILayout.Width(110));
                EditorGUILayout.PropertyField(so.FindProperty("lootTable"), GUIContent.none, GUILayout.Width(130));
                GUILayout.Label(e.HealthAt(1) + " / " + e.HealthAt(2) + " / " + e.HealthAt(3), GUILayout.Width(110));
                if (GUILayout.Button("→", GUILayout.Width(24))) { Selection.activeObject = e; EditorGUIUtility.PingObject(e); }
                EditorGUILayout.EndHorizontal();
                so.ApplyModifiedProperties();
            }
            EditorGUILayout.EndScrollView();

            int bosses = enemies.Count(x => x.isBoss);
            EditorGUILayout.LabelField((enemies.Count - bosses) + " ennemi(s) + " + bosses + " boss – objectif GDD : 12 + 3", EditorStyles.miniLabel);
        }

        private void CreateEnemy()
        {
            if (string.IsNullOrWhiteSpace(_newName)) { Show("Saisissez un nom d'ennemi avant de cliquer sur « Nouvel ennemi ».", MessageType.Warning); return; }
            bool created;
            var enemy = ForgeAssets.LoadOrCreate<EnemyData>(ForgePaths.Enemies, ForgeNaming.EnemyName(_newName, _newBoss), out created);
            if (!created) { Show("L'ennemi « " + enemy.name + " » existe déjà.", MessageType.Warning); return; }
            enemy.displayName = _newName.Trim();
            enemy.isBoss = _newBoss;
            if (_newBoss) enemy.behavior = EnemyBehavior.Boss;
            ForgeAssets.Dirty(enemy);
            AssetDatabase.SaveAssets();
            ForgeMetrics.Record("Création ennemi (Data Studio)", 1, 60f);
            _newName = "";
            Show("Ennemi créé : " + enemy.name, MessageType.Info);
        }

        // ----------------------------------------------------------------- Butin
        private void DrawLoot()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Nom", GUILayout.Width(35));
            _newName = EditorGUILayout.TextField(_newName, GUILayout.Width(180));
            if (GUILayout.Button("+ Nouvelle table", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                if (string.IsNullOrWhiteSpace(_newName)) Show("Saisissez un nom de table.", MessageType.Warning);
                else
                {
                    bool created;
                    var t = ForgeAssets.LoadOrCreate<LootTableData>(ForgePaths.Loot, ForgeNaming.LootName(_newName), out created);
                    _selectedLoot = t;
                    Show(created ? "Table créée : " + t.name : "Cette table existe déjà, elle est sélectionnée.", MessageType.Info);
                    _newName = "";
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            var tables = ForgeAssets.FindAll<LootTableData>();
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var t in tables)
            {
                var style = t == _selectedLoot ? EditorStyles.boldLabel : EditorStyles.label;
                if (GUILayout.Button(t.name + "  (" + t.entries.Count + ")", style)) _selectedLoot = t;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField(tables.Count + " table(s) – objectif GDD : ≈ 20", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            if (_selectedLoot == null) EditorGUILayout.HelpBox("Sélectionnez une table à gauche.", MessageType.Info);
            else
            {
                var so = So(_selectedLoot);
                so.Update();
                EditorGUILayout.LabelField(_selectedLoot.name, EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(so.FindProperty("minFloor"));
                EditorGUILayout.PropertyField(so.FindProperty("maxFloor"));
                EditorGUILayout.PropertyField(so.FindProperty("condition"));
                EditorGUILayout.PropertyField(so.FindProperty("entries"), true);
                so.ApplyModifiedProperties();

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Poids selon la rareté", "Commun=100, Peu commun=50, Rare=20, Épique=8, Légendaire=2"))) ContentImporters.ApplyRarityWeights(_selectedLoot);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                _fillMin = (Rarity)EditorGUILayout.EnumPopup(_fillMin, GUILayout.Width(90));
                GUILayout.Label("à", GUILayout.Width(12));
                _fillMax = (Rarity)EditorGUILayout.EnumPopup(_fillMax, GUILayout.Width(90));
                if (GUILayout.Button("Remplir avec tous les items de cette plage"))
                {
                    if (_fillMin > _fillMax) Show("La rareté minimale doit être inférieure ou égale à la rareté maximale.", MessageType.Warning);
                    else ContentImporters.FillLootTable(_selectedLoot, _fillMin, _fillMax);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Poids total : " + _selectedLoot.TotalWeight, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        // -------------------------------------------------------------- Dialogues
        private void DrawDialogues()
        {
            EditorGUILayout.HelpBox(
                "Les dialogues s'écrivent dans un tableur (Excel, Sheets). Une ligne = un choix.\n" +
                "Colonnes : npc ; node ; speaker ; text ; choice ; next ; condition ; consequence\n" +
                "Une réplique sans choix = ligne avec 'choice' vide. 'next' vide = fin de conversation.",
                MessageType.Info);
            if (GUILayout.Button("Importer un CSV de dialogues…", GUILayout.Height(28)))
                ImportCsv("Importer des dialogues", ContentImporters.ImportDialogues, "Dialogues");

            var dialogues = ForgeAssets.FindAll<DialogueData>();
            EditorGUILayout.LabelField(dialogues.Count + " PNJ – objectif GDD : 25 (≈ 150 répliques)", EditorStyles.miniLabel);
            foreach (var d in dialogues)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(d.name + " : " + d.nodes.Count + " noeuds");
                if (GUILayout.Button("→", GUILayout.Width(24))) { Selection.activeObject = d; EditorGUIUtility.PingObject(d); }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ---------------------------------------------------------------- Helpers
        private void ImportCsv(string title, System.Func<string, ImportReport> importer, string label)
        {
            string path = EditorUtility.OpenFilePanel(title, ForgePaths.Source, "csv");
            if (string.IsNullOrEmpty(path)) return;
            string text;
            try { text = File.ReadAllText(path); }
            catch (System.Exception ex) { Show("Impossible de lire le fichier : " + ex.Message, MessageType.Error); return; }

            var report = importer(text);
            Show(report.ToText(label), report.errors.Count > 0 ? MessageType.Warning : MessageType.Info);
        }

        private static void Header(string text, float width)
        {
            GUILayout.Label(text, EditorStyles.miniBoldLabel, GUILayout.Width(width));
        }
    }
}
