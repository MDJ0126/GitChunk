param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid version' }
$env:GITCHUNK_VERSION = $Version
New-Item dist,publish/package-cli,publish/package-debug -ItemType Directory -Force | Out-Null
Copy-Item publish/cli/GitChunk.exe publish/package-cli/git-chunk.exe -Force
Copy-Item Packaging/WINDOWS-README.txt publish/package-cli/README.txt -Force
Copy-Item publish/windows/GitChunk.exe publish/package-debug/GitChunk-Debug.exe -Force
Copy-Item Packaging/DEBUG-README.txt publish/package-debug/README.txt -Force
Compress-Archive -LiteralPath publish/package-cli/git-chunk.exe,publish/package-cli/README.txt -DestinationPath dist/GitChunk-Windows-x64.zip -Force
Compress-Archive -LiteralPath publish/package-debug/GitChunk-Debug.exe,publish/package-debug/README.txt -DestinationPath dist/GitChunk-Debug-Windows-x64.zip -Force
& 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe' Packaging/GitChunk.iss
if ($LASTEXITCODE) { exit $LASTEXITCODE }
