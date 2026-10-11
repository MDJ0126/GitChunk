# Git Chunk 사용과 제한

GUI는 설치·설치 상태·프로젝트 규칙·프리셋만 제공합니다. 앱에서 최상위 Git 폴더를 선택하세요. 저장소마다, PC마다 설치가 필요합니다. 설치와 규칙 저장은 자동으로 커밋하거나 푸시하지 않습니다.

## 데이터와 Git 작업

- 규칙에 포함된 50 MiB 초과 파일은 Git에 `resourcesplitter-pointer-v1` 호환 복원 정보로 저장합니다. 조각 크기는 49 MiB 이하입니다.
- 실제 원본 데이터는 `.gitchunk/objects/<blob-id>`에 저장되며 훅이 현재 staged 복원 정보에 필요한 조각만 추가합니다. 사용자가 staged한 원본 버전은 유지합니다.
- 50 MiB 이하·규칙에서 제외된 파일은 그대로 저장합니다. 일반 blob 100 MiB 이상은 커밋·푸시 검사에서 거부됩니다.
- smudge는 blob ID로 조각을 읽으므로 체크아웃 순서와 관계없이 복원합니다. 조각 및 원본 SHA-256이 맞지 않으면 실패합니다. 자동 필터 출력에 로그를 섞지 않습니다.
- clean에서 생성한 조각은 기존 `refs/resourcesplitter/cache/` 참조로 보존하여 stash·GC에서도 보호합니다. 일반 push에는 해당 참조가 전송되지 않지만 `push --mirror`에는 포함될 수 있습니다.
- 규칙 변경 후 `git add --renormalize .`로 기존 파일을 반영합니다. 규칙이 달라졌다는 이유로 과거 복원을 막지 않습니다.
- 데이터 폴더와 Git 이력을 임의로 정리하지 마세요. 전체 용량을 줄이지 않고 중복 내용의 조각 ID를 재사용합니다.

## 설치와 기존 데이터

- 설치 시 새 실행 파일은 `.gitchunk`에, PC 전용 활성 실행 파일은 Git 메타데이터 디렉터리 안에 복사합니다. Git 로컬 설정과 관리 훅을 연결합니다.
- 기존 ResourceSplitter 관리 훅은 업데이트합니다. 다른 도구의 pre-commit/pre-push 훅은 덮어쓰지 않고 중단합니다. 수동 통합이 필요합니다.
- 새 clone에서 인덱스와 동일한 복원 정보만 원본으로 복원합니다. 사용자 수정 및 삭제 파일은 그대로 둡니다.
- 이전 pointer-v1·legacy 필터·Git 조각 이력을 지원합니다. 로컬 `.resourcesplitter`의 조각을 검증해 `.gitchunk`로 통합하고 폴더 전체를 Git 내부에 백업합니다. 기존 수동 split/join 및 .part 복구 CLI는 유지합니다.
- 기존 Git 이력의 큰 일반 blob 또는 Git LFS 객체를 이 도구가 자동 마이그레이션하지 않습니다.
- 심볼릭 링크·정션을 통해 데이터/설정을 기록하는 경로는 거부합니다.

## 규칙 파일

`.gitchunk/rules.txt`와 `.gitchunk/presets/*.txt`는 UTF-8 텍스트입니다. 설치는 기본 프리셋이 없을 때만 생성하며 커스텀 파일을 덮어쓰지 않습니다. 프리셋 이름에는 문자·숫자·-·_를 사용합니다.

와일드카드, 경로와 ! 제외 문법은 README의 지원 범위를 따릅니다. .gitignore 전체 문법을 구현한 것은 아닙니다. 분할 규칙은 무시 규칙이 아니므로 대상에서 제외한 파일도 일반 Git에 올라갑니다.

설정·데이터·도구 파일과 .meta는 규칙으로 강제 포함할 수 없습니다. 규칙과 프리셋은 Git에서 텍스트 diff를 사용할 수 있습니다. 원본 대용량 파일의 텍스트 diff는 복원 정보 비교이며 별도 textconv 드라이버는 제공하지 않습니다.

## 검증

- `Test-Git.ps1`: 경계 크기, staged 버전, push/clone/pull/rebase/checkout, stash+GC, 깨진 pointer 및 커밋/푸시 검사.
- `Test-Unified.ps1`: 복구용 기존 수동 CLI와 손상 데이터 복원 거부.
- `Mac/Test.py`: 공통 코드 수동 복원·설치 상태·Git 왕복.
- `Test-Rules.py`: 패턴·제외·프리셋·일반 blob·규칙 변경·clone. 두 번째 인수로 기존 EXE를 주면 ResourceSplitter 이력 마이그레이션과 과거 복원도 검증.
- `GitChunk.exe ui-smoke <임시 Git 저장소>`: 실제 GUI 규칙 저장·프리셋·설치·재실행 상태 검증.

Mac GUI 패키지는 실제 Mac에서 빌드·실행 검증이 필요합니다. Windows EXE 명령행 실행에서 PowerShell 출력·종료 대기가 필요하면 `| Out-Host`를 붙이세요.
