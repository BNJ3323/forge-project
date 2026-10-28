using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Forge.EditorTools
{
    /// <summary>
    /// Points d'entrée en ligne de commande (mode batch) pour les scripts de pipeline :
    /// Unity -batchmode -quit -projectPath . -executeMethod Forge.EditorTools.ForgeCli.Validate
    /// </summary>
    public static class ForgeCli
    {
        public static void Validate()
        {
            try
            {
                var issues = ForgeValidator.RunAll();
                Directory.CreateDirectory(ForgePaths.Reports);
                File.WriteAllText(Path.Combine(ForgePaths.Reports, "validation.md"), ForgeValidator.ToMarkdown(issues));

                int errors = issues.Count(i => i.severity == Severity.Error);
                foreach (var i in issues.Where(x => x.severity == Severity.Error))
                    Console.WriteLine("[ERREUR] [" + i.category + "] " + i.message);
                Console.WriteLine("[Forge] Validation : " + errors + " erreur(s), " + issues.Count(i => i.severity == Severity.Warning) + " avertissement(s).");
                EditorApplication.Exit(errors > 0 ? 1 : 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Forge] Échec de la validation : " + ex);
                EditorApplication.Exit(2);
            }
        }

        public static void Build()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Console.WriteLine("[Forge] Aucune scène dans Build Settings."); EditorApplication.Exit(3); return; }

            string output = Environment.GetEnvironmentVariable("FORGE_BUILD_OUTPUT") ?? "Builds/TheDungeon";
            var result = BuildPipeline.BuildPlayer(scenes, output, EditorUserBuildSettings.activeBuildTarget, BuildOptions.None);
            bool ok = result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Console.WriteLine("[Forge] Build " + (ok ? "réussi" : "échoué") + " : " + result.summary.totalErrors + " erreur(s).");
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
