#!/bin/bash
# Quick Action de Finder -> UltraArchive ("compress-split"). Andamiaje: ver ../README.md.
# Finder pasa las rutas seleccionadas como argumentos ($@).
exec "$HOME/Library/Application Support/UltraArchive/bin/ultra-archive-shell" compress-split "$@"
