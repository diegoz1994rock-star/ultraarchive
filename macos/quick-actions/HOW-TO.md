# Convertir estos scripts en Quick Actions de Finder

> Recordatorio: esto **no funciona todavía** porque no hay `UltraArchive.app` de macOS.
> Ver `../README.md`.

Cada `.sh` de esta carpeta representa **una entrada** del menú contextual. Para cada una:

1. Abre **Automator** → *Nuevo* → **Acción rápida** (*Quick Action*).
2. Arriba: *"El flujo de trabajo recibe"* → **archivos o carpetas** en **Finder**.
3. Arrastra la acción **"Ejecutar script de shell"**.
   - *Shell*: `/bin/bash`
   - *Pasar entrada*: **como argumentos**
   - Pega el contenido del `.sh` correspondiente (o simplemente:
     `~/Library/Application\ Support/UltraArchive/quick-actions/<nombre>.sh "$@"`).
4. Guárdala con el nombre que quieras que aparezca en el menú
   (p. ej. *"Ultra Archive: Comprimir…"*).

`../install.sh` hace los pasos 1–4 por ti generando los `.workflow` en `~/Library/Services`.

## Correspondencia script → entrada de menú

| Script | Entrada de menú | Verbo CLI |
|--------|-----------------|-----------|
| `comprimir.sh`          | Comprimir…                | `--compress` |
| `comprimir-aqui.sh`     | Comprimir aquí            | `--compress-here` |
| `comprimir-zip.sh`      | Comprimir como .ZIP       | `--compress-zip` |
| `comprimir-7z.sh`       | Comprimir como .7Z        | `--compress-7z` |
| `comprimir-dividir.sh`  | Comprimir y dividir…      | `--compress-split` |
| `extraer-aqui.sh`       | Extraer aquí              | `--extract-here-flat` |
| `extraer-subcarpeta.sh` | Extraer en «Nombre»\      | `--extract-here` |
| `extraer-ficheros.sh`   | Extraer ficheros…         | `--extract-to` |

Los de compresión aceptan **selección múltiple**; los de extracción, un archivo cada vez.
Finder decide mostrar unos u otros por el tipo de la selección (Automator: *"solo aparece
para"* imágenes de disco/archivos), igual que hace `AppliesTo` en Windows.
