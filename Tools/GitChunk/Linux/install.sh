#!/bin/sh
set -eu
base=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
command -v git >/dev/null 2>&1 || { echo 'Git is required.' >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo 'Python 3 is required for desktop installation. The portable git-chunk command can run without it.' >&2; exit 1; }
data="${XDG_DATA_HOME:-$HOME/.local/share}"
bin="${GITCHUNK_BIN_DIR:-$HOME/.local/bin}"
dest="$data/gitchunk"
mkdir -p "$dest" "$bin" "$data/applications" "$data/icons/hicolor/256x256/apps"
if [ "$base" != "$dest" ]; then
    for file in GitChunk git-chunk LinuxApp.py AppIcon.png; do cp "$base/$file" "$dest/$file"; done
fi
chmod +x "$dest/GitChunk" "$dest/git-chunk"
ln -sfn "$dest/git-chunk" "$bin/git-chunk"
ln -sfn "$dest/GitChunk" "$bin/gitchunk"
cp "$dest/AppIcon.png" "$data/icons/hicolor/256x256/apps/gitchunk.png"
# Desktop Exec paths have their own quoting and percent-escape rules.
python3 - "$dest" "$data/applications/gitchunk.desktop" <<'PY'
import sys
from pathlib import Path
exe = str(Path(sys.argv[1]) / 'GitChunk')
exe = exe.replace('\\', '\\\\').replace('"', '\\"').replace('`', '\\`').replace('$', '\\$').replace('%', '%%')
Path(sys.argv[2]).write_text('[Desktop Entry]\nType=Application\nName=Git Chunk\nComment=Keep large files in Git\nExec="' + exe + '"\nIcon=gitchunk\nTerminal=false\nCategories=Development;RevisionControl;\n', encoding='utf-8')
PY
echo "Installed Git Chunk in $dest"
echo "Open Git Chunk from your application menu, or run: $bin/gitchunk"
echo "Add $bin to PATH if needed, then use: git chunk install"
