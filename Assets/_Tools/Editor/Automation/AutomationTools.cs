using System.IO;
using System.Linq;
using Forge.Runtime;
using UnityEditor;
using UnityEngine;

namespace Forge.EditorTools
{
    /// <summary>Import automatique : tout PNG déposé dans Art/Icons est configuré comme sprite d'icône.</summary>
    public class IconImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ForgePaths.Icons)) return;
            var importer = (TextureImporter)assetImporter;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single && !importer.mipmapEnabled) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            ForgeMetrics.Record("Import auto des icônes", 1, 30f);
        }
    }

    public static class AutomationTools
    {
        [MenuItem("Forge/Automatisation/Lier les icônes aux items", false, 41)]
        public static void LinkIcons()
        {
            var sprites = AssetDatabase.FindAssets("t:Sprite", new[] { ForgePaths.Icons })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => new { path = p, sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p) })
                .Where(x => x.sprite != null)
                .ToDictionary(x => Path.GetFileNameWithoutExtension(x.path).ToLowerInvariant(), x => x.sprite);

            int linked = 0, missing = 0;
            foreach (var item in ForgeAssets.FindAll<ItemData>())
            {
                if (item.icon != null) continue;
                string key = ("Icon_" + item.name.Replace("Item_", "")).ToLowerInvariant();
                Sprite s;
                if (sprites.TryGetValue(key, out s))
                {
                    Undo.RecordObject(item, "Lier icône");
                    item.icon = s;
                    EditorUtility.SetDirty(item);
                    linked++;
                }
                else missing++;
            }
            AssetDatabase.SaveAssets();
            ForgeMetrics.Record("Liaison des icônes", linked, 20f);
            EditorUtility.DisplayDialog("Liaison des icônes",
                linked + " icône(s) liée(s).\n" + missing + " item(s) sans icône correspondante.\n\n" +
                "Nom attendu : Icon_<NomItem>.png dans " + ForgePaths.Icons + " (ex : Icon_IronSword.png pour Item_IronSword).", "OK");
        }

        [MenuItem("Forge/Automatisation/Appliquer la convention de nommage", false, 42)]
        public static void ApplyNaming()
        {
            int renamed = 0;
            foreach (var it in ForgeAssets.FindAll<ItemData>())
                renamed += Rename(it, ForgeNaming.ItemRx, ForgeNaming.ItemName(string.IsNullOrEmpty(it.displayName) ? it.name : it.displayName));
            foreach (var e in ForgeAssets.FindAll<EnemyData>())
                renamed += Rename(e, ForgeNaming.EnemyRx, ForgeNaming.EnemyName(string.IsNullOrEmpty(e.displayName) ? e.name : e.displayName, e.isBoss));
            foreach (var r in ForgeAssets.FindAll<RoomData>())
                renamed += Rename(r, ForgeNaming.RoomRx, ForgeNaming.RoomName(r.floor, r.indexInFloor));
            foreach (var d in ForgeAssets.FindAll<DialogueData>())
                renamed += Rename(d, ForgeNaming.DialogueRx, ForgeNaming.DialogueName(string.IsNullOrEmpty(d.npcName) ? d.name : d.npcName));
            AssetDatabase.SaveAssets();
            ForgeMetrics.Record("Renommage automatique", renamed, 25f);
            EditorUtility.DisplayDialog("Convention de nommage", renamed + " asset(s) renommé(s).\n\n" + ForgeNaming.Summary, "OK");
        }

        private static int Rename(Object asset, System.Text.RegularExpressions.Regex rx, string expected)
        {
            if (asset.name == expected) return 0;
            string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(asset), expected);
            if (!string.IsNullOrEmpty(error)) { Debug.LogWarning("[Forge] Renommage impossible de " + asset.name + " : " + error); return 0; }
            return 1;
        }
    }
}
