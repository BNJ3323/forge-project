using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Forge.Runtime;
using UnityEditor;
using UnityEngine;

namespace Forge.EditorTools
{
    public class ImportReport
    {
        public int created, updated;
        public List<string> errors = new List<string>();

        public string ToText(string what)
        {
            var sb = new StringBuilder();
            sb.AppendLine(what + " : " + created + " créé(s), " + updated + " mis à jour, " + errors.Count + " ligne(s) en erreur.");
            foreach (var e in errors.Take(15)) sb.AppendLine(" - " + e);
            if (errors.Count > 15) sb.AppendLine(" - … et " + (errors.Count - 15) + " autres.");
            return sb.ToString();
        }
    }

    /// <summary>Import CSV pour les items, ennemis et dialogues (les designers travaillent dans un tableur).</summary>
    public static class ContentImporters
    {
        private static bool ParseEnum<T>(string s, out T value) where T : struct
        {
            return Enum.TryParse(s, true, out value) && Enum.IsDefined(typeof(T), value);
        }

        // ------------------------------------------------------------------ Items
        // Colonnes : name;rarity;type;price;health;damage;defense;effects;description
        public static ImportReport ImportItems(string csvText)
        {
            var report = new ImportReport();
            var table = ForgeCsv.Parse(csvText);
            if (!table.Has("name")) { report.errors.Add("Colonne 'name' introuvable (colonnes attendues : name;rarity;type;price;health;damage;defense;effects;description)."); return report; }

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < table.rows.Count; i++)
                {
                    var row = table.rows[i];
                    int line = table.lineNumbers[i];
                    string display = table.Get(row, "name");
                    if (display == "") { report.errors.Add("Ligne " + line + " : nom vide."); continue; }

                    Rarity rarity; ItemType type;
                    if (!ParseEnum(table.Get(row, "rarity", "Common"), out rarity))
                    { report.errors.Add("Ligne " + line + " (" + display + ") : rareté inconnue '" + table.Get(row, "rarity") + "' (Common, Uncommon, Rare, Epic, Legendary)."); continue; }
                    if (!ParseEnum(table.Get(row, "type", "Consumable"), out type))
                    { report.errors.Add("Ligne " + line + " (" + display + ") : type inconnu '" + table.Get(row, "type") + "' (Weapon, Armor, Accessory, Consumable, Key)."); continue; }

                    int price, hp, dmg, def;
                    if (!ForgeCsv.TryInt(table.Get(row, "price", "0"), out price) || price < 0)
                    { report.errors.Add("Ligne " + line + " (" + display + ") : prix invalide."); continue; }
                    ForgeCsv.TryInt(table.Get(row, "health", "0"), out hp);
                    ForgeCsv.TryInt(table.Get(row, "damage", "0"), out dmg);
                    ForgeCsv.TryInt(table.Get(row, "defense", "0"), out def);

                    bool created;
                    var item = ForgeAssets.LoadOrCreate<ItemData>(ForgePaths.Items, ForgeNaming.ItemName(display), out created);
                    item.displayName = display;
                    item.rarity = rarity;
                    item.type = type;
                    item.price = price;
                    item.bonusHealth = hp;
                    item.bonusDamage = dmg;
                    item.bonusDefense = def;
                    item.effects = table.Get(row, "effects").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).ToList();
                    item.description = table.Get(row, "description");
                    ForgeAssets.Dirty(item);
                    if (created) report.created++; else report.updated++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            AssetDatabase.SaveAssets();
            ForgeMetrics.Record("Import CSV items", report.created + report.updated, 90f);
            return report;
        }

        public static string ExportItems()
        {
            var sb = new StringBuilder("name;rarity;type;price;health;damage;defense;effects;description\n");
            foreach (var it in ForgeAssets.FindAll<ItemData>())
                sb.AppendLine(string.Join(";", new[]
                {
                    ForgeCsv.Escape(it.displayName), it.rarity.ToString(), it.type.ToString(), it.price.ToString(),
                    it.bonusHealth.ToString(), it.bonusDamage.ToString(), it.bonusDefense.ToString(),
                    ForgeCsv.Escape(string.Join("|", it.effects)), ForgeCsv.Escape(it.description)
                }));
            return sb.ToString();
        }

        // ---------------------------------------------------------------- Ennemis
        // Colonnes : name;boss;behavior;health;damage;speed;scaling;firstfloor;lastfloor;loot
        public static ImportReport ImportEnemies(string csvText)
        {
            var report = new ImportReport();
            var table = ForgeCsv.Parse(csvText);
            if (!table.Has("name")) { report.errors.Add("Colonne 'name' introuvable (colonnes attendues : name;boss;behavior;health;damage;speed;scaling;firstfloor;lastfloor;loot)."); return report; }

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < table.rows.Count; i++)
                {
                    var row = table.rows[i];
                    int line = table.lineNumbers[i];
                    string display = table.Get(row, "name");
                    if (display == "") { report.errors.Add("Ligne " + line + " : nom vide."); continue; }

                    string bossText = table.Get(row, "boss", "0").ToLowerInvariant();
                    bool boss = bossText == "1" || bossText == "true" || bossText == "oui" || bossText == "yes";

                    EnemyBehavior behavior;
                    if (!ParseEnum(table.Get(row, "behavior", boss ? "Boss" : "Melee"), out behavior))
                    { report.errors.Add("Ligne " + line + " (" + display + ") : comportement inconnu '" + table.Get(row, "behavior") + "' (Melee, Ranged, Charger, Boss)."); continue; }

                    int hp, dmg, first, last; float speed, scaling;
                    if (!ForgeCsv.TryInt(table.Get(row, "health", "20"), out hp) || hp < 1)
                    { report.errors.Add("Ligne " + line + " (" + display + ") : PV invalides (entier >= 1)."); continue; }
                    if (!ForgeCsv.TryInt(table.Get(row, "damage", "3"), out dmg) || dmg < 0)
                    { report.errors.Add("Ligne " + line + " (" + display + ") : dégâts invalides."); continue; }
                    if (!ForgeCsv.TryFloat(table.Get(row, "speed", "2"), out speed)) speed = 2f;
                    if (!ForgeCsv.TryFloat(table.Get(row, "scaling", "40"), out scaling)) scaling = 40f;
                    if (!ForgeCsv.TryInt(table.Get(row, "firstfloor", "1"), out first)) first = 1;
                    if (!ForgeCsv.TryInt(table.Get(row, "lastfloor", "3"), out last)) last = 3;

                    bool created;
                    var enemy = ForgeAssets.LoadOrCreate<EnemyData>(ForgePaths.Enemies, ForgeNaming.EnemyName(display, boss), out created);
                    enemy.displayName = display;
                    enemy.isBoss = boss;
                    enemy.behavior = behavior;
                    enemy.baseHealth = hp;
                    enemy.baseDamage = dmg;
                    enemy.baseSpeed = speed;
                    enemy.floorScalingPercent = scaling;
                    enemy.firstFloor = Mathf.Clamp(first, 1, 3);
                    enemy.lastFloor = Mathf.Clamp(last, 1, 3);

                    string lootName = table.Get(row, "loot");
                    if (lootName != "")
                    {
                        var table2 = ForgeAssets.FindByName<LootTableData>(lootName);
                        if (table2 != null) enemy.lootTable = table2;
                        else report.errors.Add("Ligne " + line + " (" + display + ") : table de butin '" + lootName + "' introuvable (importé quand même, sans butin).");
                    }
                    ForgeAssets.Dirty(enemy);
                    if (created) report.created++; else report.updated++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            AssetDatabase.SaveAssets();
            ForgeMetrics.Record("Import CSV ennemis", report.created + report.updated, 120f);
            return report;
        }

        // -------------------------------------------------------------- Dialogues
        // Colonnes : npc;node;speaker;text;choice;next;condition;consequence
        // Une ligne = un choix. Plusieurs lignes avec le même (npc,node) = plusieurs choix. choice vide = noeud sans choix.
        public static ImportReport ImportDialogues(string csvText)
        {
            var report = new ImportReport();
            var table = ForgeCsv.Parse(csvText);
            if (!table.Has("npc") || !table.Has("node"))
            { report.errors.Add("Colonnes 'npc' et 'node' obligatoires (npc;node;speaker;text;choice;next;condition;consequence)."); return report; }

            var byNpc = new Dictionary<string, DialogueData>();
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < table.rows.Count; i++)
                {
                    var row = table.rows[i];
                    int line = table.lineNumbers[i];
                    string npc = table.Get(row, "npc");
                    string nodeId = table.Get(row, "node");
                    if (npc == "" || nodeId == "") { report.errors.Add("Ligne " + line + " : 'npc' ou 'node' vide."); continue; }

                    DialogueData dlg;
                    if (!byNpc.TryGetValue(npc, out dlg))
                    {
                        bool created;
                        dlg = ForgeAssets.LoadOrCreate<DialogueData>(ForgePaths.Dialogues, ForgeNaming.DialogueName(npc), out created);
                        dlg.npcName = npc;
                        dlg.nodes = new List<DialogueNode>();
                        dlg.startNodeId = "start";
                        byNpc[npc] = dlg;
                        if (created) report.created++; else report.updated++;
                    }

                    var node = dlg.Find(nodeId);
                    if (node == null)
                    {
                        node = new DialogueNode { id = nodeId, speaker = table.Get(row, "speaker", npc), text = table.Get(row, "text") };
                        dlg.nodes.Add(node);
                        if (dlg.nodes.Count == 1) dlg.startNodeId = nodeId;
                    }
                    string choice = table.Get(row, "choice");
                    if (choice != "")
                        node.choices.Add(new DialogueChoice
                        {
                            text = choice,
                            nextNodeId = table.Get(row, "next"),
                            condition = table.Get(row, "condition"),
                            consequence = table.Get(row, "consequence")
                        });
                    ForgeAssets.Dirty(dlg);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            AssetDatabase.SaveAssets();
            ForgeMetrics.Record("Import CSV dialogues", table.rows.Count, 25f);
            return report;
        }

        // ------------------------------------------------------- Tables de butin
        public static float DefaultWeight(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return 100f;
                case Rarity.Uncommon: return 50f;
                case Rarity.Rare: return 20f;
                case Rarity.Epic: return 8f;
                default: return 2f;
            }
        }

        /// <summary>Remplit une table avec tous les items d'une plage de raretés, poids déduits de la rareté.</summary>
        public static void FillLootTable(LootTableData table, Rarity min, Rarity max)
        {
            Undo.RecordObject(table, "Remplir table de butin");
            table.entries = new List<LootEntry>();
            foreach (var item in ForgeAssets.FindAll<ItemData>())
                if (item.rarity >= min && item.rarity <= max)
                    table.entries.Add(new LootEntry { item = item, weight = DefaultWeight(item.rarity) });
            ForgeAssets.Dirty(table);
            ForgeMetrics.Record("Remplissage table de butin", table.entries.Count, 20f);
        }

        public static void ApplyRarityWeights(LootTableData table)
        {
            Undo.RecordObject(table, "Poids par rareté");
            foreach (var e in table.entries)
                if (e != null && e.item != null) e.weight = DefaultWeight(e.item.rarity);
            ForgeAssets.Dirty(table);
        }

        // ------------------------------------------------------------ Progression
        public static void GenerateProgression(PlayerProgressionData data, int baseHp, float hpGrowthPercent,
            int baseDamage, float damageGrowthPercent, int baseXp, float xpExponent, int levels)
        {
            Undo.RecordObject(data, "Générer progression");
            var previous = new Dictionary<int, string>();
            foreach (var l in data.levels) if (l != null) previous[l.level] = l.unlock;

            data.levels = new List<LevelEntry>();
            for (int lv = 1; lv <= levels; lv++)
            {
                string unlock;
                previous.TryGetValue(lv, out unlock);
                data.levels.Add(new LevelEntry
                {
                    level = lv,
                    health = Mathf.RoundToInt(baseHp * Mathf.Pow(1f + hpGrowthPercent / 100f, lv - 1)),
                    damage = Mathf.RoundToInt(baseDamage * Mathf.Pow(1f + damageGrowthPercent / 100f, lv - 1)),
                    xpRequired = lv == 1 ? 0 : Mathf.RoundToInt(baseXp * Mathf.Pow(lv - 1, xpExponent)),
                    unlock = unlock ?? ""
                });
            }
            ForgeAssets.Dirty(data);
            ForgeMetrics.Record("Génération progression", levels, 30f);
        }

        public static string ReadSource(string fileName)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), ForgePaths.Source, fileName);
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }
    }
}
