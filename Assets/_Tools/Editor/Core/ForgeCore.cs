using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Forge.EditorTools
{
    /// <summary>Emplacements des assets produits par les outils.</summary>
    public static class ForgePaths
    {
        public const string Root = "Assets/_Game/Content";
        public const string Items = Root + "/Items";
        public const string Enemies = Root + "/Enemies";
        public const string Loot = Root + "/Loot";
        public const string Rooms = Root + "/Rooms";
        public const string Floors = Root + "/Floors";
        public const string Dialogues = Root + "/Dialogues";
        public const string Progression = Root + "/Progression";
        public const string Source = Root + "/Source";
        public const string Icons = "Assets/_Game/Art/Icons";
        public const string RoomPrefabs = "Assets/_Game/Prefabs/Rooms";
        public const string Scenes = "Assets/_Game/Scenes";
        public const string Reports = "Reports";
    }

    /// <summary>Accès simplifié à l'AssetDatabase.</summary>
    public static class ForgeAssets
    {
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static List<T> FindAll<T>() where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list.OrderBy(a => a.name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Charge l'asset s'il existe déjà (même nom), sinon le crée.</summary>
        public static T LoadOrCreate<T>(string folder, string assetName, out bool created) where T : ScriptableObject
        {
            EnsureFolder(folder);
            string path = folder + "/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) { created = false; return existing; }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            created = true;
            return asset;
        }

        public static T FindByName<T>(string assetName) where T : UnityEngine.Object
        {
            foreach (var guid in AssetDatabase.FindAssets(assetName + " t:" + typeof(T).Name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == assetName)
                    return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return null;
        }

        public static void Dirty(UnityEngine.Object obj)
        {
            if (obj != null) EditorUtility.SetDirty(obj);
        }
    }

    /// <summary>Convention de nommage du studio.</summary>
    public static class ForgeNaming
    {
        // Convention : Item_NomEnPascalCase, Enemy_..., Boss_..., Loot_..., Room_F1_01, Floor_1, Dialogue_..., Icon_...
        public static readonly Regex ItemRx = new Regex(@"^Item_[A-Z][A-Za-z0-9]*$");
        public static readonly Regex EnemyRx = new Regex(@"^(Enemy|Boss)_[A-Z][A-Za-z0-9]*$");
        public static readonly Regex LootRx = new Regex(@"^Loot_[A-Z][A-Za-z0-9_]*$");
        public static readonly Regex RoomRx = new Regex(@"^Room_F[1-3]_\d{2}$");
        public static readonly Regex FloorRx = new Regex(@"^Floor_[1-3]$");
        public static readonly Regex DialogueRx = new Regex(@"^Dialogue_[A-Z][A-Za-z0-9]*$");

        public const string Summary =
            "Item_NomItem, Enemy_NomEnnemi, Boss_NomBoss, Loot_NomTable, Room_F1_01, Floor_1, Dialogue_NomPNJ (PascalCase, sans espace ni accent).";

        public static string Pascal(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "Unnamed";
            string norm = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in norm)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            var words = Regex.Split(sb.ToString(), @"[^A-Za-z0-9]+").Where(w => w.Length > 0);
            var result = new StringBuilder();
            foreach (var w in words)
                result.Append(char.ToUpperInvariant(w[0])).Append(w.Substring(1));
            string s = result.ToString();
            if (s.Length == 0) return "Unnamed";
            if (char.IsDigit(s[0])) s = "N" + s;
            return s;
        }

        public static string ItemName(string display) { return "Item_" + Pascal(display); }
        public static string EnemyName(string display, bool boss) { return (boss ? "Boss_" : "Enemy_") + Pascal(display); }
        public static string LootName(string display) { return "Loot_" + Pascal(display); }
        public static string RoomName(int floor, int index) { return string.Format("Room_F{0}_{1:00}", floor, index); }
        public static string DialogueName(string npc) { return "Dialogue_" + Pascal(npc); }
    }

    /// <summary>Compteur du temps gagné, affiché dans la fenêtre Forge Dashboard.</summary>
    public static class ForgeMetrics
    {
        private const string Prefix = "Forge.Metrics.";

        public static void Record(string tool, int units, float manualSecondsPerUnit)
        {
            if (units <= 0) return;
            string toolsKey = Prefix + "Tools";
            var tools = new HashSet<string>(EditorPrefs.GetString(toolsKey, "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            if (tools.Add(tool)) EditorPrefs.SetString(toolsKey, string.Join("|", tools));

            EditorPrefs.SetInt(Prefix + tool + ".units", EditorPrefs.GetInt(Prefix + tool + ".units", 0) + units);
            EditorPrefs.SetFloat(Prefix + tool + ".manual", EditorPrefs.GetFloat(Prefix + tool + ".manual", 0f) + units * manualSecondsPerUnit);
        }

        public static List<(string tool, int units, float manualSeconds)> GetAll()
        {
            var list = new List<(string, int, float)>();
            foreach (var t in EditorPrefs.GetString(Prefix + "Tools", "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                list.Add((t, EditorPrefs.GetInt(Prefix + t + ".units", 0), EditorPrefs.GetFloat(Prefix + t + ".manual", 0f)));
            return list;
        }

        public static void Reset()
        {
            foreach (var t in EditorPrefs.GetString(Prefix + "Tools", "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                EditorPrefs.DeleteKey(Prefix + t + ".units");
                EditorPrefs.DeleteKey(Prefix + t + ".manual");
            }
            EditorPrefs.DeleteKey(Prefix + "Tools");
        }
    }

    /// <summary>Lecteur CSV tolérant (séparateur , ou ; détecté, guillemets gérés).</summary>
    public static class ForgeCsv
    {
        public class Table
        {
            public List<string> headers = new List<string>();
            public List<string[]> rows = new List<string[]>();
            public List<int> lineNumbers = new List<int>();

            public string Get(string[] row, string column, string fallback = "")
            {
                int i = headers.IndexOf(column.ToLowerInvariant());
                if (i < 0 || i >= row.Length) return fallback;
                string v = row[i].Trim();
                return v.Length == 0 ? fallback : v;
            }

            public bool Has(string column) { return headers.Contains(column.ToLowerInvariant()); }
        }

        public static Table Parse(string text)
        {
            var table = new Table();
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int first = Array.FindIndex(lines, l => l.Trim().Length > 0);
            if (first < 0) return table;

            char delim = lines[first].Count(c => c == ';') > lines[first].Count(c => c == ',') ? ';' : ',';
            for (int i = first; i < lines.Length; i++)
            {
                if (lines[i].Trim().Length == 0 || lines[i].TrimStart().StartsWith("#")) continue;
                var cells = SplitLine(lines[i], delim);
                if (i == first) { table.headers = cells.Select(c => c.Trim().ToLowerInvariant()).ToList(); continue; }
                table.rows.Add(cells);
                table.lineNumbers.Add(i + 1);
            }
            return table;
        }

        private static string[] SplitLine(string line, char delim)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == delim && !quoted) { result.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(c);
            }
            result.Add(sb.ToString());
            return result.ToArray();
        }

        public static string Escape(string value)
        {
            if (value == null) return "";
            if (value.IndexOfAny(new[] { ',', ';', '"', '\n' }) >= 0) return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        public static bool TryInt(string s, out int v) { return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v); }
        public static bool TryFloat(string s, out float v) { return float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v); }
    }
}
