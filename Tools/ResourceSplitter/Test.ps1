$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path (Join-Path $PSScriptRoot '../../ResourceSplitter.exe')).Path
$fixture = Join-Path $PSScriptRoot ('bin/inline-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'Assets') -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $fixture 'Assets/image.bin'), [byte[]](0..255))
[IO.File]::WriteAllBytes((Join-Path $fixture 'Assets/empty.bin'), [byte[]]@())
Set-Content (Join-Path $fixture 'Assets/image.bin.meta') "fileFormatVersion: 2`nguid: 0123456789abcdef0123456789abcdef"
Set-Content (Join-Path $fixture 'Assets.meta') 'folder metadata'
$hash = (Get-FileHash (Join-Path $fixture 'Assets/image.bin')).Hash
$metaHash = (Get-FileHash (Join-Path $fixture 'Assets/image.bin.meta')).Hash
$folderMetaHash = (Get-FileHash (Join-Path $fixture 'Assets.meta')).Hash
Push-Location $fixture
try {
    & $exe split . 100
    if ($LASTEXITCODE -ne 0 -or (Test-Path Assets/image.bin) -or !(Test-Path Assets/image.bin.part000003) -or (Test-Path ResourceParts)) { throw 'Inline layout failed' }
    if ((Get-FileHash Assets/image.bin.meta).Hash -ne $metaHash -or (Get-FileHash Assets.meta).Hash -ne $folderMetaHash) { throw 'Meta changed' }
    & $exe split . 100
    if ($LASTEXITCODE -ne 1) { throw 'Repeat split guard failed' }
    $part = [IO.File]::ReadAllBytes((Join-Path $fixture 'Assets/image.bin.part000001'))
    [IO.File]::WriteAllText((Join-Path $fixture 'Assets/image.bin.part000001'), 'corrupt')
    & $exe join
    if ($LASTEXITCODE -ne 1 -or (Test-Path Assets/image.bin) -or (Test-Path Assets/empty.bin)) { throw 'Corruption preflight failed' }
    [IO.File]::WriteAllBytes((Join-Path $fixture 'Assets/image.bin.part000001'), $part)
    & $exe join
    if ($LASTEXITCODE -ne 0 -or (Get-FileHash Assets/image.bin).Hash -ne $hash -or (Get-Item Assets/empty.bin).Length -ne 0 -or (Test-Path ResourceSplitter.manifest.json) -or (Test-Path Assets/image.bin.part000001)) { throw 'Restore or cleanup failed' }
    if ((Get-FileHash Assets/image.bin.meta).Hash -ne $metaHash) { throw 'Restored meta changed' }
    Set-Content Assets/zzpointer.bin "version https://git-lfs.github.com/spec/v1`noid sha256:fake`nsize 123"
    & $exe split . 100
    if ($LASTEXITCODE -ne 1 -or !(Test-Path Assets/image.bin) -or (Test-Path Assets/image.bin.part000001) -or (Test-Path ResourceSplitter.manifest.json)) { throw 'Failed split rollback failed' }
    Remove-Item Assets/zzpointer.bin
    Set-Content Assets/image.bin.part000001 'existing part'
    $collisionHash = (Get-FileHash Assets/image.bin.part000001).Hash
    & $exe split . 100
    if ($LASTEXITCODE -ne 1 -or (Get-FileHash Assets/image.bin.part000001).Hash -ne $collisionHash -or !(Test-Path Assets/image.bin)) { throw 'Collision protection failed' }
    'PASS: inline layout, originals removed, file/folder meta preserved, repeated split guard, corrupt parts, restore hashes, cleanup, failure rollback, collision protection'
} finally { Pop-Location }
