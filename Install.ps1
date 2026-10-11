[CmdletBinding()]
param(
    [string]$Repository = (Get-Location).Path,
    [string]$SourcePath,
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'GitChunk/bin'),
    [switch]$NoPathChanges
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Git is required.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 9 SDK is required for installation from source.' }
$repositoryRoot = [IO.Path]::GetFullPath($Repository)
if (-not (Test-Path -LiteralPath $repositoryRoot -PathType Container)) { throw 'Repository folder does not exist.' }
$actual = & git -C $repositoryRoot rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($actual).TrimEnd('\') -ne $repositoryRoot.TrimEnd('\')) { throw 'Select the Git repository root.' }
$work = Join-Path ([IO.Path]::GetTempPath()) ('GitChunk-install-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
if ($SourcePath) {
    $sourceRoot = [IO.Path]::GetFullPath($SourcePath)
} else {
    $sourceRoot = Join-Path $work 'source'
    & git clone --depth 1 https://github.com/MDJ0126/GitChunk.git $sourceRoot
    if ($LASTEXITCODE -ne 0) { throw 'Could not download Git Chunk source.' }
}
$project = Join-Path $sourceRoot 'Tools/ResourceSplitter/ResourceSplitter.csproj'
if (-not (Test-Path -LiteralPath $project)) { throw 'Git Chunk project not found.' }
$rid = if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -eq 'Arm64') { 'win-arm64' } else { 'win-x64' }
$publish = Join-Path $work 'publish'
& dotnet publish $project -c Release -r $rid -p:Headless=true -p:NuGetAudit=false --self-contained true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Check that .NET 9 SDK is installed.' }
$installRoot = [IO.Path]::GetFullPath($InstallDirectory)
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
$command = Join-Path $installRoot 'git-chunk.exe'
Copy-Item -LiteralPath (Join-Path $publish 'GitChunk.exe') -Destination $command -Force
& $command install $repositoryRoot
if ($LASTEXITCODE -ne 0) { throw 'Repository installation failed.' }
if (-not $NoPathChanges) {
    $userPath = [string][Environment]::GetEnvironmentVariable('Path', 'User')
    if ($installRoot -notin @($userPath -split ';')) {
        [Environment]::SetEnvironmentVariable('Path', (($userPath.TrimEnd(';') + ';' + $installRoot).TrimStart(';')), 'User')
    }
    if ($installRoot -notin @($env:Path -split ';')) { $env:Path = $installRoot + ';' + $env:Path }
}
Write-Host "Installed command: $command"
Write-Host 'Use: git chunk install / git chunk status / git chunk rules'
Write-Host 'Open a new terminal to pick up the persistent PATH change.'
Write-Host "Build files retained at: $work"
