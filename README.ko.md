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

**LFS 자동 전환은 현재 소스·새 로컬 빌드에 구현되어 있으며, 공개 릴리스에는 아직 반영되지 않았습니다.** 전환하려면 새 빌드를 사용하세요. `GitChunk.exe`를 직접 실행할 때는 대상 프로젝트 폴더에서 아래 설치 명령 대신 실행 파일의 실제 경로를 지정하여 `GitChunk.exe install`을 실행합니다.

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

[배포 상세](Packaging/README.md) · [기술 설명](INSTRUCTIONS.md)
