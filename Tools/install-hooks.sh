#!/usr/bin/env bash
# Installe les hooks Git versionnes du dossier Tools/git-hooks.
ROOT="$(git rev-parse --show-toplevel)" || exit 1
git -C "$ROOT" config core.hooksPath Tools/git-hooks
chmod +x "$ROOT"/Tools/git-hooks/* "$ROOT"/Tools/*.sh
echo "Hooks installes. Pour activer la validation Unity au commit : export UNITY_PATH=/chemin/vers/Unity"
