#!/bin/bash
# ---------------------------------------------------------------------------
# uninstall.sh — revierte por completo lo que hace install.sh.
# ---------------------------------------------------------------------------
set -euo pipefail

DEST="$HOME/Library/Application Support/UltraArchive"
SERVICES="$HOME/Library/Services"

echo "Quitando el puente de UltraArchive para Finder…"

for label in \
  "Ultra Archive - Comprimir…" \
  "Ultra Archive - Comprimir aquí" \
  "Ultra Archive - Comprimir como .ZIP" \
  "Ultra Archive - Comprimir como .7Z" \
  "Ultra Archive - Comprimir y dividir…" \
  "Ultra Archive - Extraer aquí" \
  "Ultra Archive - Extraer en subcarpeta" \
  "Ultra Archive - Extraer ficheros…"
do
  rm -rf "$SERVICES/$label.workflow"
done

rm -rf "$DEST"

/System/Library/CoreServices/pbs -flush 2>/dev/null || true
echo "Hecho. Puede que tengas que reiniciar Finder (killall Finder) para que desaparezca del menú."
