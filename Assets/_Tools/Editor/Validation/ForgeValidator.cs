using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Forge.Runtime;
using UnityEditor;
using UnityEngine;

namespace Forge.EditorTools
{
    public enum Severity { Error, Warning, Info }

    public class ValidationIssue
    {
        public Severity severity;
        public string category;
        public string message;
        public UnityEngine.Object target;
        public string fixLabel;
        public Action fix;
    }

    /// <summary>
    /// Vérifie les règles de cohérence du GDD. Les messages sont écrits pour un non-programmeur :
    /// ils disent quoi, où, et comment corriger.
    /// </summary>
    public static class ForgeValidator
    {
        public static List<ValidationIssue> RunAll()
        {
            var issues = new List<ValidationIssue>();
            var items = ForgeAssets.FindAll<ItemData>();
            var enemies = ForgeAssets.FindAll<EnemyData>();
            var loots = ForgeAssets.FindAll<LootTableData>();
            var rooms = ForgeAssets.FindAll<RoomData>();
            var floors = ForgeAssets.FindAll<FloorData>();
            var dialogues = ForgeAssets.FindAll<DialogueData>();
            var progs = ForgeAssets.FindAll<PlayerProgressionData>();

            CheckItems(items, issues);
            CheckEnemies(enemies, issues);
            CheckLoot(loots, issues);
            CheckRooms(rooms, issues);
            CheckFloors(floors, rooms, issues);
            CheckDialogues(dialogues, issues);
            CheckProgression(progs, issues);
            CheckVolumes(items, enemies, loots, rooms, dialogues, issues);

            return issues.OrderBy(i => i.severity).ThenBy(i => i.category).ToList();
        }

        private static ValidationIssue Add(List<ValidationIssue> list, Severity sev, string cat, string msg, UnityEngine.Object target, string fixLabel = null, Action fix = null)
        {
            var issue = new ValidationIssue { severity = sev, category = cat, message = msg, target = target, fixLabel = fixLabel, fix = fix };
            list.Add(issue);
            return issue;
        }

        // --------------------------------------------------------------- Nommage
        private static void CheckName(List<ValidationIssue> list, UnityEngine.Object asset, Regex rx, string suggestion, string cat)
        {
            if (rx.IsMatch(asset.name)) return;
            string path = AssetDatabase.GetAssetPath(asset);
            Add(list, Severity.Warning, "Nommage",
                "« " + asset.name + " » ne respecte pas la convention de nommage. Nom attendu : " + suggestion + ".",
                asset, "Renommer en " + suggestion,
                () => { AssetDatabase.RenameAsset(path, suggestion); AssetDatabase.SaveAssets(); });
        }

        // ----------------------------------------------------------------- Items
        private static void CheckItems(List<ItemData> items, List<ValidationIssue> list)
        {
            foreach (var it in items)
            {
                CheckName(list, it, ForgeNaming.ItemRx, ForgeNaming.ItemName(string.IsNullOrEmpty(it.displayName) ? it.name : it.displayName), "Items");
                if (string.IsNullOrWhiteSpace(it.displayName)) Add(list, Severity.Error, "Items", "L'item « " + it.name + " » n'a pas de nom affiché.", it);
                if (it.icon == null) Add(list, Severity.Warning, "Items", "L'item « " + it.name + " » n'a pas d'icône. Déposez Icon_" + it.name.Replace("Item_", "") + ".png dans " + ForgePaths.Icons + ", puis lancez Forge > Automatisation > Lier les icônes.", it);
                if (it.price <= 0 && it.type != ItemType.Key) Add(list, Severity.Warning, "Items", "L'item « " + it.name + " » a un prix de 0 : est-ce voulu ?", it);
                if (string.IsNullOrWhiteSpace(it.description)) Add(list, Severity.Warning, "Items", "L'item « " + it.name + " » n'a pas de description.", it);
                if ((it.type == ItemType.Weapon && it.bonusDamage <= 0) || (it.type == ItemType.Armor && it.bonusDefense <= 0))
                    Add(list, Severity.Warning, "Items", "L'item « " + it.name + " » est une arme/armure mais n'a aucun bonus de dégâts/défense.", it);
            }
            foreach (var group in items.Where(i => !string.IsNullOrWhiteSpace(i.displayName)).GroupBy(i => i.displayName.Trim().ToLowerInvariant()).Where(g => g.Count() > 1))
                Add(list, Severity.Error, "Items", "Plusieurs items portent le nom affiché « " + group.First().displayName + " » : " + string.Join(", ", group.Select(g => g.name)) + ".", group.First());
        }

        // -------------------------------------------------------------- Ennemis
        private static void CheckEnemies(List<EnemyData> enemies, List<ValidationIssue> list)
        {
            foreach (var e in enemies)
            {
                CheckName(list, e, ForgeNaming.EnemyRx, ForgeNaming.EnemyName(string.IsNullOrEmpty(e.displayName) ? e.name : e.displayName, e.isBoss), "Ennemis");
                if (e.isBoss && !e.name.StartsWith("Boss_")) { /* déjà couvert par le nommage */ }
                if (string.IsNullOrWhiteSpace(e.displayName)) Add(list, Severity.Error, "Ennemis", "L'ennemi « " + e.name + " » n'a pas de nom affiché.", e);
                if (e.lootTable == null) Add(list, Severity.Warning, "Ennemis", "L'ennemi « " + e.name + " » n'a pas de table de butin associée.", e);
                if (e.prefab == null) Add(list, Severity.Info, "Ennemis", "L'ennemi « " + e.name + " » n'a pas de prefab : un cube de substitution sera utilisé.", e);
                if (e.baseHealth < 1) Add(list, Severity.Error, "Ennemis", "L'ennemi « " + e.name + " » a des PV inférieurs à 1.", e);
                if (e.firstFloor > e.lastFloor) Add(list, Severity.Error, "Ennemis", "L'ennemi « " + e.name + " » : l'étage de début (" + e.firstFloor + ") est après l'étage de fin (" + e.lastFloor + ").", e);
                if (e.isBoss && e.behavior != EnemyBehavior.Boss) Add(list, Severity.Warning, "Ennemis", "« " + e.name + " » est un boss mais son comportement n'est pas « Boss ».", e);
            }
        }

        // ---------------------------------------------------------------- Butin
        private static void CheckLoot(List<LootTableData> loots, List<ValidationIssue> list)
        {
            foreach (var t in loots)
            {
                CheckName(list, t, ForgeNaming.LootRx, ForgeNaming.LootName(t.name.Replace("Loot_", "")), "Butin");
                if (t.entries.Count == 0) { Add(list, Severity.Error, "Butin", "La table de butin « " + t.name + " » est vide.", t); continue; }
                for (int i = 0; i < t.entries.Count; i++)
                {
                    var e = t.entries[i];
                    if (e == null || e.item == null)
                        Add(list, Severity.Error, "Butin", "La table « " + t.name + " », ligne " + (i + 1) + " : l'item cité n'existe pas (référence manquante).", t);
                    else if (e.weight <= 0f)
                        Add(list, Severity.Warning, "Butin", "La table « " + t.name + " » : « " + e.item.name + " » a un poids de 0, il ne tombera jamais.", t);
                }
                var dup = t.entries.Where(e => e != null && e.item != null).GroupBy(e => e.item).Where(g => g.Count() > 1);
                foreach (var g in dup) Add(list, Severity.Warning, "Butin", "La table « " + t.name + " » contient « " + g.Key.name + " » " + g.Count() + " fois.", t);
                if (t.TotalWeight <= 0f && t.entries.Any(e => e != null && e.item != null))
                    Add(list, Severity.Error, "Butin", "La table « " + t.name + " » a un poids total de 0 : rien ne peut tomber.", t);
                if (t.minFloor > t.maxFloor) Add(list, Severity.Error, "Butin", "La table « " + t.name + " » : étage min supérieur à l'étage max.", t);
            }
        }

        // --------------------------------------------------------------- Salles
        private static void CheckRooms(List<RoomData> rooms, List<ValidationIssue> list)
        {
            foreach (var r in rooms)
            {
                CheckName(list, r, ForgeNaming.RoomRx, ForgeNaming.RoomName(r.floor, r.indexInFloor), "Salles");

                foreach (var s in r.enemies)
                {
                    if (s == null || s.enemy == null) { Add(list, Severity.Error, "Salles", "La salle « " + r.name + " » cite un ennemi qui n'existe pas (référence manquante).", r); continue; }
                    if (s.cell.x < 0 || s.cell.y < 0 || s.cell.x >= r.size.x || s.cell.y >= r.size.y)
                        Add(list, Severity.Error, "Salles", "Dans « " + r.name + " », « " + s.enemy.name + " » est placé hors de la salle (case " + s.cell.x + "," + s.cell.y + ").", r);
                    if (s.enemy.firstFloor > r.floor || s.enemy.lastFloor < r.floor)
                        Add(list, Severity.Warning, "Salles", "« " + s.enemy.name + " » apparaît dans « " + r.name + " » (étage " + r.floor + ") alors qu'il est prévu pour les étages " + s.enemy.firstFloor + " à " + s.enemy.lastFloor + ".", r);
                }
                foreach (var c in r.chests)
                {
                    if (c == null || c.lootTable == null) Add(list, Severity.Error, "Salles", "Un coffre de « " + r.name + " » n'a pas de table de butin.", r);
                    else if (c.cell.x < 0 || c.cell.y < 0 || c.cell.x >= r.size.x || c.cell.y >= r.size.y)
                        Add(list, Severity.Error, "Salles", "Dans « " + r.name + " », un coffre est placé hors de la salle.", r);
                }
                var cells = r.enemies.Where(s => s != null).Select(s => s.cell).Concat(r.chests.Where(c => c != null).Select(c => c.cell)).ToList();
                if (cells.Count != cells.Distinct().Count())
                    Add(list, Severity.Warning, "Salles", "Dans « " + r.name + " », deux éléments sont placés sur la même case.", r);

                // Portes : compatibilité entre salles reliées
                foreach (Direction d in Enum.GetValues(typeof(Direction)))
                {
                    var target = r.GetLink(d);
                    bool door = r.HasDoor(d);
                    string dirName = d.ToFrench();
                    var room = r;
                    var dir = d;

                    if (door && target == null)
                        Add(list, Severity.Warning, "Portes", "« " + r.name + " » a une porte " + dirName + " qui ne mène nulle part.", r, "Supprimer la porte",
                            () => { Undo.RecordObject(room, "Supprimer porte"); room.doors &= ~dir.ToMask(); EditorUtility.SetDirty(room); });
                    if (!door && target != null)
                        Add(list, Severity.Error, "Portes", "« " + r.name + " » est reliée à « " + target.name + " » par le " + dirName + ", mais n'a pas de porte " + dirName + ".", r, "Ajouter la porte",
                            () => { Undo.RecordObject(room, "Ajouter porte"); room.doors |= dir.ToMask(); EditorUtility.SetDirty(room); });
                    if (target != null)
                    {
                        var tgt = target;
                        var back = target.GetLink(d.Opposite());
                        if (!target.HasDoor(d.Opposite()) || back != r)
                            Add(list, Severity.Error, "Portes",
                                "Portes incompatibles : « " + r.name + " » ouvre au " + dirName + " vers « " + target.name + " », mais « " + target.name + " » n'a pas de porte " + d.Opposite().ToFrench() + " qui revient vers elle.",
                                r, "Créer la porte retour",
                                () =>
                                {
                                    Undo.RecordObject(tgt, "Créer porte retour");
                                    var od = dir.Opposite();
                                    tgt.doors |= od.ToMask();
                                    tgt.links.RemoveAll(l => l == null || l.direction == od);
                                    tgt.links.Add(new RoomLink { direction = od, target = room });
                                    EditorUtility.SetDirty(tgt);
                                });
                        if (target.floor != r.floor)
                            Add(list, Severity.Warning, "Portes", "« " + r.name + " » est reliée à « " + target.name + " » qui est à un autre étage.", r);
                    }
                }
                if (r.doors == DoorMask.None) Add(list, Severity.Error, "Portes", "« " + r.name + " » n'a aucune porte : le joueur ne peut ni y entrer ni en sortir.", r);
            }

            foreach (var g in rooms.GroupBy(r => r.floor + "-" + r.indexInFloor).Where(g => g.Count() > 1))
                Add(list, Severity.Warning, "Salles", "Plusieurs salles ont l'étage/index " + g.Key + " : " + string.Join(", ", g.Select(x => x.name)) + ".", g.First());
        }

        // --------------------------------------------------------------- Étages
        private static void CheckFloors(List<FloorData> floors, List<RoomData> allRooms, List<ValidationIssue> list)
        {
            var inFloor = new HashSet<RoomData>();
            foreach (var f in floors)
            {
                CheckName(list, f, ForgeNaming.FloorRx, "Floor_" + f.floorNumber, "Étages");
                foreach (var r in f.rooms) if (r != null) inFloor.Add(r);

                if (f.rooms.Any(r => r == null))
                    Add(list, Severity.Error, "Étages", "L'étage « " + f.name + " » contient une entrée de salle vide (référence manquante).", f);
                if (f.entrance == null)
                {
                    Add(list, Severity.Error, "Accessibilité", "L'étage « " + f.name + " » n'a pas de salle d'entrée.", f);
                    continue;
                }
                if (!f.rooms.Contains(f.entrance))
                    Add(list, Severity.Warning, "Étages", "La salle d'entrée de « " + f.name + " » n'est pas dans la liste de ses salles.", f);

                // Accessibilité : parcours en largeur depuis l'entrée
                var visited = new HashSet<RoomData> { f.entrance };
                var queue = new Queue<RoomData>();
                queue.Enqueue(f.entrance);
                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    foreach (var l in cur.links)
                        if (l != null && l.target != null && cur.HasDoor(l.direction) && visited.Add(l.target)) queue.Enqueue(l.target);
                }
                foreach (var r in f.rooms.Where(x => x != null && !visited.Contains(x)))
                    Add(list, Severity.Error, "Accessibilité", "La salle « " + r.name + " » est inaccessible depuis l'entrée de « " + f.name + " » (« " + f.entrance.name + " »). Reliez-la à une salle accessible.", r);

                foreach (var r in f.rooms.Where(x => x != null && x.floor != f.floorNumber))
                    Add(list, Severity.Warning, "Étages", "« " + r.name + " » est rangée dans l'étage " + f.floorNumber + " mais son champ Étage vaut " + r.floor + ".", r);

                // Exactement un boss par étage
                int bosses = f.rooms.Where(x => x != null).SelectMany(x => x.enemies).Count(s => s != null && s.enemy != null && s.enemy.isBoss);
                if (bosses == 0) Add(list, Severity.Error, "Boss", "L'étage « " + f.name + " » n'a aucun boss. Il en faut exactement un.", f);
                else if (bosses > 1) Add(list, Severity.Error, "Boss", "L'étage « " + f.name + " » contient " + bosses + " boss. Il en faut exactement un.", f);
            }

            foreach (var r in allRooms.Where(r => !inFloor.Contains(r)))
                Add(list, Severity.Warning, "Étages", "La salle « " + r.name + " » n'appartient à aucun étage.", r);

            foreach (var g in floors.GroupBy(f => f.floorNumber).Where(g => g.Count() > 1))
                Add(list, Severity.Error, "Étages", "Plusieurs assets d'étage portent le numéro " + g.Key + ".", g.First());
            for (int n = 1; n <= 3; n++)
                if (floors.Count > 0 && !floors.Any(f => f.floorNumber == n))
                    Add(list, Severity.Warning, "Étages", "L'étage " + n + " n'existe pas encore.", null);
        }

        // ------------------------------------------------------------- Dialogues
        private static void CheckDialogues(List<DialogueData> dialogues, List<ValidationIssue> list)
        {
            foreach (var d in dialogues)
            {
                CheckName(list, d, ForgeNaming.DialogueRx, ForgeNaming.DialogueName(string.IsNullOrEmpty(d.npcName) ? d.name : d.npcName), "Dialogues");
                if (d.nodes.Count == 0) { Add(list, Severity.Error, "Dialogues", "Le dialogue « " + d.name + " » est vide.", d); continue; }
                if (d.Find(d.startNodeId) == null)
                    Add(list, Severity.Error, "Dialogues", "Le dialogue « " + d.name + " » : le noeud de départ « " + d.startNodeId + " » n'existe pas.", d);

                foreach (var g in d.nodes.Where(n => n != null).GroupBy(n => n.id).Where(g => g.Count() > 1))
                    Add(list, Severity.Error, "Dialogues", "Le dialogue « " + d.name + " » : l'identifiant de noeud « " + g.Key + " » est utilisé " + g.Count() + " fois.", d);

                foreach (var n in d.nodes.Where(n => n != null))
                {
                    if (string.IsNullOrWhiteSpace(n.text)) Add(list, Severity.Warning, "Dialogues", "« " + d.name + " », noeud « " + n.id + " » : réplique vide.", d);
                    foreach (var c in n.choices)
                        if (c != null && !string.IsNullOrEmpty(c.nextNodeId) && d.Find(c.nextNodeId) == null)
                            Add(list, Severity.Error, "Dialogues", "« " + d.name + " », noeud « " + n.id + " » : le choix « " + c.text + " » mène au noeud « " + c.nextNodeId + " » qui n'existe pas.", d);
                }

                var reach = new HashSet<string>();
                var stack = new Stack<string>();
                stack.Push(d.startNodeId);
                while (stack.Count > 0)
                {
                    var id = stack.Pop();
                    if (!reach.Add(id)) continue;
                    var node = d.Find(id);
                    if (node == null) continue;
                    foreach (var c in node.choices) if (c != null && !string.IsNullOrEmpty(c.nextNodeId)) stack.Push(c.nextNodeId);
                }
                foreach (var n in d.nodes.Where(n => n != null && !reach.Contains(n.id)))
                    Add(list, Severity.Warning, "Dialogues", "« " + d.name + " » : le noeud « " + n.id + " » n'est jamais atteint.", d);
            }
        }

        // ------------------------------------------------------------ Progression
        private static void CheckProgression(List<PlayerProgressionData> progs, List<ValidationIssue> list)
        {
            foreach (var p in progs)
            {
                if (p.levels.Count != 20) Add(list, Severity.Error, "Progression", "« " + p.name + " » contient " + p.levels.Count + " niveau(x) au lieu de 20.", p);
                for (int i = 0; i < p.levels.Count; i++)
                {
                    var l = p.levels[i];
                    if (l == null) continue;
                    if (l.level != i + 1) Add(list, Severity.Error, "Progression", "« " + p.name + " » : la ligne " + (i + 1) + " porte le numéro de niveau " + l.level + ".", p);
                    if (i > 0 && p.levels[i - 1] != null)
                    {
                        var prev = p.levels[i - 1];
                        if (l.xpRequired <= prev.xpRequired) Add(list, Severity.Error, "Progression", "« " + p.name + " » : l'XP du niveau " + l.level + " (" + l.xpRequired + ") doit dépasser celle du niveau " + prev.level + " (" + prev.xpRequired + ").", p);
                        if (l.health < prev.health || l.damage < prev.damage) Add(list, Severity.Warning, "Progression", "« " + p.name + " » : les PV ou dégâts baissent au niveau " + l.level + ".", p);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- Volumes
        private static void CheckVolumes(List<ItemData> items, List<EnemyData> enemies, List<LootTableData> loots,
            List<RoomData> rooms, List<DialogueData> dialogues, List<ValidationIssue> list)
        {
            int bosses = enemies.Count(e => e.isBoss);
            Add(list, Severity.Info, "Avancement", "Salles : " + rooms.Count + " / 30", null);
            Add(list, Severity.Info, "Avancement", "Ennemis : " + (enemies.Count - bosses) + " / 12, boss : " + bosses + " / 3", null);
            Add(list, Severity.Info, "Avancement", "Items : " + items.Count + " / 40", null);
            Add(list, Severity.Info, "Avancement", "Tables de butin : " + loots.Count + " / ≈ 20", null);
            Add(list, Severity.Info, "Avancement", "Dialogues (PNJ) : " + dialogues.Count + " / 25", null);
        }

        // ----------------------------------------------------------------- Rapport
        public static string ToMarkdown(List<ValidationIssue> issues)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Rapport de validation – " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine();
            sb.AppendLine("- Erreurs : " + issues.Count(i => i.severity == Severity.Error));
            sb.AppendLine("- Avertissements : " + issues.Count(i => i.severity == Severity.Warning));
            sb.AppendLine();
            foreach (var g in issues.GroupBy(i => i.severity))
            {
                sb.AppendLine("## " + (g.Key == Severity.Error ? "Erreurs (à corriger)" : g.Key == Severity.Warning ? "Avertissements" : "Informations"));
                foreach (var i in g) sb.AppendLine("- [" + i.category + "] " + i.message);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
