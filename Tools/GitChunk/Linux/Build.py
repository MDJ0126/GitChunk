"""Build a self-contained Linux backend and desktop/CLI ZIP, without Actions."""
import os
from pathlib import Path
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parents[3]
rid = sys.argv[1] if len(sys.argv) > 1 else 'linux-x64'
if rid not in ('linux-x64', 'linux-arm64'):
    raise SystemExit('Supported targets: linux-x64, linux-arm64')
version = os.environ.get('GITCHUNK_VERSION', '1.0.0')
out = root / 'publish' / rid
subprocess.run(['dotnet', 'publish', str(root / 'Tools/GitChunk/GitChunk.csproj'), '-c', 'Release', '-r', rid, '-p:Version=' + version, '-o', str(out)], check=True)
files = {'git-chunk': out / 'GitChunk', 'AppIcon.png': root / 'Tools/GitChunk/Assets/AppIcon.png'}
for name in ('GitChunk', 'LinuxApp.py', 'install.sh', 'README.txt'):
    files[name] = Path(__file__).resolve().parent / name
dist = root / 'dist'; dist.mkdir(exist_ok=True)
archive = dist / f'GitChunk-{rid}.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
    for name, path in files.items():
        info = zipfile.ZipInfo(name)
        info.create_system = 3
        info.external_attr = (0o100755 if name in ('git-chunk', 'GitChunk', 'install.sh') else 0o100644) << 16
        info.compress_type = zipfile.ZIP_DEFLATED
        z.writestr(info, path.read_bytes())
print(archive)
