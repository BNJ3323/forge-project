using System.Collections.Generic;
using System.Linq;
using System.Text;
using Forge.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Forge.EditorTools
{
    /// <summary>
    /// Produit l'échantillon de contenu uniquement avec les outils : imports CSV, remplissage des tables,
    /// générateur d'étage, génération de progression, puis scène de démonstration.
    /// </summary>
    public static class SampleContentGenerator
    {
        [MenuItem("Forge/Échantillon/Générer le contenu d'exemple", false, 60)]
        public static void Generate()
        {
            var log = new StringBuilder();

            // 1. Items (CSV)
            log.Append(Import("items.csv", ContentImporters.ImportItems, "Items"));

            // 2. Tables de butin (outil de remplissage par rareté)
            MakeLoot("Loot_Floor1", 1, 1, Rarity.Common, Rarity.Uncommon, "");
            MakeLoot("Loot_Floor2", 2, 2, Rarity.Common, Rarity.Rare, "");
            MakeLoot("Loot_Floor3", 3, 3, Rarity.Uncommon, Rarity.Epic, "");
            MakeLoot("Loot_Boss", 1, 3, Rarity.Rare, Rarity.Legendary, "boss");
            MakeLoot("Loot_Chest_F1", 1, 1, Rarity.Common, Rarity.Rare, "coffre");
            MakeLoot("Loot_Chest_F2", 2, 2, Rarity.Uncommon, Rarity.Epic, "coffre");
            MakeLoot("Loot_Chest_F3", 3, 3, Rarity.Rare, Rarity.Legendary, "coffre");

            // 3. Ennemis (CSV, lient les tables par nom)
            log.Append(Import("enemies.csv", ContentImporters.ImportEnemies, "Ennemis"));

            // 4. Progression du joueur (formule)
            bool created;
            var prog = ForgeAssets.LoadOrCreate<PlayerProgressionData>(ForgePaths.Progression, "PlayerProgression", out created);
            ContentImporters.GenerateProgression(prog, 50, 12f, 5, 9f, 40, 1.6f, 20);
            prog.levels[4].unlock = "Compétence : esquive";
            prog.levels[9].unlock = "Compétence : attaque tournoyante";
            prog.levels[14].unlock = "Emplacement d'accessoire supplémentaire";
            EditorUtility.SetDirty(prog);

            // 5. Étages (générateur : salles, portes, difficulté, ennemis, coffres)
            var enemies = ForgeAssets.FindAll<EnemyData>();
            var floors = new List<FloorData>();
            for (int f = 1; f <= 3; f++)
            {
                var boss = enemies.FirstOrDefault(e => e.isBoss && e.firstFloor == f);
                floors.Add(FloorGenerator.Generate(new FloorGenerationSettings { floorNumber = f, roomCount = 10, columns = 5, boss = boss, populate = true }));
            }

            // 6. Dialogues (CSV)
            log.Append(Import("dialogues.csv", ContentImporters.ImportDialogues, "Dialogues"));

            // 7. Scène de démonstration
            BuildDemoScene(floors, ForgeAssets.FindAll<DialogueData>());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var issues = ForgeValidator.RunAll();
            int errors = issues.Count(i => i.severity == Severity.Error);
            log.AppendLine("Validation : " + errors + " erreur(s), " + issues.Count(i => i.severity == Severity.Warning) + " avertissement(s).");
            Debug.Log("[Forge] Échantillon généré.\n" + log);
            EditorUtility.DisplayDialog("Échantillon généré",
                log + "\nOuvrez la scène Assets/_Game/Scenes/Demo.unity et lancez le Play.\nOuvrez Forge > Validation du contenu pour le rapport.", "OK");
        }

        private static string Import(string file, System.Func<string, ImportReport> importer, string label)
        {
            string text = ContentImporters.ReadSource(file);
            if (text == null) return label + " : fichier " + file + " introuvable dans " + ForgePaths.Source + "\n";
            return importer(text).ToText(label);
        }

        private static void MakeLoot(string name, int minFloor, int maxFloor, Rarity min, Rarity max, string condition)
        {
            bool created;
            var table = ForgeAssets.LoadOrCreate<LootTableData>(ForgePaths.Loot, name, out created);
            table.minFloor = minFloor;
            table.maxFloor = maxFloor;
            table.condition = condition;
            ContentImporters.FillLootTable(table, min, max);
        }

        private static void BuildDemoScene(List<FloorData> floors, List<DialogueData> dialogues)
        {
            ForgeAssets.EnsureFolder(ForgePaths.Scenes);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("DemoRuntime");
            var demo = go.AddComponent<DemoRuntime>();
            demo.floors = floors;
            demo.dialogues = dialogues.ToArray();
            demo.progression = ForgeAssets.FindAll<PlayerProgressionData>().FirstOrDefault();

            string path = ForgePaths.Scenes + "/Demo.unity";
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        }
    }
}
