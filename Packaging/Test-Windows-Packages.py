"""Verify that normal Windows packages and optional debug tools are separate."""
from pathlib import Path
import sys
from zipfile import ZipFile

dist = Path(sys.argv[1])
for name, expected in {
    'GitChunk-Windows-x64.zip': {'git-chunk.exe', 'README.txt'},
    'GitChunk-Debug-Windows-x64.zip': {'GitChunk-Debug.exe', 'README.txt'},
}.items():
    with ZipFile(dist / name) as archive:
        assert set(archive.namelist()) == expected, (name, archive.namelist())
        assert archive.testzip() is None
        assert archive.read(next(n for n in expected if n.endswith('.exe'))).startswith(b'MZ')
print('PASS: CLI-only portable package and separate optional debugging GUI')
