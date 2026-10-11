$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path (Join-Path $PSScriptRoot '../../GitChunk.exe')).Path
$fixture = Join-Path $PSScriptRoot ('bin/threshold-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'Assets') -Force | Out-Null
foreach ($entry in @(@('large.bin',52428801L), @('boundary.bin',52428800L), @('below.bin',52428799L), @('empty.bin',0L))) {
    $stream = [IO.File]::Create((Join-Path $fixture ('Assets/' + $entry[0])))
    try { $stream.SetLength($entry[1]) } finally { $stream.Dispose() }
}
Set-Content (Join-Path $fixture 'Assets/small.txt') 'small file'
Set-Content (Join-Path $fixture 'Assets/large.bin.meta') "fileFormatVersion: 2`nguid: 0123456789abcdef0123456789abcdef"
Set-Content (Join-Path $fixture 'Assets.meta') 'folder metadata'
Set-Content (Join-Path $fixture 'Assets/small.txt.meta') 'small asset meta'
$hashes = @{}
Get-ChildItem (Join-Path $fixture 'Assets') -File | ForEach-Object { $hashes[$_.Name] = (Get-FileHash $_.FullName).Hash }
$folderHash = (Get-FileHash (Join-Path $fixture 'Assets.meta')).Hash
Push-Location $fixture
try {
    & $exe legacy-split | Out-Host
    if ($LASTEXITCODE -ne 0 -or (Test-Path Assets/large.bin) -or !(Test-Path Assets/large.bin.part000002) -or (Test-Path ResourceParts)) { throw 'Large file split failed' }
    $manifest = Get-Content ResourceSplitter.manifest.json -Raw | ConvertFrom-Json
    if ($manifest.Files.Count -ne 2 -or $manifest.Files[0].Path -ne 'Assets/large.bin') { throw 'Threshold selection failed' }
    foreach ($name in @('boundary.bin','below.bin','empty.bin','small.txt','small.txt.meta')) {
        if ((Get-FileHash ('Assets/' + $name)).Hash -ne $hashes[$name]) { throw "Small or meta file modified: $name" }
    }
    if ((Test-Path Assets/large.bin.meta) -or (Get-FileHash Assets/large.bin.meta.part000001).Hash -ne $hashes['large.bin.meta']) { throw 'Metadata backup failed' }
    $metaBackup = [IO.File]::ReadAllBytes((Join-Path $fixture 'Assets/large.bin.meta.part000001'))
    Set-Content Assets/large.bin.meta.part000001 'corrupt metadata'
    & $exe join | Out-Host
    if ($LASTEXITCODE -ne 1 -or (Test-Path Assets/large.bin) -or (Test-Path Assets/large.bin.meta)) { throw 'Corrupt metadata not rejected before restore' }
    [IO.File]::WriteAllBytes((Join-Path $fixture 'Assets/large.bin.meta.part000001'), $metaBackup)
    if ((Get-FileHash Assets.meta).Hash -ne $folderHash) { throw 'Folder meta changed' }
    & $exe legacy-split | Out-Host
    if ($LASTEXITCODE -ne 1) { throw 'Repeat split guard failed' }
    $partPath = Join-Path $fixture 'Assets/large.bin.part000002'
    $partBytes = [IO.File]::ReadAllBytes($partPath)
    [IO.File]::WriteAllText($partPath, 'corrupt')
    & $exe join | Out-Host
    if ($LASTEXITCODE -ne 1 -or (Test-Path Assets/large.bin)) { throw 'Corruption check failed' }
    [IO.File]::WriteAllBytes($partPath, $partBytes)
    Set-Content Assets/large.bin.meta 'regenerated GUID metadata'
    $regeneratedHash = (Get-FileHash Assets/large.bin.meta).Hash
    & $exe join | Out-Host
    if ($LASTEXITCODE -ne 0 -or (Get-FileHash Assets/large.bin).Hash -ne $hashes['large.bin'] -or (Test-Path ResourceSplitter.manifest.json) -or (Test-Path Assets/large.bin.part000001)) { throw 'Restore failed' }
    if ((Get-FileHash Assets/large.bin.meta).Hash -ne $hashes['large.bin.meta'] -or (Test-Path Assets/large.bin.meta.part000001)) { throw 'Original metadata not restored' }
    $preserved = Get-ChildItem Assets -Filter 'large.bin.meta.restore-*'
    if ($preserved.Count -ne 1 -or (Get-FileHash $preserved[0].FullName).Hash -ne $regeneratedHash) { throw 'Regenerated metadata was not retained' }
    & $exe legacy-split | Out-Host
    if ($LASTEXITCODE -ne 0 -or (Test-Path Assets/large.bin.meta)) { throw 'Second metadata backup failed' }
    & $exe join | Out-Host
    if ($LASTEXITCODE -ne 0 -or (Get-FileHash Assets/large.bin.meta).Hash -ne $hashes['large.bin.meta']) { throw 'Missing metadata restoration failed' }
    Set-Content Assets/large.bin.part000001 'existing chunk'
    $collisionHash = (Get-FileHash Assets/large.bin.part000001).Hash
    & $exe legacy-split | Out-Host
    if ($LASTEXITCODE -ne 1 -or (Get-FileHash Assets/large.bin.part000001).Hash -ne $collisionHash -or !(Test-Path Assets/large.bin)) { throw 'Collision check failed' }
    Remove-Item Assets/large.bin.part000001
    $settings = Get-Content ResourceSplitter.settings.json -Raw | ConvertFrom-Json
    $settings.IncludePaths = @('Assets/small.txt','Assets/boundary.bin','Assets/below.bin','Assets/empty.bin')
    $settings | ConvertTo-Json -Depth 10 | Set-Content ResourceSplitter.settings.json
    & $exe legacy-split | Out-Host
    if ($LASTEXITCODE -ne 0 -or (Test-Path ResourceSplitter.manifest.json)) { throw 'No-large-files case failed' }
    'PASS: >50 MiB only; exact/below boundary and small/empty files unchanged; meta preserved; inline chunks; hash restore; corruption and collision guards; no eligible files'
} finally { Pop-Location }
exit 0
