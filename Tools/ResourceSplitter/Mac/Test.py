"""Exercise the shared executable on a native runner; no third-party packages."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

tool = str(Path(sys.argv[1]).resolve())


def run(cwd, *args):
    result = subprocess.run(args, cwd=cwd, capture_output=True, text=True, encoding="utf-8")
    if result.returncode:
        raise RuntimeError(f"{args}: {result.stdout}\n{result.stderr}")
    return result.stdout


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


with tempfile.TemporaryDirectory(prefix="ResourceSplitter-native-") as temp:
    root = Path(temp)
    writer = root / "writer"
    writer.mkdir()
    run(writer, "git", "init", "-b", "main")
    run(writer, "git", "config", "user.name", "Test")
    run(writer, "git", "config", "user.email", "test@example.invalid")
    assets = writer / "Assets"
    assets.mkdir()
    asset = assets / "한글 asset.bin"
    with asset.open("wb") as stream:
        stream.truncate(60 * 1024 * 1024)
        stream.write(b"native cross-platform sample")
    meta = assets / "한글 asset.bin.meta"
    meta.write_bytes(b"guid: retained\n")
    original, meta_hash = digest(asset), digest(meta)
    run(writer, tool, "split", str(writer))
    pointer_bytes = asset.read_bytes()
    assert pointer_bytes.startswith(b"resourcesplitter-pointer-v1\n")
    pointer = json.loads(pointer_bytes.split(b"\n", 1)[1])
    assert len(pointer["Chunks"]) == 2 and digest(meta) == meta_hash
    # Move both files; restoration must follow the pointer's new location.
    moved = assets / "moved.bin"
    asset.rename(moved)
    meta.rename(assets / "moved.bin.meta")
    run(writer, tool, "join", str(writer))
    assert digest(moved) == original
    run(writer, tool, "git-install", str(writer))
    installed = writer / ".resourcesplitter" / (
        "ResourceSplitter.exe" if os.name == "nt" else
        "ResourceSplitter-osx-" + ("arm64" if os.uname().machine == "arm64" else "x64")
    )
    assert installed.exists()
    if os.name != "nt":
        assert os.access(installed, os.X_OK)
        assert os.access(writer / ".git/hooks/pre-commit", os.X_OK)
    run(writer, "git", "add", ".")
    run(writer, "git", "commit", "-m", "native test")
    assert run(writer, "git", "show", "HEAD:Assets/moved.bin").startswith("resourcesplitter-pointer-v1\n")
    remote = root / "remote.git"
    run(root, "git", "init", "--bare", str(remote))
    run(writer, "git", "remote", "add", "origin", str(remote))
    run(writer, "git", "push", "-u", "origin", "main")
    reader = root / "reader"
    run(root, "git", "clone", "--branch", "main", str(remote), str(reader))
    run(reader, tool, "git-install", str(reader))
    assert digest(reader / "Assets/moved.bin") == original
    assert not run(reader, "git", "status", "--porcelain")
    with moved.open("r+b") as stream:
        stream.write(b"changed native sample")
    updated = digest(moved)
    run(writer, "git", "add", "Assets/moved.bin")
    run(writer, "git", "commit", "-m", "update")
    run(writer, "git", "push")
    run(reader, "git", "pull", "--ff-only")
    assert digest(reader / "Assets/moved.bin") == updated
    assert not run(reader, "git", "status", "--porcelain")
print("PASS: native manual split/restore, move, metadata, executable hooks, commit/push/clone/pull")
