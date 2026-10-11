param([string]$Executable = "$PSScriptRoot/../../GitChunk.exe")
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path $Executable).Path
$fixture = "$PSScriptRoot/bin/lfs-test-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory $fixture | Out-Null
function G { $result = & git @args; if ($LASTEXITCODE) { throw "git failed: $args" }; return $result }
function E { & $exe @args | Out-Host; if ($LASTEXITCODE) { throw "GitChunk failed: $args" } }
Push-Location $fixture
try {
    G init --bare remote.git
    G init -b main writer
    Set-Location writer
    G config user.name Test
    G config user.email test@example.invalid
    G lfs install --local
    G lfs track '*.bin'
    New-Item -ItemType Directory nested | Out-Null
    Set-Content nested/.gitattributes '*.bin filter=lfs diff=lfs merge=lfs -text'
    Set-Content nested/other.bin 'nested original'
    $stream = [IO.File]::Create("$PWD/large asset.bin")
    try { $stream.SetLength(52428801); $stream.WriteByte(42) } finally { $stream.Dispose() }
    Set-Content small.bin 'small original'
    $ordinary = [IO.File]::Create("$PWD/ordinary.apk")
    try { $ordinary.SetLength(62914560); $ordinary.WriteByte(99) } finally { $ordinary.Dispose() }
    $ordinaryHash = (Get-FileHash ordinary.apk).Hash
    $hash = (Get-FileHash 'large asset.bin').Hash
    G add .
    G commit -m lfs
    G remote add origin ../remote.git
    G push -u origin main
    # A clone with only pointers exercises downloading from an actual LFS remote.
    Set-Location ..
    $env:GIT_LFS_SKIP_SMUDGE = '1'
    G clone --branch main remote.git reader
    Remove-Item Env:GIT_LFS_SKIP_SMUDGE
    Set-Location reader
    G config user.name Test
    G config user.email test@example.invalid
    G lfs install --local
    $before = G rev-parse HEAD
    E git-install "$PWD"
    if ((G rev-parse HEAD) -ne $before) { throw 'Installation committed unexpectedly' }
    if ((Get-FileHash 'large asset.bin').Hash -ne $hash) { throw 'Original hash changed' }
    if (((G show ':large asset.bin') -join "`n") -notlike 'resourcesplitter-pointer-v1*') { throw 'LFS not converted' }
    if (((G show ':small.bin') -join "`n") -ne 'small original') { throw 'Small LFS not converted' }
    if ((G check-attr filter -- nested/other.bin) -notlike '*: gitchunk') { throw 'Nested LFS attributes not converted' }
    if (((G show 'HEAD:large asset.bin') -join "`n") -notlike 'version https://git-lfs*') { throw 'History changed' }
    if (((G show ':ordinary.apk') -join "`n") -notlike 'resourcesplitter-pointer-v1*') { throw 'Existing ordinary large file not converted' }
    if (@(G ls-files '.gitchunk/objects/*').Count -ne 4) { throw 'Chunks not staged' }
    G commit -m converted
    G push
    Set-Location ..
    G clone --branch main remote.git restored
    Set-Location restored
    E git-install "$PWD"
    if ((Get-FileHash 'large asset.bin').Hash -ne $hash) { throw 'Clone restoration failed' }
    if ((Get-FileHash ordinary.apk).Hash -ne $ordinaryHash) { throw 'Ordinary large file clone restoration failed' }
    # Custom hooks are preserved, and failure does not alter the index.
    Set-Location ../writer
    Set-Content .git/hooks/pre-push '# custom hook'
    $indexBefore = G write-tree
    & $exe git-install "$PWD" | Out-Host
    if ($LASTEXITCODE -eq 0) { throw 'Custom hook accepted' }
    if ((G write-tree) -ne $indexBefore) { throw 'Failure changed index' }
    if ((Get-Content .git/hooks/pre-push -Raw).Trim() -ne '# custom hook') { throw 'Custom hook changed' }
    Remove-Item .git/hooks/pre-push
    G lfs install --local
    New-Item -ItemType Directory .gitchunk -Force | Out-Null
    Set-Content .gitchunk/rules.txt '!*.bin'
    $attrsBefore = Get-Content .gitattributes -Raw
    $hookBefore = Get-Content .git/hooks/pre-push -Raw
    & $exe git-install "$PWD" | Out-Host
    if ($LASTEXITCODE -eq 0) { throw 'Excluded large LFS was accepted' }
    if ((G write-tree) -ne $indexBefore -or (Get-Content .gitattributes -Raw) -ne $attrsBefore -or (Get-Content .git/hooks/pre-push -Raw) -ne $hookBefore) { throw 'Rollback failed' }
    Set-Content small.bin 'user modification'
    & $exe git-install "$PWD" | Out-Host
    if ($LASTEXITCODE -eq 0 -or (Get-Content small.bin).Trim() -ne 'user modification') { throw 'Dirty tree protection failed' }
    Write-Host 'PASS: LFS download, conversion, hashes, small files, history, commit/push/clone, custom hook protection.'
} finally { Remove-Item Env:GIT_LFS_SKIP_SMUDGE -ErrorAction SilentlyContinue; Pop-Location }
exit 0

