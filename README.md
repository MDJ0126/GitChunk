# 리소스 분할 / 복원

`ResourceSplitter.exe`를 실행하고 프로젝트 경로를 입력합니다. Enter를 누르면 실행 파일이 있는 폴더를 사용합니다. Unity, Unreal, 일반 코드 프로젝트 등 파일 형식과 엔진에 관계없이 사용할 수 있습니다. Windows용 .NET 9 Runtime이 필요합니다.

- `1`: 선택된 프로젝트의 **모든 파일**을 재귀적으로 찾아 ResourceParts에 분할합니다. 작은 파일도 조각 하나로 저장합니다. 아래 제외 규칙이 적용됩니다.
- `2`: manifest.json의 원래 경로에 복원합니다. SHA-256을 검사하며, 내용이 다른 기존 파일은 덮어쓰지 않습니다.
- 기본 조각 크기: 49 MiB. 원본 파일은 삭제하지 않습니다. 에디터를 닫고 실행하세요.
- 재분할 시 이전 ResourceParts는 ResourceParts.previous-*로 보존합니다. 확인 후 사용자가 정리해야 합니다. 생성 파일과 원본을 함께 보관할 디스크 공간이 필요합니다.
- 깨진/누락 조각, LFS 포인터, 경로 탈출, 심볼릭 링크/정션을 거부합니다.

## 대상 설정

첫 분할 시 대상 프로젝트에 `ResourceSplitter.settings.json`이 생성됩니다.

- `IncludePaths`: 기본 `["."]`는 전체 프로젝트입니다. Unity 리소스만 원하면 `["Assets"]`, Unreal 리소스만 원하면 `["Content", "Plugins"]`처럼 프로젝트 기준 상대 경로를 지정할 수 있습니다. 파일 경로도 가능합니다.
- `ExcludeDirectories`: 경로 중 해당 이름의 폴더를 제외합니다. 기본은 Library, Temp, Obj, Logs, UserSettings, DerivedDataCache, Intermediate, Saved, Binaries, node_modules, .vs, .idea, .venv, venv, bin, obj입니다. 실제로 필요한 데이터가 이 이름의 폴더에 있다면 목록에서 제거하세요.
- `ExcludeFilePatterns`: 파일명 기준 와일드카드입니다. 기본은 `["*.restore-*"]`입니다.
- Git/SVN/Hg 메타데이터, ResourceParts 및 이전/임시 출력, ResourceSplitter 실행 구성 파일과 도구 소스 폴더는 항상 제외됩니다.
- Unity의 `.meta`, Unreal의 `.uasset`/`.umap`뿐 아니라 이미지, 오디오, 모델, 코드, 설정 파일도 대상에 포함됩니다. 압축하지 않으며 내용을 그대로 분할합니다.

다른 프로젝트에 복사할 때는 루트의 ResourceSplitter.exe, ResourceSplitter.dll, ResourceSplitter.runtimeconfig.json, ResourceSplitter.deps.json 네 파일을 함께 복사하세요. 설정과 출력은 선택된 프로젝트 안에 생성됩니다. 기존 버전의 manifest도 복원할 수 있습니다.

## Git 저장 시 주의

ResourceParts/.gitattributes는 조각에 LFS 필터를 적용하지 않고, 내부 .gitignore는 일반적인 원본 확장자 무시 규칙을 해제합니다. **하지만 이미 추적 중인 원본을 Git에서 제외하거나 기존 원격 LFS 데이터를 삭제하지는 않습니다.** 원본과 조각을 동시에 커밋하지 마세요. Git 전환은 백업 및 이력 정책을 결정한 다음 별도로 수행해야 합니다. 현재 저장소에서는 이전 스냅샷과 staging 폴더를 Git에서 무시합니다. 다른 프로젝트에서도 아래 규칙을 .gitignore에 추가하세요.

```gitignore
ResourceParts.staging-*/
ResourceParts.previous-*/
*.restore-*
```

분할은 총용량을 줄이지 않습니다. 큰 바이너리를 일반 Git에 반복 커밋하면 저장소와 이력이 커집니다. GitHub 일반 파일은 50 MiB 초과 경고, 100 MiB 초과 차단 대상입니다.

## 빌드 / 명령행

```powershell
dotnet build Tools/ResourceSplitter/ResourceSplitter.csproj -c Release -o Tools/ResourceSplitter/dist
# dist의 ResourceSplitter.exe, .dll, .runtimeconfig.json, .deps.json을 함께 복사
./ResourceSplitter.exe split
./ResourceSplitter.exe join
# 별도 테스트 폴더 / 조각 바이트 수
./ResourceSplitter.exe split C:/path/to/test 1024
./ResourceSplitter.exe join C:/path/to/test
```
