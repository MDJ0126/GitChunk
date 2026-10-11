# Git Chunk

[English](README.md)

**Git LFS 없이 대용량 리소스를 관리합니다.** Git에는 자동으로 분할 저장하고, 로컬에서는 원본으로 작업합니다. .NET SDK는 필요 없습니다.

## 설치

**Windows** — [설치 파일 다운로드](https://github.com/MDJ0126/GitChunk/releases/latest), 또는 Scoop으로:

```powershell
scoop bucket add gitchunk https://github.com/MDJ0126/GitChunk.git
scoop install gitchunk/git-chunk
```

**Mac** — Homebrew로:

```bash
brew tap MDJ0126/gitchunk https://github.com/MDJ0126/GitChunk
brew install --cask MDJ0126/gitchunk/git-chunk
```

## 사용

업데이트는 프로젝트 루트에서 `git chunk update` 한 번으로 최신 프로그램 다운로드·체크섬 검증·저장소 적용까지 처리합니다. 이후 같은 `git chunk` 명령도 새 버전을 사용합니다. 다른 프로젝트는 `git chunk install`로 새 버전을 적용합니다. `git chunk version`으로 실행 버전과 경로를 확인할 수 있습니다.

`git chunk install`은 현재 실행 버전을 적용하며, 최신 버전이 있으면 업데이트 명령을 안내합니다. 이 기능이 없는 구버전은 최초 한 번 새 설치 파일 또는 패키지 관리자로 프로그램을 업그레이드해야 합니다. 아직 배포하지 않은 로컬 수정본은 공개 릴리스에서 다운로드되지 않습니다.

프로젝트의 Git 저장소 최상위 폴더에서 한 번 실행하세요. 새 PC에서 clone한 뒤에도 필요합니다.

```bash
git chunk install
```

기존 추적 파일이 있다면 설정을 적용하고 커밋하세요.

```bash
git add -u
git add --renormalize .
git add .gitattributes .gitignore .gitchunk
git commit -m "Configure Git Chunk"
```

이후 평소처럼 Git을 사용하시면 됩니다. 앱에서도 프로젝트를 선택하고 **Install Git Chunk**를 눌러 설정할 수 있습니다.

분할 대상은 `.gitchunk/rules.txt` 또는 앱에서 설정합니다. 대상 중 **50 MiB 초과** 파일을 최대 **49 MiB** 조각으로 저장합니다. `.gitchunk/objects`는 삭제하지 마세요.

## 기존 Git LFS 프로젝트 전환

**LFS 자동 전환에는 v1.1.0 이상이 필요합니다.** v1.0.0에는 이 기능이 없습니다. `GitChunk.exe`를 직접 실행할 때는 대상 프로젝트 폴더에서 아래 설치 명령 대신 실행 파일의 실제 경로를 지정하여 `GitChunk.exe install`을 실행합니다.

1. 대상 프로젝트의 최상위 폴더에서 추적 파일의 변경을 먼저 커밋하거나 stash합니다. 원본 다운로드가 필요하면 Git LFS와 원격 접근 권한이 필요합니다.
2. 아래 명령으로 설치합니다. 누락된 원본 확보 → 크기·SHA-256 검증 → LFS 속성과 기본 훅 전환 → 파일 변환·복원 검증 → 스테이징을 자동 처리합니다.

```bash
git chunk install
git diff --cached
git commit -m "Convert current LFS files to Git Chunk"
git push
```

현재 LFS 파일 중 **50 MiB 초과** 파일은 조각으로, 그 이하 파일은 일반 Git 파일로 전환합니다. 대용량 LFS 파일이 분할 규칙에서 제외되어 있거나 사용자 정의 커밋·푸시 훅이 있으면 중단하고 필요한 조정을 안내합니다. 설치는 자동으로 커밋·푸시하지 않으며, 전환 실패 시 기존 인덱스·설정·훅을 복구합니다.

**이후에는 원본 수정 → 평소처럼 add·commit·push**하시면 됩니다. add 시 자동 분할하고 커밋 훅이 필요한 조각을 포함합니다. pull·checkout 시 자동 복원합니다. 새 PC에서는 clone 후 `git chunk install`을 한 번 실행하세요.

**과거 커밋과 서버의 기존 LFS 객체·저장 용량은 자동 정리하지 않습니다.** 과거 LFS 버전을 사용하려면 Git LFS와 해당 원본을 유지해야 합니다.

서버 LFS 용량을 정리하려면 먼저 전환한 프로젝트를 업로드하고 복원을 검증합니다. 최신 버전만 필요하다면 기존 저장소를 백업한 뒤, 검증된 최신 파일 트리로 부모 없는 새 커밋을 만들어 공개 브랜치를 교체할 수 있습니다. 이 경우 해당 브랜치에서 예전 커밋으로 되돌아갈 수 없습니다. 삭제 요청 전 모든 원격 브랜치와 태그에 LFS 참조가 남아 있는지 확인해야 합니다. Git bundle에는 Git 이력만 들어가고 LFS 원본 데이터는 포함되지 않으므로, 필요한 LFS 원본은 별도로 백업합니다.

기존 이력을 제거하기 **전에** `git lfs ls-files --all --long`으로 과거 LFS 객체 ID와 경로를 저장합니다. 이 목록은 확보한 Git 이력이 참조하는 객체이며, 서버 전체 객체 목록이나 실제 청구 용량을 뜻하지 않습니다. 저장소 주소·기존 객체 ID·검증된 새 커밋 ID를 첨부하여 [GitHub 지원팀](https://support.github.com/)에 삭제를 요청합니다. GitChunk는 원격 LFS 객체를 삭제하지 않습니다. [GitHub 공식 문서도 파일 전환 후 서버 객체가 삭제될 때까지 LFS 용량에 포함된다고 안내합니다](https://docs.github.com/en/repositories/working-with-files/managing-large-files/removing-files-from-git-large-file-storage).

v1.1.1에서는 LFS 전환 중 이미 일반 Git으로 추적하던 대용량 파일도 조각화하고, LFS 포인터 검색을 일괄 처리합니다. LFS 리소스와 대용량 빌드 파일이 함께 있는 프로젝트도 처리합니다.

[배포 상세](Packaging/README.md) · [기술 설명](INSTRUCTIONS.md)

`.gitchunk/chunks.txt`는 원본 경로와 조각 ID를 순서대로 보여주는 자동 생성 목록입니다. 설치·업데이트 시 기존 인덱스에서 생성하며 커밋 준비 시 스테이징된 파일 기준으로 갱신하고 함께 스테이징합니다. 복원에는 기존 포인터를 사용합니다. 경로는 JSON 문자열로 표시합니다. 기존 프리셋은 업데이트 시 Git 내부에 백업됩니다.
