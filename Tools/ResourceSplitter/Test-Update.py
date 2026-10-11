"""Upgrade a real repository created by the previous GitChunk binary."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile

old, new = (str(Path(p).resolve()) for p in sys.argv[1:3])
def run(root, *args):
    p = subprocess.run(args, cwd=root, capture_output=True)
    if p.returncode:
        raise RuntimeError(f'{args}: {p.stderr.decode("utf-8", "replace")}')
    return p.stdout
def sha(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()
with tempfile.TemporaryDirectory(prefix='GitChunk-update-') as temp:
    root = Path(temp)
    run(root, 'git', 'init', '-b', 'main')
    run(root, 'git', 'config', 'user.name', 'Test')
    run(root, 'git', 'config', 'user.email', 'test@example.invalid')
    run(root, old, 'git-install', str(root))
    asset = root / '기존 파일.bin'
    with asset.open('wb') as f:
        f.truncate(60 * 1024 * 1024)
        f.write(b'existing version')
    expected = sha(asset)
    run(root, 'git', 'add', '.')
    run(root, 'git', 'commit', '-m', 'previous version')
    pointer = run(root, 'git', 'show', 'HEAD:기존 파일.bin')
    policy = (root / '.gitchunk/rules.txt').read_bytes()
    run(root, new, 'git-install', str(root))
    assert not (root / '.gitchunk/rules.txt').exists()
    assert (root / '.chunkignore').exists()
    assert any(p.read_bytes() == policy for p in (root / '.git/gitchunk').glob('rules-backup-*.txt'))
    assert run(root, new, 'git-ignore-match', str(root), 'Build/game.zip').strip() == b'include'
    assert not (root / '.gitchunk/presets').exists()
    attributes = (root / '.gitattributes').read_text()
    assert '.gitchunk/presets/**' not in attributes
    assert '.gitchunk/chunks.txt -filter text eol=lf diff' in attributes
    listing = (root / '.gitchunk/chunks.txt').read_text(encoding='utf-8')
    assert '기존 파일.bin' in listing
    for i, chunk in enumerate(json.loads(pointer.split(b'\n', 1)[1])['Chunks'], 1):
        assert f'{i:06d} = {chunk["Oid"]}' in listing
    assert run(root, 'git', 'show', ':기존 파일.bin') == pointer
    assert sha(asset) == expected
    # Historical restoration must work with the updated filter.
    restored = run(root, 'git', 'cat-file', '--filters', 'HEAD:기존 파일.bin')
    assert hashlib.sha256(restored).hexdigest() == expected
    run(root, 'git', 'mv', '기존 파일.bin', '이름 변경.bin')
    run(root, new, 'git-prepare')
    listing = (root / '.gitchunk/chunks.txt').read_text(encoding='utf-8')
    assert '이름 변경.bin' in listing and '기존 파일.bin' not in listing
    run(root, 'git', 'rm', '-f', '이름 변경.bin')
    run(root, new, 'git-prepare')
    assert '000001' not in (root / '.gitchunk/chunks.txt').read_text(encoding='utf-8')
print('PASS: previous binary upgrade, unchanged pointers/rules, historical restore, rename and deletion')
