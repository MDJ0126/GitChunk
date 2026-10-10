# 사용 지시사항

## 실행 조건

- Windows x64용 단일 EXE입니다. .NET 런타임이 포함되어 있어 별도 설치나 DLL 복사가 필요하지 않습니다.

## 분할·복원

- 실행 전 프로젝트를 백업하고 Unity·Unreal 등 에디터를 닫으세요. **분할 상태에서는 에디터를 열지 말고, 먼저 복원하세요.**
- 대상 경로를 입력하지 않으면 현재 작업 폴더를 사용합니다.
- `1` / `split`: **50 MiB(52,428,800바이트) 초과 파일만** 분할합니다. 50 MiB 이하 파일은 유지합니다. 단, 분할 대상에 연결된 `.meta`는 크기와 관계없이 함께 별도 조각으로 보관합니다. 원본과 같은 폴더에 `파일명.part000001`, `파일명.part000002`처럼 조각을 저장합니다. 모든 조각과 원본의 SHA-256을 검증한 뒤 원본을 삭제합니다. 별도 출력 폴더를 만들지 않습니다.
- 복원 정보는 대상 루트의 `ResourceSplitter.manifest.json`에 저장됩니다. 조각과 함께 Git에 보관하세요.
- `2` / `join`: 원래 경로로 복원하고 해시를 검증한 뒤 조각과 새 형식의 manifest를 삭제합니다. 내용이 다른 기존 파일은 덮어쓰지 않습니다.
- 기존 `ResourceParts/manifest.json` 형식도 복원할 수 있습니다. 기존 형식의 조각은 복원 후 유지됩니다.
- 새 형식으로 분할된 상태에서 다시 분할하려면 먼저 복원하세요. 같은 이름의 조각이 이미 있으면 덮어쓰지 않습니다.
- 분할 실패 시 manifest 게시 전이라면 이번 작업에서 만든 조각만 정리하고 원본을 유지합니다. 게시 후 원본 삭제 중 실패하면 일부 원본이 삭제됐을 수 있으나 검증된 조각은 유지됩니다.
- 크기 기준으로 대상을 선택하며 `.gitattributes`의 LFS 추적 여부를 자동 판별하지 않습니다. 작은 파일에 걸린 기존 LFS 규칙은 별도로 조정해야 합니다.
- 기본 조각 크기는 49 MiB이며 압축하지 않습니다. 검증 전까지 원본과 조각을 함께 저장할 공간이 필요합니다.

## Unity 메타 파일

- 분할 대상의 `.meta`는 `파일명.meta.part000001` 등의 별도 조각에 원래 바이트 그대로 저장하고 해시를 검증한 뒤 원래 `.meta`를 삭제합니다. GUID와 임포트 설정은 이 보관 파일에 보존됩니다. 분할하지 않은 파일과 폴더의 `.meta`는 그대로 유지합니다.
- 메타 보관 조각도 manifest와 함께 Git에 저장하세요. 복원 시 원래 `.meta`를 먼저 복원하고 원본을 복원합니다. 다른 내용의 `.meta`가 생성된 경우 `.meta.restore-*`로 보관한 뒤 원래 메타를 되돌립니다. 에디터를 닫고 복원하세요.
- 메타가 없어졌거나 다른 내용으로 재생성된 상황의 복원과 SHA-256 일치를 파일 테스트로 검증했습니다. 실제 Unity 에디터의 실행·재저장 후 씬 참조 복구는 미확인입니다. 분할 상태에서 씬·프리팹을 편집·저장하지 마세요. 메타 보관만으로 이런 변경까지 되돌리지는 않습니다.
- 근거: [Unity 6 공식 문서: 에셋 메타데이터](https://docs.unity3d.com/kr/6000.0/Manual/AssetMetadata.html). 사용 중인 Unity 버전은 확인되지 않았습니다.

## 대상 설정

첫 분할 시 대상 루트에 `ResourceSplitter.settings.json`이 생성됩니다.

- `IncludePaths`: 기본 `["."]`는 전체 프로젝트입니다. Unity는 `["Assets"]`, Unreal은 `["Content", "Plugins"]`처럼 프로젝트 기준 상대 경로를 지정할 수 있습니다.
- `ExcludeDirectories`: 기본 `Library`, `Temp`, `Obj`, `Logs`, `UserSettings`, `DerivedDataCache`, `Intermediate`, `Saved`, `Binaries`, `node_modules`, `.vs`, `.idea`, `.venv`, `venv`, `bin`, `obj`입니다.
- `ExcludeFilePatterns`: 파일명 기준 와일드카드입니다. 기본 `["*.restore-*"]`입니다.
- 일반 탐색에서는 `.meta`, Git/SVN/Hg 메타데이터, 도구 구성 파일·소스, 기존 ResourceParts 출력, 조각 파일, 루트 `.gitignore`·`.gitattributes`는 분할하지 않습니다. 제외 파일은 삭제하지 않습니다. 단, 분할 대상의 연결된 `.meta`는 별도로 함께 처리합니다.
- LFS 포인터, 심볼릭 링크·정션, 경로 탈출, 손상·누락 조각은 거부합니다.

## Git 저장

분할 후 원본 삭제, 조각 추가, manifest와 `.meta`를 함께 커밋하세요. 기존 원격 LFS 데이터와 Git 이력은 자동 삭제하지 않습니다.

대상 프로젝트의 `.gitattributes`에 아래 규칙을 마지막에 추가해 조각에 LFS 필터가 적용되지 않도록 하세요. 기존 규칙은 유지하세요.

```gitattributes
*.part* -filter -diff -merge -text
```

`.gitignore`가 원본 확장자를 무시하는 경우 조각까지 무시하는지 `git check-ignore`로 확인하세요. 필요한 경우 마지막에 `!*.part*`를 추가하세요. 상위 폴더가 무시되는 경우 해당 폴더 규칙도 조정해야 합니다.

## 명령행

```powershell
./ResourceSplitter.exe split
./ResourceSplitter.exe join
# 대상 경로와 조각 크기(바이트)
./ResourceSplitter.exe split C:/path/to/test 1024
./ResourceSplitter.exe join C:/path/to/test
```


이전 버전으로 분할한 데이터는 기존 방식으로 복원됩니다. 메타 보관이 없던 버전에서 이미 삭제된 .meta는 Git 이력이나 백업에서 복구해야 합니다.
