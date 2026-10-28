using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Forge.EditorTools
{
    /// <summary>Tableau de bord : temps gagné par les outils (estimation = unités traitées × temps manuel mesuré).</summary>
    public class DashboardWindow : EditorWindow
    {
        [MenuItem("Forge/Tableau de bord (temps gagné)", false, 50)]
        public static void Open()
        {
            GetWindow<DashboardWindow>("Forge – Temps gagné").minSize = new Vector2(480, 300);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Chaque outil enregistre le nombre d'actions qu'il a réalisées. Le temps manuel équivalent vient de chronométrages " +
                "faits à la main (voir Docs/Mesures.md). Le temps gagné = temps manuel – temps réel passé dans l'outil.",
                MessageType.Info);

            var all = ForgeMetrics.GetAll();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Outil / action", EditorStyles.miniBoldLabel, GUILayout.Width(240));
            GUILayout.Label("Actions", EditorStyles.miniBoldLabel, GUILayout.Width(70));
            GUILayout.Label("Temps manuel équivalent", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();

            foreach (var t in all.OrderByDescending(x => x.manualSeconds))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(t.tool, GUILayout.Width(240));
                GUILayout.Label(t.units.ToString(), GUILayout.Width(70));
                GUILayout.Label(Format(t.manualSeconds));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Total évité : " + Format(all.Sum(x => x.manualSeconds)), EditorStyles.boldLabel);

            if (GUILayout.Button("Remettre à zéro")) { ForgeMetrics.Reset(); Repaint(); }
        }

        private static string Format(float seconds)
        {
            if (seconds < 90) return seconds.ToString("0") + " s";
            if (seconds < 5400) return (seconds / 60f).ToString("0.0") + " min";
            return (seconds / 3600f).ToString("0.0") + " h";
        }
    }
}
