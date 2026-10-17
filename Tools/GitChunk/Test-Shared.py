"""Shared command registration, legacy migration, and repository isolation."""
from pathlib import Path
import os
import shutil
import subprocess
import sys
import tempfile

tool = str(Path(sys.argv[1]).resolve())
os.environ['GITCHUNK_SKIP_UPDATE_CHECK'] = '1'

def run(root, *args):
    p = subprocess.run(args, cwd=root, capture_output=True)
    if p.returncode:
        raise RuntimeError(f'{args}: {p.stderr.decode("utf-8", "replace")}')
    return p.stdout.decode('utf-8', 'replace').strip()

with tempfile.TemporaryDirectory(prefix='GitChunk-shared-') as temp:
    base = Path(temp).resolve()
    for name in ('first', 'second'):
        root = base / name
        root.mkdir()
        run(root, 'git', 'init')
        run(root, 'git', 'config', 'user.name', 'Test')
        run(root, 'git', 'config', 'user.email', 'test@example.invalid')
        run(root, tool, 'install', str(root))
        assert run(root, tool, 'status', str(root)) == 'installed'
        shared = run(root, 'git', 'config', '--local', 'gitchunk.executable')
        assert Path(shared).is_file() and not Path(shared).is_relative_to(root)
        assert not list((root / '.gitchunk').glob('GitChunk*'))
        assert not list((root / '.git/gitchunk').glob('GitChunk*'))
        (root / 'note.txt').write_text(name)
        run(root, 'git', 'add', '.')
        run(root, 'git', 'commit', '-m', 'shared installation')
    first, second = base / 'first', base / 'second'
    # Simulate the previous tracked binary and local runtime installation.
    old = first / '.gitchunk' / ('GitChunk.exe' if os.name == 'nt' else 'GitChunk-' + ('linux-' if sys.platform.startswith('linux') else 'osx-') + ('arm64' if os.uname().machine in ('arm64', 'aarch64') else 'x64'))
    shutil.copy2(tool, old)
    run(first, 'git', 'add', '.')
    run(first, 'git', 'commit', '-m', 'old tracked tool')
    metadata = first / '.git/gitchunk'
    metadata.mkdir(exist_ok=True)
    runtime = metadata / old.name
    shutil.copy2(tool, runtime)
    run(first, tool, 'install', str(first))
    assert not old.exists() and not runtime.exists()
    assert not run(first, 'git', 'ls-files', str(old))
    assert len(list(metadata.glob('retired-*/*'))) == 2
    assert run(first, tool, 'status', str(first)) == 'installed'
    run(first, 'git', 'add', '.')
    run(first, 'git', 'commit', '-m', 'shared migration')
    shared = run(second, 'git', 'config', '--local', 'gitchunk.executable')
    run(first, tool, 'uninstall', str(first))
    assert Path(shared).is_file()
    assert run(second, tool, 'status', str(second)) == 'installed'
    run(second, 'git', 'commit', '--allow-empty', '-m', 'other repository still works')
print('PASS: no repository executable copies, shared filters/hooks, legacy migration, independent uninstall')
