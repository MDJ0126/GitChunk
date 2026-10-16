"""Native Linux LFS conversion and original-data protection."""
import hashlib
import os
from pathlib import Path
import subprocess
import sys
import tempfile

tool = str(Path(sys.argv[1]).resolve())
def run(root, *args, ok=True, env=None):
    p = subprocess.run(args, cwd=root, capture_output=True, env=env)
    if ok and p.returncode:
        raise RuntimeError(str(args) + '\n' + p.stderr.decode(errors='replace'))
    return p
def sha(path):
    with path.open('rb') as f: return hashlib.file_digest(f, 'sha256').digest()
def large(path, size):
    with path.open('wb') as f: f.truncate(size); f.write(b'Linux LFS original')

with tempfile.TemporaryDirectory(prefix='GitChunk-lfs-') as temp:
    base = Path(temp); writer = base/'writer'
    run(base, 'git','init','--bare','remote.git')
    run(base, 'git','init','-b','main',str(writer))
    run(writer,'git','config','user.name','Test'); run(writer,'git','config','user.email','test@example.invalid')
    run(writer,'git','lfs','install','--local'); run(writer,'git','lfs','track','*.bin')
    (writer/'nested').mkdir(); (writer/'nested/.gitattributes').write_text('*.bin filter=lfs diff=lfs merge=lfs -text\n')
    (writer/'nested/other.bin').write_bytes(b'nested original\n')
    (writer/'small.bin').write_bytes(b'small original\n')
    large(writer/'large asset.bin', 50*1024*1024+1); large(writer/'ordinary.apk',60*1024*1024)
    hashes = {n:sha(writer/n) for n in ['large asset.bin','ordinary.apk']}
    run(writer,'git','add','.'); run(writer,'git','commit','-m','LFS originals')
    run(writer,'git','remote','add','origin',str(base/'remote.git')); run(writer,'git','push','-u','origin','main')
    run(base,'git','clone','--branch','main','remote.git','reader',env={**os.environ,'GIT_LFS_SKIP_SMUDGE':'1'})
    reader=base/'reader'; run(reader,'git','config','user.name','Test'); run(reader,'git','config','user.email','test@example.invalid')
    run(reader,'git','lfs','install','--local')
    before=run(reader,'git','rev-parse','HEAD').stdout
    run(reader,tool,'install',str(reader))
    assert run(reader,'git','rev-parse','HEAD').stdout == before
    for name, expected in hashes.items(): assert sha(reader/name) == expected
    assert run(reader,'git','show',':large asset.bin').stdout.startswith(b'gitchunk-pointer-v1\n')
    assert run(reader,'git','show',':ordinary.apk').stdout.startswith(b'gitchunk-pointer-v1\n')
    assert run(reader,'git','show',':small.bin').stdout == b'small original\n'
    assert b'filter: gitchunk' in run(reader,'git','check-attr','filter','--','nested/other.bin').stdout
    assert run(reader,'git','show','HEAD:large asset.bin').stdout.startswith(b'version https://git-lfs')
    run(reader,'git','commit','-m','Converted'); run(reader,'git','push')
    run(base,'git','clone','--branch','main','remote.git','restored')
    restored=base/'restored'; run(restored,tool,'install',str(restored))
    for name, expected in hashes.items(): assert sha(restored/name) == expected
    hook=writer/'.git/hooks/pre-push'; hook.write_text('# custom hook\n')
    index=run(writer,'git','write-tree').stdout
    assert run(writer,tool,'install',str(writer),ok=False).returncode != 0
    assert hook.read_text() == '# custom hook\n' and run(writer,'git','write-tree').stdout == index
    hook.unlink(); run(writer,'git','lfs','install','--local')
    (writer/'.chunkignore').write_text('*.bin\n')
    attrs=(writer/'.gitattributes').read_bytes(); hookdata=hook.read_bytes()
    assert run(writer,tool,'install',str(writer),ok=False).returncode != 0
    assert run(writer,'git','write-tree').stdout == index and (writer/'.gitattributes').read_bytes() == attrs and hook.read_bytes() == hookdata
    (writer/'small.bin').write_text('user modification')
    assert run(writer,tool,'install',str(writer),ok=False).returncode != 0
    assert (writer/'small.bin').read_text() == 'user modification'
print('PASS: Linux LFS remote download, conversion, hashes, history, clone restoration, custom hook, rollback, dirty-tree protection')
