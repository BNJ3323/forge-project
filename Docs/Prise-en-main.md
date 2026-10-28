# Prise en main (pour les designers)

Aucun outil ne demande de toucher au code. Tout est dans le menu **Forge** en haut de l'éditeur.

## Je veux ajouter des items ou des ennemis
1. **Forge > Data Studio**, onglet *Items* (ou *Ennemis*).
2. Pour un seul : saisissez le nom, cliquez **+ Nouvel item**. Le fichier est nommé automatiquement (`Item_NomEnPascalCase`).
3. Pour beaucoup : écrivez-les dans un tableur, exportez en CSV (séparateur `;` ou `,`), cliquez **Importer CSV…**. Un compte rendu liste les lignes en erreur avec leur numéro. Réimporter met à jour les assets existants au lieu de les dupliquer.
4. Modèles de colonnes dans `Assets/_Game/Content/Source/` (items.csv, enemies.csv, dialogues.csv).

## Je veux créer ou modifier une salle
1. **Forge > Room Editor**, sélectionnez une salle (ou **Nouvelle salle**).
2. Cochez les portes et choisissez la salle voisine : la porte opposée est créée dans la salle voisine.
3. Choisissez **Placer ennemi** + un ennemi, puis cliquez sur la grille (clic droit = effacer).
4. **Générer le prefab de la salle** pour obtenir le prefab.

## Je veux créer un étage complet
**Forge > Générateur d'étage** : numéro d'étage, nombre de salles, boss → *Générer*. Les salles déjà remplies ne sont jamais écrasées (sauf option explicite).

## Je veux vérifier mon travail
**Forge > Validation du contenu** → *Vérifier maintenant*. Rouge = à corriger, jaune = à surveiller. **Voir** sélectionne l'asset fautif, **Corriger** applique la réparation proposée (survolez le bouton pour la lire). *Exporter le rapport* écrit `Reports/validation.md`.

## Je veux équilibrer
**Forge > Équilibrage** : générez la courbe du joueur depuis une formule, vérifiez les temps pour tuer par étage (vert = sain) et simulez une table de butin sur 10 000 tirages.

## Icônes
Déposez `Icon_<NomItem>.png` dans `Assets/_Game/Art/Icons` (réglages d'import automatiques), puis **Forge > Automatisation > Lier les icônes aux items**.

## Convention de nommage
`Item_NomItem`, `Enemy_NomEnnemi`, `Boss_NomBoss`, `Loot_NomTable`, `Room_F1_01`, `Floor_1`, `Dialogue_NomPNJ`. **Forge > Automatisation > Appliquer la convention de nommage** renomme tout seul.
