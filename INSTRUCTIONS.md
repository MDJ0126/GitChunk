# 사용 지시사항

## 저장 형식

1번/2번 수동 모드와 3번 Git 자동화는 모두 `resourcesplitter-pointer-v1` 형식을 사용합니다. 원래 파일 경로에는 크기, SHA-256, 조각 ID와 순서를 담은 작은 복원 정보가 저장되며, 실제 바이트는 `.resourcesplitter/objects/`에 저장됩니다.

- 50 MiB(52,428,800바이트) **초과** 파일만 분할합니다. 기본 조각은 49 MiB입니다.
- 1번은 로컬 원본을 복원 정보로 바꿉니다. 2번은 이를 실제 원본으로 되돌립니다. 복원 시 조각과 전체 원본의 SHA-256을 검사하며 조각은 유지합니다.
- 3번은 Git에 추가하는 데이터만 같은 형식으로 바꿉니다. 작업 폴더의 원본은 유지하며 pull/checkout 시 Git 필터가 원본을 복원합니다.
- `.meta`는 변경하지 않습니다. 파일을 옮길 때 연결된 `.meta`도 함께 이동하세요. 복원 정보 자체에는 원본 경로가 고정되어 있지 않아, 같은 조각 저장소 안에서 다른 경로로 이동해도 해당 경로에 복원합니다.
- Git 저장소의 하위 폴더를 수동 분할하면 조각은 **Git 루트**의 `.resourcesplitter/objects/`에 저장됩니다. Git이 없는 독립 폴더에서는 대상 폴더 안에 저장됩니다. 하위 폴더만 복사하면 조각이 빠질 수 있으므로 데이터 저장소도 함께 보관하세요.
- 원본 파일과 `.resourcesplitter`의 복원 정보를 수정하거나 삭제하지 마세요. 분할 중에는 원본과 조각, 임시 파일을 함께 저장할 디스크 공간이 필요합니다. 압축하지 않습니다.
- 수동 분할 상태에서는 Unity/Unreal 에디터를 실행하지 말고 2번으로 복원하세요. 실제 에디터에서의 동작은 미확인입니다.

## 수동 모드

```powershell
./ResourceSplitter.exe split C:/path/to/project
./ResourceSplitter.exe join C:/path/to/project
# 조각 크기 바이트 (최대 49 MiB)
./ResourceSplitter.exe split C:/path/to/project 1024
```

수동 모드 설정은 `ResourceSplitter.settings.json`입니다. IncludePaths는 프로젝트 기준 상대 경로이며 기본은 전체(`.`)입니다. ExcludeDirectories는 Library, Temp, Obj, Logs, UserSettings, DerivedDataCache, Intermediate, Saved, Binaries, node_modules, .vs, .idea, .venv, venv, bin, obj를 기본 제외합니다. ExcludeFilePatterns는 파일명 와일드카드이며 기본은 `*.restore-*`입니다.

Git/SVN/Hg 메타데이터, `.resourcesplitter`, `.meta`, 도구 소스/설정, 기존 조각과 루트 Git 설정 파일은 수동 분할하지 않습니다. 심볼릭 링크·정션 경로는 거부합니다. 새 형식은 연결된 `.meta`를 그대로 유지합니다.

복원은 폴더에서 복원 정보를 찾아 수행하므로 설정 변경이나 파일 이동 후에도 복원할 수 있습니다. 손상·누락 조각은 오류로 중단하며 실패한 파일의 복원 정보는 유지합니다. 여러 파일 중 앞선 파일이 이미 복원된 경우 다시 2번을 실행하면 남은 파일을 처리합니다.

## Git 자동화

Windows x64와 PATH에서 실행 가능한 Git이 필요합니다. Git for Windows 2.55.0.windows.4에서 검증했습니다. EXE의 3번 또는 `git-install <Git 루트>`로 설치합니다.

- `.gitattributes`, `.gitignore`, `.resourcesplitter/ResourceSplitter.exe`를 함께 커밋하세요. 로컬 필터 설정과 훅은 clone마다 설치가 필요합니다. 최초 설치 후 `git add --renormalize .`로 기존 파일도 변환하세요.
- 수동으로 만든 복원 정보도 자동화 설치 후 git add/commit하면 커밋 훅이 조각을 함께 포함합니다. Git SHA-1/SHA-256 객체 형식은 해당 저장소의 형식을 따릅니다. 독립 폴더에서 만든 SHA-1 조각을 SHA-256 저장소로 이관할 때는 먼저 복원 후 다시 분할해야 합니다.
- 수동으로 사용자 지정 크기의 조각을 만든 경우 자동화 기본 49 MiB 분할과 결과가 다를 수 있습니다. 기본 크기를 사용하면 동일한 원본의 복원 정보가 같습니다.
- pre-commit은 스테이징된 복원 정보가 참조하는 조각만 포함합니다. 미선택 원본 수정분은 추가하지 않습니다. 현재 트리에서 불필요한 조각은 인덱스에서 제거하되 로컬 조각 및 과거 커밋은 유지합니다.
- 자동화는 Git에 추가한 파일을 대상으로 하며 수동 모드 IncludePaths/제외 설정은 사용하지 않습니다. 캐시/빌드 산출물은 프로젝트 .gitignore로 제외하세요.
- 루트 필터 규칙은 기존 LFS 필터와 text 변환을 덮어씁니다. 하위 .gitattributes와 .git/info/attributes가 우선할 수 있습니다. `git check-attr filter -- 경로`로 확인하세요.
- 기존 pre-commit/pre-push 훅이 있으면 설치를 중단합니다. 수동 통합 시 `.resourcesplitter/ResourceSplitter.exe git-prepare` 및 `git-check-push`를 호출하고 실패를 반환해야 합니다. 푸시 훅의 표준 입력도 전달하세요.
- 푸시하는 전체 도달 가능 이력에 50 MiB 초과 원본이나 커밋되지 않은 조각을 참조하는 복원 정보가 있으면 푸시를 차단합니다. 기존 이력/LFS 이관은 자동 수행하지 않습니다.
- 자동 필터가 생성한 조각은 미커밋/stash 데이터 보존을 위해 로컬 refs/resourcesplitter/cache/ 참조로 보호합니다. 일반 push에는 전송되지 않지만 push --mirror에는 포함될 수 있습니다. 조각과 참조의 자동 정리는 하지 않습니다.
- --no-verify로 훅을 우회하지 마세요. Git 훅/필터를 사용하지 않는 클라이언트에는 보장되지 않습니다. 실제 Git GUI, 부분 복제/희소 체크아웃, submodule 내부 자동화는 미확인입니다.

## 이전 형식 호환

기존 원본 옆 `.part000001`과 ResourceSplitter.manifest.json(버전 2/3), ResourceParts/manifest.json(버전 1)은 2번으로 복원할 수 있습니다. 새 분할 전에는 먼저 기존 형식을 복원하세요. 기존 버전 2/3은 복원 후 조각과 manifest를 삭제하며, 버전 1은 기존 조각을 유지합니다. 과거 .meta 보관이 없던 데이터의 GUID는 Git 이력이나 백업에서 복구해야 합니다.

기존 형식 생성은 호환성 테스트용 `legacy-split` 명령으로만 남아 있습니다.

## 검증

- Test-Unified.ps1: 수동/자동 동일 포인터, 조각 손상 보호, 파일 이동, .meta 유지, 조각 유지, 수동 결과 커밋.
- Test-Git.ps1: push/clone/pull/rebase/checkout, GC, stash+GC, 스테이징 이후 수정 보호, 로컬 수정 덮어쓰기 차단, 경계 크기, 훅 충돌, 커밋/푸시 원본 차단.
- Test.ps1: 이전 .part/manifest 형식과 .meta 복원 호환성.