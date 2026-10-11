$ErrorActionPreference = 'Stop'
$projectPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../TestProject'))
New-Item -ItemType Directory -Path $projectPath -Force | Out-Null
if ((Test-Path -LiteralPath (Join-Path $projectPath 'GitChunk.manifest.json')) -or
    (Get-ChildItem -LiteralPath $projectPath -Recurse -File -Filter '*.part*')) {
    throw 'Restore existing split files with GitChunk.exe join before regenerating examples.'
}
$utf8 = [Text.UTF8Encoding]::new($false)
function SamplePath([string]$relative) {
    $path = [IO.Path]::GetFullPath((Join-Path $projectPath $relative))
    if (!$path.StartsWith($projectPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Sample path outside TestProject' }
    $ancestor = $path
    while ($ancestor.Length -ge $projectPath.Length) {
        if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Sample path contains a symbolic link/junction' }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
        if (!$ancestor) { break }
    }
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($path)) -Force | Out-Null
    return $path
}
function Binary([string]$relative, [long]$size) {
    $stream = [IO.File]::Create((SamplePath $relative))
    try {
        $stream.SetLength($size)
        if ($size -gt 0) {
            $header = $utf8.GetBytes("GitChunk synthetic fixture: $relative`n")
            $stream.Write($header, 0, [int][Math]::Min($size, $header.Length))
        }
    } finally { $stream.Dispose() }
}
function TextFile([string]$relative, [string]$content) { [IO.File]::WriteAllText((SamplePath $relative), $content, $utf8) }

Binary 'Assets/Audio/큰파일_60MiB.bin' (60L * 1024 * 1024)
Binary 'Assets/Audio/경계파일_50MiB.bin' (50L * 1024 * 1024)
Binary 'Assets/Audio/경계미만_50MiB_minus1.bin' (50L * 1024 * 1024 - 1)
Binary 'Content/캐릭터/장비/빈파일.bin' 0
TextFile 'Assets/Audio/큰파일_60MiB.bin.meta' "fileFormatVersion: 2`nguid: 0123456789abcdef0123456789abcdef`n"
TextFile 'Assets/Textures/예제.txt' "분할하지 않는 작은 예제 파일입니다.`n"
TextFile 'Assets/Textures/예제.txt.meta' "fileFormatVersion: 2`nguid: fedcba9876543210fedcba9876543210`n"
TextFile 'Data/Nested/More/settings.json' '{"example":true,"value":42}'
TextFile 'Library/제외되는캐시.txt' "기본 분할 설정에서 제외되는 캐시 예제입니다.`n"
$settings = @{
    IncludePaths = @('.')
    ExcludeDirectories = @('Library','Temp','Obj','Logs','UserSettings','DerivedDataCache','Intermediate','Saved','Binaries','node_modules','.vs','.idea','.venv','venv','bin','obj')
    ExcludeFilePatterns = @('*.restore-*')
}
TextFile 'GitChunk.settings.json' ($settings | ConvertTo-Json -Depth 5)
TextFile '테스트방법.txt' @'
이 폴더는 GitChunk용 합성 데이터 예제입니다. 실제 Unity/Unreal 프로젝트나 재생 가능한 에셋이 아닙니다.
현재 상태: 원본 파일 상태. 1번/2번과 3번은 같은 포인터·조각 형식을 사용합니다.

수동 분할·복원 시험 (C:/Projects/GitChunk 폴더에서 실행):
  ./GitChunk.exe split ./TestProject | Out-Host
  ./GitChunk.exe join ./TestProject | Out-Host

GUI에서 C:/Projects/GitChunk/TestProject 폴더를 선택하고 파일 분할 / 원본 복원 카드를 선택한 뒤 실행합니다.
분할 후 60 MiB 파일은 작은 복원 정보로 바뀌며 실제 데이터는 49 MiB + 11 MiB 조각으로 저장됩니다.
Git 루트의 .gitchunk/objects/에 조각이 저장됩니다. .meta는 그대로 유지됩니다.
정확히 50 MiB인 파일, 50 MiB보다 1바이트 작은 파일, 작은 텍스트와 빈 파일은 분할하지 않습니다.
Library 폴더는 분할 대상에서 제외됩니다.
복원 후에는 실제 원본들이 돌아옵니다. 조각은 삭제하지 않고 유지합니다.
expected-sha256.txt에는 생성 직후의 원본 해시가 있습니다.

예제 다시 생성 (분할된 상태에서는 먼저 복원):
  ./Tools/GitChunk/Create-TestProject.ps1
이 명령은 알려진 예제 파일을 새 합성 데이터로 덮어씁니다.

Git 자동화의 별도 통합 테스트:
  ./Tools/GitChunk/Test-Git.ps1
위 테스트는 bin 아래의 독립된 로컬 Git 저장소를 생성하여 commit/push/pull을 검증합니다.
이 TestProject에서 3번 설치를 실행하면 부모 GitChunk 저장소의 하위 폴더이므로 설치가 거부됩니다.
'@
$files = Get-ChildItem -LiteralPath $projectPath -Recurse -File | Where-Object { $_.Name -notin @('expected-sha256.txt','테스트방법.txt','GitChunk.settings.json') }
$lines = foreach ($file in ($files | Sort-Object FullName)) {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash, [IO.Path]::GetRelativePath($projectPath, $file.FullName)
}
TextFile 'expected-sha256.txt' (($lines -join "`n") + "`n")
Write-Host "Example originals created: $projectPath"
