using System.Collections.Generic;
using System.Linq;
using Forge.Runtime;
using UnityEditor;
using UnityEngine;

namespace Forge.EditorTools
{
    /// <summary>Équilibrage : courbes de progression, temps pour tuer (TTK) et simulateur de tables de butin.</summary>
    public class BalanceWindow : EditorWindow
    {
        private enum Tab { Progression, Combat, Butin }

        private Tab _tab;
        private PlayerProgressionData _prog;
        private Vector2 _scroll;

        // Générateur de progression
        private int _baseHp = 50, _baseDmg = 5, _baseXp = 40, _levels = 20;
        private float _hpGrowth = 12f, _dmgGrowth = 9f, _xpExp = 1.6f;

        // Simulateur de butin
        private LootTableData _loot;
        private int _rolls = 10000;
        private int[] _rarityCounts;
        private List<KeyValuePair<ItemData, int>> _top;

        [MenuItem("Forge/Équilibrage", false, 30)]
        public static void Open()
        {
            GetWindow<BalanceWindow>("Forge – Équilibrage").minSize = new Vector2(620, 480);
        }

        private void OnEnable()
        {
            if (_prog == null) _prog = ForgeAssets.FindAll<PlayerProgressionData>().FirstOrDefault();
        }

        private void OnGUI()
        {
            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Progression du joueur", "Combat par étage", "Simulateur de butin" }, GUILayout.Height(26));
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Progression: DrawProgression(); break;
                case Tab.Combat: DrawCombat(); break;
                case Tab.Butin: DrawLoot(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------ Progression
        private void DrawProgression()
        {
            _prog = (PlayerProgressionData)EditorGUILayout.ObjectField("Progression", _prog, typeof(PlayerProgressionData), false);
            if (_prog == null)
            {
                if (GUILayout.Button("Créer l'asset PlayerProgression"))
                {
                    bool created;
                    _prog = ForgeAssets.LoadOrCreate<PlayerProgressionData>(ForgePaths.Progression, "PlayerProgression", out created);
                }
                return;
            }

            EditorGUILayout.LabelField("Générer les niveaux depuis une formule", EditorStyles.boldLabel);
            _levels = EditorGUILayout.IntSlider("Niveaux", _levels, 2, 40);
            _baseHp = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("PV niveau 1", "PV de départ."), _baseHp));
            _hpGrowth = Mathf.Max(0, EditorGUILayout.FloatField(new GUIContent("Croissance PV (%/niveau)", "Multiplicateur composé par niveau."), _hpGrowth));
            _baseDmg = Mathf.Max(0, EditorGUILayout.IntField("Dégâts niveau 1", _baseDmg));
            _dmgGrowth = Mathf.Max(0, EditorGUILayout.FloatField("Croissance dégâts (%/niveau)", _dmgGrowth));
            _baseXp = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("XP de base", "XP cumulée = base × (niveau-1)^exposant"), _baseXp));
            _xpExp = Mathf.Max(0.1f, EditorGUILayout.FloatField("Exposant XP", _xpExp));

            if (GUILayout.Button("Générer / régénérer les niveaux (conserve les déblocages)"))
            {
                ContentImporters.GenerateProgression(_prog, _baseHp, _hpGrowth, _baseDmg, _dmgGrowth, _baseXp, _xpExp, _levels);
                AssetDatabase.SaveAssets();
            }

            if (_prog.levels.Count < 2) { EditorGUILayout.HelpBox("Aucun niveau : cliquez sur « Générer ».", MessageType.Info); return; }

            EditorGUILayout.Space();
            DrawChart("PV du joueur", _prog.levels.Select(l => (float)l.health).ToArray(), new Color(0.3f, 0.8f, 0.4f));
            DrawChart("Dégâts du joueur", _prog.levels.Select(l => (float)l.damage).ToArray(), new Color(0.9f, 0.4f, 0.3f));
            DrawChart("XP cumulée requise", _prog.levels.Select(l => (float)l.xpRequired).ToArray(), new Color(0.4f, 0.6f, 0.95f));

            var so = new SerializedObject(_prog);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("levels"), new GUIContent("Tableau des niveaux (déblocages à saisir ici)"), true);
            so.ApplyModifiedProperties();
        }

        private static void DrawChart(string title, float[] values, Color color)
        {
            EditorGUILayout.LabelField(title + "  (max " + values.Max().ToString("0") + ")", EditorStyles.miniBoldLabel);
            Rect r = GUILayoutUtility.GetRect(100, 90, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.15f));
            float max = Mathf.Max(1f, values.Max());
            var pts = new Vector3[values.Length];
            for (int i = 0; i < values.Length; i++)
                pts[i] = new Vector3(r.x + 6 + (r.width - 12) * i / (values.Length - 1), r.yMax - 6 - (r.height - 12) * values[i] / max, 0);
            Handles.color = color;
            Handles.DrawAAPolyLine(2.5f, pts);
        }

        // ----------------------------------------------------------------- Combat
        private void DrawCombat()
        {
            EditorGUILayout.HelpBox(
                "Temps pour tuer (TTK) = PV de l'ennemi / dégâts du joueur au niveau attendu. " +
                "Si une valeur sort du vert, l'ennemi est probablement trop facile ou trop dur pour son étage.",
                MessageType.Info);
            if (_prog == null || _prog.levels.Count == 0) { EditorGUILayout.HelpBox("Générez d'abord une progression du joueur (onglet précédent).", MessageType.Warning); return; }

            int[] expected = { 3, 10, 17 }; // niveau attendu à l'étage 1, 2, 3
            var enemies = ForgeAssets.FindAll<EnemyData>();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Ennemi", EditorStyles.miniBoldLabel, GUILayout.Width(150));
            for (int f = 1; f <= 3; f++) GUILayout.Label("Étage " + f + " (niv. " + expected[f - 1] + ")", EditorStyles.miniBoldLabel, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            foreach (var e in enemies)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(e.name, GUILayout.Width(150));
                for (int f = 1; f <= 3; f++)
                {
                    if (f < e.firstFloor || f > e.lastFloor) { GUILayout.Label("—", GUILayout.Width(120)); continue; }
                    var lv = _prog.Get(Mathf.Min(expected[f - 1], _prog.levels.Count));
                    float dmg = lv != null ? Mathf.Max(1, lv.damage) : 1;
                    float ttk = e.HealthAt(f) / dmg;
                    float danger = lv != null && lv.health > 0 ? (e.DamageAt(f) * ttk) / lv.health : 0f; // part des PV perdue pendant le combat
                    float min = e.isBoss ? 10f : 2f, max = e.isBoss ? 30f : 8f;
                    var old = GUI.color;
                    GUI.color = ttk < min ? new Color(1f, 0.85f, 0.4f) : ttk > max ? new Color(1f, 0.5f, 0.5f) : new Color(0.6f, 1f, 0.6f);
                    GUILayout.Label(ttk.ToString("0.0") + " coups, -" + (danger * 100f).ToString("0") + "% PV", GUILayout.Width(120));
                    GUI.color = old;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("Vert : durée de combat saine (2–8 coups, 10–30 pour un boss). Jaune : trop court. Rouge : trop long.", EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------------ Butin
        private void DrawLoot()
        {
            _loot = (LootTableData)EditorGUILayout.ObjectField("Table de butin", _loot, typeof(LootTableData), false);
            _rolls = Mathf.Clamp(EditorGUILayout.IntField("Nombre de tirages", _rolls), 100, 1000000);

            using (new EditorGUI.DisabledScope(_loot == null))
                if (GUILayout.Button("Simuler")) Simulate();

            if (_loot == null) { EditorGUILayout.HelpBox("Choisissez une table de butin à simuler.", MessageType.Info); return; }
            if (_rarityCounts == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Répartition par rareté", EditorStyles.boldLabel);
            string[] names = System.Enum.GetNames(typeof(Rarity));
            Color[] colors = { Color.gray, new Color(0.4f, 0.8f, 0.4f), new Color(0.3f, 0.5f, 1f), new Color(0.7f, 0.3f, 0.9f), new Color(1f, 0.7f, 0.1f) };
            int total = Mathf.Max(1, _rarityCounts.Sum());
            for (int i = 0; i < names.Length; i++)
            {
                float pct = 100f * _rarityCounts[i] / total;
                Rect r = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.DrawRect(new Rect(r.x + 90, r.y + 2, (r.width - 190) * pct / 100f, r.height - 4), colors[i]);
                GUI.Label(new Rect(r.x, r.y, 90, r.height), names[i]);
                GUI.Label(new Rect(r.xMax - 95, r.y, 95, r.height), pct.ToString("0.0") + " % (" + _rarityCounts[i] + ")");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Items les plus fréquents", EditorStyles.boldLabel);
            foreach (var kv in _top.Take(8))
                EditorGUILayout.LabelField(kv.Key.displayName + " [" + kv.Key.rarity + "]", (100f * kv.Value / total).ToString("0.0") + " %");
        }

        private void Simulate()
        {
            if (_loot.TotalWeight <= 0f)
            {
                EditorUtility.DisplayDialog("Table vide", "Cette table n'a aucun item avec un poids positif : rien à simuler.", "OK");
                _rarityCounts = null;
                return;
            }
            var rng = new System.Random();
            _rarityCounts = new int[System.Enum.GetNames(typeof(Rarity)).Length];
            var counts = new Dictionary<ItemData, int>();
            for (int i = 0; i < _rolls; i++)
            {
                var item = _loot.Roll(rng);
                if (item == null) continue;
                _rarityCounts[(int)item.rarity]++;
                int c; counts.TryGetValue(item, out c); counts[item] = c + 1;
            }
            _top = counts.OrderByDescending(k => k.Value).ToList();
            ForgeMetrics.Record("Simulation de butin", 1, 600f);
        }
    }
}
