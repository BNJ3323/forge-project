#!/usr/bin/env bash
# Valide le contenu du jeu en mode batch. Code de sortie 1 si erreurs.
# Usage : UNITY_PATH=/chemin/vers/Unity ./Tools/validate.sh
if [ -z "${UNITY_PATH:-}" ]; then
  echo "Definissez UNITY_PATH (chemin de l'executable Unity)." >&2
  exit 2
fi
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOG="$(mktemp)"
"$UNITY_PATH" -batchmode -nographics -quit -projectPath "$ROOT" \
  -executeMethod Forge.EditorTools.ForgeCli.Validate -logFile "$LOG"
CODE=$?
grep -E '^\[(ERREUR|Forge)\]' "$LOG"
rm -f "$LOG"
exit $CODE
