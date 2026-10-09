#!/bin/sh
# Rebuild, then write previews under fresh names (tag $1) for viewing.
cd "$(dirname "$0")" && python build.py >/dev/null && python preview.py && python sheetprev.py xtgc xdgc xpgc xdpc wCSO && cd ../_previews && for f in sheet_xtgc sheet_xdgc sheet_xpgc sheet_xdpc sheet_wCSO terrain_map_cmp_1x terrain_map_modern_2x_crop; do cp $f.png "$1_$f.png"; done
