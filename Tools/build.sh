#!/usr/bin/env bash
# Valide le contenu puis construit le jeu. Le build est refuse si la validation echoue.
# Usage : UNITY_PATH=/chemin/vers/Unity ./Tools/build.sh [dossier_de_sortie]
if [ -z "${UNITY_PATH:-}" ]; then
  echo "Definissez UNITY_PATH (chemin de l'executable Unity)." >&2
  exit 2
fi
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "== 1/2 Validation du contenu"
"$ROOT/Tools/validate.sh" || { echo "Build annule : corrigez les erreurs de contenu."; exit 1; }

echo "== 2/2 Build"
export FORGE_BUILD_OUTPUT="${1:-Builds/TheDungeon}"
"$UNITY_PATH" -batchmode -nographics -quit -projectPath "$ROOT" \
  -executeMethod Forge.EditorTools.ForgeCli.Build -logFile -
