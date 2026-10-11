"""Verify new writes, legacy pointer reads, and legacy settings/manifest recovery."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile

tool = str(Path(sys.argv[1]).resolve())
def run(root, *args, data=None, ok=True):
    p = subprocess.run(args, cwd=root, input=data, capture_output=True)
    if ok and p.returncode:
        raise RuntimeError(p.stderr.decode('utf-8', 'replace'))
    return p

with tempfile.TemporaryDirectory(prefix='GitChunk-identity-') as folder:
    root = Path(folder)
    run(root, 'git', 'init')
    run(root, tool, 'git-install', str(root))
    data = b'identity-test' + bytes(50 * 1024 * 1024)
    pointer = run(root, tool, 'git-clean', 'asset.bin', data=data).stdout
    assert pointer.startswith(b'gitchunk-pointer-v1\n')
    refs = run(root, 'git', 'for-each-ref', '--format=%(refname)').stdout
    assert b'refs/gitchunk/cache/' in refs and b'refs/resourcesplitter/' not in refs
    legacy = pointer.replace(b'gitchunk-pointer-v1\n', b'resourcesplitter-pointer-v1\n', 1)
    for candidate in (pointer, legacy):
        restored = run(root, tool, 'git-smudge', data=candidate).stdout
        assert hashlib.sha256(restored).digest() == hashlib.sha256(data).digest()
    assert run(root, tool, 'git-clean', 'asset.bin', data=legacy).stdout == pointer
    malformed = b'resourcesplitter-pointer-v1\n{"Size":52428801,"Sha256":"INVALID","Chunks":[]}'
    assert run(root, tool, 'git-smudge', data=malformed, ok=False).returncode != 0
    # Both storage and filter names may occur in historical repositories.
    (root / '.gitchunk').rename(root / '.resourcesplitter')
    assert run(root, tool, 'git-smudge', data=legacy).stdout == data
    run(root, tool, 'git-install', str(root))
    assert not (root / '.resourcesplitter').exists()
    assert run(root, tool, 'git-smudge', data=legacy).stdout == data
    # Updating a repository keeps committed originals intact. Renormalization
    # then converts the current index without changing historical pointers.
    run(root, 'git', 'config', 'user.name', 'Test')
    run(root, 'git', 'config', 'user.email', 'test@example.invalid')
    (root / 'asset.bin').write_bytes(data)
    run(root, 'git', 'add', '.')
    oid = run(root, 'git', 'hash-object', '-w', '--stdin', data=legacy).stdout.strip().decode()
    run(root, 'git', 'update-index', '--cacheinfo', '100644,' + oid + ',asset.bin')
    run(root, 'git', 'commit', '-m', 'legacy pointer')
    run(root, tool, 'git-install', str(root))
    assert (root / 'asset.bin').read_bytes() == data
    assert run(root, 'git', 'show', 'HEAD:asset.bin').stdout == legacy
    run(root, 'git', 'add', '--renormalize', '.')
    assert run(root, 'git', 'show', ':asset.bin').stdout == pointer
    run(root, 'git', 'commit', '-m', 'current pointer')
    assert run(root, 'git', 'show', 'HEAD~1:asset.bin').stdout == legacy
    assert run(root, 'git', 'cat-file', '--filters', 'HEAD~1:asset.bin').stdout == data
    manual = root / 'manual'; manual.mkdir()
    (manual / 'asset.bin').write_bytes(data)
    settings = {'IncludePaths':['.'], 'ExcludeDirectories':[], 'ExcludeFilePatterns':[]}
    (manual / 'ResourceSplitter.settings.json').write_text(json.dumps(settings))
    run(root, tool, 'legacy-split', str(manual))
    assert json.loads((manual / 'GitChunk.settings.json').read_text()) == settings
    manifest = manual / 'GitChunk.manifest.json'
    assert manifest.exists()
    manifest.rename(manual / 'ResourceSplitter.manifest.json')
    run(root, tool, 'join', str(manual))
    assert (manual / 'asset.bin').read_bytes() == data
print('PASS: current identity, legacy pointer/storage/settings/manifest compatibility')
