#!/bin/bash
# ---------------------------------------------------------------------------
# install.sh — deja el puente de Finder de UltraArchive en su sitio.
#
# ANDAMIAJE: no habrá menú funcional hasta que exista UltraArchive.app de
# macOS (hoy la app es WPF = solo Windows). Ver README.md.
#
# Qué hace:
#   1. Copia bin/ y quick-actions/*.sh a
#      ~/Library/Application Support/UltraArchive/
#   2. Genera un .workflow por acción en ~/Library/Services/ que llama a
#      esos scripts (equivalente a crearlos a mano en Automator).
#   3. Refresca la base de datos de Servicios de Finder.
#
# Reversible con ./uninstall.sh
# ---------------------------------------------------------------------------
set -euo pipefail

SRC="$(cd "$(dirname "$0")" && pwd)"
DEST="$HOME/Library/Application Support/UltraArchive"
SERVICES="$HOME/Library/Services"

echo "Instalando puente de UltraArchive para Finder…"
mkdir -p "$DEST/bin" "$DEST/quick-actions" "$SERVICES"

install -m 755 "$SRC/bin/ultra-archive-shell" "$DEST/bin/ultra-archive-shell"
install -m 755 "$SRC"/quick-actions/*.sh "$DEST/quick-actions/"

# nombre visible en el menú  |  script  |  tipo de selección que lo muestra
#   sel: "any" archivos/carpetas ; "archive" solo tipos que UltraArchive abre
ACTIONS=(
  "Ultra Archive - Comprimir…|comprimir.sh|any"
  "Ultra Archive - Comprimir aquí|comprimir-aqui.sh|any"
  "Ultra Archive - Comprimir como .ZIP|comprimir-zip.sh|any"
  "Ultra Archive - Comprimir como .7Z|comprimir-7z.sh|any"
  "Ultra Archive - Comprimir y dividir…|comprimir-dividir.sh|any"
  "Ultra Archive - Extraer aquí|extraer-aqui.sh|archive"
  "Ultra Archive - Extraer en subcarpeta|extraer-subcarpeta.sh|archive"
  "Ultra Archive - Extraer ficheros…|extraer-ficheros.sh|archive"
)

for spec in "${ACTIONS[@]}"; do
  IFS='|' read -r label script sel <<< "$spec"
  wf="$SERVICES/$label.workflow"
  mkdir -p "$wf/Contents"

  if [[ "$sel" == "archive" ]]; then
    types='<string>com.pkware.zip-archive</string><string>public.archive</string><string>public.disk-image</string>'
  else
    types='<string>public.item</string>'
  fi

  cat > "$wf/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>NSServices</key>
  <array><dict>
    <key>NSMenuItem</key><dict><key>default</key><string>$label</string></dict>
    <key>NSMessage</key><string>runWorkflowAsService</string>
    <key>NSSendFileTypes</key><array>$types</array>
  </dict></array>
</dict>
</plist>
PLIST

  cat > "$wf/Contents/document.wflow" <<WFLOW
<?xml version="1.0" encoding="UTF-8"?>
<!--
  Quick Action de UltraArchive. Acción única "Ejecutar script de shell"
  (/bin/bash, entrada como argumentos) que delega en el puente instalado.
  Si Automator no importa este .wflow en tu versión de macOS, recréalo a
  mano siguiendo quick-actions/HOW-TO.md — el comando es exactamente:
      "\$HOME/Library/Application Support/UltraArchive/quick-actions/$script" "\$@"
-->
<plist version="1.0"><dict>
  <key>shell</key><string>/bin/bash</string>
  <key>inputMethod</key><string>arguments</string>
  <key>command</key>
  <string>exec "\$HOME/Library/Application Support/UltraArchive/quick-actions/$script" "\$@"</string>
</dict></plist>
WFLOW
done

/System/Library/CoreServices/pbs -flush 2>/dev/null || true
echo "Hecho. Menú: clic derecho en Finder → Acciones rápidas / Servicios → «Ultra Archive - …»."
echo "(Recuerda: requiere UltraArchive.app en /Applications, aún no disponible para macOS.)"
