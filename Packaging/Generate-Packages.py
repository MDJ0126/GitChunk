"""Generate package-manager manifests using the exact release asset hashes."""
import hashlib
import json
from pathlib import Path
import re
import sys

version, asset_dir, output_dir = sys.argv[1:]
if not re.fullmatch(r"\d+\.\d+\.\d+", version):
    raise SystemExit("Use a version like 1.0.0")
assets = Path(asset_dir)
output = Path(output_dir)
base = f"https://github.com/MDJ0126/GitChunk/releases/download/v{version}"
def sha(name):
    with (assets / name).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

win = 'GitChunk-Windows-x64.zip'
(output / 'bucket').mkdir(parents=True, exist_ok=True)
(output / 'bucket/git-chunk.json').write_text(json.dumps({
    'version': version, 'description': 'Git-native chunk storage for large files',
    'homepage': 'https://github.com/MDJ0126/GitChunk',
    'architecture': {'64bit': {'url': f'{base}/{win}', 'hash': sha(win)}},
    'bin': [['git-chunk.exe', 'git-chunk']],
    'shortcuts': [['GitChunk.exe', 'Git Chunk']],
}, indent=2) + '\n', encoding='utf-8')
arm, intel = 'GitChunk-osx-arm64.zip', 'GitChunk-osx-x64.zip'
(output / 'Casks').mkdir(parents=True, exist_ok=True)
(output / 'Casks/git-chunk.rb').write_text(f'''cask "git-chunk" do
  version "{version}"
  arch arm: "arm64", intel: "x64"
  sha256 arm: "{sha(arm)}", intel: "{sha(intel)}"
  url "https://github.com/MDJ0126/GitChunk/releases/download/v#{{version}}/GitChunk-osx-#{{arch}}.zip"
  name "Git Chunk"
  desc "Git-native chunk storage for large files"
  homepage "https://github.com/MDJ0126/GitChunk"
  depends_on macos: ">= :ventura"
  app "GitChunk.app"
  binary "#{{appdir}}/GitChunk.app/Contents/MacOS/GitChunk-cli", target: "git-chunk"
end
''', encoding='utf-8')
(output / 'SHA256SUMS').write_text(''.join(f'{sha(p.name)}  {p.name}\n' for p in sorted(assets.iterdir()) if p.is_file()), encoding='utf-8')
