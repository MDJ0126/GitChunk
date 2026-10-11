$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path (Join-Path $PSScriptRoot '../../GitChunk.exe')).Path
$fixture = Join-Path $PSScriptRoot ('bin/unified-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'Assets') -Force | Out-Null
function E { & $exe @args | Out-Host; if ($LASTEXITCODE -ne 0) { throw "Tool failed: $args" } }
function G { & git @args; if ($LASTEXITCODE -ne 0) { throw "Git failed: $args" } }
Push-Location $fixture
try {
    G init -b main
    G config user.name Test
    G config user.email test@example.invalid
    $asset = Join-Path $fixture 'Assets/large.bin'
    $s = [IO.File]::Create($asset)
    try { $s.SetLength(62914560); $s.WriteByte(45) } finally { $s.Dispose() }
    Set-Content Assets/large.bin.meta 'guid: retained'
    Set-Content Assets/small.txt 'small'
    $original = (Get-FileHash $asset).Hash
    $meta = (Get-FileHash Assets/large.bin.meta).Hash
    E split $fixture
    $pointer = [IO.File]::ReadAllBytes($asset)
    if ($pointer.Length -gt 1024 -or (Get-FileHash Assets/large.bin.meta).Hash -ne $meta) { throw 'Unexpected manual split representation' }
    $p = (Get-Content $asset -Raw).Substring("resourcesplitter-pointer-v1`n".Length) | ConvertFrom-Json
    $chunkPath = Join-Path $fixture ('.gitchunk/objects/' + $p.Chunks[0].Oid)
    $chunkBytes = [IO.File]::ReadAllBytes($chunkPath)
    [IO.File]::WriteAllText($chunkPath,'corrupt')
    & $exe join $fixture | Out-Host
    if ($LASTEXITCODE -eq 0 -or (Get-Item $asset).Length -ne $pointer.Length) { throw 'Corrupt restore changed pointer' }
    [IO.File]::WriteAllBytes($chunkPath,$chunkBytes)
    # Moving a pointer preserves restoration at its new path.
    New-Item -ItemType Directory Assets/Moved | Out-Null
    Move-Item -LiteralPath $asset -Destination Assets/Moved/large.bin
    Move-Item Assets/large.bin.meta Assets/Moved/large.bin.meta
    E join $fixture
    if ((Get-FileHash Assets/Moved/large.bin).Hash -ne $original) { throw 'Moved pointer did not restore' }
    if (!(Test-Path $chunkPath)) { throw 'Chunks were deleted' }
    E git-install $fixture
    E split $fixture
    G add .
    G commit -m 'manual pointer and chunks'
    $manualPointer = G rev-parse 'HEAD:Assets/Moved/large.bin'
    E join $fixture
    G add Assets/Moved/large.bin
    if ((G rev-parse ':Assets/Moved/large.bin') -ne $manualPointer) { throw 'Manual and automatic representations differ' }
    if (G status --porcelain) { throw 'Unified repository is not clean' }
    Write-Host 'PASS: manual pointer/chunks, corruption protection, moved pointer, metadata, retained chunks, manual commit, same automatic blob'
} finally { Pop-Location }
