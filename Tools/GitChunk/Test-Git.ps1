$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path (Join-Path $PSScriptRoot '../../GitChunk.exe')).Path
$fixture = Join-Path $PSScriptRoot ('bin/git-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
function G { & git @args; if ($LASTEXITCODE -ne 0) { throw "git failed: $args" } }
function E { & $exe @args | Out-Host; if ($LASTEXITCODE -ne 0) { throw "GitChunk failed: $args" } }
function Identity { G config user.name Test; G config user.email test@example.invalid }
function AssertHash($path,$expected) { if ((Get-FileHash $path).Hash -ne $expected) { throw "Hash mismatch: $path" } }
Push-Location $fixture
try {
    G init --bare remote.git
    G init -b main writer
    Set-Location writer
    Identity
    E git-install (Get-Location).Path
    Set-Content .git/hooks/pre-commit '# custom hook'
    & $exe git-install (Get-Location).Path | Out-Host
    if ($LASTEXITCODE -eq 0 -or (Get-Content .git/hooks/pre-commit -Raw) -notlike '*custom hook*') { throw 'Existing hook was overwritten' }
    Remove-Item .git/hooks/pre-commit
    E git-install (Get-Location).Path
    New-Item -ItemType Directory 'Assets' | Out-Null
    $asset = Join-Path (Get-Location) 'Assets/한글 asset.bin'
    $s = [IO.File]::Create($asset)
    try { $s.SetLength(52428801); $s.WriteByte(42) } finally { $s.Dispose() }
    Set-Content 'Assets/한글 asset.bin.meta' 'guid: original-guid'
    Set-Content note.txt 'ordinary text'
    $boundary = [IO.File]::Create((Join-Path (Get-Location) 'Assets/boundary.bin'))
    try { $boundary.SetLength(52428800) } finally { $boundary.Dispose() }
    $original = (Get-FileHash $asset).Hash
    $meta = (Get-FileHash 'Assets/한글 asset.bin.meta').Hash
    G add .
    # Change the worktree after staging; the commit must retain the staged version.
    $s = [IO.File]::OpenWrite($asset)
    try { $s.WriteByte(77) } finally { $s.Dispose() }
    $edited = (Get-FileHash $asset).Hash
    G commit -m initial
    AssertHash $asset $edited
    $pointer = G show 'HEAD:Assets/한글 asset.bin'
    if (($pointer -join "`n") -notlike 'gitchunk-pointer-v1*') { throw 'Raw original was committed' }
    if ((G cat-file -s 'HEAD:Assets/boundary.bin') -ne '52428800') { throw 'Threshold boundary was split' }
    $parts = @(G ls-tree -r --name-only HEAD | Where-Object { $_ -like '.gitchunk/objects/*' })
    if ($parts.Count -ne 2) { throw 'Required chunks not committed' }
    G remote add origin ../remote.git
    G push -u origin main
    Set-Location ..
    G clone --branch main remote.git reader
    Set-Location reader
    Identity
    E git-install (Get-Location).Path
    # First installation restores the initially cloned pointers automatically.
    AssertHash 'Assets/한글 asset.bin' $original
    AssertHash 'Assets/한글 asset.bin.meta' $meta
    if (G status --porcelain) { throw 'Clone is not clean' }
    Set-Location ../writer
    G add 'Assets/한글 asset.bin'
    G commit -m changed
    G push
    Set-Location ../reader
    G pull --ff-only
    AssertHash 'Assets/한글 asset.bin' $edited
    if (G status --porcelain) { throw 'Pull is not clean' }
    G checkout 'HEAD~1'
    AssertHash 'Assets/한글 asset.bin' $original
    G checkout main
    AssertHash 'Assets/한글 asset.bin' $edited
    G gc --prune=now
    G checkout 'HEAD~1'
    AssertHash 'Assets/한글 asset.bin' $original
    G checkout main
    # Git must refuse to overwrite a modified working asset.
    $s = [IO.File]::OpenWrite((Join-Path (Get-Location) 'Assets/한글 asset.bin'))
    try { $s.WriteByte(88) } finally { $s.Dispose() }
    & git checkout 'HEAD~1'
    if ($LASTEXITCODE -eq 0) { throw 'Local modifications were overwritten' }
    G restore 'Assets/한글 asset.bin'
    AssertHash 'Assets/한글 asset.bin' $edited
    # A rebase pull also restores through the filter.
    Set-Content reader.txt 'local commit'
    G add reader.txt
    G commit -m local
    Set-Location ../writer
    $s = [IO.File]::OpenWrite($asset)
    try { $s.WriteByte(99) } finally { $s.Dispose() }
    $third = (Get-FileHash $asset).Hash
    G add 'Assets/한글 asset.bin'
    G commit -m third
    G push
    Set-Location ../reader
    G pull --rebase
    AssertHash 'Assets/한글 asset.bin' $third
    if (G status --porcelain) { throw 'Rebase is not clean' }
    $s = [IO.File]::OpenWrite((Join-Path (Get-Location) 'Assets/한글 asset.bin'))
    try { $s.WriteByte(111) } finally { $s.Dispose() }
    $stashed = (Get-FileHash 'Assets/한글 asset.bin').Hash
    G stash push -m 'asset change'
    G gc --prune=now
    G stash pop
    AssertHash 'Assets/한글 asset.bin' $stashed
    G restore 'Assets/한글 asset.bin'
    # Corrupt/missing pointers fail rather than being restored as an asset.
    $bad = 'gitchunk-pointer-v1' + "`n" + '{"Size":52428801,"Sha256":"INVALID","Chunks":[]}'
    $bad | & $exe git-smudge | Out-Null
    if ($LASTEXITCODE -eq 0) { throw 'Malformed pointer accepted' }
    # Shrinking a previously split asset removes obsolete chunk entries.
    Set-Location ../writer
    Set-Content $asset 'now small'
    G add 'Assets/한글 asset.bin'
    G commit -m shrink
    $remaining = @(G ls-tree -r --name-only HEAD | Where-Object { $_ -like '.gitchunk/objects/*' })
    if ($remaining.Count -ne 0) { throw 'Obsolete chunks remain in the current tree' }
    G push
    # Commit and push guards catch a raw blob even if filtering was bypassed.
    $s = [IO.File]::Create($asset)
    try { $s.SetLength(104857600) } finally { $s.Dispose() }
    G -c filter.gitchunk.clean=cat add 'Assets/한글 asset.bin'
    & git commit -m 'should fail'
    if ($LASTEXITCODE -eq 0) { throw 'Raw staged blob was accepted' }
    G -c filter.gitchunk.clean=cat commit --no-verify -m 'bypass commit guard'
    & git push
    if ($LASTEXITCODE -eq 0) { throw 'Raw blob bypassed push guard' }
    Write-Host 'PASS: hook collision, boundary, staged snapshot, push/clone, pull, checkout, GC, local edit protection, rebase, stash+GC, malformed pointer, shrink, commit/push guards'
    Write-Host "Fixtures retained: $fixture"
} finally { Pop-Location }
