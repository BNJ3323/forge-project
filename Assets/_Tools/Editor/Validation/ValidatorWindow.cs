using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Forge.EditorTools
{
    /// <summary>Fenêtre de rapport de validation, lisible par un non-programmeur.</summary>
    public class ValidatorWindow : EditorWindow
    {
        private List<ValidationIssue> _issues = new List<ValidationIssue>();
        private bool _hasRun;
        private bool _showErrors = true, _showWarnings = true, _showInfo = true;
        private Vector2 _scroll;
        private string _filterCategory = "Toutes";

        [MenuItem("Forge/Validation du contenu", false, 20)]
        public static void Open()
        {
            var w = GetWindow<ValidatorWindow>("Forge – Validation");
            w.minSize = new Vector2(620, 360);
        }

        private void OnEnable() { if (!_hasRun) Run(); }

        private void Run()
        {
            _issues = ForgeValidator.RunAll();
            _hasRun = true;
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Vérifier maintenant", EditorStyles.toolbarButton, GUILayout.Width(130))) Run();
            if (GUILayout.Button(new GUIContent("Exporter le rapport (.md)", "Écrit Reports/validation.md, à partager avec l'équipe."), EditorStyles.toolbarButton, GUILayout.Width(160))) Export();
            GUILayout.FlexibleSpace();
            var categories = new List<string> { "Toutes" };
            categories.AddRange(_issues.Select(i => i.category).Distinct().OrderBy(c => c));
            int idx = Mathf.Max(0, categories.IndexOf(_filterCategory));
            idx = EditorGUILayout.Popup(idx, categories.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(120));
            _filterCategory = categories[idx];
            _showErrors = GUILayout.Toggle(_showErrors, "Erreurs", EditorStyles.toolbarButton);
            _showWarnings = GUILayout.Toggle(_showWarnings, "Avertissements", EditorStyles.toolbarButton);
            _showInfo = GUILayout.Toggle(_showInfo, "Infos", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            int errors = _issues.Count(i => i.severity == Severity.Error);
            int warnings = _issues.Count(i => i.severity == Severity.Warning);
            if (errors == 0)
                EditorGUILayout.HelpBox("Aucune erreur bloquante. " + warnings + " avertissement(s).", MessageType.Info);
            else
                EditorGUILayout.HelpBox(errors + " erreur(s) à corriger avant de livrer, " + warnings + " avertissement(s).", MessageType.Error);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var issue in _issues.ToList())
            {
                if (issue.severity == Severity.Error && !_showErrors) continue;
                if (issue.severity == Severity.Warning && !_showWarnings) continue;
                if (issue.severity == Severity.Info && !_showInfo) continue;
                if (_filterCategory != "Toutes" && issue.category != _filterCategory) continue;
                DrawIssue(issue);
            }
            EditorGUILayout.EndScrollView();

            int fixable = _issues.Count(i => i.fix != null);
            if (fixable > 0 && GUILayout.Button("Corriger automatiquement les " + fixable + " problème(s) réparable(s)"))
            {
                if (EditorUtility.DisplayDialog("Corrections automatiques", fixable + " correction(s) vont être appliquées (annulables avec Ctrl+Z pour les portes). Continuer ?", "Corriger", "Annuler"))
                {
                    foreach (var i in _issues.Where(x => x.fix != null).ToList()) i.fix();
                    ForgeMetrics.Record("Corrections auto validation", fixable, 40f);
                    AssetDatabase.SaveAssets();
                    Run();
                }
            }
        }

        private void DrawIssue(ValidationIssue issue)
        {
            string icon = issue.severity == Severity.Error ? "console.erroricon.sml" : issue.severity == Severity.Warning ? "console.warnicon.sml" : "console.infoicon.sml";
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(22), GUILayout.Height(20));
            GUILayout.Label("[" + issue.category + "]", EditorStyles.miniBoldLabel, GUILayout.Width(95));
            GUILayout.Label(issue.message, EditorStyles.wordWrappedLabel);
            if (issue.target != null && GUILayout.Button("Voir", GUILayout.Width(45)))
            {
                Selection.activeObject = issue.target;
                EditorGUIUtility.PingObject(issue.target);
            }
            if (issue.fix != null && GUILayout.Button(new GUIContent("Corriger", issue.fixLabel), GUILayout.Width(65)))
            {
                issue.fix();
                ForgeMetrics.Record("Corrections auto validation", 1, 40f);
                AssetDatabase.SaveAssets();
                Run();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void Export()
        {
            Directory.CreateDirectory(ForgePaths.Reports);
            string path = Path.Combine(ForgePaths.Reports, "validation.md");
            File.WriteAllText(path, ForgeValidator.ToMarkdown(_issues));
            EditorUtility.RevealInFinder(path);
        }
    }
}
