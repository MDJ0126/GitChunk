"""Rules, normal Git blobs, and ResourceSplitter history compatibility."""
import hashlib
import os
from pathlib import Path
import subprocess
import sys
import tempfile

tool = str(Path(sys.argv[1]).resolve())
legacy = str(Path(sys.argv[2]).resolve()) if len(sys.argv) > 2 else None
def run(root, *args, data=None):
    p = subprocess.run(args, cwd=root, input=data, capture_output=True)
    if p.returncode: raise RuntimeError(f"{args}: {p.stderr.decode('utf-8', 'replace')}")
    return p.stdout
def sha(path):
    with path.open('rb') as f: return hashlib.file_digest(f, 'sha256').hexdigest()
def large(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('wb') as f: f.truncate(60 * 1024 * 1024); f.write(b'Git Chunk rules and history')

with tempfile.TemporaryDirectory(prefix='GitChunk-rules-') as temp:
    root = Path(temp).resolve() / 'writer'; root.mkdir()
    run(root, 'git', 'init', '-b', 'main')
    run(root, 'git', 'config', 'user.name', 'Test'); run(root, 'git', 'config', 'user.email', 'test@example.invalid')
    asset = root / 'Assets' / '한글 picture.bmp'; large(asset); expected = sha(asset)
    old_commit = None
    if legacy:
        run(root, legacy, 'git-install', str(root))
        run(root, 'git', 'add', '.'); run(root, 'git', 'commit', '-m', 'legacy ResourceSplitter')
        old_commit = run(root, 'git', 'rev-parse', 'HEAD').strip().decode()
        assert b'.resourcesplitter/objects/' in run(root, 'git', 'ls-tree', '-r', '--name-only', 'HEAD')
        assert run(root, tool, 'git-status', str(root)).strip() == b'legacy'
        (root / '.resourcesplitter/user-note.txt').write_text('retain unknown legacy files', encoding='utf-8')
    (root / '.gitchunk').mkdir(exist_ok=True)
    legacy_policy = b'Assets/**\n!Assets/Small/**\n'
    (root / '.gitchunk/rules.txt').write_bytes(legacy_policy)
    run(root, tool, 'git-install', str(root))
    assert not (root / '.gitchunk/rules.txt').exists()
    assert any(p.read_bytes() == legacy_policy for p in (root / '.git/gitchunk').glob('rules-backup-*.txt'))
    for name, match in [('Assets/large.bin', b'include'), ('Assets/Small/raw.bin', b'exclude'), ('Build/game.zip', b'exclude')]:
        assert run(root, tool, 'git-ignore-match', str(root), name).strip() == match
    if legacy:
        assert not (root / '.resourcesplitter').exists()
        backups = list((root / '.git/gitchunk').glob('legacy-storage-*'))
        assert backups and (backups[0] / 'user-note.txt').read_text() == 'retain unknown legacy files'
    policy = '# 커스텀 Unicode policy\nAssets/Small/**\nOther/**\nBuild/**\n'.encode('utf-8')
    run(root, tool, 'git-rules-save', str(root), data=policy)
    assert not (root / '.gitchunk/presets').exists()
    for name, expected_match in [('Assets/picture.bmp','include'), ('Assets/deep/picture.bmp','include'), ('Assets/Small/picture.bmp','exclude'), ('Unlisted/picture.bmp','include'), ('sound.wav','include'), ('Build/game.zip','exclude'), ('Build/nested/game.apk','exclude'), ('.chunkignore','exclude'), ('.gitchunk/test.wav','exclude'), ('Assets/picture.bmp.meta','exclude')]:
        assert run(root, tool, 'git-rules-match', str(root), name).strip().decode() == expected_match, name
    assert (root / '.chunkignore').read_bytes() == policy
    assert run(root, 'git', 'check-attr', 'filter', '--', '.chunkignore').strip().endswith(b'filter: unset')
    excluded = root / 'Assets' / 'Small' / 'skip.bmp'; large(excluded)
    other = root / 'Other' / 'raw.bin'; large(other)
    run(root, 'git', 'add', '-u')
    run(root, 'git', 'add', '--renormalize', '.')
    run(root, 'git', 'add', '.')
    run(root, 'git', 'commit', '-m', 'Git Chunk custom rules')
    current = run(root, 'git', 'rev-parse', 'HEAD').strip().decode()
    assert run(root, 'git', 'show', 'HEAD:Assets/한글 picture.bmp').startswith(b'gitchunk-pointer-v1\n')
    assert run(root, 'git', 'cat-file', '-s', 'HEAD:Assets/Small/skip.bmp').strip() == b'62914560'
    assert run(root, 'git', 'cat-file', '-s', 'HEAD:Other/raw.bin').strip() == b'62914560'
    files = run(root, 'git', 'ls-tree', '-r', '--name-only', 'HEAD')
    assert b'.gitchunk/objects/' in files and b'.resourcesplitter/objects/' not in files
    assert not run(root, 'git', 'status', '--porcelain')
    import json
    pointer = json.loads(run(root, 'git', 'show', 'HEAD:Assets/한글 picture.bmp').split(b'\n', 1)[1])
    listing = (root / '.gitchunk/chunks.txt').read_text(encoding='utf-8')
    assert 'Assets/한글 picture.bmp' in json.loads(listing.splitlines()[3][1:-1])
    for i, chunk in enumerate(pointer['Chunks'], 1):
        assert f'{i:06d} = {chunk["Oid"]}' in listing
    # Reinstall must rebuild the list from existing pointers without changing them.
    (root / '.gitchunk/chunks.txt').unlink()
    presets = root / '.gitchunk/presets'; presets.mkdir()
    (presets / 'custom.txt').write_bytes(policy)
    run(root, tool, 'git-install', str(root))
    assert (root / '.gitchunk/chunks.txt').read_text(encoding='utf-8') == listing
    assert run(root, 'git', 'show', 'HEAD:Assets/한글 picture.bmp').split(b'\n', 1)[1] == json.dumps(pointer, separators=(',', ':')).encode() + b'\n'
    assert sha(asset) == expected
    assert not presets.exists()
    assert any((p / 'custom.txt').read_bytes() == policy for p in (root / '.git/gitchunk').glob('presets-backup-*'))
    # The active binary lives under .git, surviving a historical checkout.
    if old_commit:
        run(root, 'git', 'checkout', old_commit)
        assert sha(asset) == expected
        assert not (root / '.gitchunk' / 'GitChunk.exe').exists()
        run(root, 'git', 'checkout', current)
        assert sha(asset) == expected
    remote = Path(temp).resolve() / 'remote.git'
    run(root, 'git', 'init', '--bare', str(remote)); run(root, 'git', 'remote', 'add', 'origin', str(remote))
    run(root, 'git', 'push', '-u', 'origin', 'main')
    reader = Path(temp).resolve() / 'reader'
    run(root, 'git', 'clone', '--branch', 'main', str(remote), str(reader))
    run(reader, tool, 'git-install', str(reader))
    assert sha(reader / 'Assets' / '한글 picture.bmp') == expected
    assert (reader / '.gitchunk/chunks.txt').read_text(encoding='utf-8') == listing
    assert not run(reader, 'git', 'status', '--porcelain')
    run(reader, 'git', 'gc', '--prune=now')
    if old_commit:
        # Extract an old asset with checkout conversion, just like a filtered export.
        data = run(reader, 'git', 'cat-file', '--filters', old_commit + ':Assets/한글 picture.bmp')
        assert hashlib.sha256(data).hexdigest() == expected
    # Policy change leaves an existing pointer restorable and stores the next add raw.
    run(reader, tool, 'git-rules-save', str(reader), data=b'**\n')
    run(reader, 'git', 'add', '--renormalize', 'Assets/한글 picture.bmp')
    assert run(reader, 'git', 'cat-file', '-s', ':Assets/한글 picture.bmp').strip() == b'62914560'
    run(reader, tool, 'git-prepare')
    assert '000001' not in (reader / '.gitchunk/chunks.txt').read_text(encoding='utf-8')
print('PASS: rules, chunk list, reinstall, preset backup, normal blobs, clone, policy changes, history and GC')
