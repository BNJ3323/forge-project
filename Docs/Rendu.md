# Projet Forge – Document de rendu

## 1. Choix du moteur
**Unity 2022.3 LTS.** Les `ScriptableObject` correspondent exactement à l'exigence « données dans un format natif du moteur » ; les `EditorWindow`, `AssetPostprocessor` et le mode batch (`-executeMethod`) couvrent tous les besoins d'outillage sans dépendance externe ; les assemblies (`asmdef` restreint à la plateforme *Editor*) garantissent que le code des outils n'entre jamais dans le build. Unreal aurait demandé davantage de configuration (Editor Utility Widgets, Data Tables) pour un gain nul sur ce périmètre.

## 2. Analyse du GDD et choix des outils
Le GDD est avant tout un **volume de données relationnelles** : 30 salles, 15 ennemis, 40 items, 20 tables, 150 répliques. Trois constats ont guidé les choix :

1. **Le coût principal est répétitif et mécanique** (créer l'asset, remplir 8 champs, relier). → import CSV, génération d'étage, placement par peinture.
2. **Les risques sont dans les relations** : portes qui ne se répondent pas, salle inaccessible, référence à un ennemi supprimé, boss en trop. Elles sont invisibles à l'œil et coûtent un aller-retour de test chacune. → validation automatique, avec corrections.
3. **L'équilibrage est impossible à juger sans chiffres** (20 niveaux, 3 étages, tables pondérées). → courbes, temps pour tuer, simulateur.

**Outils retenus (7)** : Data Studio (+ import CSV), Room Editor, Générateur d'étage, Validateur, Fenêtre d'équilibrage, Automatisation (icônes, nommage, prefabs), Pipeline (hook, scripts batch). Ils couvrent les 5 familles.

**Outils écartés, et pourquoi**
- *Éditeur de graphe de dialogues* : gros développement (UI de nœuds) pour ≈ 150 répliques que des rédacteurs écrivent mieux dans un tableur. Remplacé par un import CSV + validation des liens.
- *Placement d'ennemis dans la Scene View* (Handles 3D) : l'éditeur sur grille est plus rapide à construire et suffit pour un jeu en vue du dessus. À faire en suite.
- *Renommage de masse des assets externes* : les assets du jeu sont tous produits par nos outils, donc déjà nommés correctement.
- *Éditeur de courbes d'XP interactif* : la formule + les courbes en lecture répondent au besoin ; le tableau reste éditable à la main.

## 3. Les outils

### 3.1 Data Studio
- **Besoin** : 40 items et 15 ennemis, chacun avec 8 à 10 champs. Les ouvrir un par un dans l'inspecteur est lent et fait oublier des champs.
- **Fonctionnement** : un tableau par type où toutes les lignes sont éditables (avec annulation Unity), filtres, duplication, création nommée automatiquement (`Item_…`). Import/export CSV (séparateur `;` ou `,` détecté, guillemets gérés, mise à jour plutôt que doublon, compte rendu des lignes en erreur avec leur numéro). Les tables de butin se remplissent depuis une plage de raretés avec poids déduits ; les dialogues s'importent en CSV.
- **Limites** : pas d'import des icônes dans le CSV (passe par l'outil de liaison) ; pas de suppression depuis le tableau (volontaire) ; le CSV n'est pas rechargé en direct si le fichier change.

### 3.2 Room Editor et Générateur d'étage
- **Besoin** : 30 salles dont les portes doivent se répondre, avec ennemis et coffres placés.
- **Fonctionnement** : le Room Editor affiche la grille de la salle ; clic gauche place l'ennemi ou le coffre choisi, clic droit efface. Cocher une porte et choisir la salle voisine crée automatiquement la porte et le lien réciproques. Le Générateur d'étage crée en un clic les 10 salles nommées `Room_F<n>_NN`, les relie en grille (donc toujours accessibles), applique une difficulté progressive, place ennemis et coffres selon l'étage de chaque ennemi et ne touche jamais une salle déjà remplie. Un bouton génère le prefab d'une salle (ou de toutes).
- **Limites** : la grille impose une disposition rectangulaire ; le placement automatique est aléatoire (graine fixe) et ne tient pas compte de la géométrie interne ; les prefabs générés sont des volumes de substitution.

### 3.3 Validateur
- **Besoin** : appliquer les règles du GDD à chaque modification, avec un rapport qu'un non-programmeur peut lire.
- **Règles** : ennemi/item/coffre cité inexistant ; portes incompatibles ou à sens unique (avec porte retour proposée) ; salles inaccessibles depuis l'entrée (parcours en largeur) ; un seul boss par étage ; nommage ; tables vides ou à poids nul ; ennemis hors de leur étage ; éléments hors de la salle ou superposés ; liens de dialogue cassés et nœuds jamais atteints ; progression sur 20 niveaux, XP strictement croissante. Un bloc « Avancement » compare au volume du GDD.
- **Fonctionnement** : fenêtre filtrable (erreurs/avertissements/infos, catégorie), bouton **Voir** (sélectionne l'asset), **Corriger** (réparation proposée, lisible au survol), export `Reports/validation.md`, et même moteur en ligne de commande pour le pipeline.
- **Limites** : la règle « portes compatibles » se base sur les liens explicites, pas sur la position des portes dans le prefab ; les conditions de dialogue (`has:`, `gold>=`) ne sont pas interprétées, seulement conservées.

### 3.4 Équilibrage
- **Besoin** : juger 20 niveaux, 15 ennemis sur 3 étages et ≈ 20 tables sans jouer.
- **Fonctionnement** : génération de la progression depuis une formule (PV et dégâts en croissance composée, XP en puissance) en conservant les déblocages saisis ; trois courbes ; tableau de temps pour tuer et de PV perdus par ennemi et par étage, coloré par plage saine ; simulateur de butin (jusqu'à 10⁶ tirages) avec répartition par rareté et items les plus fréquents.
- **Limites** : le niveau attendu par étage (3, 10, 17) est une hypothèse codée ; le modèle de combat est volontairement simple (un coup par tour, pas d'esquive ni de défense).

### 3.5 Automatisation
Import automatique des icônes (sprite, sans mipmap, point, 256 px) via un `AssetPostprocessor`, liaison des icônes aux items par nom, application de la convention de nommage à tous les assets, génération de tous les prefabs de salles. **Limite** : la convention est figée dans `ForgeNaming`, pas configurable depuis l'éditeur.

### 3.6 Pipeline
Hook `pre-commit` versionné (vérification des `.meta` sans Unity ; validation du contenu si `UNITY_PATH` est défini), `validate.sh` et `build.sh` (le build est refusé si la validation échoue). **Limite** : les scripts ont été écrits pour Linux/macOS ; sous Windows, passer par Git Bash.

## 4. Mesure du gain de temps
Le protocole et les estimations de départ sont dans `Docs/Mesures.md`. Un tableau de bord (**Forge > Tableau de bord**) cumule le temps manuel équivalent évité à chaque action des outils. Ordre de grandeur estimé : ≈ 6 h de saisie manuelle contre moins d'une heure, et une vérification de cohérence qui passe de ≈ 20 minutes à 2 secondes. **Les valeurs du tableau doivent être remplacées par des chronométrages réels avant le rendu.**

## 5. Limites connues et pistes
- Le projet n'a pas pu être ouvert dans Unity au moment de la rédaction : le code est écrit pour Unity 2022.3 mais doit être compilé et testé à l'ouverture (une passe de correction des éventuelles erreurs de compilation est à prévoir).
- Pas d'éditeur de graphe pour les dialogues, pas de prévisualisation 3D des salles, pas de tests automatisés des règles du validateur.
- Avec plus de temps : tests unitaires (Unity Test Framework en mode Edit) sur chaque règle, réglages du validateur (niveaux de sévérité configurables), placement par Handles dans la Scene View, intégration CI (GitHub Actions + GameCI) exécutant `validate.sh`, localisation des textes.
