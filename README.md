# Projet Forge – Outils d'éditeur pour *The Dungeon* (Unity)

Boîte à outils intégrée à l'éditeur Unity pour produire le contenu de *The Dungeon* (30 salles, 40 items, 15 ennemis, tables de butin, progression, dialogues) sans toucher au code. Le gameplay n'est pas l'objet du projet : seul un runtime minimal affiche le contenu produit.

## Ouvrir le projet

1. Unity **2022.3 LTS** (testé avec 2022.3.20f1), template 3D Built-in. Ouvrir le dossier avec Unity Hub (« Add project from disk »). Unity génère `Library/` et les fichiers `.meta` au premier import.
2. Menu **Forge > Échantillon > Générer le contenu d'exemple** : produit tout l'échantillon avec les outils (imports CSV, tables de butin, 3 étages de 10 salles, progression 1–20, dialogues) puis crée `Assets/_Game/Scenes/Demo.unity`.
3. Ouvrir la scène `Demo` et lancer le Play : un panneau permet de naviguer de salle en salle, d'ouvrir les coffres, de tuer un ennemi pour tirer son butin et de parler aux PNJ.
4. Ouvrir **Forge > Validation du contenu** pour lire le rapport.

## Les outils (menu *Forge*)

| Menu | Outil | Famille |
|---|---|---|
| Forge > Data Studio | Tableau d'édition des items/ennemis/tables de butin, import/export CSV, dialogues par CSV | Création de contenu |
| Forge > Room Editor | Peinture des ennemis/coffres sur la grille de la salle, portes réciproques automatiques, prefab en un clic | Création + Automatisation |
| Forge > Générateur d'étage | Étage complet en un clic (salles, portes, difficulté, ennemis, coffres) | Création + Automatisation |
| Forge > Validation du contenu | Règles du GDD, rapport lisible, corrections automatiques, export Markdown | Validation et qualité |
| Forge > Équilibrage | Courbes de progression, temps pour tuer, simulateur de butin | Équilibrage et debug |
| Forge > Automatisation | Liaison des icônes, convention de nommage, prefabs de toutes les salles | Automatisation |
| Forge > Tableau de bord | Temps gagné cumulé | Mesure |
| `Tools/` | Hook pre-commit, `validate.sh`, `build.sh` | Pipeline |

Chaque champ a une infobulle ; chaque fenêtre explique son usage en tête. Le guide pas à pas est dans `Docs/Prise-en-main.md`.

## Organisation du code

- `Assets/_Game/Runtime/` : assembly `Forge.Runtime` (données ScriptableObject + runtime de démonstration). **Seul code embarqué dans le build.**
- `Assets/_Tools/Editor/` : assembly `Forge.Editor`, restreint à la plateforme *Editor* : jamais dans le build.
- `Assets/_Game/Content/` : données produites par les outils (ScriptableObject). Aucune donnée de jeu dans le code.
- `Assets/_Game/Content/Source/` : CSV sources des designers.
- `Docs/` : prise en main, mesures, script de la vidéo, document de rendu (`Rendu.md`).

## Pipeline

```bash
Tools/install-hooks.sh                 # active le hook pre-commit
export UNITY_PATH=/chemin/vers/Unity   # active la validation Unity au commit
Tools/validate.sh                      # validation en mode batch (code de sortie 1 si erreurs)
Tools/build.sh [sortie]                # validation puis build
```

Le hook vérifie toujours la présence des `.meta` des assets ajoutés, et lance la validation du contenu quand `UNITY_PATH` est défini.
