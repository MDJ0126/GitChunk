"""Repository uninstall: restore originals, protect user state, reject unsafe removals."""
import hashlib
import json
import os
import shutil
from pathlib import Path
import subprocess
import sys
import tempfile

tool = str(Path(sys.argv[1]).resolve())
os.environ['GITCHUNK_SKIP_UPDATE_CHECK'] = '1'

def run(root, *args, ok=True):
    p = subprocess.run(args, cwd=root, capture_output=True)
    if ok and p.returncode:
        raise RuntimeError(f'{args}: {p.stderr.decode("utf-8", "replace")}')
    if not ok:
        assert p.returncode, args
    return p.stdout if ok else p.stderr

def init(root):
    root.mkdir()
    run(root, 'git', 'init', '-b', 'main')
    run(root, 'git', 'config', 'user.name', 'Test')
    run(root, 'git', 'config', 'user.email', 'test@example.invalid')
    run(root, tool, 'install', str(root))

def commit(root):
    run(root, 'git', 'add', '.')
    run(root, 'git', 'commit', '-m', 'fixture')

def large(path, size=60):
    with path.open('wb') as f:
        f.truncate(size * 1024 * 1024)
        f.write(b'Uninstall test data')

def sha(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def snapshot(root):
    return {p: (root / p).read_bytes() for p in ['.git/index', '.git/config', '.git/hooks/pre-commit', '.git/hooks/pre-push', '.gitattributes', '.gitignore', '.chunkignore']}

with tempfile.TemporaryDirectory(prefix='GitChunk-uninstall-') as temp:
    base = Path(temp).resolve()
    # Exercise the user's literal command after an executable-only update.
    command_dir = base / 'commands'; command_dir.mkdir()
    command = command_dir / ('git-chunk.exe' if os.name == 'nt' else 'git-chunk')
    shutil.copy2(tool, command)
    os.environ['PATH'] = str(command_dir) + os.pathsep + os.environ['PATH']
    for staged in (False, True):
        root = base / ('updated-staged' if staged else 'updated'); init(root); commit(root)
        runtime = next((root / '.gitchunk').glob('GitChunk*'))
        updated = runtime.read_bytes() + b'updated runtime regression fixture'
        runtime.write_bytes(updated)
        if staged:
            run(root, 'git', 'add', str(runtime))
        user = root / 'user.txt'; user.write_text('original'); run(root, 'git', 'add', 'user.txt'); run(root, 'git', 'commit', '-m', 'user file')
        # Keep both an executable update and an unrelated user edit dirty.
        runtime.write_bytes(updated + b' second update')
        user.write_text('unsaved edit')
        assert b'clean tracked' in run(root, 'git', 'chunk', 'uninstall', ok=False)
        assert user.read_text() == 'unsaved edit' and runtime.exists()
        run(root, 'git', 'restore', 'user.txt')
        if staged:
            run(root, 'git', 'add', str(runtime))
        archived = runtime.read_bytes()
        run(root, 'git', 'chunk', 'uninstall')
        assert not (root / '.gitchunk').exists()
        assert any(p.read_bytes() == archived for p in (root / '.git/gitchunk').glob('uninstall-*/.gitchunk/GitChunk*'))
    # FPSGame-like project: no chunked files. Preserve unrelated settings and custom hooks.
    root = base / 'empty'; init(root)
    (root / '.gitattributes').write_text((root / '.gitattributes').read_text() + '\n*.txt text eol=lf\n*.psd filter=lfs diff=lfs merge=lfs -text\n# GitChunk converted LFS files\n.gitchunk/chunks.txt -filter text eol=lf diff\n.chunkignore -filter text eol=lf diff\n', encoding='utf-8')
    (root / '.gitignore').write_text((root / '.gitignore').read_text() + '\nuser-cache/\n', encoding='utf-8')
    note = root / '.gitchunk/user-note.txt'; note.write_text('preserve this user file')
    commit(root)
    run(root, 'git', 'config', 'alias.user-test', 'status')
    custom = b'#!/bin/sh\necho user hook\n'
    (root / '.git/hooks/pre-push').write_bytes(custom)
    before = snapshot(root)
    # Inject a real Git failure after files/settings have started changing.
    (root / '.git/index.lock').write_bytes(b'held by test')
    assert b'Uninstall failed' in run(root, tool, 'uninstall', str(root), ok=False)
    assert snapshot(root) == before
    assert note.exists()
    (root / '.git/index.lock').unlink()
    # Execute directly from the repository tool to cover Windows executable locks.
    repo_tool = root / '.gitchunk' / ('GitChunk.exe' if os.name == 'nt' else 'GitChunk-' + ('linux-' if sys.platform.startswith('linux') else 'osx-') + ('arm64' if os.uname().machine in ('arm64', 'aarch64') else 'x64'))
    run(root, str(repo_tool), 'uninstall', str(root))
    assert not (root / '.gitchunk').exists() and not (root / '.chunkignore').exists()
    assert (root / '.git/hooks/pre-push').read_bytes() == custom
    assert not (root / '.git/hooks/pre-commit').exists()
    assert run(root, 'git', 'config', 'alias.user-test').strip() == b'status'
    attrs = (root / '.gitattributes').read_text()
    assert '*.txt text eol=lf' in attrs and 'filter=lfs' in attrs
    assert 'filter=gitchunk' not in attrs and '.gitchunk/' not in attrs and '.chunkignore' not in attrs
    assert 'user-cache/' in (root / '.gitignore').read_text()
    assert run(root, tool, 'status', str(root)).strip() == b'not-installed'
    assert any(p.read_text() == 'preserve this user file' for p in (root / '.git/gitchunk').glob('uninstall-*/.gitchunk/user-note.txt'))
    run(root, 'git', 'commit', '-m', 'uninstalled')
    assert not run(root, 'git', 'status', '--porcelain')
    run(root, tool, 'uninstall', str(root))  # repeat removal is harmless
    assert not run(root, 'git', 'status', '--porcelain')
    # Current split files below Git's limit become raw blobs and remain usable without filters.
    root = base / 'chunked'; init(root)
    asset = root / 'asset.bin'; large(asset); expected = sha(asset); commit(root)
    old = run(root, 'git', 'rev-parse', 'HEAD').strip().decode()
    dirty = root / 'asset.bin'; dirty.write_bytes(b'unsaved edit')
    assert b'clean tracked' in run(root, tool, 'uninstall', str(root), ok=False)
    run(root, 'git', 'checkout', '--', 'asset.bin')
    run(root, tool, 'uninstall', str(root))
    assert sha(asset) == expected
    assert int(run(root, 'git', 'cat-file', '-s', ':asset.bin')) == 60 * 1024 * 1024
    assert not run(root, 'git', 'config', '--local', '--get-regexp', r'^filter\.(gitchunk|resourcesplitter)\.', ok=False).strip()  # missing settings is a Git error
    run(root, 'git', 'commit', '-m', 'ordinary Git originals')
    reader = base / 'reader'
    run(root, 'git', 'clone', str(root), str(reader))
    assert sha(reader / 'asset.bin') == expected
    assert not (reader / '.gitchunk').exists()
    # Old chunks remain reachable in history; reinstallation restores historical versions.
    run(root, tool, 'install', str(root))
    restored = run(root, 'git', 'cat-file', '--filters', old + ':asset.bin')
    assert hashlib.sha256(restored).hexdigest() == expected
    # Reject files at Git's 100 MiB boundary without changing settings/index/originals.
    root = base / 'too-large'; init(root); asset = root / 'large.bin'; large(asset, 100); commit(root)
    before = snapshot(root); expected = sha(asset)
    assert b'100 MiB or more' in run(root, tool, 'uninstall', str(root), ok=False)
    assert snapshot(root) == before and sha(asset) == expected, {"changed": [p for p,b in before.items() if snapshot(root)[p] != b], "original_changed": sha(asset) != expected}
    assert (root / '.gitchunk').exists()
print('PASS: uninstall, raw-original clone, historical restore, user hooks/settings, dirty state, 100 MiB refusal, rollback and repeated removal')
