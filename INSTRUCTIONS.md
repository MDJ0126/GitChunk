# 사용 지시사항

## 실행 조건

- Windows x64용 단일 EXE입니다. .NET 런타임이 포함되어 있어 별도 설치나 DLL 복사가 필요하지 않습니다.

## 분할·복원

아래 수동 분할·복원 설명은 1번/2번에 해당합니다. 3번 Git 자동화는 원본을 유지하고 별도 저장 형식을 사용합니다.

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

### Git 자동화

- EXE의 3번 또는 `ResourceSplitter.exe git-install <Git 저장소 루트>`로 설치합니다. Windows x64와 PATH에서 실행 가능한 Git이 필요합니다. Git for Windows 2.55.0.windows.4에서 검증했습니다.
- Git clean 필터는 **`git add` 시점의 바이트**를 49 MiB 이하 조각으로 저장하고, 해당 파일 경로에는 크기·SHA-256·조각 Git 객체 ID를 담은 작은 포인터를 스테이징합니다. 50 MiB 이하 파일은 원본 바이트 그대로 저장합니다.
- 커밋 훅은 스테이징된 포인터가 참조하는 조각만 `.resourcesplitter/objects/`에 포함합니다. 원본의 스테이징 이후 수정분이나 다른 미선택 파일은 추가하지 않습니다. 현재 커밋에서 더 이상 참조하지 않는 조각은 인덱스에서 제거합니다. 오래된 커밋에는 필요한 조각이 남습니다.
- Git smudge 필터는 Git 객체 ID로 조각을 읽어 전체 SHA-256을 검증한 후 원본을 출력합니다. 조각 파일의 체크아웃 순서와 관계없이 동작하며, 누락·손상 데이터는 오류로 중단합니다. 설치 명령은 최초 clone에서 받은 포인터도 복원하되, 현재 인덱스와 동일한 포인터 파일만 처리합니다.
- `.meta`는 분할하지 않고 일반 Git 파일로 보관합니다. 자동화에서는 원본과 `.meta`를 삭제하지 않습니다. 실제 Unity·Unreal 에디터 실행은 미확인입니다. 설치·pull·브랜치 전환 시 에디터를 닫고 진행하세요.
- 설치는 루트 `.gitattributes`에 필터 규칙을 추가하고 `.gitignore`에는 조각 캐시 제외 규칙을 추가합니다. `.resourcesplitter/ResourceSplitter.exe`, `.gitattributes`, `.gitignore`를 함께 커밋하세요. 필터 설정과 훅은 로컬 Git 설정이므로 PC/클론마다 설치가 필요합니다.
- 자동화는 Git에 추가한 파일을 대상으로 하며 **수동 모드의 `IncludePaths`/제외 설정은 사용하지 않습니다.** Git에 캐시·빌드 산출물을 넣지 않도록 프로젝트의 `.gitignore`를 유지하세요. 루트 규칙은 해당 범위의 기존 LFS 필터와 text 변환을 덮어씁니다. 하위 `.gitattributes` 또는 `.git/info/attributes`의 규칙이 우선할 수 있으므로 `git check-attr filter -- 경로`로 확인하세요. 파일 diff/merge 규칙은 기존 설정을 유지합니다.
- 기존 `pre-commit`/`pre-push` 훅이 있으면 덮어쓰지 않고 중단합니다. 수동 통합 시 각 훅에서 `.resourcesplitter/ResourceSplitter.exe git-prepare` 및 `git-check-push`를 호출하고 실패를 반환해야 합니다. `git-check-push`에는 Git이 제공하는 표준 입력을 전달하세요.
- 푸시 훅은 푸시하는 전체 도달 가능 이력을 검사합니다. 50 MiB 초과 원본이나 커밋되지 않은 조각을 참조하는 포인터가 있으면 차단합니다. **기존 이력에 대용량 원본이 있으면 새 커밋만 분할해도 푸시가 차단됩니다.** 기존 이력 정리와 LFS 이관은 자동으로 수행하지 않습니다.
- `--no-verify`로 훅을 우회하지 마세요. Git 훅을 실행하지 않는 클라이언트나 Git 객체·인덱스를 직접 조작하는 도구에는 동일한 자동화가 보장되지 않습니다. 검증은 명령행 Git으로 수행했습니다.
- 조각 전체와 임시 파일을 보관할 디스크 공간이 필요합니다. 사용하지 않는 로컬 조각 캐시는 무시된 상태로 남을 수 있습니다. 미커밋·stash 데이터의 조각이 Git GC에서 삭제되지 않도록 로컬 `refs/resourcesplitter/cache/` 참조도 유지합니다. 일반 push에는 이 참조가 전송되지 않지만 `push --mirror`에는 포함될 수 있습니다. 캐시와 참조의 자동 정리는 하지 않으며 전체 저장 용량은 줄어들지 않습니다. 부분 복제/희소 체크아웃, submodule 내부 자동화, 실제 Git GUI 클라이언트 동작은 미확인입니다.
- 일반 pull, rebase pull, 이전 커밋 체크아웃, Git GC, stash 후 GC와 복원, 한글·공백 파일명, SHA-256 일치, 스테이징 이후 수정 유지, 로컬 수정 덮어쓰기 차단은 `Tools/ResourceSplitter/Test-Git.ps1`에서 검증합니다.

### 수동 모드에서 Git 저장

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
