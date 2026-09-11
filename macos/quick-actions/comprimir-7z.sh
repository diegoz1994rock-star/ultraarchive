#!/bin/bash
# Quick Action de Finder -> UltraArchive ("compress-7z"). Andamiaje: ver ../README.md.
# Finder pasa las rutas seleccionadas como argumentos ($@).
exec "$HOME/Library/Application Support/UltraArchive/bin/ultra-archive-shell" compress-7z "$@"
