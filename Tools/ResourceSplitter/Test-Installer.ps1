$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('GitChunk installer ' + [Guid]::NewGuid().ToString('N'))
$repo = Join-Path $fixture 'repository'
$bin = Join-Path $fixture 'command bin'
New-Item -ItemType Directory -Path $repo -Force | Out-Null
& git -C $repo init -b main
if ($LASTEXITCODE -ne 0) { throw 'Git init failed.' }
$userPathBefore = [Environment]::GetEnvironmentVariable('Path', 'User')
& (Join-Path $sourceRoot 'Install.ps1') -Repository $repo -SourcePath $sourceRoot -InstallDirectory $bin -NoPathChanges
if ([Environment]::GetEnvironmentVariable('Path', 'User') -ne $userPathBefore) { throw 'Isolated installation changed persistent PATH.' }
$oldPath = $env:Path
try {
    $env:Path = $bin + ';' + $oldPath
    $state = & git -C $repo chunk status
    if ($LASTEXITCODE -ne 0 -or $state -ne 'installed') { throw "External git command failed: $state" }
    $rules = & git -C $repo chunk rules
    if ($LASTEXITCODE -ne 0 -or ($rules -join "`n") -notmatch 'Only matching files') { throw 'Rules CLI failed.' }
    & git -C $repo chunk install
    if ($LASTEXITCODE -ne 0) { throw 'CLI reinstall failed.' }
    Write-Host 'PASS: source installer, headless build, git chunk dispatch, spaced paths, rules, reinstall, unchanged user PATH'
    Write-Host "Fixture: $fixture"
} finally { $env:Path = $oldPath }
