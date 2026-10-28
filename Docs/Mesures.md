# Mesure du gain de temps

## Protocole
Pour chaque tâche : chronométrer **trois essais manuels** (inspecteur Unity, copier-coller) et **trois essais avec l'outil**, prendre la médiane. Le tableau de bord (**Forge > Tableau de bord**) multiplie ensuite le nombre d'actions réellement faites par le temps manuel mesuré.

## Estimations de départ
> Ces valeurs sont des **estimations initiales** (utilisées comme constantes dans `ForgeMetrics.Record`). Elles doivent être **remplacées par vos chronométrages** avant le rendu : reportez les médianes dans la colonne « Mesuré » et mettez à jour les constantes si l'écart est notable.

| Tâche | Volume GDD | Manuel (estimé) | Avec l'outil (estimé) | Mesuré manuel | Mesuré outil |
|---|---|---|---|---|---|
| Créer un item (asset, nom, 8 champs) | 40 | 90 s → 60 min | CSV : 25 s/item en tableur + 5 s d'import → 20 min | | |
| Créer un ennemi + lier son butin | 15 | 120 s → 30 min | CSV : ≈ 30 s/ennemi → 8 min | | |
| Créer une salle + portes réciproques + nom | 30 | 240 s → 2 h | Générateur : ≈ 1 min par étage → 3 min | | |
| Placer un ennemi/coffre dans une salle | ≈ 100 | 15 s → 25 min | Clic sur la grille : ≈ 5 s → 8 min | | |
| Prefab d'une salle | 30 | 180 s → 1 h 30 | Un clic pour toutes → < 1 min | | |
| Vérifier portes, accessibilité, références | à chaque modification | ≈ 20 min par passe | 2 s | | |
| Courbe de progression niveaux 1–20 | 1 | 30 s/niveau → 10 min | Formule : 1 min | | |
| Test d'une table de butin (10 000 tirages) | ≈ 20 tables | impraticable à la main | 1 clic | | |

Total estimé hors validation : environ **6 h de travail manuel** contre **moins de 1 h** avec les outils. La validation évite en plus des erreurs qui coûtent un aller-retour de test chacune.
